using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRewardServicesNpcTests
{
    public static TheoryData<uint, string, byte[]> RootDialogCases => new()
    {
        { 456_456, "buy a random relic", [1, byte.MaxValue] },
        { 54_613, "gold prize cup", [1, byte.MaxValue] },
        { 111_235, "100,000 CPs", [1, byte.MaxValue] },
        { 25_415, "Hossu's perfection", [1, byte.MaxValue] },
        { 301_856, "reward points", [1, 2, 3, byte.MaxValue] },
    };

    [Theory]
    [MemberData(nameof(RootDialogCases))]
    public async Task HandlerSendsRootDialogsWithSourceOptionIds(uint npcId, string expectedText, byte[] expectedOptions)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            conquerPoints: 700_000,
            attributes: new Dictionary<string, string>
            {
                ["RewardPoints"] = "12",
            });

        var handled = await WorldPacketHandlers.TryHandleLegacyRewardServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: new WorldSessionStore()),
            session,
            npcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog), reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        Assert.Equal(expectedOptions, replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option).Select(reply => reply.OptionId).ToArray());
        Assert.All(replies, AssertAsciiReply);
        Assert.Contains("source=NpcHandler.", log.ToString());
    }

    [Fact]
    public async Task JiangMakerRootTranslatesNoJiangHuDialogAndActiveStageOptions()
    {
        using var noStateLog = new StringWriter();
        var noStateSent = new List<byte[]>();
        var noStateConnection = CreateSendingConnection(noStateSent);
        var noStateSession = CreateSession();

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRewardServicesNpcAsync(
            new WorldPacketHandlerContext(noStateConnection, noStateLog, Session: noStateSession, Sessions: new WorldSessionStore()),
            noStateSession,
            npcId: 15_532,
            optionId: 0,
            CancellationToken.None));

        var noStateReplies = ParseNpcReplies(noStateSent);
        Assert.Contains("not created a Jiang Hu skill", noStateReplies[0].Text);
        Assert.All(noStateReplies, AssertAsciiReply);

        using var activeLog = new StringWriter();
        var activeSent = new List<byte[]>();
        var activeConnection = CreateSendingConnection(activeSent);
        var activeSession = CreateSession(attributes: new Dictionary<string, string> { ["JiangHuActiveStages"] = "1,3" });

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRewardServicesNpcAsync(
            new WorldPacketHandlerContext(activeConnection, activeLog, Session: activeSession, Sessions: new WorldSessionStore()),
            activeSession,
            npcId: 15_532,
            optionId: 0,
            CancellationToken.None));

        var activeReplies = ParseNpcReplies(activeSent);
        Assert.Equal([1, 3, byte.MaxValue], activeReplies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option).Select(reply => reply.OptionId).ToArray());
        Assert.Contains("source=NpcHandler.JiangMaker", activeLog.ToString());
        Assert.All(activeReplies, AssertAsciiReply);
    }

    [Fact]
    public async Task RewardPointsInsufficientResourceBranchIsSafeAndAscii()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(attributes: new Dictionary<string, string> { ["RewardPoints"] = "0" });

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRewardServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: new WorldSessionStore()),
            session,
            npcId: 301_856,
            optionId: 2,
            CancellationToken.None));

        var replies = ParseNpcReplies(sent);
        Assert.Contains("You need 10 reward point(s).", replies[0].Text);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
        Assert.All(replies, AssertAsciiReply);
    }

    [Theory]
    [InlineData(456_456u, 1, "NpcHandler.ShopRilec", "5450-5498")]
    [InlineData(15_532u, 11, "NpcHandler.JiangMaker", "7907-8008")]
    public async Task MutatingBranchesSendUnavailableDialogAndSourceEvidenceGap(
        uint npcId,
        byte optionId,
        string source,
        string sourceLines)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            conquerPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["RewardPoints"] = "12",
                ["JiangHuActiveStages"] = "1",
            });

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRewardServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: new WorldSessionStore()),
            session,
            npcId,
            optionId,
            CancellationToken.None));

        var replies = ParseNpcReplies(sent);
        Assert.Contains("unavailable here", replies[0].Text);
        Assert.All(replies, AssertAsciiReply);

        var output = log.ToString();
        Assert.Contains("PARITY-GAP", output);
        Assert.Contains("gap=LegacyNpc-RewardServices", output);
        Assert.Contains("source=" + source, output);
        Assert.Contains("sourceLines=" + sourceLines, output);
        Assert.Contains("optionId=" + optionId, output);
    }

    [Fact]
    public async Task JiangMakerStageOptionsUseSourceOptionEncoding()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(attributes: new Dictionary<string, string> { ["JiangHuActiveStages"] = "1" });

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRewardServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: new WorldSessionStore()),
            session,
            npcId: 15_532,
            optionId: 1,
            CancellationToken.None));

        var replies = ParseNpcReplies(sent);
        Assert.Equal([11, 21, 31], replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option).Select(reply => reply.OptionId).ToArray());
        Assert.All(replies, AssertAsciiReply);
    }

    [Fact]
    public async Task UnknownNpcIsNotHandled()
    {
        using var log = new StringWriter();
        var session = CreateSession();

        Assert.False(await WorldPacketHandlers.TryHandleLegacyRewardServicesNpcAsync(
            new WorldPacketHandlerContext(CreateSendingConnection([]), log, Session: session, Sessions: new WorldSessionStore()),
            session,
            npcId: 123,
            optionId: 0,
            CancellationToken.None));
    }

    private static WorldSession CreateSession(
        long conquerPoints = 0,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "legacy-reward-services-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "RewardTester",
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
                Attributes: attributes ?? new Dictionary<string, string>(),
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

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IReadOnlyList<byte[]> sent)
    {
        return sent.Select(bytes =>
            {
                Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
                return packet;
            })
            .ToArray();
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
