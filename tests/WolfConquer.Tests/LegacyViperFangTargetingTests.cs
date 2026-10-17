using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyViperFangTargetingTests
{
    [Fact]
    public void ViperFangLineWidthMatchesSourceWidthOne()
    {
        var spell = new LegacyMagicRuntimeSpell(
            Id: 11_005,
            Sort: LegacyMagicSort.Line,
            Name: "ViperFang",
            Level: 0,
            UseMana: 0,
            Damage: 120,
            Experience: 0,
            MaxTargets: 0,
            Range: 12,
            NeedLevel: 0,
            WeaponType: 0,
            UseStamina: 0,
            CoolDown: 0,
            ColdTime: 0);

        Assert.Equal(1, LegacyMagicRuntime.GetLegacyLineWidth(spell));
    }
}
