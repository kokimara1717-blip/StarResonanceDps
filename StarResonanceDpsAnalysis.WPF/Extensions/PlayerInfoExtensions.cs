using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Extensions;

public static class PlayerInfoExtensions
{
    public static void Update(this PlayerInfoViewModel vm, PlayerInfo source)
    {
        ArgumentNullException.ThrowIfNull(vm);
        ArgumentNullException.ThrowIfNull(source);

        vm.Name = source.Name;
        vm.Spec = source.Spec;
        vm.PowerLevel = source.CombatPower.GetValueOrDefault();
        vm.NpcTemplateId = source.NpcTemplateId;
        vm.SeasonStrength = source.SeasonStrength.GetValueOrDefault();
        vm.SeasonLevel = source.SeasonLevel;
        vm.Class = source.Class;
        vm.IsNpc = source.NpcTemplateId != 0;
    }
}