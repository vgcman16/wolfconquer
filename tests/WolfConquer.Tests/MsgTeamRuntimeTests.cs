using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgTeamRuntimeTests : IDisposable
{
    private const uint ReturnedItemsWarehouseId = 65_535;

    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-msg-team-runtime-" + Guid.NewGuid());
    private readonly string _usersRoot;

    public MsgTeamRuntimeTests()
    {
        _usersRoot = Path.Combine(_dbRoot, "Users");
    }

    [Fact]
    public async Task TeamLeaderInviteRequestForwardsLegacyInviteToVisibleTarget()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        WriteSnapshot(accountId: 400002, uid: 400002, name: "Watcher", x: 832, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff);
        var leaderSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var leader = CreateSendingConnection(leaderSent);
        var target = CreateSendingConnection(targetSent);

        await LoginLegacyWorldAsync(handler, handoff, leader, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, target, accountId: 400001);
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        var targetCountBeforeInvite = targetSent.Count;

        await handler.HandleFrameAsync(
            leader,
            ReadFrame(new MsgTeamPacket(MsgTeamType.InviteRequest, 400001).ToFrameBytes()));

        Assert.Equal(targetCountBeforeInvite + 2, targetSent.Count);
        AssertTeamRelation(
            targetSent[targetCountBeforeInvite],
            expectedRequesterUid: 1_293_600,
            expectedReceiverUid: 400_001,
            expectedReceiverName: "Target");
        Assert.True(MsgTeamPacket.TryParse(ReadFrame(targetSent[targetCountBeforeInvite + 1]), out var invite));
        Assert.Equal(MsgTeamType.InviteRequest, invite.Type);
        Assert.Equal(1_293_600U, invite.Uid);
    }

    [Fact]
    public async Task AcceptInvitationAddsTargetToLeaderTeamAndBroadcastsMemberInfo()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff);
        var leaderSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var leader = CreateSendingConnection(leaderSent);
        var target = CreateSendingConnection(targetSent);

        await LoginLegacyWorldAsync(handler, handoff, leader, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, target, accountId: 400001);
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.InviteRequest, 400001).ToFrameBytes()));
        var leaderBeforeAccept = leaderSent.Count;
        var targetBeforeAccept = targetSent.Count;

        await handler.HandleFrameAsync(
            target,
            ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, 1293600).ToFrameBytes()));

        Assert.Equal(targetBeforeAccept + 5, targetSent.Count);
        Assert.Equal(leaderBeforeAccept + 1, leaderSent.Count);
        AssertTeamAddFrames(targetSent, targetBeforeAccept, expectedMemberUid: 400001, expectedLeaderUid: 1293600);
        AssertTeamMemberInfo(leaderSent[leaderBeforeAccept]);
        AssertTeamMemberInfo(targetSent[targetBeforeAccept + 4]);
    }

    [Fact]
    public async Task VipTeamTeleportSendsConfirmationToTeammatesBeforeLeaderTeleport()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff);
        var leaderSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var leader = CreateSendingConnection(leaderSent);
        var target = CreateSendingConnection(targetSent);

        await LoginLegacyWorldAsync(handler, handoff, leader, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, target, accountId: 400001);
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.InviteRequest, 400001).ToFrameBytes()));
        await handler.HandleFrameAsync(target, ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, 1293600).ToFrameBytes()));
        var leaderBeforeTeleport = leaderSent.Count;
        var targetBeforeTeleport = targetSent.Count;

        await handler.HandleFrameAsync(
            leader,
            ReadFrame(new LegacyVipHandlerPacket(
                LegacyVipTeleportType.TeamTeleport,
                LegacyVipTeleportLocation.DCSquare,
                Countdown: 0,
                Name: "").ToFrameBytes()));

        Assert.Equal(targetBeforeTeleport + 2, targetSent.Count);
        Assert.True(LegacyVipHandlerPacket.TryParse(ReadFrame(targetSent[targetBeforeTeleport]), out var confirmation));
        Assert.Equal(LegacyVipTeleportType.TeammateConfirmation, confirmation.TeleportType);
        Assert.Equal(LegacyVipTeleportLocation.DCSquare, confirmation.Location);
        Assert.Equal(15U, confirmation.Countdown);
        Assert.Equal("HeroOne", confirmation.Name);
        Assert.True(LegacyRemoveEntityPacket.TryParse(ReadFrame(targetSent[targetBeforeTeleport + 1]), out var remove));
        Assert.Equal(1_293_600U, remove.Uid);

        Assert.Equal(leaderBeforeTeleport + 5, leaderSent.Count);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(leaderSent[leaderBeforeTeleport]), out var action));
        Assert.Equal(1_293_600U, action.ObjId);
        Assert.Equal(1000U, action.DwParam);
        Assert.Equal(1000U, action.DwParam3);
        Assert.Equal(MsgActionType.Teleport, action.Type);
        Assert.Equal(502U, action.PositionX);
        Assert.Equal(655U, action.PositionY);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(leaderSent[leaderBeforeTeleport + 1]), out var merchantStartup));
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal(1000U, merchantStartup.DwParam3);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(leaderSent[leaderBeforeTeleport + 2]), out var stopVending));
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(1000U, stopVending.DwParam);
        Assert.Equal(1000U, stopVending.DwParam3);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(leaderSent[leaderBeforeTeleport + 3]), out var mapColor));
        Assert.Equal(MsgActionType.LegacyColor, mapColor.Type);
        Assert.Equal(0U, mapColor.DwParam);
        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(leaderSent[leaderBeforeTeleport + 4]), out var mapStatus));
        Assert.Equal(new LegacyMapStatusPacket(1000, 1000, Status: 0), mapStatus);
    }

    [Fact]
    public async Task AcceptJoinRequestAddsRequesterToLeaderTeamAndBroadcastsMemberInfo()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff);
        var leaderSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var leader = CreateSendingConnection(leaderSent);
        var target = CreateSendingConnection(targetSent);

        await LoginLegacyWorldAsync(handler, handoff, leader, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, target, accountId: 400001);
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(target, ReadFrame(new MsgTeamPacket(MsgTeamType.JoinRequest, 1293600).ToFrameBytes()));
        AssertTeamRelation(
            leaderSent[^2],
            expectedRequesterUid: 400_001,
            expectedReceiverUid: 1_293_600,
            expectedReceiverName: "HeroOne");
        Assert.True(MsgTeamPacket.TryParse(ReadFrame(leaderSent[^1]), out var joinRequest));
        Assert.Equal(MsgTeamType.JoinRequest, joinRequest.Type);
        Assert.Equal(400001U, joinRequest.Uid);
        var leaderBeforeAccept = leaderSent.Count;
        var targetBeforeAccept = targetSent.Count;

        await handler.HandleFrameAsync(
            leader,
            ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptJoinRequest, 400001).ToFrameBytes()));

        Assert.Equal(targetBeforeAccept + 5, targetSent.Count);
        Assert.Equal(leaderBeforeAccept + 1, leaderSent.Count);
        AssertTeamAddFrames(targetSent, targetBeforeAccept, expectedMemberUid: 400001, expectedLeaderUid: 1293600);
        AssertTeamMemberInfo(leaderSent[leaderBeforeAccept]);
        AssertTeamMemberInfo(targetSent[targetBeforeAccept + 4]);
    }

    [Fact]
    public async Task ExitTeamBroadcastsExitAndClearsMemberTeam()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff);
        var leaderSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var leader = CreateSendingConnection(leaderSent);
        var target = CreateSendingConnection(targetSent);

        await LoginLegacyWorldAsync(handler, handoff, leader, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, target, accountId: 400001);
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.InviteRequest, 400001).ToFrameBytes()));
        await handler.HandleFrameAsync(target, ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, 1293600).ToFrameBytes()));
        var leaderBeforeExit = leaderSent.Count;
        var targetBeforeExit = targetSent.Count;

        await handler.HandleFrameAsync(target, ReadFrame(new MsgTeamPacket(MsgTeamType.ExitTeam, 0).ToFrameBytes()));

        Assert.Equal(leaderBeforeExit + 1, leaderSent.Count);
        Assert.Equal(targetBeforeExit + 3, targetSent.Count);
        AssertTeamPacket(leaderSent[leaderBeforeExit], MsgTeamType.ExitTeam, expectedUid: 400001);
        AssertTeamPacket(targetSent[targetBeforeExit], MsgTeamType.ExitTeam, expectedUid: 400001);
        AssertTeamRemovalCleanup(targetSent, targetBeforeExit + 1, expectedRemovedUid: 400001, expectedRemainingCount: 1);
    }

    [Fact]
    public async Task KickBroadcastsKickAndClearsTargetTeam()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff);
        var leaderSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var leader = CreateSendingConnection(leaderSent);
        var target = CreateSendingConnection(targetSent);

        await LoginLegacyWorldAsync(handler, handoff, leader, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, target, accountId: 400001);
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.InviteRequest, 400001).ToFrameBytes()));
        await handler.HandleFrameAsync(target, ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, 1293600).ToFrameBytes()));
        var leaderBeforeKick = leaderSent.Count;
        var targetBeforeKick = targetSent.Count;

        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.Kick, 400001).ToFrameBytes()));

        Assert.Equal(leaderBeforeKick + 1, leaderSent.Count);
        Assert.Equal(targetBeforeKick + 3, targetSent.Count);
        AssertTeamPacket(leaderSent[leaderBeforeKick], MsgTeamType.Kick, expectedUid: 400001);
        AssertTeamPacket(targetSent[targetBeforeKick], MsgTeamType.Kick, expectedUid: 400001);
        AssertTeamRemovalCleanup(targetSent, targetBeforeKick + 1, expectedRemovedUid: 400001, expectedRemainingCount: 1);
    }

    [Fact]
    public async Task DismissPromotesNextMemberForTeamArenaSignupGate()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        WriteSnapshot(accountId: 400002, uid: 400002, name: "Watcher", x: 832, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            ArenaStatisticsProvider.Empty,
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(1293600, "HeroOne", points: 4500),
                TeamArenaRecord(400001, "Target", points: 4500),
            ]));
        var leaderSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var leader = CreateSendingConnection(leaderSent);
        var target = CreateSendingConnection(targetSent);

        await LoginLegacyWorldAsync(handler, handoff, leader, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, target, accountId: 400001);
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.InviteRequest, 400001).ToFrameBytes()));
        await handler.HandleFrameAsync(target, ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, 1293600).ToFrameBytes()));
        var leaderBeforeDismiss = leaderSent.Count;
        var targetBeforeDismiss = targetSent.Count;

        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.Dismiss, 0).ToFrameBytes()));

        Assert.Equal(leaderBeforeDismiss + 3, leaderSent.Count);
        AssertTeamPacket(leaderSent[leaderBeforeDismiss], MsgTeamType.Kick, expectedUid: 1293600);
        AssertTeamRemovalCleanup(leaderSent, leaderBeforeDismiss + 1, expectedRemovedUid: 1293600, expectedRemainingCount: 1);
        Assert.Equal(targetBeforeDismiss + 3, targetSent.Count);
        AssertTeamPacket(targetSent[targetBeforeDismiss], MsgTeamType.Kick, expectedUid: 1293600);
        AssertSingleTeamMemberInfo(targetSent[targetBeforeDismiss + 1], expectedName: "Target", expectedUid: 400001);
        AssertTeamLeadership(
            targetSent[targetBeforeDismiss + 2],
            expectedUid: 400001,
            expectedLeaderUid: 400001,
            expectedCount: 1);
        var targetBeforeSignup = targetSent.Count;

        await handler.HandleFrameAsync(
            target,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));

        Assert.Equal(targetBeforeSignup + 2, targetSent.Count);
        Assert.True(LegacyTeamArenaSignupResponsePacket.TryParse(ReadFrame(targetSent[targetBeforeSignup]), out var response));
        Assert.Equal(400001U, response.CharacterUid);
        Assert.Equal("Target", response.CharacterName);
        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(targetSent[targetBeforeSignup + 1]), out var info));
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingForOpponent, info.Status);
        Assert.Equal(4500U, info.ArenaPoints);
    }

    [Fact]
    public async Task DismissPromotesFirstRemainingMemberAndRefreshesAllRemainingMembers()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        WriteSnapshot(accountId: 400002, uid: 400002, name: "Second", x: 832, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            ArenaStatisticsProvider.Empty,
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(400001, "Target", points: 4500),
                TeamArenaRecord(400002, "Second", points: 4500),
            ]));
        var leaderSent = new List<byte[]>();
        var firstSent = new List<byte[]>();
        var secondSent = new List<byte[]>();
        var leader = CreateSendingConnection(leaderSent);
        var first = CreateSendingConnection(firstSent);
        var second = CreateSendingConnection(secondSent);

        await LoginLegacyWorldAsync(handler, handoff, leader, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, first, accountId: 400001);
        await LoginLegacyWorldAsync(handler, handoff, second, accountId: 400002);
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.InviteRequest, 400001).ToFrameBytes()));
        await handler.HandleFrameAsync(first, ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, 1293600).ToFrameBytes()));
        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.InviteRequest, 400002).ToFrameBytes()));
        await handler.HandleFrameAsync(second, ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, 1293600).ToFrameBytes()));
        var leaderBeforeDismiss = leaderSent.Count;
        var firstBeforeDismiss = firstSent.Count;
        var secondBeforeDismiss = secondSent.Count;

        await handler.HandleFrameAsync(leader, ReadFrame(new MsgTeamPacket(MsgTeamType.Dismiss, 0).ToFrameBytes()));

        Assert.Equal(leaderBeforeDismiss + 3, leaderSent.Count);
        AssertTeamRemovalCleanup(leaderSent, leaderBeforeDismiss + 1, expectedRemovedUid: 1293600, expectedRemainingCount: 2);
        Assert.Equal(firstBeforeDismiss + 3, firstSent.Count);
        Assert.Equal(secondBeforeDismiss + 3, secondSent.Count);
        AssertTeamPacket(firstSent[firstBeforeDismiss], MsgTeamType.Kick, expectedUid: 1293600);
        AssertTeamPacket(secondSent[secondBeforeDismiss], MsgTeamType.Kick, expectedUid: 1293600);
        AssertTeamMemberInfoMembers(firstSent[firstBeforeDismiss + 1], ("Target", 400001U), ("Second", 400002U));
        AssertTeamMemberInfoMembers(secondSent[secondBeforeDismiss + 1], ("Target", 400001U), ("Second", 400002U));
        AssertTeamLeadership(firstSent[firstBeforeDismiss + 2], expectedUid: 400001, expectedLeaderUid: 400001, expectedCount: 2);
        AssertTeamLeadership(secondSent[secondBeforeDismiss + 2], expectedUid: 400001, expectedLeaderUid: 400001, expectedCount: 2);
        var firstBeforeSignup = firstSent.Count;
        var secondBeforeSignup = secondSent.Count;

        await handler.HandleFrameAsync(
            first,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            second,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));

        Assert.Equal(firstBeforeSignup + 2, firstSent.Count);
        Assert.Equal(secondBeforeSignup + 1, secondSent.Count);
        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(firstSent[firstBeforeSignup + 1]), out var promotedInfo));
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingForOpponent, promotedInfo.Status);
        Assert.True(LegacyTeamArenaSignupResponsePacket.TryParse(ReadFrame(secondSent[secondBeforeSignup]), out var secondResponse));
        Assert.Equal(400002U, secondResponse.CharacterUid);
    }

    [Fact]
    public async Task TeamArenaSignupPairsTwoWaitingTeamLeadersWithCountdownPrompt()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            ArenaStatisticsProvider.Empty,
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(1293600, "HeroOne", points: 4500),
                TeamArenaRecord(400001, "Target", points: 4300),
            ]));
        var firstSent = new List<byte[]>();
        var secondSent = new List<byte[]>();
        var first = CreateSendingConnection(firstSent);
        var second = CreateSendingConnection(secondSent);

        await LoginLegacyWorldAsync(handler, handoff, first, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, second, accountId: 400001);
        await handler.HandleFrameAsync(first, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(second, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        var firstBeforeSignup = firstSent.Count;

        await handler.HandleFrameAsync(
            first,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));

        Assert.Equal(firstBeforeSignup + 2, firstSent.Count);
        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(firstSent[firstBeforeSignup + 1]), out var waitingInfo));
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingForOpponent, waitingInfo.Status);
        var firstBeforeSecondSignup = firstSent.Count;
        var secondBeforeSignup = secondSent.Count;

        await handler.HandleFrameAsync(
            second,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));

        Assert.Equal(firstBeforeSecondSignup + 2, firstSent.Count);
        Assert.Equal(secondBeforeSignup + 4, secondSent.Count);
        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(firstSent[firstBeforeSecondSignup]), out var firstInactive));
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingInactive, firstInactive.Status);
        AssertTeamArenaSignupResponse(
            firstSent[firstBeforeSecondSignup + 1],
            LegacyTeamArenaSignupDialogType.StartCountDown,
            expectedUid: 1293600);
        AssertTeamArenaSignupResponse(
            secondSent[secondBeforeSignup],
            LegacyTeamArenaSignupDialogType.ArenaIconOn,
            expectedUid: 400001);
        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(secondSent[secondBeforeSignup + 1]), out var secondWaiting));
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingForOpponent, secondWaiting.Status);
        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(secondSent[secondBeforeSignup + 2]), out var secondInactive));
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingInactive, secondInactive.Status);
        AssertTeamArenaSignupResponse(
            secondSent[secondBeforeSignup + 3],
            LegacyTeamArenaSignupDialogType.StartCountDown,
            expectedUid: 400001);
    }

    [Fact]
    public async Task TeamArenaAcceptCountdownFromBothLeadersSendsOpponentImportFrames()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            ArenaStatisticsProvider.Empty,
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(1293600, "HeroOne", points: 4500),
                TeamArenaRecord(400001, "Target", points: 4300),
            ]));
        var firstSent = new List<byte[]>();
        var secondSent = new List<byte[]>();
        var first = CreateSendingConnection(firstSent);
        var second = CreateSendingConnection(secondSent);

        await LoginLegacyWorldAsync(handler, handoff, first, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, second, accountId: 400001);
        await handler.HandleFrameAsync(first, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(second, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(
            first,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            second,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        var firstBeforeAccept = firstSent.Count;
        var secondBeforeAccept = secondSent.Count;

        await handler.HandleFrameAsync(
            first,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));

        Assert.Equal(firstBeforeAccept, firstSent.Count);
        Assert.Equal(secondBeforeAccept, secondSent.Count);

        await handler.HandleFrameAsync(
            second,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));

        var firstImportIndex = AssertTeamArenaTeleportTransition(
            firstSent,
            firstBeforeAccept,
            expectedUid: 1293600,
            expectedMapId: 700,
            expectedX: 45,
            expectedY: 45);
        var firstOpponentIndex = FindNextFrameIndex(
            firstSent,
            firstImportIndex,
            LegacyTeamArenaInfoPlayersPacket.Opcode);
        AssertTeamArenaInfoPlayers(firstSent[firstOpponentIndex], expectedLeaderUid: 400001, ("Target", 400001U));
        var firstStartFightIndex = FindNextFrameIndex(
            firstSent,
            firstOpponentIndex + 1,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            firstSent[firstStartFightIndex],
            LegacyTeamArenaSignupDialogType.StartTheFight,
            expectedUid: 400001);
        var firstMatchIndex = FindNextFrameIndex(
            firstSent,
            firstStartFightIndex + 1,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            firstSent[firstMatchIndex],
            LegacyTeamArenaSignupDialogType.Match,
            expectedUid: 400001,
            expectedButton: LegacyTeamArenaSignupDialogButton.MatchOn);
        var firstScoreIndex = FindNextFrameIndex(
            firstSent,
            firstMatchIndex + 1,
            LegacyTeamArenaMatchScorePacket.Opcode);
        AssertTeamArenaMatchScore(firstSent[firstScoreIndex]);

        var secondImportIndex = AssertTeamArenaTeleportTransition(
            secondSent,
            secondBeforeAccept,
            expectedUid: 400001,
            expectedMapId: 700,
            expectedX: 60,
            expectedY: 60);
        var secondOpponentIndex = FindNextFrameIndex(
            secondSent,
            secondImportIndex,
            LegacyTeamArenaInfoPlayersPacket.Opcode);
        AssertTeamArenaInfoPlayers(secondSent[secondOpponentIndex], expectedLeaderUid: 1293600, ("HeroOne", 1293600U));
        var secondStartFightIndex = FindNextFrameIndex(
            secondSent,
            secondOpponentIndex + 1,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            secondSent[secondStartFightIndex],
            LegacyTeamArenaSignupDialogType.StartTheFight,
            expectedUid: 1293600);
        var secondMatchIndex = FindNextFrameIndex(
            secondSent,
            secondStartFightIndex + 1,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            secondSent[secondMatchIndex],
            LegacyTeamArenaSignupDialogType.Match,
            expectedUid: 1293600,
            expectedButton: LegacyTeamArenaSignupDialogButton.MatchOn);
        var secondScoreIndex = FindNextFrameIndex(
            secondSent,
            secondMatchIndex + 1,
            LegacyTeamArenaMatchScorePacket.Opcode);
        AssertTeamArenaMatchScore(secondSent[secondScoreIndex]);
    }

    [Fact]
    public async Task TeamArenaMatchesListsActiveImportedMatch()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            ArenaStatisticsProvider.Empty,
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(1293600, "HeroOne", points: 4500),
                TeamArenaRecord(400001, "Target", points: 4300),
            ]));
        var firstSent = new List<byte[]>();
        var secondSent = new List<byte[]>();
        var first = CreateSendingConnection(firstSent);
        var second = CreateSendingConnection(secondSent);

        await LoginLegacyWorldAsync(handler, handoff, first, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, second, accountId: 400001);
        await handler.HandleFrameAsync(first, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(second, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(
            first,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            second,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            first,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        await handler.HandleFrameAsync(
            second,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        var firstBeforeQuery = firstSent.Count;

        await handler.HandleFrameAsync(
            first,
            ReadFrame(LegacyArenaMatchesPacket.Query(
                WorldPacketOpcodes.LegacyTeamArenaMatches,
                page: 1).ToFrameBytes()));

        Assert.Equal(firstBeforeQuery + 1, firstSent.Count);
        Assert.True(LegacyArenaMatchesPacket.TryParse(ReadFrame(firstSent[firstBeforeQuery]), out var matches));
        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaMatches, matches.Opcode);
        Assert.Equal(1U, matches.Page);
        Assert.Equal(LegacyArenaMatchesPacket.QualifierListArenaId, matches.ArenaId);
        Assert.Equal(1U, matches.MatchCount);
        Assert.Equal(0U, matches.PlayerCount);
        Assert.Equal(1U, matches.PageCount);
        var match = Assert.Single(matches.Entries);
        Assert.Equal(1U, match.FirstUid);
        Assert.Equal("HeroOne", match.FirstName);
        Assert.Equal(1U, match.FirstMemberCount);
        Assert.Equal(2U, match.SecondUid);
        Assert.Equal("Target", match.SecondName);
        Assert.Equal(1U, match.SecondMemberCount);
    }

    [Fact]
    public async Task TeamArenaWatcherRequestViewsActiveImportedMatch()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        WriteSnapshot(accountId: 400002, uid: 400002, name: "Watcher", x: 832, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            ArenaStatisticsProvider.Empty,
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(1293600, "HeroOne", points: 4500),
                TeamArenaRecord(400001, "Target", points: 4300),
            ]));
        var firstSent = new List<byte[]>();
        var secondSent = new List<byte[]>();
        var watcherSent = new List<byte[]>();
        var first = CreateSendingConnection(firstSent);
        var second = CreateSendingConnection(secondSent);
        var watcher = CreateSendingConnection(watcherSent);

        await LoginLegacyWorldAsync(handler, handoff, first, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, second, accountId: 400001);
        await LoginLegacyWorldAsync(handler, handoff, watcher, accountId: 400002);
        await handler.HandleFrameAsync(first, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(second, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(
            first,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            second,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            first,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        await handler.HandleFrameAsync(
            second,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        var firstBeforeBlockedWatch = firstSent.Count;
        var secondBeforeBlockedWatch = secondSent.Count;

        await handler.HandleFrameAsync(
            first,
            ReadFrame(LegacyArenaWatchersPacket.Query(
                LegacyArenaWatcherMode.RequestView,
                uid: 400001).ToFrameBytes()));

        Assert.Equal(firstBeforeBlockedWatch, firstSent.Count);
        Assert.Equal(secondBeforeBlockedWatch, secondSent.Count);
        var firstBeforeWatch = firstSent.Count;
        var secondBeforeWatch = secondSent.Count;
        var watcherBeforeWatch = watcherSent.Count;

        await handler.HandleFrameAsync(
            watcher,
            ReadFrame(LegacyArenaWatchersPacket.Query(
                LegacyArenaWatcherMode.RequestView,
                uid: 1293600).ToFrameBytes()));

        var watcherImportIndex = AssertTeamArenaTeleportTransition(
            watcherSent,
            watcherBeforeWatch,
            expectedUid: 400002,
            expectedMapId: 700,
            expectedX: 52,
            expectedY: 52);
        var watcherHeaderIndex = FindNextFrameIndex(
            watcherSent,
            watcherImportIndex,
            LegacyArenaWatchersPacket.Opcode);
        AssertArenaWatcherHeader(
            watcherSent[watcherHeaderIndex],
            LegacyArenaWatcherMode.RequestView,
            expectedCount: 0);
        var watcherListIndex = FindNextFrameIndex(
            watcherSent,
            watcherHeaderIndex + 1,
            LegacyArenaWatchersPacket.Opcode);
        AssertArenaWatcherList(watcherSent[watcherListIndex], expectedName: "Watcher", expectedUid: 400002);
        var watcherScoreIndex = FindNextFrameIndex(
            watcherSent,
            watcherListIndex + 1,
            LegacyTeamArenaMatchScorePacket.Opcode);
        AssertTeamArenaMatchScore(watcherSent[watcherScoreIndex]);

        var firstWatcherListIndex = FindNextFrameIndex(
            firstSent,
            firstBeforeWatch,
            LegacyArenaWatchersPacket.Opcode);
        AssertArenaWatcherList(firstSent[firstWatcherListIndex], expectedName: "Watcher", expectedUid: 400002);
        var firstWatcherScoreIndex = FindNextFrameIndex(
            firstSent,
            firstWatcherListIndex + 1,
            LegacyTeamArenaMatchScorePacket.Opcode);
        AssertTeamArenaMatchScore(firstSent[firstWatcherScoreIndex]);

        var secondWatcherListIndex = FindNextFrameIndex(
            secondSent,
            secondBeforeWatch,
            LegacyArenaWatchersPacket.Opcode);
        AssertArenaWatcherList(secondSent[secondWatcherListIndex], expectedName: "Watcher", expectedUid: 400002);
        var secondWatcherScoreIndex = FindNextFrameIndex(
            secondSent,
            secondWatcherListIndex + 1,
            LegacyTeamArenaMatchScorePacket.Opcode);
        AssertTeamArenaMatchScore(secondSent[secondWatcherScoreIndex]);
        var firstBeforeDuplicateWatch = firstSent.Count;
        var secondBeforeDuplicateWatch = secondSent.Count;
        var watcherBeforeDuplicateWatch = watcherSent.Count;

        await handler.HandleFrameAsync(
            watcher,
            ReadFrame(LegacyArenaWatchersPacket.Query(
                LegacyArenaWatcherMode.RequestView,
                uid: 1293600).ToFrameBytes()));

        Assert.Equal(firstBeforeDuplicateWatch, firstSent.Count);
        Assert.Equal(secondBeforeDuplicateWatch, secondSent.Count);
        Assert.Equal(watcherBeforeDuplicateWatch, watcherSent.Count);
        var firstBeforeCheer = firstSent.Count;
        var secondBeforeCheer = secondSent.Count;
        var watcherBeforeCheer = watcherSent.Count;

        await handler.HandleFrameAsync(
            watcher,
            ReadFrame(LegacyArenaWatchersPacket.Query(
                LegacyArenaWatcherMode.Fighters,
                uid: 1293600).ToFrameBytes()));

        Assert.Equal(watcherBeforeCheer + 2, watcherSent.Count);
        Assert.Equal(firstBeforeCheer + 2, firstSent.Count);
        Assert.Equal(secondBeforeCheer + 2, secondSent.Count);
        AssertArenaWatcherList(
            watcherSent[watcherBeforeCheer],
            expectedName: "Watcher",
            expectedUid: 400002,
            expectedPlayer1Cheers: 1);
        AssertTeamArenaMatchScore(watcherSent[watcherBeforeCheer + 1]);
        AssertArenaWatcherList(
            firstSent[firstBeforeCheer],
            expectedName: "Watcher",
            expectedUid: 400002,
            expectedPlayer1Cheers: 1);
        AssertTeamArenaMatchScore(firstSent[firstBeforeCheer + 1]);
        AssertArenaWatcherList(
            secondSent[secondBeforeCheer],
            expectedName: "Watcher",
            expectedUid: 400002,
            expectedPlayer1Cheers: 1);
        AssertTeamArenaMatchScore(secondSent[secondBeforeCheer + 1]);
        var watcherBeforeDuplicateCheer = watcherSent.Count;

        await handler.HandleFrameAsync(
            watcher,
            ReadFrame(LegacyArenaWatchersPacket.Query(
                LegacyArenaWatcherMode.Fighters,
                uid: 1293600).ToFrameBytes()));

        Assert.Equal(watcherBeforeDuplicateCheer, watcherSent.Count);
        var firstBeforeQuit = firstSent.Count;
        var secondBeforeQuit = secondSent.Count;
        var watcherBeforeQuit = watcherSent.Count;

        await handler.HandleFrameAsync(
            watcher,
            ReadFrame(LegacyArenaWatchersPacket.Query(
                LegacyArenaWatcherMode.QuitButton,
                uid: 0).ToFrameBytes()));

        Assert.Equal(watcherBeforeQuit + 3, watcherSent.Count);
        Assert.Equal(firstBeforeQuit + 2, firstSent.Count);
        Assert.Equal(secondBeforeQuit + 2, secondSent.Count);
        AssertArenaWatcherHeader(
            watcherSent[watcherBeforeQuit],
            LegacyArenaWatcherMode.Leave,
            expectedCount: 0);
        AssertArenaWatcherHeader(
            firstSent[firstBeforeQuit],
            LegacyArenaWatcherMode.Watchers,
            expectedCount: 0);
        AssertArenaWatcherHeader(
            secondSent[secondBeforeQuit],
            LegacyArenaWatcherMode.Watchers,
            expectedCount: 0);
        AssertTeamArenaMatchScore(firstSent[firstBeforeQuit + 1]);
        AssertTeamArenaMatchScore(secondSent[secondBeforeQuit + 1]);
        AssertArenaWatcherHeader(
            watcherSent[watcherBeforeQuit + 1],
            LegacyArenaWatcherMode.Leave,
            expectedCount: 0);
        AssertTeamArenaLocation(watcherSent[watcherBeforeQuit + 2], expectedMapId: 1015, expectedX: 832, expectedY: 584);
    }

    [Fact]
    public async Task TeamArenaCombatDamageUpdatesMatchScoreForTeamsAndWatchers()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        WriteSnapshot(accountId: 400002, uid: 400002, name: "Watcher", x: 832, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            ArenaStatisticsProvider.Empty,
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(1293600, "HeroOne", points: 4500),
                TeamArenaRecord(400001, "Target", points: 4300),
            ]));
        var firstSent = new List<byte[]>();
        var secondSent = new List<byte[]>();
        var watcherSent = new List<byte[]>();
        var first = CreateSendingConnection(firstSent);
        var second = CreateSendingConnection(secondSent);
        var watcher = CreateSendingConnection(watcherSent);

        await LoginLegacyWorldAsync(handler, handoff, first, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, second, accountId: 400001);
        await LoginLegacyWorldAsync(handler, handoff, watcher, accountId: 400002);
        await handler.HandleFrameAsync(first, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(second, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(
            first,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            second,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            first,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        await handler.HandleFrameAsync(
            second,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        await handler.HandleFrameAsync(
            watcher,
            ReadFrame(LegacyArenaWatchersPacket.Query(
                LegacyArenaWatcherMode.RequestView,
                uid: 1293600).ToFrameBytes()));
        var firstBeforeAttack = firstSent.Count;
        var secondBeforeAttack = secondSent.Count;
        var watcherBeforeAttack = watcherSent.Count;

        await handler.HandleFrameAsync(
            first,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: 1293600,
                OpponentUid: 400001,
                X: 831,
                Y: 584,
                AttackType: MsgAttackType.Physical,
                Damage: 1_234,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        Assert.Equal(firstBeforeAttack + 1, firstSent.Count);
        Assert.Equal(secondBeforeAttack + 1, secondSent.Count);
        Assert.Equal(watcherBeforeAttack + 1, watcherSent.Count);
        AssertTeamArenaMatchScore(firstSent[firstBeforeAttack], expectedDamageOne: 1_234);
        AssertTeamArenaMatchScore(secondSent[secondBeforeAttack], expectedDamageOne: 1_234);
        AssertTeamArenaMatchScore(watcherSent[watcherBeforeAttack], expectedDamageOne: 1_234);
    }

    [Fact]
    public async Task TeamArenaLethalCombatDamageEndsMatchWithoutGiveUpFrame()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        WriteSnapshot(accountId: 400002, uid: 400002, name: "Watcher", x: 832, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            ArenaStatisticsProvider.Empty,
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(1293600, "HeroOne", points: 4500),
                TeamArenaRecord(400001, "Target", points: 4300),
            ]));
        var winnerSent = new List<byte[]>();
        var loserSent = new List<byte[]>();
        var watcherSent = new List<byte[]>();
        var winner = CreateSendingConnection(winnerSent);
        var loser = CreateSendingConnection(loserSent);
        var watcher = CreateSendingConnection(watcherSent);

        await LoginLegacyWorldAsync(handler, handoff, winner, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, loser, accountId: 400001);
        await LoginLegacyWorldAsync(handler, handoff, watcher, accountId: 400002);
        await handler.HandleFrameAsync(winner, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(loser, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(
            winner,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            loser,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            winner,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        await handler.HandleFrameAsync(
            loser,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        await handler.HandleFrameAsync(
            watcher,
            ReadFrame(LegacyArenaWatchersPacket.Query(
                LegacyArenaWatcherMode.RequestView,
                uid: 1293600).ToFrameBytes()));
        var winnerBeforeAttack = winnerSent.Count;
        var loserBeforeAttack = loserSent.Count;
        var watcherBeforeAttack = watcherSent.Count;

        await handler.HandleFrameAsync(
            winner,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: 1293600,
                OpponentUid: 400001,
                X: 831,
                Y: 584,
                AttackType: MsgAttackType.Physical,
                Damage: 70_000,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        Assert.Equal(winnerBeforeAttack + 6, winnerSent.Count);
        Assert.Equal(loserBeforeAttack + 6, loserSent.Count);
        Assert.Equal(watcherBeforeAttack + 3, watcherSent.Count);
        AssertTeamArenaSignupResponse(
            winnerSent[winnerBeforeAttack],
            LegacyTeamArenaSignupDialogType.Dialog,
            expectedUid: 1293600,
            expectedButton: LegacyTeamArenaSignupDialogButton.Win);
        AssertArenaWatcherHeader(
            winnerSent[winnerBeforeAttack + 1],
            LegacyArenaWatcherMode.Watchers,
            expectedCount: 0);
        AssertTeamArenaMatchScore(winnerSent[winnerBeforeAttack + 2]);
        AssertTeamArenaLocation(winnerSent[winnerBeforeAttack + 3], expectedMapId: 1015, expectedX: 830, expectedY: 584);
        AssertTeamArenaSignupResponse(
            winnerSent[winnerBeforeAttack + 4],
            LegacyTeamArenaSignupDialogType.Dialog2,
            expectedUid: 1293600,
            expectedButton: LegacyTeamArenaSignupDialogButton.Win);
        AssertTeamArenaResultInfo(
            winnerSent[winnerBeforeAttack + 5],
            minArenaPoints: 4530,
            maxArenaPoints: 4549,
            expectedTodayWin: 1,
            expectedTodayBattles: 1,
            expectedTotalWin: 1,
            expectedTotalLose: 0);
        AssertTeamArenaSignupResponse(
            loserSent[loserBeforeAttack],
            LegacyTeamArenaSignupDialogType.Dialog,
            expectedUid: 400001,
            expectedButton: LegacyTeamArenaSignupDialogButton.Lose);
        AssertArenaWatcherHeader(
            loserSent[loserBeforeAttack + 1],
            LegacyArenaWatcherMode.Watchers,
            expectedCount: 0);
        AssertTeamArenaMatchScore(loserSent[loserBeforeAttack + 2]);
        AssertTeamArenaLocation(loserSent[loserBeforeAttack + 3], expectedMapId: 1015, expectedX: 831, expectedY: 584);
        AssertTeamArenaSignupResponse(
            loserSent[loserBeforeAttack + 4],
            LegacyTeamArenaSignupDialogType.Dialog2,
            expectedUid: 400001);
        AssertTeamArenaResultInfo(
            loserSent[loserBeforeAttack + 5],
            minArenaPoints: 4251,
            maxArenaPoints: 4270,
            expectedTodayWin: 0,
            expectedTodayBattles: 1,
            expectedTotalWin: 0,
            expectedTotalLose: 1);
        AssertArenaWatcherHeader(
            watcherSent[watcherBeforeAttack],
            LegacyArenaWatcherMode.Leave,
            expectedCount: 0);
        AssertArenaWatcherHeader(
            watcherSent[watcherBeforeAttack + 1],
            LegacyArenaWatcherMode.Leave,
            expectedCount: 0);
        AssertTeamArenaLocation(watcherSent[watcherBeforeAttack + 2], expectedMapId: 1015, expectedX: 832, expectedY: 584);
    }

    [Fact]
    public async Task TeamArenaGiveUpAfterImportSendsResultAndResetsBothLeaders()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            ArenaStatisticsProvider.Empty,
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(1293600, "HeroOne", points: 4500),
                TeamArenaRecord(400001, "Target", points: 4300),
            ]));
        var winnerSent = new List<byte[]>();
        var loserSent = new List<byte[]>();
        var winner = CreateSendingConnection(winnerSent);
        var loser = CreateSendingConnection(loserSent);

        await LoginLegacyWorldAsync(handler, handoff, winner, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, loser, accountId: 400001);
        await handler.HandleFrameAsync(winner, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(loser, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(
            winner,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            loser,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            winner,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        await handler.HandleFrameAsync(
            loser,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        var winnerBeforeGiveUp = winnerSent.Count;
        var loserBeforeGiveUp = loserSent.Count;

        await handler.HandleFrameAsync(
            loser,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.DoGiveUp).ToFrameBytes()));

        Assert.Equal(winnerBeforeGiveUp + 6, winnerSent.Count);
        Assert.Equal(loserBeforeGiveUp + 6, loserSent.Count);
        AssertTeamArenaSignupResponse(
            winnerSent[winnerBeforeGiveUp],
            LegacyTeamArenaSignupDialogType.OpponentGaveUp,
            expectedUid: 1293600);
        AssertTeamArenaInfoStatus(winnerSent[winnerBeforeGiveUp + 1], LegacyTeamArenaInfoStatus.NotSignedUp);
        AssertTeamArenaSignupResponse(
            winnerSent[winnerBeforeGiveUp + 2],
            LegacyTeamArenaSignupDialogType.Dialog,
            expectedUid: 1293600,
            expectedButton: LegacyTeamArenaSignupDialogButton.Win);
        AssertTeamArenaLocation(winnerSent[winnerBeforeGiveUp + 3], expectedMapId: 1015, expectedX: 830, expectedY: 584);
        AssertTeamArenaSignupResponse(
            winnerSent[winnerBeforeGiveUp + 4],
            LegacyTeamArenaSignupDialogType.Dialog2,
            expectedUid: 1293600,
            expectedButton: LegacyTeamArenaSignupDialogButton.Win);
        AssertTeamArenaInfoStatus(winnerSent[winnerBeforeGiveUp + 5], LegacyTeamArenaInfoStatus.NotSignedUp);
        AssertTeamArenaInfoStatus(loserSent[loserBeforeGiveUp], LegacyTeamArenaInfoStatus.WaitingInactive);
        AssertTeamArenaInfoStatus(loserSent[loserBeforeGiveUp + 1], LegacyTeamArenaInfoStatus.NotSignedUp);
        AssertTeamArenaSignupResponse(
            loserSent[loserBeforeGiveUp + 2],
            LegacyTeamArenaSignupDialogType.Dialog,
            expectedUid: 400001,
            expectedButton: LegacyTeamArenaSignupDialogButton.Lose);
        AssertTeamArenaLocation(loserSent[loserBeforeGiveUp + 3], expectedMapId: 1015, expectedX: 831, expectedY: 584);
        AssertTeamArenaSignupResponse(
            loserSent[loserBeforeGiveUp + 4],
            LegacyTeamArenaSignupDialogType.Dialog2,
            expectedUid: 400001);
        AssertTeamArenaInfoStatus(loserSent[loserBeforeGiveUp + 5], LegacyTeamArenaInfoStatus.NotSignedUp);
    }

    [Fact]
    public async Task TeamArenaGiveUpThresholdsGrantRewardItemsAndActiveness()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            ArenaStatisticsProvider.Empty,
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(1293600, "HeroOne", points: 4500, todayWin: 8, todayBattles: 19),
                TeamArenaRecord(400001, "Target", points: 4300, todayBattles: 19),
            ]),
            CreateTeamArenaRewardCatalog(),
            LegacyRuntimeUpdateLoopOptions.Disabled);
        var winnerSent = new List<byte[]>();
        var loserSent = new List<byte[]>();
        var winner = CreateSendingConnection(winnerSent);
        var loser = CreateSendingConnection(loserSent);

        await LoginLegacyWorldAsync(handler, handoff, winner, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, loser, accountId: 400001);
        await handler.HandleFrameAsync(winner, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(loser, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(
            winner,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            loser,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            winner,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        await handler.HandleFrameAsync(
            loser,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        var winnerBeforeGiveUp = winnerSent.Count;
        var loserBeforeGiveUp = loserSent.Count;

        await handler.HandleFrameAsync(
            loser,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.DoGiveUp).ToFrameBytes()));

        var winnerOpponentGaveUpIndex = FindNextFrameIndex(
            winnerSent,
            winnerBeforeGiveUp,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            winnerSent[winnerOpponentGaveUpIndex],
            LegacyTeamArenaSignupDialogType.OpponentGaveUp,
            expectedUid: 1293600);
        var winnerNotSignedUpIndex = FindNextFrameIndex(
            winnerSent,
            winnerOpponentGaveUpIndex + 1,
            LegacyTeamArenaInfoPacket.Opcode);
        AssertTeamArenaInfoStatus(winnerSent[winnerNotSignedUpIndex], LegacyTeamArenaInfoStatus.NotSignedUp);
        var winnerDialogIndex = FindNextFrameIndex(
            winnerSent,
            winnerNotSignedUpIndex + 1,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            winnerSent[winnerDialogIndex],
            LegacyTeamArenaSignupDialogType.Dialog,
            expectedUid: 1293600,
            expectedButton: LegacyTeamArenaSignupDialogButton.Win);
        var winnerReturnIndex = FindNextActionFrameIndex(
            winnerSent,
            winnerDialogIndex + 1,
            MsgActionType.SetLocation);
        AssertTeamArenaLocation(
            winnerSent[winnerReturnIndex],
            expectedMapId: 1015,
            expectedX: 830,
            expectedY: 584);
        var winnerWinRewardIndex = FindNextFrameIndex(
            winnerSent,
            winnerReturnIndex + 1,
            LegacyGameItemPacket.Opcode);
        AssertTeamArenaRewardItem(winnerSent[winnerWinRewardIndex]);
        var firstActivityRewardsIndex = FindNextFrameIndex(
            winnerSent,
            winnerWinRewardIndex + 1,
            LegacyActivityRewardsInfoPacket.Opcode);
        AssertActivityRewards(winnerSent[firstActivityRewardsIndex], expectedActivityPoints: 90);
        var firstActivityTaskIndex = FindNextFrameIndex(
            winnerSent,
            firstActivityRewardsIndex + 1,
            LegacyActivityTasksPacket.Opcode);
        AssertActivityTask(winnerSent[firstActivityTaskIndex], expectedTaskId: 12, expectedProgress: 1, expectedCompleted: 1);
        var secondActivityRewardsIndex = FindNextFrameIndex(
            winnerSent,
            firstActivityTaskIndex + 1,
            LegacyActivityRewardsInfoPacket.Opcode);
        AssertActivityRewards(winnerSent[secondActivityRewardsIndex], expectedActivityPoints: 100);
        var secondActivityTaskIndex = FindNextFrameIndex(
            winnerSent,
            secondActivityRewardsIndex + 1,
            LegacyActivityTasksPacket.Opcode);
        AssertActivityTask(winnerSent[secondActivityTaskIndex], expectedTaskId: 24, expectedProgress: 1, expectedCompleted: 1);
        var winnerBattleRewardIndex = FindNextFrameIndex(
            winnerSent,
            secondActivityTaskIndex + 1,
            LegacyGameItemPacket.Opcode);
        AssertTeamArenaRewardItem(winnerSent[winnerBattleRewardIndex]);
        var winnerFinalDialogIndex = FindNextFrameIndex(
            winnerSent,
            winnerBattleRewardIndex + 1,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            winnerSent[winnerFinalDialogIndex],
            LegacyTeamArenaSignupDialogType.Dialog2,
            expectedUid: 1293600,
            expectedButton: LegacyTeamArenaSignupDialogButton.Win);
        var winnerResultIndex = FindNextFrameIndex(
            winnerSent,
            winnerFinalDialogIndex + 1,
            LegacyTeamArenaInfoPacket.Opcode);
        AssertTeamArenaResultInfo(
            winnerSent[winnerResultIndex],
            minArenaPoints: 4530,
            maxArenaPoints: 4549,
            expectedTodayWin: 9,
            expectedTodayBattles: 20,
            expectedTotalWin: 1,
            expectedTotalLose: 0);
        Assert.Equal(2, CountFramesWithOpcode(winnerSent, winnerBeforeGiveUp, LegacyGameItemPacket.Opcode));

        var loserWaitingInactiveIndex = FindNextFrameIndex(
            loserSent,
            loserBeforeGiveUp,
            LegacyTeamArenaInfoPacket.Opcode);
        AssertTeamArenaInfoStatus(loserSent[loserWaitingInactiveIndex], LegacyTeamArenaInfoStatus.WaitingInactive);
        var loserNotSignedUpIndex = FindNextFrameIndex(
            loserSent,
            loserWaitingInactiveIndex + 1,
            LegacyTeamArenaInfoPacket.Opcode);
        AssertTeamArenaInfoStatus(loserSent[loserNotSignedUpIndex], LegacyTeamArenaInfoStatus.NotSignedUp);
        var loserDialogIndex = FindNextFrameIndex(
            loserSent,
            loserNotSignedUpIndex + 1,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            loserSent[loserDialogIndex],
            LegacyTeamArenaSignupDialogType.Dialog,
            expectedUid: 400001,
            expectedButton: LegacyTeamArenaSignupDialogButton.Lose);
        var loserReturnIndex = FindNextActionFrameIndex(
            loserSent,
            loserDialogIndex + 1,
            MsgActionType.SetLocation);
        AssertTeamArenaLocation(
            loserSent[loserReturnIndex],
            expectedMapId: 1015,
            expectedX: 831,
            expectedY: 584);
        var loserBattleRewardIndex = FindNextFrameIndex(
            loserSent,
            loserReturnIndex + 1,
            LegacyGameItemPacket.Opcode);
        AssertTeamArenaRewardItem(loserSent[loserBattleRewardIndex]);
        var loserFinalDialogIndex = FindNextFrameIndex(
            loserSent,
            loserBattleRewardIndex + 1,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            loserSent[loserFinalDialogIndex],
            LegacyTeamArenaSignupDialogType.Dialog2,
            expectedUid: 400001);
        var loserResultIndex = FindNextFrameIndex(
            loserSent,
            loserFinalDialogIndex + 1,
            LegacyTeamArenaInfoPacket.Opcode);
        AssertTeamArenaResultInfo(
            loserSent[loserResultIndex],
            minArenaPoints: 4251,
            maxArenaPoints: 4270,
            expectedTodayWin: 0,
            expectedTodayBattles: 20,
            expectedTotalWin: 0,
            expectedTotalLose: 1);
        Assert.Equal(1, CountFramesWithOpcode(loserSent, loserBeforeGiveUp, LegacyGameItemPacket.Opcode));
    }

    [Fact]
    public async Task TeamArenaGiveUpWinRewardUsesReturnedItemsWhenInventoryIsFull()
    {
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(itemsRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 400001, uid: 400001, name: "Target", x: 831, y: 584);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(CreateFullInventoryItems(), []));
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, itemsRoot),
            handoff,
            ArenaStatisticsProvider.Empty,
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(1293600, "HeroOne", points: 4500, todayWin: 8),
                TeamArenaRecord(400001, "Target", points: 4300),
            ]),
            CreateTeamArenaRewardCatalog(),
            LegacyRuntimeUpdateLoopOptions.Disabled);
        var winnerSent = new List<byte[]>();
        var loserSent = new List<byte[]>();
        var winner = CreateSendingConnection(winnerSent);
        var loser = CreateSendingConnection(loserSent);

        await LoginLegacyWorldAsync(handler, handoff, winner, accountId: 1293600);
        await LoginLegacyWorldAsync(handler, handoff, loser, accountId: 400001);
        await handler.HandleFrameAsync(winner, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(loser, ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));
        await handler.HandleFrameAsync(
            winner,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            loser,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        await handler.HandleFrameAsync(
            winner,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        await handler.HandleFrameAsync(
            loser,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.Accept).ToFrameBytes()));
        var winnerBeforeGiveUp = winnerSent.Count;
        var loserBeforeGiveUp = loserSent.Count;

        await handler.HandleFrameAsync(
            loser,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.DoGiveUp).ToFrameBytes()));

        var winnerOpponentGaveUpIndex = FindNextFrameIndex(
            winnerSent,
            winnerBeforeGiveUp,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            winnerSent[winnerOpponentGaveUpIndex],
            LegacyTeamArenaSignupDialogType.OpponentGaveUp,
            expectedUid: 1293600);
        var winnerNotSignedUpIndex = FindNextFrameIndex(
            winnerSent,
            winnerOpponentGaveUpIndex + 1,
            LegacyTeamArenaInfoPacket.Opcode);
        AssertTeamArenaInfoStatus(winnerSent[winnerNotSignedUpIndex], LegacyTeamArenaInfoStatus.NotSignedUp);
        var winnerDialogIndex = FindNextFrameIndex(
            winnerSent,
            winnerNotSignedUpIndex + 1,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            winnerSent[winnerDialogIndex],
            LegacyTeamArenaSignupDialogType.Dialog,
            expectedUid: 1293600,
            expectedButton: LegacyTeamArenaSignupDialogButton.Win);
        var winnerReturnIndex = FindNextActionFrameIndex(
            winnerSent,
            winnerDialogIndex + 1,
            MsgActionType.SetLocation);
        AssertTeamArenaLocation(
            winnerSent[winnerReturnIndex],
            expectedMapId: 1015,
            expectedX: 830,
            expectedY: 584);
        var returnedRewardIndex = FindNextFrameIndex(
            winnerSent,
            winnerReturnIndex + 1,
            LegacyGameItemPacket.Opcode);
        AssertTeamArenaRewardItem(winnerSent[returnedRewardIndex], LegacyGameItemPacket.AddItemReturnedMode);
        var firstActivityRewardsIndex = FindNextFrameIndex(
            winnerSent,
            returnedRewardIndex + 1,
            LegacyActivityRewardsInfoPacket.Opcode);
        AssertActivityRewards(winnerSent[firstActivityRewardsIndex], expectedActivityPoints: 90);
        var firstActivityTaskIndex = FindNextFrameIndex(
            winnerSent,
            firstActivityRewardsIndex + 1,
            LegacyActivityTasksPacket.Opcode);
        AssertActivityTask(winnerSent[firstActivityTaskIndex], expectedTaskId: 12, expectedProgress: 1, expectedCompleted: 1);
        var secondActivityRewardsIndex = FindNextFrameIndex(
            winnerSent,
            firstActivityTaskIndex + 1,
            LegacyActivityRewardsInfoPacket.Opcode);
        AssertActivityRewards(winnerSent[secondActivityRewardsIndex], expectedActivityPoints: 100);
        var secondActivityTaskIndex = FindNextFrameIndex(
            winnerSent,
            secondActivityRewardsIndex + 1,
            LegacyActivityTasksPacket.Opcode);
        AssertActivityTask(winnerSent[secondActivityTaskIndex], expectedTaskId: 24, expectedProgress: 1, expectedCompleted: 1);
        var winnerFinalDialogIndex = FindNextFrameIndex(
            winnerSent,
            secondActivityTaskIndex + 1,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            winnerSent[winnerFinalDialogIndex],
            LegacyTeamArenaSignupDialogType.Dialog2,
            expectedUid: 1293600,
            expectedButton: LegacyTeamArenaSignupDialogButton.Win);
        var winnerResultIndex = FindNextFrameIndex(
            winnerSent,
            winnerFinalDialogIndex + 1,
            LegacyTeamArenaInfoPacket.Opcode);
        AssertTeamArenaResultInfo(
            winnerSent[winnerResultIndex],
            minArenaPoints: 4530,
            maxArenaPoints: 4549,
            expectedTodayWin: 9,
            expectedTodayBattles: 1,
            expectedTotalWin: 1,
            expectedTotalLose: 0);
        Assert.Equal(1, CountFramesWithOpcode(winnerSent, winnerBeforeGiveUp, LegacyGameItemPacket.Opcode));

        var loserWaitingInactiveIndex = FindNextFrameIndex(
            loserSent,
            loserBeforeGiveUp,
            LegacyTeamArenaInfoPacket.Opcode);
        AssertTeamArenaInfoStatus(loserSent[loserWaitingInactiveIndex], LegacyTeamArenaInfoStatus.WaitingInactive);
        var loserNotSignedUpIndex = FindNextFrameIndex(
            loserSent,
            loserWaitingInactiveIndex + 1,
            LegacyTeamArenaInfoPacket.Opcode);
        AssertTeamArenaInfoStatus(loserSent[loserNotSignedUpIndex], LegacyTeamArenaInfoStatus.NotSignedUp);
        var loserDialogIndex = FindNextFrameIndex(
            loserSent,
            loserNotSignedUpIndex + 1,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            loserSent[loserDialogIndex],
            LegacyTeamArenaSignupDialogType.Dialog,
            expectedUid: 400001,
            expectedButton: LegacyTeamArenaSignupDialogButton.Lose);
        var loserReturnIndex = FindNextActionFrameIndex(
            loserSent,
            loserDialogIndex + 1,
            MsgActionType.SetLocation);
        AssertTeamArenaLocation(
            loserSent[loserReturnIndex],
            expectedMapId: 1015,
            expectedX: 831,
            expectedY: 584);
        var loserFinalDialogIndex = FindNextFrameIndex(
            loserSent,
            loserReturnIndex + 1,
            LegacyTeamArenaSignupResponsePacket.Opcode);
        AssertTeamArenaSignupResponse(
            loserSent[loserFinalDialogIndex],
            LegacyTeamArenaSignupDialogType.Dialog2,
            expectedUid: 400001);
        var loserResultIndex = FindNextFrameIndex(
            loserSent,
            loserFinalDialogIndex + 1,
            LegacyTeamArenaInfoPacket.Opcode);
        AssertTeamArenaInfoStatus(loserSent[loserResultIndex], LegacyTeamArenaInfoStatus.NotSignedUp);
        Assert.Equal(0, CountFramesWithOpcode(loserSent, loserBeforeGiveUp, LegacyGameItemPacket.Opcode));

        var winnerBeforeWarehouseShow = winnerSent.Count;
        await handler.HandleFrameAsync(
            winner,
            ReadFrame(new LegacyWarehousePacket(
                ReturnedItemsWarehouseId,
                LegacyWarehouseAction.Show,
                ItemUid: 0,
                Capacity: 0,
                Items: []).ToFrameBytes()));

        Assert.Equal(winnerBeforeWarehouseShow + 1, winnerSent.Count);
        Assert.True(LegacyWarehousePacket.TryParse(ReadFrame(winnerSent[winnerBeforeWarehouseShow]), out var returnedItems));
        Assert.Equal(ReturnedItemsWarehouseId, returnedItems.NpcId);
        Assert.Equal(LegacyWarehouseAction.Show, returnedItems.Action);
        var returnedItem = Assert.Single(returnedItems.Items);
        Assert.Equal(723_912U, returnedItem.ItemId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dbRoot))
        {
            Directory.Delete(_dbRoot, recursive: true);
        }
    }

    private static async Task LoginLegacyWorldAsync(
        WorldServerFrameHandler handler,
        AccountHandoffService handoff,
        ConquerTcpConnection connection,
        long accountId)
    {
        var ticket = handoff.Issue(accountId, "user" + accountId, ConquerRuntimeDefaults.WorldServer);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
    }

    private void WriteSnapshot(long accountId, long uid, string name, long x, long y)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name={{name}}
            Class=7051
            Map=1015
            X={{x}}
            Y={{y}}
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            RacePoints=0
            ChampionPoints=0
            GuildDontion=900
            PkPoints=0
            Online=7
            Day={{DateTime.Now.DayOfYear}}
            Activeness=0/80/2/1/10/1/22/0/0/

            [PaidNobility]
            Rank=King
            Identifier={{uid}}
            """);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertTeamAddFrames(
        List<byte[]> sent,
        int offset,
        uint expectedMemberUid,
        uint expectedLeaderUid)
    {
        Assert.True(LegacyTeamLeadershipPacket.TryParse(ReadFrame(sent[offset]), out var teammate));
        Assert.Equal(LegacyTeamLeadershipMode.Teammate, teammate.Mode);
        Assert.Equal(0U, teammate.Uid);
        Assert.Equal(0U, teammate.LeaderUid);
        Assert.Equal(0, teammate.Count);

        Assert.True(LegacyTeamLeadershipPacket.TryParse(ReadFrame(sent[offset + 1]), out var memberLeader));
        Assert.Equal(LegacyTeamLeadershipMode.Leader, memberLeader.Mode);
        Assert.Equal(expectedMemberUid, memberLeader.Uid);
        Assert.Equal(expectedLeaderUid, memberLeader.LeaderUid);
        Assert.Equal(2, memberLeader.Count);

        Assert.True(LegacyTeamLeadershipPacket.TryParse(ReadFrame(sent[offset + 2]), out var leaderLeader));
        Assert.Equal(LegacyTeamLeadershipMode.Leader, leaderLeader.Mode);
        Assert.Equal(expectedLeaderUid, leaderLeader.Uid);
        Assert.Equal(expectedLeaderUid, leaderLeader.LeaderUid);
        Assert.Equal(2, leaderLeader.Count);

        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[offset + 3]), out var teamUpdate));
        Assert.Equal(expectedMemberUid, teamUpdate.Uid);
        var update = Assert.Single(teamUpdate.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.Team, update.Type);
        Assert.Equal(1UL, update.Value);
    }

    private static void AssertTeamRemovalCleanup(
        List<byte[]> sent,
        int offset,
        uint expectedRemovedUid,
        int expectedRemainingCount)
    {
        Assert.True(LegacyTeamLeadershipPacket.TryParse(ReadFrame(sent[offset]), out var cleanup));
        Assert.Equal(LegacyTeamLeadershipMode.Leader, cleanup.Mode);
        Assert.Equal(expectedRemovedUid, cleanup.Uid);
        Assert.Equal(0U, cleanup.LeaderUid);
        Assert.Equal(expectedRemainingCount, cleanup.Count);
        Assert.Equal(0U, cleanup.Unknown);

        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[offset + 1]), out var teamUpdate));
        Assert.Equal(expectedRemovedUid, teamUpdate.Uid);
        var update = Assert.Single(teamUpdate.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.Team, update.Type);
        Assert.Equal(0UL, update.Value);
    }

    private static void AssertTeamLeadership(
        byte[] bytes,
        uint expectedUid,
        uint expectedLeaderUid,
        int expectedCount)
    {
        Assert.True(LegacyTeamLeadershipPacket.TryParse(ReadFrame(bytes), out var leadership));
        Assert.Equal(LegacyTeamLeadershipMode.Leader, leadership.Mode);
        Assert.Equal(expectedUid, leadership.Uid);
        Assert.Equal(expectedLeaderUid, leadership.LeaderUid);
        Assert.Equal(expectedCount, leadership.Count);
        Assert.Equal(0U, leadership.Unknown);
    }

    private static void AssertTeamRelation(
        byte[] bytes,
        uint expectedRequesterUid,
        uint expectedReceiverUid,
        string expectedReceiverName)
    {
        Assert.True(LegacyRelationPacket.TryParse(ReadFrame(bytes), out var relation));
        Assert.Equal(expectedRequesterUid, relation.RequesterUid);
        Assert.Equal(expectedReceiverUid, relation.ReceiverUid);
        Assert.Equal(140U, relation.RequesterLevel);
        Assert.Equal(0U, relation.RequesterBattlePower);
        Assert.False(relation.SameSpouse);
        Assert.False(relation.Friend);
        Assert.False(relation.TradePartner);
        Assert.False(relation.MentorApprentice);
        Assert.False(relation.Teammate);
        Assert.False(relation.GuildMember);
        Assert.False(relation.Enemy);
        Assert.Equal(expectedReceiverName, relation.ReceiverName);
    }

    private static void AssertTeamMemberInfo(byte[] bytes)
    {
        Assert.True(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(bytes), out var memberInfo));
        Assert.Equal(LegacyTeamMemberInfoAction.AddMember, memberInfo.Action);
        Assert.Equal((byte)2, memberInfo.Count);
        Assert.Equal((ushort)1, memberInfo.Unknown);
        Assert.Collection(
            memberInfo.Entries,
            leader =>
            {
                Assert.Equal("HeroOne", leader.Name);
                Assert.Equal(1_293_600U, leader.Uid);
            },
            member =>
            {
                Assert.Equal("Target", member.Name);
                Assert.Equal(400_001U, member.Uid);
            });
    }

    private static void AssertSingleTeamMemberInfo(byte[] bytes, string expectedName, uint expectedUid)
    {
        Assert.True(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(bytes), out var memberInfo));
        Assert.Equal(LegacyTeamMemberInfoAction.AddMember, memberInfo.Action);
        Assert.Equal((byte)1, memberInfo.Count);
        Assert.Equal((ushort)1, memberInfo.Unknown);
        var member = Assert.Single(memberInfo.Entries);
        Assert.Equal(expectedName, member.Name);
        Assert.Equal(expectedUid, member.Uid);
    }

    private static void AssertTeamMemberInfoMembers(
        byte[] bytes,
        params (string Name, uint Uid)[] expectedMembers)
    {
        Assert.True(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(bytes), out var memberInfo));
        Assert.Equal(LegacyTeamMemberInfoAction.AddMember, memberInfo.Action);
        Assert.Equal((byte)expectedMembers.Length, memberInfo.Count);
        Assert.Equal((ushort)1, memberInfo.Unknown);
        Assert.Equal(expectedMembers.Length, memberInfo.Entries.Count);
        for (var i = 0; i < expectedMembers.Length; i++)
        {
            Assert.Equal(expectedMembers[i].Name, memberInfo.Entries[i].Name);
            Assert.Equal(expectedMembers[i].Uid, memberInfo.Entries[i].Uid);
        }
    }

    private static void AssertTeamArenaSignupResponse(
        byte[] bytes,
        LegacyTeamArenaSignupDialogType expectedDialogType,
        uint expectedUid,
        LegacyTeamArenaSignupDialogButton expectedButton = LegacyTeamArenaSignupDialogButton.SignUp)
    {
        Assert.True(LegacyTeamArenaSignupResponsePacket.TryParse(ReadFrame(bytes), out var response));
        Assert.Equal(expectedDialogType, response.DialogType);
        Assert.Equal(expectedButton, response.DialogButton);
        Assert.Equal(expectedUid, response.CharacterUid);
    }

    private static void AssertTeamArenaInfoStatus(byte[] bytes, LegacyTeamArenaInfoStatus expectedStatus)
    {
        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(bytes), out var info));
        Assert.Equal(expectedStatus, info.Status);
    }

    private static void AssertTeamArenaResultInfo(
        byte[] bytes,
        uint minArenaPoints,
        uint maxArenaPoints,
        uint expectedTodayWin,
        uint expectedTodayBattles,
        uint expectedTotalWin,
        uint expectedTotalLose)
    {
        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(bytes), out var info));
        Assert.Equal(LegacyTeamArenaInfoStatus.NotSignedUp, info.Status);
        Assert.InRange(info.ArenaPoints, minArenaPoints, maxArenaPoints);
        Assert.Equal(expectedTodayWin, info.TodayWin);
        Assert.Equal(expectedTodayBattles, info.TodayBattles);
        Assert.Equal(expectedTotalWin, info.TotalWin);
        Assert.Equal(expectedTotalLose, info.TotalLose);
    }

    private static int AssertTeamArenaTeleportTransition(
        IReadOnlyList<byte[]> sent,
        int startIndex,
        uint expectedUid,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        var teleportIndex = FindNextActionFrameIndex(sent, startIndex, MsgActionType.Teleport);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[teleportIndex]), out var teleport));
        Assert.Equal(expectedUid, teleport.ObjId);
        Assert.Equal(expectedMapId, teleport.DwParam);
        Assert.Equal(expectedMapId, teleport.DwParam3);
        Assert.Equal(expectedX, teleport.PositionX);
        Assert.Equal(expectedY, teleport.PositionY);

        var merchantIndex = FindNextActionFrameIndex(
            sent,
            teleportIndex + 1,
            MsgActionType.LegacyMerchantStartup);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[merchantIndex]), out var merchant));
        Assert.Equal(expectedUid, merchant.ObjId);
        Assert.Equal(2U, merchant.DwParam);
        Assert.Equal(expectedMapId, merchant.DwParam3);
        Assert.Equal(expectedX, merchant.PositionX);
        Assert.Equal(expectedY, merchant.PositionY);

        var stopVendingIndex = FindNextActionFrameIndex(
            sent,
            merchantIndex + 1,
            MsgActionType.StopVending);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[stopVendingIndex]), out var stopVending));
        Assert.Equal(expectedUid, stopVending.ObjId);
        Assert.Equal(expectedMapId, stopVending.DwParam);
        Assert.Equal(expectedMapId, stopVending.DwParam3);
        Assert.Equal(expectedX, stopVending.PositionX);
        Assert.Equal(expectedY, stopVending.PositionY);

        var colorIndex = FindNextActionFrameIndex(
            sent,
            stopVendingIndex + 1,
            MsgActionType.LegacyColor);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[colorIndex]), out var color));
        Assert.Equal(expectedUid, color.ObjId);
        Assert.Equal(0U, color.DwParam);
        Assert.Equal(expectedX, color.PositionX);
        Assert.Equal(expectedY, color.PositionY);

        var mapStatusIndex = FindNextFrameIndex(
            sent,
            colorIndex + 1,
            LegacyMapStatusPacket.Opcode);
        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[mapStatusIndex]), out var mapStatus));
        Assert.Equal(expectedMapId, mapStatus.Id);
        Assert.Equal(expectedMapId, mapStatus.BaseId);
        return mapStatusIndex + 1;
    }

    private static int FindNextActionFrameIndex(
        IReadOnlyList<byte[]> sent,
        int startIndex,
        MsgActionType expectedType)
    {
        for (var index = startIndex; index < sent.Count; index++)
        {
            if (MsgActionPacket.TryParse(ReadFrame(sent[index]), out var action) &&
                action.Type == expectedType)
            {
                return index;
            }
        }

        Assert.Fail($"Expected MsgAction {expectedType} at or after frame index {startIndex}.");
        return -1;
    }

    private static int FindNextFrameIndex(
        IReadOnlyList<byte[]> sent,
        int startIndex,
        ushort expectedOpcode)
    {
        for (var index = startIndex; index < sent.Count; index++)
        {
            if (ReadFrame(sent[index]).Header.Opcode == expectedOpcode)
            {
                return index;
            }
        }

        Assert.Fail($"Expected opcode {expectedOpcode} at or after frame index {startIndex}.");
        return -1;
    }

    private static int CountFramesWithOpcode(
        IReadOnlyList<byte[]> sent,
        int startIndex,
        ushort expectedOpcode)
    {
        var count = 0;
        for (var index = startIndex; index < sent.Count; index++)
        {
            if (ReadFrame(sent[index]).Header.Opcode == expectedOpcode)
            {
                count++;
            }
        }

        return count;
    }

    private static void AssertTeamArenaLocation(
        byte[] bytes,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(bytes), out var action));
        Assert.Equal(MsgActionType.SetLocation, action.Type);
        Assert.Equal(expectedMapId, action.DwParam);
        Assert.Equal(expectedX, action.PositionX);
        Assert.Equal(expectedY, action.PositionY);
    }

    private static void AssertArenaWatcherHeader(
        byte[] bytes,
        LegacyArenaWatcherMode expectedMode,
        uint expectedCount)
    {
        Assert.True(LegacyArenaWatchersPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedMode, packet.Mode);
        Assert.Equal(expectedCount, packet.Count);
        Assert.Empty(packet.Entries);
    }

    private static void AssertArenaWatcherList(
        byte[] bytes,
        string expectedName,
        uint expectedUid,
        uint expectedPlayer1Cheers = 0,
        uint expectedPlayer2Cheers = 0)
    {
        Assert.True(LegacyArenaWatchersPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyArenaWatcherMode.Watchers, packet.Mode);
        Assert.Equal(1U, packet.Count);
        Assert.Equal(expectedPlayer1Cheers, packet.Player1Cheers);
        Assert.Equal(expectedPlayer2Cheers, packet.Player2Cheers);
        var entry = Assert.Single(packet.Entries);
        Assert.Equal(expectedName, entry.Name);
        Assert.Equal(expectedUid, entry.Uid);
        Assert.Equal(140U, entry.Level);
        Assert.Equal(7051U, entry.Class);
    }

    private static void AssertTeamArenaInfoPlayers(
        byte[] bytes,
        uint expectedLeaderUid,
        params (string Name, uint Uid)[] expectedMembers)
    {
        Assert.True(LegacyTeamArenaInfoPlayersPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal((uint)LegacyTeamArenaInfoPlayerKind.Opponents, packet.Mode);
        Assert.Equal(expectedLeaderUid, packet.LeaderUid);
        Assert.Equal((uint)expectedMembers.Length, packet.Count);
        Assert.Equal(expectedMembers.Length, packet.Entries.Count);
        for (var index = 0; index < expectedMembers.Length; index++)
        {
            Assert.Equal(expectedMembers[index].Name, packet.Entries[index].Name);
            Assert.Equal(expectedMembers[index].Uid, packet.Entries[index].Uid);
        }
    }

    private static void AssertTeamArenaMatchScore(
        byte[] bytes,
        uint expectedDamageOne = 0,
        uint expectedDamageTwo = 0)
    {
        Assert.True(LegacyTeamArenaMatchScorePacket.TryParse(ReadFrame(bytes), out var score));
        Assert.Equal(1_293_600U, score.UidOne);
        Assert.Equal("HeroOne", score.NameOne);
        Assert.Equal(expectedDamageOne, score.DamageOne);
        Assert.Equal(400_001U, score.UidTwo);
        Assert.Equal("Target", score.NameTwo);
        Assert.Equal(expectedDamageTwo, score.DamageTwo);
        Assert.Equal(LegacyTeamArenaMatchScorePacket.LegacyResult, score.Result);
    }

    private static void AssertTeamArenaRewardItem(
        byte[] bytes,
        ushort expectedMode = LegacyGameItemPacket.AddItemMode)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var item));
        Assert.Equal(723_912U, item.ItemId);
        Assert.Equal(expectedMode, item.Mode);
        Assert.Equal(1, item.StackSize);
    }

    private static void AssertActivityRewards(byte[] bytes, uint expectedActivityPoints)
    {
        Assert.True(LegacyActivityRewardsInfoPacket.TryParse(ReadFrame(bytes), out var rewards));
        Assert.Equal(LegacyActivityRewardsInfoPacket.InitializeListAction, rewards.Action);
        Assert.Equal(expectedActivityPoints, rewards.ActivityPoints);
    }

    private static void AssertActivityTask(
        byte[] bytes,
        uint expectedTaskId,
        byte expectedProgress,
        byte expectedCompleted)
    {
        Assert.True(LegacyActivityTasksPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyActivityTasksPacket.InitializeListAction, packet.Action);
        var task = Assert.Single(packet.Tasks);
        Assert.Equal(expectedTaskId, task.Id);
        Assert.Equal(expectedProgress, task.Progress);
        Assert.Equal(expectedCompleted, task.Completed);
    }

    private static TeamArenaRecord TeamArenaRecord(
        int playerId,
        string name,
        int points,
        int todayWin = 0,
        int todayBattles = 0,
        int totalWin = 0,
        int totalLose = 0)
    {
        return new TeamArenaRecord(
            playerId,
            name,
            [140, 7051, 1541008, points, 0, 0, todayBattles, todayWin, totalLose, totalWin, 0, 0, 0, 0],
            "TeamArena.ini",
            playerId,
            $"{playerId}/{name}/140/7051/1541008/{points}/0/0/{todayBattles}/{todayWin}/{totalLose}/{totalWin}/0/0/0/0/",
            [
                playerId.ToString(),
                name,
                "140",
                "7051",
                "1541008",
                points.ToString(),
                "0",
                "0",
                todayBattles.ToString(),
                todayWin.ToString(),
                totalLose.ToString(),
                totalWin.ToString(),
                "0",
                "0",
                "0",
                "0",
                "",
            ]);
    }

    internal static WorldDataCatalog CreateTeamArenaRewardCatalog(
        bool includeRegularArenaActivityTasks = false,
        IReadOnlyList<Portal>? portals = null,
        IReadOnlyList<MapMetadata>? maps = null,
        IReadOnlyList<Monster>? monsters = null,
        IReadOnlyList<MonsterSpawn>? monsterSpawns = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
                "fixture://team-arena-rewards",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [TeamArenaRewardItemType()],
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: QualifierArenaActivityTasks(includeRegularArenaActivityTasks),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: portals ?? [],
            maps: maps ?? [],
            mapNames: [],
            monsters: monsters ?? [],
            monsterSpawns: monsterSpawns ?? [],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static ItemType TeamArenaRewardItemType()
    {
        var fields = Enumerable.Repeat("0", ItemType.ConquerPointsWorthFieldIndex + 1).ToArray();
        fields[ItemType.DurabilityFieldIndex] = "1";
        return new ItemType(723_912, "TeamArenaRewardPack", fields);
    }

    private static IReadOnlyList<PlayerItemSnapshotLoaderTests.TestDbItem> CreateFullInventoryItems()
    {
        return Enumerable.Range(0, 40)
            .Select(index => PlayerItemSnapshotLoaderTests.DbItem(
                uid: (uint)(1_000 + index),
                itemId: 1_088_001,
                position: 0,
                stackSize: 1))
            .ToArray();
    }

    private static ActivityTaskCatalog QualifierArenaActivityTasks(bool includeRegularArenaActivityTasks)
    {
        ActivityTaskDefinition[] tasks = includeRegularArenaActivityTasks
            ?
            [
                QualifierArenaActivityTask(7),
                TeamArenaActivityTask(12),
                QualifierArenaActivityTask(19),
                TeamArenaActivityTask(24),
                QualifierArenaActivityTask(31),
                QualifierArenaActivityTask(36),
            ]
            :
            [
                TeamArenaActivityTask(12),
                TeamArenaActivityTask(24),
            ];

        return new ActivityTaskCatalog(
            new ActivityTaskMetadata(
                tasks.Length,
                "ActivityTask",
                SourceLine: 1,
                Entries:
                [
                    new ActivityTaskRawEntry(
                        "Num",
                        tasks.Length.ToString(CultureInfo.InvariantCulture),
                        SourceLine: 2,
                        RawLine: "Num=" + tasks.Length.ToString(CultureInfo.InvariantCulture)),
                ]),
            tasks,
            "fixture://activity");
    }

    private static ActivityTaskDefinition QualifierArenaActivityTask(int sectionId)
    {
        return new ActivityTaskDefinition(
            sectionId,
            sectionId,
            "Arena",
            Type: 0,
            Progress: 1,
            ActiveValue: 10,
            CloseLevelRaw: "0",
            CloseLevel: 0,
            Tip: "",
            OpenLevelRaw: "0",
            OpenLevel: 0,
            PreviewRaw: null,
            Preview: null,
            Intro: null,
            AnimationSection: null,
            Param: null,
            SourceSection: sectionId.ToString(),
            SourceLine: sectionId,
            Entries: []);
    }

    private static ActivityTaskDefinition TeamArenaActivityTask(int sectionId)
    {
        return new ActivityTaskDefinition(
            sectionId,
            sectionId,
            "TeamArena",
            Type: 0,
            Progress: 1,
            ActiveValue: 10,
            CloseLevelRaw: "0",
            CloseLevel: 0,
            Tip: "",
            OpenLevelRaw: "0",
            OpenLevel: 0,
            PreviewRaw: null,
            Preview: null,
            Intro: null,
            AnimationSection: null,
            Param: null,
            SourceSection: sectionId.ToString(),
            SourceLine: sectionId,
            Entries: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private static void AssertTeamPacket(byte[] bytes, MsgTeamType expectedType, uint expectedUid)
    {
        Assert.True(MsgTeamPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.Type);
        Assert.Equal(expectedUid, packet.Uid);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }
}
