using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    // A sibling of the outer fixture root: failed closure must not reach its
    // unconditional class-level cleanup. Only real transports/issuers are used.
    private sealed class TargetOwnedSpellStartupOwner : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(),
            "wolf-world-handler-" + Guid.NewGuid().ToString("N"));
        internal readonly StringWriter Log = new();
        internal readonly List<byte[]> Sent = [];
        internal OrdinaryLoginFixture? Admission { get; private set; }
        internal WorldServerFrameHandler? Handler { get; private set; }
        internal ConquerTcpConnection? Connection { get; private set; }
        private bool rootCreated;

        internal string CreateUsers()
        {
            var root = ValidatedRoot();
            if (Directory.Exists(root) || File.Exists(root))
                throw new InvalidOperationException("The target-spell fixture root already exists.");
            Directory.CreateDirectory(root);
            rootCreated = true;
            var users = Path.Combine(root, "Users");
            Directory.CreateDirectory(users);
            return users;
        }

        internal void Bind(CharacterSnapshotLoader loader)
        {
            Admission = new OrdinaryLoginFixture(loader);
            Handler = Admission.CreateHandler(Log);
            Connection = CreateSendingConnection(Sent);
        }

        public async ValueTask DisposeAsync()
        {
            var errors = new List<Exception>();
            var closed = true;
            try { if (Connection is not null) await Connection.CompleteTransportCloseAsync(); }
            catch (Exception error) { closed = false; errors.Add(error); }
            try { Admission?.Dispose(); }
            catch (Exception error) { closed = false; errors.Add(error); }
            try { Log.Dispose(); } catch (Exception error) { errors.Add(error); }
            try
            {
                var root = ValidatedRoot();
                if (!closed)
                    throw new InvalidOperationException($"Target-spell fixture retained at '{root}'; transport/admission closure was not confirmed.");
                if (rootCreated && Directory.Exists(root))
                {
                    RequireNoReparseTree(root);
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("Target-spell fixture cleanup failed.", errors);
        }

        private string ValidatedRoot()
        {
            var root = Path.GetFullPath(Root);
            var prefix = "wolf-world-handler-";
            var name = Path.GetFileName(root);
            if (!string.Equals(Path.GetDirectoryName(root), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                    StringComparison.OrdinalIgnoreCase) || !name.StartsWith(prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(name[prefix.Length..], "N", out _))
                throw new InvalidOperationException("Invalid owned target-spell fixture cleanup path.");
            return root;
        }

        private static void RequireNoReparseTree(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count != 0)
            {
                var path = pending.Pop();
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException($"Target-spell fixture retained unexpected reparse path '{path}'.");
                if ((attributes & FileAttributes.Directory) != 0)
                    foreach (var child in Directory.EnumerateFileSystemEntries(path)) pending.Push(child);
            }
        }
    }
}
