using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UpsChecklist.Core;
using UpsChecklist.Core.Reporting;

namespace UpsChecklist.Tests;

public sealed class ReportEndpointTests : IClassFixture<ChecklistWebApplicationFactory>
{
    private readonly ChecklistWebApplicationFactory _factory;

    public ReportEndpointTests(ChecklistWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_is_anonymous_and_does_not_expose_configuration()
    {
        var client = _factory.CreateClient();
        var json = await client.GetFromJsonAsync<JsonElement>("/health");
        Assert.Equal("ok", json.GetProperty("status").GetString());
        Assert.False(json.TryGetProperty("password", out _));
        Assert.False(json.TryGetProperty("emailAddress", out _));
    }

    [Fact]
    public async Task Download_without_antiforgery_is_rejected()
    {
        var client = _factory.CreateClient();
        var data = ChecklistMigrationSample.Pd4();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["checklist"] = JsonSerializer.Serialize(data),
        });
        var response = await client.PostAsync("/api/download", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unsupported_content_type_returns_415()
    {
        var client = _factory.CreateClient();
        var token = await AntiforgeryForms.TokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/download")
        {
            Content = new StringContent("{}", Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("RequestVerificationToken", token);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Explicit_json_null_checks_are_rejected_and_malformed_json_is_explained()
    {
        var client = _factory.CreateClient();
        var withNullChecks = """{"positionId":"pd4","date":"2026-09-02","name":"QA","checks":null,"count":"1","remarks":"","sectionRemarks":{},"beforeSortEvidencePhoto":"","evidencePhoto":"","signature":"QA","drawn":""}""";
        using var nullForm = await AntiforgeryForms.ChecklistFormAsync(client, withNullChecks);
        var nullResponse = await client.PostAsync("/api/download", nullForm);
        Assert.Equal(HttpStatusCode.BadRequest, nullResponse.StatusCode);
        Assert.Contains("empty required field", await nullResponse.Content.ReadAsStringAsync());

        using var badForm = await AntiforgeryForms.ChecklistFormAsync(client, "{");
        var badResponse = await client.PostAsync("/api/download", badForm);
        Assert.Equal(HttpStatusCode.BadRequest, badResponse.StatusCode);
        Assert.Contains("JSON", await badResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Chunked_oversize_json_is_rejected_without_content_length()
    {
        var client = _factory.CreateClient();
        var token = await AntiforgeryForms.TokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/download")
        {
            Content = new OversizeChunkedContent(),
        };
        request.Headers.Add("RequestVerificationToken", token);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private sealed class OversizeChunkedContent : HttpContent
    {
        public OversizeChunkedContent() => Headers.ContentType = new("application/json");

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(Encoding.UTF8.GetBytes(new string('x', ChecklistValidator.MaxBodyBytes + 8))).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}

public sealed class EmailHandoverIntegrationTests
{
    [Fact]
    public async Task Fake_transport_acceptance_is_not_called_delivery_confirmed()
    {
        await using var factory = new FakeEmailFactory();
        var client = factory.CreateClient();
        var json = JsonSerializer.Serialize(ChecklistMigrationSample.Pd4());
        using var content = await AntiforgeryForms.ChecklistFormAsync(client, json);
        var first = await client.PostAsync("/api/email-handover", content);
        first.EnsureSuccessStatusCode();
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("deliveryConfirmed").GetBoolean());
        Assert.Contains("not confirmed", body.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, factory.Sender.Calls);

        using var duplicate = await AntiforgeryForms.ChecklistFormAsync(client, json);
        var second = await client.PostAsync("/api/email-handover", duplicate);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, factory.Sender.Calls);
    }

    [Fact]
    public async Task Uncertain_smtp_failure_keeps_duplicate_reservation()
    {
        await using var factory = new FailingEmailFactory();
        var client = factory.CreateClient();
        var json = JsonSerializer.Serialize(ChecklistMigrationSample.Pd4());
        using var content = await AntiforgeryForms.ChecklistFormAsync(client, json);
        var first = await client.PostAsync("/api/email-handover", content);
        Assert.Equal(HttpStatusCode.BadGateway, first.StatusCode);
        Assert.Equal(1, factory.Sender.Calls);

        using var retry = await AntiforgeryForms.ChecklistFormAsync(client, json);
        var second = await client.PostAsync("/api/email-handover", retry);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, factory.Sender.Calls);
    }

    private sealed class FakeEmailFactory : WebApplicationFactory<Program>
    {
        public FakeSender Sender { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                var configured = new HandoverEmailOptions
                {
                    Host = "smtp.test.local",
                    Port = 587,
                    Username = "demo@test.local",
                    Password = "not-a-real-secret",
                    From = "demo@test.local",
                    To = "demo@test.local",
                };
                services.AddSingleton(configured);
                services.AddSingleton<IOptions<HandoverEmailOptions>>(_ => Options.Create(configured));
                services.AddSingleton<HandoverRuntimeInfo>(HandoverRuntimeInfo.ForOptions(configured));
                services.AddSingleton<IHandoverEmailSender>(Sender);
            });
        }
    }

    private sealed class FailingEmailFactory : WebApplicationFactory<Program>
    {
        public FailingSender Sender { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                var configured = new HandoverEmailOptions
                {
                    Host = "smtp.test.local",
                    Port = 587,
                    Username = "demo@test.local",
                    Password = "not-a-real-secret",
                    From = "demo@test.local",
                    To = "demo@test.local",
                };
                services.AddSingleton(configured);
                services.AddSingleton<IOptions<HandoverEmailOptions>>(_ => Options.Create(configured));
                services.AddSingleton<HandoverRuntimeInfo>(HandoverRuntimeInfo.ForOptions(configured));
                services.AddSingleton<IHandoverEmailSender>(Sender);
            });
        }
    }

    public sealed class FakeSender : IHandoverEmailSender
    {
        public int Calls { get; private set; }

        public Task SendAsync(byte[] pdfBytes, string filename, CancellationToken cancellationToken = default)
        {
            Calls++;
            Assert.True(pdfBytes.Length > 4);
            Assert.StartsWith("UPS_", filename);
            return Task.CompletedTask;
        }
    }

    public sealed class FailingSender : IHandoverEmailSender
    {
        public int Calls { get; private set; }

        public Task SendAsync(byte[] pdfBytes, string filename, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("SMTP transport failed after contact.");
        }
    }
}

internal static class ChecklistMigrationSample
{
    public static UpsChecklist.Core.Models.ChecklistSubmission Pd4()
    {
        var position = ChecklistPositions.Get("pd4")!;
        return new()
        {
            PositionId = position.Id,
            Date = "2026-09-02",
            Name = "DEMO QA",
            Checks = ChecklistPositions.AllItems(position).ToDictionary(i => i.Id, _ => false),
            Count = "0",
            Remarks = "",
            SectionRemarks = [],
            Signature = "QA",
        };
    }
}
