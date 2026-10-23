using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    [Theory]
    [InlineData("client")] [InlineData("player")] [InlineData("vector")]
    public Task DivineGuardPreparedTypedAcceptanceNeedsAllGatesWithoutConsumingUnattemptedPlan(string missing)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; var v = p.BitVector;
        p.LastDivineGuard = new DateTime(2026, 1, 3);
        var bindings = new LegacyNativeDivineGuardBindings(s.Client, p, v, p.View);
        using var publication = PrepareGuard(bindings, out var token);
        var before = RefinementStateSnapshot.Capture(p); var stamp = p.LastDivineGuard;
        if (missing == "client")
        {
            lock (p.SpellAdmissionSyncRoot) lock (v.SyncRoot)
                Assert.False(v.TryAcceptDivineGuard(token, bindings, publication, out _));
        }
        else if (missing == "player")
        {
            lock (s.Client.SpellAdmissionSyncRoot) lock (v.SyncRoot)
                Assert.False(v.TryAcceptDivineGuard(token, bindings, publication, out _));
        }
        else
        {
            lock (s.Client.SpellAdmissionSyncRoot) lock (p.SpellAdmissionSyncRoot)
                Assert.False(v.TryAcceptDivineGuard(token, bindings, publication, out _));
        }
        before.Unchanged(p); Assert.Equal(stamp, p.LastDivineGuard); Assert.Empty(s.Queue);
        LegacyNativePoolRandom.GetRandom = new(SeedFor(5, true));
        lock (s.Client.SpellAdmissionSyncRoot) lock (p.SpellAdmissionSyncRoot) lock (v.SyncRoot)
        {
            Assert.True(v.TryAcceptDivineGuard(token, bindings, publication, out bool grant)); Assert.True(grant);
            Assert.Equal(bindings.Stamp, p.LastDivineGuard);
            Assert.False(v.TryAcceptDivineGuard(token, bindings, publication, out _));
            Assert.Equal(bindings.Stamp, p.LastDivineGuard);
        }
        Assert.Equal(15, Assert.Single(v.ArrayFlags).Value.Secounds); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task DivineGuardPreparedOpaqueBindingsAndPublicationsCannotBeMixed()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; var v = p.BitVector;
        p.LastDivineGuard = new DateTime(2026, 1, 3);
        lock (s.Client.SpellAdmissionSyncRoot) lock (p.SpellAdmissionSyncRoot) lock (v.SyncRoot)
        {
            var first = new LegacyNativeDivineGuardBindings(s.Client, p, v, p.View);
            var second = new LegacyNativeDivineGuardBindings(s.Client, p, v, p.View);
            using var a = PrepareGuard(first, out var tokenA);
            using var b = PrepareGuard(second, out var tokenB);
            var before = RefinementStateSnapshot.Capture(p); var stamp = p.LastDivineGuard;
            var rng = new LegacyNativeSafeRandom(221); LegacyNativePoolRandom.GetRandom = new(221);
            Assert.Throws<InvalidDataException>(() => LegacyNativePreparedDivineGuardPublication.Create(second, tokenA));
            Assert.False(v.TryAcceptDivineGuard(tokenA, first, b, out _));
            Assert.False(v.TryAcceptDivineGuard(tokenA, first, a, out _)); // Attempted token cannot replay.
            before.Unchanged(p); Assert.Equal(stamp, p.LastDivineGuard); Assert.Empty(s.Queue);
            Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
            LegacyNativePoolRandom.GetRandom = new(SeedFor(5, true));
            Assert.True(v.TryAcceptDivineGuard(tokenB, second, b, out bool grant)); Assert.True(grant);
            Assert.Equal(second.Stamp, p.LastDivineGuard); Assert.Empty(s.Queue);
        }
    });

    [Fact]
    public Task DivineGuardPreparedRefusedStampDriftCannotReplayAfterRestoration()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; var v = p.BitVector;
        p.LastDivineGuard = new DateTime(2026, 1, 3);
        lock (s.Client.SpellAdmissionSyncRoot) lock (p.SpellAdmissionSyncRoot) lock (v.SyncRoot)
        {
            var bindings = new LegacyNativeDivineGuardBindings(s.Client, p, v, p.View);
            using var publication = PrepareGuard(bindings, out var token);
            var before = RefinementStateSnapshot.Capture(p); var stamp = p.LastDivineGuard;
            p.LastDivineGuard = stamp.AddDays(1);
            Assert.False(v.TryAcceptDivineGuard(token, bindings, publication, out _));
            Assert.Equal(stamp.AddDays(1), p.LastDivineGuard);
            p.LastDivineGuard = stamp;
            Assert.False(v.TryAcceptDivineGuard(token, bindings, publication, out _));
            before.Unchanged(p); Assert.Equal(stamp, p.LastDivineGuard); Assert.Empty(s.Queue);
        }
    });

    [Fact]
    public Task DivineGuardPreparedPresentRegistrationDoesNotRequireAnUnrelatedChanceOwner()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player;
        Assert.True(p.BitVector.TryAdd((int)Flag.DivineGuard, 9, false, 2));
        var before = RefinementStateSnapshot.Capture(p); var rng = LegacyNativePoolRandom.GetRandom;
        DateTime begin = DateTime.Now.AddMinutes(10);
        try
        {
            LegacyNativePoolRandom.GetRandom = null!;
            Assert.True(LegacyNativePerfectionEffects.TryDivineGuard(s.Client));
        }
        finally { LegacyNativePoolRandom.GetRandom = rng; }
        before.Unchanged(p); Assert.InRange(p.LastDivineGuard, begin, DateTime.Now.AddMinutes(10));
        Assert.Equal(Refine(17, 17, 11), Assert.Single(s.Queue));
    });
}
