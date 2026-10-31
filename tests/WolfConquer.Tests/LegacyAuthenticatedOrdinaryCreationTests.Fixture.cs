using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    internal static async Task Run(Func<CreationFixture, Task> body, ushort selection = 10,
        Action<CreationFixture>? beforeBootstrap = null, bool enter = true)
    {
        var f = new CreationFixture(); Exception? primary = null;
        try { await f.InitializeAsync(selection, beforeBootstrap, enter); await body(f); }
        catch (Exception error) { primary = error; }
        var failures = new List<Exception>();
        if (primary is not null) failures.Add(primary);
        try { await f.CloseAsync(); } catch (Exception error) { failures.Add(error); }
        try { f.RestoreMetadata(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count == 0) f.Data.Dispose();
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Owned creation and cleanup failed; fixture retained at " + f.Data.Root, failures);
    }

    internal sealed partial class CreationFixture
    {
        internal const uint Account = LegacyOwnedAdmissionRuntimeFixture.Uid + 1, RuneActor = LegacyOwnedAdmissionRuntimeFixture.Uid;
        internal const uint RuneUid = 1_035_001, RuneType = 4_020_517;
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        internal readonly StringWriter Log = new();
        private readonly NativeLoginBeastMetadataScope beasts = new(1000);
        private readonly List<LegacyOwnedAdmissionRuntime> owners = [];
        private readonly Dictionary<ConquerTcpConnection, CancellationTokenSource> connections = new();
        private readonly Dictionary<uint, List<byte[]>> sent = new();
        private Exception? observationFailure;
        internal LegacyOwnedAdmissionRuntime Runtime { get; private set; } = null!;
        internal WorldServerFrameHandler Handler { get; private set; } = null!;
        internal WorldDataCatalog Catalog { get; private set; } = null!;
        internal AccountHandoffService Handoff { get; private set; } = null!;
        internal FileAccountHandoffTicketStore TicketStore { get; private set; } = null!;
        internal ConquerTcpConnection Connection { get; private set; } = null!;
        internal ConquerTcpConnection? RuneConnection { get; private set; }
        internal ushort Selection { get; private set; }
        internal string Ticket { get; private set; } = "";
        internal Action<uint, byte[]>? BeforeSend;
        internal List<byte[]> Sent => sent[Account];
        internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
        internal WorldSession Current => CurrentFor(Connection);
        internal LegacyNativeGameClientRuntime Native => Assert.IsType<LegacyNativeGameClientRuntime>(Connection.NativeGameClient);
        internal string CharacterPath => DataPath(LegacyTradeJournalFileKind.Character);
        internal string ItemsPath => DataPath(LegacyTradeJournalFileKind.Items);
        internal string SpellsPath => DataPath(LegacyTradeJournalFileKind.Spells);
        internal string PendingPath => Path.Combine(Data.Users, ".managed-trade-journal", "pending.json");
        internal string DataPath(LegacyTradeJournalFileKind kind) => Data.Options().Roots.PathFor(kind, Account);
        internal WorldSession CurrentFor(ConquerTcpConnection connection) => Assert.IsType<WorldSession>(Store.TryGet(connection, out var current) ? current : null);
        internal bool TryCurrent(out WorldSession? current) => Store.TryGet(Connection, out current);
        internal List<byte[]> Frames(uint uid) => sent[uid];

        internal async Task InitializeAsync(ushort selection, Action<CreationFixture>? beforeBootstrap, bool enter)
        {
            Selection = selection; Catalog = BuildCatalog();
            // A separate, preexisting actor owns the Rune before the real scan.
            // Newborn remains level1/reborn0; no post-birth Rune or stat injection.
            var text = File.ReadAllText(Data.UserPath).Replace("Class=7051", "Class=6000", StringComparison.Ordinal);
            File.WriteAllText(Data.UserPath, text, new System.Text.UTF8Encoding(true));
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(RuneActor, new([], [], [], [],
                [new PlayerItemSnapshotItem { Uid = RuneUid, ItemId = RuneType, Position = 212, StackSize = 1, Durability = 100, MaximumDurability = 100, RuneExperience = 7500 }]));
            new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(RuneActor,
                new([new PlayerSpellSnapshotEntry(6001, 2, 1234, 1, 0, 0)]));
            var old = File.ReadAllBytes(Path.Combine(Data.Spells, RuneActor + ".bin")); old[15] = 0xA5;
            File.WriteAllBytes(Path.Combine(Data.Spells, RuneActor + ".bin"), old);
            beforeBootstrap?.Invoke(this); Data.Bootstrap();
            TicketStore = new(Path.Combine(Data.Root, "OwnedAccountHandoff"), "owned-fixture-key-" + Guid.NewGuid().ToString("N"));
            Handoff = new(TicketStore);
            Runtime = Open(); Handler = NewHandler(); Connection = NewConnection(Account);
            if (enter) { Ticket = await AuthenticateAsync(); await EnterAsync(); Sent.Clear(); }
        }

        internal LegacyOwnedAdmissionRuntime Open()
        {
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason), reason);
            var owner = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened); owners.Add(owner); Runtime = owner; return owner;
        }
        private WorldServerFrameHandler NewHandler() => new(Data.Loader, Handoff, Catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
            Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
        internal ConquerTcpConnection NewConnection(uint account = Account)
        {
            if (!sent.ContainsKey(account)) sent.Add(account, []);
            var lifetime = new CancellationTokenSource();
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
            {
                token.ThrowIfCancellationRequested(); var bytes = memory.ToArray();
                try { BeforeSend?.Invoke(account, bytes); } catch (Exception error) { observationFailure ??= error; throw; }
                sent[account].Add(bytes); return ValueTask.CompletedTask;
            }, _ => { lifetime.Cancel(); return ValueTask.CompletedTask; }, new LegacyNativeGameClientRuntime());
            connections.Add(connection, lifetime); return connection;
        }
        internal void RestoreMetadata() { beasts.Dispose(); Log.Dispose(); }
    }
}
