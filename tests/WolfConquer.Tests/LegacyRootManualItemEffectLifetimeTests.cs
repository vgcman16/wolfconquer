using System.Globalization;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task RootManualEarnedNeckFifteenAndRingTwentyExpireAtInclusiveBoundaries()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true);
        await fixture.QueryRootItemEffectsAsync();
        await fixture.CastAsync(1_000);
        AssertRootManualLifetimeFlags(fixture, stigma: true, shield: true);
        Assert.Equal(fixture.Now.AddSeconds(15), RootManualLifetimeDeadline(fixture, 8));
        Assert.Equal(fixture.Now.AddSeconds(20), RootManualLifetimeDeadline(fixture, 9));
        await fixture.StartRootItemEffectRuntimeAsync();
        var start = fixture.Order.Count;
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(14_980));
        AssertRootManualLifetimeFlags(fixture, stigma: true, shield: true);
        Assert.Empty(RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, start)));
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
        AssertRootManualLifetimeFlags(fixture, stigma: true, shield: false);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            Assert.Equal(new ulong[] { 512 }, RootManualLifetimeVectors(fixture.FramesSince(uid, start)));
        var ringStart = fixture.Order.Count;
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(4_980));
        AssertRootManualLifetimeFlags(fixture, stigma: true, shield: false);
        Assert.Empty(RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, ringStart)));
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
        AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            Assert.Equal(new ulong[] { 0 }, RootManualLifetimeVectors(fixture.FramesSince(uid, ringStart)));
    }

    [Fact]
    public async Task RootManualTwoDueFlagsPublishTwoIndividualOwnerPeerVectorsNotOneAtomicClear()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true);
        await fixture.QueryRootItemEffectsAsync();
        await fixture.CastAsync(1_000);
        await fixture.StartRootItemEffectRuntimeAsync();
        var start = fixture.Order.Count;
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(20));
        AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
        var own = RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, start));
        var peer = RootManualLifetimeVectors(fixture.FramesSince(TargetPlayerUid, start));
        Assert.Equal(2, own.Length);
        Assert.Contains(own[0], new ulong[] { 256, 512 }); // No native dictionary priority is invented.
        Assert.Equal(0UL, own[1]);
        Assert.Equal(own, peer);
        var all = fixture.Order.ToArray().Skip(start)
            .Where(entry => RootManualItemBits(entry.Bytes) is not null).ToArray();
        Assert.Equal(4, all.Length);
        // Independent TCP readers may interleave. Each recipient must receive
        // both individual vectors in the exact order asserted above.
        Assert.All(all, entry => Assert.Contains(entry.Recipient, new[] { PlayerUid, TargetPlayerUid }));
    }

    [Fact]
    public async Task RootManualAlreadyActiveProcDoesNotReplaceEarnedFifteenOrTwentySecondRegistration()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true);
        await fixture.QueryRootItemEffectsAsync();
        await fixture.CastAsync(1_000);
        var shieldDeadline = RootManualLifetimeDeadline(fixture, 8);
        var stigmaDeadline = RootManualLifetimeDeadline(fixture, 9);
        await fixture.StartRootItemEffectRuntimeAsync();
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(10));
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000); // A real later public input; not a timer or flag mutation.
        fixture.AssertProducedStamp(fixture.Now);
        Assert.Equal(shieldDeadline, RootManualLifetimeDeadline(fixture, 8));
        Assert.Equal(stigmaDeadline, RootManualLifetimeDeadline(fixture, 9));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            Assert.DoesNotContain(ParseSpellAnimations(fixture.FramesSince(uid, start)),
                packet => packet.SpellId is 1_095 or 1_020);
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(5));
        AssertRootManualLifetimeFlags(fixture, stigma: true, shield: false); // Original W15, not W25.
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(5));
        AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false); // Original W20, not W30.
        // Output cannot independently prove a consumed no-output rate draw; source still draws before presence.
    }

    [Fact]
    public async Task RootManualCompletedPrefixRealDeathRetiresBothFlagsWithoutFutureExpiryPublication()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true,
            nativeDeathCasterQueryControls: true);
        await fixture.PrepareRootNativeDeathCasterAsync();
        await fixture.QueryRootItemEffectsAsync();
        await fixture.CastAsync(1_000);
        await fixture.StartRootItemEffectRuntimeAsync();
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(5_000L, fixture.Owner.Character.MinHitPoints);
        AssertRootManualLifetimeFlags(fixture, stigma: true, shield: true);
        Assert.Equal(fixture.Now.AddSeconds(10), RootManualLifetimeDeadline(fixture, 8)); // Earned at W0; original W15.
        Assert.Equal(fixture.Now.AddSeconds(15), RootManualLifetimeDeadline(fixture, 9)); // Earned at W0; original W20.
        await fixture.KillRootItemEffectOwnerAsync(); // Queried peer MA1 + raw9999, then native player /2 = lethal5000.
        Assert.Equal(0L, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
        var start = fixture.Order.Count;
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(15));
        AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
        Assert.Equal(0L, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 10));
        Assert.Equal(1_003L, fixture.Owner.Character.Body);
        Assert.Equal(0L, fixture.Owner.Character.Face);
        Assert.Equal("98", fixture.Owner.Character.Attributes["TransformationID"]);
        var own = fixture.FramesSince(PlayerUid, start);
        var peer = fixture.FramesSince(TargetPlayerUid, start);
        // Native Ghost AddFlag vector, then TransformationID setter Mesh, then owner MapStatus.
        Assert.Equal(new[] { PlayerUid, TargetPlayerUid, PlayerUid, TargetPlayerUid, PlayerUid },
            fixture.Order.ToArray().Skip(start).Select(entry => entry.Recipient).ToArray());
        Assert.Equal(new ushort[] { LegacyMsgUpdatePacket.Opcode, LegacyMsgUpdatePacket.Opcode,
                LegacyMapStatusPacket.Opcode },
            own.Select(bytes => ReadFrame(bytes).Header.Opcode).ToArray());
        Assert.Equal(new ushort[] { LegacyMsgUpdatePacket.Opcode, LegacyMsgUpdatePacket.Opcode },
            peer.Select(bytes => ReadFrame(bytes).Header.Opcode).ToArray());
        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(own[2]), out var map));
        Assert.Equal(1_000U, map.Id);
        Assert.Equal(1_000U, map.BaseId);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var updates = ParseMsgUpdates(fixture.FramesSince(uid, start));
            Assert.Equal(2, updates.Count);
            Assert.All(updates, packet => Assert.Equal(PlayerUid, packet.Uid));
            var flag = Assert.Single(updates[0].Updates);
            Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, flag.Type);
            Assert.NotNull(flag.Values);
            Assert.Equal(18, flag.Values.Count);
            Assert.Equal(1_056UL, flag.Values[0]); // FULL first word: Dead5 | Ghost10; no8/9 or unexpected bits.
            Assert.All(flag.Values.Skip(1), value => Assert.Equal(0UL, value));
            Assert.Equal(new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mesh, 980_001_003UL),
                Assert.Single(updates[1].Updates)); //98*10_000_000 + Face0*10_000 + Body1003.
            Assert.Equal(new ulong[] { 0 }, RootManualLifetimeVectors(fixture.FramesSince(uid, start)));
        } // Exact five owed frames: missing Mesh OR extra item-expiry traffic must fail.
        // Only completed-prefix death-before-capture. An already-entered XP1020 tail may still add8 after nested death.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootManualExpiryOriginalOwnerLossStopsPeerAndSecondCapturedFlagAfterEnteredOwnerVector(bool rebind)
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true);
        await fixture.QueryRootItemEffectsAsync(); await fixture.CastAsync(1_000);
        var peerHp = fixture.Peer.Character.MinHitPoints;
        await fixture.StartRootItemEffectRuntimeAsync();
        Task blocked = fixture.BlockRootPeer(TargetPlayerUid);
        long? pausedGeneration = null; bool drained = false;
        try
        {
            await fixture.WaitRootPeerBlocked(TargetPlayerUid);
            Assert.False(blocked.IsCompleted);
            int start = fixture.Order.Count;
            pausedGeneration = await fixture.BeginPausedRootItemEffectAdvanceAsync(TimeSpan.FromSeconds(20));
            await WaitUntilAsync(() => fixture.RootExpiryWaiting(TargetPlayerUid), TimeSpan.FromSeconds(5));
            await fixture.DrainRootPeerObservations(PlayerUid);
            Assert.Single(RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, start)));
            var replacement = fixture.RetireRootItemEffectSubject(PlayerUid, rebind);
            await fixture.DrainRootPeer(TargetPlayerUid); drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5)); await fixture.CompletePausedRootItemEffectAdvanceAsync(pausedGeneration.Value);
            Assert.Empty(RootManualLifetimeVectors(fixture.FramesSince(TargetPlayerUid, start)));
            Assert.Equal(peerHp, fixture.Peer.Character.MinHitPoints);
            if (rebind) { Assert.Same(replacement, fixture.RootItemEffectSlot(PlayerUid)); Assert.Equal((long)PlayerUid + 9_000, replacement!.Character.Uid); }
            else Assert.Null(fixture.RootItemEffectSlot(PlayerUid));
        }
        finally
        {
            if (!drained) fixture.CloseRootPeer(TargetPlayerUid);
            if (pausedGeneration is { } generation) await fixture.CompletePausedRootItemEffectAdvanceAsync(generation);
        }
    }

    [Fact]
    public async Task RootManualExpiryRealReinsertionOfProcessedFlagSurvivesOtherCapturedDueEntry()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true);
        await fixture.QueryRootItemEffectsAsync(); await fixture.CastAsync(1_000);
        var old = Assert.IsType<LegacyRootItemEffectFlagClock>(fixture.ReadActualRootClock(9));
        await fixture.StartRootItemEffectRuntimeAsync();
        Task blocked = fixture.BlockRootPeer(TargetPlayerUid); long? pausedGeneration = null; Task? cast = null; bool drained = false;
        try
        {
            await fixture.WaitRootPeerBlocked(TargetPlayerUid);
            pausedGeneration = await fixture.BeginPausedRootItemEffectAdvanceAsync(TimeSpan.FromSeconds(20));
            await WaitUntilAsync(() => fixture.RootExpiryWaiting(TargetPlayerUid), TimeSpan.FromSeconds(5));
            AssertRootManualLifetimeFlags(fixture, stigma: false, shield: true);
            cast = fixture.CastAsync(1_000);
            await WaitUntilAsync(() => HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 9) &&
                !ReferenceEquals(fixture.ReadActualRootClock(9), old), TimeSpan.FromSeconds(5));
            var replacement = Assert.IsType<LegacyRootItemEffectFlagClock>(fixture.ReadActualRootClock(9));
            await fixture.DrainRootPeer(TargetPlayerUid); drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5)); await cast.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.CompletePausedRootItemEffectAdvanceAsync(pausedGeneration.Value);
            Assert.Same(replacement, fixture.ReadActualRootClock(9));
            AssertRootManualLifetimeFlags(fixture, stigma: true, shield: true);
            await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
            AssertRootManualLifetimeFlags(fixture, stigma: true, shield: false);
            Assert.Equal(fixture.Now.AddSeconds(20).AddMilliseconds(-20), RootManualLifetimeDeadline(fixture, 9));
            await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(19_960));
            AssertRootManualLifetimeFlags(fixture, stigma: true, shield: false);
            await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
            AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
        }
        finally
        {
            if (!drained) fixture.CloseRootPeer(TargetPlayerUid);
            if (cast is not null) await cast.WaitAsync(TimeSpan.FromSeconds(5));
            if (pausedGeneration is { } generation) await fixture.CompletePausedRootItemEffectAdvanceAsync(generation);
        }
    }

    [Fact]
    public async Task RootExpirySameUidStoreSuccessorPublishesFreshPeerVectorAndStopsOldBatch()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true);
        await fixture.QueryRootItemEffectsThroughFrameAsync(); await fixture.CastAsync(1_000);
        await fixture.StartRootItemEffectRuntimeAsync();
        Task blocked = fixture.BlockRootPeer(TargetPlayerUid); long? pausedGeneration = null; bool drained = false;
        try
        {
            await fixture.WaitRootPeerBlocked(TargetPlayerUid);
            int start = fixture.Order.Count;
            pausedGeneration = await fixture.BeginPausedRootItemEffectAdvanceAsync(TimeSpan.FromSeconds(20));
            await WaitUntilAsync(() => fixture.RootExpiryWaiting(TargetPlayerUid), TimeSpan.FromSeconds(5));
            await fixture.DrainRootPeerObservations(PlayerUid);
            Assert.Equal(new ulong[] { 256 }, RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, start)));
            var prior = fixture.Owner;
            fixture.MoveRootItemEffectOwner(835, 585); // Actual same-UID Store publisher.
            var successor = fixture.Owner; Assert.NotSame(prior, successor);
            await fixture.DrainRootPeer(TargetPlayerUid); drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5)); await fixture.CompletePausedRootItemEffectAdvanceAsync(pausedGeneration.Value);
            Assert.Same(successor, fixture.Owner); Assert.Equal(835L, fixture.Owner.Character.X);
            Assert.Equal(new ulong[] { 256 }, RootManualLifetimeVectors(fixture.FramesSince(TargetPlayerUid, start)));
            AssertRootManualLifetimeFlags(fixture, stigma: false, shield: true);
            await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
            AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
        }
        finally
        {
            if (!drained) fixture.CloseRootPeer(TargetPlayerUid);
            if (pausedGeneration is { } generation) await fixture.CompletePausedRootItemEffectAdvanceAsync(generation);
        }
    }

    [Fact]
    public async Task RootManualActualExpiryKeepsOpaqueCaseVariantsAcrossRealTimer()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true,
            opaqueCaseControls: true);
        await fixture.QueryRootItemEffectsThroughFrameAsync();
        await fixture.CastAsync(1_000);
        Assert.Equal("one", fixture.Owner.Character.Attributes["Opaque"]);
        Assert.Equal("two", fixture.Owner.Character.Attributes["opaque"]);
        await fixture.StartRootItemEffectRuntimeAsync();
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(20));
        AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
        Assert.Equal("one", fixture.Owner.Character.Attributes["Opaque"]);
        Assert.Equal("two", fixture.Owner.Character.Attributes["opaque"]);
    }

    [Fact]
    public async Task RootExpiryCapturedOtherShieldCannotRetireRealDispelAndReinstalledClock()
    {
        var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true,
            nativeDeathCasterQueryControls: true, otherFlagDispelControls: true);
        Exception? primary = null; var cleanup = new List<Exception>();
        Task? blocked = null, dispel = null, cast = null;
        long? pausedGeneration = null; bool drained = false, pauseCompleted = false;
        try
        {
            await fixture.QueryRootDispelPeerThroughFrameAsync(); await fixture.QueryRootItemEffectsThroughFrameAsync();
            await fixture.CastRootItemAttackThroughFrameAsync();
            var old = Assert.IsType<LegacyRootItemEffectFlagClock>(fixture.ReadActualRootClock(8));
            await fixture.StartRootItemEffectRuntimeAsync();
            blocked = fixture.BlockRootPeer(RootMagicEntryFixture.ExpiryObserverUid);
            await fixture.WaitRootPeerBlocked(RootMagicEntryFixture.ExpiryObserverUid);
            pausedGeneration = await fixture.BeginPausedRootItemEffectAdvanceAsync(TimeSpan.FromSeconds(20));
            await WaitUntilAsync(() => fixture.RootExpiryWaiting(RootMagicEntryFixture.ExpiryObserverUid), TimeSpan.FromSeconds(5));
            AssertRootManualLifetimeFlags(fixture, stigma: false, shield: true);
            Assert.Same(old, fixture.ReadActualRootClock(8));
            dispel = fixture.DispelRootOwnerThroughFrameAsync();
            await WaitUntilAsync(() => !HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8), TimeSpan.FromSeconds(5));
            Assert.False(dispel.IsCompleted);
            cast = fixture.CastRootItemAttackThroughFrameAsync(); // Queued real Frame, before timer reacquires Shared.
            Assert.False(cast.IsCompleted);
            await fixture.DrainRootPeer(RootMagicEntryFixture.ExpiryObserverUid); drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5)); await dispel.WaitAsync(TimeSpan.FromSeconds(5));
            await cast.WaitAsync(TimeSpan.FromSeconds(5)); await fixture.CompletePausedRootItemEffectAdvanceAsync(pausedGeneration.Value);
            pauseCompleted = true;
            var replacement = Assert.IsType<LegacyRootItemEffectFlagClock>(fixture.ReadActualRootClock(8));
            Assert.NotSame(old, replacement);
            var deadline = RootManualLifetimeDeadline(fixture, 8);
            Assert.Equal(fixture.Now.AddSeconds(15), deadline);
            AssertRootManualLifetimeFlags(fixture, stigma: true, shield: true);
            await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
            Assert.Same(replacement, fixture.ReadActualRootClock(8));
            Assert.Equal(deadline, RootManualLifetimeDeadline(fixture, 8));
            await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(14_960));
            Assert.Same(replacement, fixture.ReadActualRootClock(8));
            int shieldExpiry = fixture.Order.Count;
            await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
            AssertRootManualLifetimeFlags(fixture, stigma: true, shield: false);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid, RootMagicEntryFixture.ExpiryObserverUid })
                Assert.Equal(new ulong[] { 512 }, RootManualLifetimeVectors(fixture.FramesSince(uid, shieldExpiry)));
            int stigmaExpiry = fixture.Order.Count;
            await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(5));
            AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid, RootMagicEntryFixture.ExpiryObserverUid })
                Assert.Equal(new ulong[] { 0 }, RootManualLifetimeVectors(fixture.FramesSince(uid, stigmaExpiry)));
        }
        catch (Exception error) { primary = error; }
        finally
        {
            if (!drained && blocked is not null)
            {
                try { fixture.CloseRootPeer(RootMagicEntryFixture.ExpiryObserverUid); } catch (Exception error) { cleanup.Add(error); }
                try { await blocked.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) when (error is IOException or System.Net.Sockets.SocketException or ObjectDisposedException) { }
                catch (Exception error) { cleanup.Add(error); }
            }
            foreach (var action in new[] { dispel, cast })
                if (action is not null)
                    try { await action.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception error) { cleanup.Add(error); }
            if (!pauseCompleted && pausedGeneration is { } generation)
                try { await fixture.CompletePausedRootItemEffectAdvanceAsync(generation); }
                catch (Exception error) { cleanup.Add(error); }
            try { await fixture.DisposeAsync(); } catch (Exception error) { cleanup.Add(error); }
        }
        if (primary is not null && cleanup.Count != 0)
            throw new AggregateException("Other-clock assertion and cleanup both failed.", new[] { primary }.Concat(cleanup));
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup.Count == 1) ExceptionDispatchInfo.Capture(cleanup[0]).Throw();
        if (cleanup.Count > 1) throw new AggregateException("Other-clock cleanup failed.", cleanup);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("disagree")]
    public async Task RootExpiryGateRefusesMalformedCurrentStatusWithoutStartingWire(string kind)
    {
        var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 0, NeckEffect: 200), itemEffectLifetimeControls: true);
        Exception? primary = null;
        var cleanup = new List<Exception>();
        Task? blocked = null; long? prior = null; bool drained = false, completed = false;
        try
        {
            await fixture.QueryRootItemEffectsThroughFrameAsync(); await fixture.CastRootItemAttackThroughFrameAsync();
            Assert.NotNull(fixture.ReadActualRootClock(8));
            var deadline = RootManualLifetimeDeadline(fixture, 8);
            await fixture.StartRootItemEffectRuntimeAsync();
            blocked = fixture.BlockRootPeer(TargetPlayerUid);
            await fixture.WaitRootPeerBlocked(TargetPlayerUid);
            Assert.False(blocked.IsCompleted); // An actual unfinished socket write still holds the real gate.
            int start = fixture.Order.Count;
            var amount = deadline > fixture.Now ? deadline - fixture.Now + TimeSpan.FromMilliseconds(20) : TimeSpan.FromMilliseconds(20);
            prior = await fixture.BeginPausedRootItemEffectAdvanceAsync(amount);
            await WaitUntilAsync(() => fixture.RootExpiryWaiting(TargetPlayerUid), TimeSpan.FromSeconds(5));
            await fixture.DrainRootPeerObservations(PlayerUid);
            Assert.Null(fixture.ReadActualRootClock(8));
            Assert.Equal(new ulong[] { 0 }, RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, start)));
            var before = fixture.Owner;
            fixture.PublishMalformedCurrentRootStatus(kind); // Actual Store publisher after the last clock retired.
            Assert.NotSame(before, fixture.Owner);
            Assert.Throws<InvalidDataException>(() => LegacyWindWalkerStatusProjection.Read(fixture.Owner.Character));
            long wireStarts = fixture.RootPeerWireStarts(TargetPlayerUid);
            await fixture.DrainRootPeer(TargetPlayerUid); drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.CompletePausedRootItemEffectAdvanceAsync(prior.Value); completed = true;
            Assert.Equal(wireStarts, fixture.RootPeerWireStarts(TargetPlayerUid)); // No writer/encoder invocation.
            Assert.Empty(RootManualLifetimeVectors(fixture.FramesSince(TargetPlayerUid, start)));
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
            }
            if (!completed && prior is { } generation)
                try { await fixture.CompletePausedRootItemEffectAdvanceAsync(generation); }
                catch (Exception error) { cleanup.Add(error); }
            try { await fixture.DisposeAsync(); }
            catch (Exception error) { cleanup.Add(error); }
        }
        if (primary is not null && cleanup.Count != 0)
            throw new AggregateException("Malformed-status assertion and cleanup both failed.", new[] { primary }.Concat(cleanup));
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup.Count == 1) ExceptionDispatchInfo.Capture(cleanup[0]).Throw();
        if (cleanup.Count > 1) throw new AggregateException("Malformed-status cleanup failed.", cleanup);
    }

    private static DateTimeOffset RootManualLifetimeDeadline(RootMagicEntryFixture fixture, int flag) =>
        DateTimeOffset.Parse(fixture.Owner.Character.Attributes[flag == 9
            ? WorldPacketHandlers.RuntimeLegacyStigmaExpiresAtUtcAttribute
            : WorldPacketHandlers.RuntimeLegacyMagicShieldExpiresAtUtcAttribute], CultureInfo.InvariantCulture);

    private static void AssertRootManualLifetimeFlags(RootMagicEntryFixture fixture, bool stigma, bool shield)
    {
        Assert.Equal(stigma, HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 9));
        Assert.Equal(shield, HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
    }

    private static ulong[] RootManualLifetimeVectors(IEnumerable<byte[]> frames) =>
        ParseMsgUpdates(frames).Where(packet => packet.Uid == PlayerUid).SelectMany(packet => packet.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is { Count: > 0 })
            .Select(entry => entry.Values![0] & 768UL).ToArray();
}
