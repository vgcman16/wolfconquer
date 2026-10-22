#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCombatGear
{
    public byte[] AddFlag(LotteryEffect flag)
    {
        if (!ContainsFlag(flag))
        {
            Lottery |= (uint)(int)flag;
        }
        return new LegacyNativeMsgDaoQi
        {
            Type = LegacyNativeMsgDaoQi.Types.LOTTERY,
            LOTTERY = Lottery
        };
    }

    public bool ContainsFlag(LotteryEffect flag)
    {
        ulong num = Lottery;
        num &= (uint)(~flag);
        return num != Lottery;
    }

    public byte[] RemoveFlag(LotteryEffect flag)
    {
        if (ContainsFlag(flag))
        {
            Lottery &= (uint)(int)(~flag);
        }
        return new LegacyNativeMsgDaoQi
        {
            Type = LegacyNativeMsgDaoQi.Types.LOTTERY,
            LOTTERY = Lottery
        };
    }

    public byte[] UpdateTaoistReiki()
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return null;
        }
        return new LegacyNativeMsgDaoQi
        {
            Type = LegacyNativeMsgDaoQi.Types.TAOIST_REIKI,
            TaoistReiki = TaoistReiki
        };
    }

    public static bool Rate(int value, int discriminant)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return false;
        }
        return value > GetRandom.Next() % discriminant;
    }
}
