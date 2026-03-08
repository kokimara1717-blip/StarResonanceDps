using System.Text.Json;
using Microsoft.Extensions.Options;

namespace StarResonanceDpsAnalysis.UpdateHost;

public sealed class UpdateManifestService(
    IOptions<UpdateHostOptions> options,
    IWebHostEnvironment environment,
    IGitHubReleaseCache gitHubReleaseCache)
{
    private readonly UpdateHostOptions _options = options.Value;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<UpdateManifestDto?> GetLatestManifestAsync(bool? includePrerelease = null, CancellationToken cancellationToken = default)
    {
        if (_options.GitHub.Enabled)
        {
            var usePrerelease = includePrerelease ?? _options.GitHub.IncludePrerelease;
            var cachedRelease = gitHubReleaseCache.GetLatest(usePrerelease);
            if (cachedRelease is not null)
            {
                return cachedRelease;
            }
        }

        var manifestPath = ResolvePath(_options.ManifestFilePath);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(manifestPath);
        return await JsonSerializer.DeserializeAsync<UpdateManifestDto>(stream,
            JsonSerializerOptions, cancellationToken);
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path);
    }
}

public sealed class UpdateManifestDto
{
    public string Version { get; set; } = "0.0.0";
    public string DownloadUrl { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ReleaseNotes { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class ReleaseSummaryDto
{
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<ReleaseSummaryItemDto> Releases { get; set; } = [];
}

public sealed class ReleaseSummaryItemDto
{
    public string Version { get; set; } = "0.0.0";
    public string DownloadUrl { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? ReleaseNotes { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public bool IsPrerelease { get; set; }
}
