using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Properties;

namespace StarResonanceDpsAnalysis.WPF.Services;

public sealed class AppUpdateService : IAutoUpdateService
{
    private const string GithubApiUrl = "https://api.github.com/repos/{0}/releases";
    private const string SelfHostedLatestPath = "/api/update/latest";
    private const string SelfHostedSummaryPath = "/summary.json";

    private readonly IConfigManager _configManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMessageDialogService _messageDialogService;
    private readonly IApplicationControlService _applicationControlService;
    private readonly LocalizationManager _localization;
    private readonly ILogger<AppUpdateService> _logger;

    public AppUpdateService(IConfigManager configManager,
        IHttpClientFactory httpClientFactory,
        IMessageDialogService messageDialogService,
        IApplicationControlService applicationControlService,
        LocalizationManager localization,
        ILogger<AppUpdateService> logger)
    {
        _configManager = configManager;
        _httpClientFactory = httpClientFactory;
        _messageDialogService = messageDialogService;
        _applicationControlService = applicationControlService;
        _localization = localization;
        _logger = logger;
    }

    public async Task CheckForUpdatesAsync(bool silentIfNoUpdate = true, CancellationToken cancellationToken = default)
    {
        var config = _configManager.CurrentConfig;
        if (!config.EnableAutoUpdate)
        {
            return;
        }

        try
        {
            var latest = await GetLatestReleaseAsync(config, cancellationToken);
            if (latest == null)
            {
                return;
            }

            var currentVersion = GetCurrentVersion();
            if (latest.Version <= currentVersion)
            {
                if (!silentIfNoUpdate)
                {
                    var noUpdateTitle = _localization.GetString(ResourcesKeys.Update_NoUpdate_Title, defaultValue: "No Update");
                    var noUpdateMessage = _localization.GetString(ResourcesKeys.Update_NoUpdate_Message,
                        defaultValue: "You are already using the latest version.");
                    await ShowDialogAsync(noUpdateTitle, noUpdateMessage);
                }

                return;
            }

            var title = _localization.GetString(ResourcesKeys.Update_Available_Title, defaultValue: "Update Available");
            var messageTemplate = _localization.GetString(ResourcesKeys.Update_Available_Message,
                defaultValue: "Current version: {0}\nLatest version: {1}\n\nOpen download page now?");

            var message = string.Format(CultureInfo.CurrentCulture, messageTemplate, currentVersion, latest.DisplayVersion);
            if (!string.IsNullOrWhiteSpace(latest.Notes))
            {
                var notesLabel = _localization.GetString(ResourcesKeys.Update_Available_Notes, defaultValue: "Release notes");
                message = $"{message}\n\n{notesLabel}:\n{latest.Notes}";
            }

            var confirm = await ShowDialogAsync(title, message);
            if (confirm == true)
            {
                var canInPlaceUpdate = Uri.TryCreate(latest.DownloadUrl, UriKind.Absolute, out var downloadUri) &&
                                       downloadUri.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

                var updated = await TryDownloadAndApplyUpdateAsync(latest.DownloadUrl, cancellationToken);
                if (updated)
                {
                    return;
                }

                if (canInPlaceUpdate)
                {
                    var failedTitle = _localization.GetString(ResourcesKeys.Update_Available_Title,
                        defaultValue: "Update Failed");
                    var failedMessage = "Automatic update failed after download. Please update manually from the download page.";
                    await ShowDialogAsync(failedTitle, failedMessage);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = latest.DownloadUrl,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto update check failed");
        }
    }

    private async Task<UpdateReleaseInfo?> GetLatestReleaseAsync(AppConfig config, CancellationToken cancellationToken)
    {
        return config.UpdateSource switch
        {
            UpdateSourceType.GitHub => await GetLatestFromGithubAsync(config, cancellationToken),
            UpdateSourceType.SelfHosted => await GetLatestFromSelfHostAsync(config, cancellationToken),
            _ => null
        };
    }

    private async Task<UpdateReleaseInfo?> GetLatestFromGithubAsync(AppConfig config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.GithubRepository))
        {
            return null;
        }

        using var client = CreateClient(config.UpdateRequestTimeoutSeconds);
        var baseUrl = string.Format(CultureInfo.InvariantCulture, GithubApiUrl, config.GithubRepository.Trim());
        var url = config.GithubIncludePrerelease ? $"{baseUrl}?per_page=10" : $"{baseUrl}/latest";

        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var releaseElement = config.GithubIncludePrerelease
            ? document.RootElement.EnumerateArray().FirstOrDefault(r => !GetBoolProperty(r, "draft"))
            : document.RootElement;

        if (releaseElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        var tag = GetStringProperty(releaseElement, "tag_name");
        if (!TryParseVersion(tag, out var version))
        {
            return null;
        }

        var assetContains = config.GithubAssetNameContains.Trim();
        var downloadUrl = GetGithubAssetUrl(releaseElement, assetContains);
        downloadUrl ??= GetStringProperty(releaseElement, "html_url");

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return null;
        }

        var notes = GetStringProperty(releaseElement, "body");
        return new UpdateReleaseInfo(version, NormalizeVersionLabel(tag, version), downloadUrl, TrimNotes(notes));
    }

