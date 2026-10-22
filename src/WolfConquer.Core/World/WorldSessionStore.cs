using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed record WorldActiveSession(
    ConquerTcpConnection Connection,
    WorldSession Session);

public sealed partial class WorldSessionStore
{
    private readonly ConditionalWeakTable<ConquerTcpConnection, SessionHolder> _sessions = new();
    private readonly ConditionalWeakTable<ConquerTcpConnection, object> _connectionGates = new();
    private readonly Action<ConquerTcpConnection, WorldSession?, WorldSession>? _sessionChanged;

    // Synchronous lock-reentrancy detection ONLY. Not a client identity or async ownership token.
    [ThreadStatic]
    private static int _enteredConnectionGateDepth;
    internal static bool IsCurrentThreadInsideAnyConnectionGate => _enteredConnectionGateDepth != 0;

    private static NativeConnectionGateScope EnterNativeConnectionGate()
    {
        _enteredConnectionGateDepth = checked(_enteredConnectionGateDepth + 1);
        return default;
    }

    private readonly struct NativeConnectionGateScope : IDisposable
    {
        public void Dispose() => _enteredConnectionGateDepth--;
    }

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
            using var nativeGateScope = EnterNativeConnectionGate();
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
            using var nativeGateScope = EnterNativeConnectionGate();
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
            using var nativeGateScope = EnterNativeConnectionGate();
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

    // Synchronous native-client cache access uses the SAME gate as Set/CAS/Remove.
    // No await or external publication occurs inside this callback.
    internal bool WithCurrentNativeClientIdentity(
        ConquerTcpConnection connection, long originalUid, Action<object> action)
    {
        if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
        lock (gate)
        {
            using var nativeGateScope = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(connection, out var holder) ||
                holder.Read() is not { } current || current.Character.Uid != originalUid) return false;
            action(holder.NativeClientIdentity);
            return true;
        }
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
        public object NativeClientIdentity { get; private set; } = new();

        public WorldSession? Read()
        {
            return Volatile.Read(ref _session);
        }

        public void Publish(WorldSession session)
        {
            if (_session is { } previous && previous.Character.Uid != session.Character.Uid)
                NativeClientIdentity = new object();
            Volatile.Write(ref _session, session);
        }

        public void Clear()
        {
            NativeClientIdentity = new object();
            Volatile.Write(ref _session, null);
        }
    }
}
