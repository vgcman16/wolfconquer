using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("LegacyEliteGuildWarRuntime")]
public sealed class LegacyEliteGuildWarRewardNpcTests
{
    private const uint EliteGuildWarNpcId = 6_522;
    private const ulong WinningGuildId = 77;
    private const ulong OtherGuildId = 78;

    [Fact]
    public async Task OptionTwoUsesSourceAuditedSafeNoRewardPath()
    {
        WorldPacketHandlers.ResetLegacyEliteGuildWarScoreStateForTests();
        var result = await ClaimEliteGuildWarRewardAsync(
            CreateEliteGuildWarRewardSession(
                guildId: WinningGuildId,
                guildRank: "1",
                conquerPoints: 100,
                money: 200));

        Assert.True(result.Handled);
        AssertEliteGuildWarRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);

        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text is not null && reply.Text.Contains("reward", StringComparison.OrdinalIgnoreCase));
        AssertEnglishDialog(replies);

        Assert.Contains("source=NpcHandler.EliteGuildWar option=2", result.Log);
        Assert.Contains("SacredClassEXP", result.Log);
        Assert.Contains("Claim~reward.[EliteGuildWar]", result.Log);
        Assert.Contains("no case 2 reward body", result.Log, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PrizeAllTops.EliteGuildWar", result.Log);
        Assert.Contains("constant", result.Log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("25,000,000 CP mail reward", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(OtherGuildId, "1", "Dead")]
    [InlineData(WinningGuildId, "6", "Dead")]
    [InlineData(WinningGuildId, "1", "Alive")]
    public async Task OptionTwoDoesNotMutateNonwinnerUnsupportedRankOrActiveProcess(
        ulong guildId,
        string guildRank,
        string process)
    {
        WorldPacketHandlers.ResetLegacyEliteGuildWarScoreStateForTests();
        var result = await ClaimEliteGuildWarRewardAsync(
            CreateEliteGuildWarRewardSession(
                guildId: guildId,
                guildRank: guildRank,
                process: process,
                conquerPoints: 100,
                money: 200));

        Assert.True(result.Handled);
        AssertEliteGuildWarRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
        AssertEnglishDialog(ParseNpcReplies(result.Sent));
        Assert.Contains("source=NpcHandler.EliteGuildWar option=2", result.Log);
        Assert.Contains("no case 2 reward body", result.Log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RepeatedOptionTwoCallsRemainNonMutatingAndDoNotCreateClaimState()
    {
        WorldPacketHandlers.ResetLegacyEliteGuildWarScoreStateForTests();
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateEliteGuildWarRewardSession(
            guildId: WinningGuildId,
            guildRank: "1",
            conquerPoints: 100,
            money: 200);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var firstHandled = await ClickEliteGuildWarRewardAsync(connection, log, sessions, session);

        Assert.True(firstHandled);
        Assert.True(sessions.TryGet(connection, out var firstUpdated));
        Assert.NotNull(firstUpdated);
        Assert.Equal(100, firstUpdated.Character.ConquerPoints);
        Assert.Equal(200, firstUpdated.Character.Money);
        AssertNoEliteGuildWarClaimMarker(firstUpdated);

        sent.Clear();
        var secondHandled = await ClickEliteGuildWarRewardAsync(connection, log, sessions, firstUpdated);

        Assert.True(secondHandled);
        Assert.True(sessions.TryGet(connection, out var secondUpdated));
        Assert.NotNull(secondUpdated);
        Assert.Equal(firstUpdated.Character.ConquerPoints, secondUpdated.Character.ConquerPoints);
        Assert.Equal(firstUpdated.Character.Money, secondUpdated.Character.Money);
        AssertNoEliteGuildWarClaimMarker(secondUpdated);
        Assert.DoesNotContain(ParseMsgUpdates(sent), packet => packet.Updates.Any(IsWalletUpdate));
        Assert.Contains("no case 2 reward body", log.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PARITY-GAP", log.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SafeNoRewardPathDoesNotPersistWalletOrClaimState()
    {
        WorldPacketHandlers.ResetLegacyEliteGuildWarScoreStateForTests();
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-eliteguildwar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        try
        {
            var userPath = Path.Combine(usersRoot, "100.ini");
            await File.WriteAllTextAsync(
                userPath,
                """
                [Character]
                Name=EliteGuildHero
                UID=1293700
                ConquerPoints=100
                Money=200
                """);
            var session = CreateEliteGuildWarRewardSession(
                guildId: WinningGuildId,
                guildRank: "1",
                conquerPoints: 100,
                money: 200) with
            {
                UsersRoot = usersRoot,
            };

            var result = await ClaimEliteGuildWarRewardAsync(session);

            var persisted = await File.ReadAllTextAsync(userPath);
            AssertEliteGuildWarRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
            Assert.Contains("ConquerPoints=100", persisted);
            Assert.Contains("Money=200", persisted);
            Assert.DoesNotContain("EliteGuildWarRewardClaimed", persisted);
            Assert.DoesNotContain("WorldServer persisted LegacyNpc EliteGuildWar reward", result.Log);
            Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(usersRoot, recursive: true);
        }
    }

    private static async Task<EliteGuildWarRewardResult> ClaimEliteGuildWarRewardAsync(WorldSession session)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await ClickEliteGuildWarRewardAsync(connection, log, sessions, session);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new EliteGuildWarRewardResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private static async Task<bool> ClickEliteGuildWarRewardAsync(
        ConquerTcpConnection connection,
        StringWriter log,
        WorldSessionStore sessions,
        WorldSession session)
    {
        return await WorldPacketHandlers.TryHandleLegacyUnionEliteWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            EliteGuildWarNpcId,
            optionId: 2,
            CancellationToken.None);
    }

    private static WorldSession CreateEliteGuildWarRewardSession(
        ulong guildId,
        string guildRank,
        string process = "Dead",
        long conquerPoints = 0,
        long money = 0)
    {
        var guildIdText = guildId.ToString(CultureInfo.InvariantCulture);
        var winnerGuildIdText = WinningGuildId.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "0",
            ["GuildID"] = guildIdText,
            ["GuildId"] = guildIdText,
            ["MyGuildID"] = guildIdText,
            ["MyGuildId"] = guildIdText,
            ["GuildName"] = guildId == WinningGuildId ? "EliteWinners" : "RivalGuild",
            ["MyGuildName"] = guildId == WinningGuildId ? "EliteWinners" : "RivalGuild",
            ["GuildRank"] = guildRank,
            ["GuildMemberRank"] = guildRank,
            ["MyGuildRank"] = guildRank,
            ["MyGuildMemberRank"] = guildRank,
            ["LegacyGuildRank"] = guildRank,
        };

        SetEliteGuildWarAttributeAliases(attributes, "Process", process);
        SetEliteGuildWarAttributeAliases(attributes, "Proces", process);
        SetEliteGuildWarAttributeAliases(attributes, "RoundOwnerGuildID", winnerGuildIdText);
        SetEliteGuildWarAttributeAliases(attributes, "RoundOwnerGuildId", winnerGuildIdText);
        SetEliteGuildWarAttributeAliases(attributes, "RoundOwnerGuildName", "EliteWinners");
        SetEliteGuildWarAttributeAliases(attributes, "WinnerGuildID", winnerGuildIdText);
        SetEliteGuildWarAttributeAliases(attributes, "WinnerGuildId", winnerGuildIdText);
        SetEliteGuildWarAttributeAliases(attributes, "WinnerGuildName", "EliteWinners");

        return new WorldSession(
            AccountId: 100,
            Username: "elite-guild-war-reward-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_700,
                Name: "EliteGuildHero",
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

    private static void SetEliteGuildWarAttributeAliases(
        Dictionary<string, string> attributes,
        string suffix,
        string value)
    {
        string[] aliases =
        [
            "LegacyEliteGuildWar",
            "EliteGuildWar",
            "RuntimeEliteGuildWar",
            "LegacyMsgEliteGuildWar",
            "MsgEliteGuildWar",
            "RuntimeMsgEliteGuildWar",
        ];
        foreach (var alias in aliases)
        {
            attributes[alias + suffix] = value;
        }
    }

    private static void AssertEliteGuildWarRewardNotGranted(
        EliteGuildWarRewardResult result,
        long expectedConquerPoints,
        long expectedMoney)
    {
        Assert.Equal(expectedConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(expectedMoney, result.UpdatedSession.Character.Money);
        Assert.DoesNotContain(ParseMsgUpdates(result.Sent), packet => packet.Updates.Any(IsWalletUpdate));
        AssertNoEliteGuildWarClaimMarker(result.UpdatedSession);
    }

    private static void AssertNoEliteGuildWarClaimMarker(WorldSession updatedSession)
    {
        Assert.DoesNotContain(
            updatedSession.Character.Attributes,
            attribute => IsEliteGuildWarClaimMarker(attribute.Key, attribute.Value));
    }

    private static bool IsEliteGuildWarClaimMarker(string key, string value)
    {
        return key.Contains("EliteGuildWar", StringComparison.OrdinalIgnoreCase) &&
            key.Contains("Reward", StringComparison.OrdinalIgnoreCase) &&
            key.Contains("Claim", StringComparison.OrdinalIgnoreCase) &&
            IsTrue(value);
    }

    private static bool IsWalletUpdate(LegacyMsgUpdateEntry update)
    {
        return update.Type is LegacyMsgUpdateDataType.Conquer or
            LegacyMsgUpdateDataType.ConquerPoints or
            LegacyMsgUpdateDataType.Money;
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

    private static void AssertEnglishDialog(IEnumerable<LegacyNpcReplyPacket> replies)
    {
        Assert.All(
            replies.Where(reply => !string.IsNullOrEmpty(reply.Text)),
            reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }

    private static bool IsTrue(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("1", StringComparison.Ordinal);
    }

    private sealed record EliteGuildWarRewardResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession Session,
        WorldSession UpdatedSession);
}
