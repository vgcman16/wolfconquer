using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    public static void LoadClientSpells(LegacyNativeGameClientRuntime client)
    {
        try
        {
            SpellFileOwner? owner = CaptureSpellFileOwner(client);
            var file = new LegacyNativeItemRecordFile();
            if (!file.Open(LegacyNativeServerConfig.DbLocation + "\\PlayersSpells\\" + owner.Uid + ".bin", FileMode.Open)) return;
            var rows = ReadCompleteSpellFile(file, client, ref owner);
            PublishLoadedSpellRows(owner!, rows);
        }
        catch (Exception error) { Console.WriteLine(error.ToString()); }
    }

    internal static void LoadClientSpellsOpenedFile(LegacyNativeItemRecordFile binaryFile, LegacyNativeGameClientRuntime client)
    {
        SpellFileOwner? owner = null;
        var rows = ReadCompleteSpellFile(binaryFile, client, ref owner);
        PublishLoadedSpellRows(owner!, rows);
    }

    public static void SaveClientSpells(LegacyNativeGameClientRuntime client)
    {
        var owner = CaptureSpellFileOwner(client);
        var rows = CaptureSpellSaveRows(owner); // Before Create can truncate any destination.
        var file = new LegacyNativeItemRecordFile();
        if (!file.Open(LegacyNativeServerConfig.DbLocation + "\\PlayersSpells\\" + owner.Uid + ".bin", FileMode.Create)) return;
        WriteCompleteSpellRows(file, rows);
    }

    internal static void SaveClientSpellsOpenedFile(LegacyNativeItemRecordFile binaryFile, LegacyNativeGameClientRuntime client)
    {
        LegacyNativeClientSpells.DBSpell[] rows;
        Exception? primary = null;
        try { rows = CaptureSpellSaveRows(CaptureSpellFileOwner(client)); }
        catch (Exception error) { primary = error; throw; }
        finally { if (primary is not null) CloseOwnedSpellFile(binaryFile, primary); }
        WriteCompleteSpellRows(binaryFile, rows);
    }
}
