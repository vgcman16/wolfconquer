namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed partial class CapricePairReceipt
    {
        private readonly WorldSessionStore store;
        private readonly ProcParticipant attacker, target;
        private readonly LegacyOrdinarySpellBinding attackerBinding, targetBinding;
        private ulong attackerStamina;
        private readonly ulong targetStamina;
        private byte[] attackerFrame;
        private readonly byte[] targetFrame;
        private int sending;
        internal WorldSession Attacker => attacker.Expected;
        internal WorldSession Target => target.Expected;
        internal CapricePairReceipt(WorldSessionStore store, ProcParticipant attacker, ProcParticipant target,
            LegacyOrdinarySpellBinding attackerBinding, LegacyOrdinarySpellBinding targetBinding, ulong attackerStamina, ulong targetStamina)
        {
            this.store = store; this.attacker = attacker; this.target = target;
            this.attackerBinding = attackerBinding; this.targetBinding = targetBinding;
            this.attackerStamina = attackerStamina; this.targetStamina = targetStamina;
            acceptedAttacker = attacker.Expected; acceptedTarget = target.Expected;
            attackerFrame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(checked((uint)attacker.Expected.Character.Uid), attackerStamina).ToFrameBytes();
            targetFrame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(checked((uint)target.Expected.Character.Uid), targetStamina).ToFrameBytes();
        }
        internal async ValueTask<bool> SendAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref sending, 1) != 0) throw new InvalidOperationException("capricePairReceiptAlreadyConsumed");
            RequireIssuances();
            var actorCurrent = PendingFieldsCurrent(attackerPending: true, targetPending: false);
            RequireIssuances();
            if (actorCurrent)
            {
                try { await attacker.Connection.SendAsync(attackerFrame, cancellationToken).ConfigureAwait(false); }
                catch (Exception error) { throw Failure(error); }
            }
            // The completed attacker's field is no longer a prerequisite for
            // the unrelated target output (e.g. legitimate post-send regen).
            RequireIssuances();
            var targetCurrent = PendingFieldsCurrent(attackerPending: false, targetPending: true);
            RequireIssuances();
            if (targetCurrent)
            {
                try { await target.Connection.SendAsync(targetFrame, cancellationToken).ConfigureAwait(false); }
                catch (Exception error) { throw Failure(error); }
            }
            RequireIssuances(); return actorCurrent && targetCurrent;
        }
        private void RequireIssuances()
        {
            if (!store.IsBoundSpellOwnerCurrent(attackerBinding) || !store.IsBoundSpellOwnerCurrent(targetBinding) ||
                !LegacyNativeClientPublication.TryCurrent(store, attacker.Connection, Attacker.Character.Uid, attacker.Identity, out _) ||
                !LegacyNativeClientPublication.TryCurrent(store, target.Connection, Target.Character.Uid, target.Identity, out _))
                throw Failure(new InvalidOperationException("committedCapricePairOriginalIssuanceLost"));
        }
        private bool PendingFieldsCurrent(bool attackerPending, bool targetPending)
        {
            if (!LegacyNativeClientPublication.TryCurrent(store, attacker.Connection, Attacker.Character.Uid, attacker.Identity, out var currentAttacker) ||
                !LegacyNativeClientPublication.TryCurrent(store, target.Connection, Target.Character.Uid, target.Identity, out var currentTarget)) return false;
            if (attackerPending && (!WorldPacketHandlers.TryReadCapriceOutputStamina(currentAttacker.Character, out var first) || first != attackerStamina) ||
                targetPending && (!WorldPacketHandlers.TryReadCapriceOutputStamina(currentTarget.Character, out var second) || second != targetStamina)) return false;
            // A wrapped input can call out while acquired. Close those reads
            // against the exact still-pending records before transport begins.
            return (!attackerPending || LegacyNativeClientPublication.TryCurrent(store, attacker.Connection,
                    Attacker.Character.Uid, attacker.Identity, out var afterAttacker) && ReferenceEquals(currentAttacker, afterAttacker)) &&
                (!targetPending || LegacyNativeClientPublication.TryCurrent(store, target.Connection,
                    Target.Character.Uid, target.Identity, out var afterTarget) && ReferenceEquals(currentTarget, afterTarget));
        }
        private WorldSessionPublicationException Failure(Exception cause)
        {
            var failure = new WorldSessionPublicationException(store, attacker.Connection, attacker.Identity,
                attackerBinding, Attacker, WorldSessionPublicationStage.ProcContinuation, cause);
            FenceBoth(failure); return failure;
        }
        internal void FenceBoth(WorldSessionPublicationException failure)
        {
            var ownerFailure = new WorldSessionPublicationException(store, attacker.Connection, attacker.Identity,
                attackerBinding, Attacker, failure.Stage, failure);
            var peerFailure = new WorldSessionPublicationException(store, target.Connection, target.Identity,
                targetBinding, Target, failure.Stage, failure);
            store.FenceObserverFailure(ownerFailure); store.FenceObserverFailure(peerFailure);
            if (ownerFailure.FencingFailure is { } ownerSecondary) failure.RecordFencingFailure(ownerSecondary);
            if (peerFailure.FencingFailure is { } secondary) failure.RecordFencingFailure(secondary);
        }
    }
}
