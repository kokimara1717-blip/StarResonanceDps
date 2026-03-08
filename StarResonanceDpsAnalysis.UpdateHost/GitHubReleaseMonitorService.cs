using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace StarResonanceDpsAnalysis.UpdateHost;

public interface IGitHubReleaseCache
{
    UpdateManifestDto? GetLatest(bool includePrerelease);
    IReadOnlyList<ReleaseSummaryItemDto> GetSummary();
}

public sealed class GitHubReleaseMonitorService(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<UpdateHostOptions> options,
    ILogger<GitHubReleaseMonitorService> logger)
    : BackgroundService, IGitHubReleaseCache
{
    private const string HttpClientName = "GitHubReleaseMonitor";
    private readonly object _syncRoot = new();
    private UpdateManifestDto? _latestStable;
    private UpdateManifestDto? _latestWithPrerelease;
    private IReadOnlyList<ReleaseSummaryItemDto> _summary = [];

    public UpdateManifestDto? GetLatest(bool includePrerelease)
    {
        lock (_syncRoot)
        {
            return includePrerelease ? _latestWithPrerelease : _latestStable;
        }
    }

    public IReadOnlyList<ReleaseSummaryItemDto> GetSummary()
    {
        lock (_syncRoot)
        {
            return _summary;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshCacheAsync(stoppingToken);

            var intervalSeconds = Math.Max(30, options.CurrentValue.GitHub.PollingIntervalSeconds);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshCacheAsync(CancellationToken cancellationToken)
    {
        var gitHubOptions = options.CurrentValue.GitHub;

        if (!gitHubOptions.Enabled || string.IsNullOrWhiteSpace(gitHubOptions.Owner) || string.IsNullOrWhiteSpace(gitHubOptions.Repository))
        {
            lock (_syncRoot)
            {
                _latestStable = null;
                _latestWithPrerelease = null;
                _summary = [];
            }
            return;
        }

        try
        {
            var httpClient = httpClientFactory.CreateClient(HttpClientName);
            var releasesUrl = $"https://api.github.com/repos/{gitHubOptions.Owner}/{gitHubOptions.Repository}/releases";
            var releases = await httpClient.GetFromJsonAsync<List<GitHubReleaseDto>>(releasesUrl, cancellationToken);

            if (releases is null || releases.Count == 0)
            {
                lock (_syncRoot)
                {
                    _latestStable = null;
                    _latestWithPrerelease = null;
                    _summary = [];
                }
                return;
            }

            var latestStable = releases.FirstOrDefault(r => !r.Draft && !r.Prerelease);
            var latestAny = releases.FirstOrDefault(r => !r.Draft);
            var allReleases = releases
                .Where(r => !r.Draft)
                .Select(MapSummary)
                .ToArray();

            lock (_syncRoot)
            {
                _latestStable = MapRelease(latestStable);
                _latestWithPrerelease = MapRelease(latestAny);
                _summary = allReleases;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to refresh GitHub release cache.");
        }
    }

    private static UpdateManifestDto? MapRelease(GitHubReleaseDto? release)
    {
        if (release is null)
        {
            return null;
        }

        var downloadUrl = release.Assets?.FirstOrDefault()?.BrowserDownloadUrl
                          ?? release.HtmlUrl
                          ?? string.Empty;

        return new UpdateManifestDto
        {
            Version = release.TagName ?? "0.0.0",
            DownloadUrl = downloadUrl,
            Notes = release.Name,
            ReleaseNotes = release.Body,
            PublishedAt = release.PublishedAt
        };
    }

    private static ReleaseSummaryItemDto MapSummary(GitHubReleaseDto release)
    {
        var downloadUrl = release.Assets?.FirstOrDefault()?.BrowserDownloadUrl
                          ?? release.HtmlUrl
                          ?? string.Empty;

        return new ReleaseSummaryItemDto
        {
            Version = release.TagName ?? "0.0.0",
            DownloadUrl = downloadUrl,
            Name = release.Name,
            ReleaseNotes = release.Body,
            PublishedAt = release.PublishedAt,
            IsPrerelease = release.Prerelease
        };
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAssetDto>? Assets { get; set; }
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }

    public static void ConfigureHttpClient(HttpClient httpClient)
    {
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StarResonanceDpsAnalysis-UpdateHost", "1.0"));
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }
}
