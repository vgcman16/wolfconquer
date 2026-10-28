namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    internal sealed partial class NativeSpellJoinFixture
    {
        public async ValueTask DisposeAsync()
        {
            var errors = new List<Exception>();
            var closed = true;
            foreach (var connection in connections)
            {
                try { await connection.CompleteTransportCloseAsync(); }
                catch (Exception error) { closed = false; errors.Add(error); }
            }
            try { admission?.Dispose(); }
            catch (Exception error) { closed = false; errors.Add(error); }
            // This fixture disables runtime update workers and awaits every
            // handler call. Restore only our serialized metadata reference.
            try { beasts?.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { Log.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (!closed) RetainRoot = true;
            if (ownsRoot && !RetainRoot)
            {
                try
                {
                    var path = ValidateRoot();
                    RequireOrdinaryTree(path);
                    Directory.Delete(path, recursive: true);
                }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count != 0)
                throw new AggregateException($"Native spell owner cleanup failed; retained {Root}.", errors);
        }

        private string ValidateRoot()
        {
            var absolute = Path.GetFullPath(Root);
            Assert.True(Path.IsPathFullyQualified(Root));
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(absolute));
            const string prefix = "wolf-world-handler-";
            var name = Path.GetFileName(absolute);
            Assert.StartsWith(prefix, name);
            Assert.True(Guid.TryParseExact(name[prefix.Length..], "N", out _));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(absolute);
            return absolute;
        }

        private static void RequireOrdinaryTree(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.TryPop(out var path))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException($"Unexpected reparse path in owned spell fixture: {path}");
                if ((attributes & FileAttributes.Directory) != 0)
                    foreach (var child in Directory.GetFileSystemEntries(path)) pending.Push(child);
            }
        }
    }
}
