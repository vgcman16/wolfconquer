namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    public bool CheckProf(ushort ID, byte level)
    {
        if (ClientProf.TryGetValue(ID, out var value))
        {
            return value.Level >= level;
        }
        return false;
    }
}
