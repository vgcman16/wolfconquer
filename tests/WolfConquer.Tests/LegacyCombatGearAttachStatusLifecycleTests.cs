using System.Globalization;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int CombatGearLifecycleUnrelatedStatusFlag = 0x15;
    private const string CombatGearLifecycleSentinelAttribute = "CombatGearLifecycleSentinel";
    private const string HawksAmbitionExpiresAtUtcAttribute = "LegacyHawksAmbitionExpiresAtUtc";
    private const string HawksAmbitionRemoveOnDeadAttribute = "LegacyHawksAmbitionRemoveOnDead";
    private const string BonePulseExpiresAtUtcAttribute = "BonePulseExpiresAtUtc";
    private const string LegacyBonePulseExpiresAtUtcAttribute = "LegacyBonePulseExpiresAtUtc";
    private const string BonePulseRemoveOnDeadAttribute = "LegacyBonePulseRemoveOnDead";
    private const string MudWallExpiresAtUtcAttribute = "LegacyMudWallExpiresAtUtc";
    private const string MudWallRemoveOnDeadAttribute = "LegacyMudWallRemoveOnDead";

    private static readonly string[] CombatGearLifecycleMetadataAttributes =
    [
        HawksAmbitionExpiresAtUtcAttribute,
        HawksAmbitionRemoveOnDeadAttribute,
        BonePulseExpiresAtUtcAttribute,
        LegacyBonePulseExpiresAtUtcAttribute,
        BonePulseRemoveOnDeadAttribute,
        MudWallExpiresAtUtcAttribute,
        MudWallRemoveOnDeadAttribute,
    ];

    [Theory]
    [InlineData(HawksAmbitionStatusFlag)]
    [InlineData(BonePulseStatusFlag)]
    [InlineData(MudWallStatusFlag)]
    public async Task RuntimeLoopCombatGearAttachStatusRemainsBeforeExpiryThenClearsAtDueBoundaryForVisiblePeers(
        int statusFlag)
    {
        var start = new DateTimeOffset(2026, 7, 13, 6, 0, 0, TimeSpan.Zero);
        var expiresAt = start.AddSeconds(2);
        var timeProvider = new ManualRuntimeTimeProvider(start);
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var ownerSent = new List<byte[]>();
        var viewerSent = new List<byte[]>();
        var ownerConnection = CreateSendingConnection(ownerSent);
        var viewerConnection = CreateSendingConnection(viewerSent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromSeconds(1),
                timeProvider),
            log: log);
        var owner = CreateSession(
            attributes: CreateCombatGearLifecycleAttributes(statusFlag, expiresAt));
        var viewer = CreateSession(
            uid: TargetPlayerUid,
            name: "VisibleViewer",
            x: owner.Character.X + 1,
            y: owner.Character.Y);
        SetHandlerSession(handler, ownerConnection, owner);
        SetHandlerSession(handler, viewerConnection, viewer);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, ownerConnection, owner, cancel.Token);
            await WaitForScheduledManualTimerAsync(timeProvider);

            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await WaitForScheduledManualTimerAsync(timeProvider);

            var beforeExpiry = AssertHandlerSession(handler, ownerConnection);
            Assert.True(HasLegacyStatusFlag(beforeExpiry.Character.Attributes, statusFlag));
            AssertCombatGearLifecycleMetadataPresent(beforeExpiry.Character.Attributes, statusFlag);
            Assert.DoesNotContain(
                ParseCombatGearLifecycleStatusVectors(ownerSent, PlayerUid),
                update => IsCombatGearLifecycleFlagCleared(update, statusFlag));
            Assert.DoesNotContain(
                ParseCombatGearLifecycleStatusVectors(viewerSent, PlayerUid),
                update => IsCombatGearLifecycleFlagCleared(update, statusFlag));
            ownerSent.Clear();
            viewerSent.Clear();

            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await WaitForScheduledManualTimerAsync(timeProvider);
            await WaitUntilAsync(
                () => IsCombatGearLifecycleExpiryObserved(
                    handler,
                    ownerConnection,
                    statusFlag,
                    ownerSent,
                    viewerSent),
                TimeSpan.FromSeconds(1));

            var expired = AssertHandlerSession(handler, ownerConnection);
            Assert.False(HasLegacyStatusFlag(expired.Character.Attributes, statusFlag));
            Assert.True(HasLegacyStatusFlag(
                expired.Character.Attributes,
                CombatGearLifecycleUnrelatedStatusFlag));
            AssertCombatGearLifecycleMetadataRemoved(expired.Character.Attributes, statusFlag);
            Assert.Equal(
                "preserve-me",
                expired.Character.Attributes[CombatGearLifecycleSentinelAttribute]);

            var ownerUpdate = Assert.Single(ParseCombatGearLifecycleStatusVectors(ownerSent, PlayerUid));
            var viewerUpdate = Assert.Single(ParseCombatGearLifecycleStatusVectors(viewerSent, PlayerUid));
            AssertCombatGearLifecycleClearedVector(ownerUpdate, statusFlag);
            AssertCombatGearLifecycleClearedVector(viewerUpdate, statusFlag);

            var expectedAppearanceRefreshCount = statusFlag == MudWallStatusFlag ? 1 : 0;
            Assert.Equal(
                expectedAppearanceRefreshCount,
                ParseShowEquipmentRefreshes(ownerSent).Count);
            Assert.Empty(ParseShowEquipmentRefreshes(viewerSent));
            Assert.Equal(
                expectedAppearanceRefreshCount,
                ParseCombatGearLifecyclePlayerSpawns(viewerSent, PlayerUid).Count);
        }
        finally
        {
            await cancel.CancelAsync();
            // SessionStore has weak connection keys; retain the viewer through expiry fanout.
            GC.KeepAlive(viewerConnection);
        }
    }

    [Theory]
    [InlineData(HawksAmbitionStatusFlag)]
    [InlineData(BonePulseStatusFlag)]
    [InlineData(MudWallStatusFlag)]
    public async Task RuntimeLoopCombatGearAttachStatusMetadataOnlyExpirySendsNoStatusVector(
        int statusFlag)
    {
        var start = new DateTimeOffset(2026, 7, 13, 7, 0, 0, TimeSpan.Zero);
        var expiresAt = start.AddSeconds(2);
        var timeProvider = new ManualRuntimeTimeProvider(start);
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var ownerSent = new List<byte[]>();
        var viewerSent = new List<byte[]>();
        var ownerConnection = CreateSendingConnection(ownerSent);
        var viewerConnection = CreateSendingConnection(viewerSent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromSeconds(1),
                timeProvider),
            log: log);
        var owner = CreateSession(
            attributes: CreateCombatGearLifecycleAttributes(
                statusFlag,
                expiresAt,
                includeTimedStatusFlag: false));
        var viewer = CreateSession(
            uid: TargetPlayerUid,
            name: "VisibleViewer",
            x: owner.Character.X + 1,
            y: owner.Character.Y);
        SetHandlerSession(handler, ownerConnection, owner);
        SetHandlerSession(handler, viewerConnection, viewer);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, ownerConnection, owner, cancel.Token);
            await WaitForScheduledManualTimerAsync(timeProvider);

            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await WaitForScheduledManualTimerAsync(timeProvider);

            var beforeExpiry = AssertHandlerSession(handler, ownerConnection);
            AssertCombatGearLifecycleMetadataPresent(beforeExpiry.Character.Attributes, statusFlag);
            ownerSent.Clear();
            viewerSent.Clear();

            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await WaitForScheduledManualTimerAsync(timeProvider);
            await WaitUntilAsync(
                () => IsCombatGearLifecycleMetadataCleanupObserved(
                    handler,
                    ownerConnection,
                    statusFlag),
                TimeSpan.FromSeconds(1));

            var cleaned = AssertHandlerSession(handler, ownerConnection);
            Assert.False(HasLegacyStatusFlag(cleaned.Character.Attributes, statusFlag));
            Assert.True(HasLegacyStatusFlag(
                cleaned.Character.Attributes,
                CombatGearLifecycleUnrelatedStatusFlag));
            AssertCombatGearLifecycleMetadataRemoved(cleaned.Character.Attributes, statusFlag);
            Assert.Equal(
                "preserve-me",
                cleaned.Character.Attributes[CombatGearLifecycleSentinelAttribute]);
            Assert.Empty(ParseCombatGearLifecycleStatusVectors(ownerSent, PlayerUid));
            Assert.Empty(ParseCombatGearLifecycleStatusVectors(viewerSent, PlayerUid));
        }
        finally
        {
            await cancel.CancelAsync();
            GC.KeepAlive(viewerConnection);
        }
    }

    [Fact]
    public async Task RuntimeLoopCoExpiringCombatGearAttachStatusesSendOneStatusVectorAndMudWallAppearanceRefresh()
    {
        var start = new DateTimeOffset(2026, 7, 13, 7, 30, 0, TimeSpan.Zero);
        var expiresAt = start.AddSeconds(2);
        var timeProvider = new ManualRuntimeTimeProvider(start);
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var ownerSent = new List<byte[]>();
        var viewerSent = new List<byte[]>();
        var ownerConnection = CreateSendingConnection(ownerSent);
        var viewerConnection = CreateSendingConnection(viewerSent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromSeconds(1),
                timeProvider),
            log: log);
        var owner = CreateSession(
            attributes: CreateAllCombatGearLifecycleAttributes(expiresAt));
        var viewer = CreateSession(
            uid: TargetPlayerUid,
            name: "VisibleViewer",
            x: owner.Character.X + 1,
            y: owner.Character.Y);
        SetHandlerSession(handler, ownerConnection, owner);
        SetHandlerSession(handler, viewerConnection, viewer);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, ownerConnection, owner, cancel.Token);
            await WaitForScheduledManualTimerAsync(timeProvider);

            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await WaitForScheduledManualTimerAsync(timeProvider);

            var beforeExpiry = AssertHandlerSession(handler, ownerConnection);
            Assert.True(HasLegacyStatusFlag(
                beforeExpiry.Character.Attributes,
                HawksAmbitionStatusFlag));
            Assert.True(HasLegacyStatusFlag(beforeExpiry.Character.Attributes, BonePulseStatusFlag));
            Assert.True(HasLegacyStatusFlag(beforeExpiry.Character.Attributes, MudWallStatusFlag));
            ownerSent.Clear();
            viewerSent.Clear();

            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await WaitForScheduledManualTimerAsync(timeProvider);
            await WaitUntilAsync(
                () => IsAllCombatGearLifecycleExpiryFanoutObserved(
                    handler,
                    ownerConnection,
                    ownerSent,
                    viewerSent),
                TimeSpan.FromSeconds(1));

            var expired = AssertHandlerSession(handler, ownerConnection);
            foreach (var statusFlag in new[]
                     {
                         HawksAmbitionStatusFlag,
                         BonePulseStatusFlag,
                         MudWallStatusFlag,
                     })
            {
                Assert.False(HasLegacyStatusFlag(expired.Character.Attributes, statusFlag));
            }

            Assert.DoesNotContain(
                CombatGearLifecycleMetadataAttributes,
                expired.Character.Attributes.ContainsKey);

            var ownerUpdate = Assert.Single(ParseCombatGearLifecycleStatusVectors(ownerSent, PlayerUid));
            var viewerUpdate = Assert.Single(ParseCombatGearLifecycleStatusVectors(viewerSent, PlayerUid));
            AssertAllCombatGearLifecycleFlagsCleared(ownerUpdate);
            AssertAllCombatGearLifecycleFlagsCleared(viewerUpdate);

            var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(ownerSent));
            Assert.Equal(
                LegacyShowEquipmentPacket.CreateEquipmentRefresh(
                    owner.PlayerItems,
                    owner.AlternateEquipmentActive),
                equipmentRefresh);
            Assert.Empty(ParseShowEquipmentRefreshes(viewerSent));

            var viewerSpawn = Assert.Single(
                ParseCombatGearLifecyclePlayerSpawns(viewerSent, PlayerUid));
            AssertAllCombatGearLifecycleFlagsCleared(viewerSpawn.StatusFlags);

            var ownerFrames = SnapshotFrames(ownerSent);
            var viewerFrames = SnapshotFrames(viewerSent);
            var ownerStatusIndex = SingleFrameIndex(
                ownerFrames,
                frame => IsCombatGearLifecycleStatusVector(frame, PlayerUid));
            var equipmentIndex = SingleFrameIndex(
                ownerFrames,
                frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
            var viewerStatusIndex = SingleFrameIndex(
                viewerFrames,
                frame => IsCombatGearLifecycleStatusVector(frame, PlayerUid));
            var spawnIndex = SingleFrameIndex(
                viewerFrames,
                frame => LegacyPlayerSpawnPacket.TryParse(frame, out var spawn) &&
                    spawn.GetNumericField(2) == PlayerUid);
            Assert.True(ownerStatusIndex < equipmentIndex);
            Assert.True(viewerStatusIndex < spawnIndex);
            Assert.Contains(
                "WorldServer sent legacy MudWall expiry QueryEquipment-equivalent appearance refresh",
                log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
            GC.KeepAlive(viewerConnection);
        }
    }

    [Fact]
    public void PlayerDeathClearsAllCombatGearAttachStatusTimerFamiliesAndPreservesUnrelatedAttributes()
    {
        var now = new DateTimeOffset(2026, 7, 13, 8, 0, 0, TimeSpan.Zero);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(
                HawksAmbitionStatusFlag,
                BonePulseStatusFlag,
                MudWallStatusFlag),
            [HawksAmbitionExpiresAtUtcAttribute] = FormatCombatGearLifecycleTimestamp(now.AddMinutes(1)),
            [HawksAmbitionRemoveOnDeadAttribute] = bool.TrueString,
            [BonePulseExpiresAtUtcAttribute] = FormatCombatGearLifecycleTimestamp(now.AddMinutes(2)),
            [LegacyBonePulseExpiresAtUtcAttribute] = FormatCombatGearLifecycleTimestamp(now.AddMinutes(2)),
            [BonePulseRemoveOnDeadAttribute] = bool.TrueString,
            [MudWallExpiresAtUtcAttribute] = FormatCombatGearLifecycleTimestamp(now.AddMinutes(3)),
            [MudWallRemoveOnDeadAttribute] = bool.TrueString,
            [CombatGearLifecycleSentinelAttribute] = "preserve-me",
        };
        var session = CreateSession(attributes: attributes);

        var death = LegacyPlayerDeathState.ApplyDeath(
            session.Character,
            now,
            checked((ushort)session.Character.X),
            checked((ushort)session.Character.Y));

        Assert.True(HasLegacyStatusFlag(
            death.Character.Attributes,
            LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(HasLegacyStatusFlag(death.Character.Attributes, HawksAmbitionStatusFlag));
        Assert.False(HasLegacyStatusFlag(death.Character.Attributes, BonePulseStatusFlag));
        Assert.False(HasLegacyStatusFlag(death.Character.Attributes, MudWallStatusFlag));
        Assert.DoesNotContain(
            CombatGearLifecycleMetadataAttributes,
            death.Character.Attributes.ContainsKey);
        Assert.Equal(
            "preserve-me",
            death.Character.Attributes[CombatGearLifecycleSentinelAttribute]);
    }

    private static Dictionary<string, string> CreateCombatGearLifecycleAttributes(
        int statusFlag,
        DateTimeOffset expiresAt,
        bool includeTimedStatusFlag = true)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = includeTimedStatusFlag
                ? BuildStatusFlags(statusFlag, CombatGearLifecycleUnrelatedStatusFlag)
                : BuildStatusFlags(CombatGearLifecycleUnrelatedStatusFlag),
            [CombatGearLifecycleSentinelAttribute] = "preserve-me",
        };
        var timestamp = FormatCombatGearLifecycleTimestamp(expiresAt);

        switch (statusFlag)
        {
            case HawksAmbitionStatusFlag:
                attributes[HawksAmbitionExpiresAtUtcAttribute] = timestamp;
                attributes[HawksAmbitionRemoveOnDeadAttribute] = bool.TrueString;
                break;
            case BonePulseStatusFlag:
                attributes[BonePulseExpiresAtUtcAttribute] = timestamp;
                attributes[LegacyBonePulseExpiresAtUtcAttribute] = timestamp;
                attributes[BonePulseRemoveOnDeadAttribute] = bool.TrueString;
                break;
            case MudWallStatusFlag:
                attributes[MudWallExpiresAtUtcAttribute] = timestamp;
                attributes[MudWallRemoveOnDeadAttribute] = bool.TrueString;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(statusFlag), statusFlag, "Unknown timed status flag.");
        }

        return attributes;
    }

    private static Dictionary<string, string> CreateAllCombatGearLifecycleAttributes(
        DateTimeOffset expiresAt)
    {
        var timestamp = FormatCombatGearLifecycleTimestamp(expiresAt);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(
                HawksAmbitionStatusFlag,
                BonePulseStatusFlag,
                MudWallStatusFlag,
                CombatGearLifecycleUnrelatedStatusFlag),
            [HawksAmbitionExpiresAtUtcAttribute] = timestamp,
            [HawksAmbitionRemoveOnDeadAttribute] = bool.TrueString,
            [BonePulseExpiresAtUtcAttribute] = timestamp,
            [LegacyBonePulseExpiresAtUtcAttribute] = timestamp,
            [BonePulseRemoveOnDeadAttribute] = bool.TrueString,
            [MudWallExpiresAtUtcAttribute] = timestamp,
            [MudWallRemoveOnDeadAttribute] = bool.TrueString,
            [CombatGearLifecycleSentinelAttribute] = "preserve-me",
        };
    }

    private static void AssertCombatGearLifecycleMetadataPresent(
        IReadOnlyDictionary<string, string> attributes,
        int statusFlag)
    {
        foreach (var attribute in GetCombatGearLifecycleMetadataAttributes(statusFlag))
        {
            Assert.True(attributes.ContainsKey(attribute), $"Expected metadata attribute '{attribute}'.");
        }
    }

    private static void AssertCombatGearLifecycleMetadataRemoved(
        IReadOnlyDictionary<string, string> attributes,
        int statusFlag)
    {
        foreach (var attribute in GetCombatGearLifecycleMetadataAttributes(statusFlag))
        {
            Assert.False(attributes.ContainsKey(attribute), $"Unexpected metadata attribute '{attribute}'.");
        }
    }

    private static IReadOnlyList<string> GetCombatGearLifecycleMetadataAttributes(int statusFlag)
    {
        return statusFlag switch
        {
            HawksAmbitionStatusFlag =>
            [
                HawksAmbitionExpiresAtUtcAttribute,
                HawksAmbitionRemoveOnDeadAttribute,
            ],
            BonePulseStatusFlag =>
            [
                BonePulseExpiresAtUtcAttribute,
                LegacyBonePulseExpiresAtUtcAttribute,
                BonePulseRemoveOnDeadAttribute,
            ],
            MudWallStatusFlag =>
            [
                MudWallExpiresAtUtcAttribute,
                MudWallRemoveOnDeadAttribute,
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(statusFlag), statusFlag, "Unknown timed status flag."),
        };
    }

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseCombatGearLifecycleStatusVectors(
        IEnumerable<byte[]> frames,
        uint uid)
    {
        return ParseMsgUpdates(frames)
            .Where(update =>
                update.Uid == uid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag))
            .ToArray();
    }

    private static void AssertCombatGearLifecycleClearedVector(
        LegacyMsgUpdatePacket update,
        int statusFlag)
    {
        var entry = Assert.Single(
            update.Updates,
            candidate => candidate.Type == LegacyMsgUpdateDataType.StatusFlag);
        Assert.NotNull(entry.Values);
        Assert.False(IsLegacyStatusFlagSet(entry.Values!, statusFlag));
        Assert.True(IsLegacyStatusFlagSet(
            entry.Values!,
            CombatGearLifecycleUnrelatedStatusFlag));
    }

    private static void AssertAllCombatGearLifecycleFlagsCleared(LegacyMsgUpdatePacket update)
    {
        var entry = Assert.Single(
            update.Updates,
            candidate => candidate.Type == LegacyMsgUpdateDataType.StatusFlag);
        Assert.NotNull(entry.Values);
        AssertAllCombatGearLifecycleFlagsCleared(entry.Values!);
    }

    private static void AssertAllCombatGearLifecycleFlagsCleared(IReadOnlyList<ulong> values)
    {
        Assert.False(IsLegacyStatusFlagSet(values, HawksAmbitionStatusFlag));
        Assert.False(IsLegacyStatusFlagSet(values, BonePulseStatusFlag));
        Assert.False(IsLegacyStatusFlagSet(values, MudWallStatusFlag));
        Assert.True(IsLegacyStatusFlagSet(values, CombatGearLifecycleUnrelatedStatusFlag));
    }

    private static bool IsCombatGearLifecycleFlagCleared(
        LegacyMsgUpdatePacket update,
        int statusFlag)
    {
        return update.Updates.Any(entry =>
            entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is not null &&
            !IsLegacyStatusFlagSet(entry.Values, statusFlag));
    }

    private static IReadOnlyList<LegacyPlayerSpawnPacket> ParseCombatGearLifecyclePlayerSpawns(
        IEnumerable<byte[]> frames,
        uint uid)
    {
        var spawns = new List<LegacyPlayerSpawnPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyPlayerSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
                spawn.GetNumericField(2) == uid)
            {
                spawns.Add(spawn);
            }
        }

        return spawns;
    }

    private static bool IsCombatGearLifecycleExpiryObserved(
        WorldServerFrameHandler handler,
        ConquerTcpConnection ownerConnection,
        int statusFlag,
        IEnumerable<byte[]> ownerFrames,
        IEnumerable<byte[]> viewerFrames)
    {
        return GetHandlerSessions(handler).TryGet(ownerConnection, out var session) &&
            session is not null &&
            !HasLegacyStatusFlag(session.Character.Attributes, statusFlag) &&
            GetCombatGearLifecycleMetadataAttributes(statusFlag)
                .All(attribute => !session.Character.Attributes.ContainsKey(attribute)) &&
            ParseCombatGearLifecycleStatusVectors(ownerFrames, PlayerUid).Count == 1 &&
            ParseCombatGearLifecycleStatusVectors(viewerFrames, PlayerUid).Count == 1;
    }

    private static bool IsCombatGearLifecycleMetadataCleanupObserved(
        WorldServerFrameHandler handler,
        ConquerTcpConnection ownerConnection,
        int statusFlag)
    {
        return GetHandlerSessions(handler).TryGet(ownerConnection, out var session) &&
            session is not null &&
            GetCombatGearLifecycleMetadataAttributes(statusFlag)
                .All(attribute => !session.Character.Attributes.ContainsKey(attribute));
    }

    private static bool IsAllCombatGearLifecycleExpiryFanoutObserved(
        WorldServerFrameHandler handler,
        ConquerTcpConnection ownerConnection,
        IEnumerable<byte[]> ownerFrames,
        IEnumerable<byte[]> viewerFrames)
    {
        if (!GetHandlerSessions(handler).TryGet(ownerConnection, out var session) ||
            session is null ||
            HasLegacyStatusFlag(session.Character.Attributes, HawksAmbitionStatusFlag) ||
            HasLegacyStatusFlag(session.Character.Attributes, BonePulseStatusFlag) ||
            HasLegacyStatusFlag(session.Character.Attributes, MudWallStatusFlag) ||
            CombatGearLifecycleMetadataAttributes.Any(session.Character.Attributes.ContainsKey))
        {
            return false;
        }

        return ParseCombatGearLifecycleStatusVectors(ownerFrames, PlayerUid).Count == 1 &&
            ParseCombatGearLifecycleStatusVectors(viewerFrames, PlayerUid).Count == 1 &&
            ParseShowEquipmentRefreshes(ownerFrames).Count == 1 &&
            ParseCombatGearLifecyclePlayerSpawns(viewerFrames, PlayerUid).Count == 1;
    }

    private static bool IsCombatGearLifecycleStatusVector(PacketFrame frame, uint uid)
    {
        return LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
            update.Uid == uid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag);
    }

    private static async Task WaitForScheduledManualTimerAsync(ManualRuntimeTimeProvider timeProvider)
    {
        await WaitUntilAsync(
            () => timeProvider.ScheduledTimerCount >= 2,
            TimeSpan.FromSeconds(1));
    }

    private static string FormatCombatGearLifecycleTimestamp(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private sealed class ManualRuntimeTimeProvider : TimeProvider
    {
        private readonly object _gate = new();
        private readonly HashSet<ManualRuntimeTimer> _timers = [];
        private DateTimeOffset _utcNow;
        private long _timestamp;

        public ManualRuntimeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow.ToUniversalTime();
        }

        public int ScheduledTimerCount
        {
            get
            {
                lock (_gate)
                {
                    return _timers.Count(timer => timer.IsScheduled);
                }
            }
        }

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow()
        {
            lock (_gate)
            {
                return _utcNow;
            }
        }

        public override long GetTimestamp()
        {
            lock (_gate)
            {
                return _timestamp;
            }
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            ArgumentNullException.ThrowIfNull(callback);
            var timer = new ManualRuntimeTimer(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan amount)
        {
            if (amount < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Manual time cannot move backwards.");
            }

            ManualRuntimeTimer[] dueTimers;
            lock (_gate)
            {
                _utcNow += amount;
                _timestamp = checked(_timestamp + amount.Ticks);
                dueTimers = _timers
                    .Where(timer => timer.PrepareToFire(_timestamp))
                    .ToArray();
            }

            foreach (var timer in dueTimers)
            {
                timer.Fire();
            }
        }

        private bool Change(
            ManualRuntimeTimer timer,
            TimeSpan dueTime,
            TimeSpan period)
        {
            if (dueTime < Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(dueTime));
            }

            if (period < Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(period));
            }

            lock (_gate)
            {
                if (timer.IsDisposed)
                {
                    return false;
                }

                _timers.Add(timer);
                timer.DueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : checked(_timestamp + dueTime.Ticks);
                timer.Period = period;
                return true;
            }
        }

        private void Dispose(ManualRuntimeTimer timer)
        {
            lock (_gate)
            {
                timer.IsDisposed = true;
                timer.DueTimestamp = long.MaxValue;
                _timers.Remove(timer);
            }
        }

        private sealed class ManualRuntimeTimer : ITimer
        {
            private readonly ManualRuntimeTimeProvider _owner;
            private readonly TimerCallback _callback;
            private readonly object? _state;

            public ManualRuntimeTimer(
                ManualRuntimeTimeProvider owner,
                TimerCallback callback,
                object? state)
            {
                _owner = owner;
                _callback = callback;
                _state = state;
                DueTimestamp = long.MaxValue;
                Period = Timeout.InfiniteTimeSpan;
            }

            public bool IsDisposed { get; set; }

            public long DueTimestamp { get; set; }

            public TimeSpan Period { get; set; }

            public bool IsScheduled => !IsDisposed && DueTimestamp != long.MaxValue;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                return _owner.Change(this, dueTime, period);
            }

            public void Dispose()
            {
                _owner.Dispose(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public bool PrepareToFire(long timestamp)
            {
                if (!IsScheduled || DueTimestamp > timestamp)
                {
                    return false;
                }

                DueTimestamp = Period == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : checked(timestamp + Period.Ticks);
                return true;
            }

            public void Fire()
            {
                _callback(_state);
            }
        }
    }
}
