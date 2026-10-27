namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    // VisibleDx8D13's primary ProfessionalName.ini confirms these existing
    // prestige families through stage 57. Other class systems use separate rules.
    public static bool TryGetIndex(uint characterClass, out Type category)
    {
        category = characterClass switch
        {
            >= 1000 and <= 1057 => Type.Trojan,
            >= 2000 and <= 2057 => Type.Warrior,
            >= 4000 and <= 4057 => Type.Archer,
            >= 5000 and <= 5057 => Type.Ninja,
            >= 6000 and <= 6057 => Type.Monk,
            >= 7000 and <= 7057 => Type.Pirate,
            >= 8000 and <= 8057 => Type.DragonWarrior,
            >= 9000 and <= 9057 => Type.Thunderstriker,
            >= 13002 and <= 13057 => Type.WaterTao,
            >= 14002 and <= 14057 => Type.FireTao,
            >= 16000 and <= 16057 => Type.WindWalker,
            _ => Type.Count,
        };
        return category != Type.Count;
    }

    // Retain the existing literal compatibility exclusion. In particular, 10002
    // is absent from that primary table; this does not confirm a new client job.
    internal static bool IsWorldOnlyClass(uint characterClass) =>
        characterClass is >= 10000 and <= 10002;
}
