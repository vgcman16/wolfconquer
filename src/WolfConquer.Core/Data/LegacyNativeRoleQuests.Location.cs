using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeRoleQuests
{
    private sealed record QuestLocation(uint Uid, string Configuration, string Directory, string Path)
    {
        internal static QuestLocation Capture(uint uid)
        {
            string configuration = LegacyNativeServerConfig.DbLocation;
            if (uid == 0 || string.IsNullOrWhiteSpace(configuration) || !System.IO.Path.IsPathFullyQualified(configuration))
                throw new InvalidDataException("A quest file requires an explicit absolute data root and UID.");
            string directory = System.IO.Path.Combine(System.IO.Path.GetFullPath(configuration), "Quests");
            return new(uid, configuration, directory, System.IO.Path.Combine(directory,
                uid.ToString(CultureInfo.InvariantCulture) + ".bin"));
        }

        internal bool IsCurrent(LegacyNativePlayerRuntime player) => player.UID == Uid &&
            string.Equals(LegacyNativeServerConfig.DbLocation, Configuration, StringComparison.Ordinal);

        internal void EnsureSafe()
        {
            for (var cursor = new DirectoryInfo(Directory); cursor is not null; cursor = cursor.Parent)
            {
                // DirectoryInfo.Attributes can expose the -1 missing sentinel;
                // GetAttributes supplies an actual not-found exception instead.
                var attributes = File.GetAttributes(cursor.FullName);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Quest path traverses a reparse point.");
                if ((attributes & FileAttributes.Directory) == 0)
                    throw new IOException("Quest parent is not a directory.");
            }
            RejectUnsafeLeaf(Path);
        }

        internal string NewStagePath() => System.IO.Path.Combine(Directory,
            "." + System.IO.Path.GetFileName(Path) + ".quest-save-" + Guid.NewGuid().ToString("N") + ".tmp");

        internal void EnsureSafeStage(string stage)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            string prefix = "." + System.IO.Path.GetFileName(Path) + ".quest-save-";
            string name = System.IO.Path.GetFileName(stage);
            if (!string.Equals(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(stage)), Directory, comparison) ||
                !name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(".tmp", StringComparison.Ordinal) ||
                name.Length != prefix.Length + 32 + 4 || !Guid.TryParseExact(name.Substring(prefix.Length, 32), "N", out _))
                throw new IOException("Quest stage is not this owner's generated leaf.");
            EnsureSafe();
            RejectUnsafeLeaf(stage);
        }

        private static void RejectUnsafeLeaf(string path)
        {
            try
            {
                if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                    throw new IOException("Quest target is not a regular file.");
            }
            catch (FileNotFoundException) { }
        }
    }
}
