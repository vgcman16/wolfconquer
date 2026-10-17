using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed record WorldActiveSession(
    ConquerTcpConnection Connection,
    WorldSession Session);

public sealed class WorldSessionStore
{
    private readonly ConditionalWeakTable<ConquerTcpConnection, SessionHolder> _sessions = new();
    private readonly ConditionalWeakTable<ConquerTcpConnection, object> _connectionGates = new();
    private readonly Action<ConquerTcpConnection, WorldSession?, WorldSession>? _sessionChanged;

    public WorldSessionStore(Action<ConquerTcpConnection, WorldSession?, WorldSession>? sessionChanged = null)
    {
        _sessionChanged = sessionChanged;
    }

    public void Set(ConquerTcpConnection connection, WorldSession session)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(session);

        var gate = _connectionGates.GetValue(connection, static _ => new object());
        lock (gate)
        {
            var holder = _sessions.GetValue(connection, static _ => new SessionHolder());
            var previous = holder.Read();
            holder.Publish(session);
            _sessionChanged?.Invoke(connection, previous, session);
        }
    }

    public bool TryCompareAndSet(
        ConquerTcpConnection connection,
        WorldSession expectedSession,
        WorldSession session)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(expectedSession);
        ArgumentNullException.ThrowIfNull(session);

        var gate = _connectionGates.GetValue(connection, static _ => new object());
        lock (gate)
        {
            var holder = _sessions.GetValue(connection, static _ => new SessionHolder());
            if (!ReferenceEquals(holder.Read(), expectedSession))
            {
                return false;
            }

            holder.Publish(session);
            _sessionChanged?.Invoke(connection, expectedSession, session);
            return true;
        }
    }

    public bool TryRemove(ConquerTcpConnection connection, out WorldSession? session)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var gate = _connectionGates.GetValue(connection, static _ => new object());
        lock (gate)
        {
            if (!_sessions.TryGetValue(connection, out var holder))
            {
                session = null;
                return false;
            }

            session = holder.Read();
            if (session is null)
            {
                _sessions.Remove(connection);
                return false;
            }

            holder.Clear();
            _sessions.Remove(connection);
            return true;
        }
    }

    public bool TryGet(ConquerTcpConnection connection, out WorldSession? session)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!_sessions.TryGetValue(connection, out var holder))
        {
            session = null;
            return false;
        }

        session = holder.Read();
        return session is not null;
    }

    public IReadOnlyList<WorldActiveSession> Snapshot()
    {
        var sessions = new List<WorldActiveSession>();
        foreach (var pair in _sessions)
        {
            var session = pair.Value.Read();
            if (session is not null)
            {
                sessions.Add(new WorldActiveSession(pair.Key, session));
            }
        }

        return sessions;
    }

    private sealed class SessionHolder
    {
        private WorldSession? _session;

        public WorldSession? Read()
        {
            return Volatile.Read(ref _session);
        }

        public void Publish(WorldSession session)
        {
            Volatile.Write(ref _session, session);
        }

        public void Clear()
        {
            Volatile.Write(ref _session, null);
        }
    }
}
