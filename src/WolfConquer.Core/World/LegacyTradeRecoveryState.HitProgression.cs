namespace WolfConquer.Core.World;

internal sealed partial class LegacyTradeRecoveryState
{
    internal bool IsReadyForHitProgression(string users, string items, string spells, string proficiencies)
    {
        lock (_gate)
            return _admissionRoots?.Proficiencies is { } configured && SameReadyRoot(configured, proficiencies) &&
                IsReadyForCharacterItemsSpells(users, items, spells);
    }
}
