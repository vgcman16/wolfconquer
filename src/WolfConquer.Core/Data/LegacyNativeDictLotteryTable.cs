#nullable disable

namespace WolfConquer.Core.Data;

// Complete native CQ_DICT_LOTTERY row, mutable static cache and loader only.
internal class LegacyNativeDictLotteryTable
{
    public class CQ
    {
        public uint Id;
        public uint Type;
        public uint Value1;
        public uint Value2;
        public uint Value3;
        public uint rate;
    }

    public static Dictionary<uint, CQ> Info
    {
        get => LegacyNativeCombatGearTable.MailboxLottery;
        set => LegacyNativeCombatGearTable.MailboxLottery = value;
    }

    public static void Load(string DBLocation)
    {
        try
        {
            lock (LegacyNativeCombatGearTable.MailboxLoadGate)
            {
                if (!LegacyNativeCombatGearTable.m_Load) return;
                var prepared = LegacyNativeCombatGearTable.MailboxLoadStage.ReadLottery(DBLocation + "dict_lottery.txt");
                Info = prepared;
            }
        }
        catch (Exception error) { Console.WriteLine(error); }
    }
}
