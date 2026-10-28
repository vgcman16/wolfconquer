using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    public static void LoadClientSpells(LegacyNativeGameClientRuntime client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var binding = LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(client);
        if (binding is not null) { LoadJoinedSpells(binding); return; }
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
        RequireUnboundOpenedSpellFile(binaryFile, client);
        SpellFileOwner? owner = null;
        var rows = ReadCompleteSpellFile(binaryFile, client, ref owner);
        PublishLoadedSpellRows(owner!, rows);
    }

    public static void SaveClientSpells(LegacyNativeGameClientRuntime client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var binding = LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(client);
        if (binding is not null) { SaveJoinedSpells(binding); return; }
        var owner = CaptureSpellFileOwner(client);
        lock (client.SpellAdmissionSyncRoot)
        lock (owner.Player.SpellAdmissionSyncRoot)
        {
            if (LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(client) is not null)
                throw new InvalidOperationException("Spell save owner was admitted during unbound preparation.");
            var rows = CaptureSpellSaveRows(owner);
            var file = new LegacyNativeItemRecordFile();
            if (!file.Open(LegacyNativeServerConfig.DbLocation + "\\PlayersSpells\\" + owner.Uid + ".bin", FileMode.Create)) return;
            WriteCompleteSpellRows(file, rows);
        }
    }

    internal static void SaveClientSpellsOpenedFile(LegacyNativeItemRecordFile binaryFile, LegacyNativeGameClientRuntime client)
    {
        ArgumentNullException.ThrowIfNull(client);
        RequireUnboundOpenedSpellFile(binaryFile, client);
        Exception? primary = null;
        bool writerOwnsFile = false;
        LegacyNativeClientSpells.DBSpell[] rows;
        try
        {
            var owner = CaptureSpellFileOwner(client);
            lock (client.SpellAdmissionSyncRoot)
            lock (owner.Player.SpellAdmissionSyncRoot)
            {
                if (LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(client) is not null)
                    throw new InvalidOperationException("Opened spell save owner became bound during preparation.");
                rows = CaptureSpellSaveRows(owner);
                writerOwnsFile = true;
                WriteCompleteSpellRows(binaryFile, rows);
            }
        }
        catch (Exception error) { primary = error; throw; }
        finally { if (primary is not null && !writerOwnsFile) CloseOwnedSpellFile(binaryFile, primary); }
    }

    private static void RequireUnboundOpenedSpellFile(LegacyNativeItemRecordFile ownedFile,
        LegacyNativeGameClientRuntime client)
    {
        try
        {
            if (LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(client) is not null)
                throw new InvalidOperationException("A bound spell owner uses only its configured root, not an arbitrary opened file.");
        }
        catch (Exception error) { CloseOwnedSpellFile(ownedFile, error); throw; }
    }
}
