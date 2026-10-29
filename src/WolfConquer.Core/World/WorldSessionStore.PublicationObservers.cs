using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private readonly WorldServerFrameHandler? framePersistenceOwner;

    private WorldSessionStore(WorldServerFrameHandler owner,
        Action<ConquerTcpConnection, WorldSession?, WorldSession>? observer)
    {
        framePersistenceOwner = owner;
        _sessionChanged = observer;
    }

    internal static WorldSessionStore CreateForFrame(WorldServerFrameHandler owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return new(owner, observer: null);
    }

    // The concrete Frame postlude requires the real current Store gate. It is
    // not an arbitrary observer, acceptance callback or transaction writer.
    private void PersistFrameOwnedSessionChange(ConquerTcpConnection connection,
        WorldSession? previous, WorldSession current, object publishedIdentity,
        LegacyOrdinarySpellBinding? publishedBinding, LegacyBoundHitpointsScalarWrite? completedHitpoints = null)
    {
        try { framePersistenceOwner?.PersistChangedSessionState(this, connection, previous, current, completedHitpoints); }
        catch (Exception cause)
        {
            var failure = new WorldSessionPublicationException(this, connection, publishedIdentity,
                publishedBinding, current, WorldSessionPublicationStage.FramePersistence, cause);
            // This phase already owns the package/Store gate. Never reacquire
            // a package scope while holding it in the opposite order.
            BlockPublishedRecovery(failure);
            try { FencePublishedOwnerUnderGate(failure); }
            catch (Exception secondary) { failure.RecordFencingFailure(secondary); }
            throw failure;
        }
    }

    internal bool IsFramePersistenceCurrent(WorldServerFrameHandler owner,
        ConquerTcpConnection connection, WorldSession current) =>
        ReferenceEquals(framePersistenceOwner, owner) &&
        _connectionGates.TryGetValue(connection, out var gate) && Monitor.IsEntered(gate) &&
        _sessions.TryGetValue(connection, out var holder) && ReferenceEquals(holder.Read(), current) &&
        (holder.SpellBinding is not { } binding || BindingIsCurrentPlain(binding, holder, current));

    // Call only after this publication's package, Store and actor scopes end.
    // The committed result was assigned before this notification can replace it.
    private void NotifyPublishedSessionChange(ConquerTcpConnection connection,
        WorldSession? previous, WorldSession current, object publishedIdentity,
        LegacyOrdinarySpellBinding? publishedBinding)
    {
        try { _sessionChanged?.Invoke(connection, previous, current); }
        catch (Exception cause)
        {
            var failure = new WorldSessionPublicationException(this, connection, publishedIdentity,
                publishedBinding, current, WorldSessionPublicationStage.Observer, cause);
            FenceObserverFailure(failure);
            throw failure;
        }
    }

    private void FenceObserverFailure(WorldSessionPublicationException failure)
    {
        // These are the retained original objects, never a pin re-resolved
        // after the callback. A newly issued holder must not be erased.
        BlockPublishedRecovery(failure);
        try
        {
            using var packageScope = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages,
                failure.PublishedSession);
            if (!_connectionGates.TryGetValue(failure.Connection, out var gate)) return;
            lock (gate)
            {
                using var scope = EnterNativeConnectionGate();
                FencePublishedOwnerUnderGate(failure);
            }
        }
        catch (Exception secondary)
        {
            // A failed package/gate fence cannot authorize inverse-order cleanup.
            // Preserve the original typed publication outcome and expose the gap.
            failure.RecordFencingFailure(secondary);
        }
    }

    private void BlockPublishedRecovery(WorldSessionPublicationException failure)
    {
        // Block preserves an existing stronger committed/pending journal reason.
        try { framePersistenceOwner?.BlockPublishedSessionFailure(this, failure); }
        catch (Exception secondary) { failure.RecordFencingFailure(secondary); }
        try { failure.Binding?.Recovery.Block("sessionPostPublicationFailure:" + failure.Stage + ":recoveryRequired"); }
        catch (Exception secondary) { failure.RecordFencingFailure(secondary); }
        try { failure.Binding?.Retire(); }
        catch (Exception secondary) { failure.RecordFencingFailure(secondary); }
    }

    private void FencePublishedOwnerUnderGate(WorldSessionPublicationException failure)
    {
        if (!_sessions.TryGetValue(failure.Connection, out var holder) ||
            !ReferenceEquals(holder.NativeClientIdentity, failure.NativeIdentity)) return;
        holder.Clear();
        _sessions.Remove(failure.Connection);
    }
}
