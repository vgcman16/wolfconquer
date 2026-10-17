using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyLabyrinthNpcTests
{
    [Fact]
    public async Task LabyrinthFloorOneRootDialogUsesSourceOptionIdsAndEnglishText()
    {
        var result = await InvokeLabyrinthHandlerAsync(npcId: 1_153, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("EastGeneral", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.Text.Contains("Sky Token", StringComparison.Ordinal));
        Assert.Equal(
            [1, 3, byte.MaxValue],
            replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option)
                .Select(reply => reply.OptionId)
                .ToArray());
        Assert.Contains("source=NpcHandler.Lab1", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
        Assert.All(replies, AssertAsciiReply);
    }

    [Fact]
    public async Task SimonRootDialogPreservesLabyrinthRewardOptionIds()
    {
        var result = await InvokeLabyrinthHandlerAsync(npcId: 1_152, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("Great rewards", StringComparison.Ordinal));
        Assert.Equal(
            [1, 10, 12, 13, 14, 15, byte.MaxValue],
            replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option)
                .Select(reply => reply.OptionId)
                .ToArray());
        Assert.Contains("source=NpcHandler.Simon", result.Log);
        Assert.All(replies, AssertAsciiReply);
    }

    [Fact]
    public async Task FrozenGrottoDesertRootDialogTranslatesEnglishTravelChoices()
    {
        var result = await InvokeLabyrinthHandlerAsync(npcId: 21_116, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("Frozen Grotto is a dangerous", StringComparison.Ordinal));
        Assert.Equal(
            [1, 2, byte.MaxValue],
            replies.Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option)
                .Select(reply => reply.OptionId)
                .ToArray());
        Assert.Contains("source=NpcHandler.DesertFrozenGroto", result.Log);
        Assert.All(replies, AssertAsciiReply);
    }

    public static TheoryData<uint, byte, IReadOnlyDictionary<string, string>, uint, uint, uint, string> TeleportCases => new()
    {
        { 3_623, 3, EmptyAttributes, 1_002, 410, 354, "NpcHandler.Stanley" },
        { 1_156, 3, EmptyAttributes, 1_002, 410, 354, "NpcHandler.Lab4" },
        { 1_152, 8, VipOneAttributes, 1_351, 18, 127, "NpcHandler.Simon" },
        { 13_125, 3, EmptyAttributes, 2_054, 379, 470, "NpcHandler.FrozenGrottoGuardian5" },
        { 13_124, 2, VipFourAttributes, 2_055, 526, 562, "NpcHandler.FrozenFroto4Teleporter" },
        { 21_116, 1, EmptyAttributes, 1_762, 338, 354, "NpcHandler.DesertFrozenGroto" },
        { 21_116, 2, EmptyAttributes, 1_927, 392, 625, "NpcHandler.DesertFrozenGroto" },
    };

    [Theory]
    [MemberData(nameof(TeleportCases))]
    public async Task TravelOptionsTeleportToExactLegacyDestinations(
        uint npcId,
        byte optionId,
        IReadOnlyDictionary<string, string> attributes,
        uint expectedMapId,
        uint expectedX,
        uint expectedY,
        string expectedSource)
    {
        var result = await InvokeLabyrinthHandlerAsync(npcId, optionId, attributes: attributes);

        Assert.True(result.Handled);
        var teleport = Assert.Single(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(expectedMapId, teleport.DwParam);
        Assert.Equal(expectedX, teleport.PositionX);
        Assert.Equal(expectedY, teleport.PositionY);
        Assert.Equal((long)expectedMapId, result.UpdatedSession.Character.MapId);
        Assert.Equal((long)expectedX, result.UpdatedSession.Character.X);
        Assert.Equal((long)expectedY, result.UpdatedSession.Character.Y);
        var expectedPrevious = result.Session.Character.MapId == expectedMapId
            ? (
                PreviousMapId: result.Session.Character.PreviousMapId,
                PreviousMapX: result.Session.Character.PreviousMapX,
                PreviousMapY: result.Session.Character.PreviousMapY)
            : (
                PreviousMapId: result.Session.Character.MapId,
                PreviousMapX: result.Session.Character.X,
                PreviousMapY: result.Session.Character.Y);
        Assert.Equal(expectedPrevious.PreviousMapId, result.UpdatedSession.Character.PreviousMapId);
        Assert.Equal(expectedPrevious.PreviousMapX, result.UpdatedSession.Character.PreviousMapX);
        Assert.Equal(expectedPrevious.PreviousMapY, result.UpdatedSession.Character.PreviousMapY);
        Assert.Contains($"source={expectedSource}", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc Labyrinth travel mutation", result.Log);
    }

    [Fact]
    public async Task LabyrinthTokenPreviewChecksInventoryWithoutMutating()
    {
        var missing = await InvokeLabyrinthHandlerAsync(npcId: 1_153, optionId: 1);

        Assert.True(missing.Handled);
        var missingReplies = ParseNpcReplies(missing.Sent);
        Assert.Contains(missingReplies, reply => reply.Text.Contains("You have no Sky Token", StringComparison.Ordinal));
        Assert.DoesNotContain("PARITY-GAP", missing.Log);

        var withToken = await InvokeLabyrinthHandlerAsync(
            npcId: 1_153,
            optionId: 1,
            items: CreateItems((721_537U, 1)));

        Assert.True(withToken.Handled);
        var replies = ParseNpcReplies(withToken.Sent);
        Assert.Contains(replies, reply => reply.Text == "The next floor is more dangerous. You should be more careful.");
        Assert.Contains(replies, reply => reply.Text == "Thank~you~very~much." && reply.OptionId == 2);
        Assert.Equal(withToken.Session.PlayerItems.InventoryItems.Count, withToken.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain("PARITY-GAP", withToken.Log);
    }

    public static TheoryData<uint, byte, PlayerItemSnapshot, string> MutationPersistenceUnavailableCases => new()
    {
        { 1_153, 2, CreateItems((721_537U, 1)), "NpcHandler.cs:34069-34120" },
        { 1_152, 7, CreateItems((1_088_001U, 5)), "NpcHandler.cs:34335-34345" },
        { 13_124, 1, CreateItems((721_347U, 1)), "NpcHandler.cs:34413-34424" },
    };

    [Theory]
    [MemberData(nameof(MutationPersistenceUnavailableCases))]
    public async Task ItemAndCostMutationBranchesFailClosedWithoutPersistenceRoots(
        uint npcId,
        byte optionId,
        PlayerItemSnapshot items,
        string expectedSourceLines)
    {
        var result = await InvokeLabyrinthHandlerAsync(npcId, optionId, items: items);

        Assert.True(result.Handled);
        Assert.DoesNotContain(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Empty(ParseNpcReplies(result.Sent));
        Assert.DoesNotContain("PARITY-GAP LegacyNpc Labyrinth travel mutation", result.Log);
        Assert.Contains("option=" + optionId, result.Log);
        Assert.Contains("sourceLines=" + expectedSourceLines, result.Log);
        Assert.Contains("reason=", result.Log);
        Assert.Equal(result.Session.Character.MapId, result.UpdatedSession.Character.MapId);
        Assert.Equal(result.Session.PlayerItems.InventoryItems.Count, result.UpdatedSession.PlayerItems.InventoryItems.Count);
    }

    [Fact]
    public async Task FrozenGrottoVipGateDoesNotTeleportBelowVipFour()
    {
        var result = await InvokeLabyrinthHandlerAsync(
            npcId: 13_124,
            optionId: 2,
            attributes: new Dictionary<string, string> { ["VipLevel"] = "3" });

        Assert.True(result.Handled);
        Assert.DoesNotContain(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        var reply = Assert.Single(ParseNpcReplies(result.Sent), packet => packet.InteractType == LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("VIP Level has to reach 4", reply.Text);
        Assert.Equal(result.Session.Character.MapId, result.UpdatedSession.Character.MapId);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task UnknownNpcReturnsFalse()
    {
        var result = await InvokeLabyrinthHandlerAsync(npcId: 999_001, optionId: 0);

        Assert.False(result.Handled);
        Assert.Empty(result.Sent);
        Assert.Same(result.Session, result.UpdatedSession);
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyAttributes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> VipOneAttributes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["VipLevel"] = "1" };

    private static readonly IReadOnlyDictionary<string, string> VipFourAttributes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["VipLevel"] = "4" };

    private static async Task<LabyrinthInvokeResult> InvokeLabyrinthHandlerAsync(
        uint npcId,
        byte optionId,
        IReadOnlyDictionary<string, string>? attributes = null,
        PlayerItemSnapshot? items = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(attributes, items);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);

        var handled = await WorldPacketHandlers.TryHandleLegacyLabyrinthNpcAsync(
                context,
                session,
                npcId,
                optionId,
                CancellationToken.None)
            .ConfigureAwait(false);

        if (!sessions.TryGet(connection, out var updatedSession) || updatedSession is null)
        {
            updatedSession = session;
        }

        return new LabyrinthInvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private static WorldSession CreateSession(
        IReadOnlyDictionary<string, string>? attributes,
        PlayerItemSnapshot? items)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "legacy-labyrinth-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "LabRunner",
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
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 410,
                PreviousMapY: 354,
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
                Attributes: attributes ?? EmptyAttributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: items ?? PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshot CreateItems(params (uint ItemId, ushort StackSize)[] items)
    {
        return new PlayerItemSnapshot(
            items.Select((item, index) => new PlayerItemSnapshotItem
            {
                Uid = (uint)(1_000 + index),
                ItemId = item.ItemId,
                Position = 0,
                StackSize = item.StackSize,
            })
                .ToArray(),
            [],
            [],
            [],
            []);
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

    private static IReadOnlyList<MsgActionPacket> ParseMsgActions(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static void AssertAsciiReply(LegacyNpcReplyPacket packet)
    {
        if (packet.Text.Length == 0)
        {
            return;
        }

        Assert.All(packet.Text, character => Assert.True(character <= 127, packet.Text));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record LabyrinthInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession Session,
        WorldSession UpdatedSession);
}
