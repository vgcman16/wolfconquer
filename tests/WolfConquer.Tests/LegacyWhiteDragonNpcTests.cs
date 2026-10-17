using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyWhiteDragonNpcTests
{
    private const uint WhiteDragonNpcId = 25_004;
    private const uint RosewoodIncenseItemId = 3_007_565;

    [Theory]
    [InlineData(0, "Rosewood Incense", new byte[] { 1, byte.MaxValue })]
    [InlineData(1, "Different gifts", new byte[] { 2, 3, 4, 5, byte.MaxValue })]
    public async Task HandlerSendsWhiteDragonDialogs(byte optionId, string expectedText, byte[] expectedOptions)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(WhiteDragonNpcId, optionId, interactType: 1).ToFrameBytes()));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        foreach (var expectedOption in expectedOptions)
        {
            Assert.Contains(
                replies,
                reply => reply.InteractType == LegacyNpcReplyInteractType.Option && reply.OptionId == expectedOption);
        }

        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 226);
        Assert.All(replies, AssertAsciiReply);
        Assert.Contains("source=NpcHandler.WhiteDragon", log.ToString());
    }

    public static TheoryData<byte, ushort, uint, ushort> ExchangeCases => new()
    {
        { 2, 20, 3_007_568, 0 },
        { 3, 10, 3_007_567, 0 },
        { 4, 1, 3_007_566, 0 },
        { 5, 10, 3_005_128, 0 },
        { 5, 1, 3_007_566, 0 },
        { 2, 25, 3_007_568, 5 },
    };

    [Theory]
    [MemberData(nameof(ExchangeCases))]
    public async Task HandlerConsumesIncenseAndGrantsSourceReward(
        byte optionId,
        ushort incenseCount,
        uint expectedRewardItemId,
        ushort expectedRemainingIncense)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession([Item(50, RosewoodIncenseItemId, incenseCount)]);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(WhiteDragonNpcId, optionId, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Contains(updated.PlayerItems.InventoryItems, item => item.ItemId == expectedRewardItemId);
        if (expectedRemainingIncense == 0)
        {
            Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == RosewoodIncenseItemId);
        }
        else
        {
            var remaining = Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == RosewoodIncenseItemId);
            Assert.Equal(expectedRemainingIncense, remaining.StackSize);
        }

        Assert.Contains(ParseGameItems(sent), item => item.ItemId == expectedRewardItemId && item.Mode == LegacyGameItemPacket.AddItemMode);
        if (expectedRemainingIncense == 0)
        {
            Assert.Contains(ParseItemUsage(sent), packet => packet.Id == 50 && packet.Action == MsgItemUsageAction.RemoveInventory);
        }
        else
        {
            Assert.Contains(
                ParseGameItems(sent),
                item => item.ItemId == RosewoodIncenseItemId && item.Mode == LegacyGameItemPacket.UpdateItemMode);
        }

        Assert.Contains("WorldServer applied LegacyNpc WhiteDragon exchange", log.ToString());
        Assert.Contains("rewardItemId=" + expectedRewardItemId, log.ToString());
    }

    [Fact]
    public async Task HandlerRejectsMissingIncenseWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession([Item(60, RosewoodIncenseItemId, 9)]);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(WhiteDragonNpcId, optionId: 2, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        var incense = Assert.Single(updated.PlayerItems.InventoryItems);
        Assert.Equal(RosewoodIncenseItemId, incense.ItemId);
        Assert.Equal(9, incense.StackSize);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text.Contains("20 Rosewood Incense", StringComparison.Ordinal));
        Assert.DoesNotContain(ParseGameItems(sent), item => item.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains("source=NpcHandler.WhiteDragon", log.ToString());
    }

    private static WorldSession CreateSession(IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "white-dragon-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "WhiteDragonTester",
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
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId, ushort stackSize)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Position = 0,
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

    private static IReadOnlyList<LegacyGameItemPacket> ParseGameItems(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyGameItemPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<MsgItemUsagePacket> ParseItemUsage(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgItemUsagePacket>();
        foreach (var bytes in sent)
        {
            if (MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static void AssertAsciiReply(LegacyNpcReplyPacket reply)
    {
        Assert.All(reply.Text, character => Assert.InRange(character, (char)0x20, (char)0x7E));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
