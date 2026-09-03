using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using UpsChecklist.Core;
using UpsChecklist.Core.Pdf;
using UpsChecklist.Core.Reporting;
using UpsChecklist.Core.Scanners;
using UpsChecklist.Web.Http;
using UpsChecklist.Web.Infrastructure;
using UpsChecklist.Web.Services;
using UpsChecklist.Web;

var builder = WebApplication.CreateBuilder(args);
var localSettingsPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.Development.local.json");
if (builder.Environment.IsDevelopment() && File.Exists(localSettingsPath))
    builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);

var emailOptions = builder.Configuration
    .GetSection(HandoverEmailOptions.SectionName)
    .Get<HandoverEmailOptions>() ?? new HandoverEmailOptions();
var runtimeInfo = HandoverRuntimeInfo.ForOptions(emailOptions);

builder.Services.Configure<SiteAccessOptions>(builder.Configuration.GetSection(SiteAccessOptions.SectionName));
builder.Services.PostConfigure<SiteAccessOptions>(options =>
{
    options.Password = (options.Password ?? "").Trim().ToLowerInvariant();
});
builder.Services.Configure<HandoverEmailOptions>(builder.Configuration.GetSection(HandoverEmailOptions.SectionName));

if (builder.Environment.IsDevelopment() && !emailOptions.IsConfigured)
{
    var temp = await EtherealEmailProvisioner.ProvisionAsync();
    emailOptions = temp.Options;
    runtimeInfo = new HandoverRuntimeInfo
    {
        DisplayAddress = temp.InboxAddress,
        DevInboxUrl = temp.InboxUrl,
        UsesTempInbox = true,
    };
    Console.WriteLine("Development test inbox ready at Ethereal. Credentials are not printed.");
}

builder.Services
    .AddAuthentication(SiteAccessOptions.CookieScheme)
    .AddCookie(SiteAccessOptions.CookieScheme, options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.Cookie.Name = SiteAccessOptions.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("reports", httpContext =>
    {
        var environment = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (environment.IsEnvironment("Testing"))
            return RateLimitPartition.GetNoLimiter("test");

        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Too many report requests from this instance. Wait a minute and try again.", token);
    };
});

builder.Services.AddSingleton<IChecklistPdfCreator, ChecklistPdfCreator>();
builder.Services.AddSingleton<IScannerPdfCreator, ScannerPdfCreator>();
var scannerOptions = builder.Configuration.GetSection("ScannerLists").Get<ScannerListOptions>() ?? new ScannerListOptions();
if (scannerOptions.ReturnInstruction is null || scannerOptions.ReturnInstruction.Length > 300
    || scannerOptions.ReturnInstruction.Any(char.IsControl))
    throw new InvalidOperationException("ScannerLists:ReturnInstruction must be a single line of at most 300 characters.");
builder.Services.AddSingleton(scannerOptions);
builder.Services.AddSingleton(emailOptions);
builder.Services.AddSingleton(runtimeInfo);
builder.Services.AddSingleton<IOptions<HandoverEmailOptions>>(_ => Options.Create(emailOptions));
builder.Services.AddSingleton<IHandoverEmailSender, MailKitHandoverEmailSender>();
builder.Services.AddSingleton<ReportSubmissionGuard>();
builder.Services.AddSingleton<ChecklistReportService>();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueLengthLimit = ChecklistValidator.MaxBodyBytes;
    options.MultipartBodyLengthLimit = ChecklistValidator.MaxBodyBytes;
});

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLY_APP_NAME")))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLY_APP_NAME")))
    app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT"))
    || app.Configuration["Kestrel:Endpoints:Https:Url"] is not null)
    app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<SiteAccessMiddleware>();
app.UseAuthorization();
app.MapRazorPages();
app.MapChecklistEndpoints();
app.MapScannerEndpoints();

app.Run();

public partial class Program;
