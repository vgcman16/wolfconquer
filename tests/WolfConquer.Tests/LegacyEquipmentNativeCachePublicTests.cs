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
    public async Task HandlerEquipmentQueryOwnsEffectCachesAndRetainsAbsentNecklaceEffect()
    {
        using var fixture = new NativeEquipmentCacheFixture();
        await fixture.QueryAsync();
        var earned = LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner);
        Assert.Equal(202U, earned.RightWeaponItemEffect);
        Assert.Equal(201U, earned.RingItemEffect);
        Assert.Equal(203U, earned.NecklaceItemEffect);
        Assert.Equal(450U, earned.MaxMana);
        await fixture.UnequipAsync(4);
        Assert.Equal(0U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).RightWeaponItemEffect);
        Assert.Equal(420U, (await fixture.ReadStatusAsync()).MaxMana);
        await fixture.UnequipAsync(6);
        Assert.Equal(0U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).RingItemEffect);
        Assert.Equal(380U, (await fixture.ReadStatusAsync()).MaxMana);
        await fixture.UnequipAsync(2);
        Assert.Equal(203U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).NecklaceItemEffect);
        Assert.Equal(330U, (await fixture.ReadStatusAsync()).MaxMana);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(fixture.Owner));
        var serializedOverlay = json.RootElement.GetProperty("PhysicalStatusOverlay");
        Assert.False(serializedOverlay.TryGetProperty("RightWeaponItemEffect", out _));
        Assert.False(serializedOverlay.TryGetProperty("RingItemEffect", out _));
        Assert.False(serializedOverlay.TryGetProperty("NecklaceItemEffect", out _));
        Assert.False(serializedOverlay.TryGetProperty("MaxMana", out _));
        var foreign = fixture.Owner with { Character = fixture.Owner.Character with { Uid = PlayerUid + 1 } };
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(foreign).MaxMana);
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(foreign).NecklaceItemEffect);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerEquipmentQueryManaAndEffectsResetOnlyWhenFineRainAdmitsUnequipQuery(bool rejected)
    {
        using var fixture = new NativeEquipmentCacheFixture();
        await fixture.QueryAsync();
        if (rejected) await fixture.FineRainAsync();
        await fixture.UnequipAsync(4);
        Assert.DoesNotContain(fixture.Owner.PlayerItems.EquipmentItems, item => item.Position == 4);
        var current = LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner);
        Assert.Equal(rejected ? 202U : 0U, current.RightWeaponItemEffect);
        Assert.Equal(rejected ? 450U : 420U, current.MaxMana);
        Assert.Equal(rejected ? 450U : 420U, (await fixture.ReadStatusAsync()).MaxMana);
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
