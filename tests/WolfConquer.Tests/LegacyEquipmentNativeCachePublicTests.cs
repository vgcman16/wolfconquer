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
    [InlineData(14_005, 3_000L, 24_614U)]
    public async Task HandlerEquipmentQueryEarnsNativeManaFromItemsPurificationAndUshortClassSpirit(
        int classCode, long spirit, uint expected)
    {
        using var fixture = new NativeEquipmentCacheFixture(classCode, spirit);
        await fixture.QueryAsync();
        Assert.Equal(expected, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).MaxMana);
        Assert.Equal(expected, (await fixture.ReadStatusAsync()).MaxMana);
    }



    [Fact]
    public async Task HandlerEquipmentQueryReadsCurrentSpiritAfterNativeDerivedSpellPublication()
    {
        var entered = false;
        using var fixture = new NativeEquipmentCacheFixture(venom: true, onSend: (current, bytes) =>
        {
            var frame = ReadFrame(bytes);
            if (!entered && LegacySpellPacket.TryParse(frame, out var spell) && spell.Id == 20_000)
            {
                entered = true;
                Assert.Equal(150U, LegacyPhysicalStatusOverlay.ForOwner(current.Owner).MaxMana);
                current.ChangeSpiritDuringPublication(20);
            }
            return ValueTask.CompletedTask;
        });
        await fixture.QueryAsync();
        Assert.True(entered, fixture.Output);
        fixture.RequireSavedVenom();
        Assert.Equal(20L, fixture.Owner.Character.Spirit);
        Assert.Equal(750U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).MaxMana);
        Assert.Equal(750U, (await fixture.ReadStatusAsync()).MaxMana);
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
