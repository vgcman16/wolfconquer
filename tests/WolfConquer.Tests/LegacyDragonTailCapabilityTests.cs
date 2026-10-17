using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDragonTailCapabilityTests
{
    [Fact]
    public void ClassifyDragonTailLineAsDedicatedRuntimeSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Line, 11_000);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("Line(14)", info.SortName);
        Assert.Equal("dragontail-line-runtime", info.NextSeam);
        Assert.Contains("DragonTail 11000 Line runtime slice", info.Reason);
        Assert.Contains("width-0 Line target shape", info.Reason);
        Assert.Contains("source counter-hit map HitShoot increments for monster hits", info.Reason);
        Assert.Contains("source physical monster/player/SOB damage", info.Reason);
        Assert.Contains("player-target WildPhoenix defender charge/archive damage override", info.Reason);
        Assert.Contains("monster/SOB-only character/spell/proficiency progress", info.Reason);
        Assert.DoesNotContain("Remaining parity is exact counter-hit map HitShoot state", info.Reason);
        Assert.Contains("magictype.txt rows 1056-1060", info.SourceEvidence);
        Assert.Contains("11000 DragonTail", info.SourceEvidence);
        Assert.Contains("Line.cs case 11000", info.SourceEvidence);
        Assert.Contains("InLine(..., 0)", info.SourceEvidence);
        Assert.Contains("CanAttackMonster/CanAttackPlayer/CanAttackNpc", info.SourceEvidence);
        Assert.Contains("WorldPacketHandlers.PlayerMagic.DragonTail.cs", info.SourceEvidence);
        Assert.DoesNotContain("counter-hit-map HitShoot", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=Line(14) spellId=11000 capability=specialCombatDamage status=partiallyModeled supported=true nextSeam=dragontail-line-runtime",
            info.Diagnostic);
    }
}
