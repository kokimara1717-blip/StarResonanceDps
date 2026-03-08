# StarResonanceDpsAnalysis.UpdateHost

Blazor-based update hosting project.

## Endpoints

- `GET /api/update/latest`: returns update manifest JSON.
  - Query option: `includePrerelease=true|false` (optional, overrides config default).
- `GET /summary.json`: returns cached list of all available non-draft releases.
- `GET /downloads/*`: serves static package files.
- `GET /releases`: HTML page showing all cached releases.

## Usage

1. Put update package files under `wwwroot/downloads`.
2. Edit `update-manifest.json`.
3. (Optional) Configure GitHub release monitoring in `appsettings.json`:

```json
"UpdateServer": {
  "GitHub": {
    "Enabled": true,
    "Owner": "your-org-or-user",
    "Repository": "your-repo",
    "IncludePrerelease": false,
    "PollingIntervalSeconds": 300
  }
}
```

When enabled, the host monitors the GitHub releases API and serves cached latest release data.
3. Run:

`dotnet run --project StarResonanceDpsAnalysis.UpdateHost/StarResonanceDpsAnalysis.UpdateHost.csproj`
