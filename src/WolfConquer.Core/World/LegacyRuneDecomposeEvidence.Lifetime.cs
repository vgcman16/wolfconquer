namespace WolfConquer.Core.World;

internal sealed partial class LegacyRuneDecomposeEvidence
{
    internal string SavedBeforePath(int ordinal) => ordinal switch
    {
        0 => before.CharacterPath,
        1 => before.ItemPath,
        2 => before.SpellsPath,
        _ => throw new ArgumentOutOfRangeException(nameof(ordinal)),
    };

    // Called within the SAME actual Trades scope as the whole synchronous
    // producer call. Unexpected exceptions/cancellation are covered too.
    internal void FenceUnlessCommitted()
    {
        if (issuedAllocation is not null && !before.HasCommittedRuneDecompose(this)) FenceIssuedNamespace();
    }

    private void FenceIssuedNamespace()
    {
        if (issuedAllocation is not null) runtime.FenceRuneDecomposeNamespace(frame);
    }
}
