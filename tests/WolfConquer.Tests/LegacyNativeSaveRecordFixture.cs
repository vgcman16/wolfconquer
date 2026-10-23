using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed class NativeSaveRecordTables : IDisposable
{
    private readonly LegacyNativeActivityTaskTable activity = LegacyNativePoolSaveRecords.ActivityTasks;
    private readonly LegacyNativeInfoHeroRewardTable hero = LegacyNativePoolSaveRecords.TableHeroRewards;
    internal NativeSaveRecordTables()
    {
        LegacyNativePoolSaveRecords.ActivityTasks = new();
        LegacyNativePoolSaveRecords.TableHeroRewards = new();
    }
    public void Dispose()
    {
        LegacyNativePoolSaveRecords.ActivityTasks = activity;
        LegacyNativePoolSaveRecords.TableHeroRewards = hero;
    }
}

internal sealed class NativeSaveRecordFiles : IDisposable
{
    private readonly string prior = LegacyNativeServerConfig.DbLocation;
    private readonly string temp = Path.GetFullPath(Path.GetTempPath());
    internal string Root { get; }
    internal NativeSaveRecordFiles()
    {
        Root = Path.GetFullPath(Path.Combine(temp, "wolf-save-record-" + Guid.NewGuid().ToString("N")));
        if (Directory.Exists(Root) || File.Exists(Root)) throw new IOException("Owned fixture path exists.");
        Directory.CreateDirectory(Root);
        LegacyNativeServerConfig.DbLocation = Root + Path.DirectorySeparatorChar;
    }
    public void Dispose()
    {
        LegacyNativeServerConfig.DbLocation = prior;
        if (!string.Equals(Path.GetDirectoryName(Root)?.TrimEnd('\\', '/'), temp.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(Root).StartsWith("wolf-save-record-", StringComparison.Ordinal)
            || Path.GetFileName(Root).Length != "wolf-save-record-".Length + 32)
            throw new InvalidOperationException("Refusing cleanup outside the GUID save-record fixture.");
        Directory.Delete(Root, true);
    }
}
