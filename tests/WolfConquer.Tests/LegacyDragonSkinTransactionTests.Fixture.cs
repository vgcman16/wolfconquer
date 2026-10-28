using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDragonSkinTransactionTests
{
    private const long Account = 100;
    private const uint Uid = 1_293_600;
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);
    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Registry =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private sealed class Fixture
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-dragon-skin-transaction-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(Root, "Users");
        internal string Items => Path.Combine(Root, "PlayersItems");
        internal string CharacterPath => Path.Combine(Users, "100.ini");
        internal string ItemPath => Path.Combine(Items, "1293600.bin");
        internal List<byte[]> Sent { get; } = [];
        internal List<string> Trace { get; } = [];
        internal StringWriter Log { get; } = new();
        internal WorldSessionStore Sessions { get; set; } = new();
        internal WorldOperationBarrier Barrier { get; set; } = new();
        internal LegacyTradeRecoveryState Recovery { get; set; } = null!;
        internal LegacyTradeStateStore Trades { get; set; } = new();
        internal WorldSession Original { get; private set; } = null!;
        internal WorldDataCatalog? Catalog { get; set; }
        internal ConquerTcpConnection Connection { get; private set; } = null!;
        internal WorldServerFrameHandler? FrameOwner { get; private set; }
        internal Action? OnInventorySend { get; set; }
        internal bool FailInventorySend { get; set; }
        internal byte[] CharacterBefore { get; private set; } = [];
        internal byte[] ItemsBefore { get; private set; } = [];
        internal string CharacterTextBefore { get; private set; } = "";
        internal Encoding CharacterEncoding { get; private set; } = Encoding.UTF8;
        private readonly List<Task> tasks = [];
        private readonly Dictionary<Task, WorldSessionPublicationException> observedPublicationFaults = [];
        private readonly List<ConquerTcpConnection> connections = [];
        private readonly List<CancellationTokenSource> budgets = [];
        private readonly List<IDisposable> leases = [];

        internal void Seed(string state = "0/0/", IReadOnlyList<PlayerItemSnapshotItem>? inventory = null,
            bool utf8Bom = false)
        {
            ValidateRoot();
            Directory.CreateDirectory(Users);
            Directory.CreateDirectory(Items);
            CharacterTextBefore = Snapshot(state).Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\n", "\r\n", StringComparison.Ordinal);
            CharacterEncoding = new UTF8Encoding(utf8Bom);
            File.WriteAllBytes(CharacterPath, CharacterEncoding.GetPreamble().Concat(CharacterEncoding.GetBytes(CharacterTextBefore)).ToArray());
            var snapshot = PlayerItemSnapshot.Empty with { InventoryItems = inventory?.ToArray() ?? [Item(9, 720_001)] };
            new PlayerItemSnapshotLoader(Items).SaveByCharacterUid(Uid, snapshot);
            // This tests the business operation's independent identities, not a
            // distinct-ID live auth-loader policy or invented login success.
            var character = CharacterSnapshotParser.ParseText(CharacterTextBefore, "100.ini");
            Original = new(Account, "DragonOwner", character, new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(Uid),
                PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: Users);
            Connection = MakeConnection();
            Sessions.Set(Connection, Original);
            Recovery = new(Users, Items);
            Assert.True(Recovery.TryEnsureInitialized(out var reason), reason);
            CharacterBefore = File.ReadAllBytes(CharacterPath);
            ItemsBefore = File.ReadAllBytes(ItemPath);
        }

        private ConquerTcpConnection MakeConnection()
        {
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (bytes, token) =>
            {
                token.ThrowIfCancellationRequested();
                var copy = bytes.ToArray();
                Sent.Add(copy);
                Trace.Add("frame:" + Frame(copy).Header.Opcode);
                if (LegacyGameItemPacket.TryParse(Frame(copy), out _) || MsgItemUsagePacket.TryParse(Frame(copy), out _))
                {
                    // Real disk+memory images must already agree at FIRST exposure.
                    Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems), File.ReadAllBytes(ItemPath));
                    Assert.Equal(LegacyDragonSkinState.FromSnapshot(Current.Character).ToSnapshotValue(), ReloadCharacter().Attributes["AnimaSkin"]);
                    OnInventorySend?.Invoke();
                    if (FailInventorySend) throw new IOException("owned DragonSkin notification failure");
                }
                return ValueTask.CompletedTask;
            });
            connections.Add(connection);
            return connection;
        }

        internal WorldSession Current
        {
            get { Assert.True(Sessions.TryGet(Connection, out var value)); return Assert.IsType<WorldSession>(value); }
        }

        internal CharacterSnapshot ReloadCharacter() => CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), "100.ini");
        internal PlayerItemSnapshot ReloadItems() => new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(Uid);

        internal Task InvokeAsync(LegacyDragonSkinType type, uint skin = 1, WorldSession? supplied = null,
            bool includeStore = true, bool includeBarrier = true, bool includeRecovery = true)
        {
            Assert.Contains(WorldPacketOpcodes.LegacyAnimaSkin, Registry.Opcodes);
            var budget = new CancellationTokenSource(Bound);
            budgets.Add(budget);
            var context = new WorldPacketHandlerContext(Connection, new TraceWriter(Log, Trace), supplied ?? Current,
                WorldData: Catalog, Sessions: includeStore ? Sessions : null, PlayerItemsRoot: Items)
            { TradeOperations = includeBarrier ? Barrier : null, TradeRecovery = includeRecovery ? Recovery : null, Trades = Trades };
            var actual = Registry.HandleAsync(context, Frame(new LegacyDragonSkinPacket(type, Uid, skin, false).ToFrameBytes()), budget.Token).AsTask();
            tasks.Add(actual);
            return ObserveCallAsync(actual, budget);
        }

        internal void AttachFrameOwner()
        {
            FrameOwner = new WorldServerFrameHandler(new CharacterSnapshotLoader(Users, Items),
                handoffService: null, worldData: Catalog, runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: Log);
            Sessions = ReadOwnerField<WorldSessionStore>(FrameOwner, "_sessions");
            Barrier = ReadOwnerField<WorldOperationBarrier>(FrameOwner, "_worldOperations");
            Recovery = ReadOwnerField<LegacyTradeRecoveryState>(FrameOwner, "_tradeRecovery");
            Trades = ReadOwnerField<LegacyTradeStateStore>(FrameOwner, "_tradeStates");
            Sessions.Set(Connection, Original);
        }

        internal Task FrameAsync(LegacyDragonSkinType type, uint skin = 1)
        {
            // A per-frame token is NOT a connection-lifetime token. This old
            // handler registers it with detached disconnect cleanup. Use None,
            // bound observation, and retain the domain if actual work is stuck.
            var actual = FrameOwner!.HandleFrameAsync(Connection,
                Frame(new LegacyDragonSkinPacket(type, Uid, skin, false).ToFrameBytes()), CancellationToken.None).AsTask();
            tasks.Add(actual);
            return actual.WaitAsync(Bound + Bound);
        }

        private static async Task ObserveCallAsync(Task actual, CancellationTokenSource budget)
        {
            try { await actual.WaitAsync(Bound + Bound); }
            finally { if (actual.IsCompleted) budget.CancelAfter(Timeout.InfiniteTimeSpan); }
        }

        internal async Task<WorldSessionPublicationException> InvokeExpectPublishedFailureAsync(LegacyDragonSkinType type)
        {
            var budget = new CancellationTokenSource(Bound); budgets.Add(budget);
            var context = new WorldPacketHandlerContext(Connection, new TraceWriter(Log, Trace), Current,
                WorldData: Catalog, Sessions: Sessions, PlayerItemsRoot: Items)
            { TradeOperations = Barrier, TradeRecovery = Recovery, Trades = Trades };
            var actual = Registry.HandleAsync(context,
                Frame(new LegacyDragonSkinPacket(type, Uid, 1, false).ToFrameBytes()), budget.Token).AsTask();
            tasks.Add(actual);
            try { await actual.WaitAsync(Bound + Bound); }
            catch (WorldSessionPublicationException error)
            {
                // Cleanup must not report an already asserted actual task fault
                // twice. Only this precise terminal exception is exempted.
                observedPublicationFaults.Add(actual, error);
                if (actual.IsCompleted) budget.CancelAfter(Timeout.InfiniteTimeSpan);
                return error;
            }
            throw new InvalidOperationException("Expected real published-outcome exception was not thrown.");
        }

        internal void KeepLease(IDisposable lease) => leases.Add(lease);

        internal async Task CloseTransportAsync()
        {
            var actual = Connection.CompleteTransportCloseAsync();
            tasks.Add(actual);
            await actual.WaitAsync(Bound);
        }

        internal void ReconnectFromFiles()
        {
            var reloaded = Original with { Character = ReloadCharacter(), PlayerItems = ReloadItems() };
            Connection = MakeConnection();
            Sessions.Set(Connection, reloaded);
            Sent.Clear(); Trace.Clear();
        }

        internal async Task<List<Exception>> CleanupAsync()
        {
            var failures = new List<Exception>();
            foreach (var lease in leases) Collect(failures, lease.Dispose);
            if (tasks.Any(actual => !actual.IsCompleted))
                foreach (var budget in budgets)
                    try { await budget.CancelAsync(); } catch (Exception error) { failures.Add(error); }
            var quiescent = true;
            foreach (var actual in tasks)
            {
                try { await actual.WaitAsync(Bound); }
                catch (Exception error)
                {
                    if (!observedPublicationFaults.TryGetValue(actual, out var expected) || !ReferenceEquals(error, expected))
                        failures.Add(error);
                    if (!actual.IsCompleted) quiescent = false;
                }
            }
            if (quiescent && FrameOwner is not null)
            {
                foreach (var connection in connections)
                    try { await connection.CompleteTransportCloseAsync().WaitAsync(Bound); }
                    catch (Exception error) { failures.Add(error); quiescent = false; }
                if ((object)FrameOwner is IAsyncDisposable disposable)
                    try { await disposable.DisposeAsync().AsTask().WaitAsync(Bound); }
                    catch (Exception error) { failures.Add(error); quiescent = false; }
            }
            foreach (var budget in budgets) Collect(failures, budget.Dispose);
            if (quiescent) Collect(failures, () => { ValidateRoot(); DeleteOrdinaryTree(Root); });
            if (quiescent) Collect(failures, Log.Dispose);
            return failures;
        }

        private void ValidateRoot()
        {
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Root));
            Assert.Equal(temp, Path.GetDirectoryName(full));
            const string prefix = "wolf-dragon-skin-transaction-";
            var name = Path.GetFileName(full);
            Assert.StartsWith(prefix, name);
            Assert.True(Guid.TryParseExact(name[prefix.Length..], "N", out _));
            for (string? path = full; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
                if (File.Exists(path) || Directory.Exists(path)) Assert.Equal(0, (int)(File.GetAttributes(path) & FileAttributes.ReparsePoint));
        }

        private void DeleteOrdinaryTree(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                Assert.StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, Path.GetFullPath(entry),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                Assert.Equal(0, (int)(File.GetAttributes(entry) & FileAttributes.ReparsePoint));
                if (Directory.Exists(entry)) DeleteOrdinaryTree(entry); else File.Delete(entry);
            }
            Directory.Delete(directory, recursive: false);
        }
    }

    private static async Task RunAsync(Func<Fixture, Task> body)
    {
        var fixture = new Fixture();
        Exception? primary = null;
        try { await body(fixture); } catch (Exception error) { primary = error; }
        var cleanup = await fixture.CleanupAsync();
        if (primary is not null && cleanup.Count == 0) ExceptionDispatchInfo.Capture(primary).Throw();
        if (primary is not null) cleanup.Insert(0, primary);
        if (cleanup.Count != 0) throw new AggregateException(
            $"DragonSkin test and/or owned cleanup failed; ownedRoot={fixture.Root}; unproven live owners retain their files/logger", cleanup);
    }

    private static void Collect(List<Exception> errors, Action action)
    { try { action(); } catch (Exception error) { errors.Add(error); } }

    private static T ReadOwnerField<T>(WorldServerFrameHandler owner, string name) where T : class =>
        Assert.IsType<T>(typeof(WorldServerFrameHandler).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));

    private sealed class TraceWriter(StringWriter writer, List<string> trace) : TextWriter
    {
        public override Encoding Encoding => writer.Encoding;
        public override Task WriteLineAsync(string? value)
        { trace.Add("log:" + value); return writer.WriteLineAsync(value); }
    }

    private static PacketFrame Frame(byte[] bytes)
    { Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame; }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId, ushort stack = 1, byte bound = 0) => new()
    { Uid = uid, ItemId = itemId, StackSize = stack, Bound = bound };

    private static uint Mapped(uint skin) => skin switch { 1 => 4_200_018, 2 => 4_200_016, 3 => 4_200_017, _ => 0 };

    private static ItemType Metadata(uint itemId, string durability = "23")
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture); fields[1] = "OwnedAnima";
        fields[ItemType.DurabilityFieldIndex] = durability;
        return new((int)itemId, "OwnedAnima", fields);
    }

    private static string Snapshot(string state) => $$"""
        ; Preserve this exact header and newline encoding.
        [Character]
        UID={{Uid}}
        Name=DragonOwner
        Body=1008
        Face=154
        Haire=27
        Class=7051
        FirstClass=0
        SecoundeClass=7050
        Reborn=2
        Level=140
        Experience=0
        Map=1015
        X=830
        Y=584
        PMap=1000
        PMapX=500
        PMapY=650
        Strength=0
        Agility=36
        Vitaliti=447
        Spirit=0
        Atributes=12
        MinHitPoints=61258
        MinMana=5000
        Money=5000
        ConquerPoints=1000
        BoundConquerPoints=0
        BankCps=0
        PkPoints=0
        Online=1
        AnimaSkin={{state}}
        Sentinel=unchanged
        [Unrelated]
        Keep=exact text
        """;
}
