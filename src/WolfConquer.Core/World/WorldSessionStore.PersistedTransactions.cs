using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private static readonly ConditionalWeakTable<ConquerTcpConnection, TransactionGateOrder> TransactionGateOrders = new();
    private static long _nextTransactionGateOrder;

    // Every multi-connection owner must share this order, including native teams.
    // Independent order registries over the same connection gates can deadlock.
    internal static long GetConnectionGateOrder(ConquerTcpConnection connection) =>
        TransactionGateOrders.GetValue(connection, static _ =>
            new TransactionGateOrder(Interlocked.Increment(ref _nextTransactionGateOrder))).Value;

    // Only for callers whose persistence callback saves every changed field. Unlike Set/CAS,
    // these commits deliberately do not invoke the ordinary auto-persistence callback again.
    internal bool TryCommitSession(
        ConquerTcpConnection connection,
        WorldSession expected,
        WorldSession updated,
        Func<bool> persist)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(updated);
        ArgumentNullException.ThrowIfNull(persist);
        if (IsCurrentThreadInsideAnyConnectionGate) return false;
        var gate = _connectionGates.GetValue(connection, static _ => new object());
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(connection, out var holder) ||
                !ReferenceEquals(holder.Read(), expected) || !persist()) return false;
            holder.Publish(updated);
            return true;
        }
    }

    // Serializes both participants against other session writers and checks both references
    // before touching disk. Snapshot/TryGet remain point-in-time lock-free reads, not a
    // multi-session snapshot transaction. Trade's own state gate protects trade readers.
    internal bool TryCommitPair(
        ConquerTcpConnection firstConnection,
        WorldSession expectedFirst,
        WorldSession updatedFirst,
        ConquerTcpConnection secondConnection,
        WorldSession expectedSecond,
        WorldSession updatedSecond,
        Func<bool> persist)
    {
        ArgumentNullException.ThrowIfNull(firstConnection);
        ArgumentNullException.ThrowIfNull(secondConnection);
        ArgumentNullException.ThrowIfNull(expectedFirst);
        ArgumentNullException.ThrowIfNull(expectedSecond);
        ArgumentNullException.ThrowIfNull(updatedFirst);
        ArgumentNullException.ThrowIfNull(updatedSecond);
        ArgumentNullException.ThrowIfNull(persist);
        if (ReferenceEquals(firstConnection, secondConnection) || IsCurrentThreadInsideAnyConnectionGate) return false;
        var firstGate = _connectionGates.GetValue(firstConnection, static _ => new object());
        var secondGate = _connectionGates.GetValue(secondConnection, static _ => new object());
        var firstOrder = GetConnectionGateOrder(firstConnection);
        var secondOrder = GetConnectionGateOrder(secondConnection);
        var outer = firstOrder < secondOrder ? firstGate : secondGate;
        var inner = firstOrder < secondOrder ? secondGate : firstGate;
        lock (outer)
        lock (inner)
        {
            using var scope = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(firstConnection, out var firstHolder) ||
                !_sessions.TryGetValue(secondConnection, out var secondHolder) ||
                !ReferenceEquals(firstHolder.Read(), expectedFirst) ||
                !ReferenceEquals(secondHolder.Read(), expectedSecond) || !persist()) return false;
            firstHolder.Publish(updatedFirst);
            secondHolder.Publish(updatedSecond);
            return true;
        }
    }

    private sealed record TransactionGateOrder(long Value);
}
