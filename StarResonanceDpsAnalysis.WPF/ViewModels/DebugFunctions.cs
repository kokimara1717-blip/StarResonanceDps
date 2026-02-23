using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Views;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class DebugFunctions : BaseViewModel
{
    private readonly LocalizationManager _localizationManager;
    private readonly ILogger<DebugFunctions> _logger;
    private readonly IPacketAnalyzer _packetAnalyzer;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDataStorage _storage;

    [ObservableProperty] private List<Option<Language>> _availableLanguages =
    [
        new(Language.Auto, Language.Auto.GetLocalizedDescription()),
        new(Language.ZhCn, Language.ZhCn.GetLocalizedDescription()),
        new(Language.EnUs, Language.EnUs.GetLocalizedDescription()),
        new(Language.PtBr, Language.PtBr.GetLocalizedDescription()),
        new(Language.KoKr, Language.KoKr.GetLocalizedDescription())
    ];

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private string _filterText = string.Empty;

    private CancellationTokenSource? _replayCts;
    private Task? _replayTask;
    [ObservableProperty] private Option<Language>? _selectedLanguage;
    [ObservableProperty] private LogLevel _selectedLogLevel = LogLevel.Information;
    [ObservableProperty] private string _version;

    public DebugFunctions(ILogger<DebugFunctions> logger,
        IOptionsMonitor<AppConfig> options,
        IPacketAnalyzer packetAnalyzer,
        LocalizationManager localizationManager,
        IServiceProvider serviceProvider,
        IDataStorage storage)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        SetProperty(options.CurrentValue, null);
        options.OnChange(SetProperty);
        _packetAnalyzer = packetAnalyzer;
        _localizationManager = localizationManager;
        _storage = storage;
        Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown";

        _logger.LogInformation("Debug panel initialized");
    }

    public LogLevel[] AvailableLogLevels { get; } =
    [
        LogLevel.Trace, LogLevel.Debug, LogLevel.Information,
        LogLevel.Warning, LogLevel.Error, LogLevel.Critical
    ];

    // Event to request sample data addition - removes direct dependency on DpsStatisticsViewModel
    public event EventHandler? SampleDataRequested;

    partial void OnSelectedLanguageChanged(Option<Language>? value)
    {
        if (value == null) return;
        _localizationManager.ApplyLanguage(value.Value);
    }

    private void SetProperty(AppConfig arg1, string? arg2)
    {
        Enabled = arg1.DebugEnabled;
    }

    [RelayCommand]
    private void CallDebugWindow()
    {
        var debugWindow = new DebugView(this);
        debugWindow.Show();
    }

    [RelayCommand]
    private void CallPlayerInfoDebugWindow()
    {
        var view = _serviceProvider.GetRequiredService<PlayerInfoDebugView>();
        var vm = _serviceProvider.GetRequiredService<PlayerInfoDebugViewModel>();
        view.DataContext = vm;
        view.Show();
    }

    [RelayCommand]
    private void ClearPlayerInfoCache()
    {
        _storage.ClearPlayerInfos();
    }

    #region AddData

    [RelayCommand]
    private void AddSampleData()
    {
        // Fire event instead of directly calling DpsStatisticsViewModel
        SampleDataRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Localization

    [ObservableProperty] private string _testLocalizationKey = string.Empty;
    [ObservableProperty] private string _localizedTestValue = string.Empty;

    [RelayCommand]
    private void TestLocalization()
    {
        var ret = _localizationManager.GetString(TestLocalizationKey);
        LocalizedTestValue = ret;
    }

    #endregion

    #region Replay

    [RelayCommand]
    private void LoadDebugDataSource()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Capture files (*.pcap;*.pcapng)|*.pcap;*.pcapng|All files (*.*)|*.*",
            Title = "Open pcap/pcapng file to replay"
        };
        if (dlg.ShowDialog() != true) return;
        StartPcapReplay(dlg.FileName);
        _logger.LogInformation("Replaying PCAP: {File}", Path.GetFileName(dlg.FileName));
    }

    private void StartPcapReplay(string filePath, bool realtime = true, double speed = 1.0)
    {
        StopPcapReplay();
        _replayCts = new CancellationTokenSource();
        var token = _replayCts.Token;
        _replayTask = Task.Run(async () =>
        {
            try
            {
                await _packetAnalyzer.ReplayFileAsync(filePath, realtime, speed, token).ConfigureAwait(false);
                _logger.LogInformation("PCAP replay completed: {File}", Path.GetFileName(filePath));
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("PCAP replay cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PCAP replay failed: {File}", Path.GetFileName(filePath));
            }
            finally
            {
                try
                {
                    _replayCts?.Dispose();
                }
                catch
                {
                    // ignored
                }

                _replayCts = null;
                _replayTask = null;
            }
        }, token);
    }

    private void StopPcapReplay()
    {
        if (_replayCts == null) return;
        try
        {
            _replayCts.Cancel();
            _replayTask?.Wait(3000);
            _logger.LogInformation("PCAP replay stopped");
        }
        catch (AggregateException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping PCAP replay");
        }
        finally
        {
            try
            {
                _replayCts.Dispose();
            }
            catch
            {
                // ignored
            }

            _replayCts = null;
            _replayTask = null;
        }
    }

    #endregion
}