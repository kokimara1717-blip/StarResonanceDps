using System;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;
using StarResonanceDpsAnalysis.WPF.Views;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public sealed class PersonalDpsDesignTimeViewModel : PersonalDpsViewModel
{
    public PersonalDpsDesignTimeViewModel() : base(
        new DesignWindowManagementService(),
        new DesignDataStorage(),
        Dispatcher.CurrentDispatcher,
        new DesignConfigManager(), null!,
        new DesignMessageDialogService(),
        NullLogger<PersonalDpsViewModel>.Instance)
    {
        TotalDamage = 52_224_900d;
        Dps = 9_526_200d;
    }

    private sealed class DesignAppControlService : IApplicationControlService
    {
        public void Shutdown()
        {
        }
    }

    private sealed class DesignWindowManagementService : IWindowManagementService
    {
        public void OpenPersonalDpsView()
        {
            throw new NotSupportedException();
        }

        public void ClosePersonalDpsView()
        {
            throw new NotSupportedException();
        }

        public PersonalDpsView PersonalDpsView => throw new NotSupportedException();
        public DpsStatisticsView DpsStatisticsView => throw new NotSupportedException();
        public SettingsView SettingsView => throw new NotSupportedException();
        public SkillBreakdownView SkillBreakdownView => throw new NotSupportedException();
        public AboutView AboutView => throw new NotSupportedException();
        public DamageReferenceView DamageReferenceView => throw new NotSupportedException();
        public ModuleSolveView ModuleSolveView => throw new NotSupportedException();
        public BossTrackerView BossTrackerView => throw new NotSupportedException();
        public MainView MainView => throw new NotSupportedException();
        public SkillLogView SkillLogView => throw new NotSupportedException();
    }
}
