using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool CoversOwnedProficiencyPublication(OwnedProficiencyPublication receipt, WorldServerFrameHandler frame,
        ConquerTcpConnection connection, WorldSession? before, WorldSession after)
    {
        if (!ownedProficiencyPublications.TryGetValue(receipt, out var data) || !data.Committed ||
            !ReferenceEquals(data.Store, this) || !ReferenceEquals(data.Frame, frame) ||
            !ReferenceEquals(data.Context.Connection, connection) || !ReferenceEquals(data.Before, before) ||
            !ReferenceEquals(data.After, after) || !IsFramePersistenceCurrent(frame, connection, after)) return false;
        using var actors = new SpellActorScope(data.Binding);
        return OwnedProficiencyOutcomeIsCurrentPlain(data);
    }

    private bool OwnedProficiencyOutcomeIsCurrentPlain(OwnedProficiencyPublicationData data) => data.Committed &&
        data.FileWritten && framePersistenceOwner is { } frame && ReferenceEquals(data.Frame, frame) &&
        frame.IsActionLifecycleCurrent(this, data.Context) &&
        _sessions.TryGetValue(data.Context.Connection, out var holder) && ReferenceEquals(holder.Read(), data.After) &&
        ReferenceEquals(holder.NativeClientIdentity, data.Identity) && ReferenceEquals(holder.SpellBinding, data.Binding) &&
        BindingIsCurrentPlain(data.Binding, holder, data.After) && data.Native.HasCurrentCommittedGraph() &&
        ReferenceEquals(data.AfterImage, data.Binding.SavedProficiencies) &&
        SnapshotFileLockCoordinator.ExecuteLocked(data.Path, () => data.AfterImage.Matches(LegacySavedProficiencyImage.Read(data.Path)));

    internal bool TryReadOwnedProficiencyPublication(OwnedProficiencyPublication receipt, out WorldSession current)
    {
        current = null!;
        if (!ownedProficiencyPublications.TryGetValue(receipt, out var data) || !data.Committed) return false;
        try
        {
            lock (data.Frame.RequireActionLifecycleGate(this))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, PeekMailboxSession(data.Context.Connection));
                if (!_connectionGates.TryGetValue(data.Context.Connection, out var gate)) return false;
                lock (gate)
                {
                    using var nativeGate = EnterNativeConnectionGate();
                    using var actors = new SpellActorScope(data.Binding);
                    if (!OwnedProficiencyOutcomeIsCurrentPlain(data)) return false;
                    current = data.After; return true;
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        { return false; }
    }

    internal void FenceOwnedProficiencyOutputFailure(OwnedProficiencyPublication receipt, Exception primary)
    {
        if (!ownedProficiencyPublications.TryGetValue(receipt, out var data) || !data.Committed)
        {
            ExceptionDispatchInfo.Capture(primary).Throw();
            throw new InvalidOperationException("Unreachable failure propagation.");
        }
        var errors = new List<Exception> { primary }; bool attempted = false;
        try
        {
            lock (data.Frame.RequireActionLifecycleGate(this))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, PeekMailboxSession(data.Context.Connection));
                if (_connectionGates.TryGetValue(data.Context.Connection, out var gate))
                    lock (gate)
                    {
                        using var nativeGate = EnterNativeConnectionGate();
                        attempted = true; CollectOwnedProficiencyFences(data, errors);
                    }
            }
        }
        catch (Exception error) { errors.Add(error); }
        if (!attempted) CollectOwnedProficiencyFences(data, errors);
        ThrowOwnedProficiencyFailure(primary, errors);
    }

    private void FenceOwnedProficiencyFailure(OwnedProficiencyPublicationData data, Exception primary)
    {
        var errors = new List<Exception> { primary };
        CollectOwnedProficiencyFences(data, errors);
        ThrowOwnedProficiencyFailure(primary, errors);
    }

    private void CollectOwnedProficiencyFences(OwnedProficiencyPublicationData data, List<Exception> errors)
    {
        try { data.Binding.Recovery.Block("ownedProficiencyRecoveryRequired:committedPublicationUnproven"); }
        catch (Exception error) { errors.Add(error); }
        try { data.Binding.Retire(); } catch (Exception error) { errors.Add(error); }
        try
        {
            // Absence of the actual gate permits retirement/blocking only;
            // never clear a holder by a naked lookup after acquisition failed.
            if (IsCurrentThreadInsideAnyConnectionGate &&
                _connectionGates.TryGetValue(data.Context.Connection, out var gate) && Monitor.IsEntered(gate) &&
                _sessions.TryGetValue(data.Context.Connection, out var holder) &&
                ReferenceEquals(holder.NativeClientIdentity, data.Identity) && ReferenceEquals(holder.SpellBinding, data.Binding) &&
                ReferenceEquals(holder.Read(), data.Committed ? data.After : data.Before)) holder.Clear();
        }
        catch (Exception error) { errors.Add(error); }
    }

    private static void ThrowOwnedProficiencyFailure(Exception primary, List<Exception> errors)
    {
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(primary).Throw();
        throw new AggregateException("Owned proficiency publication and recovery fencing failed.", errors);
    }
}
