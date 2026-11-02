using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCommitOwnedRelicDecompose(WorldPacketHandlerContext context, WorldSession expected,
        LegacyPreparedSpellFileWrite writer, LegacyRelicDecomposeOwnedEvidence evidence,
        WorldPacketHandlers.ItemSpellBeforeImages before, WorldPacketHandlers.LegacySpellMutationInputs inputs,
        LegacyJournalInterruption? interruption, out WorldSession? final, out RelicWireOutput? output, out string reason)
    {
        final = null; output = null; reason = "relicDecomposeStoreOwnerUnavailable";
        if (!ReferenceEquals(context.Sessions, this) || !ReferenceEquals(context.Session, expected) ||
            string.IsNullOrWhiteSpace(context.PlayerItemsRoot) || !Path.IsPathFullyQualified(context.PlayerItemsRoot)) return false;
        RefuseBoundSpellReentry(context.Connection);
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
        if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
        lock (gate)
        {
            using var native = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                holder.SpellBinding is not { } binding || !BindingIsCurrentPlain(binding, holder, expected) ||
                !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, expected)) return false;
            using var actors = new SpellActorScope(binding);
            // Pre-enter all native storage gates in their existing fixed order.
            // The registry callback can only reenter them, never wait on a new gate.
            using var physical = binding.Items.EnterCurrentReadScope();
            if (!binding.Items.IsCurrentPlain()) return false;
            var state = new RelicDecomposePreparationState(context, holder, binding, writer, before, inputs);
            relicDecomposePreparations.Add(evidence, state);
            Exception? primary = null;
            bool succeeded = false;
            try
            {
                bool accepted = SnapshotFileLockCoordinator.ExecuteLocked(evidence.FilePaths,
                    () => CommitRelicDecomposeHeld(state, evidence, interruption));
                final = state.Final; output = accepted ? state.Output : null; reason = state.Reason;
                succeeded = accepted; return accepted;
            }
            catch (Exception failure) { primary = failure; throw; }
            finally
            {
                state.Active = false;
                relicDecomposePreparations.Remove(evidence);
                if (!succeeded && state.Output is { } unissued) AbandonPreparedRelicOutput(unissued);
                try { state.Publication?.Dispose(); }
                catch (Exception cleanup)
                {
                    Exception retained = primary is null ? cleanup : new AggregateException(primary, cleanup);
                    if (writer.HasCommittedRelicDecompose(evidence) ||
                        primary is LegacyCommittedSpellPublicationException or WorldSessionPublicationException)
                    {
                        if (state.Output is { } cleanupOutput) AbandonPreparedRelicOutput(cleanupOutput);
                        FailCommittedSpellPublication(state.Holder, state.Binding,
                            new LegacyCommittedSpellPublicationException(retained));
                    }
                    if (primary is not null) throw retained;
                    throw;
                }
            }
        }
    }

    private bool CommitRelicDecomposeHeld(RelicDecomposePreparationState state,
        LegacyRelicDecomposeOwnedEvidence evidence, LegacyJournalInterruption? interruption)
    {
        if (!evidence.TryReserveOnce(out state.Reason) || !evidence.HasCurrentAllocation() ||
            state.Publication is not { } publication || state.Changes is not { Length: 3 } changes || state.Output is null) return false;
        if (!publication.ConsumeBeforeWriterPlain()) throw InvalidSpellPublication();
        bool saved;
        try
        {
            lock (LegacySecondaryPasswordRuntime.SyncRoot)
            {
                if (!evidence.ConsumeInputProof()) { state.Reason = "relicDecomposeInputProofChanged"; return false; }
                saved = state.Writer.TryCommitRelicDecompose(state.Context.PlayerItemsRoot!, changes, evidence, interruption, out state.Reason);
            }
        }
        catch (LegacyCommittedSpellPublicationException failure)
        { FailCommittedSpellPublication(state.Holder, publication.Binding, failure); throw; }
        if (!saved) return false;
        var stage = RelicPublicationStage.WriterReceipt;
        try
        {
            if (!state.Writer.HasCommittedRelicDecompose(evidence)) throw InvalidSpellPublication();
            stage = RelicPublicationStage.ExternalImage;
            if (!publication.AcceptExternallyPersistedHitpoints()) throw InvalidSpellPublication();
            stage = RelicPublicationStage.CurrentPublication;
            if (!publication.IsCurrentPlain()) throw InvalidSpellPublication();
            stage = RelicPublicationStage.Native; publication.PublishNative();
            stage = RelicPublicationStage.Holder; publication.PublishHolder();
            stage = RelicPublicationStage.Binding;
            if (!BindingIsCurrentPlain(state.Binding, state.Holder, publication.FinalSession)) throw InvalidSpellPublication();
            stage = RelicPublicationStage.Ranking;
            evidence.ThrowBeforeRankingIfPlanned();
            if (!TryPublishRelicDecomposeRankingHeld(state, publication.FinalSession)) throw InvalidSpellPublication();
            stage = RelicPublicationStage.Evidence;
            if (!evidence.ConfirmPublishedHeld(publication.FinalSession, state.Binding, state.Writer)) throw InvalidSpellPublication();
            stage = RelicPublicationStage.Wire; PublishRelicWire(state.Output, publication.FinalSession);
            state.Final = publication.FinalSession; state.Reason = string.Empty; return true;
        }
        catch (Exception failure)
        {
            try { FailCommittedSpellPublication(state.Holder, publication.Binding, new LegacyCommittedSpellPublicationException(failure)); }
            catch (LegacyCommittedSpellPublicationException fenced)
            {
                try { state.Context.Log.WriteLine("WorldServer committed relic publication failure operation=decompose stage=" + stage +
                    " exceptionType=" + failure.GetType().Name + "."); }
                catch (Exception diagnostic) { throw new LegacyCommittedSpellPublicationException(new AggregateException(fenced, diagnostic)); }
                throw;
            }
            throw;
        }
    }
}
