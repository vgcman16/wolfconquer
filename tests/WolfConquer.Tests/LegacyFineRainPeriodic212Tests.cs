using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task FineRainPeriodicRealFirstInvokeIsInclusiveAndDelayedTickDoesNotCatchUp()
    {
        var armed = false;
        var callbacks = 0;
        await using var fixture = new FineRainExpiryFixture((current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && FineRainPeriodicIsCountdown(bytes))
            {
                Assert.Equal(1, Interlocked.Increment(ref callbacks));
                FineRainPeriodicAssertPower(current.Owner, "84", "96");
                DirectFineRainHpAssertOwnedMaximum(current.Owner, 596);
                Assert.Equal(596, current.Owner.Character.MinHitPoints);
            }
            return ValueTask.CompletedTask;
        }, power: 96, nativeEpochMilliseconds: 10_000, environmentEpochMilliseconds: 20_000);
        await fixture.AcquireAsync();
        var start = fixture.Order.Count;
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(999));
        Assert.Empty(fixture.OwnerFramesSince(start));
        armed = true;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(1));
        armed = false;
        Assert.Equal(1, callbacks);
        FineRainPeriodicAssertPulse(fixture.OwnerFramesSince(start), 4_294_967_279U, 84, 584);
        Assert.Empty(fixture.PeerFramesSince(start));
        Assert.Equal(584U, (await fixture.QueryAsync()).MaxHP);

        start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(3_500));
        FineRainPeriodicAssertPulse(fixture.OwnerFramesSince(start), 4_294_967_282U, 72, 572);
        FineRainPeriodicAssertPower(fixture.Owner, "72", "96"); // One invocation, not three catch-up decrements.
        start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(999));
        Assert.Empty(fixture.OwnerFramesSince(start));
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(1));
        FineRainPeriodicAssertPulse(fixture.OwnerFramesSince(start), 4_294_967_283U, 60, 560);
        Assert.Equal(560U, (await fixture.QueryAsync()).MaxHP);
    }

    [Fact]
    public async Task FineRainPeriodicCountdownCallbackCarriesFreshSameUidVitalsAndItemsWithoutRebuilding()
    {
        var armed = false;
        var callbacks = 0;
        await using var fixture = new FineRainExpiryFixture((current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && FineRainPeriodicIsCountdown(bytes) &&
                Interlocked.Exchange(ref callbacks, 1) == 0)
            {
                DirectFineRainHpAssertOwnedMaximum(current.Owner, 596);
                Assert.Equal(596, current.Owner.Character.MinHitPoints);
                current.ChangeOwnerEquipmentAndVitals(); // Real700HP item/currentHP400/MP77; no cache seed.
            }
            return ValueTask.CompletedTask;
        }, power: 96);
        await fixture.AcquireAsync();
        await fixture.StartAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        armed = false;
        Assert.Equal(1, callbacks);
        FineRainPeriodicAssertCountdown(Assert.Single(fixture.OwnerFramesSince(start)), 4_294_967_289U, 84);
        Assert.Empty(fixture.PeerFramesSince(start));
        Assert.Equal(400, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(77, fixture.Owner.Character.MinMana);
        Assert.Equal("callback", fixture.Owner.Character.Attributes["FineRainExpiryIdentity"]);
        Assert.Equal(111_095U, Assert.Single(fixture.Owner.PlayerItems.EquipmentItems).ItemId);
        Assert.Equal(584U, (await fixture.QueryAsync()).MaxHP); // Direct Status.Max decrement, not a fresh gear query.
        FineRainPeriodicAssertPower(fixture.Owner, "84", "96");
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("rebind")]
    public async Task FineRainPeriodicCountdownOriginalOwnerLossStopsMaxHpAndSetterTraffic(string mutation)
    {
        var armed = false;
        var callbacks = 0;
        var boundary = -1;
        WorldSession? replacement = null;
        await using var fixture = new FineRainExpiryFixture((current, recipient, bytes) =>
        {
            if (!armed || recipient != PlayerUid || !FineRainPeriodicIsCountdown(bytes) ||
                Interlocked.Exchange(ref callbacks, 1) != 0) return ValueTask.CompletedTask;
            DirectFineRainHpAssertOwnedMaximum(current.Owner, 596);
            Assert.Equal(596, current.Owner.Character.MinHitPoints);
            boundary = current.Order.Count;
            if (mutation == "remove") current.RemoveOwner();
            else replacement = current.RebindOwner();
            return ValueTask.CompletedTask;
        }, power: 96);
        await fixture.AcquireAsync();
        await fixture.StartAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        armed = false;
        Assert.Equal(1, callbacks);
        FineRainPeriodicAssertCountdown(Assert.Single(fixture.OwnerFramesSince(start)), 4_294_967_289U, 84);
        Assert.True(boundary > start);
        Assert.Empty(fixture.Order.ToArray().Skip(boundary));
        if (mutation == "remove") Assert.False(fixture.TryOwner(out _));
        else
        {
            Assert.True(fixture.TryOwner(out var current));
            Assert.Same(replacement, current);
            Assert.Equal(77_777, current!.Character.MinHitPoints);
            Assert.Equal(77, current.Character.MinMana);
            Assert.Equal("replacement", current.Character.Attributes["FineRainExpiryIdentity"]);
            Assert.Null(current.EquipmentHitpointsStatus);
        }
    }

    [Fact]
    public async Task FineRainPeriodicNaturalZeroClearsDecrementBeforeExpiredEntryMaxSubtraction()
    {
        await using var fixture = new FineRainExpiryFixture(duration: 3, power: 6);
        await fixture.AcquireAsync();
        fixture.HoldSiblingDeadline(213); // Only independent expiry is staggered; real212 duration remains3.
        await fixture.StartAsync();
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        FineRainPeriodicAssertPulse(fixture.OwnerFramesSince(start), 4_294_967_294U, 4, 504);
        start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        FineRainPeriodicAssertPulse(fixture.OwnerFramesSince(start), 4_294_967_295U, 2, 502);
        start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        var owner = fixture.OwnerFramesSince(start);
        FineRainExpiryAssertOneQuery(owner);
        DirectQueryHpAssertPairs(owner, [(500U, 502U), (500U, 500U), (500U, 500U)]);
        var countdown = Assert.Single(owner.Select((bytes, index) => (bytes, index)),
            frame => FineRainPeriodicIsCountdown(frame.bytes));
        FineRainPeriodicAssertCountdown(countdown.bytes, 0, 0);
        var postlude = Assert.Single(owner.Select((bytes, index) => (bytes, index)),
            frame => LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(frame.bytes), out _));
        Assert.True(postlude.index < countdown.index);
        Assert.DoesNotContain(owner.Skip(countdown.index + 1), DirectQueryHpIsHpPacket);
        FineRainPeriodicAssertPower(fixture.Owner, "0", "6");
        Assert.Equal(500, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(500U, (await fixture.QueryAsync()).MaxHP); // NOT498: num7 became0 when power reached0.
        FineRainExpiryAssertAbsent(fixture.Owner, 212);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 213));
        start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        Assert.Empty(fixture.OwnerFramesSince(start)); // No current212 registration remains.
    }

    [Fact]
    public async Task FineRainPeriodicGuardedRealRecastUsesSignedMaxClampToOne()
    {
        await using var fixture = new FineRainExpiryFixture(duration: 2, power: 100, recastPower: 2_400);
        await fixture.AcquireAsync();
        fixture.SelectLearnedFineRainLevel(1); // Select another fixture DB row, not desired state/cache.
        await fixture.CastAsync();
        FineRainPeriodicAssertPower(fixture.Owner, "2400", "2400");
        Assert.Equal(1, fixture.WaitCount);
        Assert.Equal(600U, (await fixture.QueryAsync()).MaxHP);
        Assert.Equal(600, fixture.Owner.Character.MinHitPoints); // Rejected query's fill retains old600.
        await fixture.StartAsync();
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        FineRainPeriodicAssertPulse(fixture.OwnerFramesSince(start), 4_294_967_295U, 1_200, 1);
        FineRainPeriodicAssertPower(fixture.Owner, "1200", "2400");
        Assert.Equal(1U, (await fixture.QueryAsync()).MaxHP); // (int)(uint(600-1200)) <=0 =>1.
        Assert.Equal(1, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 212));
    }

    [Fact]
    public async Task FineRainPeriodicNaturalBothDueInterleavesEachCapturedFlagQueryBeforeNextFlag()
    {
        await using var fixture = new FineRainExpiryFixture(power: 96);
        await fixture.AcquireAsync();
        await fixture.StartAsync();
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(8)); // One delayed pass: no invented catch-up.
        var owner = fixture.OwnerFramesSince(start);
        var vectors = owner.Select((bytes, index) => (bytes, index))
            .Where(frame => FineRainExpiryTryVector(frame.bytes, out _, out _)).ToArray();
        var queries = owner.Select((bytes, index) => (bytes, index))
            .Where(frame => LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(frame.bytes), out _)).ToArray();
        Assert.Equal(2, vectors.Length);
        Assert.Equal(2, queries.Length);
        Assert.Equal(2, owner.Count(bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _)));
        Assert.Equal(2, owner.Count(bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _)));
        var countdown = Assert.Single(owner.Select((bytes, index) => (bytes, index)),
            frame => FineRainPeriodicIsCountdown(frame.bytes));
        FineRainPeriodicAssertCountdown(countdown.bytes, 0, 84);
        Assert.DoesNotContain(fixture.PeerFramesSince(start), FineRainPeriodicIsCountdown);
        Assert.True(FineRainExpiryTryVector(vectors[0].bytes, out var firstActive, out var secondActive));
        uint finalMaximum;
        if (!firstActive)
        {
            Assert.True(secondActive); // One permissible212-first iteration, never a dictionary-order claim.
            Assert.True(queries[0].index < countdown.index && countdown.index < vectors[1].index);
            DirectQueryHpAssertPairs(owner,
                [(500U, 596U), (500U, 500U), (500U, 500U), (488U, 488U),
                 (500U, 488U), (500U, 488U), (500U, 488U)]);
            finalMaximum = 500;
            Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        }
        else
        {
            Assert.False(secondActive); // Exact213-first alternative has periodic after the later212 query.
            Assert.True(queries[1].index < countdown.index);
            DirectQueryHpAssertPairs(owner,
                [(500U, 596U), (596U, 596U), (596U, 596U), (500U, 596U),
                 (500U, 500U), (500U, 500U), (488U, 488U)]);
            finalMaximum = 488;
            Assert.Equal(18_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        }
        FineRainExpiryAssertAbsent(fixture.Owner, 212);
        FineRainExpiryAssertAbsent(fixture.Owner, 213);
        FineRainPeriodicAssertPower(fixture.Owner, "84", "96");
        Assert.Equal(488, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(finalMaximum, (await fixture.QueryAsync()).MaxHP);
    }

    [Fact]
    public async Task FineRainPeriodicCapturedExpired212StillInvokesAfterOwedQueryAndRealRecast()
    {
        var armed = false;
        var callbacks = 0;
        var callbackEnd = -1;
        await using var fixture = new FineRainExpiryFixture(async (current, recipient, bytes) =>
        {
            if (!armed || recipient != PlayerUid || !FineRainExpiryTryVector(bytes, out var first, out var second) ||
                first || !second || Interlocked.Exchange(ref callbacks, 1) != 0) return;
            await current.CastAsync(); // Real14500 replaces both objects atW8; old captured212 still owns invocation.
            Assert.Equal(596, current.Owner.Character.MinHitPoints);
            Assert.Equal(18_000, LegacyFineRainEquipmentAdmission.ForOwner(current.Owner).StampMilliseconds);
            callbackEnd = current.Order.Count;
        }, power: 96);
        await fixture.AcquireAsync();
        await fixture.StartAsync();
        armed = true;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(8));
        armed = false;
        Assert.Equal(1, callbacks); // Local212-first chosen owner; no universal source enumeration assertion.
        Assert.True(callbackEnd > 0);
        var owner = fixture.OwnerFramesSince(callbackEnd);
        var countdown = Assert.Single(owner.Select((bytes, index) => (bytes, index)),
            frame => FineRainPeriodicIsCountdown(frame.bytes));
        FineRainPeriodicAssertCountdown(countdown.bytes, 0, 84);
        var waits = owner.Select((bytes, index) => (bytes, index)).Where(frame => FineRainExpiryIsWait(frame.bytes)).ToArray();
        Assert.Equal(2, waits.Length);
        Assert.True(waits[0].index < countdown.index && countdown.index < waits[1].index);
        DirectQueryHpAssertPairs(owner, [(584U, 584U)]);
        Assert.DoesNotContain(owner, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 212));
        FineRainExpiryAssertAbsent(fixture.Owner, 213); // Old captured213 removes CURRENT replacement.
        FineRainPeriodicAssertPower(fixture.Owner, "84", "96");
        Assert.Equal(584, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(584U, (await fixture.QueryAsync()).MaxHP);
        Assert.Equal(fixture.Origin.AddSeconds(16).ToString("O"),
            fixture.Owner.Character.Attributes["LegacyFineRain1ExpiresAtUtc"]);
    }

    [Fact]
    public async Task FineRainPeriodicEachSubjectCapturesFreshComparisonTimeAfterEarlierOwnerCallback()
    {
        var armed = false;
        var callbacks = 0;
        uint first = 0;
        await using var fixture = new FineRainExpiryFixture((current, recipient, bytes) =>
        {
            if (armed && recipient == first && FineRainPeriodicIsSubjectCountdown(bytes, first) &&
                Interlocked.Exchange(ref callbacks, 1) == 0)
            {
                Assert.Equal(current.Origin.AddSeconds(1), current.WallNow);
                current.AdvanceWall(TimeSpan.FromMilliseconds(500));
            }
            return ValueTask.CompletedTask;
        }, power: 96, peerFineRainOwner: true);
        var order = fixture.SubjectOrder;
        Assert.Equal(2, order.Length);
        first = order[0];
        var second = order[1];
        await fixture.CastSubjectFineRainAsync(first); // Real registrationW0.
        Assert.Equal(596U, (await fixture.QuerySubjectAsync(first)).MaxHP);
        await fixture.StartAsync();
        fixture.AdvanceWall(TimeSpan.FromMilliseconds(500)); // Before initialW1 delay becomes due.
        await fixture.CastSubjectFineRainAsync(second); // Real independent registrationW0.5.
        Assert.Equal(596U, (await fixture.QuerySubjectAsync(second)).MaxHP);
        Assert.Equal(order, fixture.SubjectOrder); // Explicit local order precondition, not a source dictionary-order claim.
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(500));
        armed = false;
        Assert.Equal(1, callbacks);
        Assert.Equal(fixture.Origin.AddMilliseconds(1_500), fixture.WallNow);
        foreach (var uid in order)
        {
            var own = fixture.SubjectFramesSince(uid, start);
            FineRainPeriodicAssertSubjectPulse(own, uid, 4_294_967_289U, 84, 584);
            FineRainPeriodicAssertPower(fixture.Subject(uid), "84", "96");
            Assert.Equal(584, fixture.Subject(uid).Character.MinHitPoints);
            Assert.Equal(584U, (await fixture.QuerySubjectAsync(uid)).MaxHP);
        }
        var pulses = fixture.Order.ToArray().Skip(start)
            .Where(frame => FineRainPeriodicIsSubjectCountdown(frame.Bytes, frame.Recipient)).ToArray();
        Assert.Equal(order, pulses.Select(frame => frame.Recipient).ToArray());
        // Second was NOT due at oldglobalW1 (bornW0.5), but is due at its own freshW1.5 entry.
    }

    private static bool FineRainPeriodicIsSubjectCountdown(byte[] bytes, uint uid) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == uid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.FineRain && entry.Flag == 212);

    private static bool FineRainPeriodicIsCountdown(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.FineRain && entry.Flag == 212);

    private static void FineRainPeriodicAssertCountdown(byte[] bytes, uint time, uint power, uint uid = PlayerUid)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Uid);
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.FineRain, update.Type);
        Assert.Equal(0UL, update.Value);
        Assert.Equal(212U, update.Flag);
        Assert.Equal(time, update.Time);
        Assert.Equal(power, update.Damage);
        Assert.Equal(power, update.SpellLevel);
    }

    private static void FineRainPeriodicAssertPulse(byte[][] owner, uint time, uint power, uint maximum) =>
        FineRainPeriodicAssertSubjectPulse(owner, PlayerUid, time, power, maximum);

    private static void FineRainPeriodicAssertSubjectPulse(
        byte[][] owner, uint uid, uint time, uint power, uint maximum)
    {
        Assert.Equal(3, owner.Length);
        FineRainPeriodicAssertCountdown(owner[0], time, power, uid);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(owner[1]), out var first));
        Assert.Equal(uid, first.Uid);
        var maximumOnly = Assert.Single(first.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maximumOnly.Type);
        Assert.Equal((ulong)maximum, maximumOnly.Value);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(owner[2]), out var second));
        Assert.Equal(uid, second.Uid);
        Assert.Collection(second.Updates,
            max => { Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, max.Type); Assert.Equal((ulong)maximum, max.Value); },
            hp => { Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, hp.Type); Assert.Equal((ulong)maximum, hp.Value); });
    }

    private static void FineRainPeriodicAssertPower(WorldSession session, string current, string initial)
    {
        Assert.Equal(current, session.Character.Attributes["FineRainPower"]);
        Assert.Equal(initial, session.Character.Attributes["defFineRainPower"]);
    }
}
