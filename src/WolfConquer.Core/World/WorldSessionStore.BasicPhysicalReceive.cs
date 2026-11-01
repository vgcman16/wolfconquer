using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class BasicPhysicalHitReceipt { }
    private sealed class BasicPhysicalHitCommit(WorldServerFrameHandler.BasicPhysicalAttempt attempt,
        LegacyOrdinarySpellBinding attacker, LegacyOrdinarySpellBinding target)
    {
        internal readonly WorldServerFrameHandler.BasicPhysicalAttempt Attempt = attempt;
        internal readonly LegacyOrdinarySpellBinding Attacker = attacker, Target = target;
        internal WorldSession? Published;
        internal ProcReceipt? ActualPublication;
        internal BasicPhysicalPublication? OwnedPublication;
    }
    private readonly ConditionalWeakTable<BasicPhysicalHitReceipt, BasicPhysicalHitCommit> basicPhysicalHitCommits = new();
    private readonly ConditionalWeakTable<WorldPacketHandlers.BasicPhysicalReceiveDecision, BasicPhysicalHitReceipt> basicPhysicalDecisionCommits = new();

    internal bool TryCommitBasicPhysicalReceive(WorldPacketHandlerContext context,
        WorldPacketHandlers.BasicPhysicalReceiveDecision decision, WorldActiveSession target,
        out WorldSession? published)
    {
        published = null;
        if (framePersistenceOwner is not { } frame ||
            !WorldPacketHandlers.TryReadBasicPhysicalReceive(decision, out var attempt, out var kind,
                out var previous, out var damage, out var observed)) return false;
        lock (frame.RequireActionLifecycleGate(this))
        {
            if (basicPhysicalDecisionCommits.TryGetValue(decision, out _) ||
                !frame.TryGetBasicPhysicalAttemptRoles(context, attempt, out var attacker, out var victim) ||
                !ReferenceEquals(victim.Connection, target.Connection) ||
                !TryGet(attacker.Connection, out var currentAttacker) || currentAttacker is null ||
                !TryGet(victim.Connection, out var currentTarget) || currentTarget is null ||
                target.Session.Character.Uid != victim.Uid || !ReferenceEquals(target.Session.NativeGameClient, victim.Client)) return false;
            if (!WorldPacketHandlers.TryReadBasicPhysicalProjection(decision, out var capturedExpected, out var calculated) ||
                kind != WorldPacketHandlers.BasicPhysicalReceiveKind.NormalDeath && !ReferenceEquals(currentTarget, capturedExpected)) return false;
            long after;
            switch (kind)
            {
                case WorldPacketHandlers.BasicPhysicalReceiveKind.Observation:
                    if (currentTarget.Character.MinHitPoints != observed) return false;
                    after = observed;
                    break;
                case WorldPacketHandlers.BasicPhysicalReceiveKind.Nonlethal:
                    if (damage > previous || damage == previous || currentTarget.Character.MinHitPoints != previous) return false;
                    after = previous - damage;
                    break;
                case WorldPacketHandlers.BasicPhysicalReceiveKind.NormalDeath:
                    after = 0; // Only the actual normal-death HP assignment supplies this decision.
                    break;
                default: return false;
            }
            var proposed = kind == WorldPacketHandlers.BasicPhysicalReceiveKind.NormalDeath
                ? WorldPacketHandlers.ProjectAssignedLegacyHitpoints(context, currentTarget, victim.Uid, 0)
                : after == calculated.Character.MinHitPoints ? calculated :
                    calculated with { Character = calculated.Character with { MinHitPoints = after } };
            var receipt = new BasicPhysicalHitReceipt();
            var commit = new BasicPhysicalHitCommit(attempt, attacker, victim);
            // Inert registrations precede the real writer. An empty/prepared
            // token cannot pass TryReadCommittedBasicPhysicalHit.
            basicPhysicalHitCommits.Add(receipt, commit);
            basicPhysicalDecisionCommits.Add(decision, receipt);
            if (!TryCommitProc(new(victim.Connection, currentTarget, victim.NativeIdentity), proposed,
                    new ProcParticipant(attacker.Connection, currentAttacker, attacker.NativeIdentity), out var actual) || actual is null)
                return false;
            commit.Published = actual.Published;
            commit.ActualPublication = actual; // Actual writer/publication pointer is assigned LAST.
            published = actual.Published;
            if (!frame.RecordCommittedBasicPhysicalHit(this, receipt, attempt))
            {
                var failure = new WorldSessionPublicationException(this, victim.Connection, victim.NativeIdentity, victim,
                    actual.Published, WorldSessionPublicationStage.ProcContinuation,
                    new InvalidOperationException("basicPhysicalCommittedReceiveOwnerLost"));
                FenceObserverFailure(failure);
                throw failure;
            }
            return true;
        }
    }

    internal bool TryReadCommittedBasicPhysicalHit(BasicPhysicalHitReceipt receipt,
        WorldServerFrameHandler.BasicPhysicalAttempt attempt, out LegacyOrdinarySpellBinding attacker,
        out LegacyOrdinarySpellBinding target)
    {
        attacker = target = null!;
        if (!basicPhysicalHitCommits.TryGetValue(receipt, out var committed) ||
            !ReferenceEquals(committed.Attempt, attempt) ||
            committed.Published is not { } published || published.Character.Uid != committed.Target.Uid ||
            published.AccountId != committed.Target.AccountId || !ReferenceEquals(published.NativeGameClient, committed.Target.Client)) return false;
        bool actualWriter = committed.OwnedPublication is { } owned
            ? basicPhysicalPublications.TryGetValue(owned, out var joint) && joint.CommittedWriter?.IsCommitted == true &&
                ReferenceEquals(joint.ActorBinding, committed.Attacker) && ReferenceEquals(joint.TargetBinding, committed.Target) &&
                ReferenceEquals(joint.Target, published)
            : committed.ActualPublication is { } proc && ReferenceEquals(proc.Published, published);
        if (!actualWriter) return false;
        attacker = committed.Attacker; target = committed.Target;
        return true;
    }

    private BasicPhysicalHitReceipt PrepareOwnedBasicPhysicalHitReceipt(
        WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation plan, BasicPhysicalPublication publication)
    {
        if (!basicPhysicalPublications.TryGetValue(publication, out var actual) || !ReferenceEquals(actual.Plan, plan) ||
            !WorldPacketHandlers.TryReadBasicPhysicalReceive(plan.Decision, out var attempt, out _, out _, out _, out _) ||
            basicPhysicalDecisionCommits.TryGetValue(plan.Decision, out _)) throw InvalidSpellPublication();
        var receipt = new BasicPhysicalHitReceipt();
        basicPhysicalHitCommits.Add(receipt, new(attempt, plan.ActorBinding, plan.TargetBinding));
        basicPhysicalDecisionCommits.Add(plan.Decision, receipt);
        return receipt;
    }

    private void CommitOwnedBasicPhysicalHitReceipt(BasicPhysicalHitReceipt receipt, BasicPhysicalPublication publication,
        BasicPhysicalPublicationData actual)
    {
        if (!basicPhysicalHitCommits.TryGetValue(receipt, out var prepared) ||
            !basicPhysicalPublications.TryGetValue(publication, out var registered) || !ReferenceEquals(registered, actual) ||
            actual.CommittedWriter?.IsCommitted != true || !ReferenceEquals(prepared.Attacker, actual.ActorBinding) ||
            !ReferenceEquals(prepared.Target, actual.TargetBinding))
            throw FenceBasicPhysicalPersistence(actual, "basicPhysicalCommittedReceiptUnproven");
        prepared.Published = actual.Target;
        prepared.OwnedPublication = publication;
        if (!actual.Frame.RecordCommittedBasicPhysicalHit(this, receipt, prepared.Attempt))
            throw FenceBasicPhysicalPersistence(actual, "basicPhysicalCommittedGenerationUnproven");
    }
}
