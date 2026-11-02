using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public Task OwnedRootCapturedOtherShieldSurvivesRealPirateDispelAndReinstall() =>
        RunOwnedRootEquipmentAsync("manual-other", async fixture =>
    {
        await fixture.Query(); await fixture.Cast(1000);
        var oldShield = Assert.IsType<LegacyRootItemEffectFlagClock>(fixture.ActualClock(8));
        Task blocked = fixture.StartBackpressuredWrite(OwnedRootEquipmentFixture.ExpiryObserverUid);
        Task? dispel = null, recast = null;
        bool drained = false, started = false, completed = false;
        await PreserveOwnedPauseFailuresAsync(async () =>
        {
            await fixture.WaitForBackpressure(OwnedRootEquipmentFixture.ExpiryObserverUid);
            Assert.True(fixture.ActualBackpressure(OwnedRootEquipmentFixture.ExpiryObserverUid));
            Assert.False(blocked.IsCompleted);
            int start = fixture.Order.Count;
            await fixture.BeginPausedRuntimeAdvance(TimeSpan.FromSeconds(20)); started = true;
            var limit = DateTime.UtcNow.AddSeconds(5);
            while ((fixture.HasFlag(9) || fixture.Vectors(PlayerUid, start).Length == 0) && DateTime.UtcNow < limit) await Task.Delay(10);
            Assert.False(fixture.HasFlag(9)); Assert.True(fixture.HasFlag(8));
            limit = DateTime.UtcNow.AddSeconds(5);
            while (!fixture.ExpiryWaitingAtRealGate(OwnedRootEquipmentFixture.ExpiryObserverUid) && DateTime.UtcNow < limit) await Task.Delay(10);
            Assert.True(fixture.ExpiryWaitingAtRealGate(OwnedRootEquipmentFixture.ExpiryObserverUid));
            Assert.Contains(256UL, fixture.Vectors(PlayerUid, start));
            Assert.Same(oldShield, fixture.ActualClock(8));
            dispel = fixture.DispelOwner(); // Real pirate-owned public Frame.
            limit = DateTime.UtcNow.AddSeconds(5);
            while (fixture.HasFlag(8) && !dispel.IsCompleted && DateTime.UtcNow < limit) await Task.Delay(10);
            Assert.False(fixture.HasFlag(8));
            Assert.False(dispel.IsCompleted); // Its real viewer transport is still blocked.
            recast = fixture.Cast(1000); // FIFO exclusive request queues before the tick can reacquire Shared.
            Assert.False(recast.IsCompleted);
            await fixture.DrainBackpressuredWrite(OwnedRootEquipmentFixture.ExpiryObserverUid);
            drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
            await dispel.WaitAsync(TimeSpan.FromSeconds(5));
            await recast.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.CompletePausedRuntimeAdvance(() => fixture.HasFlag(8) && fixture.HasFlag(9)); completed = true;
            var replacement = Assert.IsType<LegacyRootItemEffectFlagClock>(fixture.ActualClock(8));
            Assert.NotSame(oldShield, replacement);
            Assert.Equal(fixture.Now.AddSeconds(15), fixture.Deadline(8));
            Assert.True(fixture.HasFlag(8)); Assert.True(fixture.HasFlag(9));
            var actor = fixture.Owner.NativeGameClient!.OrdinarySpellBinding!;
            Assert.Equal(fixture.Owner.Character.MinHitPoints, (long)actor.Player.HitPoints);
            Assert.Equal(fixture.Owner.Character.MinHitPoints,
                Assert.IsType<WolfConquer.Core.Data.CharacterSnapshot>(fixture.Data.Loader.LoadByAccountId(PlayerUid)).MinHitPoints);
        }, () => FinishOwnedPauseAsync(fixture, OwnedRootEquipmentFixture.ExpiryObserverUid, blocked, drained, started, completed, dispel, recast));
    });

}
