namespace WolfConquer.Core.World;

internal sealed partial class LegacyTradeRecoveryState
{
    internal bool IsReadyForCharacterItemsSpells(string? expectedUsersRoot, string? expectedItemsRoot, string? expectedSpellsRoot)
    {
        lock (_gate)
        {
            var configured = _admissionRoots;
            if (!IsReadyFor(expectedUsersRoot, expectedItemsRoot) || configured?.Items is not { } items ||
                configured.Spells is not { } spells || string.IsNullOrWhiteSpace(expectedSpellsRoot) ||
                !Path.IsPathFullyQualified(expectedSpellsRoot)) return false;
            try
            {
                return SameReadyRoot(configured.Users, expectedUsersRoot!) &&
                    SameReadyRoot(items, expectedItemsRoot!) && SameReadyRoot(spells, expectedSpellsRoot) &&
                    LegacyTradeJournal.IsCharacterItemsSpellsDomainConfigured(configured.Users, items, spells) &&
                    !LegacyTradeJournal.HasPending(configured.Users);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or
                System.Security.SecurityException or IOException or UnauthorizedAccessException)
            { return false; }
        }
    }
}
