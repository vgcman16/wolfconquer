using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTopPoleRewardNpcTests
{
    private const uint TopPoleNpcId = 43_431;
    private const uint PlayerUid = 1_293_600;
    private const uint RivalPlayerUid = 1_293_601;
    private const long SourceTopPoleRewardConquerPoints = 10_000_000;
    private const long TopPoleRewardConquerPoints = 300_000;
    private const ushort SourceTopPoleRewardStudyPoints = 10_000;
    private const int SourceTopPoleInnerPowerPotencyReward = 10_000;

    private static readonly (uint ItemId, ushort StackSize)[] ExpectedTopPoleRewardItems =
    [
        (3_009_003, 3),
        (4_050_001, 500),
        (4_040_001, 500),
    ];

    [Fact]
    public async Task TopPoleRewardActiveProcessIsDeniedWithSafeDialog()
    {
        var result = await ClaimTopPoleRewardAsync(
            attributes: CreateTopPoleWinnerAttributes(process: "Alive"),
            conquerPoints: 100);

        Assert.True(result.Handled);
        AssertTopPoleRewardNotGranted(result.Sent, result.UpdatedSession, expectedConquerPoints: 100);
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text == "Please come back when TopPole is finished.");
    }

    [Fact]
    public async Task TopPoleRewardMissingWinnerIsDeniedWithSafeDialog()
    {
        var result = await ClaimTopPoleRewardAsync(
            attributes: CreateTopPoleWinnerAttributes(winnerPlayerUid: "0", winnerName: string.Empty),
            conquerPoints: 100);

        Assert.True(result.Handled);
        AssertTopPoleRewardNotGranted(result.Sent, result.UpdatedSession, expectedConquerPoints: 100);
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text == "There is no reward available now.");
    }

    [Fact]
    public async Task TopPoleRewardWrongPlayerIsDeniedWithSafeDialog()
    {
        var result = await ClaimTopPoleRewardAsync(
            attributes: CreateTopPoleWinnerAttributes(
                winnerPlayerUid: RivalPlayerUid.ToString(CultureInfo.InvariantCulture),
                winnerName: "RivalPoleHero"),
            conquerPoints: 100);

        Assert.True(result.Handled);
        AssertTopPoleRewardNotGranted(result.Sent, result.UpdatedSession, expectedConquerPoints: 100);
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text == "You do not have any reward to claim.");
    }

    [Fact]
    public async Task TopPoleRewardEligibleClaimantGrantsTunedCpSourceBundleAndClearsWinner()
    {
        var result = await ClaimTopPoleRewardAsync(
            attributes: CreateTopPoleWinnerAttributes(),
            conquerPoints: 100);

        Assert.True(result.Handled);
        var expectedConquerPoints = 100 + TopPoleRewardConquerPoints;
        Assert.Equal(expectedConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                entry.Value == checked((ulong)expectedConquerPoints)));
        AssertTopPoleSourceRewardItemsGranted(result.UpdatedSession, result.Sent);
        AssertTopPoleStudyPointsGranted(result.UpdatedSession);
        AssertTopPoleRoundOwnerCleared(result.UpdatedSession);
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text.Contains("300000", StringComparison.Ordinal));
        Assert.Contains($"sourceConquerPoints={SourceTopPoleRewardConquerPoints.ToString(CultureInfo.InvariantCulture)}", result.Log);
        Assert.Contains($"tunedConquerPoints={TopPoleRewardConquerPoints.ToString(CultureInfo.InvariantCulture)}", result.Log);
        Assert.Contains("items=3009003x3,4050001x500,4040001x500", result.Log);
        Assert.Contains($"studyPoints={SourceTopPoleRewardStudyPoints.ToString(CultureInfo.InvariantCulture)}", result.Log);
        Assert.Contains($"innerPowerPotency={SourceTopPoleInnerPowerPotencyReward.ToString(CultureInfo.InvariantCulture)}", result.Log);
        Assert.Contains(
            @"references/DecompiledSource\GameServer\MrRayzo.Game.MsgNpc\NpcHandler.cs TopPole option 2",
            result.Log);
    }

    [Fact]
    public async Task TopPoleRewardDuplicateClaimDoesNotDoublePay()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(attributes: CreateTopPoleWinnerAttributes(), conquerPoints: 100);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var firstHandled = await ClickTopPoleRewardAsync(connection, log, sessions, session);

        Assert.True(firstHandled);
        Assert.True(sessions.TryGet(connection, out var firstUpdated));
        Assert.NotNull(firstUpdated);
        var expectedConquerPoints = 100 + TopPoleRewardConquerPoints;
        Assert.Equal(expectedConquerPoints, firstUpdated.Character.ConquerPoints);
        AssertTopPoleSourceRewardItemsGranted(firstUpdated, sent);
        AssertTopPoleStudyPointsGranted(firstUpdated);

        sent.Clear();
        var secondHandled = await ClickTopPoleRewardAsync(connection, log, sessions, firstUpdated);

        Assert.True(secondHandled);
        Assert.True(sessions.TryGet(connection, out var secondUpdated));
        Assert.NotNull(secondUpdated);
        Assert.Equal(expectedConquerPoints, secondUpdated.Character.ConquerPoints);
        AssertTopPoleSourceRewardItemsGranted(secondUpdated, []);
        AssertTopPoleStudyPointsGranted(secondUpdated);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Conquer or LegacyMsgUpdateDataType.ConquerPoints));
        Assert.DoesNotContain(
            ParseGameItems(sent),
            item => ExpectedTopPoleRewardItems.Any(expected => expected.ItemId == item.ItemId));
    }

    private static async Task<TopPoleInvokeResult> ClaimTopPoleRewardAsync(
        Dictionary<string, string>? attributes = null,
        long conquerPoints = 0)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            attributes: attributes ?? CreateTopPoleWinnerAttributes(),
            conquerPoints: conquerPoints);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await ClickTopPoleRewardAsync(connection, log, sessions, session);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new TopPoleInvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private static async Task<bool> ClickTopPoleRewardAsync(
        ConquerTcpConnection connection,
        StringWriter log,
        WorldSessionStore sessions,
        WorldSession session)
    {
        return await WorldPacketHandlers.TryHandleLegacyPoleWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            TopPoleNpcId,
            optionId: 2,
            now: new DateTime(2026, 6, 20, 11, 45, 0),
            CancellationToken.None);
    }

    private static WorldSession CreateSession(
        Dictionary<string, string>? attributes = null,
        long conquerPoints = 0)
    {
        attributes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        attributes.TryAdd("DynamicID", "0");
        attributes.TryAdd("ConquerPoints", conquerPoints.ToString(CultureInfo.InvariantCulture));

        return new WorldSession(
            AccountId: 100,
            Username: "top-pole-test",
            Character: new CharacterSnapshot(
                Uid: PlayerUid,
                Name: "PoleHero",
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
                ConquerPoints: conquerPoints,
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

    private static Dictionary<string, string> CreateTopPoleWinnerAttributes(
        string process = "Dead",
        string? winnerPlayerUid = null,
        string winnerName = "PoleHero",
        string winnerScore = "1250")
    {
        winnerPlayerUid ??= PlayerUid.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "0",
        };

        foreach (var processKey in TopPoleProcessAttributeNames())
        {
            attributes[processKey] = process;
        }

        SetTopPoleAlias(attributes, "RoundOwnerPlayerUID", winnerPlayerUid);
        SetTopPoleAlias(attributes, "RoundOwnerPlayerUid", winnerPlayerUid);
        SetTopPoleAlias(attributes, "RoundOwnerUID", winnerPlayerUid);
        SetTopPoleAlias(attributes, "RoundOwnerUid", winnerPlayerUid);
        SetTopPoleAlias(attributes, "WinnerPlayerUID", winnerPlayerUid);
        SetTopPoleAlias(attributes, "WinnerPlayerUid", winnerPlayerUid);
        SetTopPoleAlias(attributes, "WinnerUID", winnerPlayerUid);
        SetTopPoleAlias(attributes, "WinnerUid", winnerPlayerUid);
        SetTopPoleAlias(attributes, "RoundOwnerName", winnerName);
        SetTopPoleAlias(attributes, "WinnerName", winnerName);
        SetTopPoleAlias(attributes, "RoundOwnerScore", winnerScore);
        SetTopPoleAlias(attributes, "WinnerScore", winnerScore);
        return attributes;
    }

    private static void SetTopPoleAlias(IDictionary<string, string> attributes, string suffix, string value)
    {
        foreach (var prefix in TopPoleAttributePrefixes())
        {
            attributes[prefix + suffix] = value;
        }
    }

    private static IReadOnlyList<string> TopPoleAttributePrefixes()
    {
        return
        [
            "LegacyMsgTopPole",
            "MsgTopPole",
            "RuntimeMsgTopPole",
            "LegacyTopPole",
            "TopPole",
            "RuntimeTopPole",
        ];
    }

    private static IReadOnlyList<string> TopPoleProcessAttributeNames()
    {
        return
        [
            "LegacyMsgTopPoleProcess",
            "MsgTopPoleProcess",
            "RuntimeMsgTopPoleProcess",
            "LegacyTopPoleProcess",
            "TopPoleProcess",
            "RuntimeTopPoleProcess",
            "LegacyMsgTopPoleProces",
            "MsgTopPoleProces",
            "RuntimeMsgTopPoleProces",
            "LegacyTopPoleProces",
            "TopPoleProces",
            "RuntimeTopPoleProces",
        ];
    }

    private static IReadOnlyList<string> TopPoleRoundOwnerAttributeNames()
    {
        var names = new List<string>();
        foreach (var prefix in TopPoleAttributePrefixes())
        {
            names.Add(prefix + "RoundOwnerPlayerUID");
            names.Add(prefix + "RoundOwnerPlayerUid");
            names.Add(prefix + "RoundOwnerUID");
            names.Add(prefix + "RoundOwnerUid");
            names.Add(prefix + "RoundOwnerName");
            names.Add(prefix + "RoundOwnerScore");
            names.Add(prefix + "WinnerPlayerUID");
            names.Add(prefix + "WinnerPlayerUid");
            names.Add(prefix + "WinnerUID");
            names.Add(prefix + "WinnerUid");
            names.Add(prefix + "WinnerName");
            names.Add(prefix + "WinnerScore");
        }

        return names;
    }

    private static void AssertTopPoleRewardNotGranted(
        IEnumerable<byte[]> sent,
        WorldSession updatedSession,
        long expectedConquerPoints)
    {
        Assert.Equal(expectedConquerPoints, updatedSession.Character.ConquerPoints);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Conquer or LegacyMsgUpdateDataType.ConquerPoints));
        Assert.DoesNotContain(
            updatedSession.PlayerItems.InventoryItems,
            item => ExpectedTopPoleRewardItems.Any(expected => expected.ItemId == item.ItemId));
        Assert.DoesNotContain(
            ParseGameItems(sent),
            item => ExpectedTopPoleRewardItems.Any(expected => expected.ItemId == item.ItemId));
        Assert.All(ParseNpcReplies(sent).Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    private static void AssertTopPoleSourceRewardItemsGranted(
        WorldSession updatedSession,
        IEnumerable<byte[]> sent)
    {
        var inventoryRewardItems = updatedSession.PlayerItems.InventoryItems
            .Where(item => ExpectedTopPoleRewardItems.Any(expected => expected.ItemId == item.ItemId))
            .ToArray();
        Assert.Equal(ExpectedTopPoleRewardItems.Length, inventoryRewardItems.Length);

        var sentFrames = sent.ToArray();
        var sentRewardItems = ParseGameItems(sentFrames);
        foreach (var expected in ExpectedTopPoleRewardItems)
        {
            Assert.Contains(
                inventoryRewardItems,
                item => item.ItemId == expected.ItemId && item.StackSize == expected.StackSize);

            if (sentFrames.Length != 0)
            {
                Assert.Contains(
                    sentRewardItems,
                    item => item.ItemId == expected.ItemId && item.StackSize == expected.StackSize);
            }
        }
    }

    private static void AssertTopPoleStudyPointsGranted(WorldSession updatedSession)
    {
        Assert.True(
            updatedSession.Character.Attributes.TryGetValue("SubProfInfo", out var subProfInfo),
            "Expected TopPole source study reward to write SubProfInfo.");
        Assert.Equal(
            $"{SourceTopPoleRewardStudyPoints.ToString(CultureInfo.InvariantCulture)}#0#0#",
            subProfInfo);
    }

    private static void AssertTopPoleRoundOwnerCleared(WorldSession updatedSession)
    {
        foreach (var key in TopPoleRoundOwnerAttributeNames())
        {
            var value = updatedSession.Character.Attributes.TryGetValue(key, out var raw) ? raw : "<missing>";
            Assert.False(
                HasNonEmptyAttribute(updatedSession.Character.Attributes, key),
                $"Expected TopPole reward claim to clear '{key}', but value was '{value}'.");
        }
    }

    private static bool HasNonEmptyAttribute(IReadOnlyDictionary<string, string> attributes, string key)
    {
        return attributes.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value) &&
            !string.Equals(value, "0", StringComparison.Ordinal);
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

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }

    private sealed record TopPoleInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession Session,
        WorldSession UpdatedSession);
}
