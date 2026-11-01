namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool IsDerivedSpellQueryWriteOwnerCurrent(WorldServerFrameHandler frame, WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding, WorldSession expected) =>
        ReferenceEquals(framePersistenceOwner, frame) && frame.IsActionLifecycleCurrent(this, context) &&
        ReferenceEquals(context.Connection, binding.Connection) && ReferenceEquals(binding.Store, this) &&
        IsCurrentThreadInsideAnyConnectionGate && _connectionGates.TryGetValue(binding.Connection, out var gate) && Monitor.IsEntered(gate) &&
        Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
        _sessions.TryGetValue(binding.Connection, out var holder) && ReferenceEquals(holder.SpellBinding, binding) &&
        BindingIsCurrentPlain(binding, holder, expected);

    internal bool TryCommitOwnedDerivedSpellQuery(WorldPacketHandlerContext context,
        WorldPacketHandlers.OwnedDerivedSpellQueryPreparation preparation, out DerivedSpellQueryPublication? receipt, out string reason)
    {
        receipt = null; reason = "derivedSpellQuery23OwnerUnavailable";
        RefuseBoundSpellReentry(context.Connection);
        var expected = preparation.Expected;
        if (framePersistenceOwner is not { } frame || !frame.TryPrepareActionContext(this, context, expected, out var actual)) return false;
        context = actual;
        WorldSession final; LegacyOrdinarySpellBinding binding; object identity; DerivedSpellQueryPublication output;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || holder.SpellBinding is not { } current ||
                    !BindingIsCurrentPlain(current, holder, expected)) return false;
                binding = current;
                using var actors = new SpellActorScope(binding);
                LegacyPreparedDerivedSpellQueryWrite? writer = null;
                try
                {
                    writer = LegacyPreparedDerivedSpellQueryWrite.Prepare(frame, this, context, binding, preparation);
                    using var publication = new PreparedSessionSpellPublication(this, context.Connection, holder, expected,
                        writer.Proposed, derivedSpellQueryPublication: writer.PreparePublication());
                    final = publication.FinalSession; identity = publication.PublishedIdentity;
                    writer.PrepareFinal(final);
                    output = new(this, context, frame, binding, writer, final, publication.PreparedSpellGraph);
                    publication.ArmFinalProof();
                    if (!preparation.IsCurrentPlain(binding) || !publication.ConsumeBeforeWriterPlain()) throw InvalidSpellPublication();
                    var outcome = writer.TryCommit(out reason);
                    if (outcome == LegacyPreparedDerivedSpellQueryWrite.Outcome.Rejected) return false;
                    if (outcome == LegacyPreparedDerivedSpellQueryWrite.Outcome.RecoveryRequired)
                    { binding.Recovery.Block("derivedSpellQuery23RecoveryRequired:" + reason); holder.Clear(); return false; }
                    if (!publication.AcceptExternallyPersistedHitpoints() || !publication.IsCurrentPlain())
                        FailCommittedSpellPublication(holder, binding);
                    publication.PublishNative(); publication.PublishHolder(); writer.RegisterPublished(final);
                    publication.Dispose();
                    PersistFrameOwnedSessionChange(context.Connection, expected, final, identity, binding, completedDerivedSpells: writer);
                    RecordDerivedContinuationRetirement(context, binding, expected, final, writer);
                }
                catch (Exception error)
                {
                    if (LegacyTradeJournal.HasPending(binding.UsersRoot) || writer?.HasCommitted == true)
                    {
                        try { binding.Recovery.Block("derivedSpellQuery23RecoveryRequired:" + error.GetType().Name); }
                        finally { holder.Clear(); }
                        throw;
                    }
                    if (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or
                        FormatException or OverflowException or System.Security.SecurityException)
                    { reason = "derivedSpellQuery23Preparation:" + error.GetType().Name; return false; }
                    throw;
                }
            }
        }
        NotifyPublishedSessionChange(context.Connection, expected, final, identity, binding);
        receipt = output; reason = string.Empty; return true;
    }
}
