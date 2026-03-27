using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Logging;
using StarResonanceDpsAnalysis.WPF.Views;

namespace StarResonanceDpsAnalysis.WPF.Services;

public class WindowManagementService(IServiceProvider provider, ILogger<WindowManagementService> logger)
    : IWindowManagementService
{
    private readonly ILogger<WindowManagementService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IServiceProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    private readonly Dictionary<Type, Window> _toolWindows = new();

    public AboutView AboutView => GetOrCreateToolWindow<AboutView>();

    public BossTrackerView BossTrackerView => GetOrCreateToolWindow<BossTrackerView>();

    public DamageReferenceView DamageReferenceView => GetOrCreateToolWindow<DamageReferenceView>();

    public DpsStatisticsView DpsStatisticsView => GetOrCreateToolWindow<DpsStatisticsView>();

    public MainView MainView
        => InvokeOnUiThread(() => field ??= _provider.GetRequiredService<MainView>());

    public ModuleSolveView ModuleSolveView => GetOrCreateToolWindow<ModuleSolveView>();

    public PersonalDpsView PersonalDpsView => GetOrCreateToolWindow<PersonalDpsView>();

    public SettingsView SettingsView => GetOrCreateToolWindow<SettingsView>();

    public SkillBreakdownView SkillBreakdownView => GetOrCreateToolWindow<SkillBreakdownView>();

    public SkillLogView SkillLogView => GetOrCreateToolWindow<SkillLogView>();

    public void OpenPersonalDpsView()
    {
        RunOnUiThread(() => PersonalDpsView.Show());
    }

    public void ClosePersonalDpsView()
    {
        RunOnUiThread(() =>
        {
            if (!_toolWindows.Remove(typeof(PersonalDpsView), out var window)) return;
            Debug.Assert(window is PersonalDpsView);
            window.Close();
        });
    }

    private static void ConfigureOwnedToolWindow(Window view)
    {
        if (Application.Current?.MainWindow is MainView main && view.Owner == null && view != main)
        {
            view.Owner = main;
            view.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        // view.ShowInTaskbar = false; // only one taskbar icon (main)
    }

    private void RunOnUiThread(Action action)
        => InvokeOnUiThread(() =>
        {
            action();
            return true;
        });

    private T InvokeOnUiThread<T>(Func<T> factory)
    {
        var app = Application.Current;
        if (app?.Dispatcher == null || app.Dispatcher.CheckAccess())
            return factory();
        return app.Dispatcher.Invoke(factory);
    }

    private T GetOrCreateToolWindow<T>() where T : Window
    {
        return InvokeOnUiThread(() =>
        {
            if (_toolWindows.TryGetValue(typeof(T), out var existing) && existing is T typed)
            {
                return typed;
            }

            var view = CreateWindowCore<T>();
            _toolWindows[typeof(T)] = view;
            return view;
        });
    }

    private T CreateWindowCore<T>() where T : Window
    {
        var view = _provider.GetRequiredService<T>();
        ConfigureOwnedToolWindow(view);
        var windowName = typeof(T).Name;
        _logger.LogDebug(WpfLogEvents.WindowCreated, "Window created: {Window}", windowName);
        view.Closed += (_, _) =>
        {
            _ = _toolWindows.Remove(typeof(T));
            _logger.LogDebug(WpfLogEvents.WindowClosed, "Window closed: {Window}", windowName);
        };

        return view;
    }

}

public static class WindowManagementServiceExtensions
{
    public static IServiceCollection AddWindowManagementService(this IServiceCollection services)
    {
        services.AddSingleton<IWindowManagementService, WindowManagementService>();
        return services;
    }
}