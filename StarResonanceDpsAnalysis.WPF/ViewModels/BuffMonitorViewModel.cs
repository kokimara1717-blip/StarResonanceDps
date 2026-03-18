using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.WPF.Config;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// ViewModel for the buff monitor window that displays active buffs for the selected player.
/// </summary>
public partial class BuffMonitorViewModel : BaseDispatcherSupportViewModel
{
    private readonly IDataStorage _storage;
    private readonly IConfigManager? _configManager;
    private readonly EntityBuffMonitors _entityBuffMonitors;
    private BuffMonitor? _playerBuffMonitor;

    [ObservableProperty]
    private ObservableCollection<BuffDisplayItem> _activeBuffs = [];

    [ObservableProperty]
    private ICollectionView? _filteredBuffs;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private long _selectedPlayerUid;

    [ObservableProperty]
    private string _selectedPlayerName = "Local Player";

    [ObservableProperty]
    private bool _monitorAllBuffs = true;

    [ObservableProperty]
    private int _activeBuffCount;

    [ObservableProperty]
    private string _monitoredBuffIds = string.Empty;

    [ObservableProperty]
    private string _selfAppliedBuffIds = string.Empty;

    [ObservableProperty]
    private AppConfig _appConfig = new();

    public BuffMonitorViewModel(IDataStorage storage, IConfigManager? configManager, EntityBuffMonitors entityBuffMonitors, Dispatcher? dispatcher) : base(dispatcher!)
    {
        _storage = storage;
        _configManager = configManager;
        _entityBuffMonitors = entityBuffMonitors;

        if (_configManager != null)
        {
            AppConfig = _configManager.CurrentConfig;
            _configManager.ConfigurationUpdated += OnConfigurationUpdated;
        }


        RefreshPlayerInfo();
        // Subscribe to buff effect events
        _storage.BuffEffectReceived += OnBuffEffectReceived;

        // Register or get the buff monitor for this player

        FilteredBuffs = CollectionViewSource.GetDefaultView(ActiveBuffs);
        if (FilteredBuffs != null)
        {
            FilteredBuffs.Filter = FilterBuff;
            FilteredBuffs.SortDescriptions.Add(new SortDescription(nameof(BuffDisplayItem.BaseId), ListSortDirection.Ascending));
        }

        // Start update timer
        var timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher!)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        timer.Tick += UpdateBuffTimers;
        timer.Start();
    }

    private void RefreshPlayerInfo()
    {
        SelectedPlayerName = _storage.CurrentPlayerInfo.Name ?? "Local Player";
        SelectedPlayerUid = _storage.CurrentPlayerUID;
        EnsurePlayerBuffMonitor();
    }

    /// <summary>
    /// Handles configuration updates.
    /// </summary>
    private void OnConfigurationUpdated(object? sender, AppConfig newConfig)
    {
        AppConfig = newConfig;
    }

    /// <summary>
    /// Handles buff effect received events from the data storage.
    /// </summary>
    private void OnBuffEffectReceived(long entityUid, BuffProcessResult buffProcessResult)
    {
        if (entityUid != SelectedPlayerUid || _playerBuffMonitor == null) return;

        // Sync display with monitor's active buffs
        UpdateBuffs(entityUid, buffProcessResult);
    }

    /// <summary>
    /// Updates the buff display when buff changes are received.
    /// </summary>
    private void UpdateBuffs(long entityUid, BuffProcessResult buffProcessResult)
    {
        // If no update payload, the monitor's ActiveBuffs have been updated directly
        // (e.g., from DeltaInfoProcessors), so sync the entire display
        if (buffProcessResult.UpdatePayload == null || buffProcessResult.UpdatePayload.Count == 0)
        {
            SyncBuffsToDisplay();
            return;
        }

        // Use the UpdatePayload for efficient targeted updates
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        InvokeOnDispatcher(() =>
        {
            // Process each buff change event
            foreach (var change in buffProcessResult.Changes)
            {
                if (change.ChangeType != BuffChangeType.Remove) continue;

                var removedBuff = ActiveBuffs.FirstOrDefault(b => b.BaseId == change.BaseId);
                if (removedBuff != null)
                {
                    ActiveBuffs.Remove(removedBuff);
                }
            }

            // Update or add buffs from the payload
            foreach (var buffState in buffProcessResult.UpdatePayload)
            {
                var existingBuff = ActiveBuffs.FirstOrDefault(b => b.BaseId == buffState.BaseId);

                if (existingBuff != null)
                {
                    // Update existing buff
                    existingBuff.Layer = buffState.Layer;
                    existingBuff.DurationMs = buffState.DurationMs;
                    existingBuff.CreateTimeMs = buffState.CreateTimeMs;
                    existingBuff.RemainingTimeMs = Math.Max(0, buffState.DurationMs - (now - buffState.CreateTimeMs));
                }
                else
                {
                    // Add new buff
                    ActiveBuffs.Add(new BuffDisplayItem
                    {
                        BaseId = buffState.BaseId,
                        DisplayName = $"Buff {buffState.BaseId}",
                        Layer = buffState.Layer,
                        DurationMs = buffState.DurationMs,
                        CreateTimeMs = buffState.CreateTimeMs,
                        RemainingTimeMs = Math.Max(0, buffState.DurationMs - (now - buffState.CreateTimeMs))
                    });
                }
            }

            ActiveBuffCount = ActiveBuffs.Count;
        });
    }

    /// <summary>
    /// Ensures the player has a registered buff monitor in EntityBuffMonitors.
    /// </summary>
    private void EnsurePlayerBuffMonitor()
    {
        if (SelectedPlayerUid == 0) return;

        if (!_entityBuffMonitors.Monitors.TryGetValue(SelectedPlayerUid, out _playerBuffMonitor))
        {
            _playerBuffMonitor = new BuffMonitor
            {
                MonitorAllBuff = MonitorAllBuffs
            };
            _entityBuffMonitors.Monitors[SelectedPlayerUid] = _playerBuffMonitor;
        }
    }

    /// <summary>
    /// Design-time constructor.
    /// </summary>
    public BuffMonitorViewModel() : this(new Core.Data.DataStorage(), null, new EntityBuffMonitors(), Dispatcher.CurrentDispatcher)
    {
        // Add some design-time data
        ActiveBuffs.Add(new BuffDisplayItem
        {
            BaseId = 100001,
            DisplayName = "Attack Buff",
            Layer = 3,
            DurationMs = 30000,
            CreateTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            RemainingTimeMs = 25000
        });
        ActiveBuffs.Add(new BuffDisplayItem
        {
            BaseId = 100002,
            DisplayName = "Defense Buff",
            Layer = 1,
            DurationMs = 60000,
            CreateTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            RemainingTimeMs = 45000
        });
        ActiveBuffCount = ActiveBuffs.Count;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(ActiveBuffs))
        {
            FilteredBuffs = CollectionViewSource.GetDefaultView(ActiveBuffs);
            if (FilteredBuffs != null)
            {
                FilteredBuffs.Filter = FilterBuff;
                FilteredBuffs.SortDescriptions.Add(new SortDescription(nameof(BuffDisplayItem.BaseId), ListSortDirection.Ascending));
            }
        }
        else if (e.PropertyName == nameof(FilterText))
        {
            FilteredBuffs?.Refresh();
        }
        else if (e.PropertyName == nameof(MonitorAllBuffs))
        {
            if (_playerBuffMonitor != null)
            {
                _playerBuffMonitor.MonitorAllBuff = MonitorAllBuffs;
            }
        }
    }

    private bool FilterBuff(object item)
    {
        if (item is not BuffDisplayItem buff) return false;
        if (string.IsNullOrWhiteSpace(FilterText)) return true;

        return buff.BaseId.ToString().Contains(FilterText) ||
               buff.DisplayName.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void ApplyConfiguration()
    {
        var monitoredIds = ParseBuffIds(MonitoredBuffIds);
        var selfAppliedIds = ParseBuffIds(SelfAppliedBuffIds);

        if (_playerBuffMonitor != null)
        {
            _playerBuffMonitor.MonitoredBuffIds = monitoredIds.ToHashSet();
            _playerBuffMonitor.SelfAppliedBuffIds = selfAppliedIds.ToHashSet();
        }
    }

    [RelayCommand]
    private void ClearBuffs()
    {
        _playerBuffMonitor?.Clear();
        InvokeOnDispatcher(() =>
        {
            ActiveBuffs.Clear();
            ActiveBuffCount = 0;
        });
    }

    [RelayCommand]
    private void Refresh()
    {
        SyncBuffsToDisplay();
    }

    /// <summary>
    /// Synchronizes the display collection with the monitor's active buffs.
    /// Adds new buffs and updates existing ones without clearing the collection.
    /// </summary>
    private void SyncBuffsToDisplay()
    {
        if (_playerBuffMonitor == null) return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        InvokeOnDispatcher(() =>
        {
            // Get current buff IDs from monitor and display
            var monitorBuffIds = _playerBuffMonitor.ActiveBuffs.Keys.ToHashSet();
            var displayBuffIds = ActiveBuffs.Select(b => b.BaseId).ToHashSet();

            // Remove buffs that no longer exist in monitor
            var buffsToRemove = ActiveBuffs.Where(b => !monitorBuffIds.Contains(b.BaseId)).ToList();
            foreach (var buff in buffsToRemove)
            {
                ActiveBuffs.Remove(buff);
            }

            // Add new buffs or update existing ones
            foreach (var (buffId, activeBuff) in _playerBuffMonitor.ActiveBuffs)
            {
                var existingBuff = ActiveBuffs.FirstOrDefault(b => b.BaseId == buffId);

                if (existingBuff != null)
                {
                    // Update existing buff properties
                    existingBuff.Layer = activeBuff.Layer;
                    existingBuff.DurationMs = activeBuff.Duration;
                    existingBuff.CreateTimeMs = activeBuff.CreateTime;
                    existingBuff.RemainingTimeMs = Math.Max(0, activeBuff.Duration - (now - activeBuff.CreateTime));
                }
                else
                {
                    // Add new buff to display
                    ActiveBuffs.Add(new BuffDisplayItem
                    {
                        BaseId = buffId,
                        DisplayName = $"Buff {buffId}",
                        Layer = activeBuff.Layer,
                        DurationMs = activeBuff.Duration,
                        CreateTimeMs = activeBuff.CreateTime,
                        RemainingTimeMs = Math.Max(0, activeBuff.Duration - (now - activeBuff.CreateTime))
                    });
                }
            }

            ActiveBuffCount = ActiveBuffs.Count;
        });
    }

    private void UpdateBuffTimers(object? sender, EventArgs e)
    {
        RefreshPlayerInfo();

        if (ActiveBuffs.Count == 0) return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var buffsToRemove = new List<BuffDisplayItem>();

        // Update remaining time for all buffs and collect expired ones
        foreach (var buff in ActiveBuffs)
        {
            var remaining = buff.DurationMs - (now - buff.CreateTimeMs);
            if (remaining <= 0)
            {
                buffsToRemove.Add(buff);
            }
            else
            {
                buff.RemainingTimeMs = remaining;
            }
        }

        // Remove expired buffs
        if (buffsToRemove.Count > 0)
        {
            foreach (var buff in buffsToRemove)
            {
                ActiveBuffs.Remove(buff);
            }
            ActiveBuffCount = ActiveBuffs.Count;
        }
    }

    private static List<int> ParseBuffIds(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];

        return input.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .ToList();
    }
}

/// <summary>
/// Display item for a buff in the UI.
/// </summary>
public partial class BuffDisplayItem : ObservableObject
{
    [ObservableProperty]
    private int _baseId;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private int _layer;

    [ObservableProperty]
    private long _durationMs;

    [ObservableProperty]
    private long _createTimeMs;

    [ObservableProperty]
    private long _remainingTimeMs;

    public string RemainingTimeFormatted => TimeSpan.FromMilliseconds(RemainingTimeMs).ToString(@"mm\:ss");

    public double ProgressPercent => DurationMs > 0 ? (RemainingTimeMs / (double)DurationMs) * 100 : 0;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(RemainingTimeMs))
        {
            OnPropertyChanged(nameof(RemainingTimeFormatted));
            OnPropertyChanged(nameof(ProgressPercent));
        }
    }
}
