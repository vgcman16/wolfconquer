namespace WolfConquer.Core.World;

// Explicit composition only: no live host, global FullLoading producer or save owners.
// The future native-equivalent host must supply Directory.GetCurrentDirectory and
// the live DbLocation getter, not a cached path or normalized Path.Combine result.
public static class LegacyNativeItemCounterShellComposition
{
    public static LegacyNativeItemCounterShellIni OpenForInitialization(
        Func<string> currentDirectory,
        Func<string?> databaseLocation)
    {
        // Server.cs:170 opens once BEFORE the intervening171..189 startup owners.
        // Override prevents evaluation of DbLocation; callers must retain this object
        // and later pass it to InitializeAtNativeShellPhase at native lines190..191.
        return new LegacyNativeItemCounterShellIni(
            currentDirectory() + "\\shell.ini", databaseLocation, overridePath: true);
    }

    public static LegacyNativeItemCounterShellIni CreateForSave(
        Func<string> currentDirectory,
        Func<string?> databaseLocation)
    {
        // Server.cs:791 must evaluate DbLocation through new IniFile("") even though
        // line792 overwrites FileName. Its side effects/failures precede directory use.
        var shellIni = new LegacyNativeItemCounterShellIni("", databaseLocation);
        shellIni.FileName = currentDirectory() + "\\shell.ini";
        return shellIni;
    }

    public static void SaveAtNativeShellPhase(
        bool fullLoading,
        Action saveEarlierDatabaseOwners,
        Func<string> currentDirectory,
        Func<string?> databaseLocation,
        Action<ILegacyNativeItemCounterShellIni> saveRemainingShellValues,
        Action<Exception> reportException)
    {
        // No eager factory/getter evaluation before the existing FullLoading gate
        // or prefix. Real prefix35 and remaining shell-write owners are REQUIRED.
        LegacyNativeItemCounterShellLifecycle.SaveAtNativeShellPhase(
            fullLoading,
            saveEarlierDatabaseOwners,
            () => CreateForSave(currentDirectory, databaseLocation),
            saveRemainingShellValues,
            reportException);
    }
}
