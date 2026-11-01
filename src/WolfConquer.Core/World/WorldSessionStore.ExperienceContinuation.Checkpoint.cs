using System.Globalization;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed class ContinuationCheckpointSource(WorldPacketHandlers.CompletedContinuationQuery query,
        LegacyOrdinarySpellBinding binding, WorldPacketHandlerContext context, WorldSession before, WorldSession after,
        LegacyExperienceContinuation pending, byte[] image, ActionPublication publication)
    {
        internal readonly WorldPacketHandlers.CompletedContinuationQuery Query = query;
        internal readonly LegacyOrdinarySpellBinding Binding = binding;
        internal readonly WorldPacketHandlerContext Context = context;
        internal readonly WorldSession Before = before, After = after;
        internal readonly LegacyExperienceContinuation Pending = pending;
        internal readonly byte[] Image = image.ToArray();
        internal readonly ActionPublication Publication = publication;
        internal int Consumed;
    }
    private static readonly ConditionalWeakTable<OwnedContinuationCheckpoint, ContinuationCheckpointSource> ContinuationCheckpoints = new();

    // Only an actual phase/restore writer calls this after verifying its exact
    // durable image and publishing the same actual holder/native owner.
    private static OwnedContinuationCheckpoint IssueContinuationCheckpoint(WorldPacketHandlers.CompletedContinuationQuery query,
        LegacyOrdinarySpellBinding binding, WorldPacketHandlerContext context, WorldSession before, WorldSession after,
        LegacyExperienceContinuation pending, byte[] image, ActionPublication publication)
    {
        if (!ReferenceEquals(publication.Published, after) || !ReferenceEquals(after.NativeGameClient, binding.Client))
            throw new InvalidOperationException("experienceContinuationCheckpointInvalid");
        var checkpoint = new OwnedContinuationCheckpoint();
        ContinuationCheckpoints.Add(checkpoint, new(query, binding, context, before, after, pending, image, publication));
        return checkpoint;
    }

    internal sealed class OwnedContinuationCheckpoint
    {
        // One actual query may advance only to the holder published by its
        // registered durable checkpoint, never to an arbitrary equal session.
        internal bool TryResolve(WorldPacketHandlers.CompletedContinuationQuery query, LegacyOrdinarySpellBinding binding,
            WorldSession expected, out WorldSession after)
        {
            after = null!;
            try
            {
                if (!ContinuationCheckpoints.TryGetValue(this, out var source) ||
                    !ReferenceEquals(source.Query, query) || !ReferenceEquals(source.Binding, binding) ||
                    !ReferenceEquals(source.Before, expected) || Volatile.Read(ref source.Consumed) != 0 ||
                    !source.Publication.TryCurrent(out var current) || !ReferenceEquals(current, source.After)) return false;
                var store = binding.Store;
                if (store.framePersistenceOwner is not { } frame || IsCurrentThreadInsideAnyConnectionGate ||
                    Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot)) return false;
                bool valid;
                lock (frame.RequireActionLifecycleGate(store))
                {
                    using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages, current);
                    if (!store._connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
                    lock (gate)
                    {
                        using var native = EnterNativeConnectionGate();
                        using var actors = new SpellActorScope(binding);
                        if (!frame.IsActionLifecycleCurrent(store, source.Context) ||
                            source.Context.TradeOperations?.OwnsActiveExclusiveLease(source.Context.FrameOperationLease) != true ||
                            !store._sessions.TryGetValue(binding.Connection, out var holder) ||
                            !store.BindingIsCurrentPlain(binding, holder, current) ||
                            binding.Player.HitPoints != current.Character.MinHitPoints || binding.Player.Mana != current.Character.MinMana ||
                            (uint)binding.Player.Action != LegacyActionState.RequireCurrent(current.Character)) return false;
                        string path = Path.Combine(binding.UsersRoot, current.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
                        valid = CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
                            CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceContinuationCheckpoint", out var image, out _) &&
                            image.AsSpan().SequenceEqual(source.Image) &&
                            Equals(source.Pending, LegacyExperienceContinuationCodec.ReadCharacterImage(image,
                                current.AccountId, checked((uint)current.Character.Uid))) &&
                            Equals(source.Pending, LegacyExperienceContinuationCodec.ReadCharacter(current.Character, current.AccountId)) &&
                            Interlocked.CompareExchange(ref source.Consumed, 1, 0) == 0);
                    }
                }
                if (!valid) return false;
                after = current; return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or
                InvalidOperationException or ArgumentException or OverflowException or NotSupportedException or System.Security.SecurityException)
            { return false; }
        }
    }
}
