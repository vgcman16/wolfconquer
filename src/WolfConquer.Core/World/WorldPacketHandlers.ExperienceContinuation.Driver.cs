using WolfConquer.Core.Networking;
using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Entry for the existing private combat producer after real issuance of
    // its registered award purpose. The mutable source route remains
    // unqualified until the marker and admission fences are reviewed together.
    private static async ValueTask<WorldSession> CreditAndContinueExperienceAsync(WorldPacketHandlerContext context,
        ConquerTcpConnection recipient, WorldSession expected, ulong gain, OwnedCombatExperience purpose,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } store) return expected;
        if (!store.TryCreditExperienceContinuation(context, recipient, expected, gain, purpose,
                out var credited, out _, out var creditReceipt, out var reason) || credited is null || creditReceipt is null)
        {
            await context.Log.WriteLineAsync($"WorldServer refused combat experience continuation reason={reason}.").ConfigureAwait(false);
            return expected;
        }
        var recipientContext = context with { Connection = recipient, Session = credited };
        WorldSessionStore.ExperienceContinuationExecution? execution;
        try
        {
            if (!WorldSessionStore.ExperienceContinuationExecution.TryBeginCredit(store, recipientContext, creditReceipt, out execution) || execution is null)
                throw new InvalidOperationException("experienceContinuationAfterCreditOwnerUnavailable");
        }
        catch (Exception error) { creditReceipt.FenceFailure(error); throw; }
        return await RunExperienceContinuationAsync(recipientContext, execution, cancellationToken).ConfigureAwait(false);
    }

    // Entry requires the actual completed admission-startup query successor.
    // The draft login route invokes it after required startup and before workers.
    internal static async ValueTask<WorldSession?> ResumeAfterAdmissionQueryAsync(WorldPacketHandlerContext context,
        CompletedAdmissionContinuationQuery startup, CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } store ||
            !WorldSessionStore.ExperienceContinuationExecution.TryBeginAdmissionQuery(store, context, startup, out var execution) || execution is null)
            return null;
        return await RunExperienceContinuationAsync(context, execution, cancellationToken).ConfigureAwait(false);
    }

    // The input is a genuine credit/admission execution; this method cannot
    // issue a credit or accept a caller-supplied state. Cold login and query
    // joins remain unqualified until their integrated checks pass.
    private static async ValueTask<WorldSession> RunExperienceContinuationAsync(WorldPacketHandlerContext context,
        WorldSessionStore.ExperienceContinuationExecution execution, CancellationToken cancellationToken)
    {
        var completed = await RunExperienceContinuationWithReceiptAsync(context, execution, cancellationToken).ConfigureAwait(false);
        return completed.Session;
    }

    private static async ValueTask<CompletedExperienceContinuation> RunExperienceContinuationWithReceiptAsync(
        WorldPacketHandlerContext context, WorldSessionStore.ExperienceContinuationExecution execution,
        CancellationToken cancellationToken, WorldSessionStore.OwnedDragonhowlExperienceAward? procAward = null)
    {
        if (context.Sessions is not { } store || !ReferenceEquals(context.Connection, execution.Binding.Connection))
            throw new InvalidOperationException("experienceContinuationDriverOwnerInvalid");
        var originalExecution = execution;
        WorldSession tracked = execution.Current;
        CompletedContinuationQuery? lastQuery = null;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PreparedLevelUpRebuild? rebuild;
                if (execution.Pending.Phase == LegacyExperienceContinuationPhase.Ready)
                {
                    if (execution.TryClear(out var cleared, out var clearReceipt, out _) && cleared is not null && clearReceipt is not null)
                    {
                        tracked = cleared;
                        var completionState = new CompletedExperienceContinuationState(originalExecution, execution, context,
                            execution.Current, cleared, clearReceipt, lastQuery);
                        var terminalReceipt = new CompletedExperienceContinuation();
                        CompletedExperienceContinuations.Add(terminalReceipt, completionState);
                        if (procAward is not null)
                            store.ConfirmDragonhowlExperienceContinuation(procAward, originalExecution, terminalReceipt);
                        await SendContinuationExperienceAsync(context.Connection, cleared, clearReceipt, cancellationToken).ConfigureAwait(false);
                        if (!completionState.IsCurrent())
                            throw new InvalidOperationException("experienceContinuationFinalReceiptOwnerChanged");
                        if (!terminalReceipt.TryResolve(originalExecution, out var final) || !ReferenceEquals(final, cleared))
                            throw new InvalidOperationException("experienceContinuationFinalReceiptOwnerChanged");
                        return terminalReceipt;
                    }
                    var beforeStep = execution.Current;
                    if (!execution.TryCommitNextStep(out var stepped, out _, out var stepReceipt, out var query,
                            out var progression, out var stepReason) || stepped is null || stepReceipt is null || query is null)
                        throw new InvalidOperationException("experienceContinuationStepRefused:" + stepReason);
                    tracked = stepped; rebuild = query;
                    var frames = PrepareCombatExperienceFrames(new LegacyCombatExperienceProjection(
                        beforeStep.Character, stepped.Character, 0, true, progression));
                    if (frames.Length < 2) throw new InvalidOperationException("experienceContinuationStepFramesInvalid");
                    for (int i = 0; i < frames.Length - 1; i++)
                    {
                        if (!stepReceipt.TryCurrent(out var live) || !ReferenceEquals(live, stepped))
                            throw new InvalidOperationException("experienceContinuationStepOutputOwnerChanged");
                        await context.Connection.SendAsync(frames[i], cancellationToken).ConfigureAwait(false);
                        if (!stepReceipt.TryCurrent(out live) || !ReferenceEquals(live, stepped))
                            throw new InvalidOperationException("experienceContinuationStepOutputOwnerChanged");
                    }
                }
                else if (!execution.TryPreparePendingQuery(out rebuild) || rebuild is null)
                    throw new InvalidOperationException("experienceContinuationResumeQueryUnavailable");

                var completed = await rebuild.RunContinuationQueryAsync(cancellationToken).ConfigureAwait(false);
                if (completed is null || !completed.TryCurrent(out tracked))
                    throw new InvalidOperationException("experienceContinuationQueryUnproven");
                if (!store.TryMarkExperienceQueryComplete(context, completed, out var queryCheckpoint, out var queryReason) ||
                    queryCheckpoint is null || !completed.TryAdvance(queryCheckpoint) || !completed.TryCurrent(out tracked))
                    throw new InvalidOperationException("experienceContinuationQueryCheckpointRefused:" + queryReason);
                if (!store.TryRestoreExperienceHitpoints(context, completed, out var restoreCheckpoint, out var restoreReason) ||
                    restoreCheckpoint is null || !completed.TryAdvance(restoreCheckpoint) || !completed.TryCurrent(out tracked))
                    throw new InvalidOperationException("experienceContinuationRestoreRefused:" + restoreReason);
                if (!await completed.PublishRestoredHitpointsAsync(cancellationToken).ConfigureAwait(false) ||
                    !completed.TryCurrent(out tracked)) throw new InvalidOperationException("experienceContinuationRestoreOutputUnproven");
                lastQuery = completed;
                if (!WorldSessionStore.ExperienceContinuationExecution.TryBeginCompletedQuery(store, context, completed, out var following) || following is null)
                    throw new InvalidOperationException("experienceContinuationNextOwnerUnavailable");
                if (procAward is not null && !store.BindDragonhowlExperienceReady(procAward, originalExecution, following, completed))
                    throw new InvalidOperationException("ownedDragonhowlExperienceReadySuccessorChanged");
                execution = following; tracked = execution.Current;
            }
        }
        catch (Exception error)
        {
            execution.FenceFailure(error);
            throw;
        }
    }

    // Only the actual final-clear branch above registers a completion. The
    // object alone cannot grant XP or select a new holder after the query.
    internal sealed partial class CompletedExperienceContinuation
    {
        internal WorldSession Session => CompletedExperienceContinuations.TryGetValue(this, out var actual)
            ? actual.Session : throw new InvalidOperationException("experienceContinuationCompletionNotIssued");

        internal bool TryResolve(WorldSessionStore.ExperienceContinuationExecution originalExecution,
            out WorldSession session)
        {
            session = null!;
            if (!CompletedExperienceContinuations.TryGetValue(this, out var actual) ||
                !ReferenceEquals(actual.Original, originalExecution) || !actual.IsCurrent()) return false;
            session = actual.Session;
            return true;
        }

        internal bool TryResolveHeld(WorldSessionStore.ExperienceContinuationExecution originalExecution,
            out WorldSession session)
        {
            session = null!;
            if (!CompletedExperienceContinuations.TryGetValue(this, out var actual) ||
                !ReferenceEquals(actual.Original, originalExecution) || !actual.IsHeldCurrentPlain()) return false;
            session = actual.Session;
            return true;
        }

        // The proc may retain this exact query lineage after its own later
        // committed phases. The caller must independently prove those phases;
        // this method never finds or adopts a holder by UID.
        internal bool HasCurrentQueryMaximums(LegacyOrdinarySpellBinding binding, WorldSession current) =>
            CompletedExperienceContinuations.TryGetValue(this, out var actual) &&
            actual.HasCurrentQueryMaximums(binding, current);

        internal bool MatchesCompletedReceiveData(LegacyOrdinarySpellBinding binding, WorldSession current,
            DragonhowlLevelExperienceData expected) =>
            CompletedExperienceContinuations.TryGetValue(this, out var actual) &&
            actual.MatchesCompletedReceiveData(binding, current, expected);
    }

    private static readonly ConditionalWeakTable<CompletedExperienceContinuation, CompletedExperienceContinuationState>
        CompletedExperienceContinuations = new();

    private sealed partial class CompletedExperienceContinuationState
    {
        internal readonly WorldSessionStore.ExperienceContinuationExecution Original;
        internal readonly WorldSession Session;
        private readonly WorldPacketHandlerContext driverContext, operationContext;
        private readonly WorldSessionStore.ActionPublication clearReceipt;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativeAttributeMapProof attributes, paid;
        private readonly LegacyNativeMagicQueryClient.CompletedMaximums? maximums;
        private readonly WorldSessionStore.ExperienceContinuationExecution clearOwner;

        internal CompletedExperienceContinuationState(WorldSessionStore.ExperienceContinuationExecution original,
            WorldSessionStore.ExperienceContinuationExecution clearOwner,
            WorldPacketHandlerContext context, WorldSession ready, WorldSession session,
            WorldSessionStore.ActionPublication clearReceipt, CompletedContinuationQuery? lastQuery)
        {
            Original = original; Session = session; driverContext = context; this.clearOwner = clearOwner;
            operationContext = original.OperationContext; binding = original.Binding; this.clearReceipt = clearReceipt;
            attributes = LegacyNativeAttributeMapProof.CaptureRaw(session.Character.Attributes);
            paid = LegacyNativeAttributeMapProof.CaptureRaw(session.Character.PaidNobility);
            if (lastQuery is not null &&
                (!lastQuery.TryReadTerminalMaximums(ready, session, out maximums) || maximums is null))
                throw new InvalidOperationException("experienceContinuationFinalQueryLineageChanged");
        }

        internal bool IsCurrent()
        {
            // ActionPublication owns lifecycle -> package -> connection ->
            // native -> actors. Do not enter it underneath any caller's gate.
            if (WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !HasExactLineageContext() || !attributes.IsCurrentPlain() || !paid.IsCurrentPlain() ||
                !clearOwner.IsTerminalClearCurrent(Session, clearReceipt) ||
                !clearReceipt.TryCurrent(out var current) || !ReferenceEquals(current, Session) ||
                !binding.Store.IsLevelUpRebuildQueryOwnerCurrent(binding, Session, operationContext, maximums)) return false;
            try
            {
                return LegacyExperienceContinuationCodec.ReadCharacter(Session.Character, Session.AccountId) is null &&
                    attributes.IsCurrentPlain() && paid.IsCurrentPlain() &&
                    clearReceipt.TryCurrent(out var after) && ReferenceEquals(after, Session);
            }
            catch (Exception error) when (error is InvalidOperationException or InvalidDataException or ArgumentException or OverflowException)
            { return false; }
        }

        internal bool IsHeldCurrentPlain()
        {
            if (!HasExactLineageContext() || !attributes.IsCurrentPlain() || !paid.IsCurrentPlain() ||
                !clearOwner.IsTerminalClearCurrent(Session, clearReceipt) ||
                !binding.Store.IsLevelUpRebuildOwnerHeld(binding, Session, operationContext) ||
                maximums is not null && !maximums.IsCurrentPlain(binding, Session)) return false;
            var character = Session.Character;
            var player = binding.Player;
            if (player.Level != character.Level || character.Experience < 0 || player.Experience != (ulong)character.Experience ||
                player.Strength != character.Strength || player.Agility != character.Agility ||
                player.Vitality != character.Vitality || player.Spirit != character.Spirit ||
                player.Atributes != character.AttributePoints || player.HitPoints != character.MinHitPoints ||
                player.Mana != character.MinMana) return false;
            try
            {
                return LegacyExperienceContinuationCodec.ReadCharacter(character, Session.AccountId) is null &&
                    attributes.IsCurrentPlain() && paid.IsCurrentPlain();
            }
            catch (Exception error) when (error is InvalidOperationException or InvalidDataException or ArgumentException or OverflowException)
            { return false; }
        }

        internal bool HasCurrentQueryMaximums(LegacyOrdinarySpellBinding owner, WorldSession current) =>
            ReferenceEquals(owner, binding) && HasExactLineageContext() && maximums is not null &&
            attributes.IsCurrentPlain() && paid.IsCurrentPlain() &&
            binding.Store.IsLevelUpRebuildOwnerHeld(binding, current, operationContext) &&
            maximums.IsCurrentPlain(binding, current);

        internal bool MatchesCompletedReceiveData(LegacyOrdinarySpellBinding owner, WorldSession current,
            DragonhowlLevelExperienceData expected) =>
            HasCurrentQueryMaximums(owner, current) && maximums is not null &&
            expected.MatchesCompletedQuery(owner, current, maximums);

        private bool HasExactLineageContext() => ReferenceEquals(Original.OperationContext, operationContext) &&
            ReferenceEquals(Original.Binding, binding) && ReferenceEquals(driverContext.Sessions, binding.Store) &&
            ReferenceEquals(operationContext.Sessions, binding.Store) &&
            ReferenceEquals(driverContext.Connection, binding.Connection) && ReferenceEquals(operationContext.Connection, binding.Connection) &&
            ReferenceEquals(driverContext.Trades, operationContext.Trades) &&
            ReferenceEquals(driverContext.TradeRecovery, operationContext.TradeRecovery) &&
            ReferenceEquals(driverContext.WorldData, operationContext.WorldData) &&
            driverContext.ProtocolProfile == operationContext.ProtocolProfile &&
            ReferenceEquals(driverContext.TradeOperations, operationContext.TradeOperations) &&
            ReferenceEquals(driverContext.FrameOperationLease, operationContext.FrameOperationLease) &&
            operationContext.TradeOperations?.OwnsActiveExclusiveLease(operationContext.FrameOperationLease) == true &&
            ReferenceEquals(clearReceipt.Published, Session);
    }

    private static async ValueTask SendContinuationExperienceAsync(ConquerTcpConnection connection, WorldSession session,
        WorldSessionStore.ActionPublication receipt, CancellationToken cancellationToken)
    {
        if (!receipt.TryCurrent(out var before) || !ReferenceEquals(before, session) ||
            LegacyExperienceContinuationCodec.ReadCharacter(session.Character, session.AccountId) is not null)
            throw new InvalidOperationException("experienceContinuationFinalOwnerChanged");
        var packet = LegacyMsgUpdatePacket.CreateLegacyRuntimeExperienceUpdate(checked((uint)session.Character.Uid),
            checked((ulong)session.Character.Experience));
        await connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        if (!receipt.TryCurrent(out var after) || !ReferenceEquals(after, session))
            throw new InvalidOperationException("experienceContinuationFinalOwnerChanged");
    }
}
