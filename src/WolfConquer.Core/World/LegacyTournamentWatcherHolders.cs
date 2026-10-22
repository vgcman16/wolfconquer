using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Two native Match-reference families. IDs describe the group; they are NOT identity aliases.
public sealed class LegacyElitePkWatchingGroup(uint matchId, uint dynamicId)
{
    public uint MatchId { get; } = matchId;
    public uint DynamicId { get; } = dynamicId;
}

public sealed class LegacyTeamElitePkWatchingGroup(uint matchId, uint dynamicId)
{
    public uint MatchId { get; } = matchId;
    public uint DynamicId { get; } = dynamicId;
}

// Captured before the producer's first await; no public constructor or UID-only rebinding.
public sealed class LegacyTournamentWatcherClient
{
    internal LegacyTournamentWatcherClient(
        LegacyTournamentWatcherHolders owner, ConquerTcpConnection connection,
        long uid, object identity)
    {
        Owner = owner;
        Connection = connection;
        Uid = uid;
        Identity = identity;
    }

    internal LegacyTournamentWatcherHolders Owner { get; }
    internal object Identity { get; }
    public ConquerTcpConnection Connection { get; }
    public long Uid { get; }
}

public sealed record LegacyTournamentWatcherSnapshot(
    LegacyElitePkWatchingGroup? ElitePkWatchingGroup,
    LegacyTeamElitePkWatchingGroup? TeamElitePkWatchingGroup)
{
    public bool IsWatching =>
        ElitePkWatchingGroup is not null || TeamElitePkWatchingGroup is not null;
}

// Holder-only prerequisite. It does NOT implement BeginWatching, watcher lists or wire/teleport.
public sealed class LegacyTournamentWatcherHolders
{
    private readonly WorldSessionStore _sessions;
    private readonly ConditionalWeakTable<object, Holder> _holders = new();

    public LegacyTournamentWatcherHolders(WorldSessionStore sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        _sessions = sessions;
    }

    public bool TryCaptureClient(
        ConquerTcpConnection connection, out LegacyTournamentWatcherClient? client)
    {
        ArgumentNullException.ThrowIfNull(connection);
        client = null;
        if (!_sessions.TryGet(connection, out var session) || session is null ||
            !LegacyNativeClientPublication.TryCaptureIdentity(
                _sessions, connection, session.Character.Uid, out var identity))
            return false;

        client = new LegacyTournamentWatcherClient(
            this, connection, session.Character.Uid, identity);
        return true;
    }

    public bool TryRead(
        LegacyTournamentWatcherClient client, out LegacyTournamentWatcherSnapshot snapshot)
    {
        LegacyTournamentWatcherSnapshot? captured = null;
        var current = WithCurrent(client, holder =>
        {
            captured = new(holder.ElitePk, holder.TeamElitePk);
            return true;
        });
        snapshot = captured ?? new(null, null);
        return current;
    }

    // Native fields are independent direct assignments. Busy checks belong BEFORE awaited send.
    public bool TrySetElitePkWatchingGroup(
        LegacyTournamentWatcherClient client, LegacyElitePkWatchingGroup? group)
    {
        return WithCurrent(client, holder =>
        {
            holder.ElitePk = group;
            return true;
        });
    }

    public bool TrySetTeamElitePkWatchingGroup(
        LegacyTournamentWatcherClient client, LegacyTeamElitePkWatchingGroup? group)
    {
        return WithCurrent(client, holder =>
        {
            holder.TeamElitePk = group;
            return true;
        });
    }

    private bool WithCurrent(
        LegacyTournamentWatcherClient client, Func<Holder, bool> mutation)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (!ReferenceEquals(client.Owner, this))
            return false;

        var accepted = false;
        _sessions.WithCurrentNativeClientIdentity(client.Connection, client.Uid, identity =>
        {
            if (!ReferenceEquals(identity, client.Identity))
                return;
            // Same gate as Set/Remove; no await, send, Store.Set or external callback.
            accepted = mutation(_holders.GetValue(identity, static _ => new Holder()));
        });
        return accepted;
    }

    private sealed class Holder
    {
        public LegacyElitePkWatchingGroup? ElitePk { get; set; }
        public LegacyTeamElitePkWatchingGroup? TeamElitePk { get; set; }
    }
}
