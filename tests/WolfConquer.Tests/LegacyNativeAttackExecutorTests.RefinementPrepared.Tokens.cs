using WolfConquer.Core.World;
using RefineKind = WolfConquer.Core.World.LegacyNativePerfectionEffects.RefinementKind;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    [Theory]
    [InlineData("client")] [InlineData("player")] [InlineData("vector")]
    public Task RefinementTypedAcceptRequiresEveryGenuineGateWithoutConsumingAnUnattemptedToken(string missing)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; var v = p.BitVector;
        var binding = new LegacyNativeRefinementBindings(s.Client, p, v, p.View, RefineKind.Mirror);
        using var publication = PrepareRefinement(binding, out var token);
        var before = RefinementStateSnapshot.Capture(p);
        if (missing == "client")
        {
            lock (p.SpellAdmissionSyncRoot) lock (v.SyncRoot)
                Assert.False(v.TryAcceptRefinement(token, binding, publication, out _));
        }
        else if (missing == "player")
        {
            lock (s.Client.SpellAdmissionSyncRoot) lock (v.SyncRoot)
                Assert.False(v.TryAcceptRefinement(token, binding, publication, out _));
        }
        else
        {
            lock (s.Client.SpellAdmissionSyncRoot) lock (p.SpellAdmissionSyncRoot)
                Assert.False(v.TryAcceptRefinement(token, binding, publication, out _));
        }
        before.Unchanged(p); Assert.Empty(s.Queue);
        lock (s.Client.SpellAdmissionSyncRoot) lock (p.SpellAdmissionSyncRoot) lock (v.SyncRoot)
        {
            Assert.True(v.TryAcceptRefinement(token, binding, publication, out bool granted)); Assert.True(granted);
            Assert.False(v.TryAcceptRefinement(token, binding, publication, out _));
        }
        Assert.Empty(s.Queue); Assert.Equal(20, Assert.Single(v.ArrayFlags).Value.Secounds);
    });

    [Fact]
    public Task RefinementOpaquePlansCannotBeMixedWithOtherBindingsOrPublications()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; var v = p.BitVector;
        lock (s.Client.SpellAdmissionSyncRoot) lock (p.SpellAdmissionSyncRoot) lock (v.SyncRoot)
        {
            var first = new LegacyNativeRefinementBindings(s.Client, p, v, p.View, RefineKind.Mirror);
            var second = new LegacyNativeRefinementBindings(s.Client, p, v, p.View, RefineKind.Mirror);
            using var a = PrepareRefinement(first, out var tokenA);
            using var b = PrepareRefinement(second, out var tokenB);
            var before = RefinementStateSnapshot.Capture(p);
            Assert.Throws<InvalidDataException>(() => LegacyNativePreparedRefinementPublication.Create(second, tokenA));
            Assert.False(v.TryAcceptRefinement(tokenA, first, b, out _));
            before.Unchanged(p); Assert.Empty(s.Queue);
            Assert.False(v.TryAcceptRefinement(tokenA, first, a, out _)); // Attempt consumed, not replayable.
            Assert.True(v.TryAcceptRefinement(tokenB, second, b, out bool grant)); Assert.True(grant);
            Assert.Single(v.ArrayFlags); Assert.Empty(s.Queue);
        }
    });

    [Fact]
    public Task RefinementFinalOwnerRefusalConsumesTokenEvenAfterRawBindingIsRestored()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; var v = p.BitVector;
        lock (s.Client.SpellAdmissionSyncRoot) lock (p.SpellAdmissionSyncRoot) lock (v.SyncRoot)
        {
            var binding = new LegacyNativeRefinementBindings(s.Client, p, v, p.View, RefineKind.Mirror);
            using var publication = PrepareRefinement(binding, out var token);
            var before = RefinementStateSnapshot.Capture(p); p.UID = 18;
            Assert.False(v.TryAcceptRefinement(token, binding, publication, out _));
            p.UID = 17;
            Assert.False(v.TryAcceptRefinement(token, binding, publication, out _));
            before.Unchanged(p); Assert.Empty(s.Queue);
        }
    });

    private static LegacyNativePreparedRefinementPublication PrepareRefinement(LegacyNativeRefinementBindings binding,
        out LegacyNativeStatusFlagsBigVector32.RefinementChange token)
    {
        lock (binding.Vector.SyncRoot)
        {
            Assert.True(binding.Vector.TryPrepareRefinement(binding, out var result)); token = result!;
            return LegacyNativePreparedRefinementPublication.Create(binding, token);
        }
    }
}
