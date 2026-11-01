using WolfConquer.Core.Networking;

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
        if (context.Sessions is not { } store || !ReferenceEquals(context.Connection, execution.Binding.Connection))
            throw new InvalidOperationException("experienceContinuationDriverOwnerInvalid");
        WorldSession tracked = execution.Current;
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
                        await SendContinuationExperienceAsync(context.Connection, cleared, clearReceipt, cancellationToken).ConfigureAwait(false);
                        return cleared;
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
                if (!WorldSessionStore.ExperienceContinuationExecution.TryBeginCompletedQuery(store, context, completed, out var following) || following is null)
                    throw new InvalidOperationException("experienceContinuationNextOwnerUnavailable");
                execution = following; tracked = execution.Current;
            }
        }
        catch (Exception error)
        {
            execution.FenceFailure(error);
            throw;
        }
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
