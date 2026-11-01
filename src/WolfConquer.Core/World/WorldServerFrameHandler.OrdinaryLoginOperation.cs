using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly object ordinaryLoginOperationGate = new();
    private readonly ConditionalWeakTable<ConquerTcpConnection, OrdinaryLoginOperation> ordinaryLoginOperations = new();

    // Installed only around the real inbound login dispatch. It retains the
    // already-owned operation; it never acquires or upgrades another lease.
    private OrdinaryLoginOperation? EnterOrdinaryLoginOperation(ConquerTcpConnection connection, IDisposable? operation)
    {
        if (_protocolProfile != ConquerProtocolProfile.VisibleDx8D13) return null;
        if (operation is null || !_worldOperations.OwnsActiveExclusiveLease(operation))
            throw new InvalidOperationException("ordinaryLoginExclusiveOperationUnavailable");
        lock (ordinaryLoginOperationGate)
        {
            if (ordinaryLoginOperations.TryGetValue(connection, out _))
                throw new InvalidOperationException("ordinaryLoginOperationAlreadyRegistered");
            var source = new OrdinaryLoginOperation(this, connection, operation);
            ordinaryLoginOperations.Add(connection, source);
            return source;
        }
    }

    // This supplies operation identity, not credit/resume authority. The
    // continuation must still prove its real admission plan and saved marker.
    private bool TryCreateOrdinaryContinuationLoginContext(ConquerTcpConnection connection,
        WorldSession expected, out WorldPacketHandlerContext context)
    {
        context = null!;
        if (!ordinaryLoginOperations.TryGetValue(connection, out var source) || !source.IsCurrent(this, connection) ||
            !_sessions.TryGet(connection, out var current) || !ReferenceEquals(current, expected)) return false;
        var actual = CreateMonsterRuntimeContext(new(connection, expected)) with
        {
            TradeOperations = _worldOperations,
            FrameOperationLease = source.Operation,
            PlayerProficienciesRoot = _snapshotLoader?.PlayerProficienciesRoot,
        };
        if (!TryPrepareActionContext(_sessions, actual, expected, out actual) || !source.IsCurrent(this, connection)) return false;
        context = actual;
        return true;
    }

    internal bool IsOrdinaryContinuationLoginContext(ConquerTcpConnection connection, WorldPacketHandlerContext context) =>
        ReferenceEquals(context.Connection, connection) && ReferenceEquals(context.Sessions, _sessions) &&
        ReferenceEquals(context.WorldData, _worldData) && ReferenceEquals(context.Trades, _tradeStates) &&
        ReferenceEquals(context.TradeRecovery, _tradeRecovery) && ReferenceEquals(context.TradeOperations, _worldOperations) &&
        ordinaryLoginOperations.TryGetValue(connection, out var source) && source.IsCurrent(this, connection) &&
        ReferenceEquals(context.FrameOperationLease, source.Operation);

    internal bool IsOrdinaryContinuationLoginContextCurrent(WorldPacketHandlerContext context, WorldSession expected) =>
        IsOrdinaryContinuationLoginContext(context.Connection, context) &&
        TryPrepareActionContext(_sessions, context, expected, out _);

    private sealed class OrdinaryLoginOperation : IDisposable
    {
        private readonly WorldServerFrameHandler owner;
        private readonly ConquerTcpConnection connection;
        private int disposed;
        internal IDisposable Operation { get; }

        internal OrdinaryLoginOperation(WorldServerFrameHandler owner, ConquerTcpConnection connection, IDisposable operation)
        { this.owner = owner; this.connection = connection; Operation = operation; }

        internal bool IsCurrent(WorldServerFrameHandler actual, ConquerTcpConnection candidate) =>
            Volatile.Read(ref disposed) == 0 && ReferenceEquals(actual, owner) && ReferenceEquals(candidate, connection) &&
            owner._worldOperations.OwnsActiveExclusiveLease(Operation) &&
            owner.ordinaryLoginOperations.TryGetValue(connection, out var retained) && ReferenceEquals(retained, this);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lock (owner.ordinaryLoginOperationGate)
                if (owner.ordinaryLoginOperations.TryGetValue(connection, out var retained) && ReferenceEquals(retained, this))
                    owner.ordinaryLoginOperations.Remove(connection);
        }
    }
}
