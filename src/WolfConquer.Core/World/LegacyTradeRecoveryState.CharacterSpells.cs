namespace WolfConquer.Core.World;

internal sealed partial class LegacyTradeRecoveryState
{
    internal bool IsReadyForCharacterSpells(string? expectedUsersRoot, string? expectedSpellsRoot)
    {
        lock (_gate)
        {
            var configured = _admissionRoots;
            if (!IsReadyForUsers(expectedUsersRoot) || configured?.Spells is not { } spells ||
                string.IsNullOrWhiteSpace(expectedSpellsRoot) || !Path.IsPathFullyQualified(expectedSpellsRoot))
                return false;
            try
            {
                return SameReadyRoot(configured.Users, expectedUsersRoot!) &&
                    SameReadyRoot(spells, expectedSpellsRoot) &&
                    LegacyTradeJournal.IsCharacterSpellsDomainConfigured(configured.Users, spells) &&
                    !LegacyTradeJournal.HasPending(configured.Users);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or
                System.Security.SecurityException or IOException or UnauthorizedAccessException)
            { return false; }
        }
    }
}
