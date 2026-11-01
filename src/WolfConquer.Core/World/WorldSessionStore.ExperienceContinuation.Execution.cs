using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // The marker is data. A continuation starts only from one real committed
    // credit receipt or one exact published ordinary-admission origin, plus the
    // recipient's genuine active exclusive Frame operation and native binding.
    // This owner has no method that can publish a step or grant experience yet.
    internal sealed partial class ExperienceContinuationExecution
    {
        private readonly WorldSessionStore store;
        private readonly WorldServerFrameHandler frame;
        private readonly WorldPacketHandlerContext context;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSession current;
        private readonly LegacyExperienceContinuation pending;
        private readonly byte[] image;
        private int resumePurposeIssued;

        private ExperienceContinuationExecution(WorldSessionStore store, WorldServerFrameHandler frame,
            WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding, WorldSession current,
            LegacyExperienceContinuation pending, byte[] image)
        {
            this.store = store; this.frame = frame; this.context = context; this.binding = binding;
            this.current = current; this.pending = pending; this.image = image;
        }

        internal WorldSession Current => current;
        internal LegacyExperienceContinuation Pending => pending;
        internal WorldPacketHandlerContext OperationContext => context;
        internal LegacyOrdinarySpellBinding Binding => binding;
        internal byte[] CopyCharacterImage() => image.ToArray();

        // Invoked only after an actual credit/step is known durable and a
        // query, output or subsequent step can no longer be proved. No stale
        // receipt may clear a replacement binding.
        internal void FenceFailure(Exception error)
        {
            if (store.TryRetireRecoverableContinuation(context, binding, error))
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
            lock (frame.RequireActionLifecycleGate(store))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages,
                    store.PeekMailboxSession(binding.Connection));
                if (!store._connectionGates.TryGetValue(binding.Connection, out var gate))
                {
                    FenceContinuationDetached(binding, error);
                    throw new InvalidOperationException("experienceContinuationFenceUnexpectedReturn");
                }
                lock (gate)
                {
                    using var native = EnterNativeConnectionGate();
                    if (store._sessions.TryGetValue(binding.Connection, out var holder) &&
                        ReferenceEquals(holder.SpellBinding, binding) && ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity))
                    {
                        using var actors = new SpellActorScope(binding);
                        // Cleanup only: a real query may have published before
                        // the caller could adopt it. Uncertain persistence still
                        // blocks the domain; never select this holder for play.
                        if (store._sessions.TryGetValue(binding.Connection, out var same) && ReferenceEquals(same, holder) &&
                            ReferenceEquals(holder.SpellBinding, binding) && ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity))
                            FenceContinuationOutcome(holder, binding, error);
                    }
                    FenceContinuationDetached(binding, error);
                }
            }
            throw new InvalidOperationException("experienceContinuationFenceUnexpectedReturn");
        }

        internal bool TryCurrent(out WorldSession currentSession)
        {
            currentSession = null!;
            RefuseBoundSpellReentry(binding.Connection);
            lock (frame.RequireActionLifecycleGate(store))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages, current);
                if (!store._connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
                lock (gate)
                {
                    using var native = EnterNativeConnectionGate();
                    using var actors = new SpellActorScope(binding);
                    string path = Path.Combine(binding.UsersRoot, current.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
                    bool valid = CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
                        CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceContinuationCurrent", out var saved, out _) &&
                        IsHeldCurrentPlain(saved));
                    if (!valid) return false;
                    currentSession = current;
                    return true;
                }
            }
        }

        // Caller holds lifecycle -> package -> connection -> native -> actors,
        // then the Character coordinator lock. This is a proof, not a writer.
        internal bool IsHeldCurrentPlain(byte[] currentImage)
        {
            if (!frame.IsActionLifecycleCurrent(store, context) ||
                context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) != true ||
                !store._connectionGates.TryGetValue(binding.Connection, out var gate) || !Monitor.IsEntered(gate) ||
                !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !binding.Recovery.IsReadyForUsers(binding.UsersRoot) || !binding.Recovery.TryRequireLiveJournalClear(out _) ||
                !store._sessions.TryGetValue(binding.Connection, out var holder) || !store.BindingIsCurrentPlain(binding, holder, current) ||
                !currentImage.AsSpan().SequenceEqual(image)) return false;
            try
            {
                return Equals(pending, LegacyExperienceContinuationCodec.ReadCharacterImage(currentImage,
                    current.AccountId, checked((uint)current.Character.Uid))) &&
                    Equals(pending, LegacyExperienceContinuationCodec.ReadCharacter(current.Character, current.AccountId)) &&
                    current.Character.Experience >= 0 && (ulong)current.Character.Experience == pending.RemainingExperience &&
                    current.Character.Level == pending.CurrentLevel;
            }
            catch (Exception error) when (error is InvalidDataException or ArgumentException or OverflowException)
            { return false; }
        }

        private static bool TryBegin(WorldSessionStore store, WorldPacketHandlerContext context,
            WorldSession expected, LegacyExperienceContinuation pending, out ExperienceContinuationExecution? execution)
        {
            execution = null;
            if (store.framePersistenceOwner is not { } frame ||
                !frame.TryPrepareActionContext(store, context, expected, out var actual) ||
                actual.TradeOperations?.OwnsActiveExclusiveLease(actual.FrameOperationLease) != true ||
                expected.NativeGameClient?.OrdinarySpellBinding is not { } binding ||
                !ReferenceEquals(binding.Connection, actual.Connection)) return false;
            lock (frame.RequireActionLifecycleGate(store))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages, expected);
                if (!store._connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
                lock (gate)
                {
                    using var native = EnterNativeConnectionGate();
                    using var actors = new SpellActorScope(binding);
                    string path = Path.Combine(binding.UsersRoot, expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
                    ExperienceContinuationExecution? captured = null;
                    bool found = CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
                    {
                        if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceContinuationBegin", out var image, out _)) return false;
                        var candidate = new ExperienceContinuationExecution(store, frame, actual, binding, expected, pending, image.ToArray());
                        if (!candidate.IsHeldCurrentPlain(image)) return false;
                        captured = candidate;
                        return true;
                    });
                    execution = captured;
                    return found;
                }
            }
        }

        internal static bool TryBeginCredit(WorldSessionStore store, WorldPacketHandlerContext context,
            ExperienceCreditReceipt receipt, out ExperienceContinuationExecution? execution)
        {
            execution = null;
            return receipt.TryCurrent(out var current, out var pending) && pending is not null &&
                TryBegin(store, context, current, pending, out execution);
        }

        internal static bool TryBeginCompletedQuery(WorldSessionStore store, WorldPacketHandlerContext context,
            WorldPacketHandlers.CompletedContinuationQuery query, out ExperienceContinuationExecution? execution)
        {
            execution = null;
            if (!query.TryCurrent(out var current) || !ReferenceEquals(query.Session, current) ||
                !ReferenceEquals(current.NativeGameClient?.OrdinarySpellBinding?.Store, store)) return false;
            try
            {
                var pending = LegacyExperienceContinuationCodec.ReadCharacter(current.Character, current.AccountId);
                return pending is { Phase: LegacyExperienceContinuationPhase.Ready } && query.CoversPending(pending) &&
                    TryBegin(store, context, current, pending, out execution);
            }
            catch (Exception error) when (error is InvalidDataException or ArgumentException or OverflowException) { return false; }
        }

        internal static bool TryBeginAdmissionQuery(WorldSessionStore store, WorldPacketHandlerContext context,
            WorldPacketHandlers.CompletedAdmissionContinuationQuery query, out ExperienceContinuationExecution? execution)
        {
            execution = null;
            if (!query.TryCurrent(out var current) || !ReferenceEquals(query.Session, current) ||
                !ReferenceEquals(current.NativeGameClient?.OrdinarySpellBinding?.Store, store)) return false;
            try
            {
                return Equals(query.Pending, LegacyExperienceContinuationCodec.ReadCharacter(current.Character, current.AccountId)) &&
                    TryBegin(store, context, current, query.Pending, out execution);
            }
            catch (Exception error) when (error is InvalidDataException or ArgumentException or OverflowException) { return false; }
        }
    }
}
