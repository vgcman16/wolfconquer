using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeLevelExperienceTests
{
    private sealed class Scope : IDisposable
    {
        private readonly Dictionary<LegacyNativeDbLevExp.Sort, Dictionary<byte, LegacyNativeDbLevExp>> priorLevels = LegacyNativePoolLevelInfo.LevelInfo;
        private readonly string priorLocation = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo priorCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo priorUi = CultureInfo.CurrentUICulture;
        private readonly string root = Path.Combine(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(),
            "native-level-info-" + Guid.NewGuid().ToString("N"));
        private readonly Dictionary<string, byte[]?> expected = new(StringComparer.OrdinalIgnoreCase);
        internal string FilePath => Path.Combine(root, "levexp.txt");
        internal string PrefixPath => Path.Combine(root, "prefixlevexp.txt");

        internal Scope()
        {
            GuardRoot(); Assert.False(Directory.Exists(root)); Assert.False(File.Exists(root));
            Directory.CreateDirectory(root);
            expected.Add(FilePath, null); expected.Add(PrefixPath, null);
            LegacyNativePoolLevelInfo.LevelInfo = new();
            LegacyNativeServerConfig.DbLocation = root + Path.DirectorySeparatorChar;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        internal void LiteralPrefix() => LegacyNativeServerConfig.DbLocation = root + Path.DirectorySeparatorChar + "prefix";

        internal void Seed(string text, bool prefix = false, bool unicode = false)
        {
            string path = prefix ? PrefixPath : FilePath;
            Guard(path); Assert.Null(expected[path]); Assert.False(File.Exists(path));
            byte[] bytes = unicode
                ? Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray()
                : new UTF8Encoding(false, true).GetBytes(text);
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes); file.Flush(true);
            }
            expected[path] = bytes;
        }

        private void GuardRoot()
        {
            Assert.Equal(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(), Path.GetDirectoryName(Path.GetFullPath(root)));
            Assert.Matches("^native-level-info-[0-9a-f]{32}$", Path.GetFileName(root));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(root);
        }

        private void Guard(string path)
        {
            GuardRoot(); Assert.True(expected.ContainsKey(path));
            Assert.Equal(root, Path.GetDirectoryName(Path.GetFullPath(path)));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
        }

        public void Dispose()
        {
            LegacyNativePoolLevelInfo.LevelInfo = priorLevels;
            LegacyNativeServerConfig.DbLocation = priorLocation;
            CultureInfo.CurrentCulture = priorCulture;
            CultureInfo.CurrentUICulture = priorUi;
            GuardRoot();
            foreach (string path in Directory.GetFileSystemEntries(root))
                Assert.True(expected.ContainsKey(path) && !Directory.Exists(path), "Unexpected owned entry preserved.");
            // Validate all leaves before deleting any. No original data or recursive cleanup.
            foreach (var pair in expected)
            {
                Guard(pair.Key);
                if (pair.Value is null) Assert.False(File.Exists(pair.Key) || Directory.Exists(pair.Key));
                else Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            }
            foreach (var pair in expected)
                if (pair.Value is not null) File.Delete(pair.Key);
            Directory.Delete(root, recursive: false);
        }
    }
}
