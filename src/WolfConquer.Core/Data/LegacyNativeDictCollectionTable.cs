#nullable disable

namespace WolfConquer.Core.Data;

// Complete native CQ_DICT_COLLECTION row, mutable static cache and loader.
// No CQ startup/metadata registration or host publication is installed here.
internal class LegacyNativeDictCollectionTable
{
    public class CQ
    {
        public uint Id;
        public uint Type;
        public uint Min;
        public uint Mix;
        public LegacyNativeDaoqiDictTypeTable.AttrType attr_type1;
        public uint attr_value1;
        public uint value2;
    }

    public static Dictionary<uint, CQ> Info
    {
        get => LegacyNativeCombatGearTable.MailboxCollection;
        set => LegacyNativeCombatGearTable.MailboxCollection = value;
    }

    public static void Load(string DBLocation)
    {
        try
        {
            lock (LegacyNativeCombatGearTable.MailboxLoadGate)
            {
                if (!LegacyNativeCombatGearTable.m_Load) return;
                var prepared = LegacyNativeCombatGearTable.MailboxLoadStage.ReadCollection(DBLocation + "dict_collection.txt");
                Info = prepared;
            }
        }
        catch (Exception error) { Console.WriteLine(error); }
    }
}
