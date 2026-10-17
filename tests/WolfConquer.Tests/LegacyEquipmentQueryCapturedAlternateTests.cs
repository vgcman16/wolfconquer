using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task DirectQueryCapturedAlternateSurvivesActualToggleToNormalInsideReadonlyAppend()
    {
        var callbacks = 0;
        LegacyPlayerStatusPacket? beforeToggle = null;
        LegacyPlayerStatusPacket? afterToggle = null;
        using var fixture = new DirectQueryStagesFixture("append-alt", async (current, recipient, bytes) =>
        {
            if (recipient != PlayerUid || !LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _) ||
                Interlocked.CompareExchange(ref callbacks, 1, 0) != 0) return;
            Assert.True(current.Owner.AlternateEquipmentActive);
            beforeToggle = await current.QueryStatusAsync();
            Assert.Equal(60U, beforeToggle.Breakthrough);
            Assert.Equal(50U, beforeToggle.Counteraction);
            await current.ToggleToNormalAsync(); // Real44 action; no private state/overlay mutation.
            Assert.False(current.Owner.AlternateEquipmentActive);
            afterToggle = await current.QueryStatusAsync();
            Assert.Equal(80U, afterToggle.Breakthrough);
            Assert.Equal(70U, afterToggle.Counteraction);
        });
        Assert.True(fixture.Owner.AlternateEquipmentActive);
        Assert.Null(fixture.Owner.PhysicalStatusOverlay);
        Assert.Null(fixture.Owner.EquipmentHitpointsStatus);
        var acceptedItems = fixture.Owner.PlayerItems;
        await fixture.QueryEquipmentAsync(); // Outer scope captured ALT before its Append callback.
        Assert.Equal(1, callbacks);
        Assert.NotNull(beforeToggle);
        Assert.NotNull(afterToggle);
        Assert.False(fixture.Owner.AlternateEquipmentActive); // Never restore the stale accepted toggle.
        Assert.Same(acceptedItems, fixture.Owner.PlayerItems);
        Assert.Equal(new ushort[] { 1, 3, 51, 53 },
            fixture.Owner.PlayerItems.EquipmentItems.Select(item => item.Position).Order().ToArray());
        var final = await fixture.QueryStatusAsync();
        Assert.Equal(60U, final.Breakthrough); // CapturedALT60/50, NOT freshNORMAL80/70 or droppedALT0/0.
        Assert.Equal(50U, final.Counteraction);
        Assert.Equal(60U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).Breakthrough);
        Assert.Equal(50U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).Counteraction);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 100_000);
        Assert.Equal(100_000, fixture.Owner.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, DirectOverlayFrontBreakFlag));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRain1StatusFlag));
        Assert.Equal(0, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
    }
}
