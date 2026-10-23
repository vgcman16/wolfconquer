using System.Runtime.ExceptionServices;

namespace WolfConquer.Tests;

public sealed partial class WindowsJournalRootIdentityTests
{
    private const string Prefix = "wolf-journal-root-identity-";

    private static void Run(Action<OwnedDirectories> action)
    {
        var fixture = new OwnedDirectories();
        Exception? primary = null;
        try { action(fixture); }
        catch (Exception error) { primary = error; }
        try { fixture.Dispose(); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException(primary, cleanup);
            throw;
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }

    private sealed class OwnedDirectories : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(root, "Users");
        internal string Items => Path.Combine(root, "PlayersItems");
        internal string Quests => Path.Combine(root, "Quests");
        internal string Child => Path.Combine(Users, "ActualChild");
        internal string Missing => Path.Combine(root, "Missing");

        internal OwnedDirectories()
        {
            RequireOwnedRoot();
            Directory.CreateDirectory(Users);
            Directory.CreateDirectory(Items);
            Directory.CreateDirectory(Quests);
            Directory.CreateDirectory(Child);
        }

        private void RequireOwnedRoot()
        {
            var full = Path.GetFullPath(root);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(full));
            Assert.StartsWith(Prefix, Path.GetFileName(full));
            Assert.True(Guid.TryParseExact(Path.GetFileName(full)[Prefix.Length..], "N", out _));
            for (string? path = full; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
                if (Directory.Exists(path) || File.Exists(path))
                    Assert.Equal(0, (int)(File.GetAttributes(path) & FileAttributes.ReparsePoint));
        }

        public void Dispose()
        {
            RequireOwnedRoot();
            DeleteOrdinary(root);
        }

        private void DeleteOrdinary(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                Assert.StartsWith(root + Path.DirectorySeparatorChar, Path.GetFullPath(entry), StringComparison.OrdinalIgnoreCase);
                Assert.Equal(0, (int)(File.GetAttributes(entry) & FileAttributes.ReparsePoint));
                if (Directory.Exists(entry)) DeleteOrdinary(entry); else File.Delete(entry);
            }
            Directory.Delete(directory, recursive: false);
        }
    }
}
