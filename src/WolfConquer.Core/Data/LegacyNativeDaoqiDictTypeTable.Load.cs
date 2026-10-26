namespace WolfConquer.Core.Data;

internal partial class LegacyNativeDaoqiDictTypeTable
{
    public static void Load(string DBLocation)
    {
        try
        {
            lock (LegacyNativeCombatGearTable.MailboxLoadGate)
            {
                if (!LegacyNativeCombatGearTable.m_Load) return;
                var prepared = LegacyNativeCombatGearTable.MailboxLoadStage.ReadJade(DBLocation + "daoqi_dict_type.txt");
                Info = prepared;
            }
        }
        catch (Exception error) { Console.WriteLine(error); }
    }
}
