using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClassPkWarNpcTests
{
    private const uint ClassPkEnvoyNpcId = 662;
    private const uint ClassPkWarNpcId = 16_851;
    private const uint PlayerUid = 1_293_700;
    private const long InitialConquerPoints = 123;

    [Fact]
    public async Task EnvoyRootDialogUsesSourceTextOptionsAndAvatar()
    {
        var result = await ClickClassPkWarNpcAsync(ClassPkEnvoyNpcId, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Everyone settle down! I need to announce that there are new rules for the Class PK War!");
        AssertOption(replies, "Tell me more.", 1);
        AssertOption(replies, "Sign up.", 2);
        AssertOption(replies, "Not interested.", LegacyNpcReplyPacket.ClosedOptionId);
        AssertAvatar(replies);
        Assert.Empty(ParseMsgActions(result.Sent));
    }

    [Fact]
    public async Task EnvoyTellMeMoreKeepsSourceNoOp()
    {
        var result = await ClickClassPkWarNpcAsync(ClassPkEnvoyNpcId, optionId: 1);

        Assert.True(result.Handled);
        Assert.Empty(result.Sent);
        Assert.Equal(InitialConquerPoints, result.UpdatedSession.Character.ConquerPoints);
    }

    [Fact]
    public async Task EnvoySignUpTeleportsToModeledClassPkWarMapWhenJoinIsAllowed()
    {
        var attributes = CreateClassPkWarAliveAttributes();
        var result = await ClickClassPkWarNpcAsync(
            ClassPkEnvoyNpcId,
            optionId: 2,
            session: CreateSession(mapId: 1_002, x: 430, y: 378, attributes: attributes));

        Assert.True(result.Handled);
        var teleport = Assert.Single(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_764U, teleport.DwParam);
        Assert.Equal(81U, teleport.PositionX);
        Assert.Equal(62U, teleport.PositionY);
        Assert.Equal(1_764, result.UpdatedSession.Character.MapId);
        Assert.Equal(81, result.UpdatedSession.Character.X);
        Assert.Equal(62, result.UpdatedSession.Character.Y);
    }

    [Fact]
    public async Task EnvoySignUpSendsMondayUnavailableDialogWhenJoinIsDenied()
    {
        var result = await ClickClassPkWarNpcAsync(
            ClassPkEnvoyNpcId,
            optionId: 2,
            session: CreateSession(attributes: CreateClassPkWarUnavailableAttributes()));

        Assert.True(result.Handled);
        Assert.Empty(ParseMsgActions(result.Sent));
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "I'm sorry, but the Class PK War is only held on Mondays.");
        AssertOption(replies, "Alright.", LegacyNpcReplyPacket.ClosedOptionId);
        AssertAvatar(replies);
        Assert.Equal(1_002, result.UpdatedSession.Character.MapId);
    }

    [Fact]
    public async Task ArenaRootDialogUsesSourceTextOptionsAndAvatar()
    {
        var result = await ClickClassPkWarNpcAsync(ClassPkWarNpcId, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Hello, are you the last player alive?");
        AssertOption(replies, "Yes", 1);
        AssertOption(replies, "Teleport to Twin City.", 2);
        AssertOption(replies, "Ah, wait.", LegacyNpcReplyPacket.ClosedOptionId);
        AssertAvatar(replies);
    }

    [Fact]
    public async Task ArenaRewardOptionClaimsSourceRewardWhenSoleAlive()
    {
        var result = await ClickClassPkWarNpcAsync(
            ClassPkWarNpcId,
            optionId: 1,
            session: CreateSession(
                mapId: 1_764,
                x: 81,
                y: 62,
                attributes: CreateClassPkWarAliveAttributes()));

        Assert.True(result.Handled);
        Assert.Equal(InitialConquerPoints + 500_000, result.UpdatedSession.Character.ConquerPoints);
        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(722_057U, reward.ItemId);
        Assert.Equal(4, reward.StackSize);
        Assert.Contains(ParseGameItems(result.Sent), item => item.ItemId == 722_057U && item.StackSize == 4);
        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry => IsConquerPointUpdate(entry) && entry.Value == 500_123UL));
        var teleport = Assert.Single(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(354U, teleport.PositionY);
    }

    [Fact]
    public async Task ArenaTeleportOptionAsksForTwinCityConfirmation()
    {
        var result = await ClickClassPkWarNpcAsync(ClassPkWarNpcId, optionId: 2);

        Assert.True(result.Handled);
        Assert.Empty(ParseMsgActions(result.Sent));
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("teleport back to Twin City", StringComparison.Ordinal));
        AssertOption(replies, "Yes", 3);
        AssertOption(replies, "Ah no.", LegacyNpcReplyPacket.ClosedOptionId);
        AssertAvatar(replies);
    }

    [Fact]
    public async Task ArenaConfirmTeleportReturnsToTwinCity()
    {
        var result = await ClickClassPkWarNpcAsync(
            ClassPkWarNpcId,
            optionId: 3,
            session: CreateSession(mapId: 1_764, x: 81, y: 62));

        Assert.True(result.Handled);
        var teleport = Assert.Single(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(354U, teleport.PositionY);
        Assert.Equal(1_002, result.UpdatedSession.Character.MapId);
        Assert.Equal(410, result.UpdatedSession.Character.X);
        Assert.Equal(354, result.UpdatedSession.Character.Y);
    }

    private static async Task<ClassPkWarNpcInvokeResult> ClickClassPkWarNpcAsync(
        uint npcId,
        byte optionId,
        WorldSession? session = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var activeSession = session ?? CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, activeSession);

        var handled = await WorldPacketHandlers.TryHandleLegacyClassPkWarNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            activeSession,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new ClassPkWarNpcInvokeResult(handled, sent, log.ToString(), updatedSession);
    }

    private static Dictionary<string, string> CreateClassPkWarAliveAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyClassPkWarProcess"] = "Alive",
            ["LegacyClassPkWarFinishTimerUtc"] = DateTimeOffset.UtcNow.AddMinutes(30).ToString("O", CultureInfo.InvariantCulture),
            ["LegacyClassPkWarDynamicID"] = "123",
        };
    }

    private static Dictionary<string, string> CreateClassPkWarUnavailableAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyClassPkWarProcess"] = "Dead",
            ["LegacyClassPkWarFinishTimerUtc"] = DateTimeOffset.UtcNow.AddMinutes(-30).ToString("O", CultureInfo.InvariantCulture),
            ["LegacyClassPkWarDynamicID"] = "123",
        };
    }

    private static WorldSession CreateSession(
        long mapId = 1_002,
        long x = 430,
        long y = 378,
        Dictionary<string, string>? attributes = null)
    {
        attributes = attributes is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase);
        attributes.TryAdd("DynamicID", "0");

        return new WorldSession(
            AccountId: 100,
            Username: "class-pk-war-test",
            Character: new CharacterSnapshot(
                Uid: PlayerUid,
                Name: "ClassPkHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 1_000,
                FirstClass: 1_000,
                SecondClass: 0,
                Reborn: 0,
                Level: 130,
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
                ConquerPoints: InitialConquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: attributes,
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

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text, byte optionId)
    {
        Assert.Contains(replies, reply => reply.Text == text && reply.OptionId == optionId);
    }

    private static void AssertAvatar(IReadOnlyList<LegacyNpcReplyPacket> replies)
    {
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == 154);
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

    private static bool IsConquerPointUpdate(LegacyMsgUpdateEntry entry)
    {
        return entry.Type is LegacyMsgUpdateDataType.ConquerPoints;
    }

    private sealed record ClassPkWarNpcInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession);
}
