using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDeitylandSupportItemNpcTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyDeitylandSupportItemNpcTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RelicSpiritItemUseOpensSourceDialogAndSupportsNextPageReply()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spirit = new PlayerItemSnapshotItem
        {
            Uid = 501,
            ItemId = 3_307_142,
            StackSize = 1,
        };
        var session = CreateSession() with
        {
            PlayerItems = PlayerItemSnapshot.Empty with { InventoryItems = [spirit] },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var playerItemsRoot = Path.Combine(_root, "PlayersItems");
        new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(
            session.Character.Uid,
            session.PlayerItems);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = LegacyItemUseComponentTestOwner.Prepare(new WorldPacketHandlerContext(
            connection, log, Session: session, Sessions: sessions, PlayerItemsRoot: playerItemsRoot), _root, ref session);

        await registry.HandleAsync(
            context,
            ReadFrame(CreateItemUse(spirit.Uid).ToFrameBytes()));

        Assert.Equal(15, sent.Count);
        var heading = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Shaping of Relic Spirit", heading.Text);
        var next = AssertNpcReply(sent[13], LegacyNpcReplyInteractType.Option);
        Assert.Equal(8, next.OptionId);
        Assert.Equal("Next.", next.Text);
        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.Equal(3_307_142U, activeSession?.ActiveNpcId);

        var countBeforeNextPage = sent.Count;
        await registry.HandleAsync(
            context with { Session = activeSession },
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 8, interactType: 1, npcId: 0)));

        Assert.Equal(10, sent.Count - countBeforeNextPage);
        var mysteryFruit = AssertNpcReply(sent[countBeforeNextPage + 6], LegacyNpcReplyInteractType.Option);
        Assert.Equal(9, mysteryFruit.OptionId);
        Assert.Equal("1~Mystery~Fruit.", mysteryFruit.Text);
        Assert.DoesNotContain("clicked NPC is not currently visible", log.ToString());
        Assert.Contains("source=NpcHandler.RelicSpirit", log.ToString());
    }

    [Fact]
    public async Task RelicCrystalItemUsePreviewsAndRepairsEquippedRelic()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var crystal = new PlayerItemSnapshotItem
        {
            Uid = 601,
            ItemId = 3_306_885,
            StackSize = 2,
        };
        var relic = new PlayerItemSnapshotItem
        {
            Uid = 602,
            ItemId = 4_100_001,
            Position = 13,
            Durability = 900,
            MaximumDurability = 1_100,
            StackSize = 1,
        };
        var session = CreateSession() with
        {
            PlayerItems = PlayerItemSnapshot.Empty with
            {
                InventoryItems = [crystal],
                EquipmentItems = [relic],
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var playerItemsRoot = Path.Combine(_root, "PlayersItems-Crystal");
        new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(
            session.Character.Uid,
            session.PlayerItems);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = LegacyItemUseComponentTestOwner.Prepare(new WorldPacketHandlerContext(
            connection, log, Session: session, Sessions: sessions, PlayerItemsRoot: playerItemsRoot), _root, ref session);

        await registry.HandleAsync(
            context,
            ReadFrame(CreateItemUse(crystal.Uid).ToFrameBytes()));

        Assert.Equal(4, sent.Count);
        var intro = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Relic Crystal can be used to repair your equipped Relic", intro.Text);
        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.Equal(3_306_885U, activeSession?.ActiveNpcId);

        var countBeforePreview = sent.Count;
        await registry.HandleAsync(
            context with { Session = activeSession },
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(9, sent.Count - countBeforePreview);
        var required = AssertNpcReply(sent[countBeforePreview + 3], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Relic Crystals Required: 2", required.Text);
        var repairOption = AssertNpcReply(sent[countBeforePreview + 7], LegacyNpcReplyInteractType.Option);
        Assert.Equal(3, repairOption.OptionId);

        var countBeforeRepair = sent.Count;
        Assert.True(sessions.TryGet(connection, out var previewSession));
        await registry.HandleAsync(
            context with { Session = previewSession },
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 3, interactType: 1, npcId: 0)));

        Assert.Equal(2, sent.Count - countBeforeRepair);
        AssertRemoveInventory(sent[countBeforeRepair], crystal.Uid);
        var repaired = AssertGameItem(sent[countBeforeRepair + 1], relic.ItemId);
        Assert.Equal(relic.MaximumDurability, repaired.Durability);
        Assert.True(sessions.TryGet(connection, out var repairedSession));
        Assert.DoesNotContain(repairedSession!.PlayerItems.InventoryItems, item => item.ItemId == 3_306_885);
        Assert.Equal(
            relic.MaximumDurability,
            Assert.Single(repairedSession.PlayerItems.EquipmentItems, item => item.Uid == relic.Uid).Durability);
        Assert.Contains("source=NpcHandler.RelicCrystal", log.ToString());
        Assert.Contains("crystals=2", log.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static MsgItemUsagePacket CreateItemUse(uint itemUid)
    {
        return new MsgItemUsagePacket(
            itemUid,
            DwParam: 0,
            MsgItemUsageAction.Equip,
            Timestamp: 0,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0);
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "user02",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "HeroOne",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 10_250,
                X: 1_014,
                Y: 1_282,
                PreviousMapId: 1002,
                PreviousMapX: 577,
                PreviousMapY: 586,
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

    private static byte[] BuildLegacyNpcDialogReply(byte optionId, byte interactType, uint npcId)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 11);
        writer.WriteUInt32(0, npcId);
        writer.WriteUInt16(4, 0);
        writer.Payload[6] = optionId;
        writer.Payload[7] = interactType;
        writer.Payload[8] = 0;
        writer.WriteUInt16(9, 0);
        return writer.ToArray();
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes, LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes, uint expectedItemId)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedItemId, packet.ItemId);
        return packet;
    }

    private static void AssertRemoveInventory(byte[] bytes, uint itemUid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(itemUid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
