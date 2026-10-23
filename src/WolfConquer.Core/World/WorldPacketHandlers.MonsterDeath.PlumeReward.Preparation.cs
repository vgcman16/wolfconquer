using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryPrepareLegacyPlumeObjective(
        WorldSession session,
        PlayerQuestSnapshotEntry quest,
        out WorldSession preparedSession,
        out byte[] objectiveFrame)
    {
        preparedSession = session;
        objectiveFrame = [];
        if (quest.Intentions is null) return false;
        try
        {
            var intentions = quest.Intentions.Count == 0 ? new uint[1] : quest.Intentions.ToArray();
            intentions[0] = checked(intentions[0] + 1);
            var preparedQuest = quest with { Intentions = intentions };
            var frame = MsgQuestDataPacket.CreateObjectiveFrame(preparedQuest);
            var candidate = WithPlayerQuests(session,
                session.Character.PlayerQuests.WithQuest(preparedQuest));
            preparedSession = candidate;
            objectiveFrame = frame;
            return true;
        }
        catch (Exception error) when (error is OverflowException or ArgumentOutOfRangeException)
        {
            // Checked objective arithmetic and the actual frame-size/count bound
            // are admission failures, before either item or quest publication.
            return false;
        }
    }
}
