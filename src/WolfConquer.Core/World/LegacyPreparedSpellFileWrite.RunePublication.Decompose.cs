using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedSpellFileWrite
{
    internal bool HasCommittedRuneDecompose(LegacyRuneDecomposeEvidence evidence) =>
        committedRuneCharacter?.IsCommittedDecompose(this, evidence) ?? false;

    internal sealed partial class RuneCharacterPublication
    {
        // Historical commit identity only. After holder publication the old
        // expected record is no longer current. This grants no admission or
        // cleanup permission; it only exempts that exact committed request
        // from the issued-but-uncommitted DENY-only fence.
        internal bool IsCommittedDecompose(LegacyPreparedSpellFileWrite actual, LegacyRuneDecomposeEvidence evidence) =>
            ReferenceEquals(writer, actual) && writer.committed && ReferenceEquals(writer.committedRuneCharacter, this) &&
            decompose is { } actualEvidence && ReferenceEquals(actualEvidence, evidence) && ReferenceEquals(actualEvidence.Operation, evidence.Operation);

        // Called only after the genuine journal sets this writer's private
        // committed pointer, before native publication and namespace cleanup.
        internal void RequireDecomposeNamespaceRetirement(LegacyItemUidRegistry registry,
            LegacyItemUidReservationReceipt receipt)
        {
            if (decompose is null || !ReferenceEquals(decompose.Operation.Allocation, receipt) ||
                !ReferenceEquals(writer.committedRuneCharacter, this) || !RequireCommittedCharacter()) throw Unavailable();
            decompose.RequireRegistry(registry);
        }

        // RequireCommittedCharacter owns the complete ordered target locks for
        // this branch. The row may legitimately be retired after this proof;
        // neither row absence nor Evidence.IsCurrent authorizes post-writer state.
        private bool DecomposeCommittedSidecarsMatch() => decompose is null ||
            CharacterSnapshotFileBaseline.TryReadBytes(decompose.ItemPath, "runeDecomposePublication", out var items, out _) &&
            items.AsSpan().SequenceEqual(changes[1].After) &&
            CharacterSnapshotFileBaseline.TryReadBytes(writer.SpellsPath, "runeDecomposePublication", out var spells, out _) &&
            spells.AsSpan().SequenceEqual(changes[2].After);
    }
}
