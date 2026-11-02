using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public Task BackpressuredRealTcpPeerDoesNotBlockIndependentOwnedCombat() =>
        RunOwnedRootEquipmentAsync("independent", async fixture =>
    {
        await fixture.Query();
        await fixture.QueryIndependent(OwnedRootEquipmentFixture.IndependentCasterUid);
        await fixture.QueryIndependent(OwnedRootEquipmentFixture.IndependentTargetUid);
        await fixture.Cast(1000);
        var target = fixture.Current(OwnedRootEquipmentFixture.IndependentTargetUid);
        var binding = target.NativeGameClient!.OrdinarySpellBinding!;
        var priorHp = binding.Player.HitPoints;
        // The genuine loopback stream has no reader during this oversized write;
        // it occupies the same SemaphoreSlim used by the expiry writer.
        Task blocked = fixture.StartBackpressuredWrite(TargetPlayerUid);
        bool drained = false, started = false, completed = false;
        await PreserveOwnedPauseFailuresAsync(async () =>
        {
            await fixture.WaitForBackpressure(TargetPlayerUid);
            Assert.True(fixture.ActualBackpressure(TargetPlayerUid));
            Assert.False(blocked.IsCompleted); // The OS TCP send is actually backpressured.
            var outputStart = fixture.Order.Count;
            await fixture.BeginPausedRuntimeAdvance(TimeSpan.FromSeconds(20)); started = true;
            var limit = DateTime.UtcNow.AddSeconds(5);
            while (fixture.HasFlag(9) && DateTime.UtcNow < limit) await Task.Delay(10);
            Assert.False(fixture.HasFlag(9)); // Actual expiry commit happened.
            limit = DateTime.UtcNow.AddSeconds(5);
            while (!fixture.ExpiryWaitingAtRealGate(TargetPlayerUid) && DateTime.UtcNow < limit) await Task.Delay(10);
            Assert.True(fixture.ExpiryWaitingAtRealGate(TargetPlayerUid));
            var starts = fixture.WireStarts(TargetPlayerUid);
            Assert.False(fixture.AttemptUngatedActualExpiryPermit(TargetPlayerUid));
            Assert.Equal(starts, fixture.WireStarts(TargetPlayerUid));
            limit = DateTime.UtcNow.AddSeconds(5);
            while (fixture.Vectors(PlayerUid, outputStart).Length == 0 && DateTime.UtcNow < limit) await Task.Delay(10);
            Assert.NotEmpty(fixture.Vectors(PlayerUid, outputStart));
            Assert.True(fixture.ExpiryWaitingAtRealGate(TargetPlayerUid)); // The real viewer gate remains occupied.
            await fixture.CastIndependent().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(binding.Player.HitPoints < priorHp);
            var after = fixture.Current(OwnedRootEquipmentFixture.IndependentTargetUid);
            Assert.Equal(after.Character.MinHitPoints, (long)binding.Player.HitPoints);
            Assert.Equal(after.Character.MinHitPoints,
                Assert.IsType<WolfConquer.Core.Data.CharacterSnapshot>(fixture.Data.Loader.LoadByAccountId(
                    OwnedRootEquipmentFixture.IndependentTargetUid)).MinHitPoints);
            Assert.False(blocked.IsCompleted);
            await fixture.DrainBackpressuredWrite(TargetPlayerUid); drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.CompletePausedRuntimeAdvance(() => !fixture.HasFlag(9)); completed = true;
        }, () => FinishOwnedPauseAsync(fixture, TargetPlayerUid, blocked, drained, started, completed));
    });
    [Fact]
    public Task ExpiryGatePublishesFreshVectorAfterRealHpSuccessor() =>
        RunOwnedRootEquipmentAsync("successor", async fixture =>
    {
        await fixture.Query();
        await fixture.QueryIndependent(OwnedRootEquipmentFixture.IndependentCasterUid);
        await fixture.Cast(1000);
        Assert.True(fixture.HasFlag(9)); Assert.True(fixture.HasFlag(8));
        Task blocked = fixture.StartBackpressuredWrite(PlayerUid);
        Task? damage = null;
        bool drained = false, started = false, completed = false;
        await PreserveOwnedPauseFailuresAsync(async () =>
        {
            await fixture.WaitForBackpressure(PlayerUid);
            Assert.True(fixture.ActualBackpressure(PlayerUid));
            Assert.False(blocked.IsCompleted);
            int outputStart = fixture.Order.Count;
            await fixture.BeginPausedRuntimeAdvance(TimeSpan.FromSeconds(20)); started = true;
            var limit = DateTime.UtcNow.AddSeconds(5);
            while (fixture.HasFlag(9) && DateTime.UtcNow < limit) await Task.Delay(10);
            Assert.False(fixture.HasFlag(9));
            limit = DateTime.UtcNow.AddSeconds(5);
            while (!fixture.ExpiryWaitingAtRealGate(PlayerUid) && DateTime.UtcNow < limit) await Task.Delay(10);
            Assert.True(fixture.ExpiryWaitingAtRealGate(PlayerUid));
            Assert.True(fixture.HasFlag(8));
            var expired = fixture.Owner;
            var hp = expired.Character.MinHitPoints;
            damage = fixture.DamageOwnerFromIndependent();
            limit = DateTime.UtcNow.AddSeconds(5);
            while (fixture.Owner.Character.MinHitPoints == hp && !damage.IsCompleted && DateTime.UtcNow < limit) await Task.Delay(10);
            var successor = fixture.Owner;
            Assert.NotSame(expired, successor);
            Assert.True(successor.Character.MinHitPoints < hp);
            Assert.True(fixture.HasFlag(8));
            Assert.False(blocked.IsCompleted);
            await fixture.DrainBackpressuredWrite(PlayerUid);
            drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
            await damage.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.CompletePausedRuntimeAdvance(() => !fixture.HasFlag(9)); completed = true;
            Assert.Contains(256UL, fixture.Vectors(PlayerUid, outputStart));
            Assert.True(fixture.HasFlag(8)); Assert.False(fixture.HasFlag(9));
            var binding = fixture.Owner.NativeGameClient!.OrdinarySpellBinding!;
            Assert.Equal(fixture.Owner.Character.MinHitPoints, (long)binding.Player.HitPoints);
            Assert.Equal(fixture.Owner.Character.MinHitPoints,
                Assert.IsType<WolfConquer.Core.Data.CharacterSnapshot>(fixture.Data.Loader.LoadByAccountId(PlayerUid)).MinHitPoints);
        }, () => FinishOwnedPauseAsync(fixture, PlayerUid, blocked, drained, started, completed, damage));
    });

    [Fact]
    public Task ExpiryGateUsesGenuineReplacementGenerationInsteadOfOldVector() =>
        RunOwnedRootEquipmentAsync("generation", async fixture =>
    {
        await fixture.Query(); await fixture.Cast(1000);
        var old = Assert.IsType<LegacyRootItemEffectFlagClock>(fixture.ActualClock(9));
        Assert.True(fixture.HasFlag(9)); Assert.True(fixture.HasFlag(8));
        Task blocked = fixture.StartBackpressuredWrite(TargetPlayerUid);
        Task? replacementCast = null;
        bool drained = false, started = false, completed = false;
        await PreserveOwnedPauseFailuresAsync(async () =>
        {
            await fixture.WaitForBackpressure(TargetPlayerUid);
            Assert.True(fixture.ActualBackpressure(TargetPlayerUid));
            Assert.False(blocked.IsCompleted);
            int start = fixture.Order.Count;
            await fixture.BeginPausedRuntimeAdvance(TimeSpan.FromSeconds(20)); started = true;
            var limit = DateTime.UtcNow.AddSeconds(5);
            while ((fixture.HasFlag(9) || fixture.Vectors(PlayerUid, start).Length == 0) && DateTime.UtcNow < limit) await Task.Delay(10);
            Assert.False(fixture.HasFlag(9));
            limit = DateTime.UtcNow.AddSeconds(5);
            while (!fixture.ExpiryWaitingAtRealGate(TargetPlayerUid) && DateTime.UtcNow < limit) await Task.Delay(10);
            Assert.True(fixture.ExpiryWaitingAtRealGate(TargetPlayerUid));
            Assert.Contains(256UL, fixture.Vectors(PlayerUid, start));
            Assert.True(fixture.ExpiryWaitingAtRealGate(TargetPlayerUid));
            replacementCast = fixture.Cast(1000); // Actual ordinary request, resources and item-effect writer.
            limit = DateTime.UtcNow.AddSeconds(5);
            while ((!fixture.HasFlag(9) || ReferenceEquals(fixture.ActualClock(9), old)) && !replacementCast.IsCompleted && DateTime.UtcNow < limit)
                await Task.Delay(10);
            var replacement = Assert.IsType<LegacyRootItemEffectFlagClock>(fixture.ActualClock(9));
            Assert.NotSame(old, replacement);
            Assert.True(fixture.HasFlag(9), "The actual replacement Stigma must be visible.");
            Assert.True(fixture.HasFlag(8), "The captured other Shield must still be visible at this stage.");
            Assert.False(blocked.IsCompleted);
            await fixture.DrainBackpressuredWrite(TargetPlayerUid);
            drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
            await replacementCast.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.CompletePausedRuntimeAdvance(() => fixture.HasFlag(9)); completed = true;
            var viewerVectors = fixture.Vectors(TargetPlayerUid, start);
            Assert.NotEmpty(viewerVectors);
            Assert.Equal(768UL, viewerVectors[0]); // The retired expiry image (256) never reaches this gate.
            Assert.Same(replacement, fixture.ActualClock(9));
            Assert.True(fixture.HasFlag(9)); Assert.True(fixture.HasFlag(8));
        }, () => FinishOwnedPauseAsync(fixture, TargetPlayerUid, blocked, drained, started, completed, replacementCast));
    });


    [Fact]
    public Task MpPrefixPrecedesARealReplacementClockWhileItsPeerIsStalled() =>
        RunOwnedRootEquipmentAsync("manual", async fixture =>
    {
        await fixture.Query(); await fixture.Cast(1000);
        var old = Assert.IsType<LegacyRootItemEffectFlagClock>(fixture.ActualClock(9));
        Task blocked = fixture.StartBackpressuredWrite(TargetPlayerUid);
        Task? cast = null;
        bool drained = false, started = false, completed = false;
        await PreserveOwnedPauseFailuresAsync(async () =>
        {
            await fixture.WaitForBackpressure(TargetPlayerUid);
            int start = fixture.Order.Count;
            await fixture.BeginPausedRuntimeAdvance(TimeSpan.FromSeconds(20)); started = true;
            await WaitUntilAsync(() => fixture.ExpiryWaitingAtRealGate(TargetPlayerUid), TimeSpan.FromSeconds(5));
            Assert.False(fixture.HasFlag(9)); Assert.True(fixture.HasFlag(8));
            cast = fixture.Cast(1000);
            await WaitUntilAsync(() => fixture.Animations(PlayerUid, start).Any(frame => frame.SpellId == 1175),
                TimeSpan.FromSeconds(5));
            Assert.False(cast.IsCompleted);
            Assert.True(fixture.ActualBackpressure(TargetPlayerUid));
            Assert.False(fixture.HasFlag(9));
            Assert.Null(fixture.ActualClock(9)); // Real MP presentation precedes the Ring installation.
            await fixture.DrainBackpressuredWrite(TargetPlayerUid); drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
            await cast.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.CompletePausedRuntimeAdvance(() => fixture.HasFlag(9) && fixture.HasFlag(8)); completed = true;
            Assert.NotSame(old, Assert.IsType<LegacyRootItemEffectFlagClock>(fixture.ActualClock(9)));
            Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(fixture.Owner.NativeGameClient!.OrdinarySpellBinding!));
        }, () => FinishOwnedPauseAsync(fixture, TargetPlayerUid, blocked, drained, started, completed, cast));
    });

}
