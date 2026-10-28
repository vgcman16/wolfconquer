using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyCompleteLoginRuntime
{
    internal sealed record Owner(ConquerTcpConnection Connection, LegacyNativeGameClientRuntime Client,
        LegacyNativePlayerRuntime Player, long AccountId, uint Uid);

    private sealed class CompletionSlot { internal Owner? Issued; }
    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<LegacyNativePlayerRuntime, CompletionSlot> Slots = new();

    internal static bool TryCapture(ConquerTcpConnection connection, WorldSession session, out Owner? owner)
    {
        lock (Gate) return TryCaptureCore(connection, session, out owner);
    }

    internal static bool Matches(Owner owner, ConquerTcpConnection connection, WorldSession currentSession)
    {
        lock (Gate) return MatchesCore(owner, connection, currentSession);
    }

    // Acquire the actual issuance cell before the Store's final map proof. No
    // CWT insertion/allocation follows the first physical completion store.
    internal static PreparedCompletion? PrepareComplete(Owner owner, ConquerTcpConnection connection, WorldSession currentSession) =>
        PreparedCompletion.Capture(owner, connection, currentSession);

    internal sealed class PreparedCompletion : IDisposable
    {
        private readonly Owner owner;
        private readonly ConquerTcpConnection connection;
        private readonly WorldSession session;
        private readonly CompletionSlot slot;
        private bool disposed;
        private PreparedCompletion(Owner owner, ConquerTcpConnection connection, WorldSession session, CompletionSlot slot)
        { this.owner = owner; this.connection = connection; this.session = session; this.slot = slot; }
        internal static PreparedCompletion? Capture(Owner owner, ConquerTcpConnection connection, WorldSession session)
        {
            Monitor.Enter(Gate);
            try
            {
                if (!MatchesCore(owner, connection, session)) { Monitor.Exit(Gate); return null; }
                var slot = Slots.GetValue(owner.Player, static _ => new CompletionSlot());
                return new(owner, connection, session, slot);
            }
            catch { Monitor.Exit(Gate); throw; }
        }
        internal bool IsCurrentPlain() => !disposed && Monitor.IsEntered(Gate) && MatchesCore(owner, connection, session);
        internal void PublishPrepared()
        {
            owner.Player.CompleteLogin = true;
            slot.Issued = owner;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Monitor.Exit(Gate);
        }
    }

    internal static bool TryComplete(Owner owner, ConquerTcpConnection connection, WorldSession currentSession)
    {
        using var prepared = PrepareComplete(owner, connection, currentSession);
        if (prepared is null || !prepared.IsCurrentPlain()) return false;
        prepared.PublishPrepared(); return true;
    }

    internal static bool IsComplete(ConquerTcpConnection connection, WorldSession session)
    {
        lock (Gate)
        {
            if (connection.NativeGameClient?.Player is not { } player ||
                !Slots.TryGetValue(player, out var slot) || slot.Issued is not { } issued) return false;
            if (!player.CompleteLogin)
            {
                // An observed raw false retires issuance; a later raw true
                // cannot resurrect it without this actual producer.
                slot.Issued = null; return false;
            }
            return MatchesCore(issued, connection, session);
        }
    }

    private static bool TryCaptureCore(ConquerTcpConnection connection, WorldSession session, out Owner? owner)
    {
        owner = null;
        if (connection is null || session is null || session.Character is null || session.AccountId <= 0 ||
            session.Character.Uid is <= 0 or > uint.MaxValue ||
            connection.NativeGameClient is not { } client || !ReferenceEquals(session.NativeGameClient, client) ||
            client.Player is not { } player || !ReferenceEquals(player.Owner, client) ||
            player.UID != session.Character.Uid) return false;
        owner = new(connection, client, player, session.AccountId, player.UID);
        return true;
    }

    private static bool MatchesCore(Owner owner, ConquerTcpConnection connection, WorldSession session) =>
        owner is not null && owner.Client is not null && owner.Player is not null &&
        session is not null && session.Character is not null && session.AccountId > 0 &&
        ReferenceEquals(owner.Connection, connection) && ReferenceEquals(connection.NativeGameClient, owner.Client) &&
        ReferenceEquals(session.NativeGameClient, owner.Client) && ReferenceEquals(owner.Client.Player, owner.Player) &&
        ReferenceEquals(owner.Player.Owner, owner.Client) && owner.Player.UID == owner.Uid &&
        session.AccountId == owner.AccountId && session.Character.Uid == owner.Uid;
}
