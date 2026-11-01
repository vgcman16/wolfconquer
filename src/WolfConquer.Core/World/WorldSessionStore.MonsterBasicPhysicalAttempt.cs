using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Actual actor owner gates precede the monster store's short witness read.
    internal bool TryBeginMonsterPhysical(WorldPacketHandlerContext context, MsgAttackPacket packet,
        LegacyMonsterInstance monster, Monster family, out WorldServerFrameHandler.MonsterPhysicalAttempt? attempt)
    {
        attempt = null;
        RefuseBoundSpellReentry(context.Connection);
        if (framePersistenceOwner is not { } frame || context.Session is not { } expected ||
            !frame.TryPrepareActionContext(this, context, expected, out var actual)) return false;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    holder.SpellBinding is not { } actor || !BindingIsCurrentPlain(actor, holder, expected)) return false;
                using var actors = new SpellActorScope(actor);
                if (!BindingIsCurrentPlain(actor, holder, expected)) return false;
                return frame.TryBeginMonsterPhysical(actual, packet, monster, family, out attempt);
            }
        }
    }

    internal bool TryCommitMonsterPhysicalDamage(WorldPacketHandlerContext context,
        WorldServerFrameHandler.MonsterPhysicalAttempt attempt, LegacyMonsterInstance selected,
        uint requestedDamage, DateTimeOffset now, out MonsterDamageResult result)
    {
        result = default;
        RefuseBoundSpellReentry(context.Connection);
        if (framePersistenceOwner is not { } frame || context.Session is not { } expected ||
            !frame.TryPrepareActionContext(this, context, expected, out var actual)) return false;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    holder.SpellBinding is not { } actor || !BindingIsCurrentPlain(actor, holder, expected)) return false;
                using var actors = new SpellActorScope(actor);
                bool damaged = false;
                try
                {
                    if (!frame.TryReadMonsterPhysicalAttempt(attempt, actor, expected, out var target, out var before,
                            out var generation, out var stamp) || target is null || before is null) return false;
                    var change = IssueBasicPhysicalChange(frame, actor, expected, before, generation,
                        LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.CommittedHit, null, stamp, actual);
                    var native = LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication.Prepare(actor, change);
                    using var scope = target.TryEnterDamage();
                    if (scope is null || !scope.TryReadCurrent(out var current, out _, out _) ||
                        current != selected || !native.IsCurrentPlain() || !BindingIsCurrentPlain(actor, holder, expected)) return false;
                    string name = !string.IsNullOrWhiteSpace(expected.Character.Name) ? expected.Character.Name : expected.Username;
                    result = string.IsNullOrWhiteSpace(name)
                        ? frame.MonsterStatesForPhysicalDamage.ApplyDamage(current, requestedDamage, now)
                        : frame.MonsterStatesForPhysicalDamage.ApplyDamage(current, requestedDamage, now,
                            checked((uint)expected.Character.Uid), name);
                    if (result.AlreadyDead) return false;
                    damaged = true;
                    if (!frame.PublishMonsterPhysicalStamp(attempt, actor, expected, native))
                        throw new InvalidOperationException("monsterPhysicalStampPublicationLost");
                    return true;
                }
                catch (Exception error) when (damaged)
                {
                    var failures = new List<Exception> { error };
                    try { actor.Recovery.Block("monsterPhysicalCommittedOutcomeUnproven"); } catch (Exception fence) { failures.Add(fence); }
                    try { FenceBasicPhysicalBinding(actor); } catch (Exception fence) { failures.Add(fence); }
                    if (failures.Count != 1) throw new AggregateException("Monster physical hit and fencing failed.", failures);
                    throw;
                }
                finally { if (!damaged) frame.AbandonMonsterPhysicalAttempt(attempt); }
            }
        }
    }

    internal bool TryArmCompletedMonsterPhysical(WorldPacketHandlers.CompletedMonsterPhysicalHit receipt)
    {
        if (!WorldPacketHandlers.TryReadCompletedMonsterPhysicalHit(receipt, this, out var context, out var attempt, out var expected) ||
            framePersistenceOwner is not { } frame ||
            !frame.TryPrepareActionContext(this, context with { Session = expected }, expected, out var actual)) return false;
        LegacyOrdinarySpellBinding? armedActor = null;
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? armed = null;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    holder.SpellBinding is not { } actor || !BindingIsCurrentPlain(actor, holder, expected)) return false;
                using var actors = new SpellActorScope(actor);
                try
                {
                    if (!actor.Player.Alive || !frame.TryReadMonsterArm(attempt, actor, out var target, out var before,
                            out var generation, out var query) || target is null || before is null) return false;
                    using var scope = target.TryEnterDamage();
                    if (scope is null || !scope.TryReadCurrent(out _, out _, out _) ||
                        !BindingIsCurrentPlain(actor, holder, expected)) return false;
                    var change = IssueBasicPhysicalChange(frame, actor, expected, before, generation,
                        LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.ArmRepeat, query, null, actual);
                    var native = LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication.Prepare(actor, change);
                    if (!frame.PublishMonsterArm(attempt, actor, change, native)) return false;
                    armedActor = actor; armed = native.After;
                }
                finally { frame.AbandonMonsterPhysicalAttempt(attempt); }
            }
        }
        if (armedActor is null || armed is null) return false;
        frame.NotifyActualBasicPhysicalArm(this, armedActor, armed);
        return true;
    }

    internal bool IsMonsterPhysicalOutputCurrent(WorldPacketHandlerContext context,
        WorldServerFrameHandler.MonsterPhysicalAttempt attempt, WorldSession expected)
    {
        if (framePersistenceOwner is not { } frame ||
            context.TradeOperations is not { } operation || !operation.OwnsActiveExclusiveLease(context.FrameOperationLease) ||
            !frame.TryPrepareActionContext(this, context with { Session = expected }, expected, out _)) return false;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    holder.SpellBinding is not { } actor || !BindingIsCurrentPlain(actor, holder, expected)) return false;
                using var actors = new SpellActorScope(actor);
                return BindingIsCurrentPlain(actor, holder, expected) && frame.IsMonsterCommittedAttemptCurrent(attempt, actor);
            }
        }
    }

    internal Exception? FinishMonsterPhysical(WorldPacketHandlerContext context,
        WorldServerFrameHandler.MonsterPhysicalAttempt attempt, Exception? failure)
    {
        if (framePersistenceOwner is not { } frame) return failure;
        var errors = new List<Exception>();
        if (failure is not null) errors.Add(failure);
        try
        {
            lock (frame.RequireActionLifecycleGate(this))
            {
                if (!frame.TryGetMonsterPhysicalAttemptActor(attempt, out var actor) ||
                    !ReferenceEquals(actor.Connection, context.Connection)) return failure;
                // A continuation may already have retired this exact actor at
                // a proven durable checkpoint for this same propagated error.
                // This is a historical, one-use proof, not a fresh retirement.
                if (failure is not null && TryConsumeSafelyRetiredContinuationFailure(context, actor, failure))
                {
                    frame.AbandonMonsterPhysicalAttempt(attempt);
                    return failure;
                }
                // The hit has committed. Fence recovery even if a concurrent
                // retirement removed the connection gate before cleanup.
                if (failure is not null)
                    try { actor.Recovery.Block("monsterPhysicalCommittedOutputFailed"); } catch (Exception error) { errors.Add(error); }
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, PeekMailboxSession(context.Connection));
                if (_connectionGates.TryGetValue(context.Connection, out var gate))
                {
                    lock (gate)
                    {
                        using var nativeGate = EnterNativeConnectionGate();
                        using var actors = new SpellActorScope(actor);
                        if (failure is not null)
                            try { FenceBasicPhysicalBinding(actor); } catch (Exception error) { errors.Add(error); }
                        else frame.AbandonMonsterPhysicalAttempt(attempt);
                    }
                }
                else if (failure is not null)
                    try { actor.Retire(); } catch (Exception error) { errors.Add(error); }
            }
        }
        catch (Exception error) { errors.Add(error); }
        return errors.Count switch { 0 => null, 1 => errors[0], _ => new AggregateException("Monster physical hit and fencing failed.", errors) };
    }
}
