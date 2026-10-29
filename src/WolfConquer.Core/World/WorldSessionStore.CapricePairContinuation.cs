using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed partial class CapricePairReceipt
    {
        private WorldSession acceptedAttacker, acceptedTarget;

        internal WorldSession PublishAttackerContinuation(WorldSessionStore? actualStore,
            ConquerTcpConnection actualConnection, WorldSession proposed) =>
            PublishContinuation(actualStore, actualConnection, proposed, isAttacker: true);

        internal WorldSession PublishTargetContinuation(WorldSessionStore? actualStore,
            ConquerTcpConnection actualConnection, WorldSession proposed) =>
            PublishContinuation(actualStore, actualConnection, proposed, isAttacker: false);

        internal WorldSession PublishLightOfStaminaContinuation(WorldSessionStore? actualStore,
            ConquerTcpConnection actualConnection, WorldSession proposed)
        {
            if (!WorldPacketHandlers.TryReadCapriceOutputStamina(proposed.Character, out var value) || value != 100)
                throw Stop("attackerLightRestoreInvalid");
            // Source-supported Light restore: prepare its later Caprice output
            // before the actual CAS. Only plain field stores follow success.
            var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(checked((uint)Attacker.Character.Uid), 100).ToFrameBytes();
            var final = PublishContinuation(actualStore, actualConnection, proposed, isAttacker: true);
            attackerStamina = 100; attackerFrame = frame;
            return final;
        }

        private WorldSession PublishContinuation(WorldSessionStore? actualStore, ConquerTcpConnection actualConnection,
            WorldSession proposed, bool isAttacker)
        {
            var participant = isAttacker ? attacker : target;
            var role = isAttacker ? "attacker" : "target";
            if (!ReferenceEquals(actualStore, store) || !ReferenceEquals(actualConnection, participant.Connection))
                throw Stop(role + "ContextMismatch");
            if (Volatile.Read(ref sending) != 0) throw Stop(role + "OutputAlreadyStarted");
            var expected = isAttacker ? Volatile.Read(ref acceptedAttacker) : Volatile.Read(ref acceptedTarget);
            if (!store.TryCompareAndSetWithNativeIdentity(participant.Connection, expected, participant.Identity, proposed, out var final))
            {
                if (!LegacyNativeClientPublication.TryCurrent(store, participant.Connection,
                    participant.Expected.Character.Uid, participant.Identity, out var current)) throw Stop(role + "IssuanceLost");
                throw Stop(role + (ReferenceEquals(current, expected) ? "PublicationRefused" : "RecordChanged"));
            }
            if (isAttacker) Volatile.Write(ref acceptedAttacker, final!);
            else Volatile.Write(ref acceptedTarget, final!);
            return final!;
        }

        // Only consume concrete transform/death/query return records. This
        // does not discover a latest UID owner or authorize another mutation.
        internal WorldSession ObserveCompletedAttackerContinuation(WorldSession completed) =>
            ObserveCompletedContinuation(completed, isAttacker: true);
        internal WorldSession ObserveCompletedTargetContinuation(WorldSession completed) =>
            ObserveCompletedContinuation(completed, isAttacker: false);
        private WorldSession ObserveCompletedContinuation(WorldSession completed, bool isAttacker)
        {
            var participant = isAttacker ? attacker : target;
            if (!LegacyNativeClientPublication.TryCurrent(store, participant.Connection, participant.Expected.Character.Uid,
                    participant.Identity, out var current) || !ReferenceEquals(current, completed))
                throw Stop((isAttacker ? "attacker" : "target") + "CompletedRecordChanged");
            if (isAttacker) Volatile.Write(ref acceptedAttacker, completed);
            else Volatile.Write(ref acceptedTarget, completed);
            return completed;
        }

        internal async ValueTask<bool> SendLightOfStaminaAsync(CancellationToken cancellationToken)
        {
            RequireIssuances();
            var current = PendingFieldsCurrent(attackerPending: true, targetPending: false);
            RequireIssuances();
            if (!current) return false;
            try { await attacker.Connection.SendAsync(attackerFrame, cancellationToken).ConfigureAwait(false); }
            catch (Exception error) { throw Failure(error); }
            RequireIssuances(); return true;
        }

        private LegacyCapricePairContinuationRefusalException Stop(string reason) => new(reason, Attacker, Target);
    }
}

// The pair is historical committed state. A newer accepted record is retained,
// without quarantine, compensation or a retry grant for the old receive.
internal sealed class LegacyCapricePairContinuationRefusalException : Exception
{
    internal string Reason { get; }
    internal WorldSession PublishedAttacker { get; }
    internal WorldSession PublishedTarget { get; }
    internal LegacyCapricePairContinuationRefusalException(string reason, WorldSession attacker, WorldSession target)
        : base("capricePairCommittedContinuationRefused:" + reason)
    {
        Reason = "capricePairCommittedContinuationRefused:" + reason;
        PublishedAttacker = attacker; PublishedTarget = target;
    }
}
