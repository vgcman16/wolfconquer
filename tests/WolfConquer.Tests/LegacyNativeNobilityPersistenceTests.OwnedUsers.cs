using System.Text;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityPersistenceTests
{
    private sealed class OwnedUsers : IDisposable
    {
        private readonly string parent = LegacyNativeDbReadOwnedFiles.ArtifactsRoot();
        private readonly Dictionary<string, byte[]> seeds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> directories = new(StringComparer.OrdinalIgnoreCase);
        internal string Root { get; }
        internal string Users => Path.Combine(Root, "Users");

        internal OwnedUsers(bool createUsers)
        {
            Assert.True(OperatingSystem.IsWindows(), "Actual native INI reads require Windows.");
            Root = Path.Combine(parent, "native-nobility-persistence-" + Guid.NewGuid().ToString("N"));
            Guard(); Assert.False(File.Exists(Root)); Assert.False(Directory.Exists(Root));
            Directory.CreateDirectory(Root); directories.Add(Root);
            if (createUsers) Create(Users);
        }

        internal string Prefix()
        {
            string prefix = Path.Combine(Root, "prefix");
            Create(prefix); Create(Path.Combine(prefix, "Users"));
            return prefix;
        }

        private void Create(string path)
        {
            Assert.True(path == Users || path == Path.Combine(Root, "prefix") || path == Path.Combine(Root, "prefix", "Users"));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
            Directory.CreateDirectory(path); directories.Add(path);
        }

        internal string Seed(string name, string text, string? prefix = null)
        {
            Assert.Matches("^[a-z0-9][a-z0-9_.-]*$", name);
            string directory = prefix == null ? Users : Path.Combine(prefix, "Users");
            Assert.Contains(directory, directories);
            string path = Path.GetFullPath(Path.Combine(directory, name));
            Assert.Equal(directory, Path.GetDirectoryName(path));
            Assert.False(File.Exists(path));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
            byte[] bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(true); }
            seeds.Add(path, bytes);
            return path;
        }

        internal void RewriteBeforeRead(string path, string text)
        {
            Assert.Contains(path, seeds.Keys);
            Assert.Equal(seeds[path], File.ReadAllBytes(path));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
            byte[] bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();
            using (var file = new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(true); }
            seeds[path] = bytes;
        }

        internal void Unchanged()
        {
            foreach (var (path, bytes) in seeds)
            { LegacyNativeDbReadOwnedFiles.OrdinaryChain(path); Assert.Equal(bytes, File.ReadAllBytes(path)); }
        }

        private void Guard()
        {
            Assert.Equal(parent, Path.GetDirectoryName(Path.GetFullPath(Root)));
            Assert.Matches("^native-nobility-persistence-[0-9a-f]{32}$", Path.GetFileName(Root));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(Root);
        }

        public void Dispose()
        {
            Guard(); Unchanged();
            // Validate the entire finite manifest before deleting any owned leaf.
            foreach (string directory in directories)
            {
                LegacyNativeDbReadOwnedFiles.OrdinaryChain(directory);
                foreach (string entry in Directory.GetFileSystemEntries(directory))
                    Assert.True(seeds.ContainsKey(entry) || directories.Contains(entry), "Unexpected owned entry preserved.");
            }
            foreach (string path in seeds.Keys) File.Delete(path);
            foreach (string directory in directories.OrderByDescending(path => path.Length)) Directory.Delete(directory, false);
        }
    }
}
