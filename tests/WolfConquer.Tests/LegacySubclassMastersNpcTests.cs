using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySubclassMastersNpcTests
{
    private const uint PerformerNpcId = 7_776;
    private const uint ApothecaryNpcId = 7_777;
    private const uint MartialArtistNpcId = 7_778;
    private const uint WarlockNpcId = 7_779;
    private const uint SageNpcId = 7_780;
    private const uint ChiMasterNpcId = 7_781;
    private const uint LeadWranglerNpcId = 8_616;

    [Theory]
    [InlineData(PerformerNpcId, "NpcHandler.Performer", "Performer sub-class", 20)]
    [InlineData(ApothecaryNpcId, "NpcHandler.ApothecarySubClass", "Apothecary sub-class", 3)]
    [InlineData(MartialArtistNpcId, "NpcHandler.MartialArtist", "Martial Artist", 3)]
    [InlineData(WarlockNpcId, "NpcHandler.Warlock", "Warlock", 3)]
    [InlineData(SageNpcId, "NpcHandler.Sage", "Sage sub-class", 3)]
    [InlineData(ChiMasterNpcId, "NpcHandler.ChiMaster", "Chi Master", 1)]
    [InlineData(LeadWranglerNpcId, "NpcHandler.LeadWrangler", "Wrangler", 1)]
    public async Task RootDialogsUseMappedNpcIdsSourceNamesAndEnglish(
        uint npcId,
        string source,
        string expectedText,
        byte expectedOptionId)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();

        var handled = await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.OptionId == expectedOptionId);
        Assert.Contains(replies, reply => reply.OptionId == byte.MaxValue);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains($"source={source}", log.ToString());
    }

    [Fact]
    public async Task PromotionRootUsesSubclassPhraseForSourceOption()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(subProfInfo: "0#0#1#1#2#1#");

        var handled = await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            MartialArtistNpcId,
            optionId: 7,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains("Phase 2", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.OptionId == 8);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task PromotionConfirmLogsSafeGapWithoutMutatingSubclassState()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(level: 90, subProfInfo: "0#0#1#9#2#1#");
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            LeadWranglerNpcId,
            optionId: 8,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Equal("0#0#1#9#2#1#", stored!.Character.Attributes["SubProfInfo"]);
        Assert.Contains("Promotion is not available yet", JoinReplyText(sent));
        var output = log.ToString();
        Assert.Contains("PARITY-GAP LegacyNpc SubclassMasters", output);
        Assert.Contains("source=NpcHandler.LeadWrangler", output);
        Assert.Contains("range=NpcHandler.cs:15862-16573", output);
        Assert.Contains("SetPhrase(Wrangler, 2)", output);
    }

    [Fact]
    public async Task StudyPointJoinAtExactly800LearnsWithoutSpendingStudy()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(level: 70, subProfInfo: "800#0#0#");
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WarlockNpcId,
            optionId: 5,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Equal("800#0#1#2#1#1#", stored!.Character.Attributes["SubProfInfo"]);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc subclass join", output);
        Assert.Contains("source=NpcHandler.Warlock", output);
        Assert.Contains("subclassId=2", output);
        Assert.Contains("removedStudyPoints=False", output);
    }

    [Fact]
    public async Task ItemJoinConsumesInventoryAndLearnsSubclass()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var celestialStones = new PlayerItemSnapshotItem
        {
            Uid = 900_001,
            ItemId = 721_259,
            StackSize = 5,
        };
        var session = CreateSession(
            level: 70,
            subProfInfo: "0#0#0#",
            playerItems: new PlayerItemSnapshot([celestialStones], [], [], [], []));
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            MartialArtistNpcId,
            optionId: 6,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Empty(stored!.PlayerItems.InventoryItems);
        Assert.Equal("0#0#1#1#1#1#", stored.Character.Attributes["SubProfInfo"]);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc subclass join", output);
        Assert.Contains("source=NpcHandler.MartialArtist", output);
        Assert.Contains("subclassId=1", output);
    }

    [Fact]
    public async Task PerformerDanceLearningLogsSafeGapWhenPhaseRequirementIsMet()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(subProfInfo: "0#0#1#6#3#3#");

        var handled = await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            PerformerNpcId,
            optionId: 7,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Contains("Dance learning is not available yet", JoinReplyText(sent));
        var output = log.ToString();
        Assert.Contains("PARITY-GAP LegacyNpc SubclassMasters", output);
        Assert.Contains("source=NpcHandler.Performer", output);
        Assert.Contains("range=NpcHandler.cs:11387-12416", output);
        Assert.Contains("Triumph", output);
    }

    [Fact]
    public async Task UnknownNpcOrOptionIsNotHandled()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();

        Assert.False(await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId: 1,
            optionId: 0,
            CancellationToken.None));
        Assert.False(await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            WarlockNpcId,
            optionId: 99,
            CancellationToken.None));
    }

    private static WorldSession CreateSession(
        long level = 140,
        long reborn = 0,
        string? subProfInfo = null,
        PlayerItemSnapshot? playerItems = null)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (subProfInfo is not null)
        {
            attributes["SubProfInfo"] = subProfInfo;
        }

        return new WorldSession(
            AccountId: 100,
            Username: "subclass-master-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "SubclassHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
                SecondClass: 0,
                Reborn: reborn,
                Level: level,
                Experience: 0,
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
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
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

    private static string JoinReplyText(IEnumerable<byte[]> sent)
    {
        return string.Join(' ', ParseNpcReplies(sent).Select(reply => reply.Text));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }
}
