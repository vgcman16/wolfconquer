using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAccountSecurityNpcTests
{
    [Theory]
    [InlineData(22_155U, "NpcHandler.AddSTGMA", "Stigma and Magic Shield", "Please add it.", 1)]
    [InlineData(489_123U, "NpcHandler.SecurityAnima", "protect your Anima", "Create security code.", 1)]
    [InlineData(465_123U, "NpcHandler.FullAccount", "full-account service packs", "Full Rune [Red].", 12)]
    [InlineData(52_003U, "NpcHandler.PkReset", "reduce your PK points", "Clear 100 PK points [cost: 2,000,000 CPs].", 1)]
    [InlineData(7_935U, "NpcHandler.NameRegister", "get a new name", "Reset edit times.", 10)]
    public async Task RootMenusSendAsciiEnglishOptionsAndSourceMarkers(
        uint npcId,
        string source,
        string expectedDialogText,
        string expectedOptionText,
        byte expectedOptionId)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();

        var handled = await HandleAsync(sent, log, CreateSession(attributes: new Dictionary<string, string>
        {
            ["TREPIN2"] = "true",
        }), npcId, optionId: 0);

        Assert.True(handled);
        Assert.Contains(ParseReplies(sent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text.Contains(expectedDialogText, StringComparison.Ordinal));
        Assert.Contains(ParseReplies(sent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.Text == expectedOptionText &&
            reply.OptionId == expectedOptionId);
        Assert.Contains("source=" + source, log.ToString());
        AssertAsciiReplies(sent);
    }

    [Theory]
    [InlineData(1, 2, "Input:", 6)]
    [InlineData(10, 11, "Old PIN:", 6)]
    [InlineData(12, 13, "New PIN:", 6)]
    [InlineData(5, 6, "Yahoo email:", 16)]
    [InlineData(50, 60, "Yahoo email:", 16)]
    [InlineData(81, 80, "PIN:", 6)]
    public async Task SecurityAnimaInputPromptsPreserveLegacyInputOptionIds(
        byte promptOptionId,
        byte inputOptionId,
        string inputLabel,
        ushort maxLength)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();

        var handled = await HandleAsync(sent, log, CreateSession(), npcId: 489_123, optionId: promptOptionId);

        Assert.True(handled);
        var input = Assert.Single(
            ParseReplies(sent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Input);
        Assert.Equal(inputLabel, input.Text);
        Assert.Equal(inputOptionId, input.OptionId);
        Assert.Equal(maxLength, input.InputMaxLength);
        Assert.Contains("source=NpcHandler.SecurityAnima", log.ToString());
        AssertAsciiReplies(sent);
    }

    [Fact]
    public async Task SecurityAnimaPinEntryMasksConfirmationAndKeepsConfirmOption()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();

        var handled = await HandleAsync(sent, log, CreateSession(), npcId: 489_123, optionId: 2, input: "ABC123");

        Assert.True(handled);
        AssertReply(sent[0], LegacyNpcReplyInteractType.Dialog, "Is this your PIN code? ***123");
        AssertReply(sent[1], LegacyNpcReplyInteractType.Option, "Yes, use this PIN code.", optionId: 3);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
        AssertAsciiReplies(sent);
    }

    [Theory]
    [InlineData(7_935U, 1, "NpcHandler.NameRegister", "26523-26612", 810, 0)]
    public async Task MutationBranchesReturnUnavailableDialogsAndParityGapLogs(
        uint npcId,
        byte optionId,
        string source,
        string sourceLines,
        long conquerPoints,
        long pkPoints)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var session = CreateSession(conquerPoints: conquerPoints, pkPoints: pkPoints);

        var handled = await HandleAsync(sent, log, session, npcId, optionId);

        Assert.True(handled);
        AssertReply(
            sent[0],
            LegacyNpcReplyInteractType.Dialog,
            "This account-security action is unavailable here until its legacy storage and side effects are fully modeled. No account data was changed.");
        var output = log.ToString();
        Assert.Contains("PARITY-GAP LegacyNpc AccountSecurity", output);
        Assert.Contains("source=" + source, output);
        Assert.Contains("sourceLines=" + sourceLines, output);
        Assert.Contains("npcId=" + npcId, output);
        Assert.Contains("optionId=" + optionId, output);
        Assert.DoesNotContain(sent, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
        AssertAsciiReplies(sent);
    }

    [Theory]
    [InlineData(22_155U, 1, "Sorry, you do not have enough CPs.")]
    [InlineData(52_003U, 1, "Sorry, you do not have enough CPs.")]
    [InlineData(7_935U, 1, "Sorry, you do not have enough CPs to rename yourself. It costs 810 CPs.")]
    public async Task ModeledInsufficientBalancesSendDialogsWithoutParityGap(
        uint npcId,
        byte optionId,
        string expectedDialog)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();

        var handled = await HandleAsync(sent, log, CreateSession(conquerPoints: 0, pkPoints: 0), npcId, optionId);

        Assert.True(handled);
        AssertReply(sent[0], LegacyNpcReplyInteractType.Dialog, expectedDialog);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
        AssertAsciiReplies(sent);
    }

    [Fact]
    public async Task UnknownNpcAndUnknownOptionReturnFalse()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();

        Assert.False(await HandleAsync(sent, log, CreateSession(), npcId: 123, optionId: 0));
        Assert.False(await HandleAsync(sent, log, CreateSession(), npcId: 22_155, optionId: 99));
        Assert.Empty(sent);
    }

    private static async ValueTask<bool> HandleAsync(
        List<byte[]> sent,
        TextWriter log,
        WorldSession session,
        uint npcId,
        byte optionId,
        string input = "")
    {
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        return await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
                new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
                session,
                npcId,
                optionId,
                input,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static WorldSession CreateSession(
        long conquerPoints = 0,
        long pkPoints = 0,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new WorldSession(
            AccountId: 1_293_800,
            Username: "account-security-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_800,
                Name: "Security",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 15,
                FirstClass: 15,
                SecondClass: 0,
                Reborn: 2,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
                Strength: 11,
                Agility: 12,
                Vitality: 13,
                Spirit: 14,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: pkPoints,
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

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseReplies(IEnumerable<byte[]> sent)
    {
        var replies = new List<LegacyNpcReplyPacket>();
        foreach (var frame in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var packet))
            {
                replies.Add(packet);
            }
        }

        return replies;
    }

    private static LegacyNpcReplyPacket AssertReply(
        byte[] bytes,
        LegacyNpcReplyInteractType expectedType,
        string expectedText,
        byte? optionId = null)
    {
        var packet = AssertNpcReply(bytes, expectedType);
        Assert.Equal(expectedText, packet.Text);
        if (optionId.HasValue)
        {
            Assert.Equal(optionId.Value, packet.OptionId);
        }

        return packet;
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes, LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static void AssertAsciiReplies(IEnumerable<byte[]> sent)
    {
        foreach (var bytes in sent)
        {
            if (!LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                continue;
            }

            Assert.All(packet.Text, character => Assert.True(character <= 127));
        }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
