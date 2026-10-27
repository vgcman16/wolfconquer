namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void AddFlag(ulong flag) => ExecutePreparedFlagChange(flag, remove: false);

    public bool ContainsFlag(ulong flag)
    {
        ulong flag2 = Flag;
        flag2 &= ~flag;
        return flag2 != Flag;
    }

    public void RemoveFlag(ulong flag) => ExecutePreparedFlagChange(flag, remove: true);
}
