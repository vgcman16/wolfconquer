using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyUnionEliteWarEventNpcTests
{
    private const uint UnionWarNpcId = 44_443;
    private const uint EliteGuildWarNpcId = 6_522;
    private const uint TrepilWarNpcId = 6_523;
    private const long LegacyUnionWarSourceConquerPointsReward = 150_000;
    private const long TunedUnionWarConquerPointsReward = 150_000;
    private const long TunedUnionWarMoneyReward = 500_000;
    private const long LegacyTrepilWarSourceConquerPointsReward = 5_000_000;
    private const long TunedTrepilWarConquerPointsReward = 300_000;
    private const ulong WinningUnionId = 77;
    private const ulong OtherUnionId = 78;
    private const ulong WinningGuildId = 77;
    private const ulong OtherGuildId = 78;

    [Theory]
    [InlineData(UnionWarNpcId, "Hi, I manage Union War. It runs every day from 22:30 to 23:00. How can I help?", "NpcHandler.UnionWar")]
    [InlineData(EliteGuildWarNpcId, "Hi, I manage Elite Guild War. It runs every day from 20:00 to 20:30. How can I help?", "NpcHandler.EliteGuildWar")]
    [InlineData(TrepilWarNpcId, "Hi, I manage Trepil War. It runs every day from 02:00 to 02:30. How can I help?", "NpcHandler.MsgTrepilWar")]
    public async Task HandlerSendsSourceBackedTranslatedRootDialogAndOptions(
        uint npcId,
        string expectedText,
        string sourceMethod)
    {
        var result = await InvokeHandlerAsync(npcId, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == expectedText);
        Assert.Contains(replies, reply => reply.Text == "Enter the battle." && reply.OptionId == 1);
        Assert.Contains(replies, reply => reply.Text == "Claim reward." && reply.OptionId == 2);
        Assert.Contains(replies, reply => reply.Text == "Okay, I understand." && reply.OptionId == byte.MaxValue);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains($"source={sourceMethod}", result.Log);
    }

    [Theory]
    [InlineData(UnionWarNpcId, 2_060, 135, 135, "NpcHandler.UnionWar")]
    [InlineData(EliteGuildWarNpcId, 6_521, 83, 139, "NpcHandler.EliteGuildWar")]
    [InlineData(TrepilWarNpcId, 3_838, 330, 220, "NpcHandler.MsgTrepilWar")]
    public async Task HandlerTeleportsEnterOptionToSourceDestination(
        uint npcId,
        uint expectedMapId,
        uint expectedX,
        uint expectedY,
        string sourceMethod)
    {
        var result = await InvokeHandlerAsync(npcId, optionId: 1);

        Assert.True(result.Handled);
        AssertLegacyNpcTeleportTransition(result.Sent, result.Session, expectedMapId, expectedX, expectedY);
        Assert.Equal(expectedMapId, (uint)result.UpdatedSession.Character.MapId);
        Assert.Equal(expectedX, (uint)result.UpdatedSession.Character.X);
        Assert.Equal(expectedY, (uint)result.UpdatedSession.Character.Y);
        Assert.Contains($"source={sourceMethod} map={expectedMapId} x={expectedX} y={expectedY}", result.Log);
    }

    [Fact]
    public async Task UnionWarRewardPaysTunedConquerPointsToWinningEmperorAndLogsSourceEvidence()
    {
        var result = await InvokeHandlerAsync(
            UnionWarNpcId,
            optionId: 2,
            CreateUnionWarRewardSession(unionId: WinningUnionId, unionRank: "Emperor"));

        Assert.True(result.Handled);
        Assert.Equal(TunedUnionWarConquerPointsReward, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(TunedUnionWarMoneyReward, result.UpdatedSession.Character.Money);
        Assert.Contains(result.Sent, frame => ContainsWalletUpdate(
            frame,
            result.UpdatedSession.Character.Uid,
            (ulong)TunedUnionWarConquerPointsReward));
        Assert.Contains(result.Sent, frame => ContainsMoneyUpdate(
            frame,
            result.UpdatedSession.Character.Uid,
            (ulong)TunedUnionWarMoneyReward));
        Assert.Contains("source=NpcHandler.UnionWar option=2", result.Log);
        Assert.Contains($"sourceConquerPoints={LegacyUnionWarSourceConquerPointsReward}", result.Log);
        Assert.Contains($"tunedConquerPoints={TunedUnionWarConquerPointsReward}", result.Log);
        Assert.Contains($"tunedMoney={TunedUnionWarMoneyReward}", result.Log);
        Assert.Contains("NewSystem.PrizeAllTops.MsgUnionWar", result.Log);
    }

    [Fact]
    public async Task UnionWarRewardRecordsClaimSoSecondCallDoesNotPayAgain()
    {
        var first = await InvokeHandlerAsync(
            UnionWarNpcId,
            optionId: 2,
            CreateUnionWarRewardSession(unionId: WinningUnionId, unionRank: "Emperor"));

        var second = await InvokeHandlerAsync(UnionWarNpcId, optionId: 2, first.UpdatedSession);

        Assert.Equal(TunedUnionWarConquerPointsReward, first.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(TunedUnionWarConquerPointsReward, second.UpdatedSession.Character.ConquerPoints);
        Assert.Contains(
            second.UpdatedSession.Character.Attributes,
            attribute => attribute.Key.Contains("UnionWar", StringComparison.OrdinalIgnoreCase) &&
                attribute.Key.Contains("Claim", StringComparison.OrdinalIgnoreCase) &&
                bool.TryParse(attribute.Value, out var claimed) &&
                claimed);
        Assert.DoesNotContain(second.Sent, frame => ContainsWalletUpdate(
            frame,
            second.UpdatedSession.Character.Uid,
            (ulong)(TunedUnionWarConquerPointsReward * 2)));
        Assert.Contains("source=NpcHandler.UnionWar option=2", second.Log);
        Assert.Contains("already claimed", second.Log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnionWarRewardPersistsWalletAndClaimState()
    {
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-unionwar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        try
        {
            var userPath = Path.Combine(usersRoot, "100.ini");
            await File.WriteAllTextAsync(
                userPath,
                """
                [Character]
                Name=WarHero
                UID=1293600
                ConquerPoints=0
                Money=0
                """);
            var session = CreateUnionWarRewardSession(unionId: WinningUnionId, unionRank: "Emperor") with
            {
                UsersRoot = usersRoot,
            };

            var result = await InvokeHandlerAsync(UnionWarNpcId, optionId: 2, session);

            var persisted = await File.ReadAllTextAsync(userPath);
            Assert.Equal(TunedUnionWarConquerPointsReward, result.UpdatedSession.Character.ConquerPoints);
            Assert.Contains($"ConquerPoints={TunedUnionWarConquerPointsReward}", persisted);
            Assert.Contains($"Money={TunedUnionWarMoneyReward}", persisted);
            Assert.Contains("LegacyUnionWarRewardClaimed=true", persisted);
            Assert.Contains("WorldServer persisted LegacyNpc UnionWar reward", result.Log);
        }
        finally
        {
            Directory.Delete(usersRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("DeputyLeader", WinningUnionId)]
    [InlineData("Emperor", OtherUnionId)]
    public async Task UnionWarRewardRejectsNonEmperorOrWrongUnionWithoutConquerPoints(
        string unionRank,
        ulong unionId)
    {
        var result = await InvokeHandlerAsync(
            UnionWarNpcId,
            optionId: 2,
            CreateUnionWarRewardSession(unionId: unionId, unionRank: unionRank));

        Assert.True(result.Handled);
        Assert.Equal(0, result.UpdatedSession.Character.ConquerPoints);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => !string.IsNullOrWhiteSpace(reply.Text));
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.UnionWar option=2", result.Log);
    }

    [Fact]
    public async Task EliteGuildWarRewardBranchSendsSourceAuditedNoRewardDialogWithoutParityGap()
    {
        var result = await InvokeHandlerAsync(EliteGuildWarNpcId, optionId: 2);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "No reward is available from this NPC in the current runtime state.");
        Assert.Equal(0, result.UpdatedSession.Character.ConquerPoints);
        Assert.Contains("WorldServer handled source-audited LegacyNpc UnionEliteWarEvent no reward", result.Log);
        Assert.Contains("source=NpcHandler.EliteGuildWar option=2", result.Log);
        Assert.Contains("Claim~reward.[EliteGuildWar]", result.Log);
        Assert.Contains("no case 2 reward body", result.Log);
        Assert.Contains("PrizeAllTops.EliteGuildWar runtime usage", result.Log);
        Assert.DoesNotContain("25,000,000 CP mail reward", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc UnionEliteWarEvent reward unavailable", result.Log);
    }

    [Fact]
    public async Task TrepilWarRewardPaysTunedSourceBackedRewardToWinningGuildLeader()
    {
        var result = await InvokeHandlerAsync(
            TrepilWarNpcId,
            optionId: 2,
            CreateTrepilWarRewardSession(guildId: WinningGuildId, guildRank: "GuildLeader", conquerPoints: 50));

        Assert.True(result.Handled);
        Assert.Equal(50 + TunedTrepilWarConquerPointsReward, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal("500", result.UpdatedSession.Character.Attributes["ClassExperience"]);
        Assert.Equal("10000#0#0#", result.UpdatedSession.Character.Attributes["SubProfInfo"]);
        Assert.True(result.UpdatedSession.Character.Attributes.ContainsKey("LegacyTrepilWarRewardClaimed"));
        Assert.False(result.UpdatedSession.Character.Attributes.ContainsKey("TrepilWarRoundOwnerGuildID"));

        Assert.Contains(result.Sent, frame => ContainsWalletUpdate(
            frame,
            result.UpdatedSession.Character.Uid,
            (ulong)(50 + TunedTrepilWarConquerPointsReward)));
        Assert.Contains(result.Sent, frame => ContainsScalarUpdate(
            frame,
            result.UpdatedSession.Character.Uid,
            LegacyMsgUpdateDataType.ClassExperience,
            500));

        var rewardItems = ParseGameItems(result.Sent);
        Assert.Contains(rewardItems, item => item.ItemId == 3_009_003 && item.StackSize == 5);
        Assert.Contains(rewardItems, item => item.ItemId == 3_324_226 && item.StackSize == 2);
        Assert.Contains(rewardItems, item => item.ItemId == 4_050_001 && item.StackSize == 500);
        Assert.Contains(rewardItems, item => item.ItemId == 4_040_001 && item.StackSize == 500);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_009_003 && item.StackSize == 5);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_324_226 && item.StackSize == 2);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 4_050_001 && item.StackSize == 500);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 4_040_001 && item.StackSize == 500);

        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("You claimed 300000 CPs", StringComparison.Ordinal));
        Assert.Contains("WorldServer handled LegacyNpc TrepilWar reward", result.Log);
        Assert.Contains("source=NpcHandler.MsgTrepilWar option=2", result.Log);
        Assert.Contains($"sourceConquerPoints={LegacyTrepilWarSourceConquerPointsReward}", result.Log);
        Assert.Contains($"tunedConquerPoints={TunedTrepilWarConquerPointsReward}", result.Log);
        Assert.Contains("items=3009003x5,3324226x2,4050001x500,4040001x500", result.Log);
        Assert.Contains("studyPoints=10000", result.Log);
        Assert.Contains("innerPowerPotency=10000", result.Log);
        Assert.Contains("references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgNpc\\NpcHandler.cs MsgTrepilWar case 2", result.Log);
        Assert.Contains("WorldServer deferred LegacyNpc TrepilWar InnerPower potency reward", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc UnionEliteWarEvent reward unavailable", result.Log);
    }

    [Fact]
    public async Task TrepilWarRewardRecordsClaimSoSecondCallDoesNotDoublePay()
    {
        var first = await InvokeHandlerAsync(
            TrepilWarNpcId,
            optionId: 2,
            CreateTrepilWarRewardSession(guildId: WinningGuildId, guildRank: "GuildLeader"));

        var second = await InvokeHandlerAsync(TrepilWarNpcId, optionId: 2, first.UpdatedSession);

        Assert.Equal(TunedTrepilWarConquerPointsReward, first.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(TunedTrepilWarConquerPointsReward, second.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(4, first.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.Equal(4, second.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.Empty(ParseGameItems(second.Sent));
        Assert.Contains("already claimed", second.Log, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("GuildMember", WinningGuildId, "Only the guild leader can claim the reward.")]
    [InlineData("GuildLeader", OtherGuildId, "You do not have any reward to claim.")]
    public async Task TrepilWarRewardRejectsNonLeaderOrWrongGuildWithoutPayment(
        string guildRank,
        ulong guildId,
        string expectedDialog)
    {
        var result = await InvokeHandlerAsync(
            TrepilWarNpcId,
            optionId: 2,
            CreateTrepilWarRewardSession(guildId: guildId, guildRank: guildRank));

        Assert.True(result.Handled);
        Assert.Equal(0, result.UpdatedSession.Character.ConquerPoints);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Empty(ParseGameItems(result.Sent));
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == expectedDialog);
        Assert.Contains("WorldServer skipped LegacyNpc TrepilWar reward", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc UnionEliteWarEvent reward unavailable", result.Log);
    }

    private static async Task<UnionEliteWarEventInvokeResult> InvokeHandlerAsync(
        uint npcId,
        byte optionId,
        WorldSession? customSession = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = customSession ?? CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);

        var handled = await WorldPacketHandlers.TryHandleLegacyUnionEliteWarEventNpcAsync(
            context,
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new UnionEliteWarEventInvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 100,
            Username: "union-elite-war-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "WarHero",
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
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static WorldSession CreateUnionWarRewardSession(ulong unionId, string unionRank)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyMsgUnionWarProces"] = "Dead",
            ["MsgUnionWarProces"] = "Dead",
            ["LegacyMsgUnionWarProcess"] = "Dead",
            ["MsgUnionWarProcess"] = "Dead",
            ["LegacyUnionWarWinnerUnionID"] = WinningUnionId.ToString(),
            ["LegacyUnionWarWinnerUnionId"] = WinningUnionId.ToString(),
            ["UnionWarWinnerUnionID"] = WinningUnionId.ToString(),
            ["UnionWarWinnerUnionId"] = WinningUnionId.ToString(),
            ["LegacyUnionWarWinnerUnionName"] = "SourceWinners",
            ["UnionWarWinnerUnionName"] = "SourceWinners",
            ["UnionID"] = unionId.ToString(),
            ["UnionId"] = unionId.ToString(),
            ["UnionMemberUnionID"] = unionId.ToString(),
            ["UnionMemberUnionId"] = unionId.ToString(),
            ["UnionMemeberUnionID"] = unionId.ToString(),
            ["UnionMemeberUnionId"] = unionId.ToString(),
            ["UnionMemberRank"] = unionRank,
            ["UnionMemeberRank"] = unionRank,
        };

        var session = CreateSession();
        return session with
        {
            Character = session.Character with
            {
                Attributes = attributes,
            },
        };
    }

    private static WorldSession CreateTrepilWarRewardSession(
        ulong guildId,
        string guildRank,
        long conquerPoints = 0)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyMsgTrepilWarProcess"] = "Dead",
            ["MsgTrepilWarProcess"] = "Dead",
            ["LegacyMsgTrepilWarProces"] = "Dead",
            ["MsgTrepilWarProces"] = "Dead",
            ["LegacyMsgTrepilWarRoundOwnerGuildID"] = WinningGuildId.ToString(),
            ["LegacyMsgTrepilWarRoundOwnerGuildId"] = WinningGuildId.ToString(),
            ["MsgTrepilWarRoundOwnerGuildID"] = WinningGuildId.ToString(),
            ["MsgTrepilWarRoundOwnerGuildId"] = WinningGuildId.ToString(),
            ["LegacyTrepilWarRoundOwnerGuildID"] = WinningGuildId.ToString(),
            ["LegacyTrepilWarRoundOwnerGuildId"] = WinningGuildId.ToString(),
            ["TrepilWarRoundOwnerGuildID"] = WinningGuildId.ToString(),
            ["TrepilWarRoundOwnerGuildId"] = WinningGuildId.ToString(),
            ["LegacyMsgTrepilWarRoundOwnerGuildName"] = "SourceGuild",
            ["MsgTrepilWarRoundOwnerGuildName"] = "SourceGuild",
            ["LegacyTrepilWarRoundOwnerGuildName"] = "SourceGuild",
            ["TrepilWarRoundOwnerGuildName"] = "SourceGuild",
            ["LegacyMsgTrepilWarRoundOwnerScore"] = "123456",
            ["MsgTrepilWarRoundOwnerScore"] = "123456",
            ["LegacyTrepilWarRoundOwnerScore"] = "123456",
            ["TrepilWarRoundOwnerScore"] = "123456",
            ["GuildID"] = guildId.ToString(),
            ["GuildId"] = guildId.ToString(),
            ["MyGuildID"] = guildId.ToString(),
            ["MyGuildId"] = guildId.ToString(),
            ["GuildRank"] = guildRank,
            ["GuildMemberRank"] = guildRank,
            ["ClassExperience"] = "0",
        };

        var session = CreateSession();
        return session with
        {
            Character = session.Character with
            {
                ConquerPoints = conquerPoints,
                Attributes = attributes,
            },
        };
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

    private static bool ContainsWalletUpdate(byte[] bytes, long expectedUid, ulong expectedConquerPoints)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Uid == (uint)expectedUid &&
            packet.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                update.Value == expectedConquerPoints);
    }

    private static bool ContainsMoneyUpdate(byte[] bytes, long expectedUid, ulong expectedMoney)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Uid == (uint)expectedUid &&
            packet.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.Money &&
                update.Value == expectedMoney);
    }

    private static bool ContainsScalarUpdate(
        byte[] bytes,
        long expectedUid,
        LegacyMsgUpdateDataType expectedType,
        ulong expectedValue)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Uid == (uint)expectedUid &&
            packet.Updates.Any(update =>
                update.Type == expectedType &&
                update.Value == expectedValue);
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

    private static void AssertLegacyNpcTeleportTransition(
        IReadOnlyList<byte[]> sent,
        WorldSession session,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        var expectedClientMapId = (uint)LegacyMapAliasResolver.ResolveTerrainSourceMapId((int)expectedMapId);

        Assert.Equal(5, sent.Count);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var action));
        Assert.Equal((uint)session.Character.Uid, action.ObjId);
        Assert.Equal(expectedClientMapId, action.DwParam);
        Assert.Equal(MsgActionType.Teleport, action.Type);
        Assert.Equal(expectedX, action.PositionX);
        Assert.Equal(expectedY, action.PositionY);
        Assert.Equal((long)expectedClientMapId, action.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[1]), out var merchantStartup));
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal((long)expectedMapId, merchantStartup.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[2]), out var stopVending));
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(expectedMapId, stopVending.DwParam);
        Assert.Equal((long)expectedMapId, stopVending.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[3]), out var color));
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(0U, color.DwParam);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[4]), out _));
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

    private sealed record UnionEliteWarEventInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession Session,
        WorldSession UpdatedSession);
}
