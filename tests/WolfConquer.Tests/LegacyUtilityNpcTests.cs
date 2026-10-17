using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyUtilityNpcTests
{
    [Fact]
    public async Task ClearInventoryRootSendsSafeEnglishConfirmation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handled = await HandleAsync(sent, log, npcId: 1_245, optionId: 0);

        Assert.True(handled);
        Assert.Equal(5, sent.Count);
        AssertReply(sent[0], LegacyNpcReplyInteractType.Dialog, "I can help clean unwanted items from your inventory.");
        AssertReply(sent[1], LegacyNpcReplyInteractType.Option, "Clear my inventory.", optionId: 5);
        AssertReply(sent[2], LegacyNpcReplyInteractType.Option, "No thanks.", optionId: byte.MaxValue);
        Assert.Equal(0, AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Avatar).InputMaxLength);
        AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Finish);
        AssertAsciiReplies(sent);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task ClearInventoryConfirmDoesNotClearAndLogsSafeGap()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var session = CreateSession(attributePoints: 3);
        var handled = await HandleAsync(sent, log, npcId: 1_245, optionId: 5, session: session);

        Assert.True(handled);
        AssertReply(
            sent[0],
            LegacyNpcReplyInteractType.Dialog,
            "Inventory clearing is unavailable here because the legacy action deletes items.");
        AssertReply(sent[1], LegacyNpcReplyInteractType.Option, "I understand.", optionId: byte.MaxValue);
        Assert.Equal(0, AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar).InputMaxLength);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);
        Assert.Contains("PARITY-GAP LegacyNpc clearinventory", log.ToString());
        Assert.Contains("source=NpcHandler.cs:5229-5248", log.ToString());
        AssertAsciiReplies(sent);
    }

    [Fact]
    public async Task ReallotPointsRootSendsSourceOptionMenu()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handled = await HandleAsync(sent, log, npcId: 2_542, optionId: 0);

        Assert.True(handled);
        Assert.Equal(9, sent.Count);
        AssertReply(sent[0], LegacyNpcReplyInteractType.Dialog, "Hello, friend. I can help you assign your attribute points.");
        AssertReply(sent[1], LegacyNpcReplyInteractType.Option, "Vitality points.", optionId: 5);
        AssertReply(sent[2], LegacyNpcReplyInteractType.Option, "Agility points.", optionId: 6);
        AssertReply(sent[3], LegacyNpcReplyInteractType.Option, "Strength points.", optionId: 7);
        AssertReply(sent[4], LegacyNpcReplyInteractType.Option, "Spirit points.", optionId: 8);
        AssertReply(sent[5], LegacyNpcReplyInteractType.Option, "Reset all my points.", optionId: 9);
        AssertReply(sent[6], LegacyNpcReplyInteractType.Option, "Just passing by.", optionId: byte.MaxValue);
        Assert.Equal(6, AssertNpcReply(sent[7], LegacyNpcReplyInteractType.Avatar).InputMaxLength);
        AssertNpcReply(sent[8], LegacyNpcReplyInteractType.Finish);
        AssertAsciiReplies(sent);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Theory]
    [InlineData(5, 1, "Enter vitality points.", "Vitality points.")]
    [InlineData(6, 2, "Enter agility points.", "Agility points.")]
    [InlineData(7, 3, "Enter strength points.", "Strength points.")]
    [InlineData(8, 4, "Enter spirit points.", "Spirit points.")]
    public async Task ReallotPointsPromptOptionsUseLegacyInputOptionIds(
        byte promptOptionId,
        byte inputOptionId,
        string text,
        string inputLabel)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handled = await HandleAsync(sent, log, npcId: 2_542, optionId: promptOptionId);

        Assert.True(handled);
        Assert.Equal(5, sent.Count);
        AssertReply(sent[0], LegacyNpcReplyInteractType.Dialog, text);
        var input = AssertReply(sent[1], LegacyNpcReplyInteractType.Input, inputLabel, optionId: inputOptionId);
        Assert.Equal(16, input.InputMaxLength);
        AssertReply(sent[2], LegacyNpcReplyInteractType.Option, "Just passing by.", optionId: byte.MaxValue);
        Assert.Equal(6, AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Avatar).InputMaxLength);
        AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Finish);
        AssertAsciiReplies(sent);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task ReallotPointsResetIsSafeGapAndLeavesSessionUnchanged()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var session = CreateSession(attributePoints: 77);
        var handled = await HandleAsync(sent, log, npcId: 2_542, optionId: 9, session: session);

        Assert.True(handled);
        AssertReply(
            sent[0],
            LegacyNpcReplyInteractType.Dialog,
            "Attribute reset is unavailable here because it must consume one Dragon Ball and rewrite your stats safely.");
        Assert.Contains("PARITY-GAP LegacyNpc ReallotPoints", log.ToString());
        Assert.Contains("source=NpcHandler.cs:7494-7661", log.ToString());
        AssertSessionUnchanged(sent, session);
        AssertAsciiReplies(sent);
    }

    [Fact]
    public async Task ReallotPointsInsufficientAllocationSendsSourceMessageBoxWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var session = CreateSession(attributePoints: 7);
        var handled = await HandleAsync(sent, log, npcId: 2_542, optionId: 1, input: "8", session: session);

        Assert.True(handled);
        var message = Assert.Single(sent);
        AssertReply(message, LegacyNpcReplyInteractType.MessageBox, "You do not have: 8 attribute points.");
        Assert.Contains("WorldServer skipped LegacyNpc ReallotPoints allocation", log.ToString());
        Assert.Contains("option=1", log.ToString());
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
        AssertSessionUnchanged(sent, session);
        AssertAsciiReplies(sent);
    }

    private static async ValueTask<bool> HandleAsync(
        List<byte[]> sent,
        TextWriter log,
        uint npcId,
        byte optionId,
        string input = "",
        WorldSession? session = null)
    {
        var connection = CreateSendingConnection(sent);
        session ??= CreateSession(attributePoints: 10);
        return await WorldPacketHandlers.TryHandleLegacyUtilityNpcAsync(
                new WorldPacketHandlerContext(connection, log, Session: session),
                session,
                npcId,
                optionId,
                input,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static WorldSession CreateSession(long attributePoints)
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "utility-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "HeroOne",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 15,
                FirstClass: 15,
                SecondClass: 0,
                Reborn: 0,
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
                AttributePoints: attributePoints,
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

    private static void AssertSessionUnchanged(IReadOnlyCollection<byte[]> sent, WorldSession session)
    {
        Assert.DoesNotContain(sent, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
        Assert.Equal(11, session.Character.Strength);
        Assert.Equal(12, session.Character.Agility);
        Assert.Equal(13, session.Character.Vitality);
        Assert.Equal(14, session.Character.Spirit);
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
            var packet = AssertNpcReply(bytes, LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var parsed)
                ? parsed.InteractType
                : throw new InvalidOperationException("Expected an NPC reply packet."));
            Assert.All(packet.Text, character => Assert.True(character <= 127));
        }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
