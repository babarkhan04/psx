using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Radzen;
using Serilog;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Domain.Authorization;
using SmartPOS.Infrastructure;
using SmartPOS.Infrastructure.Data;
using SmartPOS.Infrastructure.ExceptionHandling;
using SmartPOS.Infrastructure.Identity;
using SmartPOS.Shared.Contracts;
using SmartPOS.Web.Components;
using SmartPOS.Web.Components.Account;
using SmartPOS.Web.Services;

// SmartPOS is deployed in Pakistan — force PKR currency and Pakistani number
// formatting app-wide so every ToString("C") renders as "Rs 1,234.00".
var pakistanCulture = new CultureInfo("en-PK")
{
    NumberFormat =
    {
        CurrencySymbol = "Rs ",
        CurrencyDecimalDigits = 2
    }
};
CultureInfo.DefaultThreadCurrentCulture = pakistanCulture;
CultureInfo.DefaultThreadCurrentUICulture = pakistanCulture;
CultureInfo.CurrentCulture = pakistanCulture;
CultureInfo.CurrentUICulture = pakistanCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "SmartPOS")
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
        .WriteTo.File(
            path: "logs/smartpos-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            formatProvider: CultureInfo.InvariantCulture);
});

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
}).AddIdentityCookies();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
        options.MaxBufferedUnacknowledgedRenderBatches = 10;
    });

builder.Services.AddRadzenComponents();
builder.Services.AddSingleton<CustomerDisplayStateService>();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
builder.Services.AddScoped<CircuitHandler, BlazorCircuitExceptionHandler>();
builder.Services.AddScoped<IErrorBoundaryLogger, BlazorErrorBoundaryLogger>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Auth endpoints handle cookie sign-in/out via static requests
// (cannot mutate the response from inside an interactive circuit).
app.MapPost("/auth/login", async (HttpContext context,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) =>
{
    var form = await context.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var user = await userManager.FindByEmailAsync(email);
    if (user is null || !user.IsActive)
    {
        return Results.Redirect("/login?error=invalid");
    }

    var result = await signInManager.PasswordSignInAsync(
        user.UserName!, password, isPersistent: true, lockoutOnFailure: true);

    if (!result.Succeeded)
    {
        return Results.Redirect(result.IsLockedOut ? "/login?error=locked" : "/login?error=invalid");
    }

    user.LastLoginOn = DateTime.UtcNow;
    await userManager.UpdateAsync(user);

    var target = string.IsNullOrWhiteSpace(returnUrl) || !Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
        ? "/dashboard"
        : returnUrl;
    return Results.Redirect(target);
}).DisableAntiforgery();

app.MapGet("/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

var catalogManage = Permissions.Catalog.ProductsManage;

app.MapGet("/api/products/export", async (IProductImportExportService exportService) =>
{
    var bytes = await exportService.ExportAsync();
    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"products-{DateTime.UtcNow:yyyyMMdd}.xlsx");
}).RequireAuthorization(catalogManage);

app.MapGet("/api/reports/export", async (
    Guid? storeId,
    DateTime? fromUtc,
    DateTime? toUtc,
    IReportService reportService) =>
{
    var bytes = await reportService.ExportReportsWorkbookAsync(new ReportQueryDto
    {
        StoreId = storeId,
        FromUtc = fromUtc,
        ToUtc = toUtc
    });

    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"smartpos-reports-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
}).RequireAuthorization(Permissions.Insights.ReportsView);

app.MapPost("/api/products/import", async (HttpRequest request, IProductImportExportService importService) =>
{
    var file = request.Form.Files.FirstOrDefault();
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "No file uploaded." });
    }

    await using var stream = file.OpenReadStream();
    var result = await importService.ImportAsync(stream);
    return result.Succeeded
        ? Results.Ok(result.Data)
        : Results.BadRequest(new { message = result.Error, data = result.Data });
}).RequireAuthorization(catalogManage).DisableAntiforgery();

app.MapPost("/api/products/upload-image", async (
    HttpRequest request,
    IWebHostEnvironment env,
    ICurrentUserService currentUser) =>
{
    var file = request.Form.Files.FirstOrDefault();
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "No image selected." });
    }

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext is not ".jpg" and not ".jpeg" and not ".png" and not ".webp" and not ".gif")
    {
        return Results.BadRequest(new { message = "Only JPG, PNG, WebP, or GIF images are allowed." });
    }

    if (file.Length > 5 * 1024 * 1024)
    {
        return Results.BadRequest(new { message = "Image must be 5 MB or smaller." });
    }

    var folder = Path.Combine(env.WebRootPath, "uploads", "products");
    Directory.CreateDirectory(folder);

    var fileName = $"{Guid.NewGuid():N}{ext}";
    var fullPath = Path.Combine(folder, fileName);
    await using (var fs = File.Create(fullPath))
    {
        await file.CopyToAsync(fs);
    }

    return Results.Ok(new { url = $"/uploads/products/{fileName}" });
}).RequireAuthorization(catalogManage).DisableAntiforgery();

app.Run();
