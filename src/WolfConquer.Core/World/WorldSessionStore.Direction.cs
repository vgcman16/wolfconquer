using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Only the real ChangeDirection handler calls this narrow Angle operation.
    // Same-angle input still accepts/cancels; Action value is retained in full.
    internal bool TryCommitOwnedDirection(WorldPacketHandlerContext context, ushort facing, out WorldSession? final)
    {
        final = null;
        RefuseBoundSpellReentry(context.Connection);
        if (context.Session is not { } expected || framePersistenceOwner is not { } frame ||
            !frame.TryPrepareActionContext(this, context, expected, out var actual)) return false;
        context = actual;
        var before = new LegacyNativeAttributeMapProof(expected.Character.Attributes);
        var fields = new Dictionary<string, string>(before.Fields, StringComparer.OrdinalIgnoreCase)
        { ["Angle"] = facing.ToString(CultureInfo.InvariantCulture) };
        var proposed = expected with { Character = expected.Character with { Attributes = fields } };
        LegacyOrdinarySpellBinding binding; object identity;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected, proposed);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    holder.SpellBinding is not { } current || !BindingIsCurrentPlain(current, holder, expected) ||
                    !frame.IsActionLifecycleCurrent(this, context) || !before.IsCurrentPlain() ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                binding = current;
                using var publication = new PreparedSessionSpellPublication(this, context.Connection, holder, expected, proposed);
                if (publication.HitpointsWrite is not null ||
                    CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(expected.Character, publication.FinalSession.Character))
                    throw InvalidSpellPublication();
                var cancellation = PrepareBasicPhysicalCancellation(frame, context, binding, expected);
                if (!before.IsCurrentPlain() || !TryPublishFrameOwnedHitpoints(publication, holder, out _, basicPhysical: cancellation)) return false;
                final = publication.FinalSession; identity = publication.PublishedIdentity;
                publication.Dispose();
                PersistFrameOwnedSessionChange(context.Connection, expected, final, identity, binding);
            }
        }
        NotifyPublishedSessionChange(context.Connection, expected, final, identity, binding);
        return true;
    }
}
