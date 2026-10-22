namespace WolfConquer.Core.World;

// A host phase dependency, NOT an INI parser or a registered WorldServer service.
// An actual adapter must preserve native IniFile read/write behavior independently.
public interface ILegacyNativeItemCounterShellIni
{
    uint ReadUInt32(string section, string key);

    void WriteUInt32(string section, string key, uint value);
}

// These explicit phases have no call site in the current WorldServer host.
// Their owner must supply the exact native prefix/tail and shell.ini factory;
// adding this component does not prove full host Initialize/SaveDatabase parity.
public static class LegacyNativeItemCounterShellLifecycle
{
    public static void InitializeAtNativeShellPhase(ILegacyNativeItemCounterShellIni shellIni)
    {
        // Do NOT pre-read the uint or call InitializeFromShellItemUid(read(...)):
        // native Server.cs:190 captures ItemCounter BEFORE evaluating the INI read.
        LegacyNativePoolItems.ItemCounter.Set(shellIni.ReadUInt32("Database", "ItemUID"));

        // Native line191 reads the shared mutable field again, not the Set receiver.
        _ = LegacyNativePoolItems.ItemCounter.Next;
    }

    public static void SaveAtNativeShellPhase(
        bool fullLoading,
        Action saveEarlierDatabaseOwners,
        Func<ILegacyNativeItemCounterShellIni> createShellIni,
        Action<ILegacyNativeItemCounterShellIni> saveRemainingShellValues,
        Action<Exception> reportException)
    {
        // Native FullLoading gate precedes the save try and ALL dependency use.
        if (!fullLoading)
        {
            return;
        }

        try
        {
            // Native Server.cs:756..790. The owner supplies both direct name-change
            // calls and all 33 individually Save-wrapped calls in their exact order.
            saveEarlierDatabaseOwners();

            // Native lines791..792 construct/configure shell.ini only after that prefix.
            var shellIni = createShellIni();
            shellIni.WriteUInt32("Database", "ItemUID", LegacyNativePoolItems.ItemCounter.Count);

            // Native lines794..797 follow the ItemUID write; not implemented here.
            saveRemainingShellValues(shellIni);
        }
        catch (Exception exception)
        {
            // The outer native save catch reports once; reporter failures propagate.
            reportException(exception);
        }
    }
}
