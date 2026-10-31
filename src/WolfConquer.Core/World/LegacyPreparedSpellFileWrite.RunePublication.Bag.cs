using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedSpellFileWrite
{
    internal bool HasCommittedRuneBag(LegacyRuneBagEvidence evidence) => committedRuneCharacter?.IsCommittedBag(this, evidence) ?? false;

    internal sealed partial class RuneCharacterPublication
    {
        internal bool IsCommittedBag(LegacyPreparedSpellFileWrite actual, LegacyRuneBagEvidence evidence) =>
            ReferenceEquals(writer, actual) && writer.committed && ReferenceEquals(writer.committedRuneCharacter, this) &&
            bag is { } actualEvidence && ReferenceEquals(actualEvidence, evidence) && ReferenceEquals(actualEvidence.Operation, evidence.Operation);

        internal void RequireRuneBagNamespaceRetirement(LegacyItemUidRegistry registry, LegacyItemUidReservationReceipt receipt)
        {
            if (bag is null || !ReferenceEquals(bag.Operation.Allocation, receipt) ||
                !ReferenceEquals(writer.committedRuneCharacter, this) || !RequireCommittedCharacter()) throw Unavailable();
            bag.RequireRegistry(registry);
        }

        private bool BagCommittedSidecarsMatch() => bag is null ||
            CharacterSnapshotFileBaseline.TryReadBytes(bag.ItemPath, "runeBagPublication", out var items, out _) &&
            items.AsSpan().SequenceEqual(changes[1].After) &&
            CharacterSnapshotFileBaseline.TryReadBytes(writer.SpellsPath, "runeBagPublication", out var spells, out _) &&
            spells.AsSpan().SequenceEqual(changes[2].After);

        internal LegacyNativePlayerRuntime.OwnedRuneBagPublication? PrepareBagNativePublication(LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed)
        {
            if (bag is null) return null;
            RequireCurrent(binding.Store, binding.Connection, expected, proposed);
            return LegacyNativePlayerRuntime.OwnedRuneBagPublication.PrepareBound(binding);
        }
    }
}
