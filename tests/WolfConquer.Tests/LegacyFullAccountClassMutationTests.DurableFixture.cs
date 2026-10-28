using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyFullAccountClassMutationTests
{
    internal static async Task WithDurableClassAsync(Func<DurableClassFixture, Task> body, long level = 140)
    {
        var fixture = new DurableClassFixture();
        Exception? primary = null;
        try { fixture.Initialize(level); await body(fixture); }
        catch (Exception error) { primary = error; }
        try { await fixture.DisposeAsync(); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException($"FullAccount fixture retained at {fixture.Root}.", primary, cleanup);
            ExceptionDispatchInfo.Capture(cleanup).Throw();
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }

    internal sealed partial class DurableClassFixture : IAsyncDisposable
    {
        internal const long DurableAccountId = AccountId + 73; // Account file and character UID are independent keys.
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-fullaccount-durable-class-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(Root, "Users");
        internal string SpellsRoot => Path.Combine(Root, "PlayerSpells");
        internal string CharacterPath => Path.Combine(Users, DurableAccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        internal string SpellPath => Path.Combine(SpellsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string JournalRoot => Path.Combine(Users, ".managed-trade-journal");
        internal string PendingPath => Path.Combine(JournalRoot, "pending.json");
        internal List<byte[]> Sent { get; } = [];
        internal StringWriter Log { get; } = new();
        internal bool Disconnected { get; private set; }
        internal WorldSession Original { get; private set; } = null!;
        internal ConquerTcpConnection Connection { get; private set; } = null!;
        internal WorldSessionStore Store { get; private set; } = null!;
        internal LegacyTradeRecoveryState Recovery { get; private set; } = null!;
        internal LegacyTradeStateStore Trades { get; } = new();
        internal WorldOperationBarrier Operations { get; } = new();
        private readonly List<ConquerTcpConnection> connections = [];
        private readonly List<WorldSessionStore> stores = [];
        private readonly Dictionary<string, byte[]> expected = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> ownedSpellStages = new(StringComparer.OrdinalIgnoreCase);
        private bool ownsRoot;

        internal void Initialize(long level = 140)
        {
            Guard();
            Assert.False(Directory.Exists(Root) || File.Exists(Root));
            Directory.CreateDirectory(Root); ownsRoot = true;
            Directory.CreateDirectory(Users);
            Directory.CreateDirectory(SpellsRoot);
            WriteCharacter(CharacterPath, classCode: 15, reborn: 0, level: level, attributePoints: 10);
            var spells = Spells(Spell(1020, 7, 888, 6, 4, 3), Spell(9999, 5, 444, 2, 1, 1));
            new PlayerSpellSnapshotLoader(SpellsRoot).SaveByCharacterUid(CharacterUid, spells);
            expected.Add(CharacterPath, File.ReadAllBytes(CharacterPath));
            expected.Add(SpellPath, File.ReadAllBytes(SpellPath));
            var character = ReadCharacter();
            var item = new PlayerItemSnapshotItem { Uid = 900001, ItemId = 753003, StackSize = 3,
                OwnerUid = CharacterUid, OwnerName = character.Name, Signature = "durable-class-sentinel", PerfectionLevel = 2 };
            Original = new WorldSession(DurableAccountId, "full-account-durable-test", character,
                new PlayerItemSnapshot(new[] { item }, [], [], [], []), spells,
                new PlayerProficiencySnapshot([new(410, 5, 3, 765, 1600000)]),
                CombatGear: CombatGearSnapshot.Empty, UsersRoot: Users);
            Connection = NewConnection();
            Store = NewStore();
            Store.Set(Connection, Original);
            Recovery = new LegacyTradeRecoveryState(Users, null, null, null, null, SpellsRoot, null);
            Assert.True(Recovery.TryEnsureInitialized(out var reason), reason);
        }

        internal WorldSessionStore NewStore()
        {
            var store = new WorldSessionStore(); // Actual holder; no accepting persistence delegate.
            stores.Add(store);
            return store;
        }

        internal ConquerTcpConnection NewConnection(bool withSend = true, bool withDisconnect = true, bool throwOnSend = false)
        {
            Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? send = withSend
                ? (frame, _) =>
                {
                    if (throwOnSend) throw new IOException("owned-class-send-failure");
                    Sent.Add(frame.ToArray()); return ValueTask.CompletedTask;
                } : null;
            Func<CancellationToken, ValueTask>? disconnect = withDisconnect
                ? _ => { Disconnected = true; return ValueTask.CompletedTask; } : null;
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), send, disconnect);
            connections.Add(connection);
            return connection;
        }

        internal void ReplaceConnection(bool withSend = true, bool withDisconnect = true, bool throwOnSend = false)
        {
            Assert.True(Store.TryRemove(Connection, out var retired));
            Assert.Same(Original, retired);
            Connection = NewConnection(withSend, withDisconnect, throwOnSend);
            Store.Set(Connection, Original);
        }

        internal async ValueTask<bool> InvokeAsync(WorldSession expectedSession, WorldSessionStore? suppliedStore = null, byte optionId = 2,
            string? literalSpellsRoot = null)
        {
            using var operation = await Operations.EnterSharedAsync(CancellationToken.None);
            return await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(new WorldPacketHandlerContext(
                Connection, Log, Session: expectedSession, Sessions: suppliedStore ?? Store,
                WorldData: LegacyItemWorldDataFixture.Create(), PlayerSpellsRoot: literalSpellsRoot ?? SpellsRoot)
                { Trades = Trades, TradeOperations = Operations, TradeRecovery = Recovery },
                expectedSession, NpcId, optionId, input: string.Empty, CancellationToken.None);
        }

        internal byte[] CharacterBytes => File.ReadAllBytes(CharacterPath);
        internal byte[] SpellBytes => File.ReadAllBytes(SpellPath);

        internal CharacterSnapshot ReadCharacter()
        {
            Guard();
            Assert.Equal(".ini", Path.GetExtension(CharacterPath));
            Assert.True(long.TryParse(Path.GetFileNameWithoutExtension(CharacterPath), NumberStyles.None,
                CultureInfo.InvariantCulture, out var accountFileId));
            Assert.Equal(DurableAccountId, accountFileId);
            var basename = Path.GetFileName(CharacterPath);
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out _);
            var character = CharacterSnapshotParser.ParseText(text, basename);
            Assert.InRange(character.Uid, 1L, (long)uint.MaxValue);
            Assert.Equal((long)CharacterUid, character.Uid);
            Assert.Equal(basename, character.SourceFile);
            return character;
        }

        internal void RemoveSeededSpellParent()
        {
            RequireFilesUnchanged();
            File.Delete(SpellPath);
            Directory.Delete(SpellsRoot, recursive: false);
            expected.Remove(SpellPath);
        }

        internal void ExpectImage(string path, byte[] bytes)
        {
            Assert.True(path == CharacterPath || path == SpellPath || path == PendingPath || ownedSpellStages.Contains(path));
            Guard();
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
            Assert.Equal(bytes, File.ReadAllBytes(path));
            expected[path] = bytes.ToArray();
        }

        internal void ExpectNoPending()
        {
            Guard();
            Assert.False(File.Exists(PendingPath));
            expected.Remove(PendingPath);
        }

        internal string CreateOwnedSpellStage(string transactionId, byte[] bytes)
        {
            Assert.True(Guid.TryParseExact(transactionId, "N", out var id) && id != Guid.Empty);
            var path = SpellPath + ".managed-trade-" + transactionId + ".tmp";
            Guard();
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(true); }
            Assert.True(ownedSpellStages.Add(path));
            expected.Add(path, bytes.ToArray());
            return path;
        }

        internal void ExpectStageAbsent(string path)
        {
            Assert.Contains(path, ownedSpellStages, ownedSpellStages.Comparer);
            Guard();
            Assert.False(File.Exists(path));
            expected.Remove(path); ownedSpellStages.Remove(path);
        }

        internal void RequireFilesUnchanged()
        {
            Guard();
            var hasSpells = expected.Keys.Any(path => Path.GetDirectoryName(path) == SpellsRoot);
            Assert.Equal((hasSpells ? new[] { Users, SpellsRoot } : new[] { Users }).Order(StringComparer.Ordinal),
                Directory.GetFileSystemEntries(Root).Order(StringComparer.Ordinal));
            Assert.Equal(Directory.Exists(JournalRoot) ? new[] { JournalRoot } : Array.Empty<string>(), Directory.GetDirectories(Users));
            Assert.Equal(new[] { CharacterPath }, Directory.GetFiles(Users));
            if (hasSpells)
            {
                Assert.Empty(Directory.GetDirectories(SpellsRoot));
                Assert.Equal(expected.Keys.Where(path => Path.GetDirectoryName(path) == SpellsRoot).Order(StringComparer.Ordinal),
                    Directory.GetFiles(SpellsRoot).Order(StringComparer.Ordinal));
            }
            else Assert.False(Directory.Exists(SpellsRoot) || File.Exists(SpellsRoot));
            if (Directory.Exists(JournalRoot))
            {
                Assert.Empty(Directory.GetDirectories(JournalRoot));
                Assert.Equal(expected.ContainsKey(PendingPath) ? new[] { PendingPath } : Array.Empty<string>(), Directory.GetFiles(JournalRoot));
            }
            foreach (var file in expected) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
        }

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
                throw new AggregateException($"FullAccount owner closure unproven; retained {Root}.", failures);
            try
            {
                if (!ownsRoot) return;
                RequireFilesUnchanged();
                foreach (var path in expected.Keys) File.Delete(path);
                if (Directory.Exists(JournalRoot)) Directory.Delete(JournalRoot, recursive: false);
                Directory.Delete(Users, recursive: false);
                if (Directory.Exists(SpellsRoot)) Directory.Delete(SpellsRoot, recursive: false);
                Directory.Delete(Root, recursive: false);
            }
            finally { Log.Dispose(); }
        }

        private void Guard()
        {
            Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(Path.GetFullPath(Root)));
            Assert.Matches("^wolf-fullaccount-durable-class-[0-9a-f]{32}$", Path.GetFileName(Root));
            foreach (var directory in new[] { Users, SpellsRoot })
            {
                Assert.Equal(Root, Path.GetDirectoryName(Path.GetFullPath(directory)));
                LegacyNativeDbReadOwnedFiles.OrdinaryChain(directory);
            }
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(Root);
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(JournalRoot);
            foreach (var path in expected.Keys)
            {
                Assert.True(path == CharacterPath || path == SpellPath || path == PendingPath || ownedSpellStages.Contains(path));
                Assert.Contains(Path.GetDirectoryName(Path.GetFullPath(path)), new[] { Users, SpellsRoot, JournalRoot });
                LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
            }
        }
    }
}
