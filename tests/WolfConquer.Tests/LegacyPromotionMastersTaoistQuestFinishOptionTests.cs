using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersTaoistQuestFinishOptionTests
{
    private const uint TaoistPromotionNpcId = 10_000;
    private const byte QuestFinishOption = 111;
    private const long CharacterUid = 1_293_721;

    [Theory]
    [InlineData(10_000, 1, "I~want~to~get~promoted")]
    [InlineData(10_001, 3, "I~want~to~get~promoted.")]
    [InlineData(13_002, 53, "I~want~to~get~promoted.")]
    [InlineData(14_002, 53, "I~want~to~get~promoted.")]
    public async Task Option111WithoutLocalFinishQuestSendsFollowupDialogWithoutMutation(
        long classCode,
        byte expectedNextOption,
        string expectedOptionText)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(classCode);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            TaoistPromotionNpcId,
            QuestFinishOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(classCode, updated.Character.Class);
        Assert.Equal(456, updated.Character.ConquerPoints);
        Assert.Equal(789, updated.Character.Experience);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.Empty(updated.PlayerSpells.Spells);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "Wow, look at you, you`ll be another hero in the world of Conquer! Here, you can learn some skills and get promoted! Do you want to learn more?");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "~get promoted here to start your life. Would you like to learn something about your class promotion and quests?.");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
                reply.OptionId == expectedNextOption &&
                reply.Text == expectedOptionText);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == 259);
        Assert.DoesNotContain(sent, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(sent, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));

        var logText = log.ToString();
        Assert.Contains("PARITY-GAP LegacyNpc PromotionTaoist quest-finish reward skipped", logText);
        Assert.Contains("source=NpcHandler.PromotionTaoist", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:41392-41442", logText);
        Assert.Contains("finishLookup=NpcHandler.cs:41215", logText);
        Assert.Contains("option=111", logText);
        Assert.Contains("GetFinishQuest(2001, class, 1004)", logText);
        Assert.Contains("item=3005160", logText);
        Assert.Contains("GainExpBall(300, angelwing)", logText);
        Assert.Contains("source FinishQuest returned false because quest is not active", logText);
        Assert.DoesNotContain("PromotionMaster mutation unavailable", logText);
    }

    private static WorldSession CreateSession(long classCode)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "taoist-quest-finish-option-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "TaoistQuestHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: 0,
                Level: 70,
                Experience: 789,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: 456,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
