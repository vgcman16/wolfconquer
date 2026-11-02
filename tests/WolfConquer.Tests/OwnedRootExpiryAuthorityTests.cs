using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public Task OwnedRootExpiryRejectsUnassociatedFutureAndInvalidDataWithoutMutation() =>
        RunOwnedRootEquipmentAsync("expiry-authority", async fixture =>
    {
        await fixture.Query();
        var beforeProc = fixture.Owner; var beforeProcFiles = fixture.Data.Files(); int beforeProcFrames = fixture.Order.Count;
        Assert.False(fixture.HasFlag(8)); Assert.Null(fixture.ActualClock(8));
        Assert.False(fixture.AttemptUnassociatedManualProc());
        Assert.Same(beforeProc, fixture.Owner); Assert.False(fixture.HasFlag(8)); Assert.Null(fixture.ActualClock(8));
        Assert.Equal(beforeProcFrames, fixture.Order.Count); fixture.Data.AssertFiles(beforeProcFiles);
        await fixture.Cast(1000); // Real equipped necklace proc installs Shield8.
        Assert.True(fixture.HasFlag(8));
        var shield = Assert.IsType<LegacyRootItemEffectFlagClock>(fixture.ActualClock(8));
        Assert.True(fixture.Deadline(8) > fixture.Now);
        await fixture.Cast(1095, PlayerUid, 830, 584); // Real Classic Stigma, no Root clock.
        Assert.True(fixture.HasFlag(9)); Assert.Null(fixture.ActualClock(9));
        Assert.True(fixture.Deadline(9) > fixture.Now);
        var current = fixture.Owner;
        var binding = current.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
        var resources = (binding.Player.HitPoints, binding.Player.Mana, binding.Player.Stamina,
            current.Character.MinHitPoints, current.Character.MinMana, current.Character.ConquerPoints);
        var shieldDeadline = fixture.Deadline(8); var stigmaDeadline = fixture.Deadline(9);
        var files = fixture.Data.Files(); int start = fixture.Order.Count;

        // These read the real current clock and configured provider; they do
        // not mint a producer authorization or advance the runtime clock.
        Assert.Null(fixture.ObserveRealClockExpiry(8));
        Assert.Null(fixture.ObserveRealClockExpiry(9));
        Assert.Null(fixture.ObserveRealClockExpiry(7));
        Assert.Null(fixture.ObserveRealClockExpiry(10));
        Assert.False(fixture.AttemptInvalidFlagPreparation(7));
        Assert.False(fixture.AttemptInvalidFlagPreparation(10));
        Assert.False(fixture.AttemptUnassociatedExpiry());
        Assert.False(fixture.AttemptUnassociatedExpiry());

        Assert.Same(current, fixture.Owner); Assert.Same(shield, fixture.ActualClock(8));
        Assert.Null(fixture.ActualClock(9)); Assert.True(fixture.HasFlag(8)); Assert.True(fixture.HasFlag(9));
        Assert.Equal(shieldDeadline, fixture.Deadline(8)); Assert.Equal(stigmaDeadline, fixture.Deadline(9));
        Assert.Equal(resources, (binding.Player.HitPoints, binding.Player.Mana, binding.Player.Stamina,
            fixture.Owner.Character.MinHitPoints, fixture.Owner.Character.MinMana, fixture.Owner.Character.ConquerPoints));
        Assert.Equal(start, fixture.Order.Count);
        fixture.Data.AssertFiles(files);
    });

    [Fact]
    public Task OwnedRootActualGlobalTickAuthorizesBothDueEntriesInOrder() =>
        RunOwnedRootEquipmentAsync("manual", async fixture =>
    {
        await fixture.Query(); await fixture.Cast(1000);
        Assert.True(fixture.HasFlag(9)); Assert.True(fixture.HasFlag(8));
        Assert.NotNull(fixture.ActualClock(9)); Assert.NotNull(fixture.ActualClock(8));
        var binding = fixture.Owner.NativeGameClient!.OrdinarySpellBinding!;
        var files = fixture.Data.Files(); int start = fixture.Order.Count;
        await fixture.AdvanceRuntimeObserving(TimeSpan.FromSeconds(20), () => !fixture.HasFlag(9) && !fixture.HasFlag(8));
        Assert.Null(fixture.ActualClock(9)); Assert.Null(fixture.ActualClock(8));
        Assert.Same(binding, fixture.Owner.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
        Assert.Equal(new[] { (PlayerUid, 256UL), (PlayerUid, 0UL) },
            fixture.Vectors(PlayerUid, start).Select(value => (PlayerUid, value)));
        Assert.Equal(new[] { (TargetPlayerUid, 256UL), (TargetPlayerUid, 0UL) },
            fixture.Vectors(TargetPlayerUid, start).Select(value => (TargetPlayerUid, value)));
        fixture.Data.AssertFiles(files);
    });
}
