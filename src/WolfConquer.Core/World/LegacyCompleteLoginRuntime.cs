using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Adapter provenance for the direct native CompleteLogin field. There is no saved
// scalar fallback and no dependency on secondary-PIN verification or popup state.
internal static class LegacyCompleteLoginRuntime
{
    internal sealed record Owner(ConquerTcpConnection Connection, LegacyNativeGameClientRuntime Client,
        LegacyNativePlayerRuntime Player, long AccountId, uint Uid);

    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<LegacyNativePlayerRuntime, Owner> Issued = new();

    internal static bool TryCapture(ConquerTcpConnection connection, WorldSession session, out Owner? owner)
    {
        lock (Gate) return TryCaptureCore(connection, session, out owner);
    }

    internal static bool Matches(Owner owner, ConquerTcpConnection connection, WorldSession currentSession)
    {
        lock (Gate) return MatchesCore(owner, connection, currentSession);
    }

    internal static bool TryComplete(Owner owner, ConquerTcpConnection connection, WorldSession currentSession)
    {
        lock (Gate)
        {
            if (!MatchesCore(owner, connection, currentSession)) return false;
            owner.Player.CompleteLogin = true;
            Issued.Remove(owner.Player);
            Issued.Add(owner.Player, owner);
            return true;
        }
    }

    internal static bool IsComplete(ConquerTcpConnection connection, WorldSession session)
    {
        lock (Gate)
        {
            if (!TryCaptureCore(connection, session, out var current) || current is null ||
                !Issued.TryGetValue(current.Player, out var issued)) return false;
            if (!current.Player.CompleteLogin)
            {
                // Once observed false, a later raw true write cannot resurrect an
                // issued completion; the real producer must complete again.
                Issued.Remove(current.Player);
                return false;
            }
            return MatchesCore(issued, connection, session);
        }
    }

    private static bool TryCaptureCore(ConquerTcpConnection connection, WorldSession session, out Owner? owner)
    {
        owner = null;
        if (connection is null || session is null || session.AccountId <= 0 ||
            session.Character.Uid is <= 0 or > uint.MaxValue ||
            connection.NativeGameClient is not { } client || !ReferenceEquals(session.NativeGameClient, client) ||
            client.Player is not { } player || !ReferenceEquals(player.Owner, client) ||
            player.UID != session.Character.Uid) return false;
        owner = new(connection, client, player, session.AccountId, player.UID);
        return true;
    }

    private static bool MatchesCore(Owner owner, ConquerTcpConnection connection, WorldSession session) =>
        owner is not null && ReferenceEquals(owner.Connection, connection) &&
        TryCaptureCore(connection, session, out var current) && current is not null &&
        ReferenceEquals(owner.Client, current.Client) && ReferenceEquals(owner.Player, current.Player) &&
        owner.AccountId == current.AccountId && owner.Uid == current.Uid;
}