    private async Task<UpdateReleaseInfo?> GetLatestFromSelfHostAsync(AppConfig config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.SelfHostedManifestUrl))
        {
            return null;
        }

        var includePrerelease = config.GithubIncludePrerelease;
        if (IsSummaryCandidate(config.SelfHostedManifestUrl))
        {
            using var summaryClient = CreateClient(config.UpdateRequestTimeoutSeconds);
            return await TryGetLatestFromSelfHostedSummaryAsync(summaryClient, config.SelfHostedManifestUrl,
                includePrerelease, cancellationToken);
        }

        var selfHostedUrl = BuildSelfHostedLatestUrl(config.SelfHostedManifestUrl, includePrerelease);

        using var client = CreateClient(config.UpdateRequestTimeoutSeconds);
        using var response = await client.GetAsync(selfHostedUrl, cancellationToken);

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = document.RootElement;
        var versionText = GetStringProperty(root, "version");
        if (!TryParseVersion(versionText, out var version))
        {
            return null;
        }

        var downloadUrl = GetStringProperty(root, "downloadUrl") ?? GetStringProperty(root, "url");
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return null;
        }

        var notes = GetStringProperty(root, "notes") ?? GetStringProperty(root, "releaseNotes");
        return new UpdateReleaseInfo(version, NormalizeVersionLabel(versionText, version), downloadUrl, TrimNotes(notes));
    }

    private static string BuildSelfHostedLatestUrl(string selfHostedManifestUrl, bool includePrerelease)
    {
        var trimmedUrl = selfHostedManifestUrl.Trim();
        if (trimmedUrl.EndsWith(SelfHostedSummaryPath, StringComparison.OrdinalIgnoreCase))
        {
            return trimmedUrl;
        }

        var latestUrl = trimmedUrl;
        if (Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri))
        {
            if (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/")
            {
                latestUrl = new Uri(uri, SelfHostedLatestPath).ToString();
            }
        }
        else if (!trimmedUrl.Contains("/", StringComparison.Ordinal))
        {
            latestUrl = $"{trimmedUrl.TrimEnd('/')}{SelfHostedLatestPath}";
        }

        return AppendIncludePrereleaseQuery(latestUrl, includePrerelease);
    }

    private static bool IsSummaryCandidate(string selfHostedManifestUrl)
    {
        return selfHostedManifestUrl.Trim().EndsWith(SelfHostedSummaryPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string AppendIncludePrereleaseQuery(string url, bool includePrerelease)
    {
        if (!includePrerelease)
        {
            return url;
        }

        const string key = "includePrerelease=true";
        if (url.Contains("includePrerelease=", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return url.Contains('?', StringComparison.Ordinal) ? $"{url}&{key}" : $"{url}?{key}";
    }

    private static async Task<UpdateReleaseInfo?> TryGetLatestFromSelfHostedSummaryAsync(HttpClient client,
        string selfHostedSummaryUrl,
        bool includePrerelease,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(selfHostedSummaryUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("releases", out var releasesElement) ||
            releasesElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var candidates = new List<UpdateReleaseInfo>();
        foreach (var item in releasesElement.EnumerateArray())
        {
            var isPrerelease = GetBoolProperty(item, "isPrerelease");
            if (!includePrerelease && isPrerelease)
            {
                continue;
            }

            var versionText = GetStringProperty(item, "version");
            if (!TryParseVersion(versionText, out var version))
            {
                continue;
            }

            var downloadUrl = GetStringProperty(item, "downloadUrl") ?? GetStringProperty(item, "url");
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                continue;
            }

            var notes = GetStringProperty(item, "releaseNotes") ?? GetStringProperty(item, "name");
            candidates.Add(new UpdateReleaseInfo(version, NormalizeVersionLabel(versionText, version), downloadUrl,
                TrimNotes(notes)));
        }

        return candidates
            .OrderByDescending(x => x.Version)
            .FirstOrDefault();
    }

    private static string? GetGithubAssetUrl(JsonElement releaseElement, string? assetNameContains)
    {
        if (!releaseElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? fallback = null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = GetStringProperty(asset, "name");
            var url = GetStringProperty(asset, "browser_download_url");
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            fallback ??= url;
            if (string.IsNullOrWhiteSpace(assetNameContains) ||
                name?.Contains(assetNameContains, StringComparison.OrdinalIgnoreCase) == true)
            {
                return url;
            }
        }

        return fallback;
    }

    private static Version GetCurrentVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version ?? new Version(0, 0, 0, 0);
    }

    private HttpClient CreateClient(int timeoutSeconds)
    {
        var client = _httpClientFactory.CreateClient(nameof(AppUpdateService));
        client.Timeout = TimeSpan.FromSeconds(Math.Max(2, timeoutSeconds));
        if (!client.DefaultRequestHeaders.UserAgent.Any())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/27.0.1453.94 Safari/537.36");
        }

        return client;
    }

    private async Task<bool?> ShowDialogAsync(string title, string message)
    {
        var app = Application.Current;
        if (app?.Dispatcher == null)
        {
            return _messageDialogService.Show(title, message);
        }

        return await app.Dispatcher.InvokeAsync(() => _messageDialogService.Show(title, message));
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static bool GetBoolProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind == JsonValueKind.True ||
               (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var result) && result);
    }

    private static bool TryParseVersion(string? value, [NotNullWhen(true)] out Version? version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().TrimStart('v', 'V');
        var cleaned = new string(normalized.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()).Trim('.');
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return false;
        }

        return Version.TryParse(cleaned, out version);
    }

    private static string NormalizeVersionLabel(string? raw, Version parsed)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw.StartsWith('v') || raw.StartsWith('V') ? raw : $"v{raw}";
        }

        return $"v{parsed}";
    }

    private static string? TrimNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var ret = notes.Trim();
        return ret.Length > 300 ? $"{ret[..300]}..." : ret;
    }

    private async Task<bool> TryDownloadAndApplyUpdateAsync(string downloadUrl, CancellationToken cancellationToken)
    {
        DownloadProgressIndicator? progressIndicator = null;
        try
        {
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var packageUri) ||
                !packageUri.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "StarResonanceDpsUpdate", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var packagePath = Path.Combine(tempRoot, "update.zip");
            var extractPath = Path.Combine(tempRoot, "extract");

            progressIndicator = await ShowDownloadProgressIndicatorAsync();

            using (var client = CreateClient(_configManager.CurrentConfig.UpdateRequestTimeoutSeconds))
            {
                await DownloadFileWithProgressAsync(client, packageUri, packagePath,
                    (downloaded, total) => progressIndicator?.Report(downloaded, total), cancellationToken);
            }

            ZipFile.ExtractToDirectory(packagePath, extractPath, true);
            var sourceDirectory = ResolveExtractedRootDirectory(extractPath);

            var installDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var entryPath = Assembly.GetEntryAssembly()?.Location;
            var executableFileName = string.IsNullOrWhiteSpace(entryPath)
                ? "StarResonanceDpsAnalysis.WPF.exe"
                : Path.GetFileName(entryPath);
            var currentProcessId = Environment.ProcessId;

            var updaterScript = Path.Combine(tempRoot, "apply-update.cmd");
            await File.WriteAllTextAsync(updaterScript,
                BuildApplyUpdateScript(sourceDirectory, installDirectory, executableFileName, currentProcessId), Encoding.UTF8,
                cancellationToken);

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{updaterScript}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            _applicationControlService.Shutdown();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to perform in-place zip update, falling back to browser download.");
            return false;
        }
        finally
        {
            if (progressIndicator != null)
            {
                await progressIndicator.CloseAsync();
            }
        }
    }

    private static async Task DownloadFileWithProgressAsync(HttpClient client,
        Uri packageUri,
        string packagePath,
        Action<long, long?> onProgress,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920,
            useAsync: true);

        var buffer = new byte[81920];
        long totalRead = 0;

        while (true)
        {
            var bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead <= 0)
            {
                break;
            }

            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;
            onProgress(totalRead, totalBytes);
        }
    }

    private static async Task<DownloadProgressIndicator?> ShowDownloadProgressIndicatorAsync()
    {
        var app = Application.Current;
        if (app?.Dispatcher == null)
        {
            return null;
        }

        return await app.Dispatcher.InvokeAsync(() =>
        {
            var progressText = new TextBlock
            {
                Margin = new Thickness(0, 0, 0, 8),
                Text = "Downloading update package..."
            };

            var progressBar = new ProgressBar
            {
                Height = 16,
                Minimum = 0,
                Maximum = 100,
                IsIndeterminate = true
            };

            var content = new StackPanel
            {
                Margin = new Thickness(14)
            };
            content.Children.Add(progressText);
            content.Children.Add(progressBar);

            var window = new Window
            {
                Title = "Updating",
                Width = 420,
                Height = 120,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false,
                Topmost = true,
                Content = content
            };

            if (app.MainWindow is { IsVisible: true })
            {
                window.Owner = app.MainWindow;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            window.Show();
            return new DownloadProgressIndicator(window, progressBar, progressText);
        });
    }

    private static string ResolveExtractedRootDirectory(string extractPath)
    {
        var directories = Directory.GetDirectories(extractPath);
        var files = Directory.GetFiles(extractPath);
        return directories.Length == 1 && files.Length == 0 ? directories[0] : extractPath;
    }

    private static string BuildApplyUpdateScript(string sourceDirectory, string installDirectory, string executableFileName, int processId)
    {
        return $"""
                @echo off
                setlocal
                set "SRC={sourceDirectory}"
                set "DST={installDirectory}"
                set "EXE={executableFileName}"
                set "PID={processId}"

                :wait_process_exit
                tasklist /FI "PID eq %PID%" | find "%PID%" >nul
                if not errorlevel 1 (
                    timeout /t 1 /nobreak >nul
                    goto wait_process_exit
                )

                robocopy "%SRC%" "%DST%" /E /R:2 /W:1 /NFL /NDL /NJH /NJS /NP /XF appsettings.json PlayerInfoCache.dat >nul

                if exist "%DST%\%EXE%" start "" "%DST%\%EXE%"

                endlocal
                """;
    }

    private sealed class DownloadProgressIndicator(Window window, ProgressBar progressBar, TextBlock progressText)
    {
        public void Report(long downloadedBytes, long? totalBytes)
        {
            if (!window.Dispatcher.CheckAccess())
            {
                window.Dispatcher.BeginInvoke(() => Report(downloadedBytes, totalBytes));
                return;
            }

            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                var percent = Math.Min(100d, downloadedBytes * 100d / totalBytes.Value);
                progressBar.IsIndeterminate = false;
                progressBar.Value = percent;
                progressText.Text =
                    $"Downloading update package... {percent:0.0}% ({FormatSize(downloadedBytes)} / {FormatSize(totalBytes.Value)})";
                return;
            }

            progressBar.IsIndeterminate = true;
            progressText.Text = $"Downloading update package... {FormatSize(downloadedBytes)}";
        }

        public async Task CloseAsync()
        {
            if (!window.Dispatcher.CheckAccess())
            {
                await window.Dispatcher.InvokeAsync(window.Close);
                return;
            }

            window.Close();
        }

        private static string FormatSize(long bytes)
        {
            string[] units = ["B", "KB", "MB", "GB"];
            var value = (double)bytes;
            var unitIndex = 0;

            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            return $"{value:0.##} {units[unitIndex]}";
        }
    }

    private sealed record UpdateReleaseInfo(Version Version, string DisplayVersion, string DownloadUrl, string? Notes);
}
