using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacyQuestList)]
    private static async ValueTask HandleLegacyQuestListAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyQuestListPacket.TryParseQuery(frame, out var query))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyQuestList packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyQuestList " +
                    $"{FormatSession(context)} mode={query.Mode} questId={query.QuestId}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (query.Mode == LegacyQuestListMode.Review)
        {
            if (query.Count >= LegacyQuestListPacket.MaxReviewItemsPerFrame)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer ignored LegacyQuestList " +
                        $"{FormatSession(context)} mode={query.Mode} count={query.Count}; legacy review guard requires count<80.")
                    .ConfigureAwait(false);
                return;
            }

            var reviewQuests = LegacyQuestListPacket.EnsureLegacyReviewStarterQuest(
                session.Character.PlayerQuests,
                session.Character.Class,
                out var starterQuestId,
                out var addedStarterQuest);
            var reviewSession = ReferenceEquals(reviewQuests, session.Character.PlayerQuests)
                ? session
                : WithPlayerQuests(session, reviewQuests);
            if (addedStarterQuest)
            {
                context.Sessions?.Set(context.Connection, reviewSession);
                await PersistLegacyPlayerQuestsAsync(
                        context,
                        reviewSession,
                        "LegacyQuestList",
                        "review-starter",
                        starterQuestId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var useQuestCatalog = context.WorldData is { QuestMissions.Count: > 0 };
            var responses = useQuestCatalog
                ? LegacyQuestListPacket.CreateReviewFramesFromQuestCatalog(
                    reviewSession.Character.PlayerQuests,
                    context.WorldData!.QuestMissions)
                : LegacyQuestListPacket.CreateReviewFramesFromPlayerQuests(reviewSession.Character.PlayerQuests);
            foreach (var response in responses)
            {
                await context.Connection.SendAsync(response, cancellationToken)
                    .ConfigureAwait(false);
            }

            var responseCount = useQuestCatalog
                ? CountDistinctLegacyQuestMissions(context.WorldData!.QuestMissions)
                : reviewSession.Character.PlayerQuests.Quests.Count;
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyQuestList " +
                    $"{FormatSession(context with { Session = reviewSession })} mode={query.Mode} count={query.Count} " +
                    $"responseCount={responseCount} responseFrames={responses.Count}. " +
                    $"source={(useQuestCatalog ? "quest-catalog" : "player-snapshot")} " +
                    $"starterQuestId={starterQuestId} starterAdded={addedStarterQuest}.")
                .ConfigureAwait(false);
            return;
        }

        if (query.Mode == LegacyQuestListMode.AcceptQuest)
        {
            await HandleLegacyQuestListAcceptAsync(context, session, query, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (query.Mode == LegacyQuestListMode.QuitQuest)
        {
            await HandleLegacyQuestListQuitAsync(context, session, query, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (query.Mode == LegacyQuestListMode.FinishQuest)
        {
            await HandleLegacyQuestListFinishAsync(context, session, query, cancellationToken).ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer ignored LegacyQuestList " +
                $"{FormatSession(context)} mode={query.Mode} questId={query.QuestId}; mode is not defined by source QuestMode.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyQuestListAcceptAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyQuestListPacket query,
        CancellationToken cancellationToken)
    {
        if (session.Character.PlayerQuests.AcceptedQuests.Count >= 5)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyQuestList accept " +
                    $"{FormatSession(context)} questId={query.QuestId}; accepted quest limit reached.")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.PlayerQuests.ContainsQuest(query.QuestId))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyQuestList accept " +
                    $"{FormatSession(context)} questId={query.QuestId}; quest already exists in runtime snapshot.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryGetLegacyQuestMission(context.WorldData, query.QuestId, out var mission))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyQuestList accept " +
                    $"{FormatSession(context)} questId={query.QuestId}; source QuestInfo.AllQuests entry is not loaded.")
                .ConfigureAwait(false);
            return;
        }

        var questId = checked((uint)mission.MissionId);
        var time = LegacyKingdomMissionIds.Contains(questId) ? 0 : query.Time;
        var entry = new PlayerQuestSnapshotEntry(
            questId,
            PlayerQuestStatus.Accepted,
            time,
            new uint[GetLegacyQuestIntentionCount(mission)]);
        var updatedSession = WithPlayerQuests(
            session,
            session.Character.PlayerQuests.WithQuest(entry));
        context.Sessions?.Set(context.Connection, updatedSession);
        await PersistLegacyPlayerQuestsAsync(
                context,
                updatedSession,
                "LegacyQuestList",
                "accept",
                questId,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyQuestListPacket.CreateSingleQuestFrame(
                    LegacyQuestListMode.AcceptQuest,
                    ToLegacyQuestListItem(entry)),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyQuestList accept " +
                $"{FormatSession(context with { Session = updatedSession })} questId={questId} " +
                $"time={time} intentions={entry.Intentions.Count}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyQuestListQuitAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyQuestListPacket query,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(query.QuestId, out var quest))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyQuestList quit " +
                    $"{FormatSession(context)} questId={query.QuestId}; quest is not in runtime snapshot.")
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(
                LegacyQuestListPacket.CreateSingleQuestFrame(
                    LegacyQuestListMode.QuitQuest,
                    ToLegacyQuestListItem(quest)),
                cancellationToken)
            .ConfigureAwait(false);

        var available = new LegacyQuestListItem(query.QuestId, LegacyQuestStatus.Available, Time: 0);
        await context.Connection.SendAsync(
                LegacyQuestListPacket.CreateSingleQuestFrame(LegacyQuestListMode.Review, available),
                cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = WithPlayerQuests(
            session,
            session.Character.PlayerQuests.WithoutQuest(query.QuestId));
        context.Sessions?.Set(context.Connection, updatedSession);
        await PersistLegacyPlayerQuestsAsync(
                context,
                updatedSession,
                "LegacyQuestList",
                "quit",
                query.QuestId,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyQuestList quit " +
                $"{FormatSession(context with { Session = updatedSession })} questId={query.QuestId} " +
                $"removedStatus={quest.Status} remainingQuests={updatedSession.Character.PlayerQuests.Quests.Count} " +
                "responseFrames=2.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyQuestListFinishAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyQuestListPacket query,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(query.QuestId, out var quest))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyQuestList finish " +
                    $"{FormatSession(context)} questId={query.QuestId}; quest is not in runtime snapshot.")
                .ConfigureAwait(false);
            return;
        }

        if (quest.Status == PlayerQuestStatus.Finished)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyQuestList finish " +
                    $"{FormatSession(context)} questId={query.QuestId}; quest is already finished.")
                .ConfigureAwait(false);
            return;
        }

        var finished = quest with { Status = PlayerQuestStatus.Finished };
        var updatedSession = WithPlayerQuests(
            session,
            session.Character.PlayerQuests.WithQuest(finished));
        context.Sessions?.Set(context.Connection, updatedSession);
        await PersistLegacyPlayerQuestsAsync(
                context,
                updatedSession,
                "LegacyQuestList",
                "finish",
                query.QuestId,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyQuestListPacket.CreateSingleQuestFrame(
                    LegacyQuestListMode.FinishQuest,
                    ToLegacyQuestListItem(finished)),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyQuestList finish " +
                $"{FormatSession(context with { Session = updatedSession })} questId={query.QuestId} " +
                $"intentions={finished.Intentions.Count} responseFrames=1.")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyQuestMission(
        WorldDataCatalog? worldData,
        uint questId,
        out QuestMission mission)
    {
        mission = default!;
        if (questId > ushort.MaxValue ||
            worldData is null ||
            !worldData.QuestMissionsByMissionId.TryGetValue((int)questId, out var missions) ||
            missions.Count == 0)
        {
            return false;
        }

        mission = missions[0];
        return true;
    }

    private static int GetLegacyQuestIntentionCount(QuestMission mission)
    {
        return mission.MissionId switch
        {
            1469 => 4,
            409 => 3,
            _ => Math.Max(0, mission.IntentAmount ?? mission.Intentions.Count),
        };
    }

    private static int CountDistinctLegacyQuestMissions(IReadOnlyList<QuestMission> questCatalog)
    {
        var count = 0;
        var seenQuestIds = new HashSet<uint>();
        foreach (var mission in questCatalog)
        {
            if (mission.MissionId >= 0 && seenQuestIds.Add(checked((uint)mission.MissionId)))
            {
                count++;
            }
        }

        return count;
    }

    private static LegacyQuestListItem ToLegacyQuestListItem(PlayerQuestSnapshotEntry quest)
    {
        return new LegacyQuestListItem(
            quest.Uid,
            (LegacyQuestStatus)quest.Status,
            quest.Time);
    }

    private static WorldSession WithPlayerQuests(WorldSession session, PlayerQuestSnapshot playerQuests)
    {
        return session with
        {
            Character = session.Character with { PlayerQuests = playerQuests },
        };
    }

}
