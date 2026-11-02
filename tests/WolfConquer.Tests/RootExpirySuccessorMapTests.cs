using System.Globalization;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootExpirySecondClocklessEntryRequiresUnchangedPublishedSuccessorMap(bool mutate)
    {
        var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 0, NeckEffect: 0), itemEffectLifetimeControls: true,
            clocklessDualControls: true);
        Exception? primary = null; var cleanup = new List<Exception>();
        Task? blocked = null; long? priorGeneration = null; bool drained = false;
        try
        {
            await fixture.QueryClocklessDualThroughFrameAsync();
            await fixture.CastClassicStigmaThroughFrameAsync();
            await fixture.CastClocklessShieldThroughFrameAsync();
            AssertRootManualLifetimeFlags(fixture, stigma: true, shield: true);
            Assert.Null(fixture.ReadActualRootClock(9)); Assert.Null(fixture.ReadActualRootClock(8));
            var stigmaDeadline = DateTimeOffset.Parse(fixture.Owner.Character.Attributes[
                WorldPacketHandlers.RuntimeLegacyStigmaExpiresAtUtcAttribute], CultureInfo.InvariantCulture);
            var shieldDeadline = DateTimeOffset.Parse(fixture.Owner.Character.Attributes[
                WorldPacketHandlers.RuntimeLegacyMagicShieldExpiresAtUtcAttribute], CultureInfo.InvariantCulture);
            var bothDueAt = stigmaDeadline > shieldDeadline ? stigmaDeadline : shieldDeadline;
            var firstAdvance = bothDueAt > fixture.Now ? bothDueAt - fixture.Now + TimeSpan.FromMilliseconds(20) : TimeSpan.FromMilliseconds(20);
            await fixture.StartRootItemEffectRuntimeAsync();
            blocked = fixture.BlockRootPeer(TargetPlayerUid);
            await fixture.WaitRootPeerBlocked(TargetPlayerUid);
            Assert.False(blocked.IsCompleted);
            int first = fixture.Order.Count;
            priorGeneration = await fixture.BeginPausedRootItemEffectAdvanceAsync(firstAdvance);
            Assert.True(fixture.Now >= stigmaDeadline && fixture.Now >= shieldDeadline);
            await WaitUntilAsync(() => fixture.RootExpiryWaiting(TargetPlayerUid), TimeSpan.FromSeconds(5));
            await fixture.DrainRootPeerObservations(PlayerUid);
            Assert.Equal(new ulong[] { 256 }, RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, first)));
            AssertRootManualLifetimeFlags(fixture, stigma: false, shield: true);
            var committedFirst = fixture.Owner;
            var later = fixture.Now.AddMinutes(5);
            if (mutate) fixture.ChangeClocklessShieldDeadlineInCurrentMap(later);
            await fixture.DrainRootPeer(TargetPlayerUid); drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.CompletePausedRootItemEffectAdvanceAsync(priorGeneration.Value);
            if (mutate)
            {
                Assert.Same(committedFirst, fixture.Owner);
                AssertRootManualLifetimeFlags(fixture, stigma: false, shield: true);
                Assert.Equal(later, DateTimeOffset.Parse(fixture.Owner.Character.Attributes[
                    WorldPacketHandlers.RuntimeLegacyMagicShieldExpiresAtUtcAttribute], CultureInfo.InvariantCulture));
                Assert.Equal(new ulong[] { 256 }, RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, first)));
                Assert.Equal(new ulong[] { 256 }, RootManualLifetimeVectors(fixture.FramesSince(TargetPlayerUid, first)));
                int beforeLater = fixture.Order.Count;
                await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMinutes(5) - TimeSpan.FromMilliseconds(20));
                AssertRootManualLifetimeFlags(fixture, stigma: false, shield: true);
                Assert.Equal(later, DateTimeOffset.Parse(fixture.Owner.Character.Attributes[
                    WorldPacketHandlers.RuntimeLegacyMagicShieldExpiresAtUtcAttribute], CultureInfo.InvariantCulture));
                Assert.Empty(RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, beforeLater)));
                Assert.Empty(RootManualLifetimeVectors(fixture.FramesSince(TargetPlayerUid, beforeLater)));
                int laterOutput = fixture.Order.Count;
                await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
                AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
                Assert.False(fixture.Owner.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyMagicShieldExpiresAtUtcAttribute));
                Assert.Equal(new ulong[] { 0 }, RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, laterOutput)));
                Assert.Equal(new ulong[] { 0 }, RootManualLifetimeVectors(fixture.FramesSince(TargetPlayerUid, laterOutput)));
            }
            else
            {
                AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
                Assert.Equal(new ulong[] { 256, 0 }, RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, first)));
                Assert.Equal(new ulong[] { 256, 0 }, RootManualLifetimeVectors(fixture.FramesSince(TargetPlayerUid, first)));
            }
        }
        catch (Exception error) { primary = error; }
        finally
        {
            if (!drained && blocked is not null)
            {
                try { fixture.CloseRootPeer(TargetPlayerUid); }
                catch (Exception error) { cleanup.Add(error); }
                try { await blocked.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) when (error is IOException or System.Net.Sockets.SocketException or ObjectDisposedException) { }
                catch (Exception error) { cleanup.Add(error); }
                if (priorGeneration is { } prior)
                    try { await fixture.CompletePausedRootItemEffectAdvanceAsync(prior); }
                    catch (Exception error) { cleanup.Add(error); }
            }
            try { await fixture.DisposeAsync(); }
            catch (Exception error) { cleanup.Add(error); }
        }
        if (primary is not null && cleanup.Count != 0)
            throw new AggregateException("Successor-map assertion and cleanup both failed.", new[] { primary }.Concat(cleanup));
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup.Count == 1) ExceptionDispatchInfo.Capture(cleanup[0]).Throw();
        if (cleanup.Count > 1) throw new AggregateException("Successor-map cleanup failed.", cleanup);
    }
}
