using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(1_005, 10L, 200U)]
    [InlineData(13_002, 10L, 300U)]
    [InlineData(13_003, 10L, 350U)]
    [InlineData(13_004, 10L, 400U)]
    [InlineData(13_005, 10L, 450U)]
    [InlineData(14_005, 2_000L, 60_150U)]
    public async Task HandlerEquipmentQueryEarnsNativeManaFromItemsPurificationAndUshortClassSpirit(
        int classCode, long spirit, uint expected)
    {
        using var fixture = new NativeEquipmentCacheFixture(classCode, spirit);
        await fixture.QueryAsync();
        Assert.Equal(expected, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).MaxMana);
        Assert.Equal(expected, (await fixture.ReadStatusAsync()).MaxMana);
    }



    [Fact]
    public async Task DetachedDerivedSpellQueryDoesNotInventAConsumptionOrCallback()
    {
        using var fixture = new NativeEquipmentCacheFixture(venom: true);
        var before = fixture.Owner.Character;
        await fixture.QueryAsync();
        Assert.Equal(before.Spirit, fixture.Owner.Character.Spirit);
        Assert.Equal(before.AttributePoints, fixture.Owner.Character.AttributePoints);
        Assert.False(fixture.Owner.PlayerSpells.ContainsSpell(20000));
        Assert.DoesNotContain(fixture.Frames, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var row) && row.Id == 20000);
    }

    [Fact]
    public async Task HandlerEquipmentQueryEarnsAllMatchingPirateMaxMpAndAlignedInactiveJiangStars()
    {
        using var fixture = new NativeEquipmentCacheFixture(classCode: 7_005, pirateAndJiang: true);
        await fixture.QueryAsync();
        var earned = LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner);
        Assert.Equal(220U, earned.JiangHuMaxMana);
        Assert.Equal(459U, earned.MaxMana); //150 item/purif +24 zero-score matching tiers +15 jade +50 spirit +220 Jiang.
        Assert.Equal(459U, (await fixture.ReadStatusAsync()).MaxMana);
        await fixture.QueryAsync();
        Assert.Equal(459U, (await fixture.ReadStatusAsync()).MaxMana); //Rebuild, never accumulate a prior MaxMana.
    }
}
