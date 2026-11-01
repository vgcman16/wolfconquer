using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool CanPrepareOwnedHawksAmbitionCast(WorldPacketHandlerContext context, WorldSession expected,
        LegacyOrdinarySpellBinding binding, LegacyOwnedHawksAmbitionMetadata metadata)
    {
        RefuseBoundSpellReentry(context.Connection);
        if (!ReferenceEquals(binding.Store, this) || !ReferenceEquals(binding.Connection, context.Connection) ||
            !ReferenceEquals(binding.Client, context.Connection.NativeGameClient) || framePersistenceOwner is not { } frame ||
            !frame.TryPrepareActionContext(this, context, expected, out var actual)) return false;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                using var actors = new SpellActorScope(binding);
                return frame.IsActionLifecycleCurrent(this, actual) && HawksOwnerIsCurrentPlain(binding, expected) &&
                    metadata.IsCurrentPlain(binding.ItemCatalog) && IsOwnedPhysicalMetadataCatalogCurrent(binding, binding.ItemCatalog);
            }
        }
    }

    internal bool TryCommitOwnedHawksAmbitionCosts(WorldPacketHandlerContext context, WorldSession expected, object identity,
        WorldPacketHandlers.OwnedHawksAmbitionCast cast, LegacyRuneCastEligibility? runeEligibility,
        out MagicCostOutputReceipt? receipt, out OwnedHawksOutputReceipt? output)
    {
        receipt = null; output = null;
        RefuseBoundSpellReentry(context.Connection);
        if (framePersistenceOwner is not { } frame || !frame.TryPrepareActionContext(this, context, expected, out var actual)) return false;
        WorldSession final; MagicCostOutputReceipt prepared; OwnedHawksOutputReceipt preparedOutput; LegacyOrdinarySpellBinding binding;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || holder.SpellBinding is not { } currentBinding ||
                    !ReferenceEquals(holder.Read(), expected) || !ReferenceEquals(holder.NativeClientIdentity, identity) ||
                    !BindingIsCurrentPlain(currentBinding, holder, expected)) return false;
                binding = currentBinding;
                using var actors = new SpellActorScope(binding);
                if (!frame.IsActionLifecycleCurrent(this, actual) ||
                    !cast.TryReadPreparedProposal(binding, expected, out var proposed, out _, out _) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                using var publication = new PreparedSessionSpellPublication(this, context.Connection, holder, expected, proposed,
                    ownedHawksCast: cast);
                prepared = new(this, context.Connection, identity, binding, expected, publication.FinalSession);
                preparedOutput = new(this, frame, actual with { Session = expected }, binding, identity, cast);
                if (!frame.IsActionLifecycleCurrent(this, actual) || !cast.IsCurrentPlain(binding, expected) ||
                    runeEligibility is not null && !runeEligibility.IsCurrentPlain() ||
                    !TryPublishFrameOwnedHitpoints(publication, holder, out _, runeEligibility: runeEligibility)) return false;
                final = publication.FinalSession;
                publication.Dispose();
                PersistFrameOwnedSessionChange(context.Connection, expected, final, identity, binding, publication.HitpointsWrite);
            }
        }
        NotifyPublishedSessionChange(context.Connection, expected, final, identity, binding);
        receipt = prepared;
        output = preparedOutput;
        return true;
    }
}
