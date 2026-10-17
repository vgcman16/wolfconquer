using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task RootMagicRawUnqueriedRidingCropCannotReplaceNativeEquipmentCache()
    {
        await using var fixture = new RootMagicEntryFixture(ridingCrop: true, ridingCropQueryControls: true);
        Assert.Equal(203_003U, Assert.Single(fixture.Owner.PlayerItems.EquipmentItems,
            item => item.Position == 18).ItemId);
        Assert.Null(fixture.Owner.PhysicalStatusOverlay?.RidingCropItemId);
        await fixture.MountAndWaitAsync(queryEquippedCrop: false);
        var hadStamp = fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var beforeStamp);
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0x32));
        Assert.Equal(hadStamp, fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var afterStamp));
        Assert.Equal(beforeStamp, afterStamp);
        Assert.Empty(RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.DoesNotContain(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)),
            packet => packet.SpellId == 1_000);
        Assert.Equal(100_000L, fixture.Peer.Character.MinHitPoints);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootMagicUsesEarnedRidingCropCacheAcrossAdmittedOrRejectedUnequip(bool rejectQuery)
    {
        await using var fixture = new RootMagicEntryFixture(ridingCrop: true, ridingCropQueryControls: true);
        await fixture.MountAndWaitAsync(); // Public251 earns source cache before real7001.
        Assert.Equal(203_003U, fixture.Owner.PhysicalStatusOverlay!.RidingCropItemId);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0x32));
        if (rejectQuery) await fixture.CastAsync(14_500, target: PlayerUid, x: 830, y: 584);
        fixture.Advance(TimeSpan.FromMilliseconds(500));
        await fixture.UnequipRootRidingCropAsync();
        Assert.DoesNotContain(fixture.Owner.PlayerItems.EquipmentItems, item => item.Position == 18);
        Assert.Contains(fixture.Owner.PlayerItems.InventoryItems, item => item.Uid == 99_180 && item.Position == 0);
        Assert.Equal(rejectQuery ? 203_003U : 0U, fixture.Owner.PhysicalStatusOverlay!.RidingCropItemId);
        var hadStamp = fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var beforeStamp);
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000);
        Assert.Equal(rejectQuery, HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0x32));
        if (rejectQuery)
        {
            fixture.AssertProducedStamp(fixture.Now);
            fixture.AssertCostPairSince(start, 90, 960);
            Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == 1_000);
        }
        else
        {
            Assert.Equal(hadStamp, fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var afterStamp));
            Assert.Equal(beforeStamp, afterStamp);
            Assert.Empty(RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
            Assert.DoesNotContain(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)),
                packet => packet.SpellId == 1_000);
        }
    }
}
