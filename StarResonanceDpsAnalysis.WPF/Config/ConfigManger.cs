using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StarResonanceDpsAnalysis.WPF.Config;

public class ConfigManger : IConfigManager
{
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IOptionsMonitor<AppConfig> _optionsMonitor;
    private readonly ILogger<IConfigManager> _logger;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public ConfigManger(IOptionsMonitor<AppConfig> optionsMonitor,
        IOptions<JsonSerializerOptions> jsonOptions, ILogger<IConfigManager> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _jsonOptions = jsonOptions.Value;
        _configFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        // Subscribe to configuration changes
        _optionsMonitor.OnChange(OnConfigurationChanged);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="newConfig"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task SaveAsync(AppConfig? newConfig = null)
    {
        await _saveLock.WaitAsync();
        try
        {
            // Read the current appsettings.json using FileStream with proper sharing
            string jsonContent;
            await using (var readStream = new FileStream(_configFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(readStream, Encoding.UTF8))
            {
                jsonContent = await reader.ReadToEndAsync();
            }

            var rootDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent, _jsonOptions) ??
                           new Dictionary<string, object>();

            // Update the Config section
            newConfig ??= CurrentConfig;
            rootDict["Config"] = newConfig;

            // Write back to file using FileStream with proper sharing
            var updatedJson = JsonSerializer.Serialize(rootDict, _jsonOptions);
            await using (var writeStream = new FileStream(_configFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            await using (var writer = new StreamWriter(writeStream, Encoding.UTF8))
            {
                await writer.WriteAsync(updatedJson);
            }

            //// Force configuration reload (the file watcher should pick this up automatically)
            //// But we can also manually notify if needed
            //OnConfigurationChanged(newConfig);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to Update config: {ex}", ex);
            throw new InvalidOperationException($"Failed to update configuration: {ex.Message}", ex);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public event EventHandler<AppConfig>? ConfigurationUpdated;

    public AppConfig CurrentConfig => _optionsMonitor.CurrentValue;

    private void OnConfigurationChanged(AppConfig newConfig)
    {
        ConfigurationUpdated?.Invoke(this, newConfig);
    }
}