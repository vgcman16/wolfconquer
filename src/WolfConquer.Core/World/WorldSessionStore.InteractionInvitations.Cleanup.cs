namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Actual next-Frame housekeeping, not permission derived from raw links.
    // EOF never acquires this peer's gate or modifies its live holder itself.
    internal bool TryCompleteEndedInteractionCleanup(WorldPacketHandlerContext context, out WorldPacketHandlerContext refreshed)
    {
        refreshed = context;
        if (context.Session is not { } expected || framePersistenceOwner is not { } frame ||
            context.Connection.NativeGameClient?.OrdinarySpellBinding is not { } binding ||
            !frame.HasEndedInteractionCleanup(binding)) return true;
        RefuseBoundSpellReentry(context.Connection);
        if (!frame.TryPrepareActionContext(this, context, expected, out var actual)) return false;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                using var actors = new SpellActorScope(binding);
                if (!_sessions.TryGetValue(context.Connection, out var holder) ||
                    !BindingIsCurrentPlain(binding, holder, expected) ||
                    !frame.TryPrepareEndedInteractionCleanup(this, actual, binding, expected, out var cleanup)) return false;
                if (cleanup is null) { refreshed = actual; return true; }
                var proposed = cleanup.Proposed;
                if (!LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                using var publication = new PreparedSessionSpellPublication(this, context.Connection, holder, expected, proposed);
                RequireInteractionDataUnchanged(expected, publication.FinalSession, publication.HitpointsWrite);
                var canonicalContext = actual with { Session = publication.FinalSession };
                publication.RequireCurrentBeforeWriter(); publication.ArmFinalProof();
                if (!cleanup.IsCurrentPlain(binding, expected) || !publication.ConsumeBeforeWriterPlain() ||
                    !publication.IsCurrentPlain() || !cleanup.IsCurrentPlain(binding, expected)) return false;
                publication.PublishNative(); publication.PublishHolder(); cleanup.PublishPrepared();
                refreshed = canonicalContext;
                return true;
            }
        }
    }
}
