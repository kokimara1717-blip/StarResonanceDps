using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using SharpPcap;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Logging;
using StarResonanceDpsAnalysis.WPF.Plugins;
using StarResonanceDpsAnalysis.WPF.Plugins.Interfaces;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.Themes;
using StarResonanceDpsAnalysis.WPF.ViewModels;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;
using StarResonanceDpsAnalysis.WPF.Views;

namespace StarResonanceDpsAnalysis.WPF;

public partial class App : Application
{
    private const string DefaultAppSettingsJson = """
{
  "Config": {
    "DpsUpdateMode": "Active",
    "DpsUpdateInterval": 100,
    "UseProcessPortsFilter": false,
    "EnableAutoUpdate": true,
    "AutoUpdateCheckOnStartup": true,
    "UpdateSource": "GitHub",
    "GithubRepository": "anying1073/StarResonanceDps",
    "GithubIncludePrerelease": false,
    "GithubAssetNameContains": "WPF",
    "SelfHostedManifestUrl": "",
    "UpdateRequestTimeoutSeconds": 50
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "System": "Warning"
    }
  },
  "Serilog": {
    "Using": [
      "Serilog.Sinks.File"
    ],
    "MinimumLevel": {
      "Default": "Warning",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/logs-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 3
        }
      }
    ]
  }
}
""";

    private static ILogger<App>? _logger;

    private static readonly Dictionary<Type, ServiceLifetime> LifeTimeOverrides = new()
    {
        { typeof(DpsStatisticsViewModel), ServiceLifetime.Singleton },
        { typeof(DpsStatisticsView), ServiceLifetime.Singleton },
        { typeof(SkillBreakdownViewModel), ServiceLifetime.Transient },
        { typeof(SkillBreakdownView), ServiceLifetime.Transient },
        { typeof(PlayerInfoDebugViewModel), ServiceLifetime.Transient },
        { typeof(PlayerInfoDebugView), ServiceLifetime.Transient }
    };

    public static IHost? Host { get; private set; }

    [STAThread]
    private static void Main(string[] args)
    {
        var configRoot = BuildConfiguration();
        ConfigureLogging(configRoot);

        Host = CreateHostBuilder(args, configRoot).Build();
        _logger = Host.Services.GetRequiredService<ILogger<App>>();

        _logger.LogInformation(WpfLogEvents.AppStarting, "Application starting");

        var app = new App();
        app.InitializeComponent();

        // General exception handler for unhandled exceptions
        app.DispatcherUnhandledException += (sender, e) =>
        {
            _logger?.LogError(e.Exception, "Unhandled dispatcher exception");
            // Optionally handle gracefully
            // e.Handled = true;
        };

        // Centralized application startup (localization, adapter, analyzer)
        var appStartup = Host.Services.GetRequiredService<IApplicationStartup>();
        appStartup.InitializeAsync().Wait();

        app.MainWindow = Host.Services.GetRequiredService<DpsStatisticsView>();
        app.MainWindow.Visibility = Visibility.Visible;
        app.Run();

        // Centralized shutdown
        try
        {
            appStartup.ShutdownAsync().Wait();
        }
        catch
        {
            // ignored
        }

        _logger.LogInformation(WpfLogEvents.AppExiting, "Application exiting");
        Log.CloseAndFlush();
    }

    private static IConfiguration BuildConfiguration()
    {
        EnsureValidAppSettings();

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile("appsettings.Development.json", true, true)
            .Build();
    }

    private static void EnsureValidAppSettings()
    {
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (IsValidJsonFile(appSettingsPath))
        {
            return;
        }

        File.WriteAllText(appSettingsPath, DefaultAppSettingsJson);
    }

    private static bool IsValidJsonFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var content = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            using var _ = JsonDocument.Parse(content);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void ConfigureLogging(IConfiguration configRoot)
    {
        var debugEnabled = configRoot.GetSection("Config").GetValue<bool>("DebugEnabled");
        if (debugEnabled)
        {
            Interop.Kernel32.AllocConsole();
        }

        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configRoot)
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext();

        if (debugEnabled)
        {
            loggerConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");
        }

        Log.Logger = loggerConfig.CreateLogger();
    }

    private static IHostBuilder CreateHostBuilder(string[] args, IConfiguration configRoot)
    {
        return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(builder => { builder.AddConfiguration(configRoot); })
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                services.AddJsonConfiguration();
                services.Configure<AppConfig>(context.Configuration.GetSection("Config"));

                RegisterViewModels(services);
                RegisterViews(services);

                services.AddPacketAnalyzer();
                services.AddHttpClient();
                services.AddThemes();
                services.AddWindowManagementService();
                services.AddMessageDialogService();
                services.AddClassColorService();
                services.AddSingleton<IAutoUpdateService, AppUpdateService>();

                // ? Register new DPS services (SOLID refactoring)
                services.AddDpsServices();

                services.AddSingleton<BattleHistoryService>();
                services.AddSingleton<ISkillLogService, SkillLogService>();

                services.AddSingleton<DebugFunctions>();
                services.AddSingleton(CaptureDeviceList.Instance);
                services.AddSingleton<IApplicationControlService, ApplicationControlService>();
                services.AddSingleton<IDeviceManagementService, DeviceManagementService>();
                services.AddSingleton<IApplicationStartup, ApplicationStartup>();
                services.AddSingleton<IConfigManager, ConfigManger>();
                services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
                services.AddSingleton<IMousePenetrationService, MousePenetrationService>();
                services.AddSingleton<ITopmostService, TopmostService>();
                services.AddSingleton<DataSourceEngine>();
                RegisterBuiltInPlugins(services);

                services.AddSingleton<IPluginManager, PluginManager>();
                services.AddSingleton<ITrayService, TrayService>();

                services.AddSingleton(_ => Current.Dispatcher);

                // Localization manager singleton
                services.AddSingleton(new LocalizationConfiguration
                {
                    LocalizationDirectory = Path.Combine(AppContext.BaseDirectory, "Data")
                });
                services.AddSingleton<LocalizationManager>();
            })
            .ConfigureLogging(lb => lb.ClearProviders());
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        RegisterTypes(services, "StarResonanceDpsAnalysis.WPF.ViewModels", "ViewModel");
    }

    private static void RegisterViews(IServiceCollection services)
    {
        RegisterTypes(services, "StarResonanceDpsAnalysis.WPF.Views", "View");
    }

    private static void RegisterBuiltInPlugins(IServiceCollection services)
    {
        RegisterTypes(services, "StarResonanceDpsAnalysis.WPF.Plugins.BuiltIn", "Plugin", typeof(IPlugin),
            ServiceLifetime.Singleton);
    }

    private static void RegisterTypes(
        IServiceCollection services,
        string @namespace,
        string suffix,
        Type? overrideServiceType = null,
        ServiceLifetime defLifetime = ServiceLifetime.Transient)
    {
        var types = typeof(App).Assembly
            .GetTypes()
            .Where(t =>
                t is { IsAbstract: false, IsClass: true } &&
                t.Namespace != null &&
                t.Namespace.StartsWith(@namespace, StringComparison.Ordinal) &&
                t.Name.EndsWith(suffix, StringComparison.Ordinal));

        foreach (var type in types)
        {
            var lifetime = LifeTimeOverrides.TryGetValue(type, out var overrideLifetime)
                ? overrideLifetime
                : defLifetime;

            var serviceType = overrideServiceType ?? type;

            services.Add(new ServiceDescriptor(serviceType, type, lifetime));
        }
    }
}