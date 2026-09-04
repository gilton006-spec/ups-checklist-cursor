using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UpsChecklist.Tests;

[Collection("MaintenanceEnv")]
public sealed class MaintenanceMiddlewareTests
{
    [Fact]
    public async Task Flag_file_returns_503_with_no_store_except_health()
    {
        var flag = Path.Combine(Path.GetTempPath(), $"ups-maint-{Guid.NewGuid():N}.flag");
        File.WriteAllText(flag, "on");
        var previous = Environment.GetEnvironmentVariable("APP_MAINTENANCE_FLAG_FILE");
        Environment.SetEnvironmentVariable("APP_MAINTENANCE_FLAG_FILE", flag);
        try
        {
            await using var factory = new ChecklistWebApplicationFactory();
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var health = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);

            var home = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, home.StatusCode);
            Assert.Contains("no-store", home.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Temporarily offline", await home.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            var image = await client.GetAsync("/workbook/image1.png");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, image.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("APP_MAINTENANCE_FLAG_FILE", previous);
            File.Delete(flag);
        }
    }
}

[CollectionDefinition("MaintenanceEnv", DisableParallelization = true)]
public sealed class MaintenanceEnvCollection;
