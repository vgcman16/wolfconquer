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

}
