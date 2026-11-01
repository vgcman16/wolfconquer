using WolfConquer.Core.Networking;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryRegisterUnboundCharacterOrigin(WorldServerFrameHandler frame, ConquerTcpConnection connection,
        WorldSession expected, WorldServerFrameHandler.UnboundOriginRegistration registration)
    {
        RefuseBoundSpellReentry(connection);
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
        if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
        lock (gate)
        {
            using var native = EnterNativeConnectionGate();
            return ReferenceEquals(framePersistenceOwner, frame) && _sessions.TryGetValue(connection, out var holder) &&
                ReferenceEquals(holder.Read(), expected) && holder.SpellBinding is null &&
                connection.NativeGameClient is null && expected.NativeGameClient is null &&
                frame.CompleteUnboundOriginRegistration(this, connection, expected, registration);
        }
    }

    internal bool TryCommitUnboundQueryCharacter(WorldPacketHandlerContext context, WorldSession expected,
        WorldPacketHandlers.ManagedQueryHitpointsPurpose purpose, int value, bool vigor,
        out bool applicable, out WorldSession? final)
    {
        final = null;
        applicable = framePersistenceOwner?.RequiresUnboundCharacterOrigin(expected) == true;
        if (!applicable) return false;
        RefuseBoundSpellReentry(context.Connection);
        object identity;
        using (var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected))
        {
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                if (!ReferenceEquals(context.Sessions, this) || !_sessions.TryGetValue(context.Connection, out var holder) ||
                    !ReferenceEquals(holder.Read(), expected) || holder.SpellBinding is not null ||
                    !WorldPacketHandlers.TryConsumeManagedQueryHitpoints(purpose, context, expected, value, vigor)) return false;
                var proposed = vigor ? WorldPacketHandlers.ProjectManagedQueryVigor(expected, checked((ushort)value)) :
                    WorldPacketHandlers.ProjectAssignedLegacyHitpoints(context, expected, expected.Character.Uid, value);
                if (!LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                uint nextVigor = LegacyEquipmentQueryBounds.ReadVigor(proposed.Character);
                if (!CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(expected.Character, proposed.Character))
                {
                    using var runtime = new PreparedSessionSpellPublication(this, context.Connection, holder, expected, proposed);
                    runtime.ArmFinalProof();
                    if (!runtime.ConsumeBeforeWriterPlain() || !runtime.IsCurrentPlain()) throw InvalidSpellPublication();
                    runtime.PublishNative(); runtime.PublishHolder();
                    final = runtime.FinalSession; identity = runtime.PublishedIdentity;
                    framePersistenceOwner!.RecordCompletedUnboundQueryRuntime(this, context, expected, final,
                        purpose, value, vigor, nextVigor);
                    // An unchanged HP assignment/runtime Vigor clamp has no
                    // scalar save and must not acquire a disk mutation owner.
                    return true;
                }
                var write = WorldServerFrameHandler.UnboundQueryCharacterWrite.Prepare(framePersistenceOwner!,
                    this, context.Connection, expected, proposed, writesFile: !vigor, nextVigor);
                using var publication = new PreparedSessionSpellPublication(this, context.Connection, holder, expected, write.Proposed);
                publication.ArmFinalProof();
                if (!publication.ConsumeBeforeWriterPlain()) throw InvalidSpellPublication();
                bool uncertain = false;
                try
                {
                    if (!write.TryCommit(out uncertain, out var reason))
                    {
                        if (uncertain) throw new InvalidOperationException("managedQueryWriteUncertain:" + reason);
                        return false;
                    }
                    if (!publication.IsCurrentPlain()) throw InvalidSpellPublication();
                    publication.PublishNative(); publication.PublishHolder();
                    final = publication.FinalSession; identity = publication.PublishedIdentity;
                    write.CompletePublication(final);
                    // This closed receipt completed the only changed persisted
                    // component. Do not run the generic scalar writer again.
                }
                catch (Exception error) when (write.IsCommitted || uncertain)
                {
                    var failures = new List<Exception> { error };
                    try { framePersistenceOwner!.BlockUnboundQueryFailure(); } catch (Exception fence) { failures.Add(fence); }
                    try { holder.Clear(); } catch (Exception fence) { failures.Add(fence); }
                    if (failures.Count > 1) throw new AggregateException("Managed query commit and fencing failed.", failures);
                    throw;
                }
            }
        }
        NotifyPublishedSessionChange(context.Connection, expected, final!, identity, null);
        return true;
    }

    // Cleanup after the real Frame's initial managed-origin bind fails. A
    // replacement holder, including a same-UID holder, is never retired here.
    internal bool TryRemoveUnboundOriginExact(ConquerTcpConnection connection, WorldSession expected)
    {
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
        if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
        lock (gate)
        {
            using var native = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                holder.SpellBinding is not null || connection.NativeGameClient is not null || expected.NativeGameClient is not null)
                return false;
            holder.Clear(); _sessions.Remove(connection); return true;
        }
    }
}
