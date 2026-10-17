using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPoleDominationRewardNpcTests
{
    private const uint PoleDominationNpcId = 770_001;
    private const uint PlayerUid = 1_293_600;

    [Theory]
    [InlineData(1, "PoleDomination")]
    [InlineData(2, "PoleDominationPC")]
    [InlineData(3, "PoleDominationDC")]
    [InlineData(4, "PoleDominationBI")]
    public async Task PoleDominationRewardOptionsStayBehindSafeFenceForDistinctLocalLanes(
        int optionId,
        string scheduleName)
    {
        var result = await ClaimPoleDominationRewardAsync(
            checked((byte)optionId),
            attributes: CreatePoleDominationWinnerAttributes(scheduleName),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertPoleDominationRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
        AssertSafeUnavailableDialog(result);
        AssertPoleDominationRewardFenceLog(result, checked((byte)optionId), scheduleName);
    }

    [Fact]
    public async Task PoleDominationRewardActiveProcessIsDeniedSafelyWithoutEconomyChange()
    {
        var result = await ClaimPoleDominationRewardAsync(
            optionId: 1,
            attributes: CreatePoleDominationWinnerAttributes("PoleDomination", process: "Alive"),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertPoleDominationRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
        AssertSafeUnavailableDialog(result);
        AssertPoleDominationRewardFenceLog(result, optionId: 1, scheduleName: "PoleDomination");
    }

    [Fact]
    public async Task PoleDominationRewardMissingWinnerIsDeniedSafelyWithoutEconomyChange()
    {
        var result = await ClaimPoleDominationRewardAsync(
            optionId: 1,
            attributes: CreatePoleDominationWinnerAttributes(
                "PoleDomination",
                winnerGuildId: "0",
                winnerGuildName: string.Empty),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertPoleDominationRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
        AssertSafeUnavailableDialog(result);
        AssertPoleDominationRewardFenceLog(result, optionId: 1, scheduleName: "PoleDomination");
    }

    [Theory]
    [InlineData("wrong guild", "88", "100")]
    [InlineData("non leader", "77", "6")]
    public async Task PoleDominationRewardWrongGuildOrNonLeaderIsDeniedSafelyWithoutEconomyChange(
        string scenario,
        string playerGuildId,
        string guildRank)
    {
        var result = await ClaimPoleDominationRewardAsync(
            optionId: 1,
            attributes: CreatePoleDominationWinnerAttributes(
                "PoleDomination",
                playerGuildId: playerGuildId,
                guildRank: guildRank),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertPoleDominationRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
        AssertSafeUnavailableDialog(result);
        AssertPoleDominationRewardFenceLog(result, optionId: 1, scheduleName: "PoleDomination");
        Assert.False(string.IsNullOrWhiteSpace(scenario));
    }

    [Fact]
    public async Task PoleDominationEligibleWinningGuildLeaderRemainsUnclaimedAndUnclearedUntilSourceBackedRewardExists()
    {
        const string scheduleName = "PoleDomination";
        var result = await ClaimPoleDominationRewardAsync(
            optionId: 1,
            attributes: CreatePoleDominationWinnerAttributes(scheduleName),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertPoleDominationRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
        AssertPoleDominationRewardStateNotClaimedOrCleared(result.UpdatedSession, scheduleName);
        AssertPoleDominationRoundOwnerStillPresent(result.UpdatedSession, scheduleName);
        AssertSafeUnavailableDialog(result);
        AssertPoleDominationRewardFenceLog(result, optionId: 1, scheduleName);
    }

    private static async Task<PoleDominationInvokeResult> ClaimPoleDominationRewardAsync(
        byte optionId,
        Dictionary<string, string>? attributes = null,
        long conquerPoints = 0,
        long money = 0)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            attributes: attributes ?? CreatePoleDominationWinnerAttributes("PoleDomination"),
            conquerPoints: conquerPoints,
            money: money);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPoleWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            PoleDominationNpcId,
            optionId,
            new DateTime(2026, 6, 20, 16, 5, 0),
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new PoleDominationInvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private static WorldSession CreateSession(
        Dictionary<string, string> attributes,
        long conquerPoints,
        long money)
    {
        attributes.TryAdd("DynamicID", "0");
        attributes.TryAdd("ConquerPoints", conquerPoints.ToString(CultureInfo.InvariantCulture));
        attributes.TryAdd("Money", money.ToString(CultureInfo.InvariantCulture));

        return new WorldSession(
            AccountId: 100,
            Username: "pole-domination-reward-test",
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
                Money: money,
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

    private static Dictionary<string, string> CreatePoleDominationWinnerAttributes(
        string scheduleName,
        string playerGuildId = "77",
        string playerGuildName = "SourceGuild",
        string guildRank = "100",
        string process = "Dead",
        string winnerGuildId = "77",
        string winnerGuildName = "SourceGuild",
        string winnerScore = "456789")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GuildID"] = playerGuildId,
            ["GuildId"] = playerGuildId,
            ["MyGuildID"] = playerGuildId,
            ["MyGuildId"] = playerGuildId,
            ["GuildName"] = playerGuildName,
            ["GuildRank"] = guildRank,
            ["GuildMemberRank"] = guildRank,
            ["MyGuildRank"] = guildRank,
            ["MyGuildMemberRank"] = guildRank,
            ["LegacyGuildRank"] = guildRank,
        };

        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "Process", process);
        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "Proces", process);
        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "PoleHitPoints", "50000");
        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "PoleHitpoints", "50000");
        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "WinnerGuildID", winnerGuildId);
        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "WinnerGuildId", winnerGuildId);
        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "RoundOwnerGuildID", winnerGuildId);
        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "RoundOwnerGuildId", winnerGuildId);
        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "WinnerGuildName", winnerGuildName);
        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "RoundOwnerGuildName", winnerGuildName);
        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "WinnerScore", winnerScore);
        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "RoundOwnerScore", winnerScore);
        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "LeaderReward", "1");
        SetPoleDominationScheduleAttributeTriplet(attributes, scheduleName, "WinnerLeaderReward", "1");
        return attributes;
    }

    private static void SetPoleDominationScheduleAttributeTriplet(
        IDictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        string value)
    {
        attributes["Legacy" + scheduleName + suffix] = value;
        attributes[scheduleName + suffix] = value;
        attributes["Runtime" + scheduleName + suffix] = value;
    }

    private static void AssertPoleDominationRewardNotGranted(
        PoleDominationInvokeResult result,
        long expectedConquerPoints,
        long expectedMoney)
    {
        Assert.True(
            result.UpdatedSession.Character.ConquerPoints == expectedConquerPoints,
            "PoleDomination reward fence should not grant CP without source-backed nonzero reward evidence. " +
            $"Expected {expectedConquerPoints.ToString(CultureInfo.InvariantCulture)}, " +
            $"actual {result.UpdatedSession.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture)}.");
        Assert.True(
            result.UpdatedSession.Character.Money == expectedMoney,
            "PoleDomination reward fence should not grant silver without source-backed nonzero reward evidence. " +
            $"Expected {expectedMoney.ToString(CultureInfo.InvariantCulture)}, " +
            $"actual {result.UpdatedSession.Character.Money.ToString(CultureInfo.InvariantCulture)}.");
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.DoesNotContain(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Conquer or LegacyMsgUpdateDataType.Money));
        Assert.All(ParseNpcReplies(result.Sent).Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    private static void AssertSafeUnavailableDialog(PoleDominationInvokeResult result)
    {
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.Text == "No reward is available from this NPC in the current runtime state.");
    }

    private static void AssertPoleDominationRewardFenceLog(
        PoleDominationInvokeResult result,
        byte optionId,
        string scheduleName)
    {
        Assert.Contains("PARITY-GAP LegacyNpc PoleWarEvent reward unavailable", result.Log);
        Assert.Contains("source=NpcHandler.PoleDomination", result.Log);
        Assert.Contains($"option={optionId.ToString(CultureInfo.InvariantCulture)}", result.Log);
        Assert.True(
            result.Log.Contains(scheduleName, StringComparison.Ordinal),
            "Expected the safe-fence evidence to retain the local PoleDomination lane name " +
            $"'{scheduleName}' while the source reward remains unresolved.");
        Assert.Contains("no direct NpcHandler.PoleDomination branch was found in the external source references.", result.Log);
    }

    private static void AssertPoleDominationRewardStateNotClaimedOrCleared(
        WorldSession updatedSession,
        string scheduleName)
    {
        foreach (var key in PoleDominationRewardClaimStateAttributeNames(scheduleName))
        {
            Assert.False(
                IsTruthyAttribute(updatedSession.Character.Attributes, key),
                $"Expected safe-fenced PoleDomination reward to leave '{key}' unclaimed until source-backed reward evidence exists.");
        }
    }

    private static void AssertPoleDominationRoundOwnerStillPresent(
        WorldSession updatedSession,
        string scheduleName)
    {
        foreach (var key in PoleDominationRoundOwnerAttributeNames(scheduleName))
        {
            Assert.True(
                HasNonEmptyAttribute(updatedSession.Character.Attributes, key),
                $"Expected safe-fenced PoleDomination reward to preserve source winner attribute '{key}' for later parity work.");
        }
    }

    private static IEnumerable<string> PoleDominationRewardClaimStateAttributeNames(string scheduleName)
    {
        foreach (var prefix in PoleDominationSchedulePrefixes(scheduleName))
        {
            yield return prefix + "RewardClaimed";
            yield return prefix + "LeaderRewardClaimed";
            yield return prefix + "WinnerCleared";
        }
    }

    private static IEnumerable<string> PoleDominationRoundOwnerAttributeNames(string scheduleName)
    {
        foreach (var prefix in PoleDominationSchedulePrefixes(scheduleName))
        {
            yield return prefix + "WinnerGuildID";
            yield return prefix + "WinnerGuildId";
            yield return prefix + "RoundOwnerGuildID";
            yield return prefix + "RoundOwnerGuildId";
            yield return prefix + "WinnerGuildName";
            yield return prefix + "RoundOwnerGuildName";
            yield return prefix + "WinnerScore";
            yield return prefix + "RoundOwnerScore";
        }
    }

    private static IEnumerable<string> PoleDominationSchedulePrefixes(string scheduleName)
    {
        yield return "Legacy" + scheduleName;
        yield return scheduleName;
        yield return "Runtime" + scheduleName;
    }

    private static bool IsTruthyAttribute(IReadOnlyDictionary<string, string> attributes, string key)
    {
        if (!attributes.TryGetValue(key, out var value))
        {
            return false;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("1", StringComparison.Ordinal);
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }

    private sealed record PoleDominationInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession Session,
        WorldSession UpdatedSession);
}
