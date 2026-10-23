using System.Globalization;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeClientItemSave
{
    private sealed record SaveLocation(uint Uid, string Configuration, string Root, string Directory, string Path)
    {
        internal static SaveLocation Capture(uint uid)
        {
            string configuration = LegacyNativeServerConfig.DbLocation;
            if (uid == 0 || string.IsNullOrWhiteSpace(configuration))
                throw new InvalidDataException("A native item save requires a real UID and configured data root.");
            string root = System.IO.Path.GetFullPath(configuration);
            string directory = System.IO.Path.Combine(root, "PlayersItems");
            string path = System.IO.Path.Combine(directory, uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            return new(uid, configuration, root, directory, path);
        }

        internal bool IsCurrent(LegacyNativePlayerRuntime player) => player.UID == Uid &&
            string.Equals(LegacyNativeServerConfig.DbLocation, Configuration, StringComparison.Ordinal) &&
            string.Equals(System.IO.Path.GetFullPath(Configuration), Root,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        internal void EnsureSafe()
        {
            if (!System.IO.Directory.Exists(Directory)) throw new DirectoryNotFoundException("Native PlayersItems directory is missing.");
            RejectReparseAncestors(Directory);
            RejectUnsafeLeaf(Path);
        }

        internal string NewStagePath() => System.IO.Path.Combine(Directory,
            "." + System.IO.Path.GetFileName(Path) + ".native-save-" + Guid.NewGuid().ToString("N") + ".tmp");

        internal void EnsureSafeStage(string stage)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(stage)), Directory, comparison))
                throw new IOException("Native item stage escaped its owned directory.");
            string name = System.IO.Path.GetFileName(stage);
            string prefix = "." + System.IO.Path.GetFileName(Path) + ".native-save-";
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(".tmp", StringComparison.Ordinal) ||
                name.Length != prefix.Length + 32 + 4 ||
                !Guid.TryParseExact(name.Substring(prefix.Length, 32), "N", out _))
                throw new IOException("Unrecognized native item stage.");
            RejectReparseAncestors(Directory);
            RejectUnsafeLeaf(stage);
        }

        private static void RejectUnsafeLeaf(string path)
        {
            try
            {
                if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                    throw new IOException("Native item target is not a regular file.");
            }
            catch (FileNotFoundException) { }
        }

        private static void RejectReparseAncestors(string directory)
        {
            for (var cursor = new DirectoryInfo(directory); cursor is not null; cursor = cursor.Parent)
                if ((cursor.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Native item path traverses a reparse point.");
        }
    }
}
