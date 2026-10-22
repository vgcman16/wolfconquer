namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void AddFlagMastery(UnlockMasterys flag)
    {
        unchecked
        {
            if (!ContainsFlagMastery(flag))
            {
                UnlockMastery |= (uint)(int)flag;
            }
        }
    }

    public bool ContainsFlagMastery(UnlockMasterys flag)
    {
        unchecked
        {
            ulong num = UnlockMastery;
            num &= (uint)(~flag);
            return num != UnlockMastery;
        }
    }

    public void RemoveFlagMastery(UnlockMasterys flag)
    {
        unchecked
        {
            if (ContainsFlagMastery(flag))
            {
                UnlockMastery &= (uint)(int)(~flag);
            }
        }
    }
}
