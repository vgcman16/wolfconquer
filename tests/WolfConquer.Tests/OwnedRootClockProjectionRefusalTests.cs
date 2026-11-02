using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData("LegacyRootItemEffectFlag.8.NativeOwned")]
    [InlineData("LegacyMagicShieldExpiresAtUtc")]
    [InlineData("LegacyMagicShieldRemoveOnDead")]
    [InlineData("StatusFlags")]
    public Task OwnedRootStoreRefusesAmbiguousClockProjectionAndExpiresRealClock(string key) =>
        RunOwnedRootEquipmentAsync("expiry-authority", async fixture =>
    {
        await fixture.Query();
        await fixture.Cast(1000); // Actual equipped necklace proc inserts the real Shield8 clock.
        Assert.True(fixture.HasFlag(8));
        var clock = Assert.IsType<LegacyRootItemEffectFlagClock>(fixture.ActualClock(8));
        var deadline = fixture.Deadline(8);
        Assert.True(deadline > fixture.Now);
        var owner = fixture.Owner;
        var binding = owner.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
        var native = (binding.Player.HitPoints, binding.Player.Mana, binding.Player.Stamina);
        var files = fixture.Data.Files();
        var frames = fixture.Order.Count;

        // Exercise actual Store.Set and native ownership; implementations may
        // refuse in the native guard before reaching the clock proof.
        var failure = Record.Exception(() => fixture.AttemptAmbiguousRootProjectionThroughStore(key));
        Assert.NotNull(failure);
        Assert.True(failure is InvalidDataException or InvalidOperationException or ArgumentException,
            $"Unexpected refusal type: {failure}");
        Assert.Same(owner, fixture.Owner);
        Assert.Same(clock, fixture.ActualClock(8));
        Assert.True(fixture.HasFlag(8));
        Assert.Equal(deadline, fixture.Deadline(8));
        Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
        Assert.Equal(native, (binding.Player.HitPoints, binding.Player.Mana, binding.Player.Stamina));
        Assert.Equal(frames, fixture.Order.Count);
        fixture.Data.AssertFiles(files);

        // This predicate is false before advancing; successful completion thus
        // requires the actual global worker to retire the real Shield clock.
        Assert.True(fixture.HasFlag(8));
        await fixture.AdvanceRuntimeObserving(deadline - fixture.Now + TimeSpan.FromSeconds(1), () => !fixture.HasFlag(8));
        Assert.False(fixture.HasFlag(8));
        Assert.Null(fixture.ActualClock(8));
        Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
        Assert.Same(binding, fixture.Owner.NativeGameClient!.OrdinarySpellBinding);
    });
}
