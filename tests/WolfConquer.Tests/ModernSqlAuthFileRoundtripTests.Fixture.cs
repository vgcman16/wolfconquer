using System.Buffers;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
[Trait("Category", "OwnedSqlIntegration")]
public sealed partial class ModernSqlAuthFileRoundtripTests
{
    private static async Task Run(Func<SqlWorldFixture, Task> body, string? tradeItemState = null)
    {
        OwnedModernSqlAuthDatabase? database = null; SqlWorldFixture? world = null;
        var errors = new List<Exception>();
        try
        {
            database = await OwnedModernSqlAuthDatabase.CreateAsync(SqlWorldFixture.FirstUid, SqlWorldFixture.SecondUid);
            world = new(database, tradeItemState); await world.InitializeAsync(); await body(world);
        }
        catch (Exception error) { errors.Add(error); }
        if (world is not null)
        {
            try { await world.CloseWorldAsync(); } catch (Exception error) { errors.Add(error); }
            try { world.RestoreMetadata(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count == 0) try { world.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        }
        if (database is not null) try { await database.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private sealed partial class SqlWorldFixture
    {
        internal const uint FirstUid = LegacyOwnedAdmissionRuntimeFixture.Uid, SecondUid = FirstUid + 1;
        internal const uint SoldUid = 1_000_091, KeptUid = 1_000_092, PeerItemUid = 1_000_093;
        internal const uint ItemId = 1_000_000, NpcUid = 20_499;
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        internal readonly OwnedModernSqlAuthDatabase Database;
        internal readonly List<byte[]> Sent = [], OtherSent = [];
        private readonly StringWriter buffer = new();
        private readonly TextWriter log;
        private readonly InMemoryAccountHandoffTicketStore tickets = new();
        private AccountServerFrameHandler account = null!;
        private AccountHandoffService handoff = null!;
        private NativeLoginBeastMetadataScope? beasts;
        private WorldDataCatalog catalog = null!;
        private Exception? observerFailure;
        internal WorldServerFrameHandler Handler = null!;
        internal LegacyOwnedAdmissionRuntime? Runtime;
        internal RootLoopbackFramePeer? Peer, OtherPeer;
        internal Action<byte[]>? BeforeSend;
        internal string UserPath => Data.UserPath;
        internal string ItemsPath => Path.Combine(Data.Items, FirstUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string OtherUserPath => Path.Combine(Data.Users, SecondUid.ToString(CultureInfo.InvariantCulture) + ".ini");
        internal string OtherItemsPath => Path.Combine(Data.Items, SecondUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal int TicketCount => tickets.Tickets.Count;
        internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
        internal WorldSession Current => CurrentFor(Peer!);
        internal WorldSession Other => CurrentFor(OtherPeer!);
        internal LegacyNativeGameClientRuntime Native => Current.NativeGameClient!;
        internal string Diagnostics => buffer.ToString();

        internal SqlWorldFixture(OwnedModernSqlAuthDatabase database, string? tradeItemState = null)
        { Database = database; this.tradeItemState = tradeItemState; log = TextWriter.Synchronized(buffer); }

        internal async Task InitializeAsync()
        {
            WriteItems(FirstUid, SoldUid, KeptUid); WriteItems(SecondUid, PeerItemUid);
            string first = File.ReadAllText(Data.UserPath);
            File.WriteAllText(OtherUserPath, first.Replace("UID=" + FirstUid + "\r\n", "UID=" + SecondUid + "\r\n", StringComparison.Ordinal)
                .Replace("Name=OwnedStartup", "Name=OwnedSqlPeer", StringComparison.Ordinal));
            new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(SecondUid, PlayerSpellSnapshot.Empty);
            new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(SecondUid, PlayerProficiencySnapshot.Empty);
            new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(SecondUid, PlayerQuestSnapshot.Empty);
            new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(SecondUid, CombatGearSnapshot.Empty);
            var fields = Enumerable.Repeat("0", 66).ToArray(); fields[0] = ItemId.ToString(CultureInfo.InvariantCulture);
            fields[1] = "SqlOwnedSale"; fields[ItemType.GoldWorthFieldIndex] = "900";
            fields[ItemType.DurabilityFieldIndex] = "10"; fields[ItemType.StackSizeFieldIndex] = "1";
            catalog = NativeCallerWorldDataFixture.Create([new ItemType((int)ItemId, fields[1], fields)], [],
                npcs: [new Npc((int)NpcUid, 0, 0, 1002, 430, 378, null)]);
            handoff = new(tickets);
            account = new(new AccountLoginService(Database.CreateRepository()), handoff, log);
            ConfigureTradeInputs();
            beasts = new(1000); Data.Bootstrap(); await OpenWorldAsync();
        }

        private void WriteItems(uint uid, params uint[] ids) => new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(uid,
            PlayerItemSnapshot.Empty with { InventoryItems = ids.Select(id => new PlayerItemSnapshotItem
            { Uid = id, ItemId = ItemId, StackSize = 1, Durability = 10, MaximumDurability = 10, Color = 5 }).ToArray() });

        internal async Task<LegacyAccountForwardPacket> AuthenticateAsync(string username, string password)
        {
            var frames = await ObserveAuthenticationAsync(username, password);
            Assert.True(LegacyAccountForwardPacket.TryParse(ReadFrame(Assert.Single(frames)), out var forward));
            return forward;
        }

        // The real Account entry catches/logs provider faults without a forward.
        // Observation must not assume successful (or ordinary denied) authentication.
        internal async Task<byte[][]> ObserveAuthenticationAsync(string username, string password)
        {
            var frames = new List<byte[]>();
            await using var peer = new RootLoopbackFramePeer(bytes => { frames.Add(bytes); return ValueTask.CompletedTask; });
            await account.HandleFrameAsync(peer.Connection,
                ReadFrame(new LegacyAccountAuthenticationPacket(username, password, "SqlOwned").ToFrameBytes()));
            await peer.DrainObservations();
            return frames.ToArray();
        }

        private WorldSession CurrentFor(RootLoopbackFramePeer peer)
        {
            Assert.True(Store.TryGet(peer.Connection, out var session), "Actual ordinary/native admission did not produce a current holder.");
            return Assert.IsType<WorldSession>(session);
        }

        internal static byte[] SaleRequest() => new MsgItemUsagePacket(NpcUid, SoldUid, MsgItemUsageAction.SellItem, 0, 0, 0, 0).ToFrameBytes();
        internal async Task SellAsync()
        {
            await Handler.HandleFrameAsync(Peer!.Connection, ReadFrame(SaleRequest()));
            await Peer.DrainObservations();
            if (observerFailure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
        }

        internal void AssertConverged()
        {
            var current = Current;
            Assert.Equal(File.ReadAllBytes(ItemsPath), PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
            Assert.Equal(current.Character.Money, CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath)).Money);
            Assert.True(Native.OrdinarySpellBinding!.Items.Image.IsCurrentPlain());
            Assert.Same(current.PlayerItems, Native.OrdinarySpellBinding.Items.Image.FinalSnapshot);
            Assert.Equal(current.PlayerItems.InventoryItems.Select(row => row.Uid).Order(), Native.Inventory!.ClientItems.Keys.Order());
            Assert.Equal(current.Character.Money, Native.Player!.Money);
        }

        internal void RestoreMetadata() { try { beasts?.Dispose(); } finally { beasts = null; log.Dispose(); buffer.Dispose(); } }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    { Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame; }
}
