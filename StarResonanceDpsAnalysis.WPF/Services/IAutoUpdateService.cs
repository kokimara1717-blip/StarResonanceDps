namespace StarResonanceDpsAnalysis.WPF.Services;

public interface IAutoUpdateService
{
    Task CheckForUpdatesAsync(bool silentIfNoUpdate = true, CancellationToken cancellationToken = default);
}
