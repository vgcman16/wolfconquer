using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyItemPackServicesNpcTests
{
    public static TheoryData<uint, uint, ushort, string, byte[], string> RootDialogCases => new()
    {
        { 4_294_967_264, 3_007_283, 1, "+3 bound steeds", [1, 5, 10, byte.MaxValue], "NpcHandler.Steed3Pack" },
        { 4_294_967_292, 3_004_249, 1, "Choose what Refineries with level 6 you need .", [32, 39], "NpcHandler.SelectSacredMaterial" },
        { 19_436, 0, 0, "Spirit of Legend", [1, 30, byte.MaxValue], "NpcHandler.IdealDesgner" },
        { 19_452, 0, 0, "Elite Points", [byte.MaxValue], "NpcHandler.AstralPheonix" },
        { 4_294_967_279, 3_008_732, 1, "30 pieces", [1, 4, byte.MaxValue], "NpcHandler.DazzlingDiamondBox" },
        { 4_294_967_293, 3_004_248, 1, "Select the p7 weapon you need.", [1, 8], "NpcHandler.SelectP7EquipmentSoulPack" },
        { 4_294_967_294, 3_004_247, 1, "Select the p7 weapon you need.", [1, 8], "NpcHandler.SelectP7WeaponSoulPack" },
        { 16_849, 0, 0, "mysterious treasure", [1, 2, byte.MaxValue], "NpcHandler.P7SacredTreasuresEnvoy" },
        { 16_833, 0, 0, "Bloodshed Sea", [1, byte.MaxValue], "NpcHandler.SarckSoldier" },
        { 16_834, 0, 0, "Nemesis Tyrant", [1, 3, byte.MaxValue], "NpcHandler.SarckSoldierEnctrance" },
    };

    [Theory]
    [MemberData(nameof(RootDialogCases))]
    public async Task RootDialogsAreAsciiAndExposeExpectedOptionsAndSource(
        uint npcId,
        uint itemId,
        ushort itemCount,
        string expectedText,
        byte[] expectedOptions,
        string source)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(items: CreateItems(
            itemId,
            itemCount,
            bound: npcId is 4_294_967_264 or 4_294_967_265 ? (byte)1 : (byte)0));

        var handled = await WorldPacketHandlers.TryHandleLegacyItemPackServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: new WorldSessionStore()),
            session,
            npcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        foreach (var option in expectedOptions)
        {
            Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option && reply.OptionId == option);
        }

        Assert.All(replies, AssertAsciiReply);
        Assert.Contains("source=" + source, log.ToString());
    }

    public static TheoryData<uint, byte, uint, ushort, string, byte[], string> SelectionDialogCases => new()
    {
        { 4_294_967_264, 10, 3_007_283, 1, "+3 Black Steed", [11, byte.MaxValue], "NpcHandler.Steed3Pack" },
        { 4_294_967_292, 39, 3_004_249, 1, "Choose what Refineries with level 6 you need .", [15, 16, 21], "NpcHandler.SelectSacredMaterial" },
        { 19_436, 30, 0, 0, "phoenix cups", [31, 32, 33, 34], "NpcHandler.IdealDesgner" },
        { 4_294_967_294, 8, 3_004_247, 1, "Selecte the p7 weapon you need.", [9, 15, 20, 21], "NpcHandler.SelectP7WeaponSoulPack" },
    };

    [Theory]
    [MemberData(nameof(SelectionDialogCases))]
    public async Task SelectionDialogsPreserveLegacyOptionIds(
        uint npcId,
        byte optionId,
        uint itemId,
        ushort itemCount,
        string expectedText,
        byte[] expectedOptions,
        string source)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(items: CreateItems(
            itemId,
            itemCount,
            bound: npcId is 4_294_967_264 or 4_294_967_265 ? (byte)1 : (byte)0));

        var handled = await WorldPacketHandlers.TryHandleLegacyItemPackServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: new WorldSessionStore()),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        foreach (var option in expectedOptions)
        {
            Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option && reply.OptionId == option);
        }

        Assert.All(replies, AssertAsciiReply);
        Assert.Contains("source=" + source, log.ToString());
    }

    [Fact]
    public async Task UnknownNpcOrOptionIsNotHandled()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(items: CreateItems(3_007_287, 1, bound: 1));

        Assert.False(await WorldPacketHandlers.TryHandleLegacyItemPackServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId: 123,
            optionId: 0,
            CancellationToken.None));

        Assert.False(await WorldPacketHandlers.TryHandleLegacyItemPackServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId: 4_294_967_265,
            optionId: 99,
            CancellationToken.None));
    }

    [Theory]
    [InlineData(16_833U, 1, 3_846, 381, 271, "NpcHandler.SarckSoldier")]
    [InlineData(16_834U, 3, 3_055, 62, 89, "NpcHandler.SarckSoldierEnctrance")]
    [InlineData(16_852U, 3, 3_055, 62, 89, "NpcHandler.SarckSoldierSuppluPoint1")]
    public async Task SarckSoldierTeleportsUseLegacyDestinations(
        uint npcId,
        byte optionId,
        int expectedMapId,
        int expectedX,
        int expectedY,
        string source)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyItemPackServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(expectedMapId, updatedSession.Character.MapId);
        Assert.Equal(expectedX, updatedSession.Character.X);
        Assert.Equal(expectedY, updatedSession.Character.Y);
        Assert.Contains("WorldServer sent LegacyNpc message-box", log.ToString());
        Assert.Contains("WorldServer sent LegacyNpc teleport transition", log.ToString());
        Assert.Contains("source=" + source, log.ToString());
    }

    [Fact]
    public async Task SarckSoldierSupplyPointOpensSeaPotionShop()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();

        var handled = await WorldPacketHandlers.TryHandleLegacyItemPackServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: new WorldSessionStore()),
            session,
            npcId: 16_856,
            optionId: 2,
            CancellationToken.None);

        Assert.True(handled);
        var bytes = Assert.Single(sent);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(bytes), out var action));
        Assert.Equal(MsgActionType.OpenGuiNpc, action.Type);
        Assert.Equal(32U, action.DwParam);
        Assert.Equal(16_856, action.NpcId);
        Assert.Equal(session.Character.MapId, action.DwParam3);
        Assert.Contains("source=NpcHandler.SarckSoldierSuppluPoint5", log.ToString());
    }

    [Fact]
    public async Task P7SacredTreasuresOption11MessagesBeforeGaleShallowTeleport()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyItemPackServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 16_849,
            optionId: 11,
            CancellationToken.None);

        Assert.True(handled);
        Assert.NotEmpty(sent);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[0]), out var message));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, message.InteractType);
        Assert.Equal("You~arrived~at~the~Gale~Shallow.", message.Text);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(10_137, updated.Character.MapId);
        Assert.Equal(100, updated.Character.X);
        Assert.Equal(403, updated.Character.Y);
        Assert.Contains("source=NpcHandler.P7SacredTreasuresEnvoy", log.ToString());
        Assert.Contains("map=10137 x=100 y=403", log.ToString());
    }

    private static PlayerItemSnapshotItem[] CreateItems(uint itemId, ushort count, byte bound = 0)
    {
        if (itemId == 0 || count == 0)
        {
            return [];
        }

        return
        [
            new PlayerItemSnapshotItem
            {
                Uid = 501,
                ItemId = itemId,
                StackSize = count,
                Bound = bound,
            },
        ];
    }

    private static WorldSession CreateSession(
        long conquerPoints = 0,
        IReadOnlyList<PlayerItemSnapshotItem>? items = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "legacy-item-pack-services-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "PackTester",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 380,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 380,
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty with
            {
                InventoryItems = (items ?? []).ToArray(),
            },
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
            Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
            packets.Add(packet);
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertAsciiReply(LegacyNpcReplyPacket reply)
    {
        Assert.All(
            reply.Text,
            character => Assert.True(character == '\n' || character is >= (char)0x20 and <= (char)0x7E));
    }
}
