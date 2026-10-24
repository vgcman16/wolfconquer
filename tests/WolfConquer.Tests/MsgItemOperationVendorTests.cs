using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgItemOperationVendorTests : IDisposable
{
    private const long BuyerAccountId = 101;
    private const uint BuyerCharacterUid = 1_001;
    private const long SellerAccountId = 202;
    private const uint SellerCharacterUid = 2_002;
    private const uint SellerBoothUid = 102_002;
    private const int MapId = 1002;
    private const int SellerX = 430;
    private const int SellerY = 380;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Registry =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-MsgItemOperationVendorTests-" + Guid.NewGuid().ToString("N"));
    private readonly string _usersRoot;
    private readonly string _playerItemsRoot;
    private readonly LegacyTradeStateStore _trades = new();
    private readonly WorldOperationBarrier _operations = new();
    private LegacyTradeRecoveryState? _recovery;

    public MsgItemOperationVendorTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _playerItemsRoot = Path.Combine(_root, "PlayerItems");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_playerItemsRoot);
    }

    [Fact]
    public async Task GoldAddUsesProtobufField2AndEchoesTheAcceptedListing()
    {
        const ulong price = 4_000_000_000;
        var item = Item(90_001, 410_339);
        var seller = ActivateSeller(CreateSeller(money: 300, conquerPoints: 50, Items(item)));
        var sent = new List<SentFrame>();
        var connection = CreateConnection("seller", sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, seller);
        using var log = new StringWriter();

        await DispatchAsync(
            connection,
            seller,
            sessions,
            CreateVendorOperationFrame(
                item.Uid,
                parameterField: 2,
                price,
                MsgItemOperationAction.AddVendingItemGold,
                timestampToken: 1_234),
            log);

        var updated = StoredSession(sessions, connection);
        Assert.NotSame(seller, updated);
        Assert.Equal(300, updated.Character.Money);
        Assert.Equal(50, updated.Character.ConquerPoints);
        Assert.Same(item, Assert.Single(updated.PlayerItems.InventoryItems));
        Assert.NotNull(updated.LegacyVendorListings);
        Assert.True(updated.LegacyVendorListings.TryGetListing(item.Uid, out var listing));
        Assert.Equal(LegacyVendorListingCostMode.Gold, listing.CostMode);
        Assert.Equal(checked((long)price), listing.Amount);

        var response = Assert.Single(sent);
        Assert.Equal("seller", response.Recipient);
        Assert.True(MsgItemOperationPacket.TryParse(ReadFrame(response.Bytes), out var echoed));
        Assert.Equal(item.Uid, echoed.ItemUid);
        Assert.Equal(price, echoed.Price);
        Assert.True(echoed.HasPrice);
        Assert.False(echoed.HasSecondaryValue);
        Assert.Equal(MsgItemOperationAction.AddVendingItemGold, echoed.Action);
        Assert.Equal(1_234U, echoed.TimestampToken);
    }

    [Fact]
    public async Task ConquerPointAddUsesProtobufField3AndEchoesTheAcceptedListing()
    {
        const uint price = 2_000_000_000;
        var item = Item(90_002, 410_340);
        var seller = ActivateSeller(CreateSeller(money: 300, conquerPoints: 50, Items(item)));
        var sent = new List<SentFrame>();
        var connection = CreateConnection("seller", sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, seller);
        using var log = new StringWriter();

        await DispatchAsync(
            connection,
            seller,
            sessions,
            CreateVendorOperationFrame(
                item.Uid,
                parameterField: 3,
                price,
                MsgItemOperationAction.AddVendingItemConquerPoints,
                timestampToken: 2_345),
            log);

        var updated = StoredSession(sessions, connection);
        Assert.NotSame(seller, updated);
        Assert.Equal(300, updated.Character.Money);
        Assert.Equal(50, updated.Character.ConquerPoints);
        Assert.Same(item, Assert.Single(updated.PlayerItems.InventoryItems));
        Assert.NotNull(updated.LegacyVendorListings);
        Assert.True(updated.LegacyVendorListings.TryGetListing(item.Uid, out var listing));
        Assert.Equal(LegacyVendorListingCostMode.CPs, listing.CostMode);
        Assert.Equal(price, listing.Amount);

        var response = Assert.Single(sent);
        Assert.Equal("seller", response.Recipient);
        Assert.True(MsgItemOperationPacket.TryParse(ReadFrame(response.Bytes), out var echoed));
        Assert.Equal(item.Uid, echoed.ItemUid);
        Assert.Equal(price, echoed.SecondaryValue);
        Assert.False(echoed.HasPrice);
        Assert.True(echoed.HasSecondaryValue);
        Assert.Equal(MsgItemOperationAction.AddVendingItemConquerPoints, echoed.Action);
        Assert.Equal(2_345U, echoed.TimestampToken);
    }

    [Fact]
    public async Task GoldAddRejectsTheField3BuilderShapeWithoutMutation()
    {
        var item = Item(90_010, 410_350);
        var seller = ActivateSeller(CreateSeller(money: 300, conquerPoints: 50, Items(item)));
        var sent = new List<SentFrame>();
        var connection = CreateConnection("seller", sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, seller);
        using var log = new StringWriter();

        await DispatchAsync(
            connection,
            seller,
            sessions,
            CreateVendorOperationFrame(
                item.Uid,
                parameterField: 3,
                parameterValue: 500,
                MsgItemOperationAction.AddVendingItemGold,
                timestampToken: 2_500),
            log);

        Assert.Empty(sent);
        Assert.Same(seller, StoredSession(sessions, connection));
        Assert.Empty(seller.LegacyVendorListings!.Listings);
        Assert.Contains("invalid field2/field3 presence", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InactiveAnimaPinBlocksProtectedItemListingBeforeMutation()
    {
        var item = Item(90_011, 410_351, animaItemId: 4_200_001);
        var seller = CreateSeller(money: 300, conquerPoints: 50, Items(item));
        var attributes = new Dictionary<string, string>(
            seller.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["PinCodeAnima"] = "abc123",
        };
        seller = ActivateSeller(seller with
        {
            Character = seller.Character with { Attributes = attributes },
        });
        var sent = new List<SentFrame>();
        var connection = CreateConnection("seller", sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, seller);
        using var log = new StringWriter();

        await DispatchAsync(
            connection,
            seller,
            sessions,
            CreateVendorOperationFrame(
                item.Uid,
                parameterField: 2,
                parameterValue: 500,
                MsgItemOperationAction.AddVendingItemGold,
                timestampToken: 2_501),
            log);

        Assert.Same(seller, StoredSession(sessions, connection));
        Assert.Empty(seller.LegacyVendorListings!.Listings);
        var denialFrame = Assert.Single(sent);
        Assert.Equal("seller", denialFrame.Recipient);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(denialFrame.Bytes), out var denial));
        Assert.Equal("Please Active Pincode", denial.Text);
        Assert.Contains("source=MsgItemUsuagePacket.AddVendingItemGold", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveRejectsWrongBoothWithoutMutationThenAcceptsExactBoothUid()
    {
        var item = Item(90_003, 410_341);
        var listings = ListingBook(item, LegacyVendorListingCostMode.Gold, amount: 125);
        var seller = ActivateSeller(
            CreateSeller(money: 300, conquerPoints: 50, Items(item)),
            listings);
        var sent = new List<SentFrame>();
        var connection = CreateConnection("seller", sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, seller);
        using var log = new StringWriter();

        await DispatchAsync(
            connection,
            seller,
            sessions,
            CreateVendorOperationFrame(
                item.Uid,
                parameterField: 3,
                SellerBoothUid + 1,
                MsgItemOperationAction.RemoveVendingItem,
                timestampToken: 3_456),
            log);

        Assert.Empty(sent);
        Assert.Same(seller, StoredSession(sessions, connection));
        Assert.True(seller.LegacyVendorListings!.Contains(item.Uid));
        Assert.Contains("reason=vendor-uid-mismatch", log.ToString(), StringComparison.Ordinal);

        await DispatchAsync(
            connection,
            seller,
            sessions,
            CreateVendorOperationFrame(
                item.Uid,
                parameterField: 3,
                SellerBoothUid,
                MsgItemOperationAction.RemoveVendingItem,
                timestampToken: 3_457),
            log);

        var updated = StoredSession(sessions, connection);
        Assert.NotSame(seller, updated);
        Assert.NotNull(updated.LegacyVendorListings);
        Assert.Empty(updated.LegacyVendorListings.Listings);
        Assert.Same(item, Assert.Single(updated.PlayerItems.InventoryItems));

        var response = Assert.Single(sent);
        Assert.Equal("seller", response.Recipient);
        Assert.True(MsgItemOperationPacket.TryParse(ReadFrame(response.Bytes), out var echoed));
        Assert.Equal(item.Uid, echoed.ItemUid);
        Assert.Equal(SellerBoothUid, echoed.SecondaryValue);
        Assert.Equal(MsgItemOperationAction.RemoveVendingItem, echoed.Action);
        Assert.Equal(3_457U, echoed.TimestampToken);
    }

    [Fact]
    public async Task GoldPurchaseTransfersExactItemAndCommitsPacketsSessionsAndFourFiles()
    {
        const long price = 250;
        var buyerItem = Item(80_001, 410_001);
        var listedItem = Item(90_101, 410_339);
        var sellerRemainder = Item(90_102, 410_340);
        var buyer = CreateBuyer(money: 1_000, conquerPoints: 200, Items(buyerItem));
        var seller = ActivateSeller(
            CreateSeller(money: 300, conquerPoints: 50, Items(listedItem, sellerRemainder)));
        var persistedBefore = SeedPersistence(buyer, seller);
        var sent = new List<SentFrame>();
        var buyerConnection = CreateConnection("buyer", sent);
        var sellerConnection = CreateConnection("seller", sent);
        var sessions = new WorldSessionStore();
        sessions.Set(buyerConnection, buyer);
        sessions.Set(sellerConnection, seller);
        using var log = new StringWriter();

        await DispatchAsync(
            sellerConnection,
            seller,
            sessions,
            CreateVendorOperationFrame(
                listedItem.Uid,
                parameterField: 2,
                checked((ulong)price),
                MsgItemOperationAction.AddVendingItemGold,
                timestampToken: 4_000),
            log);

        var listedSeller = StoredSession(sessions, sellerConnection);
        Assert.True(listedSeller.LegacyVendorListings!.Contains(listedItem.Uid));
        Assert.Single(sent);
        sent.Clear();

        await DispatchAsync(
            buyerConnection,
            buyer,
            sessions,
            CreateVendorOperationFrame(
                listedItem.Uid,
                parameterField: 3,
                SellerBoothUid,
                MsgItemOperationAction.BuyVendingItem,
                timestampToken: 4_001),
            log);

        var updatedBuyer = StoredSession(sessions, buyerConnection);
        var updatedSeller = StoredSession(sessions, sellerConnection);
        Assert.NotSame(buyer, updatedBuyer);
        Assert.NotSame(listedSeller, updatedSeller);
        Assert.Equal(750, updatedBuyer.Character.Money);
        Assert.Equal(200, updatedBuyer.Character.ConquerPoints);
        Assert.Equal(550, updatedSeller.Character.Money);
        Assert.Equal(50, updatedSeller.Character.ConquerPoints);
        Assert.DoesNotContain(updatedSeller.PlayerItems.InventoryItems, item => item.Uid == listedItem.Uid);
        var receivedItem = Assert.Single(updatedBuyer.PlayerItems.InventoryItems, item => item.Uid == listedItem.Uid);
        Assert.NotSame(listedItem, receivedItem);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Items(listedItem)),
            PlayerItemSnapshotLoader.ToLegacyBytes(Items(receivedItem)));
        Assert.Same(
            sellerRemainder,
            Assert.Single(updatedSeller.PlayerItems.InventoryItems));
        Assert.NotNull(updatedSeller.LegacyVendorListings);
        Assert.False(updatedSeller.LegacyVendorListings.Contains(listedItem.Uid));
        Assert.Empty(updatedSeller.LegacyVendorListings.Listings);

        Assert.Collection(
            sent,
            frame => AssertBuyerItemFrame(frame, listedItem),
            frame => AssertVendorOperationFrame(
                frame,
                "buyer",
                listedItem.Uid,
                SellerBoothUid,
                MsgItemOperationAction.BuyVendingItem,
                timestampToken: 4_001),
            frame => AssertVendorOperationFrame(
                frame,
                "seller",
                listedItem.Uid,
                SellerBoothUid,
                MsgItemOperationAction.RemoveVendingItem,
                timestampToken: 4_001),
            frame => AssertSellerInventoryRemovalFrame(frame, listedItem.Uid),
            frame => AssertWalletFrame(frame, "buyer", BuyerCharacterUid, expectedBalance: 750),
            frame => AssertWalletFrame(frame, "seller", SellerCharacterUid, expectedBalance: 550));

        Assert.Equal(4, persistedBefore.Count);
        foreach (var target in persistedBefore)
        {
            Assert.True(File.Exists(target.Key), target.Key);
            Assert.False(
                target.Value.AsSpan().SequenceEqual(File.ReadAllBytes(target.Key)),
                $"Expected vendor purchase to update {target.Key}.");
        }

        Assert.Equal((750L, 200L), ReadBalances(BuyerAccountId));
        Assert.Equal((550L, 50L), ReadBalances(SellerAccountId));
        var itemLoader = new PlayerItemSnapshotLoader(_playerItemsRoot);
        var persistedBuyerItems = itemLoader.LoadByCharacterUid(BuyerCharacterUid);
        var persistedSellerItems = itemLoader.LoadByCharacterUid(SellerCharacterUid);
        Assert.Equal(2, persistedBuyerItems.InventoryItems.Count);
        Assert.Contains(persistedBuyerItems.InventoryItems, item => item.Uid == buyerItem.Uid);
        Assert.Contains(persistedBuyerItems.InventoryItems, item => item.Uid == listedItem.Uid);
        Assert.Single(persistedSellerItems.InventoryItems);
        Assert.Equal(sellerRemainder.Uid, persistedSellerItems.InventoryItems[0].Uid);
        Assert.Equal(2, Directory.EnumerateFiles(_usersRoot).Count());
        Assert.Equal(2, Directory.EnumerateFiles(_playerItemsRoot).Count());
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task InsufficientFundsRejectPurchaseWithoutSessionPacketOrDiskMutation()
    {
        const long price = 250;
        var listedItem = Item(90_201, 410_339);
        var buyer = CreateBuyer(money: price - 1, conquerPoints: 200, PlayerItemSnapshot.Empty);
        var seller = ActivateSeller(
            CreateSeller(money: 300, conquerPoints: 50, Items(listedItem)),
            ListingBook(listedItem, LegacyVendorListingCostMode.Gold, price));
        var persistedBefore = SeedPersistence(buyer, seller);
        var sent = new List<SentFrame>();
        var buyerConnection = CreateConnection("buyer", sent);
        var sellerConnection = CreateConnection("seller", sent);
        var sessions = new WorldSessionStore();
        sessions.Set(buyerConnection, buyer);
        sessions.Set(sellerConnection, seller);
        using var log = new StringWriter();

        await DispatchAsync(
            buyerConnection,
            buyer,
            sessions,
            CreateVendorOperationFrame(
                listedItem.Uid,
                parameterField: 3,
                SellerBoothUid,
                MsgItemOperationAction.BuyVendingItem,
                timestampToken: 5_001),
            log);

        Assert.Empty(sent);
        Assert.Same(buyer, StoredSession(sessions, buyerConnection));
        Assert.Same(seller, StoredSession(sessions, sellerConnection));
        Assert.Empty(buyer.PlayerItems.InventoryItems);
        Assert.Same(listedItem, Assert.Single(seller.PlayerItems.InventoryItems));
        Assert.True(seller.LegacyVendorListings!.Contains(listedItem.Uid));
        Assert.Equal(price - 1, buyer.Character.Money);
        Assert.Equal(300, seller.Character.Money);
        Assert.Contains(
            "reason=purchase-plan-rejected:InsufficientBuyerBalance",
            log.ToString(),
            StringComparison.Ordinal);

        foreach (var target in persistedBefore)
        {
            Assert.True(
                target.Value.AsSpan().SequenceEqual(File.ReadAllBytes(target.Key)),
                $"Rejected vendor purchase changed {target.Key}.");
        }

        AssertNoTransactionArtifacts();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task DispatchAsync(
        ConquerTcpConnection connection,
        WorldSession session,
        WorldSessionStore sessions,
        byte[] frameBytes,
        TextWriter log)
    {
        _recovery ??= new(_usersRoot, _playerItemsRoot);
        Assert.True(_recovery.TryEnsureInitialized(out var recoveryReason), recoveryReason);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions,
            PlayerItemsRoot: _playerItemsRoot)
        {
            Trades = _trades,
            TradeOperations = _operations,
            TradeRecovery = _recovery,
        };

        var frame = ReadFrame(frameBytes);
        var buy = MsgItemOperationPacket.TryParse(frame, out var packet) &&
            packet.Action == MsgItemOperationAction.BuyVendingItem;
        using var operation = buy ? null : await _operations.EnterSharedAsync();
        await Registry.HandleAsync(context, frame);
    }

    private static ConquerTcpConnection CreateConnection(
        string recipient,
        List<SentFrame> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(new SentFrame(recipient, frame.ToArray()));
                return ValueTask.CompletedTask;
            });
    }

    private WorldSession CreateBuyer(long money, long conquerPoints, PlayerItemSnapshot items)
    {
        return CreateSession(
            BuyerAccountId,
            BuyerCharacterUid,
            "Buyer",
            x: SellerX - 1,
            money,
            conquerPoints,
            items);
    }

    private WorldSession CreateSeller(long money, long conquerPoints, PlayerItemSnapshot items)
    {
        return CreateSession(
            SellerAccountId,
            SellerCharacterUid,
            "Seller",
            SellerX,
            money,
            conquerPoints,
            items);
    }

    private WorldSession CreateSession(
        long accountId,
        uint characterUid,
        string name,
        int x,
        long money,
        long conquerPoints,
        PlayerItemSnapshot items)
    {
        return new WorldSession(
            AccountId: accountId,
            Username: name.ToLowerInvariant(),
            Character: new CharacterSnapshot(
                Uid: characterUid,
                Name: name,
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 10,
                FirstClass: 10,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId: MapId,
                X: x,
                Y: SellerY,
                PreviousMapId: MapId,
                PreviousMapX: x,
                PreviousMapY: SellerY,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: money,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: accountId.ToString(CultureInfo.InvariantCulture) + ".ini",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: items,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: _usersRoot);
    }

    private static WorldSession ActivateSeller(
        WorldSession seller,
        LegacyVendorListingBook? listings = null)
    {
        var attributes = new Dictionary<string, string>(
            seller.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["RuntimeVendorUid"] = SellerBoothUid.ToString(CultureInfo.InvariantCulture),
            ["RuntimeVendorMapId"] = seller.Character.MapId.ToString(CultureInfo.InvariantCulture),
            ["RuntimeVendorX"] = (seller.Character.X + 1).ToString(CultureInfo.InvariantCulture),
            ["RuntimeVendorY"] = seller.Character.Y.ToString(CultureInfo.InvariantCulture),
        };

        return seller with
        {
            Character = seller.Character with { Attributes = attributes },
            LegacyVendorListings = listings ?? LegacyVendorListingBook.Empty,
        };
    }

    private Dictionary<string, byte[]> SeedPersistence(params WorldSession[] sessions)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in sessions)
        {
            var characterPath = CharacterPath(session.AccountId);
            File.WriteAllText(
                characterPath,
                $$"""
                [Character]
                UID={{session.Character.Uid}}
                Name={{session.Character.Name}}
                Money={{session.Character.Money}}
                ConquerPoints={{session.Character.ConquerPoints}}
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var itemPath = ItemPath(session.Character.Uid);
            File.WriteAllBytes(itemPath, PlayerItemSnapshotLoader.ToLegacyBytes(session.PlayerItems));
            result.Add(characterPath, File.ReadAllBytes(characterPath));
            result.Add(itemPath, File.ReadAllBytes(itemPath));
        }

        return result;
    }

    private (long Money, long ConquerPoints) ReadBalances(long accountId)
    {
        var character = IniDocument.Parse(File.ReadAllText(CharacterPath(accountId)))
            .GetSection("Character");
        return (
            long.Parse(character["Money"], CultureInfo.InvariantCulture),
            long.Parse(character["ConquerPoints"], CultureInfo.InvariantCulture));
    }

    private string CharacterPath(long accountId)
    {
        return Path.Combine(
            _usersRoot,
            accountId.ToString(CultureInfo.InvariantCulture) + ".ini");
    }

    private string ItemPath(long characterUid)
    {
        return Path.Combine(
            _playerItemsRoot,
            characterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
    }

    private void AssertNoTransactionArtifacts()
    {
        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                _root,
                "*.vendor-purchase-*",
                SearchOption.AllDirectories));
    }

    private static LegacyVendorListingBook ListingBook(
        PlayerItemSnapshotItem item,
        LegacyVendorListingCostMode costMode,
        long amount)
    {
        var result = new LegacyVendorListingBook().TryAdd(item, costMode, amount);
        Assert.True(result.Added);
        return result.Book;
    }

    private static WorldSession StoredSession(
        WorldSessionStore sessions,
        ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static byte[] CreateVendorOperationFrame(
        uint itemUid,
        int parameterField,
        ulong parameterValue,
        MsgItemOperationAction action,
        uint timestampToken)
    {
        Assert.Contains(parameterField, new[] { 2, 3 });
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, fieldNumber: 1, itemUid);
        LegacyProtoWire.WriteVarintField(payload, parameterField, parameterValue);
        LegacyProtoWire.WriteVarintField(payload, fieldNumber: 6, (uint)action);
        LegacyProtoWire.WriteVarintField(payload, fieldNumber: 7, timestampToken);
        var writer = new PacketWriter(MsgItemOperationPacket.Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static void AssertBuyerItemFrame(
        SentFrame frame,
        PlayerItemSnapshotItem expectedItem)
    {
        Assert.Equal("buyer", frame.Recipient);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(frame.Bytes), out var packet));
        Assert.Equal(expectedItem.Uid, packet.Uid);
        Assert.Equal(expectedItem.ItemId, packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, packet.Mode);
    }

    private static void AssertVendorOperationFrame(
        SentFrame frame,
        string expectedRecipient,
        uint expectedItemUid,
        uint expectedBoothUid,
        MsgItemOperationAction expectedAction,
        uint timestampToken)
    {
        Assert.Equal(expectedRecipient, frame.Recipient);
        Assert.True(MsgItemOperationPacket.TryParse(ReadFrame(frame.Bytes), out var packet));
        Assert.Equal(expectedItemUid, packet.ItemUid);
        Assert.Equal(expectedBoothUid, packet.SecondaryValue);
        Assert.Equal(expectedAction, packet.Action);
        Assert.Equal(timestampToken, packet.TimestampToken);
    }

    private static void AssertSellerInventoryRemovalFrame(SentFrame frame, uint expectedItemUid)
    {
        Assert.Equal("seller", frame.Recipient);
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(frame.Bytes), out var packet));
        Assert.Equal(expectedItemUid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
    }

    private static void AssertWalletFrame(
        SentFrame frame,
        string expectedRecipient,
        uint expectedUid,
        ulong expectedBalance)
    {
        Assert.Equal(expectedRecipient, frame.Recipient);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frame.Bytes), out var packet));
        Assert.Equal(expectedUid, packet.Uid);
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.Money, update.Type);
        Assert.Equal(expectedBalance, update.Value);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }

    private static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] items)
    {
        return new PlayerItemSnapshot(items, [], [], [], []);
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId, uint animaItemId = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 100,
            MaximumDurability = 100,
            Position = 0,
            StackSize = 1,
            AnimaItemId = animaItemId,
        };
    }

    private sealed record SentFrame(string Recipient, byte[] Bytes);
}
