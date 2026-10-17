using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task FineRainInitialAcquisitionCapturesEachCountdownButRereadsPowerBetweenWholeSends()
    {
        var phases = new List<string>();
        byte[]? firstOwner = null;
        byte[]? secondOwner = null;
        await using var fixture = new FineRainExpiryFixture((current, recipient, bytes) =>
        {
            if (!FineRainInitialTryCountdown(bytes, PlayerUid, out var entry)) return ValueTask.CompletedTask;
            Assert.False(HasLegacyStatusFlag(current.Owner.Character.Attributes, 212));
            Assert.False(HasLegacyStatusFlag(current.Owner.Character.Attributes, 213));
            Assert.False(current.Owner.Character.Attributes.ContainsKey("LegacyFineRain212Invocation.TimerTicks"));
            DirectFineRainHpAssertOwnedMaximum(current.Owner, 500);
            Assert.Equal(300, current.Owner.Character.MinHitPoints);
            if (entry.Flag == 212 && recipient == PlayerUid)
            {
                phases.Add("212-owner");
                firstOwner = bytes;
                FineRainInitialAssertCountdown(entry, 212, 8, 96);
                current.ChangeInitialAcquisitionPower(42); // Explicit accepted SAME-UID callback input, not an acquisition seed.
            }
            else if (entry.Flag == 212)
            {
                phases.Add("212-peer");
                Assert.Equal(firstOwner, bytes); // First packet is NOT reserialized with42 for a later recipient.
                Assert.Equal("42", current.Owner.Character.Attributes["FineRainPower"]);
                current.ChangeInitialAcquisitionPower(24); // SECOND countdown must wait for the whole first fanout.
            }
            else if (recipient == PlayerUid)
            {
                phases.Add("213-owner");
                secondOwner = bytes;
                FineRainInitialAssertCountdown(entry, 213, 8, 24);
                current.ChangeInitialAcquisitionPower(12);
            }
            else
            {
                phases.Add("213-peer");
                Assert.Equal(secondOwner, bytes);
                FineRainInitialAssertCountdown(entry, 213, 8, 24); // Captured213 packet, not new current12.
            }
            return ValueTask.CompletedTask;
        }, power: 96);
        var start = fixture.Order.Count;
        await fixture.CastAsync();
        Assert.Equal(new[] { "212-owner", "212-peer", "213-owner", "213-peer" }, phases);
        FineRainPeriodicAssertPower(fixture.Owner, "12", "96");
        Assert.Equal(512U, (await fixture.QueryAsync()).MaxHP);
        Assert.Equal(512, fixture.Owner.Character.MinHitPoints);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 512);
        DirectQueryHpAssertPairs(fixture.OwnerFramesSince(start),
            [(500U, 300U), (512U, 300U), (512U, 300U), (512U, 512U)]);
        FineRainInitialAssertVectors(fixture, start, [(PlayerUid, true, false), (TargetPlayerUid, true, false),
            (PlayerUid, true, true), (TargetPlayerUid, true, true)]);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
    }

    [Fact]
    public async Task FineRainInitialAcquisitionCreatesEachRegistrationOnlyAtItsOwnAddBoundary()
    {
        var observations = new List<string>();
        await using var fixture = new FineRainExpiryFixture((current, recipient, bytes) =>
        {
            if (recipient != PlayerUid) return ValueTask.CompletedTask;
            if (FineRainInitialTryCountdown(bytes, PlayerUid, out var entry))
            {
                Assert.False(HasLegacyStatusFlag(current.Owner.Character.Attributes, 212));
                Assert.False(HasLegacyStatusFlag(current.Owner.Character.Attributes, 213));
                if (entry.Flag == 212) { observations.Add("countdown212"); current.AdvanceWall(TimeSpan.FromMilliseconds(125)); }
                else { observations.Add("countdown213"); current.AdvanceWall(TimeSpan.FromMilliseconds(250)); }
            }
            else if (FineRainInitialTryVector(bytes, PlayerUid, out var first, out var second))
            {
                Assert.True(first);
                var attributes = current.Owner.Character.Attributes;
                if (!second)
                {
                    observations.Add("add212");
                    Assert.False(attributes.ContainsKey("LegacyFineRain2ExpiresAtUtc"));
                    Assert.False(attributes.ContainsKey(LegacyStatusFlagDeathRegistration.Key(213)));
                    FineRainInitialAssert212Timer(current.Owner, current.Origin.AddMilliseconds(375), seconds: 8);
                    Assert.Equal(current.Origin.AddMilliseconds(8_375).ToString("O"), attributes["LegacyFineRain1ExpiresAtUtc"]);
                    Assert.Equal("true", attributes[LegacyStatusFlagDeathRegistration.Key(212)]);
                    current.AdvanceWall(TimeSpan.FromMilliseconds(500));
                }
                else
                {
                    observations.Add("add213");
                    FineRainInitialAssert212Timer(current.Owner, current.Origin.AddMilliseconds(375), seconds: 8);
                    Assert.Equal(current.Origin.AddMilliseconds(8_875).ToString("O"), attributes["LegacyFineRain2ExpiresAtUtc"]);
                    Assert.Equal("true", attributes[LegacyStatusFlagDeathRegistration.Key(213)]);
                }
            }
            return ValueTask.CompletedTask;
        }, power: 96);
        var start = fixture.Order.Count;
        await fixture.CastAsync();
        Assert.Equal(new[] { "countdown212", "countdown213", "add212", "add213" }, observations);
        FineRainInitialAssertVectors(fixture, start, [(PlayerUid, true, false), (TargetPlayerUid, true, false),
            (PlayerUid, true, true), (TargetPlayerUid, true, true)]);
        Assert.Equal(fixture.Origin.AddMilliseconds(875), fixture.WallNow);
        Assert.Equal(10_875, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.Equal(596, fixture.Owner.Character.MinHitPoints);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 596);
        DirectQueryHpAssertPairs(fixture.OwnerFramesSince(start),
            [(500U, 300U), (596U, 300U), (596U, 300U), (596U, 596U)]);
    }

    [Fact]
    public async Task FineRainInitialAcquisitionRecastAt212VectorPreservesNew212AndStillAddsOuter213()
    {
        var entered = false;
        var callbackEnd = -1;
        string? replacementIdentity = null;
        string? replacementInvoker = null;
        byte[]? outer212 = null;
        await using var fixture = new FineRainExpiryFixture(async (current, recipient, bytes) =>
        {
            if (entered || recipient != PlayerUid ||
                !FineRainInitialTryVector(bytes, PlayerUid, out var first, out var second) || !first || second) return;
            entered = true;
            outer212 = bytes;
            FineRainInitialAssert212Timer(current.Owner, current.Origin, seconds: 8);
            current.AdvanceWall(TimeSpan.FromSeconds(1));
            current.SelectLearnedFineRainLevel(1);
            await current.CastAsync(); // REAL positive24/duration12 replacement; no desired timer/power/cache is seeded.
            FineRainInitialAssert212Timer(current.Owner, current.Origin.AddSeconds(1), seconds: 12);
            var attributes = current.Owner.Character.Attributes;
            replacementIdentity = attributes["LegacyFineRain212Invocation.Identity"];
            replacementInvoker = attributes["LegacyFineRain212Invocation.InvokerTicks"];
            Assert.Equal(current.Origin.AddSeconds(13).ToString("O"), attributes["LegacyFineRain1ExpiresAtUtc"]);
            Assert.Equal(current.Origin.AddSeconds(13).ToString("O"), attributes["LegacyFineRain2ExpiresAtUtc"]);
            FineRainPeriodicAssertPower(current.Owner, "24", "24");
            DirectFineRainHpAssertOwnedMaximum(current.Owner, 524);
            callbackEnd = current.Order.Count;
        }, power: 96, recastPower: 24, recastDuration: 12);
        await fixture.CastAsync();
        Assert.True(entered);
        Assert.True(callbackEnd > 0);
        var resumed = fixture.Order.ToArray().Skip(callbackEnd).ToArray();
        Assert.NotEmpty(resumed);
        Assert.Equal(TargetPlayerUid, resumed[0].Recipient);
        Assert.Equal(outer212, resumed[0].Bytes); // Captured outer212 fanout is still owed, despite nested current BOTH flags.
        FineRainInitialAssertVectors(fixture, callbackEnd, [(TargetPlayerUid, true, false),
            (PlayerUid, true, true), (TargetPlayerUid, true, true)]);
        var after = fixture.Owner.Character.Attributes;
        Assert.Equal(replacementIdentity, after["LegacyFineRain212Invocation.Identity"]);
        Assert.Equal(replacementInvoker, after["LegacyFineRain212Invocation.InvokerTicks"]);
        FineRainInitialAssert212Timer(fixture.Owner, fixture.Origin.AddSeconds(1), seconds: 12);
        Assert.Equal(fixture.Origin.AddSeconds(13).ToString("O"), after["LegacyFineRain1ExpiresAtUtc"]);
        Assert.Equal(fixture.Origin.AddSeconds(9).ToString("O"), after["LegacyFineRain2ExpiresAtUtc"]); // Outer8s Add occurs AFTER nested action.
        FineRainPeriodicAssertPower(fixture.Owner, "24", "24");
        Assert.Equal(11_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.Equal(524, fixture.Owner.Character.MinHitPoints);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 524);
        var owner = fixture.OwnerFramesSince(callbackEnd);
        Assert.Single(owner, FineRainExpiryIsWait); // Entered outer query rejects; its separate fill still runs.
        Assert.DoesNotContain(owner, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(owner, bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
        DirectQueryHpAssertPairs(owner, [(524U, 524U)]);
        Assert.Single(ParseSpellAnimations(owner), animation => animation.SpellId == 14_500);
    }

    [Fact]
    public async Task FineRainInitialAcquisitionCarriesSameUidStateThroughSeparateAddsIntoQueryAndFill()
    {
        var callbacks = 0;
        await using var fixture = new FineRainExpiryFixture((current, recipient, bytes) =>
        {
            if (recipient == PlayerUid && FineRainInitialTryVector(bytes, PlayerUid, out var first, out var second) &&
                first && !second && Interlocked.Exchange(ref callbacks, 1) == 0)
            {
                current.ChangeOwnerEquipmentAndVitals();
                current.ChangeInitialAcquisitionHitpoints(0); // Declared SAME-UID input; entered action must not add an alive gate.
                Assert.Equal(0, current.Owner.Character.MinHitPoints);
                Assert.Equal(77, current.Owner.Character.MinMana);
                DirectFineRainHpAssertOwnedMaximum(current.Owner, 500); // Query has NOT run at212 insertion.
            }
            if (recipient == PlayerUid && FineRainInitialTryVector(bytes, PlayerUid, out _, out var has213) && has213)
            {
                Assert.Equal(0, current.Owner.Character.MinHitPoints);
                Assert.Equal(77, current.Owner.Character.MinMana);
                Assert.Equal("callback", current.Owner.Character.Attributes["FineRainExpiryIdentity"]);
                Assert.Equal(111_095U, Assert.Single(current.Owner.PlayerItems.EquipmentItems).ItemId);
                DirectFineRainHpAssertOwnedMaximum(current.Owner, 500);
            }
            return ValueTask.CompletedTask;
        }, power: 96);
        var start = fixture.Order.Count;
        await fixture.CastAsync();
        Assert.Equal(1, callbacks);
        Assert.Equal("callback", fixture.Owner.Character.Attributes["FineRainExpiryIdentity"]);
        Assert.Equal(111_095U, Assert.Single(fixture.Owner.PlayerItems.EquipmentItems).ItemId);
        Assert.Equal(77, fixture.Owner.Character.MinMana);
        Assert.Equal(796, fixture.Owner.Character.MinHitPoints); // Entered source query then explicit fill; no new HP0/alive re-gate.
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 796);
        DirectQueryHpAssertPairs(fixture.OwnerFramesSince(start),
            [(700U, 0U), (796U, 0U), (796U, 0U), (796U, 796U)]);
        FineRainPeriodicAssertPower(fixture.Owner, "96", "96");
    }

    private static bool FineRainInitialTryCountdown(byte[] bytes, uint uid, out LegacyMsgUpdateEntry entry)
    {
        entry = default;
        if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) || packet.Uid != uid) return false;
        foreach (var update in packet.Updates)
            if (update.Type == LegacyMsgUpdateDataType.FineRain && update.Flag is 212 or 213)
            { entry = update; return true; }
        return false;
    }

    private static bool FineRainInitialTryVector(byte[] bytes, uint uid, out bool first, out bool second)
    {
        first = second = false;
        if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) || packet.Uid != uid) return false;
        var entry = packet.Updates.FirstOrDefault(update => update.Type == LegacyMsgUpdateDataType.StatusFlag);
        if (entry.Values is not { } values) return false;
        first = IsLegacyStatusFlagSet(values, 212);
        second = IsLegacyStatusFlagSet(values, 213);
        return true;
    }

    private static void FineRainInitialAssertCountdown(LegacyMsgUpdateEntry entry, uint flag, uint duration, uint power)
    {
        Assert.Equal(LegacyMsgUpdateDataType.FineRain, entry.Type);
        Assert.Equal(flag, entry.Flag);
        Assert.Equal(duration, entry.Time);
        Assert.Equal(power, entry.Damage);
        Assert.Equal(power, entry.SpellLevel);
    }

    private static void FineRainInitialAssert212Timer(WorldSession owner, DateTimeOffset timer, int seconds)
    {
        var attributes = owner.Character.Attributes;
        Assert.Equal(owner.Character.Uid.ToString(CultureInfo.InvariantCulture), attributes["LegacyFineRain212Invocation.OwnerUid"]);
        Assert.True(Guid.TryParse(attributes["LegacyFineRain212Invocation.Identity"], out _)); // Read PRODUCED identity, never predict it.
        Assert.Equal(seconds.ToString(CultureInfo.InvariantCulture), attributes["LegacyFineRain212Invocation.Seconds"]);
        Assert.Equal(timer.DateTime.Ticks.ToString(CultureInfo.InvariantCulture), attributes["LegacyFineRain212Invocation.TimerTicks"]);
        Assert.Equal(timer.DateTime.Ticks.ToString(CultureInfo.InvariantCulture), attributes["LegacyFineRain212Invocation.InvokerTicks"]);
    }

    private static void FineRainInitialAssertVectors(FineRainExpiryFixture fixture, int start,
        (uint Recipient, bool First, bool Second)[] expected)
    {
        var vectors = fixture.Order.ToArray().Skip(start)
            .Where(frame => FineRainInitialTryVector(frame.Bytes, PlayerUid, out _, out _)).ToArray();
        Assert.Equal(expected.Length, vectors.Length);
        for (var index = 0; index < vectors.Length; index++)
        {
            Assert.Equal(expected[index].Recipient, vectors[index].Recipient);
            Assert.True(FineRainInitialTryVector(vectors[index].Bytes, PlayerUid, out var first, out var second));
            Assert.Equal((expected[index].First, expected[index].Second), (first, second));
        }
    }
}
