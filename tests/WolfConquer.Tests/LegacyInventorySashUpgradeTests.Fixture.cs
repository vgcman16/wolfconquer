using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);
    private static readonly ConcurrentBag<Fixture> Unfinished = [];

    private static async Task RunAsync(Func<Fixture, Task> body)
    {
        var fixture = new Fixture();
        var errors = new List<Exception>();
        try { await body(fixture); } catch (Exception error) { errors.Add(error); }
        try { await fixture.CleanupAsync(errors); }
        catch (Exception error)
        {
            errors.Add(error);
            Unfinished.Add(fixture);
            errors.Add(new InvalidOperationException($"Cleanup retained sash fixture ownedRoot={fixture.Root}."));
        }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Inventory sash upgrade test/cleanup failed.", errors);
    }

    private sealed partial class Fixture
    {
        internal const uint Uid = 1_293_600;
        private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Registry =
            PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "WolfConquer-SashUpgrade-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(Root, "Users");
        internal string Items => Path.Combine(Root, "Items");
        internal long Account { get; private set; } = 301;
        internal string CharacterPath => Path.Combine(Users, Account.ToString(CultureInfo.InvariantCulture) + ".ini");
        internal string ItemPath => Path.Combine(Items, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal StringWriter Log { get; } = new();
        internal List<byte[]> Sent { get; } = [];
        internal List<Task> Calls { get; } = [];
        internal List<TaskCompletionSource> Releases { get; } = [];
        private readonly List<FileStream> heldFiles = [];
        private readonly HashSet<Task> observed = [];
        internal WorldSessionStore Sessions { get; } = new();
        internal LegacyTradeStateStore Trades { get; } = new();
        internal WorldOperationBarrier Operations { get; } = new();
        internal LegacyTradeRecoveryState Recovery { get; private set; } = null!;
        internal ConquerTcpConnection Connection { get; }
        internal WorldSession Session { get; private set; } = null!;
        internal WorldSession Current => Assert.IsType<WorldSession>(Sessions.TryGet(Connection, out var value) ? value : null);
        internal WorldPacketHandlerContext Context { get; private set; } = null!;
        internal Action<byte[]>? SendObserver { get; set; }

        internal Fixture()
        {
            Directory.CreateDirectory(Users);
            Directory.CreateDirectory(Items);
            Connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                var bytes = frame.ToArray();
                Sent.Add(bytes);
                SendObserver?.Invoke(bytes);
                return ValueTask.CompletedTask;
            });
        }

        internal void Seed(long cp = 150, ulong count = 0, bool frameLogin = false)
        {
            Account = frameLogin ? Uid : 301;
            var text = FormattableString.Invariant($$"""
                [Character]
                UID={{Uid}}
                Name=SashOwner
                Body=1003
                Face=0
                Haire=0
                Class=10
                FirstClass=10
                SecoundeClass=0
                Reborn=0
                Level=120
                Experience=0
                Map=1002
                X=430
                Y=380
                PMap=1002
                PMapX=430
                PMapY=380
                Strength=0
                Agility=0
                Vitaliti=0
                Spirit=0
                Atributes=0
                MinHitPoints=1
                MinMana=0
                Money=7654
                ConquerPoints={{cp}}
                InventorySashCount={{count}}
                BoundConquerPoints=7
                BankCps=3
                PkPoints=0
                Online=1
                SashUpgradeSentinel=preserved
                [Unrelated]
                Value=unchanged
                """);
            File.WriteAllLines(CharacterPath, text.Replace("\r\n", "\n").Split('\n'), new UTF8Encoding(false));
            static PlayerItemSnapshotItem Item(uint uid) => new()
            { Uid = uid, ItemId = 1_088_001, Durability = 5, MaximumDurability = 10, StackSize = 1 };
            var items = new PlayerItemSnapshot(new[] { Item(9000) }, new[] { Item(9001).WithPosition(213) },
                new[] { Item(9002).WithPosition(217) }, new[] { Item(9003).WithPosition(4) },
                new[] { Item(9004).WithPosition(211) }, new[] { Item(9005).WithWarehouseId(100) })
            { WarehouseItems = new[] { Item(9006).WithWarehouseId(1000) } };
            var loader = new PlayerItemSnapshotLoader(Items);
            loader.SaveByCharacterUid(Uid, items);
            var loaded = loader.LoadByCharacterUid(Uid);
            Assert.Equal(File.ReadAllBytes(ItemPath), PlayerItemSnapshotLoader.ToLegacyBytes(loaded));
            Session = new(Account, "sash-fixture", CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath),
                loaded, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: Users);
            Sessions.Set(Connection, Session);
            File.WriteAllText(Path.Combine(Root, "unrelated.keep"), "preserve complete file set", new UTF8Encoding(false));
            Recovery = new(Users, Items);
            Assert.True(Recovery.TryEnsureInitialized(out var reason), reason);
            Assert.True(Recovery.IsReadyFor(Users, Items));
            Context = new(Connection, Log, Session, Sessions: Sessions, PlayerItemsRoot: Items,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
            { Trades = Trades, TradeOperations = Operations, TradeRecovery = Recovery };
        }

        internal void SetContext(WorldPacketHandlerContext context) => Context = context;
        internal static byte[] Request() => new MsgActionPacket
        { ObjId = Uid, Type = MsgActionType.UpdateInventorySash, Timestamp = 1234 }.ToFrameBytes();

        internal Task InvokeAsync()
        {
            // The actual action owns Exclusive. No outer Shared upgrade here.
            var actual = Registry.HandleAsync(Context, ReadFrame(Request()), CancellationToken.None).AsTask();
            Calls.Add(actual);
            return ObserveAsync(actual);
        }

        internal async Task ObserveAsync(Task actual)
        {
            try { await actual.WaitAsync(Bound); }
            catch (Exception observation)
            {
                if (actual.IsCompleted)
                {
                    Exception? terminal = null;
                    try { await actual; } catch (Exception error) { terminal = error; }
                    observed.Add(actual);
                    if (terminal is not null && !ReferenceEquals(observation, terminal))
                        throw new AggregateException(observation, terminal);
                }
                throw;
            }
            observed.Add(actual);
        }

        internal void HoldCharacterFile() => heldFiles.Add(new FileStream(CharacterPath, FileMode.Open, FileAccess.Read, FileShare.None));
        internal void ReleaseFiles()
        {
            foreach (var file in heldFiles) file.Dispose();
            heldFiles.Clear();
        }

        internal Snapshot Capture() => new(Current, JsonSerializer.Serialize(Current), Files(), Sent.Count);
        internal Dictionary<string, byte[]> Files() => Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(Root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        internal void AssertFiles(Dictionary<string, byte[]> expected)
        {
            var actual = Files();
            Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
            foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
        }
        internal void AssertUnchanged(Snapshot before)
        {
            Assert.Same(before.Owner, Current);
            Assert.Equal(before.Json, JsonSerializer.Serialize(Current));
            AssertFiles(before.Files);
            Assert.Equal(before.FrameCount, Sent.Count);
        }

        internal async Task CleanupAsync(List<Exception> errors)
        {
            foreach (var release in Releases) release.TrySetResult(); // Only test-owned scheduling holds.
            var terminal = true;
            foreach (var actual in Calls.ToArray())
            {
                if (observed.Contains(actual)) continue;
                try { await ObserveAsync(actual); }
                catch (Exception error) { errors.Add(error); if (!actual.IsCompleted) terminal = false; }
            }
            if (!terminal) { Retain(errors, "actual operation unfinished"); return; }
            // An actual leaf lease stays open until work has REALLY terminated.
            try { ReleaseFiles(); } catch (Exception error) { errors.Add(error); terminal = false; }
            try
            {
                var close = Connection.CompleteTransportCloseAsync();
                Calls.Add(close);
                await ObserveAsync(close);
            }
            catch (Exception error) { errors.Add(error); terminal = false; }
            if (terminal && (object?)frameOwner is IAsyncDisposable drainOwner)
            {
                var drain = drainOwner.DisposeAsync().AsTask();
                Calls.Add(drain);
                try { await ObserveAsync(drain); } catch (Exception error) { errors.Add(error); terminal = false; }
            }
            if (!terminal) { Retain(errors, "transport/owner closure unproven"); return; }
            SendObserver = null;
            try
            {
                var target = Path.GetFullPath(Root);
                var temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
                if (!string.Equals(Path.GetDirectoryName(target), temporary,
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                    !Path.GetFileName(target).StartsWith("WolfConquer-SashUpgrade-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Refusing deletion outside exact owned sash-upgrade root.");
                Directory.Delete(target, recursive: true);
            }
            catch (Exception error) { errors.Add(error); }
            try { Log.Dispose(); } catch (Exception error) { errors.Add(error); }
        }
        private void Retain(List<Exception> errors, string reason)
        {
            Unfinished.Add(this);
            errors.Add(new InvalidOperationException($"Retained sash fixture ownedRoot={Root}; {reason}."));
        }
    }

    private sealed record Snapshot(WorldSession Owner, string Json, Dictionary<string, byte[]> Files, int FrameCount);
    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
