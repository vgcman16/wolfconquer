using System.Globalization;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPerformerDanceLearningTests
{
    private static async Task WithDurableDanceAsync(Func<DurableDanceFixture, Task> body)
    {
        var fixture = new DurableDanceFixture();
        Exception? primary = null;
        try { fixture.Initialize(); await body(fixture); }
        catch (Exception error) { primary = error; }
        try { await fixture.DisposeAsync(); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException($"Performer fixture retained at {fixture.Root}.", primary, cleanup);
            ExceptionDispatchInfo.Capture(cleanup).Throw();
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }

    private sealed class DurableDanceFixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-performer-dance-durable-" + Guid.NewGuid().ToString("N"));
        internal string SpellsRoot => Path.Combine(Root, "spells");
        internal List<byte[]> Sent { get; } = [];
        internal StringWriter Log { get; } = new();
        private readonly List<ConquerTcpConnection> connections = [];
        private readonly List<WorldSessionStore> stores = [];
        private readonly Dictionary<string, byte[]> expected = new(StringComparer.OrdinalIgnoreCase);
        private bool ownsRoot;

        internal void Initialize()
        {
            Guard();
            Assert.False(Directory.Exists(Root) || File.Exists(Root));
            Directory.CreateDirectory(Root); ownsRoot = true;
            Directory.CreateDirectory(SpellsRoot);
        }

        internal ConquerTcpConnection Connection()
        {
            var connection = CreateSendingConnection(Sent);
            connections.Add(connection);
            return connection;
        }

        internal WorldSessionStore Store()
        {
            var store = new WorldSessionStore(); // No substitute persistence callback.
            stores.Add(store);
            return store;
        }

        internal string SpellPath(long uid) => Path.Combine(SpellsRoot, uid.ToString(CultureInfo.InvariantCulture) + ".bin");

        internal void Seed(long uid, PlayerSpellSnapshot spells)
        {
            Guard();
            var path = SpellPath(uid);
            Assert.False(expected.ContainsKey(path) || File.Exists(path));
            new PlayerSpellSnapshotLoader(SpellsRoot).SaveByCharacterUid(uid, spells);
            expected.Add(path, File.ReadAllBytes(path));
        }

        internal byte[] Bytes(long uid) => File.ReadAllBytes(SpellPath(uid));

        internal void SeedOpaqueByte(long uid, byte opaque)
        {
            var path = SpellPath(uid);
            Assert.True(expected.ContainsKey(path));
            Guard();
            var image = Bytes(uid);
            image[4 + 11] = opaque; // First genuine 12-byte row's unmodeled final byte.
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            { file.Write(image); file.Flush(true); }
            expected[path] = image.ToArray();
        }

        internal void Expect(long uid, byte[] bytes)
        {
            var path = SpellPath(uid);
            Assert.True(expected.ContainsKey(path));
            expected[path] = bytes.ToArray();
            Assert.Equal(bytes, Bytes(uid));
        }

        internal void RequireFilesUnchanged()
        {
            Guard();
            Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), Directory.GetFiles(SpellsRoot).Order(StringComparer.Ordinal));
            Assert.Empty(Directory.GetDirectories(SpellsRoot));
            foreach (var file in expected) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
        }

        internal ValueTask<bool> InvokeAsync(ConquerTcpConnection connection, WorldSessionStore store, WorldSession expectedSession,
            byte optionId = 7) =>
            WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
                new WorldPacketHandlerContext(connection, Log, Session: expectedSession, Sessions: store, PlayerSpellsRoot: SpellsRoot),
                expectedSession, PerformerNpcId, optionId, CancellationToken.None);

        public async ValueTask DisposeAsync()
        {
            var failures = new List<Exception>();
            foreach (var connection in connections)
            {
                try { await connection.CompleteTransportCloseAsync(); }
                catch (Exception error) { failures.Add(error); }
            }
            foreach (var store in stores)
            foreach (var connection in connections)
            {
                try { store.TryRemove(connection, out _); }
                catch (Exception error) { failures.Add(error); }
            }
            if (failures.Count != 0)
                throw new AggregateException($"Performer owner closure unproven; retained {Root}.", failures);
            try
            {
                if (!ownsRoot) return;
                RequireFilesUnchanged();
                Assert.Equal(new[] { SpellsRoot }, Directory.GetFileSystemEntries(Root));
                foreach (var path in expected.Keys) File.Delete(path);
                Directory.Delete(SpellsRoot, recursive: false);
                Directory.Delete(Root, recursive: false);
            }
            finally { Log.Dispose(); }
        }

        private void Guard()
        {
            Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(Path.GetFullPath(Root)));
            Assert.Matches("^wolf-performer-dance-durable-[0-9a-f]{32}$", Path.GetFileName(Root));
            Assert.Equal(Root, Path.GetDirectoryName(Path.GetFullPath(SpellsRoot)));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(Root);
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(SpellsRoot);
            foreach (var path in expected.Keys)
            {
                Assert.Equal(SpellsRoot, Path.GetDirectoryName(Path.GetFullPath(path)));
                LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
            }
        }
    }
}
