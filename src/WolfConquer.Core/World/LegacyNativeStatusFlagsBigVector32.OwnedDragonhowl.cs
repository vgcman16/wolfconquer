using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    // Preparation captures the real vector, but is inert until the registered
    // Store proc phase consumes it. No AddFlag compatibility mutation is used.
    internal sealed class DragonhowlFlashingPreparation
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativeStatusFlagsBigVector32 vector;
        private readonly State before, after;
        private readonly PreparedState staged;
        private bool publicationIssued, published;

        private DragonhowlFlashingPreparation(LegacyOrdinarySpellBinding binding,
            LegacyNativeStatusFlagsBigVector32 vector, State before, DateTime timer)
        {
            this.binding = binding; this.vector = vector; this.before = before;
            var row = new Flag(0, 30, true, 0) { Timer = timer };
            var registrations = new ConcurrentDictionary<int, Flag>(before.Registrations,
                before.Registrations.Comparer);
            if (!registrations.TryAdd(0, row)) throw InvalidFlashing();
            var words = (uint[])before.SnapshotWords.Clone();
            words[0] |= 1;
            var rows = before.Rows.Append(new RowState(0, row, 30, 0, timer, default, true)).ToArray();
            after = new(registrations, words, (uint[])words.Clone(), rows);
            staged = new(before, registrations, words, registrations.Values.ToArray());
        }

        internal static DragonhowlFlashingPreparation Prepare(LegacyOrdinarySpellBinding binding, DateTime timer)
        {
            var vector = binding.Player.BitVector;
            if (!NativeOwnerCurrent(binding, vector) || timer.Kind != DateTimeKind.Unspecified ||
                !TryDeadline(timer, 30, out _) || !vector.TryCapture(out var before) || before is null ||
                before.Words.Length == 0 || !ReferenceEquals(before.Registrations.Comparer, EqualityComparer<int>.Default) ||
                before.Rows.Any(row => row.Key == 0)) throw InvalidFlashing();
            var result = new DragonhowlFlashingPreparation(binding, vector, before, timer);
            if (!result.IsCurrentPlain()) throw InvalidFlashing();
            return result;
        }

        internal bool IsCurrentPlain() => NativeOwnerCurrent(binding, vector) &&
            vector.HasUnchangedCapturedGraph(published ? after : before);

        internal Publication PreparePublication(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
            int index, WorldSession expected, WorldSession proposed) =>
            Publication.Prepare(this, plan, index, expected, proposed);

        internal sealed class Publication
        {
            private readonly DragonhowlFlashingPreparation preparation;
            private readonly WorldPacketHandlers.OwnedDragonhowlPreparation plan;
            private readonly int index;
            private readonly WorldSession expected, proposed;
            private Publication(DragonhowlFlashingPreparation preparation,
                WorldPacketHandlers.OwnedDragonhowlPreparation plan, int index,
                WorldSession expected, WorldSession proposed)
            { this.preparation = preparation; this.plan = plan; this.index = index; this.expected = expected; this.proposed = proposed; }
            internal static Publication Prepare(DragonhowlFlashingPreparation preparation,
                WorldPacketHandlers.OwnedDragonhowlPreparation plan, int index,
                WorldSession expected, WorldSession proposed)
            {
                if (preparation.publicationIssued || preparation.published || !preparation.IsCurrentPlain() ||
                    !preparation.binding.Store.IsActualDragonhowlFlashingPhase(plan, index,
                        preparation.binding, expected, proposed, preparation)) throw InvalidFlashing();
                var publication = new Publication(preparation, plan, index, expected, proposed);
                preparation.publicationIssued = true;
                return publication;
            }
            internal bool IsCurrentPlain() => !preparation.published && preparation.IsCurrentPlain() &&
                preparation.binding.Store.IsActualDragonhowlFlashingPhase(plan, index,
                    preparation.binding, expected, proposed, preparation);
            // The real Store consumed all phase/native proofs immediately before
            // these plain assignments. All rows, words and cache are preallocated.
            internal void PublishPrepared()
            {
                preparation.vector.CommitPreparedState(preparation.staged);
                preparation.published = true;
            }
        }

        private static bool NativeOwnerCurrent(LegacyOrdinarySpellBinding binding,
            LegacyNativeStatusFlagsBigVector32 vector) =>
            WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
            Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) &&
            Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) && Monitor.IsEntered(vector.SyncRoot) &&
            !binding.IsRetired && ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) &&
            ReferenceEquals(binding.Client.Player, binding.Player) && ReferenceEquals(binding.Player.Owner, binding.Client) &&
            ReferenceEquals(binding.Player.BitVector, vector) && binding.Player.UID == binding.Uid &&
            binding.Store.IsBoundSpellOwnerCurrent(binding);

        private static InvalidOperationException InvalidFlashing() => new("ownedDragonhowlFlashingNativeChanged");
    }
}
