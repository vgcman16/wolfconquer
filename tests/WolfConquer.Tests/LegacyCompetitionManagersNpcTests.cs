using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyCompetitionManagersNpcTests
{
    private const uint ArenaManagerWangNpcId = 1_881;
    private const uint MissConquerNpcId = 60_112;
    private const uint MonthlyPkNpcId = 2_316;
    private const uint FreezeWarNpcId = 52_004;
    private const uint SuperGuildWarNpcId = 25_015;
    private const uint CaptureTheFlagNpcId = 22_234;
    private const uint ArenaNpcId = 10_021;
    private const uint SkillTeamPkManagerNpcId = 8_592;
    private const uint ExitElitePkNpcId = 168_052;
    private const int FreezeWarStatusFlag = 0x3B;
    private const string RuntimeLegacyAttackStampUtcAttribute = "LegacyAttackStampUtc";
    private const string RuntimeLegacyProtectExpiresAtUtcAttribute = "LegacyProtectExpiresAtUtc";

    [Fact]
    public async Task ArenaManagerRootAndRefineryPagesUseTranslatedSourceOptions()
    {
        var root = await InvokeCompetitionManagerAsync(ArenaManagerWangNpcId, optionId: 0);

        Assert.True(root.Handled);
        var replies = ParseNpcReplies(root.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("Power Arena runs", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.Text == "Enter Arena." && reply.OptionId == 30);
        Assert.Contains(replies, reply => reply.Text == "What are the rules?" && reply.OptionId == 20);
        Assert.Contains(replies, reply => reply.Text == "Exchange for refinery packs." && reply.OptionId == 10);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));

        var superior = await InvokeCompetitionManagerAsync(ArenaManagerWangNpcId, optionId: 17);

        Assert.True(superior.Handled);
        replies = ParseNpcReplies(superior.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("12 Refinery Badges", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.Text == "Veneer - Critical Strike." && reply.OptionId == 140);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task GenderAndMonthlyPkEntriesTeleportDuringLegacyWindows()
    {
        var miss = await InvokeCompetitionManagerAsync(
            MissConquerNpcId,
            optionId: 1,
            now: new DateTime(2026, 6, 20, 6, 5, 0),
            body: 2007);

        Assert.True(miss.Handled);
        Assert.Equal(6_011, miss.UpdatedSession.Character.MapId);
        Assert.Equal(70, miss.UpdatedSession.Character.X);
        Assert.Equal(70, miss.UpdatedSession.Character.Y);
        Assert.Contains(ParseMsgActions(miss.Sent), action => action.Type == MsgActionType.Teleport);

        var monthly = await InvokeCompetitionManagerAsync(
            MonthlyPkNpcId,
            optionId: 1,
            now: new DateTime(2026, 6, 20, 9, 5, 0));

        Assert.True(monthly.Handled);
        Assert.Equal(3_978, monthly.UpdatedSession.Character.MapId);
        Assert.Equal(165, monthly.UpdatedSession.Character.X);
        Assert.Equal(200, monthly.UpdatedSession.Character.Y);
        Assert.Contains(ParseMsgActions(monthly.Sent), action => action.Type == MsgActionType.Teleport);
    }

    [Fact]
    public async Task GenderPkRejectsWrongBodyWithEnglishDialog()
    {
        var result = await InvokeCompetitionManagerAsync(
            MissConquerNpcId,
            optionId: 1,
            now: new DateTime(2026, 6, 20, 6, 5, 0),
            body: 1003);

        Assert.True(result.Handled);
        Assert.Equal(1_002, result.UpdatedSession.Character.MapId);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Sorry, this event is only for female characters.");
        Assert.DoesNotContain(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
    }

    [Fact]
    public async Task ArenaNpcChargesFeeUpdatesMoneyAndTeleports()
    {
        var result = await InvokeCompetitionManagerAsync(ArenaNpcId, optionId: 1, money: 100);

        Assert.True(result.Handled);
        Assert.Equal(50, result.UpdatedSession.Character.Money);
        Assert.Equal(1_005, result.UpdatedSession.Character.MapId);
        Assert.Equal(51, result.UpdatedSession.Character.X);
        Assert.Equal(69, result.UpdatedSession.Character.Y);
        var update = Assert.Single(ParseMsgUpdates(result.Sent), packet => packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Money));
        Assert.Contains(update.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Money && entry.Value == 50);
        Assert.Contains(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
    }

    [Fact]
    public async Task SuperGuildWarEntryTeleportsDuringSourceAliveWindowWithAttackProtection()
    {
        var result = await InvokeCompetitionManagerAsync(
            SuperGuildWarNpcId,
            optionId: 6,
            now: new DateTime(2026, 6, 23, 21, 5, 0));

        Assert.True(result.Handled);
        Assert.Equal(3_868, result.UpdatedSession.Character.MapId);
        Assert.Equal(232, result.UpdatedSession.Character.X);
        Assert.Equal(385, result.UpdatedSession.Character.Y);
        Assert.Contains(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.True(result.UpdatedSession.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
        Assert.True(result.UpdatedSession.Character.Attributes.ContainsKey(RuntimeLegacyProtectExpiresAtUtcAttribute));
        Assert.Contains("source=NpcHandler.SuperGuildWar", result.Log);
        Assert.DoesNotContain("Super Guild War entry is not available", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc CompetitionManagers", result.Log);
    }

    [Fact]
    public async Task SuperGuildWarClosedEntryStaysOfflineAndRewardBranchHandlesMissingWinnerState()
    {
        var closed = await InvokeCompetitionManagerAsync(
            SuperGuildWarNpcId,
            optionId: 6,
            now: new DateTime(2026, 6, 23, 22, 0, 0));

        Assert.True(closed.Handled);
        Assert.Equal(1_002, closed.UpdatedSession.Character.MapId);
        Assert.DoesNotContain(ParseMsgActions(closed.Sent), action => action.Type == MsgActionType.Teleport);
        var replies = ParseNpcReplies(closed.Sent);
        Assert.Contains(replies, reply => reply.Text == "Super Guild War has already ended.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));

        var reward = await InvokeCompetitionManagerAsync(
            SuperGuildWarNpcId,
            optionId: 7,
            now: new DateTime(2026, 6, 23, 22, 5, 0));

        Assert.True(reward.Handled);
        Assert.DoesNotContain(ParseMsgActions(reward.Sent), action => action.Type == MsgActionType.Teleport);
        replies = ParseNpcReplies(reward.Sent);
        Assert.Contains(replies, reply => reply.Text == "The tournament is offline at the moment.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.SuperGuildWar", reward.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc CompetitionManagers", reward.Log);
    }

    [Fact]
    public async Task FreezeWarIdleSignupAssignsBalancedTeamsAppliesGarmentFreezeAndTeleports()
    {
        var freezeWarStates = new LegacyFreezeWarRuntimeStateStore();
        Assert.NotNull(freezeWarStates.Open(new DateTimeOffset(2026, 6, 20, 12, 55, 0, TimeSpan.Zero)));

        var first = await InvokeCompetitionManagerAsync(
            FreezeWarNpcId,
            optionId: 1,
            freezeWarStates: freezeWarStates,
            uid: 1_293_600,
            name: "FirstFreeze",
            playerItems: CreateFreezeWarEligibleItems(),
            playerSpells: CreateFreezeWarEligibleSpells());

        var second = await InvokeCompetitionManagerAsync(
            FreezeWarNpcId,
            optionId: 1,
            freezeWarStates: freezeWarStates,
            uid: 1_293_601,
            name: "SecondFreeze",
            playerItems: CreateFreezeWarEligibleItems(),
            playerSpells: CreateFreezeWarEligibleSpells());

        Assert.True(first.Handled);
        Assert.True(second.Handled);
        Assert.Equal(1_505, first.UpdatedSession.Character.MapId);
        Assert.Equal(165, first.UpdatedSession.Character.X);
        Assert.Equal(245, first.UpdatedSession.Character.Y);
        Assert.Equal("2", first.UpdatedSession.Character.Attributes["FreezeTeamType"]);
        Assert.Equal("Red", first.UpdatedSession.Character.Attributes["LegacyFreezeWarTeam"]);
        Assert.Equal("185625", first.UpdatedSession.Character.Attributes["SpecialGarment"]);
        Assert.Equal("FreezeWar", first.UpdatedSession.Character.Attributes["LegacyTournamentType"]);
        Assert.Equal("Idle", first.UpdatedSession.Character.Attributes["LegacyTournamentProcess"]);
        Assert.True(HasLegacyStatusFlag(first.UpdatedSession.Character.Attributes["StatusFlags"], FreezeWarStatusFlag));

        Assert.Equal(1_505, second.UpdatedSession.Character.MapId);
        Assert.Equal(153, second.UpdatedSession.Character.X);
        Assert.Equal(105, second.UpdatedSession.Character.Y);
        Assert.Equal("1", second.UpdatedSession.Character.Attributes["FreezeTeamType"]);
        Assert.Equal("Blue", second.UpdatedSession.Character.Attributes["LegacyFreezeWarTeam"]);
        Assert.Equal("185825", second.UpdatedSession.Character.Attributes["SpecialGarment"]);
        Assert.Equal(
            first.UpdatedSession.Character.Attributes["DynamicID"],
            second.UpdatedSession.Character.Attributes["DynamicID"]);
        Assert.True(HasLegacyStatusFlag(second.UpdatedSession.Character.Attributes["StatusFlags"], FreezeWarStatusFlag));

        Assert.Contains(ParseMsgActions(first.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Contains(ParseMsgActions(second.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Contains("source=NpcHandler.FreezeWar option=1 team=Red", first.Log);
        Assert.Contains("source=NpcHandler.FreezeWar option=1 team=Blue", second.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc CompetitionManagers", first.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc CompetitionManagers", second.Log);
    }

    [Fact]
    public async Task FreezeWarSignupOutsideIdleStateKeepsSourceClosedDialog()
    {
        var result = await InvokeCompetitionManagerAsync(FreezeWarNpcId, optionId: 1);

        Assert.True(result.Handled);
        Assert.Equal(1_002, result.UpdatedSession.Character.MapId);
        Assert.DoesNotContain(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.Text == "We are sorry, but you can't enter right now. Try when the tournament starts.");
    }

    [Fact]
    public async Task FreezeWarMissingSpellDialogPrecedesTeleportPreflight()
    {
        var freezeWarStates = new LegacyFreezeWarRuntimeStateStore();
        Assert.NotNull(freezeWarStates.Open(new DateTimeOffset(2026, 6, 20, 12, 55, 0, TimeSpan.Zero)));

        var result = await InvokeCompetitionManagerAsync(
            FreezeWarNpcId,
            optionId: 1,
            freezeWarStates: freezeWarStates,
            mapId: 6_001,
            playerItems: CreateFreezeWarEligibleItems());

        Assert.True(result.Handled);
        Assert.Equal(6_001, result.UpdatedSession.Character.MapId);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.Text == "Please learn FastBlade, ScentSword, or DragonSlash before entering.");
        Assert.DoesNotContain("denied legacy GameClient.Teleport preflight", result.Log);
    }

    [Fact]
    public async Task ExitElitePkTeleportsToTwinCityAndLogsUnmodeledFlagWork()
    {
        var confirm = await InvokeCompetitionManagerAsync(ExitElitePkNpcId, optionId: 2, mapId: 7_879, x: 45, y: 45);

        Assert.True(confirm.Handled);
        Assert.Contains(ParseNpcReplies(confirm.Sent), reply => reply.Text == "Are you sure you want to leave for Twin City?");

        var exit = await InvokeCompetitionManagerAsync(ExitElitePkNpcId, optionId: 3, mapId: 7_879, x: 45, y: 45);

        Assert.True(exit.Handled);
        Assert.Equal(1_002, exit.UpdatedSession.Character.MapId);
        Assert.Equal(410, exit.UpdatedSession.Character.X);
        Assert.Equal(354, exit.UpdatedSession.Character.Y);
        Assert.Contains(ParseMsgActions(exit.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Contains("PARITY-GAP LegacyNpc CompetitionManagers", exit.Log);
        Assert.Contains("InElitePk/InTeamPk", exit.Log);
    }

    [Fact]
    public async Task DynamicSchedulerBranchesSendUnavailableDialogAndParityGap()
    {
        var ctf = await InvokeCompetitionManagerAsync(CaptureTheFlagNpcId, optionId: 5);

        Assert.True(ctf.Handled);
        Assert.Contains(ParseNpcReplies(ctf.Sent), reply => reply.Text == "Capture the Flag sign-up is not available from this runtime yet.");
        Assert.Contains("PARITY-GAP LegacyNpc CompetitionManagers", ctf.Log);
        Assert.Contains("MsgSchedules.CaptureTheFlag.Join", ctf.Log);

        var skillTeam = await InvokeCompetitionManagerAsync(SkillTeamPkManagerNpcId, optionId: 3);

        Assert.True(skillTeam.Handled);
        Assert.Contains(ParseNpcReplies(skillTeam.Sent), reply => reply.Text == "Skill Team PK sign-up is not available from this runtime yet.");
        Assert.Contains("MsgSchedules.SkillTeamPkTournament.SignUp", skillTeam.Log);
    }

    private static async Task<CompetitionManagerInvokeResult> InvokeCompetitionManagerAsync(
        uint npcId,
        byte optionId,
        DateTime? now = null,
        long body = 1003,
        long money = 0,
        long mapId = 1_002,
        long x = 430,
        long y = 378,
        long uid = 1_293_600,
        string name = "CompetitionHero",
        LegacyFreezeWarRuntimeStateStore? freezeWarStates = null,
        PlayerItemSnapshot? playerItems = null,
        PlayerSpellSnapshot? playerSpells = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(body, money, mapId, x, y, uid, name, playerItems, playerSpells);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyCompetitionManagersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                FreezeWarStates: freezeWarStates),
            session,
            npcId,
            optionId,
            now ?? new DateTime(2026, 6, 20, 12, 0, 0),
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new CompetitionManagerInvokeResult(handled, sent, log.ToString(), updatedSession);
    }

    private static WorldSession CreateSession(
        long body,
        long money,
        long mapId,
        long x,
        long y,
        long uid,
        string name,
        PlayerItemSnapshot? playerItems,
        PlayerSpellSnapshot? playerSpells)
    {
        return new WorldSession(
            AccountId: uid,
            Username: "competition-manager-test",
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: body,
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
                Money: money,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DynamicID"] = "0",
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpells: playerSpells ?? PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshot CreateFreezeWarEligibleItems()
    {
        return new PlayerItemSnapshot(
            [],
            [],
            [],
            [new PlayerItemSnapshotItem { Uid = 400_001, ItemId = 410_003, Position = 4 }],
            []);
    }

    private static PlayerSpellSnapshot CreateFreezeWarEligibleSpells()
    {
        return new PlayerSpellSnapshot([new PlayerSpellSnapshotEntry(1_045, 0, 0, 0, 0, 0)]);
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

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }

    private static bool HasLegacyStatusFlag(string raw, int flag)
    {
        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > wordIndex &&
            ulong.TryParse(parts[wordIndex], out var word) &&
            (word & (1UL << bitIndex)) != 0;
    }

    private sealed record CompetitionManagerInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession);
}
