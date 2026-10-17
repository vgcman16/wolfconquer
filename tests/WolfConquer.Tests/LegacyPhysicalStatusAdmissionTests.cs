using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(5_999, false)]
    [InlineData(6_000, true)]
    [InlineData(-1, false)]
    [InlineData(int.MinValue, false)]
    public void PhysicalStatusAdmissionStartsAtSourceSignedZero(int now, bool admitted)
    {
        var session = StatusAdmissionSession(fineRain: true);
        var result = LegacyEquipmentRebuildAdmission.ApplyCurrent(
            session, LegacyEquipmentRebuildReason.FineRainChange, new StatusAdmissionClock(now));
        Assert.Equal(admitted, result.Admitted);
        Assert.Equal(admitted ? now : 0,
            LegacyFineRainEquipmentAdmission.ForOwner(result.Session).StampMilliseconds);
        var overlay = LegacyPhysicalStatusOverlay.ForOwner(result.Session);
        Assert.Equal(admitted ? null : (uint?)66, overlay.Breakthrough);
        Assert.Equal(admitted ? null : (uint?)75, overlay.Counteraction);
        Assert.Equal(StatusAdmissionDeadline, overlay.FrontBreakExpiresAtUtc);
        Assert.Equal(66U, LegacyPhysicalStatusOverlay.ForOwner(session).Breakthrough);
        Assert.Null(session.FineRainEquipmentAdmission);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5_999)]
    [InlineData(6_000)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void PhysicalStatusAdmissionWithoutFineRainClearsOnlyValuesAndPreservesStamp(int now)
    {
        var session = StatusAdmissionSession(fineRain: false) with
        {
            FineRainEquipmentAdmission = new LegacyFineRainEquipmentAdmission(PlayerUid, 12_345),
        };
        var result = LegacyEquipmentRebuildAdmission.ApplyCurrent(
            session, LegacyEquipmentRebuildReason.EquipmentMutation, new StatusAdmissionClock(now));
        Assert.True(result.Admitted);
        var overlay = LegacyPhysicalStatusOverlay.ForOwner(result.Session);
        Assert.Null(overlay.Breakthrough);
        Assert.Null(overlay.Counteraction);
        Assert.Equal(StatusAdmissionDeadline, overlay.FrontBreakExpiresAtUtc);
        Assert.Equal(12_345, LegacyFineRainEquipmentAdmission.ForOwner(result.Session).StampMilliseconds);
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, DirectOverlayFrontBreakFlag));
        Assert.Equal(66U, LegacyPhysicalStatusOverlay.ForOwner(session).Breakthrough);
    }

    [Fact]
    public void PhysicalStatusAdmissionFineRainTwoAloneDoesNotGateRebuild()
    {
        var session = StatusAdmissionSession(fineRain: false) with
        {
            FineRainEquipmentAdmission = new LegacyFineRainEquipmentAdmission(PlayerUid, 10_000),
        };
        session = session with
        {
            Character = session.Character with
            {
                Attributes = new Dictionary<string, string>(session.Character.Attributes)
                {
                    ["StatusFlags"] = BuildStatusFlags(213, DirectOverlayFrontBreakFlag),
                },
            },
        };
        var result = LegacyEquipmentRebuildAdmission.ApplyCurrent(
            session, LegacyEquipmentRebuildReason.RuneStorageQuery, new StatusAdmissionClock(10_001));
        Assert.True(result.Admitted);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(result.Session).StampMilliseconds);
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(result.Session).Breakthrough);
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(result.Session).Counteraction);
    }

    [Fact]
    public void PhysicalStatusAdmissionRejectedRequestsDoNotRenewTheSixSecondStamp()
    {
        var current = StatusAdmissionSession(fineRain: true) with
        {
            FineRainEquipmentAdmission = new LegacyFineRainEquipmentAdmission(PlayerUid, 10_000),
        };
        foreach (var now in new[] { 14_000, 15_999 })
        {
            var rejected = LegacyEquipmentRebuildAdmission.ApplyCurrent(
                current, LegacyEquipmentRebuildReason.RuneStorageQuery, new StatusAdmissionClock(now));
            Assert.False(rejected.Admitted);
            Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(rejected.Session).StampMilliseconds);
            Assert.Equal(66U, LegacyPhysicalStatusOverlay.ForOwner(rejected.Session).Breakthrough);
            Assert.Equal(75U, LegacyPhysicalStatusOverlay.ForOwner(rejected.Session).Counteraction);
            current = rejected.Session;
        }
        var equal = LegacyEquipmentRebuildAdmission.ApplyCurrent(
            current, LegacyEquipmentRebuildReason.RuneStorageQuery, new StatusAdmissionClock(16_000));
        Assert.True(equal.Admitted);
        Assert.Equal(16_000, LegacyFineRainEquipmentAdmission.ForOwner(equal.Session).StampMilliseconds);
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(equal.Session).Breakthrough);
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(equal.Session).Counteraction);
    }

    [Theory]
    [InlineData(int.MaxValue - 1_000, int.MaxValue - 1_000, true)]
    [InlineData(int.MaxValue - 1_000, int.MinValue, false)]
    [InlineData(int.MaxValue - 1_000, -2_147_478_649, true)]
    [InlineData(int.MinValue, int.MinValue + 5_999, false)]
    [InlineData(int.MinValue, int.MinValue + 6_000, true)]
    public void PhysicalStatusAdmissionUsesUncheckedAdditionAndDirectSignedComparison(
        int stamp, int now, bool admitted)
    {
        var session = StatusAdmissionSession(fineRain: true) with
        {
            FineRainEquipmentAdmission = new LegacyFineRainEquipmentAdmission(PlayerUid, stamp),
        };
        var result = LegacyEquipmentRebuildAdmission.ApplyCurrent(
            session, LegacyEquipmentRebuildReason.FrontBreakExpiry, new StatusAdmissionClock(now));
        Assert.Equal(admitted, result.Admitted);
        Assert.Equal(admitted ? now : stamp,
            LegacyFineRainEquipmentAdmission.ForOwner(result.Session).StampMilliseconds);
        Assert.Equal(admitted ? null : (uint?)66,
            LegacyPhysicalStatusOverlay.ForOwner(result.Session).Breakthrough);
    }

    [Fact]
    public void PhysicalStatusAdmissionOwnerMismatchNeverImportsAnotherPlayersState()
    {
        var session = StatusAdmissionSession(fineRain: true) with
        {
            PhysicalStatusOverlay = new LegacyPhysicalStatusOverlay(TargetPlayerUid, 999, 888, StatusAdmissionDeadline),
            FineRainEquipmentAdmission = new LegacyFineRainEquipmentAdmission(TargetPlayerUid, 20_000),
        };
        var freshOverlay = LegacyPhysicalStatusOverlay.ForOwner(session);
        Assert.Equal((long)PlayerUid, freshOverlay.OwnerUid);
        Assert.Null(freshOverlay.Breakthrough);
        Assert.Null(freshOverlay.Counteraction);
        Assert.Null(freshOverlay.FrontBreakExpiresAtUtc);
        Assert.Equal((long)PlayerUid, LegacyFineRainEquipmentAdmission.ForOwner(session).OwnerUid);
        Assert.Equal(0, LegacyFineRainEquipmentAdmission.ForOwner(session).StampMilliseconds);
        var result = LegacyEquipmentRebuildAdmission.ApplyCurrent(
            session, LegacyEquipmentRebuildReason.FineRainChange, new StatusAdmissionClock(6_000));
        Assert.True(result.Admitted); // Foreign stamp20000 must not impose deadline26000.
        Assert.Equal(6_000, LegacyFineRainEquipmentAdmission.ForOwner(result.Session).StampMilliseconds);
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(result.Session).Breakthrough);
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(result.Session).Counteraction);
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(result.Session).FrontBreakExpiresAtUtc);
    }

    [Theory]
    [InlineData("both-null", 60U, 50U)]
    [InlineData("one-null", 66U, 50U)]
    [InlineData("both-zero", 0U, 0U)]
    [InlineData("both-set", 66U, 75U)]
    public void PhysicalStatusAdmissionRejectedMutationMaterializesTheEntirePreMutationPair(
        string state, uint beforeBreakthrough, uint beforeCounteraction)
    {
        var before = StatusAdmissionSession(fineRain: true) with
        {
            PhysicalStatusOverlay = new LegacyPhysicalStatusOverlay(PlayerUid,
                state is "one-null" or "both-set" ? beforeBreakthrough : null,
                state == "both-set" ? beforeCounteraction : null, StatusAdmissionDeadline),
            FineRainEquipmentAdmission = new LegacyFineRainEquipmentAdmission(PlayerUid, 10_000),
        };
        var accepted = before with
        {
            PlayerItems = before.PlayerItems with
            {
                EquipmentItems = [new PlayerItemSnapshotItem { Uid = 90_002, ItemId = 111_070, Position = 1 }],
            },
        };
        var result = LegacyEquipmentRebuildAdmission.ApplyEquipmentMutation(before, accepted,
            new LegacyPhysicalStatusValues(beforeBreakthrough, beforeCounteraction),
            LegacyEquipmentRebuildReason.EquipmentMutation, new StatusAdmissionClock(10_001));
        Assert.False(result.Admitted);
        Assert.Same(accepted.PlayerItems, result.Session.PlayerItems); // Placement is accepted, never rolled back.
        var overlay = LegacyPhysicalStatusOverlay.ForOwner(result.Session);
        Assert.Equal((uint?)beforeBreakthrough, overlay.Breakthrough);
        Assert.Equal((uint?)beforeCounteraction, overlay.Counteraction); // Null and actual zero are distinct.
        Assert.Equal(StatusAdmissionDeadline, overlay.FrontBreakExpiresAtUtc);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(result.Session).StampMilliseconds);
        Assert.Equal(state is "one-null" or "both-set" ? (uint?)beforeBreakthrough : null,
            LegacyPhysicalStatusOverlay.ForOwner(before).Breakthrough);
    }

    [Fact]
    public void PhysicalStatusAdmissionAdmittedMutationClearsThePairButKeepsAcceptedItemsAndLifetime()
    {
        var before = StatusAdmissionSession(fineRain: true) with
        {
            FineRainEquipmentAdmission = new LegacyFineRainEquipmentAdmission(PlayerUid, 10_000),
        };
        var accepted = before with { PlayerItems = before.PlayerItems with { InventoryItems = [] } };
        var result = LegacyEquipmentRebuildAdmission.ApplyEquipmentMutation(before, accepted,
            new LegacyPhysicalStatusValues(66, 75), LegacyEquipmentRebuildReason.EquipmentMutation,
            new StatusAdmissionClock(16_000));
        Assert.True(result.Admitted);
        Assert.Same(accepted.PlayerItems, result.Session.PlayerItems);
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(result.Session).Breakthrough);
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(result.Session).Counteraction);
        Assert.Equal(StatusAdmissionDeadline, LegacyPhysicalStatusOverlay.ForOwner(result.Session).FrontBreakExpiresAtUtc);
        Assert.Equal(16_000, LegacyFineRainEquipmentAdmission.ForOwner(result.Session).StampMilliseconds);
    }

    [Fact]
    public void PhysicalStatusAdmissionRejectsMutationAcrossDifferentAcceptedUids()
    {
        var before = StatusAdmissionSession(fineRain: true);
        var accepted = before with { Character = before.Character with { Uid = TargetPlayerUid } };
        Assert.Throws<ArgumentException>(() => LegacyEquipmentRebuildAdmission.ApplyEquipmentMutation(
            before, accepted, new LegacyPhysicalStatusValues(66, 75),
            LegacyEquipmentRebuildReason.EquipmentMutation, new StatusAdmissionClock(6_000)));
    }

    [Fact]
    public void PhysicalStatusAdmissionReadsAgainForTheAdmittedStampOnly()
    {
        var admittedClock = new StatusAdmissionSequenceClock([6_000, 6_001]);
        var admitted = LegacyEquipmentRebuildAdmission.ApplyCurrent(StatusAdmissionSession(fineRain: true),
            LegacyEquipmentRebuildReason.FineRainChange, admittedClock);
        Assert.True(admitted.Admitted);
        Assert.Equal(6_001, LegacyFineRainEquipmentAdmission.ForOwner(admitted.Session).StampMilliseconds);
        Assert.Equal(2, admittedClock.Reads);
        var rejectedClock = new StatusAdmissionSequenceClock([5_999]);
        var rejected = LegacyEquipmentRebuildAdmission.ApplyCurrent(StatusAdmissionSession(fineRain: true),
            LegacyEquipmentRebuildReason.FineRainChange, rejectedClock);
        Assert.False(rejected.Admitted);
        Assert.Equal(0, LegacyFineRainEquipmentAdmission.ForOwner(rejected.Session).StampMilliseconds);
        Assert.Equal(1, rejectedClock.Reads);
        var ordinaryClock = new StatusAdmissionSequenceClock([]);
        var ordinary = LegacyEquipmentRebuildAdmission.ApplyCurrent(StatusAdmissionSession(fineRain: false),
            LegacyEquipmentRebuildReason.EquipmentMutation, ordinaryClock);
        Assert.True(ordinary.Admitted);
        Assert.Equal(0, ordinaryClock.Reads);
    }

    [Fact]
    public void PhysicalStatusClockCapturesAnExplicitEpochAndUsesWholeElapsedMilliseconds()
    {
        var provider = new StatusAdmissionTimestampProvider(1_234_567_890_000);
        var clock = LegacySignedMillisecondClock.FromTimeProvider(provider, epochMilliseconds: 5_999);
        Assert.Equal(5_999, clock.GetMilliseconds()); // Provider's arbitrary timestamp origin is NOT source epoch.
        provider.AdvanceTicks(TimeSpan.TicksPerMillisecond - 1);
        Assert.Equal(5_999, clock.GetMilliseconds());
        provider.AdvanceTicks(1);
        Assert.Equal(6_000, clock.GetMilliseconds());
        provider.AdvanceTicks(TimeSpan.TicksPerMillisecond);
        Assert.Equal(6_001, clock.GetMilliseconds());
    }

    [Fact]
    public void PhysicalStatusClockUncheckedWrapsSignedAndCompleteUnsignedCycles()
    {
        var provider = new StatusAdmissionTimestampProvider(987_654_321);
        var clock = LegacySignedMillisecondClock.FromTimeProvider(provider, int.MaxValue - 1);
        provider.AdvanceTicks(2 * TimeSpan.TicksPerMillisecond);
        Assert.Equal(int.MinValue, clock.GetMilliseconds());
        provider.AdvanceTicks(4_294_967_296L * TimeSpan.TicksPerMillisecond);
        Assert.Equal(int.MinValue, clock.GetMilliseconds());
        provider.AdvanceTicks(TimeSpan.TicksPerMillisecond);
        Assert.Equal(int.MinValue + 1, clock.GetMilliseconds());
    }

    private static readonly DateTimeOffset StatusAdmissionDeadline =
        new(2026, 10, 15, 12, 0, 10, TimeSpan.Zero);

    private static WorldSession StatusAdmissionSession(bool fineRain)
    {
        return CreateSession(attributes: new Dictionary<string, string>
        {
            ["StatusFlags"] = fineRain
                ? BuildStatusFlags(212, DirectOverlayFrontBreakFlag)
                : BuildStatusFlags(DirectOverlayFrontBreakFlag),
        }) with
        {
            PhysicalStatusOverlay = new LegacyPhysicalStatusOverlay(PlayerUid, 66, 75, StatusAdmissionDeadline),
        };
    }

    private sealed class StatusAdmissionClock(int milliseconds) : ILegacySignedMillisecondClock
    {
        public int GetMilliseconds() => milliseconds;
    }

    private sealed class StatusAdmissionTimestampProvider(long initialTimestamp) : TimeProvider
    {
        private long _timestamp = initialTimestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public override DateTimeOffset GetUtcNow() => new(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public void AdvanceTicks(long ticks) => _timestamp = checked(_timestamp + ticks);
    }

    private sealed class StatusAdmissionSequenceClock(int[] readings) : ILegacySignedMillisecondClock
    {
        public int Reads { get; private set; }
        public int GetMilliseconds()
        {
            Assert.True(Reads < readings.Length, "Unexpected extra signed-clock read.");
            return readings[Reads++];
        }
    }
}
