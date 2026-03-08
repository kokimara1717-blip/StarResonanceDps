using StarResonanceDpsAnalysis.UpdateHost;
using StarResonanceDpsAnalysis.UpdateHost.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<UpdateHostOptions>(builder.Configuration.GetSection(UpdateHostOptions.SectionName));
builder.Services.AddHttpClient("GitHubReleaseMonitor", GitHubReleaseMonitorService.ConfigureHttpClient);
builder.Services.AddSingleton<GitHubReleaseMonitorService>();
builder.Services.AddSingleton<IGitHubReleaseCache>(sp => sp.GetRequiredService<GitHubReleaseMonitorService>());
builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<GitHubReleaseMonitorService>());
builder.Services.AddSingleton<UpdateManifestService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/api/update/latest", async (bool? includePrerelease, UpdateManifestService updater, CancellationToken cancellationToken) =>
{
    var manifest = await updater.GetLatestManifestAsync(includePrerelease, cancellationToken);
    return manifest is null
        ? Results.NotFound(new { message = "Update manifest not found." })
        : Results.Ok(manifest);
});

app.MapGet("/summary.json", (IGitHubReleaseCache gitHubReleaseCache) =>
{
    return Results.Ok(new ReleaseSummaryDto
    {
        GeneratedAt = DateTimeOffset.UtcNow,
        Releases = gitHubReleaseCache.GetSummary()
    });
});

app.MapRazorComponents<App>();

app.Run();
