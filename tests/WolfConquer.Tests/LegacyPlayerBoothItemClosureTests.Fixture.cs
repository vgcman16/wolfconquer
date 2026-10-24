using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPlayerBoothItemClosureTests
{
    private static async Task RunBoothClosureAsync(Func<BoothFixture, Task> body)
    {
        var fixture = new BoothFixture();
        Exception? primary = null;
        try { await body(fixture); }
        catch (Exception error) { primary = error; }
        var cleanup = new List<Exception>();
        var terminal = fixture.Calls.All(task => task.IsCompleted);
        if (!terminal)
            cleanup.Add(new InvalidOperationException($"Retained nonterminal booth fixture: {fixture.Root}"));
        else
        {
            foreach (var connection in new[] { fixture.SellerConnection, fixture.BuyerConnection })
            {
                try { await connection.CompleteTransportCloseAsync(); }
                catch (Exception error) { cleanup.Add(error); }
            }
            if (cleanup.Count == 0)
            {
                try { fixture.Log.Dispose(); }
                catch (Exception error) { cleanup.Add(error); }
                try
                {
                    var target = Path.GetFullPath(fixture.Root);
                    var temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
                    if (!string.Equals(Path.GetDirectoryName(target), temporary,
                            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                        !Path.GetFileName(target).StartsWith("WolfConquer-D13VendorTests-", StringComparison.Ordinal))
                        throw new InvalidOperationException("Refusing cleanup outside the exact owned booth root.");
                    fixture.Harness.Dispose();
                }
                catch (Exception error) { cleanup.Add(error); }
            }
            else cleanup.Add(new InvalidOperationException($"Retained booth resources after transport cleanup fault: {fixture.Root}"));
        }
        if (primary is not null && cleanup.Count != 0)
            throw new AggregateException(new[] { primary }.Concat(cleanup));
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup.Count != 0) throw new AggregateException(cleanup);
    }

    private sealed partial class BoothFixture
    {
        private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Registry =
            PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        internal D13VendorTestHarness Harness { get; } = new();
        internal StringWriter Log { get; } = new();
        internal List<Task> Calls { get; } = [];
        internal ConquerTcpConnection SellerConnection { get; }
        internal ConquerTcpConnection BuyerConnection { get; }
        internal string Root => Path.GetDirectoryName(Harness.UsersRoot)!;
        internal WorldSession Seller => Harness.StoredSession(SellerConnection);
        internal WorldSession Buyer => Harness.StoredSession(BuyerConnection);

        internal BoothFixture()
        {
            SellerConnection = CreateSettlementConnection("seller");
            BuyerConnection = CreateSettlementConnection("buyer");
        }

        internal void Seed(params PlayerItemSnapshotItem[] sellerInventory)
        {
            SeedOne(SellerConnection, Harness.CreateSeller(300, 50,
                SevenStores(sellerInventory, 40_000)));
            SeedOne(BuyerConnection, Harness.CreateBuyer(1_000, 200,
                SevenStores(new[] { BoothItem(50) }, 30_000)));
            InitializeSettlementOwners();
        }

        private void SeedOne(ConquerTcpConnection connection, WorldSession session)
        {
            var character = session.Character;
            var text = FormattableString.Invariant($$"""
                [Character]
                UID={{character.Uid}}
                Name={{character.Name}}
                Body={{character.Body}}
                Face=0
                Haire=0
                Class=10
                FirstClass=10
                SecoundeClass=0
                Reborn=0
                Level=120
                Experience=0
                Map=1002
                X={{character.X}}
                Y=380
                PMap=1002
                PMapX={{character.X}}
                PMapY=380
                Strength=0
                Agility=0
                Vitaliti=0
                Spirit=0
                Atributes=0
                MinHitPoints=1
                MinMana=0
                Money={{character.Money}}
                ConquerPoints={{character.ConquerPoints}}
                BoundConquerPoints=0
                BankCps=0
                PkPoints=0
                Online=1
                BoothClosureSentinel=preserved
                [Unrelated]
                Value=unchanged
                """);
            var path = CharacterPath(session);
            // Seed in the existing vendor persistence owner's actual WriteAllLines
            // convention, so valid raw-byte assertions do not invent EOL policy.
            File.WriteAllLines(path, text.Replace("\r\n", "\n").Split('\n'), new UTF8Encoding(false));
            var loader = new PlayerItemSnapshotLoader(Harness.PlayerItemsRoot);
            loader.SaveByCharacterUid(character.Uid, session.PlayerItems);
            var parsed = loader.LoadByCharacterUid(character.Uid);
            Assert.Equal(File.ReadAllBytes(ItemPath(session)), PlayerItemSnapshotLoader.ToLegacyBytes(parsed));
            Harness.Sessions.Set(connection, session with
            {
                Character = CharacterSnapshotParser.ParseText(File.ReadAllText(path), path),
                PlayerItems = parsed,
            });
        }

        internal WorldPacketHandlerContext Context(ConquerTcpConnection connection) => new(
            connection, Log, Harness.StoredSession(connection), Sessions: Harness.Sessions,
            PlayerItemsRoot: Harness.PlayerItemsRoot, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
        {
            Trades = SettlementTrades,
            TradeOperations = SettlementOperations,
            TradeRecovery = SettlementRecovery,
        };

        private async Task DispatchAsync(ConquerTcpConnection connection, byte[] bytes)
        {
            var actual = DispatchSettlementScopeAsync(connection, bytes);
            Calls.Add(actual);
            await actual; // No detached call, observation timeout, or canceled-waiter drain claim.
        }

        internal async Task StartAsync()
        {
            Assert.False(Seller.Character.Attributes.ContainsKey("RuntimeVendorUid"));
            var before = Harness.Sent.Count;
            var request = new MsgActionPacket { ObjId = D13VendorTestHarness.SellerCharacterUid, Type = MsgActionType.StartVendor };
            await DispatchAsync(SellerConnection, request.ToFrameBytes());
            var sent = Harness.Sent.Skip(before).ToArray();
            Assert.Equal(3, sent.Length);
            Assert.Equal(new[] { "buyer", "seller", "seller" }, sent.Select(frame => frame.Recipient));
            Assert.Equal(sent[0].Bytes, sent[1].Bytes);
            Assert.True(LegacySobNpcSpawnPacket.TryParse(D13VendorTestHarness.ReadFrame(sent[0].Bytes), out var spawn));
            Assert.Equal(D13VendorTestHarness.SellerBoothUid, spawn.Uid);
            Assert.Equal(406, spawn.Mesh);
            Assert.Equal(14, spawn.Type);
            Assert.Equal(431, spawn.X);
            Assert.Equal(380, spawn.Y);
            Assert.Equal((request with { DwParam = D13VendorTestHarness.SellerBoothUid, PositionX = 431, PositionY = 380 }).ToFrameBytes(), sent[2].Bytes);
            Assert.Equal("102002", Seller.Character.Attributes["RuntimeVendorUid"]);
            Assert.NotNull(Seller.LegacyVendorListings);
            Assert.Empty(Seller.LegacyVendorListings.Listings);
        }

        internal async Task DepositAsync(uint childUid)
        {
            var actual = DepositSettlementScopeAsync(childUid);
            Calls.Add(actual);
            await actual;
        }

        internal static MsgItemUsagePacket ListingRequest(bool cps = false) => new(10, 125,
            cps ? MsgItemUsageAction.AddVendingItemConquerPoints : MsgItemUsageAction.AddVendingItemGold, 1234, 2, 3, 4);

        internal static MsgItemUsagePacket BuyRequest() => new(10, D13VendorTestHarness.SellerBoothUid,
            MsgItemUsageAction.BuyVendingItem, 3456, 2, 3, 4);

        internal Task ListAsync(bool cps = false) => DispatchAsync(SellerConnection, ListingRequest(cps).ToFrameBytes());
        internal Task BuyAsync() => DispatchAsync(BuyerConnection, BuyRequest().ToFrameBytes());

        internal Snapshot Capture() => new(Buyer, Seller, JsonSerializer.Serialize(Buyer), JsonSerializer.Serialize(Seller),
            ReadFiles(), Harness.Sent.Count, Log.ToString().Length);

        internal Dictionary<string, byte[]> ReadFiles() => Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(Root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

        internal void AssertUnchanged(Snapshot before)
        {
            Assert.Same(before.Buyer, Buyer);
            Assert.Same(before.Seller, Seller);
            Assert.Same(before.Seller.LegacyVendorListings, Seller.LegacyVendorListings);
            Assert.Equal(before.BuyerValues, JsonSerializer.Serialize(Buyer));
            Assert.Equal(before.SellerValues, JsonSerializer.Serialize(Seller));
            AssertFiles(before.Files);
            Assert.Equal(before.FrameCount, Harness.Sent.Count);
            var attemptLog = Log.ToString()[before.LogLength..];
            Assert.DoesNotContain("completed vendor purchase", attemptLog, StringComparison.Ordinal);
            Assert.DoesNotContain("added vendor listing", attemptLog, StringComparison.Ordinal);
        }

        internal void AssertFiles(Dictionary<string, byte[]> expected)
        {
            var actual = ReadFiles();
            Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
            foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
        }

        internal void SaveBuyerItems(PlayerItemSnapshot items)
        {
            new PlayerItemSnapshotLoader(Harness.PlayerItemsRoot).SaveByCharacterUid(Buyer.Character.Uid, items);
            Harness.Sessions.Set(BuyerConnection, Buyer with { PlayerItems = items });
        }

        internal string CharacterPath(WorldSession session) => Path.Combine(Harness.UsersRoot,
            session.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        internal string ItemPath(WorldSession session) => Path.Combine(Harness.PlayerItemsRoot,
            session.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
    }

    private static PlayerItemSnapshot SevenStores(PlayerItemSnapshotItem[] inventory, uint baseUid) => new(
        inventory,
        new[] { BoothItem(baseUid + 1).WithPosition(213) },
        new[] { BoothItem(baseUid + 2).WithPosition(217) },
        new[] { BoothItem(baseUid + 3).WithPosition(4) },
        new[] { BoothItem(baseUid + 4).WithPosition(211) },
        new[] { BoothItem(baseUid + 5).WithWarehouseId(100) })
    {
        WarehouseItems = new[] { BoothItem(baseUid + 6).WithWarehouseId(1000) },
    };

    private static PlayerItemSnapshotItem BoothItem(uint uid, uint itemId = 1_088_001,
        byte bound = 0, byte locked = 0, ushort inscribed = 0) => new()
    {
        Uid = uid, ItemId = itemId, Durability = 5, MaximumDurability = 10, StackSize = 1,
        Bound = bound, Locked = locked, Inscribed = inscribed,
        OwnerUid = uid, OwnerName = "BoothOriginal", Signature = "complete row sentinel",
    };

    private static PlayerItemSnapshotItem RestrictedBoothItem(uint uid, string restriction) => restriction switch
    {
        "bound1" => BoothItem(uid, bound: 1),
        "bound2" => BoothItem(uid, bound: 2),
        "locked" => BoothItem(uid, locked: 1),
        "inscribed" => BoothItem(uid, inscribed: 1),
        "unable" => BoothItem(uid, itemId: 750_000),
        _ => throw new ArgumentOutOfRangeException(nameof(restriction)),
    };

    private sealed record Snapshot(WorldSession Buyer, WorldSession Seller, string BuyerValues,
        string SellerValues, Dictionary<string, byte[]> Files, int FrameCount, int LogLength);
}
