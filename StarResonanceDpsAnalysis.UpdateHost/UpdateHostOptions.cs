namespace StarResonanceDpsAnalysis.UpdateHost;

public sealed class UpdateHostOptions
{
    public const string SectionName = "UpdateServer";

    public string ManifestFilePath { get; set; } = "update-manifest.json";
    public GitHubReleaseOptions GitHub { get; set; } = new();
}

public sealed class GitHubReleaseOptions
{
    public bool Enabled { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public bool IncludePrerelease { get; set; }
    public int PollingIntervalSeconds { get; set; } = 300;
}
