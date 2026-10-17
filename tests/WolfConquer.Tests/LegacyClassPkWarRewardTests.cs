using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClassPkWarRewardTests
{
    private const uint ClassPkWarNpcId = 16_851;
    private const uint PlayerUid = 1_293_700;
    private const uint RivalPlayerUid = 1_293_701;
    private const uint PreviousWinnerUid = 1_293_702;
    private const long InitialConquerPoints = 123;
    private const long ClassPkWarRewardConquerPoints = 500_000;
    private const uint PowerExpBallItemId = 722_057;
    private const ushort PowerExpBallStackSize = 4;
    private const int TopTrojanFlag = 39;
    private const string DynamicId = "123";
    private const string SourceAura = "TopTrojan";

    [Fact]
    public async Task SoleAliveEligiblePlayerClaimsSourceRewardWinnerStateAndTwinCityTeleport()
    {
        var result = await ClaimClassPkWarRewardAsync();

        Assert.True(result.Handled);
        var expectedConquerPoints = InitialConquerPoints + ClassPkWarRewardConquerPoints;
        Assert.Equal(expectedConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                IsConquerPointUpdate(entry) &&
                entry.Value == checked((ulong)expectedConquerPoints)));

        var powerExpBall = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == PowerExpBallItemId);
        Assert.Equal(PowerExpBallStackSize, powerExpBall.StackSize);
        Assert.Contains(
            ParseGameItems(result.Sent),
            item => item.ItemId == PowerExpBallItemId &&
                item.StackSize == PowerExpBallStackSize &&
                item.Mode == LegacyGameItemPacket.AddItemMode);

        AssertClassPkWarWinnerClaimState(result.UpdatedSession);
        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, TopTrojanFlag)));

        var teleport = Assert.Single(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(354U, teleport.PositionY);
        Assert.Equal(1_002, result.UpdatedSession.Character.MapId);
        Assert.Equal(410, result.UpdatedSession.Character.X);
        Assert.Equal(354, result.UpdatedSession.Character.Y);
    }

    [Fact]
    public async Task RewardClaimClearsPreviousOnlineWinnerSourceTitleFlag()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var previousSent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var previousConnection = CreateSendingConnection(previousSent);
        var attributes = CreateAliveClassPkWarAttributes();
        attributes["LegacyClassPkWarWinnerUID"] = PreviousWinnerUid.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyClassPkWarLastFlag"] = SourceAura;
        var session = CreateSession(PlayerUid, "ClassPkHero", attributes);
        var previousWinner = CreateSession(
            PreviousWinnerUid,
            "OldClassPkHero",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(TopTrojanFlag),
            });
        previousWinner = previousWinner with
        {
            Character = previousWinner.Character with
            {
                MapId = 1_002,
                X = 410,
                Y = 354,
                PreviousMapId = 1_002,
                PreviousMapX = 410,
                PreviousMapY = 354,
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        sessions.Set(previousConnection, previousWinner);

        var handled = await ClickClassPkWarRewardAsync(connection, log, sessions, session);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, TopTrojanFlag));
        Assert.True(sessions.TryGet(previousConnection, out var updatedPreviousWinner));
        Assert.NotNull(updatedPreviousWinner);
        Assert.False(HasLegacyStatusFlag(updatedPreviousWinner.Character.Attributes, TopTrojanFlag));
        Assert.Contains(
            ParseMsgUpdates(previousSent),
            packet => packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, TopTrojanFlag)));
    }

    [Fact]
    public async Task AnotherAlivePlayerInSameDynamicInstancePreventsReward()
    {
        var result = await ClaimClassPkWarRewardAsync(includeAliveRival: true);

        Assert.True(result.Handled);
        AssertClassPkWarRewardNotGranted(result.Sent, result.UpdatedSession, InitialConquerPoints);
        AssertUnavailableDialog(result.Sent);
    }

    [Fact]
    public async Task SecondClaimAfterSuccessfulRewardDoesNotDoublePay()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(PlayerUid, "ClassPkHero");
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var firstHandled = await ClickClassPkWarRewardAsync(connection, log, sessions, session);

        Assert.True(firstHandled);
        Assert.True(sessions.TryGet(connection, out var firstUpdated));
        Assert.NotNull(firstUpdated);
        var expectedConquerPoints = InitialConquerPoints + ClassPkWarRewardConquerPoints;
        Assert.Equal(expectedConquerPoints, firstUpdated.Character.ConquerPoints);
        Assert.Equal(1, firstUpdated.PlayerItems.InventoryItems.Count(item => item.ItemId == PowerExpBallItemId));

        sent.Clear();
        var secondHandled = await ClickClassPkWarRewardAsync(connection, log, sessions, firstUpdated);

        Assert.True(secondHandled);
        Assert.True(sessions.TryGet(connection, out var secondUpdated));
        Assert.NotNull(secondUpdated);
        Assert.Equal(expectedConquerPoints, secondUpdated.Character.ConquerPoints);
        Assert.Equal(1, secondUpdated.PlayerItems.InventoryItems.Count(item => item.ItemId == PowerExpBallItemId));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry => IsConquerPointUpdate(entry)));
        Assert.DoesNotContain(ParseGameItems(sent), item => item.ItemId == PowerExpBallItemId);
    }

    private static async Task<ClassPkWarRewardResult> ClaimClassPkWarRewardAsync(bool includeAliveRival = false)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(PlayerUid, "ClassPkHero");
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        if (includeAliveRival)
        {
            sessions.Set(
                CreateSendingConnection([]),
                CreateSession(RivalPlayerUid, "ClassPkRival"));
        }

        var handled = await ClickClassPkWarRewardAsync(connection, log, sessions, session);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new ClassPkWarRewardResult(handled, sent, log.ToString(), updatedSession);
    }

    private static async Task<bool> ClickClassPkWarRewardAsync(
        ConquerTcpConnection connection,
        StringWriter log,
        WorldSessionStore sessions,
        WorldSession session)
    {
        return await WorldPacketHandlers.TryHandleLegacyClassPkWarNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            ClassPkWarNpcId,
            optionId: 1,
            CancellationToken.None);
    }

    private static WorldSession CreateSession(
        uint playerUid,
        string name,
        Dictionary<string, string>? attributes = null)
    {
        attributes ??= CreateAliveClassPkWarAttributes();

        return new WorldSession(
            AccountId: playerUid,
            Username: name.ToLowerInvariant(),
            Character: new CharacterSnapshot(
                Uid: playerUid,
                Name: name,
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 1_000,
                FirstClass: 1_000,
                SecondClass: 0,
                Reborn: 0,
                Level: 130,
                Experience: 0,
                MapId: 1_764,
                X: 81,
                Y: 62,
                PreviousMapId: 1_764,
                PreviousMapX: 81,
                PreviousMapY: 62,
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

    private static Dictionary<string, string> CreateAliveClassPkWarAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = DynamicId,
            ["LegacyClassPkWarDynamicID"] = DynamicId,
            ["LegacyClassPkWarProcess"] = "Alive",
            ["LegacyClassPkWarFinishTimerUtc"] = DateTimeOffset.UtcNow.AddMinutes(30).ToString("O", CultureInfo.InvariantCulture),
        };
    }

    private static void AssertClassPkWarRewardNotGranted(
        IEnumerable<byte[]> sent,
        WorldSession updatedSession,
        long expectedConquerPoints)
    {
        Assert.Equal(expectedConquerPoints, updatedSession.Character.ConquerPoints);
        Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == PowerExpBallItemId);
        Assert.DoesNotContain(ParseGameItems(sent), item => item.ItemId == PowerExpBallItemId);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry => IsConquerPointUpdate(entry)));
    }

    private static void AssertClassPkWarWinnerClaimState(WorldSession updatedSession)
    {
        var attributes = updatedSession.Character.Attributes;
        Assert.Contains(attributes, pair =>
            IsClassPkWarKey(pair.Key) &&
            pair.Key.Contains("Process", StringComparison.OrdinalIgnoreCase) &&
            pair.Value.Equals("Dead", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attributes, pair =>
            IsClassPkWarKey(pair.Key) &&
            pair.Key.Contains("Winner", StringComparison.OrdinalIgnoreCase) &&
            ContainsUidKey(pair.Key) &&
            pair.Value == PlayerUid.ToString(CultureInfo.InvariantCulture));
        Assert.Contains(attributes, pair =>
            IsClassPkWarKey(pair.Key) &&
            pair.Key.Contains("Claim", StringComparison.OrdinalIgnoreCase) &&
            IsTrue(pair.Value));
        Assert.Contains(attributes, pair =>
            IsClassPkWarKey(pair.Key) &&
            pair.Key.Contains("Last", StringComparison.OrdinalIgnoreCase) &&
            pair.Key.Contains("Flag", StringComparison.OrdinalIgnoreCase) &&
            pair.Value == SourceAura);
        Assert.Contains(attributes, pair =>
            IsClassPkWarKey(pair.Key) &&
            pair.Key.Contains("Aura", StringComparison.OrdinalIgnoreCase) &&
            pair.Value == SourceAura);
        Assert.True(HasLegacyStatusFlag(attributes, TopTrojanFlag));
    }

    private static void AssertUnavailableDialog(IEnumerable<byte[]> sent)
    {
        var replies = ParseNpcReplies(sent);
        Assert.Contains(
            replies,
            reply => reply.Text.Contains("available", StringComparison.OrdinalIgnoreCase) ||
                reply.Text.Contains("finished", StringComparison.OrdinalIgnoreCase) ||
                reply.Text.Contains("alive", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsClassPkWarKey(string key)
    {
        return key.Contains("ClassPkWar", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("ClassPKWar", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsUidKey(string key)
    {
        return key.Contains("UID", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Uid", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrue(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("1", StringComparison.Ordinal);
    }

    private static bool HasLegacyStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        if (!attributes.TryGetValue("StatusFlags", out var raw) &&
            !attributes.TryGetValue("BitVector", out raw))
        {
            return false;
        }

        var values = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0UL)
            .ToArray();
        return IsLegacyStatusFlagSet(values, flag);
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var values = new ulong[10];
        foreach (var flag in flags)
        {
            var wordIndex = flag / 32;
            var bitIndex = flag % 32;
            values[wordIndex] |= 1UL << bitIndex;
        }

        return string.Join('/', values) + "/";
    }

    private static bool IsLegacyStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << bitIndex)) != 0;
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

    private sealed record ClassPkWarRewardResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession);
}
