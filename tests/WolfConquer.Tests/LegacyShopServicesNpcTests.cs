using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyShopServicesNpcTests
{
    [Theory]
    [InlineData(1U, 0, "NpcHandler.TheStorekeeper", true)]
    [InlineData(3U, 1, "NpcHandler.Pharmacist", false)]
    [InlineData(4U, 0, "NpcHandler.Armorer", true)]
    public async Task BasicShopNpcsSendSourceShapedOpenGuiAction(
        uint npcId,
        byte optionId,
        string sourceMethod,
        bool usesMapIdField)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002, x: 430, y: 378);

        var handled = await WorldPacketHandlers.TryHandleLegacyShopServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        var action = Assert.Single(ParseActions(sent));
        Assert.Equal(MsgActionType.OpenGuiNpc, action.Type);
        Assert.Equal(32U, action.DwParam);
        Assert.Equal(1_293_600U, action.ObjId);
        Assert.Equal((int)npcId, action.NpcId);
        Assert.Equal(430U, action.PositionX);
        Assert.Equal(378U, action.PositionY);
        Assert.Equal(usesMapIdField ? 1_002U : 0U, action.MapId);
        Assert.Equal(usesMapIdField ? 0 : 1_002, action.DwParam3);
        Assert.Contains("source=" + sourceMethod, log.ToString());
    }

    [Fact]
    public async Task FurnitureStoreDialogAndTeleportUseLegacyDestination()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(mapId: 1_002, x: 420, y: 360);
        sessions.Set(connection, session);

        Assert.True(await WorldPacketHandlers.TryHandleLegacyShopServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 30_161,
            optionId: 0,
            CancellationToken.None));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains("Twin City Furniture Store", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.Text == "Show~me~your~wares." && reply.OptionId == 1);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), AssertAsciiText);

        sent.Clear();
        Assert.True(await WorldPacketHandlers.TryHandleLegacyShopServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 30_161,
            optionId: 1,
            CancellationToken.None));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_511, updated.Character.MapId);
        Assert.Equal(52, updated.Character.X);
        Assert.Equal(70, updated.Character.Y);
        var teleport = Assert.Single(ParseActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_511U, teleport.DwParam);
        Assert.Equal(52U, teleport.PositionX);
        Assert.Equal(70U, teleport.PositionY);
        Assert.Contains("source=NpcHandler.FurnitureStore map=1511 x=52 y=70", log.ToString());
    }

    [Theory]
    [InlineData(832_662U, 0, "Which armor", 1, "NpcHandler.Tinter")]
    [InlineData(50_029U, 0, "random garment", 2, "NpcHandler.GarmentsDealer")]
    [InlineData(50_031U, 0, "discount prices", 111, "NpcHandler.MarketShop")]
    [InlineData(2_007U, 0, "excellent items", 4, "NpcHandler.ArchveSHop")]
    [InlineData(50_049U, 0, "random hairstyle or avatar", 2, "NpcHandler.BeauticianDealer")]
    [InlineData(3_085U, 2, "take all items out of the sashes", 3, "NpcHandler.MasterXuan")]
    public async Task DialogBranchesAreTranslatedAsciiAndUseSourceOptionIds(
        uint npcId,
        byte optionId,
        string expectedText,
        byte expectedOption,
        string sourceMethod)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();

        var handled = await WorldPacketHandlers.TryHandleLegacyShopServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.OptionId == expectedOption);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), AssertAsciiText);
        Assert.Contains("source=" + sourceMethod, log.ToString());
    }

    [Theory]
    [InlineData(832_662U, 3, "NpcHandler.Tinter", "38457-38527")]
    [InlineData(50_029U, 1, "NpcHandler.GarmentsDealer", "56297-56416")]
    [InlineData(50_031U, 120, "NpcHandler.MarketShop", "56537-56817")]
    [InlineData(50_049U, 2, "NpcHandler.BeauticianDealer", "57237-57357")]
    [InlineData(3_085U, 3, "NpcHandler.MasterXuan", "38681-38800")]
    [InlineData(10_002U, 0, "NpcHandler.Barber", "36480-38172")]
    public async Task MutatingOrStatefulBranchesSendUnavailableDialogAndSourceGap(
        uint npcId,
        byte optionId,
        string sourceMethod,
        string sourceLines)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(conquerPoints: 5_000_000, body: 1003);
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyShopServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains("unavailable here", StringComparison.Ordinal));
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), AssertAsciiText);

        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.NotNull(stored);
        Assert.Equal(5_000_000, stored.Character.ConquerPoints);
        Assert.Equal(1003, stored.Character.Body);
        Assert.Empty(stored.PlayerItems.InventoryItems);

        var output = log.ToString();
        Assert.Contains("PARITY-GAP", output);
        Assert.Contains("gap=LegacyNpc-ShopServices", output);
        Assert.Contains("source=" + sourceMethod, output);
        Assert.Contains("sourceLines=" + sourceLines, output);
        Assert.Contains("optionId=" + optionId, output);
    }

    [Fact]
    public async Task UnknownNpcOrOptionIsNotHandled()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();

        Assert.False(await WorldPacketHandlers.TryHandleLegacyShopServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId: 123_456,
            optionId: 0,
            CancellationToken.None));
        Assert.False(await WorldPacketHandlers.TryHandleLegacyShopServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId: 30_161,
            optionId: 99,
            CancellationToken.None));
        Assert.Empty(sent);
    }

    private static WorldSession CreateSession(
        long conquerPoints = 0,
        int body = 1003,
        int mapId = 1_002,
        int x = 430,
        int y = 380)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "legacy-shop-services-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "ShopServiceTester",
                Body: body,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: mapId,
                X: x,
                Y: y,
                PreviousMapId: mapId,
                PreviousMapX: x,
                PreviousMapY: y,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<MsgActionPacket> ParseActions(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertAsciiText(LegacyNpcReplyPacket reply)
    {
        AssertAsciiText(reply.Text);
    }

    private static void AssertAsciiText(string text)
    {
        Assert.All(text, character => Assert.InRange(character, (char)0x20, (char)0x7E));
    }
}
