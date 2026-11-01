namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private sealed record BasicPhysicalRepeat(LegacyOrdinarySpellBinding? Target,
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot Native, long Generation,
        MonsterRuntimeStateStore.OwnedMonsterRepeatTarget? Monster = null, DragonhowlPrimaryState? Dragonhowl = null);

    internal sealed class BasicPhysicalNativePublication
    {
        private readonly WorldServerFrameHandler frame;
        private readonly BasicPhysicalSlot slot;
        private readonly LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot before, after;
        private readonly long generation, nextGeneration;
        private readonly DateTime stamp;
        private readonly LegacyNativeGameClientRuntime.BasicPhysicalNativeKind kind;
        private readonly BasicPhysicalRepeat? repeat;
        private readonly BasicPhysicalAttemptState? hit;
        private readonly DragonhowlAttemptState? proc;

        private BasicPhysicalNativePublication(WorldServerFrameHandler frame, BasicPhysicalSlot slot,
            LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot before,
            LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot after,
            LegacyNativeGameClientRuntime.BasicPhysicalNativeKind kind, BasicPhysicalRepeat? repeat,
            BasicPhysicalAttemptState? hit, DragonhowlAttemptState? proc = null)
        {
            this.frame = frame; this.slot = slot; this.before = before; this.after = after; this.kind = kind; this.repeat = repeat;
            this.hit = hit;
            this.proc = proc;
            generation = slot.Generation; nextGeneration = checked(generation + 1); stamp = after.AttackStamp;
        }

        internal bool IsCurrentPlain() => Monitor.IsEntered(frame._tradeStates.SyncRoot) &&
            frame.IsStaminaReadinessCurrent(slot.Readiness) && ReferenceEquals(slot.Native, before) &&
            Volatile.Read(ref slot.Generation) == generation;

        internal static BasicPhysicalNativePublication Prepare(WorldServerFrameHandler frame, WorldSessionStore store,
            LegacyOrdinarySpellBinding binding, WorldSessionStore.BasicPhysicalNativeChange change,
            LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication native, BasicPhysicalAttempt? armAttempt)
        {
            if (!ReferenceEquals(store, frame._sessions) || !Monitor.IsEntered(frame._tradeStates.SyncRoot) ||
                !frame.basicPhysicalSlots.TryGetValue(binding.Connection, out var slot) || !ReferenceEquals(slot.Readiness.Binding, binding) ||
                !native.IsPreparedFor(binding, change) || !change.TryReadPreparedProposal(binding, out var before, out var kind, out _, out _) ||
                !ReferenceEquals(before, slot.Native)) throw new InvalidOperationException("basicPhysicalNativeSlotChanged");
            BasicPhysicalRepeat? repeat = null;
            BasicPhysicalAttemptState? hit = null;
            if (kind == LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.ArmRepeat)
            {
                if (armAttempt is null || !frame.basicPhysicalAttempts.TryGetValue(armAttempt, out var issued) ||
                    !ReferenceEquals(issued.Slot, slot) || !ReferenceEquals(slot.Attempt, armAttempt) ||
                    issued.CommittedGeneration < 0 || slot.Generation != issued.CommittedGeneration ||
                    slot.Committed is null && (slot.DragonhowlCommitted is not { } proc ||
                        !store.IsActualDragonhowlCompletion(proc, binding, armAttempt)))
                    throw new InvalidOperationException("basicPhysicalArmRequiresCommittedAttempt");
                repeat = new(issued.Target, native.After, slot.Generation);
            }
            else if (kind == LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.CommittedHit)
            {
                if (armAttempt is null || !frame.basicPhysicalAttempts.TryGetValue(armAttempt, out hit) ||
                    !ReferenceEquals(hit.Slot, slot) || !ReferenceEquals(slot.Attempt, armAttempt) ||
                    hit.CommittedGeneration >= 0 || slot.Generation != hit.Generation)
                    throw new InvalidOperationException("basicPhysicalStampRequiresActualAttempt");
            }
            var result = new BasicPhysicalNativePublication(frame, slot, before!, native.After, kind, repeat, hit);
            if (!result.IsCurrentPlain()) throw new InvalidOperationException("basicPhysicalNativeSlotChanged");
            return result;
        }

        internal static BasicPhysicalNativePublication PrepareDragonhowl(WorldServerFrameHandler frame, WorldSessionStore store,
            LegacyOrdinarySpellBinding binding, WorldSessionStore.BasicPhysicalNativeChange change,
            LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication native, OwnedDragonhowlAttempt attempt)
        {
            if (!ReferenceEquals(store, frame._sessions) || !frame.dragonhowlAttempts.TryGetValue(attempt, out var proc) ||
                !frame.DragonhowlAttemptCurrent(attempt, proc.Request, out _) || !ReferenceEquals(proc.Source.Actor, binding) ||
                !native.IsPreparedFor(binding, change) ||
                !change.TryReadPreparedProposal(binding, out var before, out var kind, out _, out _) ||
                !ReferenceEquals(before, proc.Source.Wire.Slot.Native))
                throw new InvalidOperationException("ownedDragonhowlNativeSlotChanged");
            var slot = proc.Source.Wire.Slot;
            BasicPhysicalRepeat? repeat = null;
            if (kind == LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.ArmRepeat)
            {
                if (proc.CommittedGeneration < 0 || slot.DragonhowlCommitted is not { } completion ||
                    !store.IsActualDragonhowlCompletion(completion, binding, attempt) ||
                    !frame.dragonhowlPrimaries.TryGetValue(proc.Source.Primary, out var primary))
                    throw new InvalidOperationException("ownedDragonhowlArmRequiresActualCompletion");
                repeat = primary.Binding is { } player
                    ? new(player, native.After, slot.Generation)
                    : new(null, native.After, slot.Generation, Dragonhowl: primary);
            }
            else if (kind != LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.CommittedHit || proc.CommittedGeneration >= 0)
                throw new InvalidOperationException("ownedDragonhowlNativeKindInvalid");
            var result = new BasicPhysicalNativePublication(frame, slot, before!, native.After, kind, repeat, null, proc);
            if (!result.IsCurrentPlain()) throw new InvalidOperationException("ownedDragonhowlNativeSlotChanged");
            return result;
        }

        internal void PublishPrepared()
        {
            slot.Native = after; slot.AttackStamp = stamp;
            if (kind == LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.CancelRepeat)
            {
                slot.Generation = nextGeneration;
                slot.Attempt = null; slot.Committed = null; slot.DragonhowlCommitted = null; slot.Repeat = null;
            }
            else if (kind == LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.CommittedHit)
            {
                slot.Generation = nextGeneration;
                if (proc is not null) proc.RecordCommittedGeneration(nextGeneration);
                else hit!.CommittedGeneration = nextGeneration;
                slot.Committed = null; slot.DragonhowlCommitted = null; slot.Repeat = null;
            }
            else slot.Repeat = repeat;
        }
    }

    internal BasicPhysicalNativePublication PrepareBasicPhysicalNativePublication(WorldSessionStore store,
        LegacyOrdinarySpellBinding binding, WorldSessionStore.BasicPhysicalNativeChange change,
        LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication native, BasicPhysicalAttempt? armAttempt = null)
        => BasicPhysicalNativePublication.Prepare(this, store, binding, change, native, armAttempt);

    internal BasicPhysicalNativePublication PrepareDragonhowlNativePublication(WorldSessionStore store,
        LegacyOrdinarySpellBinding binding, WorldSessionStore.BasicPhysicalNativeChange change,
        LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication native, OwnedDragonhowlAttempt attempt)
        => BasicPhysicalNativePublication.PrepareDragonhowl(this, store, binding, change, native, attempt);
}
