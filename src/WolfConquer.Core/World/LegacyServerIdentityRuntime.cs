using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyServerIdentityRuntime
{
    private sealed record Binding(ConquerTcpConnection Connection, LegacyNativeGameClientRuntime Client,
        LegacyNativePlayerRuntime Player, long AccountId, uint Uid, uint ServerId);

    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<LegacyNativePlayerRuntime, Binding> Issued = new();

    internal static bool TryInitialize(ConquerTcpConnection connection, WorldSession session, LegacyLocalServerIdentity? identity)
    {
        lock (Gate)
        {
            if (identity is null || !TryPlayer(connection, session, out var client, out var player)) return false;
            player.ServerID = unchecked((ushort)identity.ServerId);
            Issued.Remove(player);
            Issued.Add(player, new(connection, client, player, session.AccountId, player.UID, identity.ServerId));
            return true;
        }
    }

    internal static bool IsOnOwnServer(ConquerTcpConnection connection, WorldSession session, LegacyLocalServerIdentity? identity)
    {
        lock (Gate)
        {
            if (identity is null || !TryPlayer(connection, session, out var client, out var player) ||
                !Issued.TryGetValue(player, out var binding) || !ReferenceEquals(binding.Connection, connection) ||
                !ReferenceEquals(binding.Client, client) || !ReferenceEquals(binding.Player, player) ||
                binding.AccountId != session.AccountId || binding.Uid != player.UID || binding.ServerId != identity.ServerId ||
                player.ServerID != unchecked((ushort)binding.ServerId)) return false;
            // Native Player1363 compares its ushort field to the FULL configured uint,
            // not to another narrowed value. A wrapped assignment can therefore be false.
            return player.ServerID == identity.ServerId;
        }
    }

    private static bool TryPlayer(ConquerTcpConnection connection, WorldSession session,
        out LegacyNativeGameClientRuntime client, out LegacyNativePlayerRuntime player)
    {
        client = null!;
        player = null!;
        if (connection is null || session is null || session.AccountId <= 0 ||
            session.Character.Uid is <= 0 or > uint.MaxValue ||
            connection.NativeGameClient is not { } actualClient || !ReferenceEquals(session.NativeGameClient, actualClient) ||
            actualClient.Player is not { } actualPlayer || !ReferenceEquals(actualPlayer.Owner, actualClient) ||
            actualPlayer.UID != session.Character.Uid) return false;
        client = actualClient;
        player = actualPlayer;
        return true;
    }
}
