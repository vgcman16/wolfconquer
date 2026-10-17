using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDragonWarRuntimeTransitionTests
{
    private static readonly DateTimeOffset OpenedAt =
        new(2026, 6, 28, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RuntimeReviveMutationWithinMap700SendsMap700TeleportTransitionWithoutMerchantStartup()
    {
        using var log = new StringWriter(CultureInfo.InvariantCulture);
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log: log);
        var sessions = GetSessions(handler);
        var dragonWarStates = GetDragonWarStates(handler);
        Assert.NotNull(dragonWarStates.Open(OpenedAt));

        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent, 9958);
        var joined = JoinDragonWar(dragonWarStates, CreateSession(1_293_600, "RevivedDragon"));
        var tickAt = OpenedAt.AddSeconds(20);
        var deadSession = joined with
        {
            Character = joined.Character with
            {
                X = 1,
                Y = 1,
                MinHitPoints = 0,
                Attributes = CreateDeadDragonWarAttributes(joined.Character.Attributes, tickAt.AddSeconds(-5)),
            },
        };
        sessions.Set(connection, deadSession);
        dragonWarStates.SetProcess(LegacyDragonWarProcess.Alive);

        await InvokeDragonWarRuntimeTickAsync(handler, tickAt);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(LegacyDragonWarRuntimeStateStore.DragonWarMapId, updated.Character.MapId);
        Assert.NotEqual(deadSession.Character.X, updated.Character.X);
        Assert.NotEqual(deadSession.Character.Y, updated.Character.Y);
        AssertSameMapTeleportHistory(deadSession, updated);

        Assert.Contains(sent, IsLegacyStatusFrame);
        Assert.Contains(sent, frame => IsAction(frame, MsgActionType.Teleport));
        Assert.Contains(sent, frame => IsAction(frame, MsgActionType.StopVending));
        Assert.Contains(sent, frame => IsAction(frame, MsgActionType.LegacyColor));
        Assert.Contains(sent, IsLegacyMapStatusFrame);
        Assert.DoesNotContain(sent, frame => IsAction(frame, MsgActionType.LegacyMerchantStartup));
        var transition = AssertMap700Transition(sent, updated);
        var revivedStatusIndex = FindStatusFlagVectorIndex(
            sent,
            (uint)updated.Character.Uid,
            LegacyPlayerDeathState.CreateStatusFlagVector(
                updated.Character.Attributes,
                LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength));
        Assert.True(revivedStatusIndex > transition.MapStatusIndex);
    }

    [Fact]
    public async Task RuntimeEndMutationFromMap700ToTwinCitySendsSourceMapAwareTeleportTransitionToEveryParticipant()
    {
        using var log = new StringWriter(CultureInfo.InvariantCulture);
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log: log);
        var sessions = GetSessions(handler);
        var dragonWarStates = GetDragonWarStates(handler);
        Assert.NotNull(dragonWarStates.Open(OpenedAt));

        var winnerSent = new List<byte[]>();
        var runnerUpSent = new List<byte[]>();
        var winnerConnection = CreateSendingConnection(winnerSent, 9959);
        var runnerUpConnection = CreateSendingConnection(runnerUpSent, 9960);
        var winner = JoinDragonWar(
            dragonWarStates,
            CreateSession(
                1_293_601,
                "DragonWinner",
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DragonWarScore"] = "90",
                }));
        var runnerUp = JoinDragonWar(
            dragonWarStates,
            CreateSession(
                1_293_602,
                "DragonRunner",
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DragonWarScore"] = "50",
                }));
        sessions.Set(winnerConnection, winner);
        sessions.Set(runnerUpConnection, runnerUp);
        dragonWarStates.SetProcess(LegacyDragonWarProcess.Alive);

        await InvokeDragonWarRuntimeTickAsync(handler, OpenedAt.AddMinutes(5).AddSeconds(1));

        Assert.True(sessions.TryGet(winnerConnection, out var updatedWinner));
        Assert.NotNull(updatedWinner);
        Assert.True(sessions.TryGet(runnerUpConnection, out var updatedRunnerUp));
        Assert.NotNull(updatedRunnerUp);
        AssertTwinCityTeleportHistory(winner, updatedWinner);
        AssertTwinCityTeleportHistory(runnerUp, updatedRunnerUp);
        AssertTwinCityEndTransition(winnerSent, updatedWinner);
        AssertTwinCityEndTransition(runnerUpSent, updatedRunnerUp);
    }

    [Fact]
    public async Task RuntimeEndTeleportCleansMovementStatusesBeforeLeavingMapWithoutReaddingThem()
    {
        const int SupermanStatusFlag = 0x12;
        const int FocusedStatusFlag = 0x15;
        const int CastPrayStatusFlag = 0x1E;
        const int PrayingStatusFlag = 0x1F;
        const int AzureShieldStatusFlag = 0x5D;
        const int MagicDefenderStatusFlag = 0x80;
        const int DragonSwingStatusFlag = 0xA0;
        const string CastPrayRemoveOnDeadAttribute = "LegacyCastPrayRemoveOnDead";
        const string DragonSwingExpiresAtUtcAttribute = "LegacyDragonSwingExpiresAtUtc";

        using var log = new StringWriter(CultureInfo.InvariantCulture);
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log: log);
        var sessions = GetSessions(handler);
        var dragonWarStates = GetDragonWarStates(handler);
        Assert.NotNull(dragonWarStates.Open(OpenedAt));

        var preservedMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WorldPacketHandlers.RuntimeLegacyIntensifyStampAttribute] = "2026-06-28T18:59:55.0000000",
            [WorldPacketHandlers.RuntimeLegacyIntensifyStampUtcAttribute] = "2026-06-28T18:59:55.0000000+00:00",
            [WorldPacketHandlers.RuntimeLegacyIntensifyDamageAttribute] = "54321",
            [WorldPacketHandlers.RuntimeLegacyFocusClientSpellIdAttribute] = "9000",
            [WorldPacketHandlers.RuntimeLegacyFocusClientSpellLevelAttribute] = "4",
            [WorldPacketHandlers.RuntimeLegacyFocusClientSpellUseSpellSoulAttribute] = "1",
            [WorldPacketHandlers.RuntimeLegacyFocusedExpiresAtUtcAttribute] = "2026-06-28T19:00:55.0000000+00:00",
            ["LegacyFocusedRemoveOnDead"] = "true",
            ["AzureShieldDefence"] = "42000",
            ["AzureShieldLevel"] = "3",
            ["UnrelatedMetadata"] = "keep-me",
        };
        var participantAttributes = new Dictionary<string, string>(preservedMetadata, StringComparer.OrdinalIgnoreCase)
        {
            ["DragonWarScore"] = "90",
            ["StatusFlags"] = BuildStatusFlags(
                SupermanStatusFlag,
                FocusedStatusFlag,
                CastPrayStatusFlag,
                PrayingStatusFlag,
                AzureShieldStatusFlag,
                MagicDefenderStatusFlag,
                DragonSwingStatusFlag),
            [WorldPacketHandlers.RuntimeLegacyInUseIntensifyAttribute] = "true",
            [WorldPacketHandlers.RuntimeLegacyCastPrayExpiresAtUtcAttribute] =
                "2026-07-28T19:00:00.0000000+00:00",
            [CastPrayRemoveOnDeadAttribute] = "true",
            [DragonSwingExpiresAtUtcAttribute] = "2026-07-28T19:00:00.0000000+00:00",
        };

        var ownerSent = new List<byte[]>();
        var observerSent = new List<byte[]>();
        var destinationObserverSent = new List<byte[]>();
        var ownerConnection = CreateSendingConnection(ownerSent, 9962);
        var observerConnection = CreateSendingConnection(observerSent, 9963);
        var destinationObserverConnection = CreateSendingConnection(destinationObserverSent, 9964);
        var participant = JoinDragonWar(
            dragonWarStates,
            CreateSession(1_293_604, "StatusDragon", attributes: participantAttributes));
        var activeParticipantAttributes = new Dictionary<string, string>(
            participant.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["DragonWarScore"] = "90",
            ["StatusFlags"] = BuildStatusFlags(
                SupermanStatusFlag,
                FocusedStatusFlag,
                CastPrayStatusFlag,
                PrayingStatusFlag,
                AzureShieldStatusFlag,
                MagicDefenderStatusFlag,
                DragonSwingStatusFlag),
            [DragonSwingExpiresAtUtcAttribute] = "2026-07-28T19:00:00.0000000+00:00",
        };
        participant = participant with
        {
            Character = participant.Character with { Attributes = activeParticipantAttributes },
        };
        var observer = CreateSession(1_293_605, "OldMapObserver");
        observer = observer with
        {
            Character = observer.Character with
            {
                MapId = LegacyDragonWarRuntimeStateStore.DragonWarMapId,
                X = participant.Character.X + 1,
                Y = participant.Character.Y,
                Attributes = new Dictionary<string, string>(observer.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                {
                    ["DynamicID"] = participant.Character.Attributes["DynamicID"],
                },
            },
        };
        var destinationObserver = CreateSession(1_293_606, "TwinCityObserver");
        destinationObserver = destinationObserver with
        {
            Character = destinationObserver.Character with
            {
                X = 411,
                Y = 355,
            },
        };
        sessions.Set(ownerConnection, participant);
        sessions.Set(observerConnection, observer);
        sessions.Set(destinationObserverConnection, destinationObserver);
        dragonWarStates.SetProcess(LegacyDragonWarProcess.Alive);

        await InvokeDragonWarRuntimeTickAsync(handler, OpenedAt.AddMinutes(5).AddSeconds(1));

        Assert.True(sessions.TryGet(ownerConnection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1002, updated.Character.MapId);
        Assert.Equal(410, updated.Character.X);
        Assert.Equal(355, updated.Character.Y);
        Assert.Equal(LegacyDragonWarRuntimeStateStore.DragonWarMapId, updated.Character.PreviousMapId);
        AssertTwinCityTeleportHistory(participant, updated);
        Assert.Equal("false", updated.Character.Attributes[WorldPacketHandlers.RuntimeLegacyInUseIntensifyAttribute]);
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(updated.Character.Attributes, PrayingStatusFlag));
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(updated.Character.Attributes, CastPrayStatusFlag));
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(updated.Character.Attributes, MagicDefenderStatusFlag));
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(updated.Character.Attributes, FocusedStatusFlag));
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(updated.Character.Attributes, AzureShieldStatusFlag));
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(updated.Character.Attributes, SupermanStatusFlag));
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(updated.Character.Attributes, DragonSwingStatusFlag));
        Assert.False(updated.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyCastPrayExpiresAtUtcAttribute));
        Assert.False(updated.Character.Attributes.ContainsKey(CastPrayRemoveOnDeadAttribute));
        Assert.False(updated.Character.Attributes.ContainsKey(DragonSwingExpiresAtUtcAttribute));
        foreach (var attribute in preservedMetadata)
        {
            Assert.Equal(attribute.Value, updated.Character.Attributes[attribute.Key]);
        }

        var afterPrayingRemoval = BuildStatusFlagVector(
            SupermanStatusFlag,
            FocusedStatusFlag,
            CastPrayStatusFlag,
            AzureShieldStatusFlag,
            MagicDefenderStatusFlag,
            DragonSwingStatusFlag);
        var afterCastPrayRemoval = BuildStatusFlagVector(
            SupermanStatusFlag,
            FocusedStatusFlag,
            AzureShieldStatusFlag,
            MagicDefenderStatusFlag,
            DragonSwingStatusFlag);
        var afterMagicDefenderRemoval = BuildStatusFlagVector(
            SupermanStatusFlag,
            FocusedStatusFlag,
            AzureShieldStatusFlag,
            DragonSwingStatusFlag);
        var afterDragonSwingRemoval = BuildStatusFlagVector(
            SupermanStatusFlag,
            FocusedStatusFlag,
            AzureShieldStatusFlag);

        var participantUid = (uint)participant.Character.Uid;
        var ownerAfterPrayingRemovalIndex = FindStatusFlagVectorIndex(
            ownerSent,
            participantUid,
            afterPrayingRemoval);
        var ownerAfterCastPrayRemovalIndex = FindStatusFlagVectorIndex(
            ownerSent,
            participantUid,
            afterCastPrayRemoval);
        var ownerAfterMagicDefenderRemovalIndex = FindStatusFlagVectorIndex(
            ownerSent,
            participantUid,
            afterMagicDefenderRemoval);
        var ownerMagicDefenderResetIndex = FindMagicDefenderResetIndex(
            ownerSent,
            participantUid,
            MagicDefenderStatusFlag);
        var ownerTransition = AssertTwinCityEndTransition(ownerSent, updated);
        var ownerAfterDragonSwingRemovalIndex = FindStatusFlagVectorIndex(
            ownerSent,
            participantUid,
            afterDragonSwingRemoval);
        var ownerDestinationObserverSpawnIndex = FindPlayerSpawnIndex(
            ownerSent,
            (uint)destinationObserver.Character.Uid);
        var ownerConquerPointsRewardIndex = FindConquerPointsRewardIndex(ownerSent, updated);

        AssertStatusFlagVector(
            ownerSent[ownerAfterPrayingRemovalIndex],
            participantUid,
            afterPrayingRemoval);
        AssertStatusFlagVector(
            ownerSent[ownerAfterCastPrayRemovalIndex],
            participantUid,
            afterCastPrayRemoval);
        AssertStatusFlagVector(
            ownerSent[ownerAfterMagicDefenderRemovalIndex],
            participantUid,
            afterMagicDefenderRemoval);
        AssertMagicDefenderReset(
            ownerSent[ownerMagicDefenderResetIndex],
            participantUid,
            MagicDefenderStatusFlag);
        AssertStatusFlagVector(
            ownerSent[ownerAfterDragonSwingRemovalIndex],
            participantUid,
            afterDragonSwingRemoval);
        AssertConquerPointsReward(ownerSent[ownerConquerPointsRewardIndex], updated);
        Assert.Equal(ownerAfterPrayingRemovalIndex + 1, ownerAfterCastPrayRemovalIndex);
        Assert.Equal(ownerAfterCastPrayRemovalIndex + 1, ownerAfterMagicDefenderRemovalIndex);
        Assert.Equal(ownerAfterMagicDefenderRemovalIndex + 1, ownerMagicDefenderResetIndex);
        Assert.Equal(ownerMagicDefenderResetIndex + 1, ownerTransition.TeleportIndex);
        Assert.True(ownerDestinationObserverSpawnIndex > ownerTransition.MapStatusIndex);
        Assert.True(ownerDestinationObserverSpawnIndex < ownerAfterDragonSwingRemovalIndex);
        Assert.True(ownerAfterDragonSwingRemovalIndex > ownerTransition.MapStatusIndex);
        Assert.Equal(ownerAfterDragonSwingRemovalIndex + 1, ownerConquerPointsRewardIndex);
        Assert.Equal(
            4,
            ownerSent.Count(frame => IsStatusFlagVectorForUid(frame, participantUid)));

        var observerAfterPrayingRemovalIndex = FindStatusFlagVectorIndex(
            observerSent,
            participantUid,
            afterPrayingRemoval);
        var observerAfterCastPrayRemovalIndex = FindStatusFlagVectorIndex(
            observerSent,
            participantUid,
            afterCastPrayRemoval);
        var observerAfterMagicDefenderRemovalIndex = FindStatusFlagVectorIndex(
            observerSent,
            participantUid,
            afterMagicDefenderRemoval);
        var observerMagicDefenderResetIndex = FindMagicDefenderResetIndex(
            observerSent,
            participantUid,
            MagicDefenderStatusFlag);
        var observerRemoveIndex = FindRemoveEntityIndex(observerSent, participantUid);

        AssertStatusFlagVector(
            observerSent[observerAfterPrayingRemovalIndex],
            participantUid,
            afterPrayingRemoval);
        AssertStatusFlagVector(
            observerSent[observerAfterCastPrayRemovalIndex],
            participantUid,
            afterCastPrayRemoval);
        AssertStatusFlagVector(
            observerSent[observerAfterMagicDefenderRemovalIndex],
            participantUid,
            afterMagicDefenderRemoval);
        AssertMagicDefenderReset(
            observerSent[observerMagicDefenderResetIndex],
            participantUid,
            MagicDefenderStatusFlag);
        Assert.True(LegacyRemoveEntityPacket.TryParse(ReadFrame(observerSent[observerRemoveIndex]), out var observerRemove));
        Assert.Equal(participantUid, observerRemove.Uid);
        Assert.Equal(observerAfterPrayingRemovalIndex + 1, observerAfterCastPrayRemovalIndex);
        Assert.Equal(observerAfterCastPrayRemovalIndex + 1, observerAfterMagicDefenderRemovalIndex);
        Assert.Equal(observerAfterMagicDefenderRemovalIndex + 1, observerMagicDefenderResetIndex);
        Assert.Equal(observerMagicDefenderResetIndex + 1, observerRemoveIndex);
        Assert.Equal(
            3,
            observerSent.Count(frame => IsStatusFlagVectorForUid(frame, participantUid)));

        var destinationObserverStatusIndex = FindStatusFlagVectorIndex(
            destinationObserverSent,
            participantUid,
            afterDragonSwingRemoval);
        var destinationObserverParticipantSpawnIndex = FindPlayerSpawnIndex(
            destinationObserverSent,
            participantUid);
        AssertStatusFlagVector(
            destinationObserverSent[destinationObserverStatusIndex],
            participantUid,
            afterDragonSwingRemoval);
        Assert.True(destinationObserverParticipantSpawnIndex < destinationObserverStatusIndex);
        Assert.Equal(
            1,
            destinationObserverSent.Count(
                frame => IsStatusFlagVectorForUid(frame, participantUid)));
        Assert.DoesNotContain(
            destinationObserverSent,
            frame => LegacyRemoveEntityPacket.TryParse(ReadFrame(frame), out var remove) &&
                remove.Uid == participantUid);

        Assert.DoesNotContain(
            observerSent.Skip(observerRemoveIndex + 1),
            frame => IsStatusFlagVectorForUid(frame, participantUid));
        GC.KeepAlive(observerConnection);
        GC.KeepAlive(destinationObserverConnection);
    }

    [Fact]
    public async Task RuntimeEndRewardPersistsCharacterScalarsItemsAndLogsSourceMethod()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "WolfConquer-DragonWar-EndReward-" + Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(root, "Users");
        var itemsRoot = Path.Combine(root, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);

        try
        {
            using var log = new StringWriter(CultureInfo.InvariantCulture);
            var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), log: log);
            var sessions = GetSessions(handler);
            var dragonWarStates = GetDragonWarStates(handler);
            Assert.NotNull(dragonWarStates.Open(OpenedAt));

            var sent = new List<byte[]>();
            var connection = CreateSendingConnection(sent, 9961);
            var policy = LegacyDragonWarRuntimeStateStore.EndRewardEconomyPolicy;
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DragonWarScore"] = "120",
                ["ClassExperience"] = "10",
            };
            var initialSession = CreateSession(
                1_293_603,
                "PersistentDragon",
                attributes: attributes);
            var initial = initialSession with
            {
                UsersRoot = usersRoot,
                Character = initialSession.Character with
                {
                    ConquerPoints = 25,
                },
            };
            WriteCharacterSnapshotForSession(usersRoot, initial);
            var joined = JoinDragonWar(dragonWarStates, initial);
            var joinedAttributes = new Dictionary<string, string>(
                joined.Character.Attributes,
                StringComparer.OrdinalIgnoreCase);
            foreach (var attribute in initial.Character.Attributes)
            {
                joinedAttributes[attribute.Key] = attribute.Value;
            }

            joined = joined with
            {
                UsersRoot = usersRoot,
                Character = joined.Character with
                {
                    ConquerPoints = initial.Character.ConquerPoints,
                    Attributes = joinedAttributes,
                },
            };
            sessions.Set(connection, joined);
            dragonWarStates.SetProcess(LegacyDragonWarProcess.Alive);

            await InvokeDragonWarRuntimeTickAsync(handler, OpenedAt.AddMinutes(5).AddSeconds(1));

            Assert.True(sessions.TryGet(connection, out var updated));
            Assert.NotNull(updated);
            Assert.Equal(25 + policy.TunedWinnerConquerPointsReward, updated.Character.ConquerPoints);
            Assert.Equal("510", updated.Character.Attributes["ClassExperience"]);

            var persistedCharacter = CharacterSnapshotParser.ParseText(
                File.ReadAllText(Path.Combine(usersRoot, "1293603.ini")));
            Assert.Equal(25 + policy.TunedWinnerConquerPointsReward, persistedCharacter.ConquerPoints);
            Assert.Equal("510", persistedCharacter.Attributes["ClassExperience"]);

            var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_293_603);
            var rewardItem = Assert.Single(persistedItems.InventoryItems);
            Assert.Equal(policy.TunedWinnerItemId, rewardItem.ItemId);
            Assert.Equal(policy.TunedWinnerItemStackSize, rewardItem.StackSize);

            var output = log.ToString();
            Assert.Contains("WorldServer applied legacy DragonWar reward", output);
            Assert.Contains($"sourceMethod={policy.SourceRewardOwnerName}", output);
            Assert.Contains($"conquerPointsDelta={policy.TunedWinnerConquerPointsReward}", output);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task InvokeDragonWarRuntimeTickAsync(WorldServerFrameHandler handler, DateTimeOffset now)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "SendDueLegacyDragonWarRuntimeUpdatesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method.Invoke(handler, [now, CancellationToken.None]);
        Assert.NotNull(result);
        await (ValueTask)result;
    }

    private static WorldSession JoinDragonWar(
        LegacyDragonWarRuntimeStateStore dragonWarStates,
        WorldSession session)
    {
        Assert.True(dragonWarStates.TryJoin(session, out var joined));
        return joined;
    }

    private static (int TeleportIndex, int MapStatusIndex) AssertMap700Transition(
        IReadOnlyList<byte[]> sent,
        WorldSession session)
    {
        var transition = ExtractOrderedMapTransition(sent);
        Assert.Equal(
            [MsgActionType.Teleport, MsgActionType.StopVending, MsgActionType.LegacyColor],
            transition.Actions.Select(action => action.Type).ToArray());
        AssertTeleportAction(transition.Actions[0], session, 700, (uint)session.Character.X, (uint)session.Character.Y);
        AssertTransitionAction(transition.Actions[1], session, MsgActionType.StopVending, 700);
        AssertColorAction(transition.Actions[2], session);
        Assert.Equal(LegacyMapStatusPacket.FromMap(700, mapsById: null), transition.MapStatus);
        return (transition.StartIndex, transition.StartIndex + 3);
    }

    private static (int TeleportIndex, int MapStatusIndex) AssertTwinCityEndTransition(
        IReadOnlyList<byte[]> sent,
        WorldSession session)
    {
        var transition = ExtractOrderedTwinCityEndTransition(sent, session);
        AssertTwinCityEndTransitionFrames(transition.Frames, session);
        Assert.DoesNotContain(sent, frame => IsAction(frame, MsgActionType.LegacyMerchantStartup));
        return (transition.StartIndex, transition.StartIndex + 3);
    }

    private static void AssertTwinCityEndTransitionFrames(
        IReadOnlyList<byte[]> transition,
        WorldSession session)
    {
        Assert.Equal(4, transition.Count);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(transition[0]), out var teleport));
        AssertTeleportAction(teleport, session, 1002, 410, 355);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(transition[1]), out var stopVending));
        AssertTransitionAction(stopVending, session, MsgActionType.StopVending, 1002);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(transition[2]), out var color));
        AssertColorAction(color, session);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(transition[3]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(1002, mapsById: null), mapStatus);
    }

    private static (int StartIndex, IReadOnlyList<byte[]> Frames) ExtractOrderedTwinCityEndTransition(
        IReadOnlyList<byte[]> sent,
        WorldSession session)
    {
        for (var index = 0; index + 3 < sent.Count; index++)
        {
            if (!MsgActionPacket.TryParse(ReadFrame(sent[index]), out var teleport) ||
                teleport.Type != MsgActionType.Teleport ||
                teleport.ObjId != (uint)session.Character.Uid ||
                !MsgActionPacket.TryParse(ReadFrame(sent[index + 1]), out var stopVending) ||
                stopVending.Type != MsgActionType.StopVending ||
                stopVending.ObjId != (uint)session.Character.Uid ||
                !MsgActionPacket.TryParse(ReadFrame(sent[index + 2]), out var color) ||
                color.Type != MsgActionType.LegacyColor ||
                color.ObjId != (uint)session.Character.Uid ||
                !LegacyMapStatusPacket.TryParse(ReadFrame(sent[index + 3]), out _))
            {
                continue;
            }

            return (index, sent.Skip(index).Take(4).ToArray());
        }

        Assert.Fail("Expected ordered source-map-aware DragonWar end transition frames were not sent.");
        return default;
    }

    private static (
        int StartIndex,
        IReadOnlyList<MsgActionPacket> Actions,
        LegacyMapStatusPacket MapStatus) ExtractOrderedMapTransition(
        IReadOnlyList<byte[]> sent)
    {
        for (var index = 0; index + 3 < sent.Count; index++)
        {
            if (!MsgActionPacket.TryParse(ReadFrame(sent[index]), out var teleport) ||
                teleport.Type != MsgActionType.Teleport ||
                !MsgActionPacket.TryParse(ReadFrame(sent[index + 1]), out var stopVending) ||
                stopVending.Type != MsgActionType.StopVending ||
                !MsgActionPacket.TryParse(ReadFrame(sent[index + 2]), out var color) ||
                color.Type != MsgActionType.LegacyColor ||
                !LegacyMapStatusPacket.TryParse(ReadFrame(sent[index + 3]), out var mapStatus))
            {
                continue;
            }

            return (index, [teleport, stopVending, color], mapStatus);
        }

        Assert.Fail("Expected ordered map-700 DragonWar transition frames were not sent.");
        return default;
    }

    private static void AssertTeleportAction(
        MsgActionPacket action,
        WorldSession session,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        Assert.Equal((uint)session.Character.Uid, action.ObjId);
        Assert.Equal(MsgActionType.Teleport, action.Type);
        Assert.Equal(expectedMapId, action.DwParam);
        Assert.Equal((long)expectedMapId, action.DwParam3);
        Assert.Equal(expectedX, action.PositionX);
        Assert.Equal(expectedY, action.PositionY);
    }

    private static void AssertTransitionAction(
        MsgActionPacket action,
        WorldSession session,
        MsgActionType expectedType,
        uint expectedDwParam)
    {
        Assert.Equal((uint)session.Character.Uid, action.ObjId);
        Assert.Equal(expectedType, action.Type);
        Assert.Equal(expectedDwParam, action.DwParam);
        Assert.Equal((long)session.Character.MapId, action.DwParam3);
        Assert.Equal((uint)session.Character.X, action.PositionX);
        Assert.Equal((uint)session.Character.Y, action.PositionY);
    }

    private static void AssertColorAction(MsgActionPacket action, WorldSession session)
    {
        Assert.Equal((uint)session.Character.Uid, action.ObjId);
        Assert.Equal(MsgActionType.LegacyColor, action.Type);
        Assert.Equal(0U, action.DwParam);
        Assert.Equal((uint)session.Character.X, action.PositionX);
        Assert.Equal((uint)session.Character.Y, action.PositionY);
    }

    private static void AssertSameMapTeleportHistory(WorldSession source, WorldSession destination)
    {
        Assert.Equal(source.Character.MapId, destination.Character.MapId);
        Assert.Equal(source.Character.PreviousMapId, destination.Character.PreviousMapId);
        Assert.Equal(source.Character.PreviousMapX, destination.Character.PreviousMapX);
        Assert.Equal(source.Character.PreviousMapY, destination.Character.PreviousMapY);
        Assert.True(source.Character.Attributes.TryGetValue("DynamicID", out var sourceDynamicId));
        Assert.True(destination.Character.Attributes.TryGetValue("DynamicID", out var destinationDynamicId));
        Assert.Equal(sourceDynamicId, destinationDynamicId);

        var sourceHasPreviousDynamicId = source.Character.Attributes.TryGetValue(
            "PDinamycID",
            out var sourcePreviousDynamicId);
        var destinationHasPreviousDynamicId = destination.Character.Attributes.TryGetValue(
            "PDinamycID",
            out var destinationPreviousDynamicId);
        Assert.Equal(sourceHasPreviousDynamicId, destinationHasPreviousDynamicId);
        if (sourceHasPreviousDynamicId)
        {
            Assert.Equal(sourcePreviousDynamicId, destinationPreviousDynamicId);
        }
    }

    private static void AssertTwinCityTeleportHistory(WorldSession source, WorldSession destination)
    {
        Assert.Equal(1002, destination.Character.MapId);
        Assert.Equal(source.Character.MapId, destination.Character.PreviousMapId);
        Assert.Equal(source.Character.X, destination.Character.PreviousMapX);
        Assert.Equal(source.Character.Y, destination.Character.PreviousMapY);
        Assert.True(source.Character.Attributes.TryGetValue("DynamicID", out var sourceDynamicId));
        Assert.NotEqual("0", sourceDynamicId);
        Assert.Equal("0", destination.Character.Attributes["DynamicID"]);
        Assert.Equal("0", destination.Character.Attributes["PDinamycID"]);
    }

    private static WorldSessionStore GetSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static LegacyDragonWarRuntimeStateStore GetDragonWarStates(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_dragonWarStates",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<LegacyDragonWarRuntimeStateStore>(field.GetValue(handler));
    }

    private static Dictionary<string, string> CreateDeadDragonWarAttributes(
        IReadOnlyDictionary<string, string> attributes,
        DateTimeOffset deadAt)
    {
        return new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(LegacyPlayerDeathState.DeadStatusFlag, LegacyPlayerDeathState.GhostStatusFlag),
            [LegacyPlayerDeathState.DeadStampAttribute] = deadAt.ToString("O", CultureInfo.InvariantCulture),
            [LegacyPlayerDeathState.GhostStampAttribute] = deadAt.AddSeconds(1).ToString("O", CultureInfo.InvariantCulture),
            [LegacyPlayerDeathState.DeadXAttribute] = "1",
            [LegacyPlayerDeathState.DeadYAttribute] = "1",
        };
    }

    private static bool IsAction(byte[] bytes, MsgActionType type)
    {
        return MsgActionPacket.TryParse(ReadFrame(bytes), out var action) && action.Type == type;
    }

    private static bool IsLegacyStatusFrame(byte[] bytes)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag);
    }

    private static bool IsLegacyMapStatusFrame(byte[] bytes)
    {
        return LegacyMapStatusPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static int FindStatusFlagVectorIndex(
        IReadOnlyList<byte[]> sent,
        uint uid,
        IReadOnlyList<ulong> expected)
    {
        var matches = new List<int>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (IsStatusFlagVector(sent[index], uid, expected))
            {
                matches.Add(index);
            }
        }

        return Assert.Single(matches);
    }

    private static int FindMagicDefenderResetIndex(
        IReadOnlyList<byte[]> sent,
        uint uid,
        int magicDefenderStatusFlag)
    {
        var matches = new List<int>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (IsMagicDefenderReset(sent[index], uid, magicDefenderStatusFlag))
            {
                matches.Add(index);
            }
        }

        return Assert.Single(matches);
    }

    private static int FindConquerPointsRewardIndex(IReadOnlyList<byte[]> sent, WorldSession session)
    {
        var matches = new List<int>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (IsConquerPointsReward(sent[index], session))
            {
                matches.Add(index);
            }
        }

        return Assert.Single(matches);
    }

    private static int FindPlayerSpawnIndex(IReadOnlyList<byte[]> sent, uint uid)
    {
        const int UidField = 2;

        var matches = new List<int>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyPlayerSpawnPacket.TryParse(ReadFrame(sent[index]), out var spawn) &&
                spawn.GetNumericField(UidField) == uid)
            {
                matches.Add(index);
            }
        }

        return Assert.Single(matches);
    }

    private static int FindRemoveEntityIndex(IReadOnlyList<byte[]> sent, uint uid)
    {
        var matches = new List<int>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyRemoveEntityPacket.TryParse(ReadFrame(sent[index]), out var remove) &&
                remove.Uid == uid)
            {
                matches.Add(index);
            }
        }

        return Assert.Single(matches);
    }

    private static bool IsStatusFlagVector(
        byte[] bytes,
        uint uid,
        IReadOnlyList<ulong> expected)
    {
        if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) ||
            update.Uid != uid ||
            update.Updates.Count != 1)
        {
            return false;
        }

        var entry = update.Updates[0];
        return entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is { } values &&
            values.SequenceEqual(expected);
    }

    private static bool IsMagicDefenderReset(byte[] bytes, uint uid, int magicDefenderStatusFlag)
    {
        if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) ||
            update.Uid != uid ||
            update.Updates.Count != 1)
        {
            return false;
        }

        var entry = update.Updates[0];
        return entry.Type == LegacyMsgUpdateDataType.AzureShield &&
            entry.Flag == (uint)magicDefenderStatusFlag &&
            entry.Value == 0 &&
            entry.Time == 0 &&
            entry.Damage == 0 &&
            entry.SpellLevel == 0;
    }

    private static bool IsConquerPointsReward(byte[] bytes, WorldSession session)
    {
        if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) ||
            update.Uid != (uint)session.Character.Uid ||
            update.Updates.Count != 1)
        {
            return false;
        }

        var entry = update.Updates[0];
        return entry.Type == LegacyMsgUpdateDataType.ConquerPoints &&
            entry.Value == (ulong)session.Character.ConquerPoints;
    }

    private static void AssertStatusFlagVector(byte[] bytes, uint uid, IReadOnlyList<ulong> expected)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        Assert.Equal(uid, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, entry.Type);
        Assert.NotNull(entry.Values);
        Assert.Equal(LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength, entry.Values.Count);
        Assert.Equal(expected, entry.Values);
    }

    private static void AssertMagicDefenderReset(byte[] bytes, uint uid, int magicDefenderStatusFlag)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        Assert.Equal(uid, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.AzureShield, entry.Type);
        Assert.Equal((uint)magicDefenderStatusFlag, entry.Flag);
        Assert.Equal(0UL, entry.Value);
        Assert.Equal(0U, entry.Time);
        Assert.Equal(0U, entry.Damage);
        Assert.Equal(0U, entry.SpellLevel);
    }

    private static void AssertConquerPointsReward(byte[] bytes, WorldSession session)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        Assert.Equal((uint)session.Character.Uid, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, entry.Type);
        Assert.Equal((ulong)session.Character.ConquerPoints, entry.Value);
    }

    private static bool IsStatusFlagVectorForUid(byte[] bytes, uint uid)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Uid == uid &&
            update.Updates.Count == 1 &&
            update.Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag &&
            update.Updates[0].Values?.Count == LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength;
    }

    private static ulong[] BuildStatusFlagVector(params int[] flags)
    {
        var words = new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return words;
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[10];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words.Select(word => word.ToString(CultureInfo.InvariantCulture))) + "/";
    }

    private static WorldSession CreateSession(
        long uid,
        string name,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new WorldSession(
            AccountId: uid,
            Username: name,
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
                Strength: 0,
                Agility: 0,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 61_258,
                MinMana: 5_000,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 7,
                SourceFile: "fixture",
                Attributes: attributes is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static void WriteCharacterSnapshotForSession(string usersRoot, WorldSession session)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, session.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".ini"),
            $$"""
            [Character]
            UID={{session.Character.Uid}}
            Body={{session.Character.Body}}
            Face={{session.Character.Face}}
            Name={{session.Character.Name}}
            Class={{session.Character.Class}}
            Map={{session.Character.MapId}}
            X={{session.Character.X}}
            Y={{session.Character.Y}}
            FirstClass={{session.Character.FirstClass}}
            SecoundeClass={{session.Character.SecondClass}}
            Reborn={{session.Character.Reborn}}
            Level={{session.Character.Level}}
            Haire={{session.Character.Hair}}
            Experience={{session.Character.Experience}}
            PMap={{session.Character.PreviousMapId}}
            PMapX={{session.Character.PreviousMapX}}
            PMapY={{session.Character.PreviousMapY}}
            Agility={{session.Character.Agility}}
            Strength={{session.Character.Strength}}
            Vitaliti={{session.Character.Vitality}}
            Spirit={{session.Character.Spirit}}
            Atributes={{session.Character.AttributePoints}}
            MinHitPoints={{session.Character.MinHitPoints}}
            MinMana={{session.Character.MinMana}}
            MaxMana={{session.Character.MaxMana}}
            ConquerPoints={{session.Character.ConquerPoints}}
            BankCps={{session.Character.BankConquerPoints}}
            BoundConquerPoints={{session.Character.BoundConquerPoints}}
            Money={{session.Character.Money}}
            PkPoints={{session.Character.PkPoints}}
            Online={{session.Character.Online}}
            ExpBallUsed={{session.Character.ExpBallUsed}}
            ClassExperience={{session.Character.Attributes["ClassExperience"]}}
            """);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent, int port)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, port), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
