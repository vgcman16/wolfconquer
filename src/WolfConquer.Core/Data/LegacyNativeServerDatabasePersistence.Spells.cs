#nullable disable
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native spell-file methods only, not SaveClient or an ExecuteLogin worker.
internal static partial class LegacyNativeServerDatabasePersistence
{
    public unsafe static void LoadClientSpells(LegacyNativeGameClientRuntime client)
    {
        try
        {
            LegacyNativeItemRecordFile binaryFile = new LegacyNativeItemRecordFile();
            if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\PlayersSpells\\" + client.Player.UID + ".bin", FileMode.Open))
            {
                return;
            }
            LoadClientSpellsOpenedFile(binaryFile, client);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    internal unsafe static void LoadClientSpellsOpenedFile(
        LegacyNativeItemRecordFile binaryFile, LegacyNativeGameClientRuntime client)
    {
        int num = default(int);
        binaryFile.ReadCount(ref num);
        LegacyNativeClientSpells.DBSpell dBSpell = default(LegacyNativeClientSpells.DBSpell);
        for (int i = 0; i < num; i++)
        {
            binaryFile.ReadSpell(ref dBSpell);
            LegacyNativeMsgSpell clientSpell = dBSpell.GetClientSpell();
            if (LegacyNativePoolMagic.Magic.TryGetValue(clientSpell.ID, out var value) && value.Count > clientSpell.Level)
            {
                if (LegacyNativePoolMagic.Magic.ContainsKey(clientSpell.ID) && LegacyNativePoolMagic.Magic[clientSpell.ID].ContainsKey(clientSpell.Level))
                {
                    LegacyNativeMagicType.Magic magic = LegacyNativePoolMagic.Magic[clientSpell.ID][clientSpell.Level];
                    if (magic != null && magic.isRuneSkill)
                    {
                        uint runeFromSkill = LegacyNativeRuneItemStorage.GetRuneFromSkill(clientSpell);
                        if (!LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(LegacyNativePoolItems.ItemsBase[runeFromSkill], client))
                        {
                            continue;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("ERORR [SPELL LOAD] Id: {0} Level {1}", clientSpell.ID, clientSpell.Level);
                }
            }
            client.MySpells.ClientSpells.TryAdd(clientSpell.ID, clientSpell);
        }
        binaryFile.Close();
    }

    public unsafe static void SaveClientSpells(LegacyNativeGameClientRuntime client)
    {
        LegacyNativeItemRecordFile binaryFile = new LegacyNativeItemRecordFile();
        if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\PlayersSpells\\" + client.Player.UID + ".bin", FileMode.Create))
        {
            return;
        }
        SaveClientSpellsOpenedFile(binaryFile, client);
    }

    internal unsafe static void SaveClientSpellsOpenedFile(
        LegacyNativeItemRecordFile binaryFile, LegacyNativeGameClientRuntime client)
    {
        LegacyNativeClientSpells.DBSpell dBSpell = default(LegacyNativeClientSpells.DBSpell);
        int count = client.MySpells.ClientSpells.Count;
        binaryFile.WriteCount(ref count);
        foreach (LegacyNativeMsgSpell value in client.MySpells.ClientSpells.Values)
        {
            dBSpell.GetDBSpell(value);
            binaryFile.WriteSpell(ref dBSpell);
        }
        binaryFile.Close();
    }
}
