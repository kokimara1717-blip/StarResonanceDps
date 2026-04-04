using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Localization;

namespace StarResonanceDpsAnalysis.Tests.WpfExtensions;

public class StatisticsToViewModelConverterTests
{
    [Fact]
    public void ToSkillItemVmList_IncludesCritAndLuckyEntriesInCritMetrics()
    {
        var playerStats = new PlayerStatistics(1);
        playerStats.AttackDamage.Total = 500;

        var skill = playerStats.GetOrCreateSkill(1001);
        skill.TotalValue = 500;
        skill.UseTimes = 5;
        skill.CritTimes = 1;
        skill.CritValue = 120;
        skill.LuckyTimes = 1;
        skill.LuckValue = 80;
        skill.CritAndLuckyTimes = 2;
        skill.CritAndLuckyValue = 200;

        var result = playerStats.ToSkillItemVmList(LocalizationManager.Instance);
        var item = Assert.Single(result.Damage);

        Assert.Equal(3, item.CritCount);
        Assert.Equal(320, item.CritValue);
        Assert.Equal(0.6, item.CritRate, 6);
        Assert.Equal(3, item.LuckyCount);
        Assert.Equal(280, item.LuckyValue);
        Assert.Equal(100, item.NormalValue);
    }
}
