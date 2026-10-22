using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    // Complete native ServerDatabase1565..1582. The UID value and asymmetric
    // Treasury/UnionGoldBrick keys are literal, not corrected to inferred intent.
    // No queue, worker, account lookup, login or save orchestration is installed.
    public static void UpdateUnionMember(LegacyNativeUnionMember Member)
    {
        LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("\\Users\\" + Member.UID + ".ini");
        if (Member.ReceiveKick == 0)
        {
            iniFile.Write("Character", "UnionUID", Member.UID);
            iniFile.Write("Character", "UnionRank", (uint)Member.Rank);
            iniFile.Write("Character", "UnionExploits", Member.Exploits);
            iniFile.Write("Character", "Treasury", Member.MyTreasury);
        }
        else
        {
            iniFile.Write("Character", "UnionUID", 0u);
            iniFile.Write("Character", "UnionRank", 0u);
            iniFile.Write("Character", "UnionExploits", 0u);
            iniFile.Write("Character", "UnionGoldBrick", 0u);
        }
    }
}
