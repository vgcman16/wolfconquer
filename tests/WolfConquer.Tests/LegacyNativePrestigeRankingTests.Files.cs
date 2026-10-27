using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    private sealed class OwnedRankFile : IDisposable
    {
        private readonly string previous = LegacyNativeServerConfig.DbLocation;
        private readonly Dictionary<string, byte[]?> expected = new(StringComparer.OrdinalIgnoreCase);
        internal string Root { get; }
        internal string PathFor(bool prefix = false) => Path.Combine(Root, (prefix ? "prefix" : "") + "PrestigeRanking.txt");

        internal OwnedRankFile()
        {
            Root = Path.Combine(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(),
                "native-prestige-rank-" + Guid.NewGuid().ToString("N"));
            GuardRoot();
            Assert.False(File.Exists(Root) || Directory.Exists(Root));
            Directory.CreateDirectory(Root);
            expected.Add(PathFor(), null);
            expected.Add(PathFor(true), null);
            UseLocation();
        }

        internal void UseLocation(bool prefix = false) =>
            LegacyNativeServerConfig.DbLocation = Root + Path.DirectorySeparatorChar + (prefix ? "prefix" : "");

        internal void Seed(string text, bool prefix = false) => SeedBytes(Encoding.UTF8.GetBytes(text), prefix);

        internal void SeedBytes(byte[] bytes, bool prefix = false)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            string path = PathFor(prefix);
            Guard(path);
            Assert.Null(expected[path]);
            Assert.False(File.Exists(path) || Directory.Exists(path));
            var ownedBytes = (byte[])bytes.Clone();
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(ownedBytes);
                file.Flush(true);
            }
            expected[path] = ownedBytes;
        }

        internal void Expect(string text, bool prefix = false)
        {
            string path = PathFor(prefix);
            Guard(path);
            expected[path] = Encoding.UTF8.GetBytes(text);
            Assert.Equal(expected[path], File.ReadAllBytes(path));
        }

        private void GuardRoot()
        {
            Assert.Equal(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(), Path.GetDirectoryName(Path.GetFullPath(Root)));
            Assert.Matches("^native-prestige-rank-[0-9a-f]{32}$", Path.GetFileName(Root));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(Root);
        }

        private void Guard(string path)
        {
            GuardRoot();
            Assert.True(expected.ContainsKey(path));
            Assert.Equal(Root, Path.GetDirectoryName(Path.GetFullPath(path)));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
        }

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = previous;
            GuardRoot();
            foreach (string path in Directory.GetFileSystemEntries(Root))
                Assert.True(expected.ContainsKey(path) && !Directory.Exists(path), "Unexpected owned entry is preserved.");
            foreach (var pair in expected)
            {
                Guard(pair.Key);
                if (pair.Value is null) Assert.False(File.Exists(pair.Key) || Directory.Exists(pair.Key));
                else Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            }
            foreach (var pair in expected)
                if (pair.Value is not null) File.Delete(pair.Key);
            Directory.Delete(Root, recursive: false);
        }
    }

    private static string Text(params string[] rows) =>
        "Count=" + rows.Length + Environment.NewLine + string.Concat(rows.Select(row => row + Environment.NewLine));
}
