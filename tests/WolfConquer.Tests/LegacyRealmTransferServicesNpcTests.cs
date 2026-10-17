using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRealmTransferServicesNpcTests
{
    public static TheoryData<uint, string, byte[]> RootDialogCases => new()
    {
        { 15_702, "server transfers", [1, 10, byte.MaxValue] },
        { 52_100, "keeper of the [EU] server", [1, 10, 11] },
        { 18_787, "I serve the Realm", [byte.MaxValue] },
        { 17_400, "A real hero must harbor great ambition", [1, 10, 20, 30, 40, 50, byte.MaxValue] },
        { 52_300, "vote manager", [40, 41, byte.MaxValue] },
        { 52_001, "Football Tournament", [1, 2, byte.MaxValue] },
        { 421, "Saltpeter", [1, byte.MaxValue] },
        { 15_805, "gender reassignment", [1, 2, byte.MaxValue] },
    };

    [Theory]
    [MemberData(nameof(RootDialogCases))]
    public async Task HandlerSendsAsciiRootDialogsWithSourceOptionIds(
        uint npcId,
        string expectedText,
        byte[] expectedOptions)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["KingDomDeeds"] = "150",
                ["VotePoints"] = "7",
            });

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            CreateContext(sent, log, session),
            session,
            npcId,
            optionId: 0,
            CancellationToken.None));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog), reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        Assert.Equal(expectedOptions, OptionIds(replies));
        Assert.All(replies, AssertAsciiReply);
        Assert.Contains("source=NpcHandler.", log.ToString());
    }

    [Theory]
    [InlineData(4_294_967_269u, 723_467u, 3, "Gold Prize", new byte[] { 1, byte.MaxValue })]
    [InlineData(4_294_967_277u, 3_600_024u, 5, "20 percent", new byte[] { 1, 10 })]
    public async Task ItemRewardNpcRootsPreserveLegacyOptionsWhenItemsArePresent(
        uint npcId,
        uint itemId,
        ushort count,
        string expectedText,
        byte[] expectedOptions)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var session = CreateSession(inventoryItems: [InventoryItem(itemId, count)]);

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            CreateContext(sent, log, session),
            session,
            npcId,
            optionId: 0,
            CancellationToken.None));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog), reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        Assert.Equal(expectedOptions, OptionIds(replies));
        Assert.All(replies, AssertAsciiReply);
        Assert.Contains("source=NpcHandler.", log.ToString());
    }

    [Theory]
    [InlineData(52_100u, 10, "To go to another server", new byte[] { 11 })]
    [InlineData(17_400u, 10, "cross-server missions", new byte[] { 90, 60, 11, 80, 70 })]
    [InlineData(52_300u, 41, "Use vote points", new byte[] { 42, 48, 43, 44, 45, 46, 47 })]
    [InlineData(52_001u, 2, "Football points", new byte[] { 3, 4, byte.MaxValue })]
    [InlineData(15_805u, 2, "must be single", new byte[] { 3, byte.MaxValue })]
    public async Task InformationalMenusPreserveSourceOptionIdsAndAscii(
        uint npcId,
        byte optionId,
        string expectedText,
        byte[] expectedOptions)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var session = CreateSession();

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            CreateContext(sent, log, session),
            session,
            npcId,
            optionId,
            CancellationToken.None));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog), reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        Assert.Equal(expectedOptions, OptionIds(replies));
        Assert.All(replies, AssertAsciiReply);
        Assert.Contains("source=NpcHandler.", log.ToString());
    }

    [Fact]
    public async Task RealmEnvoyForeignServerRootOffersRealmReturnWithSourceOptions()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var session = CreateSession(attributes: new Dictionary<string, string> { ["OnMyOwnServer"] = "false" });

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            CreateContext(sent, log, session),
            session,
            npcId: 18_787,
            optionId: 0,
            CancellationToken.None));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains("return to the Realm", StringComparison.Ordinal));
        Assert.Equal([1, byte.MaxValue], OptionIds(replies));
        Assert.All(replies, AssertAsciiReply);
        Assert.Contains("source=NpcHandler.RealmEnvoy", log.ToString());
    }

    [Fact]
    public async Task GreenSnakeTeleportsToSourceSnakeArrayDestination()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var session = CreateSession(mapId: 1_002, x: 410, y: 355);
        var sessions = new WorldSessionStore();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 30_115,
            optionId: 4,
            CancellationToken.None));

        var teleport = Assert.Single(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_063U, teleport.DwParam);
        Assert.Equal(444U, teleport.PositionX);
        Assert.Equal(355U, teleport.PositionY);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_063, updated.Character.MapId);
        Assert.Equal(444, updated.Character.X);
        Assert.Equal(355, updated.Character.Y);
        Assert.Contains("source=NpcHandler.GreenSnake map=1063 x=444 y=355", log.ToString());
    }

    [Fact]
    public async Task SafeGappedBranchesSendUnavailableDialogAndSourceEvidenceLog()
    {
        var cases = new[]
        {
            new GapCase(15_702, 10, "NpcHandler.ServerTransfer", "21419-21470", CreateSession()),
            new GapCase(52_100, 1, "NpcHandler.MrRayzo_EU", "24258-24303", CreateSession()),
            new GapCase(18_787, 1, "NpcHandler.RealmEnvoy", "24354-24378", CreateSession()),
            new GapCase(17_400, 1, "NpcHandler.KingdomMissionEnvoy", "24381-24829", CreateSession()),
            new GapCase(17_400, 31, "NpcHandler.KingdomMissionEnvoy", "24381-24829", CreateSession()),
            new GapCase(52_001, 1, "NpcHandler.Football", "25901-26015", CreateSession()),
        };

        foreach (var testCase in cases)
        {
            using var log = new StringWriter();
            var sent = new List<byte[]>();

            Assert.True(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
                CreateContext(sent, log, testCase.Session),
                testCase.Session,
                testCase.NpcId,
                testCase.OptionId,
                CancellationToken.None));

            var replies = ParseNpcReplies(sent);
            Assert.Contains(replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog), reply => reply.Text.Contains("unavailable here", StringComparison.Ordinal));
            Assert.All(replies, AssertAsciiReply);

            var output = log.ToString();
            Assert.Contains("PARITY-GAP LegacyNpc RealmTransferServices", output);
            Assert.Contains("source=" + testCase.Source, output);
            Assert.Contains("sourceLines=" + testCase.SourceLines, output);
            Assert.Contains("npcId=" + testCase.NpcId, output);
            Assert.Contains("optionId=" + testCase.OptionId, output);
        }
    }

    [Fact]
    public async Task UnknownNpcAndUnknownOptionReturnFalse()
    {
        using var log = new StringWriter();
        var session = CreateSession();

        Assert.False(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            CreateContext([], log, session),
            session,
            npcId: 123,
            optionId: 0,
            CancellationToken.None));

        Assert.False(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            CreateContext([], log, session),
            session,
            npcId: 30_115,
            optionId: 99,
            CancellationToken.None));
    }

    private sealed record GapCase(
        uint NpcId,
        byte OptionId,
        string Source,
        string SourceLines,
        WorldSession Session);

    private static byte[] OptionIds(IReadOnlyList<LegacyNpcReplyPacket> replies)
    {
        return replies
            .Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option)
            .Select(reply => reply.OptionId)
            .ToArray();
    }

    private static WorldPacketHandlerContext CreateContext(
        List<byte[]> sent,
        TextWriter log,
        WorldSession session)
    {
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        return new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);
    }

    private static WorldSession CreateSession(
        long mapId = 1_002,
        long x = 430,
        long y = 378,
        long level = 140,
        long reborn = 0,
        IReadOnlyDictionary<string, string>? attributes = null,
        IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "legacy-realm-transfer-services-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "RealmTester",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: reborn,
                Level: level,
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
                Money: 50_000,
                ConquerPoints: 2_000,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshotItem InventoryItem(uint itemId, ushort stackSize)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = itemId,
            ItemId = itemId,
            Position = 40,
            StackSize = stackSize,
        };
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

    private static IReadOnlyList<MsgActionPacket> ParseMsgActions(IEnumerable<byte[]> sent)
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

    private static void AssertAsciiReply(LegacyNpcReplyPacket reply)
    {
        Assert.DoesNotContain("\\u", reply.Text);
        Assert.All(reply.Text, character => Assert.InRange(character, (char)0x20, (char)0x7E));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
