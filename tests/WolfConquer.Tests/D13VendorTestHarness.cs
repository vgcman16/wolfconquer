using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed class D13VendorTestHarness : IDisposable
{
    internal const long BuyerAccountId = 101;
    internal const uint BuyerCharacterUid = 1_001;
    internal const long SellerAccountId = 202;
    internal const uint SellerCharacterUid = 2_002;
    internal const uint SellerBoothUid = 102_002;
    internal const int MapId = 1_002;
    internal const int SellerX = 430;
    internal const int SellerY = 380;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Registry =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-D13VendorTests-" + Guid.NewGuid().ToString("N"));

    internal D13VendorTestHarness()
    {
        UsersRoot = Path.Combine(_root, "Users");
        PlayerItemsRoot = Path.Combine(_root, "PlayerItems");
        Directory.CreateDirectory(UsersRoot);
        Directory.CreateDirectory(PlayerItemsRoot);
    }

    internal string UsersRoot { get; }

    internal string PlayerItemsRoot { get; }

    internal WorldSessionStore Sessions { get; } = new();

    internal List<D13VendorSentFrame> Sent { get; } = [];

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    internal ConquerTcpConnection CreateConnection(string recipient, Action<byte[]>? afterSend = null)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9_958),
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                Sent.Add(new D13VendorSentFrame(recipient, bytes));
                afterSend?.Invoke(bytes);
                return ValueTask.CompletedTask;
            });
    }

    internal WorldSession CreateBuyer(
        long money,
        long conquerPoints,
        PlayerItemSnapshot items,
        int x = SellerX - 1)
    {
        return CreateSession(
            BuyerAccountId,
            BuyerCharacterUid,
            "Buyer",
            x,
            money,
            conquerPoints,
            items);
    }

    internal WorldSession CreateSeller(long money, long conquerPoints, PlayerItemSnapshot items)
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

    internal static WorldSession ActivateSeller(
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

    internal async Task<string> DispatchAsync(
        ConquerTcpConnection connection,
        WorldSession session,
        MsgItemUsagePacket packet,
        ConquerProtocolProfile protocolProfile = ConquerProtocolProfile.VisibleDx8D13)
    {
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: Sessions,
            PlayerItemsRoot: PlayerItemsRoot,
            ProtocolProfile: protocolProfile);
        await Registry.HandleAsync(context, ReadFrame(packet.ToFrameBytes()));
        return log.ToString();
    }

    internal void SeedPersistence(params WorldSession[] sessions)
    {
        foreach (var session in sessions)
        {
            File.WriteAllText(
                CharacterPath(session.AccountId),
                $$"""
                [Character]
                UID={{session.Character.Uid}}
                Name={{session.Character.Name}}
                Money={{session.Character.Money}}
                ConquerPoints={{session.Character.ConquerPoints}}
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            new PlayerItemSnapshotLoader(PlayerItemsRoot)
                .SaveByCharacterUid(session.Character.Uid, session.PlayerItems);
        }
    }

    internal (long Money, long ConquerPoints) ReadBalances(long accountId)
    {
        var character = IniDocument.Parse(File.ReadAllText(CharacterPath(accountId)))
            .GetSection("Character");
        return (
            long.Parse(character["Money"], CultureInfo.InvariantCulture),
            long.Parse(character["ConquerPoints"], CultureInfo.InvariantCulture));
    }

    internal WorldSession StoredSession(ConquerTcpConnection connection)
    {
        Assert.True(Sessions.TryGet(connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    internal static LegacyVendorListingBook ListingBook(
        params (PlayerItemSnapshotItem Item, LegacyVendorListingCostMode CostMode, long Amount)[] listings)
    {
        var book = LegacyVendorListingBook.Empty;
        foreach (var (item, costMode, amount) in listings)
        {
            var result = book.TryAdd(item, costMode, amount);
            Assert.True(result.Added);
            book = result.Book;
        }

        return book;
    }

    internal static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] items)
    {
        return new PlayerItemSnapshot(items, [], [], [], []);
    }

    internal static PlayerItemSnapshotItem Item(
        uint uid,
        uint itemId,
        uint purificationItemId = 0,
        uint refineryEffectId = 0,
        uint perfectionLevel = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 100,
            MaximumDurability = 100,
            Position = 0,
            StackSize = 1,
            PurificationItemId = purificationItemId,
            PurificationLevel = purificationItemId == 0 ? 0U : 3U,
            RefineryEffectId = refineryEffectId,
            RefineryEffectLevel = refineryEffectId == 0 ? 0U : 2U,
            RefineryEffectPercent = refineryEffectId == 0 ? 0U : 10U,
            RefineryEffectPercent2 = refineryEffectId == 0 ? 0U : 20U,
            PerfectionLevel = perfectionLevel,
            OwnerName = "OriginalOwner",
        };
    }

    internal static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
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
            UsersRoot: UsersRoot);
    }

    private string CharacterPath(long accountId)
    {
        return Path.Combine(UsersRoot, accountId.ToString(CultureInfo.InvariantCulture) + ".ini");
    }
}

internal sealed record D13VendorSentFrame(string Recipient, byte[] Bytes);
