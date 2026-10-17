namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static WorldActiveSession? FindLegacyMapBlockPlayer(
        WorldSessionStore sessions,
        WorldSession session,
        uint opponentUid)
    {
        // RoleView.TryGetValue is a raw neighboring-block lookup, unlike RoleView.Roles/CanSee.
        return sessions.Snapshot().FirstOrDefault(active =>
            active.Session.Character.Uid == opponentUid &&
            active.Session.Character.MapId == session.Character.MapId &&
            Math.Abs(active.Session.Character.X / 18 - session.Character.X / 18) <= 1 &&
            Math.Abs(active.Session.Character.Y / 18 - session.Character.Y / 18) <= 1);
    }
}
