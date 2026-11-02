using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Fixed real publisher: the only persistence operation is the typed relic
    // journal writer below. It accepts no caller-supplied saver or acceptance.
    internal bool TryCommitOwnedRelicFuse(WorldPacketHandlerContext context, WorldSession expected,
        WorldSession candidate, LegacyPreparedSpellFileWrite spellWrite, LegacyRelicFuseOwnedEvidence evidence,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyJournalInterruption? interruption,
        out WorldSession? final, out RelicWireOutput? output, out string reason)
    {
        final = null; output = null; reason = "relicFuseStoreOwnerUnavailable";
        if (!ReferenceEquals(context.Sessions, this) || !ReferenceEquals(context.Session, expected) ||
            string.IsNullOrWhiteSpace(context.PlayerItemsRoot) || !Path.IsPathFullyQualified(context.PlayerItemsRoot)) return false;
        var connection = context.Connection;
        RefuseBoundSpellReentry(connection);
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected, candidate);
        if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
        lock (gate)
        {
            using var native = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                holder.SpellBinding is not { } binding || !BindingIsCurrentPlain(binding, holder, expected) ||
                !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, candidate)) return false;
            if (!evidence.TryReadRetainedAllocation(out var preparedUid, out _, out _)) return false;
            var preparedOutput = PrepareRelicWire(context, holder, binding, spellWrite, evidence, null,
                evidence.CopyPreparedFrames(preparedUid));
            using var publication = new PreparedSessionSpellPublication(this, connection, holder, expected, candidate);
            publication.ArmFinalProof();
            if (!publication.ConsumeBeforeWriterPlain()) throw InvalidSpellPublication();
            bool saved;
            try
            {
                lock (LegacySecondaryPasswordRuntime.SyncRoot)
                {
                    if (!evidence.ConsumeInputProof()) { reason = "relicFuseInputProofChanged"; return false; }
                    saved = spellWrite.TryCommitRelicFuse(context.PlayerItemsRoot, changes, evidence, interruption, out reason);
                }
            }
            catch (LegacyCommittedSpellPublicationException failure)
            { FailCommittedSpellPublication(holder, publication.Binding, failure); throw; }
            if (!saved) return false;
            var stage = RelicPublicationStage.WriterReceipt;
            try
            {
                if (!spellWrite.HasCommittedRelicFuse(evidence)) throw InvalidSpellPublication();
                stage = RelicPublicationStage.ExternalImage;
                if (!publication.AcceptExternallyPersistedHitpoints()) throw InvalidSpellPublication();
                stage = RelicPublicationStage.CurrentPublication;
                if (!publication.IsCurrentPlain()) throw InvalidSpellPublication();
                stage = RelicPublicationStage.Native;
                publication.PublishNative();
                stage = RelicPublicationStage.Holder;
                publication.PublishHolder();
                stage = RelicPublicationStage.Binding;
                if (!BindingIsCurrentPlain(binding, holder, publication.FinalSession)) throw InvalidSpellPublication();
                stage = RelicPublicationStage.Evidence;
                if (!evidence.ConfirmPublishedHeld(publication.FinalSession, binding, spellWrite)) throw InvalidSpellPublication();
                stage = RelicPublicationStage.Wire;
                PublishRelicWire(preparedOutput, publication.FinalSession);
                output = preparedOutput; final = publication.FinalSession; reason = string.Empty; return true;
            }
            catch (Exception failure)
            {
                FailRelicPublication(context, holder, publication.Binding, returned: false, stage, failure);
                throw;
            }
        }
    }
}
