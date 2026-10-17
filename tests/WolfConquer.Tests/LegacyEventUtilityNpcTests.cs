using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyEventUtilityNpcTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyEventUtilityNpcTests-" + Guid.NewGuid().ToString("N"));

    private const uint DailyItem1NpcId = uint.MaxValue - 7;
    private const uint DancePkNpcId = 6_017;
    private const uint VicotryBuddhaNpcId = 25_003;
    private const uint MarialDealerNpcId = 15_987;
    private const uint SwapperStarryNpcId = 832_663;
    private const uint ElementalPoolNpcId = 832_664;

    [Fact]
    public async Task DailyItem1TeleportsSourceDestinations()
    {
        var bird = await InvokeAsync(DailyItem1NpcId, optionId: 1);

        Assert.True(bird.Handled);
        Assert.Equal(1_015, bird.UpdatedSession.Character.MapId);
        Assert.Equal(746, bird.UpdatedSession.Character.X);
        Assert.Equal(665, bird.UpdatedSession.Character.Y);

        var twin = await InvokeAsync(DailyItem1NpcId, optionId: 2);

        Assert.True(twin.Handled);
        Assert.Equal(1_002, twin.UpdatedSession.Character.MapId);
        Assert.Equal(270, twin.UpdatedSession.Character.X);
        Assert.Equal(470, twin.UpdatedSession.Character.Y);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc EventUtilityNpcs", bird.Log + twin.Log);
    }

    [Fact]
    public async Task DancePkChargesOneCpUpdatesScalarPersistsWalletAndTeleports()
    {
        var usersRoot = Path.Combine(_root, "Users");
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, "100.ini"),
            """
            [Character]
            UID=1293600
            Name=UtilityHero
            ConquerPoints=5
            """);

        var result = await InvokeAsync(DancePkNpcId, optionId: 1, conquerPoints: 5, usersRoot: usersRoot);

        Assert.True(result.Handled);
        Assert.Equal(4, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(7_357, result.UpdatedSession.Character.MapId);
        Assert.Equal(50, result.UpdatedSession.Character.X);
        Assert.Equal(50, result.UpdatedSession.Character.Y);
        Assert.Contains(ParseMsgUpdates(result.Sent), packet =>
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ConquerPoints && entry.Value == 4));
        Assert.Contains(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Contains("ConquerPoints=4", File.ReadAllText(Path.Combine(usersRoot, "100.ini")));
        Assert.Contains("WorldServer persisted LegacyNpc DancePK character", result.Log);
        Assert.Contains("sourceConquerPointCost=1", result.Log);
        Assert.Contains("tunedConquerPointCost=1", result.Log);
        Assert.Contains("updatedConquerPoints=4", result.Log);
        Assert.Contains("walletUpdate=LegacyMsgUpdate.ConquerPoints", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc DancePK character persistence", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc EventUtilityNpcs", result.Log);
    }

    [Fact]
    public async Task DancePkRejectsMissingCpWithoutMutation()
    {
        var result = await InvokeAsync(DancePkNpcId, optionId: 1, conquerPoints: 0);

        Assert.True(result.Handled);
        Assert.Equal(0, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(1_002, result.UpdatedSession.Character.MapId);
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text == "You do not have 1 CP with you.");
        Assert.DoesNotContain(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
    }

    [Fact]
    public async Task BuddhaSwapperAndElementalRootDialogsAreTranslatedAscii()
    {
        var buddha = await InvokeAsync(VicotryBuddhaNpcId, optionId: 0);
        var swapper = await InvokeAsync(SwapperStarryNpcId, optionId: 0);
        var pool = await InvokeAsync(ElementalPoolNpcId, optionId: 0);

        Assert.True(buddha.Handled);
        Assert.True(swapper.Handled);
        Assert.True(pool.Handled);
        Assert.Contains(ParseNpcReplies(buddha.Sent), reply => reply.Text.Contains("Buddha's Necklace", StringComparison.Ordinal));
        Assert.Contains(ParseNpcReplies(swapper.Sent), reply => reply.Text == "Teleport to Evil Abyss." && reply.OptionId == 2);
        Assert.Contains(ParseNpcReplies(pool.Sent), reply => reply.Text == "Change my Stardust." && reply.OptionId == 1);
        AssertAsciiReplies(buddha.Sent.Concat(swapper.Sent).Concat(pool.Sent));
    }

    [Fact]
    public async Task BuddhaAndSwapperTeleportBranchesUseSourceDestinations()
    {
        var buddha = await InvokeAsync(VicotryBuddhaNpcId, optionId: 3);
        var abyss = await InvokeAsync(SwapperStarryNpcId, optionId: 2);
        var twin = await InvokeAsync(SwapperStarryNpcId, optionId: 10, mapId: 1_700, x: 612, y: 634);

        Assert.True(buddha.Handled);
        Assert.Equal(1_004, buddha.UpdatedSession.Character.MapId);
        Assert.Equal(37, buddha.UpdatedSession.Character.X);
        Assert.Equal(36, buddha.UpdatedSession.Character.Y);
        Assert.Equal(1_700, abyss.UpdatedSession.Character.MapId);
        Assert.Equal(612, abyss.UpdatedSession.Character.X);
        Assert.Equal(634, abyss.UpdatedSession.Character.Y);
        Assert.Equal(1_002, twin.UpdatedSession.Character.MapId);
        Assert.Equal(410, twin.UpdatedSession.Character.X);
        Assert.Equal(354, twin.UpdatedSession.Character.Y);
    }

    [Fact]
    public async Task MarialDealerRequiresLocalJiangHuGradeAndShowsMenusWhenPresent()
    {
        var missing = await InvokeAsync(MarialDealerNpcId, optionId: 0);

        Assert.True(missing.Handled);
        Assert.Contains(ParseNpcReplies(missing.Sent), reply => reply.Text.Contains("Jiang Hu score reaches level 9", StringComparison.Ordinal));
        Assert.Contains("PARITY-GAP LegacyNpc EventUtilityNpcs", missing.Log);
        Assert.Contains("NpcHandler.MarialDealer", missing.Log);

        var qualified = await InvokeAsync(
            MarialDealerNpcId,
            optionId: 70,
            attributes: new Dictionary<string, string> { ["JiangHuGrade"] = "9" });

        Assert.True(qualified.Handled);
        Assert.Contains(ParseNpcReplies(qualified.Sent), reply => reply.Text == "Viper Fang." && reply.OptionId == 1);
        Assert.Contains(ParseNpcReplies(qualified.Sent), reply => reply.Text == "Next Page." && reply.OptionId == 8);
        Assert.DoesNotContain("PARITY-GAP", qualified.Log);
        AssertAsciiReplies(qualified.Sent);
    }

    [Theory]
    [InlineData(VicotryBuddhaNpcId, 2, "Buddha's Necklace exchange is unavailable here.", "NpcHandler.VicotryBuddha")]
    [InlineData(ElementalPoolNpcId, 3, "Elemental monster opening is unavailable here.", "NpcHandler.ElementalPool")]
    [InlineData(MarialDealerNpcId, 1, "Spell Soul updating is unavailable here.", "NpcHandler.MarialDealer")]
    public async Task UnsafeInventoryRandomAndSpellBranchesAreFenced(
        uint npcId,
        byte optionId,
        string text,
        string sourceMethod)
    {
        var attributes = npcId == MarialDealerNpcId
            ? new Dictionary<string, string> { ["JiangHuGrade"] = "9" }
            : null;
        var result = await InvokeAsync(npcId, optionId, conquerPoints: 10_000, attributes: attributes);

        Assert.True(result.Handled);
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text == text);
        Assert.Contains("PARITY-GAP LegacyNpc EventUtilityNpcs", result.Log);
        Assert.Contains(sourceMethod, result.Log);
    }

    private static async Task<EventUtilityInvokeResult> InvokeAsync(
        uint npcId,
        byte optionId,
        long conquerPoints = 0,
        long mapId = 1_002,
        long x = 430,
        long y = 378,
        Dictionary<string, string>? attributes = null,
        string? usersRoot = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(conquerPoints, mapId, x, y, attributes, usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyEventUtilityNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new EventUtilityInvokeResult(handled, sent, log.ToString(), updatedSession);
    }

    private static WorldSession CreateSession(
        long conquerPoints,
        long mapId,
        long x,
        long y,
        Dictionary<string, string>? attributes,
        string? usersRoot)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "event-utility-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "UtilityHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: mapId,
                X: x,
                Y: y,
                PreviousMapId: mapId,
                PreviousMapX: x,
                PreviousMapY: y,
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
                Attributes: attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
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

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
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

    private static void AssertAsciiReplies(IEnumerable<byte[]> sent)
    {
        foreach (var reply in ParseNpcReplies(sent))
        {
            if (!string.IsNullOrEmpty(reply.Text))
            {
                Assert.True(reply.Text.All(static character => character <= 127), reply.Text);
            }
        }
    }

    private sealed record EventUtilityInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession);
}
