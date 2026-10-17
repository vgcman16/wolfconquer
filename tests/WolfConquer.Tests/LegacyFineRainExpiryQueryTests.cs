using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task FineRainExpiryRealBothDuePublishesTwoIndependentRemoveThenQueryEvents()
    {
        await using var fixture = new FineRainExpiryFixture();
        await fixture.AcquireAsync();
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(7));
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(999));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 212));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 213));
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(1));
        var owner = fixture.OwnerFramesSince(start);
        var vectors = owner.Select((bytes, index) => (bytes, index))
            .Where(entry => FineRainExpiryTryVector(entry.bytes, out _, out _)).ToArray();
        Assert.Equal(2, vectors.Length);
        Assert.True(FineRainExpiryTryVector(vectors[0].bytes, out var first212, out var first213));
        Assert.NotEqual(first212, first213); // ConcurrentDictionary does not promise which flag expires first.
        Assert.True(FineRainExpiryTryVector(vectors[1].bytes, out var last212, out var last213));
        Assert.False(last212);
        Assert.False(last213);
        var firstQuery = owner.Skip(vectors[0].index + 1).Take(vectors[1].index - vectors[0].index - 1).ToArray();
        var secondQuery = owner.Skip(vectors[1].index + 1).ToArray();
        FineRainExpiryAssertOneQuery(firstQuery);
        FineRainExpiryAssertOneQuery(secondQuery);
        DirectQueryHpAssertPairs(firstQuery, first212
            ? [(500U, 501U), (501U, 501U), (501U, 501U)]
            : [(500U, 501U), (500U, 500U), (500U, 500U)]);
        DirectQueryHpAssertPairs(secondQuery, [(500U, 500U), (500U, 500U), (500U, 500U)]);
        Assert.Equal(first212 ? 18_000 : 10_000,
            LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        FineRainExpiryAssertRetainedPowers(fixture.Owner);
        FineRainExpiryAssertAbsent(fixture.Owner, 212);
        FineRainExpiryAssertAbsent(fixture.Owner, 213);
        Assert.Equal(500, fixture.Owner.Character.MinHitPoints);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 500);
        Assert.Equal(0, fixture.WaitCount);
        Assert.Equal(2, fixture.PeerFramesSince(start).Count(bytes => FineRainExpiryTryVector(bytes, out _, out _)));
    }

    [Fact]
    public async Task FineRainExpiry212RemovalBypassesGuardAndDoesNotAdvanceItsStamp()
    {
        await using var fixture = new FineRainExpiryFixture(duration: 2);
        await fixture.AcquireAsync();
        fixture.HoldSiblingDeadline(213); // Timestamp-only branch bridge AFTER the real2s cast.
        var retainedSibling = fixture.Owner.Character;
        await fixture.StartAsync();
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(2));
        var owner = fixture.OwnerFramesSince(start);
        var vector = Assert.Single(owner, bytes => FineRainExpiryTryVector(bytes, out _, out _));
        Assert.True(FineRainExpiryTryVector(vector, out var first, out var second));
        Assert.False(first);
        Assert.True(second);
        FineRainExpiryAssertOneQuery(owner);
        DirectQueryHpAssertPairs(owner, [(500U, 501U), (500U, 500U), (500U, 500U)]);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.Equal(0, fixture.WaitCount);
        FineRainExpiryAssertRetainedPowers(fixture.Owner);
        Assert.Equal(fixture.Origin.AddSeconds(22).ToString("O"),
            fixture.Owner.Character.Attributes["LegacyFineRain2ExpiresAtUtc"]);
        FineRainExpiryAssertCanonicalPolicyRetained(retainedSibling, fixture.Owner.Character, 213);
    }

    [Fact]
    public async Task FineRainExpiry213UnderLive212ConsumesRejectedQueryWithoutAutomaticRetry()
    {
        await using var fixture = new FineRainExpiryFixture(duration: 2, mutableCounteraction: true);
        await fixture.AcquireAsync();
        await fixture.EarnCounteractionAsync();
        Assert.Equal(75U, (await fixture.QueryAsync()).Counteraction);
        fixture.HoldSiblingDeadline(212); // Only sibling deadline; no flag/power/cache/stamp seeds.
        var retainedSibling = fixture.Owner.Character;
        await fixture.StartAsync();
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(2));
        var owner = fixture.OwnerFramesSince(start);
        var vectorIndex = Array.FindIndex(owner, bytes => FineRainExpiryTryVector(bytes, out _, out _));
        var waitIndex = Array.FindIndex(owner, FineRainExpiryIsWait);
        Assert.True(vectorIndex >= 0 && waitIndex > vectorIndex);
        Assert.True(FineRainExpiryTryVector(owner[vectorIndex], out var first, out var second));
        Assert.True(first);
        Assert.False(second);
        Assert.DoesNotContain(owner, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(owner, DirectQueryHpIsHpPacket);
        Assert.Equal(1, fixture.WaitCount);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.Equal(210, fixture.Owner.Character.MinHitPoints); // Real501-291, not a seeded mutable result.
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 501);
        FineRainExpiryAssertRetainedPowers(fixture.Owner);
        FineRainExpiryAssertAbsent(fixture.Owner, 213);
        FineRainExpiryAssertCanonicalPolicyRetained(retainedSibling, fixture.Owner.Character, 212);
        Assert.Equal(75U, (await fixture.QueryAsync()).Counteraction);
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(4));
        Assert.Equal(1, fixture.WaitCount);
        Assert.Equal(75U, (await fixture.QueryAsync()).Counteraction);
        Assert.DoesNotContain(fixture.OwnerFramesSince(start),
            bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        await fixture.QueryEquipmentAsync(); // A REAL later2286 query, not an expiry retry.
        Assert.Equal(16_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.Equal(1, fixture.WaitCount);
        var admitted = await fixture.QueryAsync();
        Assert.Equal(501U, admitted.MaxHP);
        Assert.Equal(50U, admitted.Counteraction); // REAL admitted query resets the earned75.
        Assert.Equal(210, fixture.Owner.Character.MinHitPoints);
    }

    [Fact]
    public async Task FineRainExpiryAbsentFlagMetadataCleanupDoesNotQueryOrDeleteIndependentPower()
    {
        await using var fixture = new FineRainExpiryFixture();
        await fixture.AcquireAsync();
        fixture.HoldSiblingDeadline(213);
        fixture.Remove212ForMalformedMetadataControl(); // Explicit corrupt/compatibility input, NOT canonical acquisition.
        Assert.Equal("true", fixture.Owner.Character.Attributes[LegacyStatusFlagDeathRegistration.Key(212)]);
        Assert.Equal("false", fixture.Owner.Character.Attributes[LegacyStatusFlagDeathRegistration.Key(213)]);
        await fixture.StartAsync();
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(8));
        var owner = fixture.OwnerFramesSince(start);
        Assert.DoesNotContain(owner, bytes => FineRainExpiryTryVector(bytes, out _, out _));
        Assert.DoesNotContain(owner, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(owner, FineRainExpiryIsWait);
        Assert.DoesNotContain(owner, DirectQueryHpIsHpPacket);
        FineRainExpiryAssertAbsent(fixture.Owner, 212);
        FineRainExpiryAssertRetainedPowers(fixture.Owner);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 213));
        Assert.Equal("false", fixture.Owner.Character.Attributes[LegacyStatusFlagDeathRegistration.Key(213)]);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FineRainExpiryOwnerIdentityLossStopsOldVectorFanoutAndQuery(bool rebind)
    {
        var armed = false;
        var callbackEnd = -1;
        WorldSession? replacement = null;
        await using var fixture = new FineRainExpiryFixture((current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && FineRainExpiryTryVector(bytes, out var first, out var second) &&
                !first && second && Interlocked.CompareExchange(ref callbackEnd, 0, -1) == -1)
            {
                if (rebind) replacement = current.RebindOwner();
                else current.RemoveOwner();
                callbackEnd = current.Order.Count;
            }
            return ValueTask.CompletedTask;
        });
        await fixture.AcquireAsync();
        fixture.HoldSiblingDeadline(213);
        await fixture.StartAsync();
        armed = true;
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(8));
        Assert.True(callbackEnd > start);
        Assert.Single(fixture.OwnerFramesSince(start), bytes => FineRainExpiryTryVector(bytes, out _, out _));
        Assert.Empty(fixture.Order.ToArray().Skip(callbackEnd)); // No stale peer vector, query, or replacement publication.
        if (rebind) Assert.Same(replacement, fixture.Owner);
        else Assert.False(fixture.TryOwner(out _));
    }

    [Fact]
    public async Task FineRainExpiryQueryUsesFreshSameUidEquipmentAndUnfilledCurrentHp()
    {
        var armed = false;
        var changed = 0;
        await using var fixture = new FineRainExpiryFixture((current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && FineRainExpiryTryVector(bytes, out var first, out var second) &&
                !first && second && Interlocked.Exchange(ref changed, 1) == 0)
                current.ChangeOwnerEquipmentAndVitals();
            return ValueTask.CompletedTask;
        });
        await fixture.AcquireAsync();
        fixture.HoldSiblingDeadline(213);
        await fixture.StartAsync();
        armed = true;
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(1, changed);
        FineRainExpiryAssertOneQuery(fixture.OwnerFramesSince(start));
        DirectQueryHpAssertPairs(fixture.OwnerFramesSince(start),
            [(700U, 400U), (700U, 400U), (700U, 400U)]);
        Assert.Equal(400, fixture.Owner.Character.MinHitPoints); // Expiry query clamps; it does not recast/fill.
        Assert.Equal(77, fixture.Owner.Character.MinMana);
        Assert.Equal("callback", fixture.Owner.Character.Attributes["FineRainExpiryIdentity"]);
        var status = await fixture.QueryAsync();
        Assert.Equal(700U, status.MaxHP);
        Assert.Equal(80U, status.Breakthrough);
        Assert.Equal(70U, status.Counteraction);
        FineRainExpiryAssertRetainedPowers(fixture.Owner);
    }

    [Fact]
    public async Task FineRainExpiryCapturedOld213StillRemovesReplacementFromRealRecast()
    {
        var armed = false;
        var recast = 0;
        var callbackEnd = -1;
        await using var fixture = new FineRainExpiryFixture(async (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && FineRainExpiryTryVector(bytes, out var first, out var second) &&
                !first && second && Interlocked.Exchange(ref recast, 1) == 0)
            {
                await current.CastAsync();
                callbackEnd = current.Order.Count;
            }
        });
        await fixture.AcquireAsync();
        await fixture.StartAsync();
        armed = true;
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(1, recast); // Local212-first is one permissible source snapshot order, not a global ordering claim.
        Assert.True(callbackEnd > start);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 212));
        FineRainExpiryAssertAbsent(fixture.Owner, 213); // Captured old Flag entry consumes NEW213 despite its future deadline.
        Assert.Equal(fixture.Origin.AddSeconds(16).ToString("O"),
            fixture.Owner.Character.Attributes["LegacyFineRain1ExpiresAtUtc"]);
        Assert.Equal(18_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.Equal(2, fixture.OwnerFramesSince(callbackEnd).Count(FineRainExpiryIsWait));
        Assert.DoesNotContain(fixture.PeerFramesSince(callbackEnd),
            bytes => FineRainExpiryTryVector(bytes, out var first, out _) && !first);
        FineRainExpiryAssertRetainedPowers(fixture.Owner);
        Assert.Equal(501U, (await fixture.QueryAsync()).MaxHP);
        Assert.Equal(501, fixture.Owner.Character.MinHitPoints);
    }

    [Fact]
    public async Task FineRainExpiryCapturedLater213AbsentStillQueriesWithoutInventingAnotherVector()
    {
        var armed = false;
        var removed = 0;
        await using var fixture = new FineRainExpiryFixture((current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && FineRainExpiryTryVector(bytes, out var first, out var second) &&
                !first && second && Interlocked.Exchange(ref removed, 1) == 0)
                current.ClearAcquiredFlagForBranchInput(213);
            return ValueTask.CompletedTask;
        });
        await fixture.AcquireAsync();
        await fixture.StartAsync();
        armed = true;
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(1, removed); // Local212-first is permissible, not a source dictionary-order promise.
        var owner = fixture.OwnerFramesSince(start);
        Assert.Single(owner, bytes => FineRainExpiryTryVector(bytes, out _, out _));
        Assert.DoesNotContain(fixture.PeerFramesSince(start), bytes => FineRainExpiryTryVector(bytes, out _, out _));
        Assert.Equal(2, owner.Count(bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _)));
        Assert.Equal(2, owner.Count(bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _)));
        Assert.Equal(2, owner.Count(bytes => LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(bytes), out _)));
        DirectQueryHpAssertPairs(owner,
            [(500U, 501U), (500U, 500U), (500U, 500U), (500U, 500U), (500U, 500U), (500U, 500U)]);
        FineRainExpiryAssertAbsent(fixture.Owner, 212);
        FineRainExpiryAssertAbsent(fixture.Owner, 213);
        FineRainExpiryAssertRetainedPowers(fixture.Owner);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.Equal(0, fixture.WaitCount);
    }

    private static bool FineRainExpiryTryVector(byte[] bytes, out bool first, out bool second)
    {
        first = second = false;
        if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) || packet.Uid != PlayerUid) return false;
        var entry = packet.Updates.FirstOrDefault(update => update.Type == LegacyMsgUpdateDataType.StatusFlag);
        if (entry.Values is not { } values) return false;
        first = IsLegacyStatusFlagSet(values, 212);
        second = IsLegacyStatusFlagSet(values, 213);
        return true;
    }

    private static bool FineRainExpiryIsWait(byte[] bytes) =>
        LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
        packet.Content.Any(content =>
            content.Contains("You Shoud wait 6 seconds.", StringComparison.Ordinal));

    private static void FineRainExpiryAssertOneQuery(byte[][] frames)
    {
        Assert.Single(frames, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Single(frames, bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Single(frames, bytes => LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(frames, FineRainExpiryIsWait);
    }

    private static void FineRainExpiryAssertCanonicalPolicyRetained(
        WolfConquer.Core.Data.CharacterSnapshot before, WolfConquer.Core.Data.CharacterSnapshot after, int flag)
    {
        var key = LegacyStatusFlagDeathRegistration.Key(flag);
        Assert.Equal(before.Attributes.TryGetValue(key, out var expected), after.Attributes.TryGetValue(key, out var actual));
        Assert.Equal(expected, actual); // Do not invent a positive policy where the real writer leaves it untracked.
    }

    private static void FineRainExpiryAssertRetainedPowers(WorldSession session)
    {
        Assert.Equal("1", session.Character.Attributes["FineRainPower"]);
        Assert.Equal("1", session.Character.Attributes["defFineRainPower"]);
    }

    private static void FineRainExpiryAssertAbsent(WorldSession session, int flag)
    {
        Assert.False(HasLegacyStatusFlag(session.Character.Attributes, flag));
        Assert.False(session.Character.Attributes.ContainsKey("LegacyFineRain" + (flag - 211) + "ExpiresAtUtc"));
        Assert.False(session.Character.Attributes.ContainsKey("LegacyFineRain" + (flag - 211) + "RemoveOnDead"));
        Assert.False(session.Character.Attributes.ContainsKey(LegacyStatusFlagDeathRegistration.Key(flag)));
    }
}
