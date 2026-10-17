using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGuildConductorsNpcTests
{
    [Fact]
    public async Task HandlerGuildConductorOnGuildWarMapChargesAndTeleportsToSourceConductor()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_038, money: 2_000);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(101_621, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_000, updated.Character.Money);
        Assert.Equal(1_000, updated.Character.MapId);
        Assert.Equal(307, updated.Character.X);
        Assert.Equal(279, updated.Character.Y);

        Assert.Contains(ParseMoneyUpdates(sent), value => value == 1_000UL);
        var teleport = Assert.Single(ParseTeleports(sent));
        Assert.Equal(1_000U, teleport.DwParam);
        Assert.Equal(307U, teleport.PositionX);
        Assert.Equal(279U, teleport.PositionY);
        Assert.Contains("source=NpcHandler.GuildConductorsProces", log.ToString());
    }

    [Fact]
    public async Task HandlerGuildConductorOutsideGuildWarMapFallsThroughToParityGap()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002, money: 2_000);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(101_621, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.DoesNotContain(ParseTeleports(sent), _ => true);
        Assert.Contains("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task HandlerGuildConductorBackChargesAndTeleportsToGuildWarMap()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_000, money: 700);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(101_620, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(200, updated.Character.Money);
        Assert.Equal(1_038, updated.Character.MapId);
        Assert.Equal(348, updated.Character.X);
        Assert.Equal(339, updated.Character.Y);
        Assert.Contains(ParseMoneyUpdates(sent), value => value == 200UL);
        Assert.Contains("source=NpcHandler.GuildCondTeleBack", log.ToString());
    }

    [Fact]
    public async Task HandlerSpaceMarksRequiresDynamicIdZeroAndWaterClass()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_002,
            money: 2_000,
            classCode: 135,
            attributes: new Dictionary<string, string> { ["DynamicID"] = "44" });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(10_031, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.DoesNotContain(ParseGameItems(sent), item => item.ItemId == 1_060_025U);
        Assert.Contains("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task HandlerSpaceMarksWaterClassBuysSourceScroll()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_002,
            money: 2_000,
            classCode: 135,
            attributes: new Dictionary<string, string> { ["DynamicID"] = "0" });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(10_036, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_000, updated.Character.Money);
        var item = Assert.Single(updated.PlayerItems.InventoryItems);
        Assert.Equal(1_060_038U, item.ItemId);
        Assert.Contains(ParseGameItems(sent), packet => packet.ItemId == 1_060_038U);
        Assert.Contains("source=NpcHandler.SpaceMarks", log.ToString());
    }

    [Fact]
    public async Task HandlerGuildTeleporterPurchaseRequiresWinningGuildDeadProcess()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_038,
            money: 150_000,
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
                ["LegacyNewGuildWarWinnerGuildID"] = "77",
                ["LegacyNewGuildWarProcess"] = "Dead",
            });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(101_621, optionId: 4, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(50_000, updated.Character.Money);
        var item = Assert.Single(updated.PlayerItems.InventoryItems);
        Assert.Equal(720_022U, item.ItemId);
        Assert.Contains(ParseGameItems(sent), packet => packet.ItemId == 720_022U);
    }

    private static WorldSession CreateSession(
        int mapId,
        long money,
        long classCode = 15,
        IReadOnlyDictionary<string, string>? attributes = null,
        IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "guild-conductor-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_601,
                Name: "ConductorTest",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: mapId,
                X: 300,
                Y: 300,
                PreviousMapId: mapId,
                PreviousMapX: 300,
                PreviousMapY: 300,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: money,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: attributes ?? new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
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

    private static IReadOnlyList<MsgActionPacket> ParseTeleports(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Type == MsgActionType.Teleport)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<ulong> ParseMoneyUpdates(IEnumerable<byte[]> sent)
    {
        var values = new List<ulong>();
        foreach (var bytes in sent)
        {
            if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                continue;
            }

            foreach (var entry in packet.Updates)
            {
                if (entry.Type == LegacyMsgUpdateDataType.Money)
                {
                    values.Add(entry.Value);
                }
            }
        }

        return values;
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
