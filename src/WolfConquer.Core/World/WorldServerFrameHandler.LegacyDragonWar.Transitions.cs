using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private static LegacyDragonWarRuntimeSessionUpdate CreateLegacyDragonWarRuntimeTeleportSessionUpdate(
        LegacyDragonWarRuntimeSessionUpdate sessionUpdate)
    {
        var cleanup = WorldPacketHandlers.CreateLegacyTeleportStatusCleanup(sessionUpdate.Session);
        if (!cleanup.HasChanges)
        {
            return sessionUpdate;
        }

        var statusFrameBytes = cleanup.Removals.Count > 0
            ? CreateLegacyDragonWarRuntimeStatusFrame(cleanup.CleanedSession)
            : sessionUpdate.StatusFrameBytes;
        return sessionUpdate with
        {
            Session = cleanup.CleanedSession,
            StatusFrameBytes = statusFrameBytes,
        };
    }

    private static byte[] CreateLegacyDragonWarRuntimeStatusFrame(WorldSession session)
    {
        return LegacyMsgUpdatePacket
            .CreateLegacyRuntimeStatusFlagVectorUpdate(
                CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                LegacyPlayerDeathState.CreateStatusFlagVector(
                    session.Character.Attributes,
                    LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
            .ToFrameBytes();
    }

    private static async ValueTask<int> SendLegacyDragonWarRuntimeStatusAfterTeleportAsync(
        IReadOnlyList<WorldActiveSession> activeSessions,
        IReadOnlyList<ConquerTcpConnection> ownerConnections,
        LegacyDragonWarRuntimeSessionUpdate sessionUpdate,
        CancellationToken cancellationToken)
    {
        var recipients = new HashSet<ConquerTcpConnection>();
        foreach (var connection in ownerConnections)
        {
            recipients.Add(connection);
        }

        foreach (var active in activeSessions)
        {
            if (active.Session.Character.Uid == sessionUpdate.Session.Character.Uid ||
                !CanSeePlayer(active.Session.Character, sessionUpdate.Session.Character))
            {
                continue;
            }

            recipients.Add(active.Connection);
        }

        foreach (var recipient in recipients)
        {
            await recipient.SendAsync(sessionUpdate.StatusFrameBytes, cancellationToken).ConfigureAwait(false);
        }

        return recipients.Count;
    }

    private static bool IsLegacyDragonWarRuntimeTeleport(
        CharacterSnapshot previousCharacter,
        CharacterSnapshot updatedCharacter)
    {
        return previousCharacter.MapId != updatedCharacter.MapId ||
            previousCharacter.X != updatedCharacter.X ||
            previousCharacter.Y != updatedCharacter.Y;
    }

}
