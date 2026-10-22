namespace WolfConquer.Core.World;

public static partial class LegacyNativeBaseFunc
{
    public static uint ProficiencyLevelExperience(byte Level)
    {
        return proficiencyLevelExperience[Math.Min(Level, (byte)20)];
    }
}
