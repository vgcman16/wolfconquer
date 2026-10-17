using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyKingDoomCrystalsNpcTests
{
    private const uint KingDoomCrystalNpcId = 19_121;
    private const uint QuestId = 35_028;

    [Theory]
    [InlineData(19_121)]
    [InlineData(19_125)]
    public async Task HandlerSendsClaimedTalkWhenQuestIsNotAccepted(uint npcId)
    {
        var result = await ClickKingDoomCrystalAsync(npcId, CreateSession(quest: null));

        var message = Assert.Single(ParseMessages(result.Sent));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, message.ChatType);
        Assert.Contains("already claimed a bounty today", message.Content[3]);
        Assert.Contains("source=NpcHandler.KingDoomCrystals", result.Log);
        Assert.DoesNotContain("unmodeled LegacyNpcReply", result.Log);
    }

    [Fact]
    public async Task HandlerPromptsForKillsWhenQuestObjectiveIsIncomplete()
    {
        var quest = new PlayerQuestSnapshotEntry(QuestId, PlayerQuestStatus.Accepted, Time: 0, [49U]);
        var result = await ClickKingDoomCrystalAsync(KingDoomCrystalNpcId, CreateSession(quest));

        var reply = Assert.Single(ParseNpcReplies(result.Sent));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal("To claim a bounty, you should kill 50 enemies in the Realm.", reply.Text);
        Assert.True(result.UpdatedSession.Character.PlayerQuests.TryGetQuest(QuestId, out var updatedQuest));
        Assert.Equal(PlayerQuestStatus.Accepted, updatedQuest.Status);
    }

    [Fact]
    public async Task HandlerChecksInventorySpaceBeforeKingdomObjective()
    {
        var quest = new PlayerQuestSnapshotEntry(QuestId, PlayerQuestStatus.Accepted, Time: 0, [49U]);
        var fullInventory = Enumerable.Range(1, 40)
            .Select(index => Item((uint)index))
            .ToArray();
        var result = await ClickKingDoomCrystalAsync(
            KingDoomCrystalNpcId,
            CreateSession(quest, fullInventory));

        var reply = Assert.Single(ParseNpcReplies(result.Sent));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal("Please make 1 more space in your inventory.", reply.Text);
        Assert.Contains("inventory is full", result.Log);
    }

    [Fact]
    public async Task HandlerFinishesReadyQuestWithoutInventoryRewardSideEffects()
    {
        var quest = new PlayerQuestSnapshotEntry(QuestId, PlayerQuestStatus.Accepted, Time: 123, [50U]);
        var inventoryItem = Item(7_001);
        var session = CreateSession(quest, [inventoryItem]);
        var result = await ClickKingDoomCrystalAsync(KingDoomCrystalNpcId, session);

        Assert.Equal(2, result.Sent.Count);
        Assert.True(LegacyQuestListPacket.TryParseReviewFrame(ReadFrame(result.Sent[0]), out var questFrame));
        Assert.Equal(LegacyQuestListMode.FinishQuest, questFrame.Mode);
        var item = Assert.Single(questFrame.Items);
        Assert.Equal(QuestId, item.QuestId);
        Assert.Equal(LegacyQuestStatus.Finished, item.Status);

        var reply = AssertNpcReply(result.Sent[1], LegacyNpcReplyInteractType.MessageBox);
        Assert.Equal(
            "You've restored peace to the Realm by killing 50 enemy soldiers, and received a Realm Crystal Fragment.",
            reply.Text);

        Assert.True(result.UpdatedSession.Character.PlayerQuests.TryGetQuest(QuestId, out var updatedQuest));
        Assert.Equal(PlayerQuestStatus.Finished, updatedQuest.Status);
        Assert.Same(session.PlayerItems, result.UpdatedSession.PlayerItems);
        Assert.Same(inventoryItem, Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems));
        Assert.Empty(ParseGameItems(result.Sent));
        Assert.DoesNotContain("PARITY-GAP LegacyNpc KingDoomCrystals QuestGUI reward", result.Log);
        Assert.Contains("source-complete QuestGUI.FinishQuest has no inventory reward side effects", result.Log);
        Assert.Contains("WorldServer handled LegacyNpc KingDoomCrystals claim", result.Log);
    }

    [Fact]
    public async Task HandlerPersistsFinishedQuestWhenPlayerQuestsRootIsAvailable()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "wolf-kingdoomcrystals-player-quests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var quest = new PlayerQuestSnapshotEntry(QuestId, PlayerQuestStatus.Accepted, Time: 123, [50U]);
            var session = CreateSession(quest);
            var loader = new PlayerQuestSnapshotLoader(root);
            loader.SaveByCharacterUid(session.Character.Uid, session.Character.PlayerQuests);

            var result = await ClickKingDoomCrystalAsync(
                KingDoomCrystalNpcId,
                session,
                playerQuestsRoot: root);

            var reloaded = loader.LoadByCharacterUid(session.Character.Uid);
            Assert.True(reloaded.TryGetQuest(QuestId, out var persistedQuest));
            Assert.Equal(PlayerQuestStatus.Finished, persistedQuest.Status);
            Assert.Equal(quest.Time, persistedQuest.Time);
            Assert.Equal(quest.Intentions.ToArray(), persistedQuest.Intentions.ToArray());
            Assert.Contains("WorldServer persisted LegacyPlayerQuests", result.Log);
            Assert.Contains("source=NpcHandler.KingDoomCrystals", result.Log);
            Assert.DoesNotContain("quest update is session-only", result.Log);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerIgnoresNonzeroOptionsLikeSource()
    {
        var quest = new PlayerQuestSnapshotEntry(QuestId, PlayerQuestStatus.Accepted, Time: 0, [50U]);
        var result = await ClickKingDoomCrystalAsync(KingDoomCrystalNpcId, CreateSession(quest), optionId: 1);

        Assert.Empty(result.Sent);
        Assert.Contains("source returns for nonzero options", result.Log);
        Assert.True(result.UpdatedSession.Character.PlayerQuests.TryGetQuest(QuestId, out var updatedQuest));
        Assert.Equal(PlayerQuestStatus.Accepted, updatedQuest.Status);
    }

    private static async Task<ClickResult> ClickKingDoomCrystalAsync(
        uint npcId,
        WorldSession session,
        byte optionId = 0,
        string? playerQuestsRoot = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerQuestsRoot: playerQuestsRoot),
            ReadFrame(LegacyNpcServerRequestPacket.Script(npcId, optionId, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new ClickResult(sent, log.ToString(), updatedSession);
    }

    private static WorldSession CreateSession(
        PlayerQuestSnapshotEntry? quest,
        IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        var playerQuests = quest is null
            ? PlayerQuestSnapshot.Empty
            : new PlayerQuestSnapshot([quest]);
        return new WorldSession(
            AccountId: 100,
            Username: "kingdoom-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "RealmSeeker",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 3_935,
                X: 252,
                Y: 233,
                PreviousMapId: 3_935,
                PreviousMapX: 252,
                PreviousMapY: 233,
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
                PaidNobility: new Dictionary<string, string>())
            {
                PlayerQuests = playerQuests,
            },
            PlayerItems: new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshotItem Item(uint uid)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = 100_000 + uid,
            Position = 0,
            StackSize = 1,
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

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes, LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
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

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseMessages(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record ClickResult(
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession);
}
