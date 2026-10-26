using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WorldPacketDispatchTests
{
    private const int LegacyFocusedFlag = 0x15;
    private const int LegacyFlyFlag = 0x1B;
    private const int LegacyInfinityFlag = 0xD7;
    private const int LegacyHeavensWrathFlag = 0xEC;
    private const int LegacyCaptureTheFlagCarrierFlag = 0x76;

    [Fact]
    public async Task HandlerDispatchesMsgTeamThroughRegistry()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new MsgTeamPacket(MsgTeamType.InviteRequest, 1_400_123);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(packet.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgTeam", output);
        Assert.Contains("type=InviteRequest", output);
        Assert.Contains("uid=1400123", output);
        Assert.DoesNotContain("WorldServer received opcode 2243", output);
    }

    [Fact]
    public async Task HandlerDispatchesMsgArenaSignupThroughRegistry()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new MsgArenaSignupPacket(
            MsgArenaSignupDialogType.Match,
            MsgArenaSignupDialogButton.Accept);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(packet.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgArenaSignup", output);
        Assert.Contains("dialogType=Match", output);
        Assert.Contains("dialogButton=Win", output);
        Assert.DoesNotContain("WorldServer received opcode 2140", output);
    }

    [Fact]
    public async Task HandlerDispatchesMsgAttackThroughRegistry()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new MsgAttackPacket(new MsgAttackQuery(
            Uid: 1_400_123,
            OpponentUid: 400_001,
            X: 830,
            Y: 584,
            AttackType: MsgAttackType.Physical,
            Damage: 12_345,
            KilledMonster: 0,
            ResponseDamage: 12_345,
            Effect: MsgAttackEffect.Block | MsgAttackEffect.CriticalStrike,
            Unknown: 0));

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(packet.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAttack", output);
        Assert.Contains("uid=1400123", output);
        Assert.Contains("opponentUid=400001", output);
        Assert.Contains("type=Physical", output);
        Assert.Contains("damage=12345", output);
        Assert.DoesNotContain("WorldServer received opcode 2135", output);
    }

    [Fact]
    public async Task HandlerDispatchesConstructedMsgAttackThroughRegistry()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(CreateConnection(), CreateSyntheticMsgAttackFrame());

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAttack", output);
        Assert.Contains("uid=1400123", output);
        Assert.Contains("opponentUid=500123", output);
        Assert.Contains("x=91", output);
        Assert.Contains("y=113", output);
        Assert.Contains("type=Physical", output);
        Assert.Contains("damage=321", output);
        Assert.DoesNotContain("WorldServer rejected malformed MsgAttack", output);
        Assert.DoesNotContain("WorldServer received opcode 2135", output);
    }

    [Fact]
    public async Task HandlerDispatchesMsgAttackWithOmittedResultFieldsThroughRegistry()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var frame = CreateProtoFixtureFrame(WorldPacketOpcodes.MsgAttack,
            (4, 1), (5, 2), (7, 3), (8, 4), (13, (uint)MsgAttackType.Physical));

        await handler.HandleFrameAsync(CreateConnection(), frame);

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAttack", output);
        Assert.Contains("uid=1", output);
        Assert.Contains("opponentUid=2", output);
        Assert.Contains("x=3", output);
        Assert.Contains("y=4", output);
        Assert.Contains("type=Physical", output);
        Assert.Contains("damage=0", output);
        Assert.DoesNotContain("WorldServer rejected malformed MsgAttack", output);
        Assert.DoesNotContain("WorldServer received opcode 2135", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedMsgAttackPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(CreateConnection(), CreateMalformedMsgAttackFrame());

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed MsgAttack packet session=none.", output);
        Assert.Contains("opcode=2135 payloadLength=0", output);
        Assert.DoesNotContain("WorldServer received opcode 2135", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedMsgActionPacketWithFrameDiagnostic()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.MsgAction, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x01;

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed MsgAction packet session=none.", output);
        Assert.Contains("opcode=2046 payloadLength=2 payloadSampleHex=0801", output);
        Assert.DoesNotContain("WorldServer received opcode 2046", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyNpcReplyPacketWithFrameDiagnostic()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 3);
        writer.Payload[0] = 0xAA;
        writer.Payload[1] = 0xBB;
        writer.Payload[2] = 0xCC;

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyNpcReply packet session=none.", output);
        Assert.Contains("opcode=2255 payloadLength=3 payloadSampleHex=AABBCC", output);
        Assert.DoesNotContain("WorldServer received opcode 2255", output);
    }

    [Fact]
    public async Task HandlerDispatchesConstructedShortMsgActionThroughRegistry()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            CreateProtoFixtureFrame(WorldPacketOpcodes.MsgAction, (3, 17), (12, (ushort)MsgActionType.QueryEquipment)));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction session=none type=QueryEquipment dwParam=17", output);
        Assert.Contains("WorldServer source-backed no-op MsgAction QueryEquipment session=none typeId=408", output);
        Assert.Contains("source has no [Data(408)]", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
        Assert.DoesNotContain("WorldServer rejected malformed MsgAction", output);
        Assert.DoesNotContain("WorldServer received opcode 2046", output);
    }

    [Fact]
    public async Task HandlerDispatchesSetPkModeWithoutGenericUnmodeledWarning()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_400_123,
                DwParam = 3,
                Type = MsgActionType.SetPkMode,
            }.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction session=none type=SetPkMode dwParam=3", output);
        Assert.Contains("WorldServer skipped MsgAction SetPkMode; no active world session.", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
        Assert.DoesNotContain("WorldServer received opcode 2046", output);
    }

    [Fact]
    public async Task HandlerDispatchesGhostWithoutGenericUnmodeledWarning()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_400_123,
                Timestamp = 123_456,
                Type = MsgActionType.Ghost,
                PositionX = 830,
                PositionY = 584,
            }.ToFrameBytes()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction session=none type=Ghost dwParam=0", output);
        Assert.Contains("WorldServer skipped MsgAction Ghost; no active world session.", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
        Assert.DoesNotContain("WorldServer received opcode 2046", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyUnregistered476AsSourceBackedNoOp()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            CreateProtoFixtureFrame(WorldPacketOpcodes.MsgAction, (3, 1), (12, (ushort)MsgActionType.LegacyUnregistered476)));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction session=none type=LegacyUnregistered476 dwParam=1", output);
        Assert.Contains("WorldServer source-backed no-op MsgAction LegacyUnregistered476 session=none typeId=476", output);
        Assert.Contains("source has no [Data(476)]", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
        Assert.DoesNotContain("WorldServer rejected malformed MsgAction", output);
        Assert.DoesNotContain("WorldServer received opcode 2046", output);
    }

    [Fact]
    public async Task HandlerDispatchesTeamSearchForMemberWithLegacyCoordinates()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var leaderConnection = CreateConnection(sent);
        var memberConnection = CreateConnection([]);
        var sessions = new WorldSessionStore();
        var leader = CreateTitleStorageSession(Path.GetTempPath(), "");
        var member = CreateTitleStorageSession(Path.GetTempPath(), "") with
        {
            Character = leader.Character with
            {
                Uid = 1_293_601,
                Name = "Member",
                X = 840,
                Y = 602,
            },
        };
        sessions.Set(leaderConnection, leader);
        sessions.Set(memberConnection, member);
        var teamStates = new TeamSessionStateStore();
        teamStates.CreateTeam(leaderConnection, leader.Character.Uid);
        Assert.True(teamStates.TryAddMember(leaderConnection, memberConnection, member.Character.Uid));
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(
            leaderConnection,
            log,
            leader,
            Sessions: sessions,
            TeamStates: teamStates);

        await registry.HandleAsync(
            context,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_400_123,
                DwParam = 1_293_601,
                Type = MsgActionType.TeamSearchForMember,
            }.ToFrameBytes()));

        var response = Assert.Single(sent);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(response), out var echoed));
        Assert.Equal(MsgActionType.TeamSearchForMember, echoed.Type);
        Assert.Equal(1_293_601U, echoed.DwParam);
        Assert.Equal(840U, echoed.PositionX);
        Assert.Equal(602U, echoed.PositionY);
        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction TeamSearchForMember", output);
        Assert.DoesNotContain("PARITY-GAP registered MsgAction", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
    }

    [Fact]
    public async Task HandlerDispatchesQuerySpawnWithLegacyWindowSpawn()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var requesterConnection = CreateConnection(sent);
        var targetConnection = CreateConnection([]);
        var requester = CreateTitleStorageSession(Path.GetTempPath(), "");
        var target = CreateTitleStorageSession(Path.GetTempPath(), "") with
        {
            Character = requester.Character with
            {
                Uid = 1_293_601,
                Name = "VisibleTarget",
                X = 841,
                Y = 603,
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(requesterConnection, requester);
        sessions.Set(targetConnection, target);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(
            requesterConnection,
            log,
            requester,
            Sessions: sessions);

        await registry.HandleAsync(
            context,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_400_123,
                DwParam = 1_293_601,
                Type = MsgActionType.QuerySpawn,
            }.ToFrameBytes()));

        var frame = Assert.Single(sent);
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(frame), out var spawn));
        Assert.Equal(1_293_601UL, spawn.GetNumericField(2));
        Assert.Equal(841UL, spawn.GetNumericField(27));
        Assert.Equal(603UL, spawn.GetNumericField(28));
        Assert.Equal(1UL, spawn.GetNumericField(36));
        Assert.Equal("VisibleTarget", spawn.Names[0]);
        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction session=accountId=1400123", output);
        Assert.Contains("type=QuerySpawn", output);
        Assert.DoesNotContain("PARITY-GAP registered MsgAction", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
    }

    [Fact]
    public async Task HandlerDispatchesEndFlyAndClearsLegacyStatusFlags()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var statusFlags = BuildStatusFlags(LegacyFlyFlag, LegacyInfinityFlag, LegacyHeavensWrathFlag, LegacyFocusedFlag);
        var session = CreateTitleStorageSession(
            Path.GetTempPath(),
            "",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = statusFlags,
                ["HeavensWrath"] = "1",
            });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions);

        await registry.HandleAsync(
            context,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_400_123,
                Type = MsgActionType.EndFly,
            }.ToFrameBytes()));

        var frame = Assert.Single(sent);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update));
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, entry.Type);
        Assert.NotNull(entry.Values);
        Assert.False(HasLegacyStatusFlag(entry.Values!, LegacyFlyFlag));
        Assert.False(HasLegacyStatusFlag(entry.Values!, LegacyInfinityFlag));
        Assert.False(HasLegacyStatusFlag(entry.Values!, LegacyHeavensWrathFlag));
        Assert.True(HasLegacyStatusFlag(entry.Values!, LegacyFocusedFlag));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        var updatedFlags = updated.Character.Attributes["StatusFlags"];
        Assert.False(HasLegacyStatusFlag(updatedFlags, LegacyFlyFlag));
        Assert.False(HasLegacyStatusFlag(updatedFlags, LegacyInfinityFlag));
        Assert.False(HasLegacyStatusFlag(updatedFlags, LegacyHeavensWrathFlag));
        Assert.True(HasLegacyStatusFlag(updatedFlags, LegacyFocusedFlag));
        Assert.False(updated.Character.Attributes.ContainsKey("HeavensWrath"));
        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction EndFly", output);
        Assert.DoesNotContain("PARITY-GAP registered MsgAction", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
    }

    [Fact]
    public async Task HandlerDispatchesChangeLookfaceAndMoneyUpdate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var baseSession = CreateTitleStorageSession(Path.GetTempPath(), "");
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                Body = 1008,
                Money = 1_000,
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions);

        await registry.HandleAsync(
            context,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_400_123,
                DwParam = 250,
                Type = MsgActionType.ChangeLookface,
            }.ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var echoed));
        Assert.Equal(MsgActionType.ChangeLookface, echoed.Type);
        Assert.Equal(250U, echoed.DwParam);
        AssertSingleUpdate(sent[1], LegacyMsgUpdateDataType.Money, 500);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(50, updated.Character.Face);
        Assert.Equal(500, updated.Character.Money);
        Assert.Equal("50", updated.Character.Attributes["Face"]);
        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction ChangeLookface", output);
        Assert.DoesNotContain("PARITY-GAP registered MsgAction", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
    }

    [Fact]
    public async Task HandlerDispatchesCompleteLoginStartupBurstAndMarksSession()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateTitleStorageSession(Path.GetTempPath(), "");
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions);

        await registry.HandleAsync(
            context,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_400_123,
                Type = MsgActionType.CompleteLogin,
            }.ToFrameBytes()));

        Assert.Contains(sent, bytes => LegacyVipStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Contains(
            sent,
            bytes => LegacyUserAbilityScorePacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Type == LegacyUserAbilityScorePacket.ResponseType &&
                packet.Uid == 1_400_123 &&
                packet.Level == 140);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("1", updated.Character.Attributes["CompleteLogin"]);
        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction CompleteLogin", output);
        Assert.DoesNotContain("PARITY-GAP registered MsgAction", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
    }

    [Fact]
    public async Task HandlerDispatchesVendorStartupAndShutdownBoothFrames()
    {
        using var log = new StringWriter();
        var ownerSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var ownerConnection = CreateConnection(ownerSent);
        var peerConnection = CreateConnection(peerSent);
        var owner = CreateTitleStorageSession(Path.GetTempPath(), "");
        var peer = CreateTitleStorageSession(Path.GetTempPath(), "") with
        {
            Character = owner.Character with
            {
                Uid = 1_293_601,
                Name = "Peer",
                X = 836,
                Y = 596,
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(ownerConnection, owner);
        sessions.Set(peerConnection, peer);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(
            ownerConnection,
            log,
            owner,
            Sessions: sessions);

        await registry.HandleAsync(
            context,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_400_123,
                Type = MsgActionType.StartVendor,
            }.ToFrameBytes()));

        Assert.Equal(2, ownerSent.Count);
        Assert.Single(peerSent);
        Assert.True(LegacySobNpcSpawnPacket.TryParse(ReadFrame(ownerSent[0]), out var ownerSpawn));
        Assert.True(LegacySobNpcSpawnPacket.TryParse(ReadFrame(peerSent[0]), out var peerSpawn));
        Assert.Equal(ownerSpawn, peerSpawn);
        Assert.Equal(1_500_123U, ownerSpawn.Uid);
        Assert.Equal(836, ownerSpawn.X);
        Assert.Equal(596, ownerSpawn.Y);
        Assert.Equal(406, ownerSpawn.Mesh);
        Assert.Equal(14, ownerSpawn.Type);
        Assert.Equal("FixturePlayer", ownerSpawn.Name);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(ownerSent[1]), out var startEcho));
        Assert.Equal(MsgActionType.StartVendor, startEcho.Type);
        Assert.Equal(1_500_123U, startEcho.DwParam);
        Assert.Equal(836U, startEcho.PositionX);
        Assert.Equal(596U, startEcho.PositionY);
        Assert.True(sessions.TryGet(ownerConnection, out var vendoring));
        Assert.NotNull(vendoring);
        Assert.Equal("1500123", vendoring.Character.Attributes["RuntimeVendorUid"]);
        Assert.Same(LegacyVendorListingBook.Empty, vendoring.LegacyVendorListings);
        var latestVendoring = vendoring with
        {
            Character = vendoring.Character with { Money = vendoring.Character.Money + 123 },
        };
        sessions.Set(ownerConnection, latestVendoring);

        await registry.HandleAsync(
            context with { Session = vendoring },
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_400_123,
                Type = MsgActionType.StopVending,
            }.ToFrameBytes()));

        Assert.Equal(3, ownerSent.Count);
        Assert.Equal(2, peerSent.Count);
        Assert.True(LegacyRemoveEntityPacket.TryParse(ReadFrame(ownerSent[2]), out var ownerRemove));
        Assert.True(LegacyRemoveEntityPacket.TryParse(ReadFrame(peerSent[1]), out var peerRemove));
        Assert.Equal(1_500_123U, ownerRemove.Uid);
        Assert.Equal(ownerRemove, peerRemove);
        Assert.True(sessions.TryGet(ownerConnection, out var stopped));
        Assert.NotNull(stopped);
        Assert.False(stopped.Character.Attributes.ContainsKey("RuntimeVendorUid"));
        Assert.Equal(latestVendoring.Character.Money, stopped.Character.Money);
        Assert.Null(stopped.LegacyVendorListings);
        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction StartVendor", output);
        Assert.Contains("WorldServer handled MsgAction StopVending", output);
        Assert.DoesNotContain("PARITY-GAP registered MsgAction", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
    }

    [Fact]
    public async Task HandlerDispatchesHotkeysMsgActionAsSourceBackedEcho()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new MsgActionPacket
        {
            ObjId = 1_400_123,
            Timestamp = 508_455_344,
            Type = MsgActionType.Hotkeys,
        };

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            ReadFrame(packet.ToFrameBytes()));

        Assert.Single(sent);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var echoed));
        Assert.Equal(MsgActionType.Hotkeys, echoed.Type);
        Assert.Equal(1_400_123U, echoed.ObjId);

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction session=none type=Hotkeys dwParam=0", output);
        Assert.Contains("WorldServer echoed source-backed MsgAction Hotkeys session=none typeId=75", output);
        Assert.Contains("legacy MsgDataPacket.Hotkeys [Data(75)]", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
        Assert.DoesNotContain("WorldServer received opcode 2046", output);
    }

    [Fact]
    public async Task HandlerDispatchesConfirmAssociatesMsgActionAsSourceBackedEcho()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new MsgActionPacket
        {
            ObjId = 1_400_123,
            DwParam = 42,
            Timestamp = 508_455_344,
            Type = MsgActionType.ConfirmAssociates,
            PositionX = 830,
            PositionY = 584,
        };

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            ReadFrame(packet.ToFrameBytes()));

        Assert.Single(sent);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var echoed));
        Assert.Equal(MsgActionType.ConfirmAssociates, echoed.Type);
        Assert.Equal(packet.ObjId, echoed.ObjId);
        Assert.Equal(packet.DwParam, echoed.DwParam);
        Assert.Equal(packet.Timestamp, echoed.Timestamp);
        Assert.Equal(packet.PositionX, echoed.PositionX);
        Assert.Equal(packet.PositionY, echoed.PositionY);

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction session=none type=ConfirmAssociates dwParam=42", output);
        Assert.Contains("WorldServer echoed source-backed MsgAction ConfirmAssociates session=none typeId=76", output);
        Assert.Contains("legacy MsgDataPacket.ConfirmAssociates [Data(76)]", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
        Assert.DoesNotContain("gap=movement-action", output);
        Assert.DoesNotContain("PARITY-GAP registered MsgAction", output);
        Assert.DoesNotContain("WorldServer received opcode 2046", output);
    }

    [Fact]
    public async Task HandlerDispatchesMsgActionChangeStanceThroughRegistry()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_400_123,
                DwParam = 100,
                Timestamp = 123_456,
                Type = MsgActionType.ChangeStance,
                PositionX = 775,
                PositionY = 548,
            }.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction session=none type=ChangeStance dwParam=100", output);
        Assert.Contains("WorldServer skipped MsgAction ChangeStance session=none; no character session is available.", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
        Assert.DoesNotContain("WorldServer rejected malformed MsgAction", output);
        Assert.DoesNotContain("WorldServer received opcode 2046", output);
    }

    [Fact]
    public async Task HandlerDispatchesMsgActionChangeDirectionUpdatesAngleWithoutFrames()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateTitleStorageSession(
            Path.GetTempPath(),
            "",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Angle"] = "2",
            });
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions);

        await registry.HandleAsync(
            context,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_400_123,
                Timestamp = 123_456,
                Type = MsgActionType.ChangeDirection,
                Facing = 7,
                PositionX = 100,
                PositionY = 200,
            }.ToFrameBytes()));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("7", updated.Character.Attributes["Angle"]);
        Assert.Equal(835, updated.Character.X);
        Assert.Equal(596, updated.Character.Y);

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction session=accountId=1400123", output);
        Assert.Contains("type=ChangeDirection", output);
        Assert.Contains("WorldServer handled MsgAction ChangeDirection", output);
        Assert.Contains("angle=7", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
        Assert.DoesNotContain("WorldServer rejected malformed MsgAction", output);
    }

    [Fact]
    public async Task HandlerDispatchesMsgActionWithUnknownFieldsThroughRegistry()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(CreateMsgActionWithUnknownVarintField()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction", output);
        Assert.Contains("type=Revive", output);
        Assert.DoesNotContain("PARITY-GAP MsgAction ignored unknown proto fields", output);
        Assert.DoesNotContain("WorldServer rejected malformed MsgAction", output);
        Assert.DoesNotContain("WorldServer received opcode 2046", output);
    }

    [Fact]
    public async Task HandlerDispatchesUpdateInventorySashWithoutGenericUnmodeledWarning()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            ReadFrame(new MsgActionPacket
            {
                Type = MsgActionType.UpdateInventorySash,
            }.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction session=none type=UpdateInventorySash", output);
        Assert.Contains("WorldServer skipped MsgAction UpdateInventorySash; no active world session.", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
        Assert.DoesNotContain("WorldServer received opcode 2046", output);
    }

    [Fact]
    public async Task HandlerDispatchesAwayWithoutGenericUnmodeledWarning()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_400_123,
                DwParam = 1,
                Type = MsgActionType.Away,
            }.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction session=none type=Away", output);
        Assert.Contains("WorldServer skipped MsgAction Away; no active world session.", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
        Assert.DoesNotContain("WorldServer received opcode 2046", output);
    }

    [Fact]
    public async Task HandlerDispatchesMsgQuestDataThroughRegistry()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new MsgQuestDataPacket(Unknown: 7, Uid: 35_024, Intent: 99);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(packet.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgQuestData", output);
        Assert.Contains("unknown=7", output);
        Assert.Contains("uid=35024", output);
        Assert.Contains("intent=99", output);
        Assert.DoesNotContain("WorldServer received opcode 2136", output);
    }

    [Fact]
    public async Task HandlerDispatchesLatestClientMsgQuestDataQueryThroughRegistry()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        // One fixed-layout task: control, record count, UID, then seven intentions.
        var writer = new PacketWriter(WorldPacketOpcodes.MsgQuestData, payloadLength: 36);
        writer.WriteUInt16(0, 0);
        writer.WriteUInt16(2, 1);
        writer.WriteUInt32(4, 7001);
        for (var index = 0; index < 7; index++)
        {
            writer.WriteUInt32(8 + index * sizeof(uint), (uint)(index + 1));
        }

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgQuestData", output);
        Assert.Contains("layout=latest-client-task-detail", output);
        Assert.Contains("control=0", output);
        Assert.Contains("recordCount=1", output);
        Assert.Contains("taskUids=7001", output);
        Assert.Contains("incomingIntentions=7001:[1,2,3,4,5,6,7]", output);
        Assert.Contains("payloadLength=36", output);
        Assert.Contains("runtimeQuestsFound=0", output);
        Assert.Contains("responses=0", output);
        Assert.DoesNotContain("rejected malformed MsgQuestData", output);
        Assert.DoesNotContain("WorldServer received opcode 2136", output);
    }

    [Fact]
    public async Task HandlerDispatchesMsgMovementThroughRegistry()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new MsgMovementPacket
        {
            Direction = 5,
            Uid = 1_400_123,
            Running = MsgMovementMode.Walk,
            Timestamp = 1234,
        };

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(packet.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgMovement", output);
        Assert.Contains("uid=1400123", output);
        Assert.Contains("direction=5", output);
        Assert.Contains("running=Walk", output);
        Assert.DoesNotContain("WorldServer received opcode 2205", output);
    }

    [Fact]
    public async Task HandlerDispatchesMsgMovementWithUnknownFieldsThroughRegistry()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(CreateMsgMovementWithUnknownVarintField()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgMovement", output);
        Assert.Contains("uid=1400123", output);
        Assert.Contains("direction=5", output);
        Assert.Contains("running=Walk", output);
        Assert.DoesNotContain("PARITY-GAP MsgMovement ignored unknown proto fields", output);
        Assert.DoesNotContain("WorldServer rejected malformed MsgMovement", output);
        Assert.DoesNotContain("WorldServer received opcode 2205", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedRealPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.MsgTeam, payloadLength: 3);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed MsgTeam packet session=none.", output);
        Assert.Contains("opcode=2243 payloadLength=3", output);
        Assert.DoesNotContain("WorldServer received opcode 2243", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedMsgQuestDataPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.MsgQuestData, payloadLength: 8);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed MsgQuestData packet session=none.", output);
        Assert.DoesNotContain("WorldServer received opcode 2136", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyNobilityRankListenThroughRegistry()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new LegacyNobilityRequestPacket(
            LegacyNobilityAction.RankListen,
            Value: 2,
            DonationType: 0);

        await handler.HandleFrameAsync(CreateConnection(sent), ReadFrame(packet.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyNobility", output);
        Assert.Contains("action=RankListen", output);
        Assert.Contains("value=2", output);
        Assert.Contains("WorldServer sent LegacyNobility rank list", output);
        Assert.DoesNotContain("WorldServer received opcode 2091", output);
        Assert.Single(sent);
        Assert.True(LegacyNobilityRankListPacket.TryParse(ReadFrame(sent[0]), out var response));
        Assert.Equal((ushort)2, response.Page);
        Assert.Equal((ushort)0, response.TotalCount);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyNobilityRankListenWithActiveSessionRanking()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateTitleStorageSession(
            Path.Combine(Path.GetTempPath(), "wolf-nobility-dispatch-" + Guid.NewGuid(), "Users"),
            "",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DonationNobility"] = "250000000"
            });
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions);
        var packet = new LegacyNobilityRequestPacket(
            LegacyNobilityAction.RankListen,
            Value: 0,
            DonationType: 0);

        await registry.HandleAsync(context, ReadFrame(packet.ToFrameBytes()));

        Assert.Single(sent);
        Assert.True(LegacyNobilityRankListPacket.TryParse(ReadFrame(sent[0]), out var response));
        Assert.Equal((ushort)0, response.Page);
        Assert.Equal((ushort)1, response.TotalCount);
        var entry = Assert.Single(response.Entries);
        Assert.Equal(1_400_123U, entry.Uid);
        Assert.Equal(250_000_000UL, entry.Donation);
        Assert.Equal((byte)12, entry.Rank);
        Assert.Equal(0, entry.Position);

        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacyNobility rank list", output);
        Assert.Contains("entries=1", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyNobilityInformationThroughRegistry()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new LegacyNobilityRequestPacket(
            LegacyNobilityAction.NobilityInformation,
            Value: 0,
            DonationType: 0);

        await handler.HandleFrameAsync(CreateConnection(sent), ReadFrame(packet.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyNobility", output);
        Assert.Contains("action=NobilityInformation", output);
        Assert.Contains("WorldServer sent LegacyNobility information", output);
        Assert.DoesNotContain("WorldServer received opcode 2091", output);
        Assert.Single(sent);
        Assert.True(LegacyNobilityInformationPacket.TryParse(ReadFrame(sent[0]), out _));
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyNobilityPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNobilityIcon, payloadLength: 3);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyNobility packet session=none.", output);
        Assert.DoesNotContain("WorldServer received opcode 2091", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyDragonSkinThroughRegistryWithoutSession()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new LegacyDragonSkinPacket(
            LegacyDragonSkinType.MyShow,
            UserID: 1_400_123,
            SkinType: 0,
            OK: false);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(packet.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyDragonSkin", output);
        Assert.Contains("type=MyShow", output);
        Assert.Contains("userId=1400123", output);
        Assert.Contains("WorldServer skipped LegacyDragonSkin session=none; no character session is available.", output);
        Assert.DoesNotContain("WorldServer received opcode 2415", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyDragonSkinPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyAnimaSkin, payloadLength: 1);
        writer.Payload[0] = 0x08;

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyDragonSkin packet session=none.", output);
        Assert.DoesNotContain("WorldServer received opcode 2415", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyPlayerStatusThroughRegistryWithoutSession()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            ReadFrame(new LegacyPlayerStatusQueryPacket(1_400_123).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer skipped LegacyPlayerStatus session=none uid=1400123; no character session is available.", output);
        Assert.DoesNotContain("WorldServer received opcode 2301", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyPlayerStatusPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyPlayerStatus, payloadLength: 3);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyPlayerStatus packet session=none.", output);
        Assert.Contains("opcode=2301 payloadLength=3 payloadSampleHex=000000", output);
        Assert.DoesNotContain("WorldServer received opcode 2301", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyGenericRankingThroughRegistryWithoutSession()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new LegacyGenericRankingPacket(
            LegacyGenericRankingAction.QueryCount,
            LegacyGenericRankingRankType.DragonChi,
            RegisteredCount: 0,
            Page: 0,
            Finish: 0,
            Count: 0,
            Reserved: 0,
            Entries: []);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(packet.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer skipped LegacyGenericRanking session=none action=QueryCount rankType=DragonChi; no character session is available.", output);
        Assert.DoesNotContain("WorldServer received opcode 2165", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyGenericRankingHeaderOnlyRequest()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(
            WorldPacketOpcodes.LegacyGenericRanking,
            LegacyGenericRankingPacket.PayloadLengthForEntryCount(0));
        writer.Payload[0] = (byte)LegacyGenericRankingAction.QueryCount;
        writer.WriteUInt32(1, (uint)LegacyGenericRankingRankType.DragonChi);
        writer.WriteInt32(9, 1);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer skipped LegacyGenericRanking session=none action=QueryCount rankType=DragonChi; no character session is available.", output);
        Assert.DoesNotContain("WorldServer received opcode 2165", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyGenericRankingPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyGenericRanking, payloadLength: 12);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyGenericRanking packet session=none.", output);
        Assert.Contains("opcode=2165 payloadLength=12", output);
        Assert.DoesNotContain("WorldServer received opcode 2165", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyHeroRewardThroughRegistryWithoutSession()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            ReadFrame(new LegacyHeroRewardPacket(LegacyHeroRewardAction.Show, Prize: 5).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains(
            "WorldServer skipped LegacyHeroReward session=none action=Show prize=5; no character session is available.",
            output);
        Assert.DoesNotContain("WorldServer received opcode 2077", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyHeroRewardPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyHeroReward, payloadLength: 4);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyHeroReward packet session=none.", output);
        Assert.Contains("opcode=2077 payloadLength=4 payloadSampleHex=00000000", output);
        Assert.DoesNotContain("WorldServer received opcode 2077", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyExchangeShopGoodsInfoThroughRegistryWithoutSession()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new LegacyExchangeShopGoodsInfoPacket(
            LegacyExchangeShopGoodsInfoType.Show,
            NpcID: 123,
            ShopID: 168);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(packet.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains(
            "WorldServer skipped LegacyExchangeShopGoodsInfo session=none type=Show npcId=123 shopId=168; no character session is available.",
            output);
        Assert.DoesNotContain("WorldServer received opcode 2212", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyExchangeShopGoodsInfoPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyExchangeShopGoodsInfo, payloadLength: 1);
        writer.Payload[0] = 0x08;

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyExchangeShopGoodsInfo packet session=none.", output);
        Assert.Contains("opcode=2212 payloadLength=1 payloadSampleHex=08", output);
        Assert.DoesNotContain("WorldServer received opcode 2212", output);
    }

    [Fact]
    public async Task HandlerDispatchesConstructedLegacyExchangeShopGoodsInfoOpenWithoutSession()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var frame = CreateProtoFixtureFrame(WorldPacketOpcodes.LegacyExchangeShopGoodsInfo,
            (1, 3), (2, 77), (3, 0));

        await handler.HandleFrameAsync(CreateConnection(), frame);

        var output = log.ToString();
        Assert.Contains(
            "WorldServer skipped LegacyExchangeShopGoodsInfo session=none type=Open npcId=77 shopId=0; no character session is available.",
            output);
        Assert.DoesNotContain("WorldServer rejected malformed LegacyExchangeShopGoodsInfo", output);
        Assert.DoesNotContain("WorldServer received opcode 2212", output);
    }

    [Fact]
    public async Task DispatchRegistryCanClassifyLegacyExchangeShopBuyOpcode()
    {
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes(
            [typeof(LegacyExchangeShopBuyDispatchProbeHandlers)]);
        var packet = new LegacyExchangeShopBuyPacket(
            LegacyExchangeShopBuyType.Goods,
            NpcID: 24_702,
            ShopID: 168,
            Count: 2,
            Index: 444,
            Items: [new LegacyExchangeShopBuyItem(ID: 3_303_526, Count: 4)]);

        Assert.Contains(WorldPacketOpcodes.LegacyExchangeShopBuy, registry.Opcodes);
        Assert.True(registry.TryGetHandler(WorldPacketOpcodes.LegacyExchangeShopBuy, out _));

        await registry.HandleAsync(
            new WorldPacketHandlerContext(CreateConnection(), log),
            ReadFrame(packet.ToFrameBytes()));

        Assert.Contains(
            "WorldServer classified LegacyExchangeShopBuy session=none type=Goods npcId=24702 shopId=168 count=2 index=444 items=1.",
            log.ToString());
    }

    [Fact]
    public async Task HandlerDispatchesLegacyGuildProcessThroughRegistryWithoutSession()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            ReadFrame(LegacyGuildProcessPacket.Create(LegacyGuildProcessAction.InfoName, uid: 1370).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains(
            "WorldServer skipped LegacyGuildProcess session=none action=InfoName uid=1370; no character session is available.",
            output);
        Assert.DoesNotContain("WorldServer received opcode 2167", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyGuildProcessPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyGuildProcess, payloadLength: 1);
        writer.Payload[0] = 0x08;

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyGuildProcess packet session=none.", output);
        Assert.DoesNotContain("WorldServer received opcode 2167", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyCoatStorageThroughRegistryWithoutSession()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new LegacyCoatStoragePacket(
            ActionId: 1,
            DwParam1: 123,
            DwParam2: 0,
            DwParam3: 0,
            Items: []);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(packet.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains(
            "WorldServer skipped LegacyCoatStorage session=none action=1 dwParam1=123 dwParam2=0 dwParam3=0 items=0; no character session is available.",
            output);
        Assert.DoesNotContain("WorldServer received opcode 2220", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyCoatStoragePacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyCoatStorage, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x01;

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyCoatStorage packet session=none.", output);
        Assert.Contains("opcode=2220 payloadLength=2 payloadSampleHex=0801", output);
        Assert.DoesNotContain("WorldServer received opcode 2220", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacySubClassThroughRegistryWithoutSession()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            ReadFrame(new LegacySubClassPacket(LegacySubClassAction.Open, SubClassId: 0).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains(
            "WorldServer skipped LegacySubClass session=none action=Open subclassId=0; no character session is available.",
            output);
        Assert.DoesNotContain("WorldServer received opcode 2033", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacySubClassPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacySubClass, payloadLength: 2);
        writer.WriteUInt16(0, (ushort)LegacySubClassAction.Open);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacySubClass packet session=none.", output);
        Assert.Contains("opcode=2033 payloadLength=2 payloadSampleHex=0600", output);
        Assert.DoesNotContain("WorldServer received opcode 2033", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyBlessingInfoThroughRegistryWithoutSession()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            ReadFrame(new LegacyBlessingInfoPacket(LegacyBlessingInfoAction.Show, 0, 0).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains(
            "WorldServer skipped LegacyBlessingInfo session=none action=Show; no character session is available.",
            output);
        Assert.DoesNotContain("WorldServer received opcode 2258", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyBlessingInfoPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyBlessingInfo, payloadLength: 4);
        writer.WriteUInt32(0, (uint)LegacyBlessingInfoAction.Show);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyBlessingInfo packet session=none.", output);
        Assert.Contains("opcode=2258 payloadLength=4 payloadSampleHex=00000000", output);
        Assert.DoesNotContain("WorldServer received opcode 2258", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyClaimThroughRegistryWithoutSession()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            ReadFrame(LegacyClaimPacket.Show(1_400_123, 0, 0, 0, 0, 0).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains(
            "WorldServer skipped LegacyClaim session=none mode=5; no character session is available.",
            output);
        Assert.DoesNotContain("WorldServer received opcode 2143", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyClaimPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyClaim, payloadLength: 4);
        writer.WriteUInt32(0, (uint)LegacyClaimAction.Show);

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyClaim packet session=none.", output);
        Assert.Contains("opcode=2143 payloadLength=4 payloadSampleHex=05000000", output);
        Assert.DoesNotContain("WorldServer received opcode 2143", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyHundredWeaponsInfoThroughRegistryWithoutSession()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            ReadFrame(LegacyHundredWeaponsInfoPacket.Request(ownerUid: 1_400_123).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains(
            "WorldServer skipped LegacyHundredWeaponsInfo session=none type=RequestInfo ownerUid=1400123; no character session is available.",
            output);
        Assert.DoesNotContain("WorldServer received opcode 2352", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyHundredWeaponsInfoPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyHundredWeaponsInfo, payloadLength: 2);
        writer.Payload[0] = 0x10;
        writer.Payload[1] = 0x01;

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyHundredWeaponsInfo packet session=none.", output);
        Assert.Contains("opcode=2352 payloadLength=2 payloadSampleHex=1001", output);
        Assert.DoesNotContain("WorldServer received opcode 2352", output);
    }

    [Fact]
    public async Task HandlerDispatchesConstructedSourceSilentLegacy2029ThroughRegistry()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            CreateProtoFixtureFrame(WorldPacketOpcodes.LegacySourceSilent2029, (1, 11), (4, 2), (5, 9)));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains(
            "WorldServer source-silent legacy packet session=none opcode=2029 payloadLength=6.",
            output);
        Assert.Contains("sourceName=CMsgRedEnvelops", output);
        Assert.Contains("MrRayzo.Packets catalog", output);
        Assert.Contains("opcode=2029 payloadLength=6 payloadSampleHex=080B20022809", output);
        Assert.DoesNotContain("WorldServer received opcode 2029", output);
        Assert.DoesNotContain("unknown world opcode", output);
    }

    [Fact]
    public async Task HandlerDispatchesSourceSilentLegacy2196ThroughRegistry()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            CreateProtoFixtureFrame(2196, (1, 11)));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains(
            "WorldServer source-silent legacy packet session=none opcode=2196 payloadLength=2.",
            output);
        Assert.Contains("sourceName=CMsgTexasExInteractive", output);
        Assert.Contains("MrRayzo.Packets catalog", output);
        Assert.DoesNotContain("WorldServer received opcode 2196", output);
    }

    [Fact]
    public async Task HandlerDispatchesSourceSilentLegacy2383ThroughRegistry()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            CreateProtoFixtureFrame(2383, (1, 0), (2, 271)));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains(
            "WorldServer source-silent legacy packet session=none opcode=2383 payloadLength=5.",
            output);
        Assert.Contains("sourceName=unmapped", output);
        Assert.Contains("no [Packet] attribute", output);
        Assert.DoesNotContain("WorldServer received opcode 2383", output);
    }

    [Fact]
    public async Task HandlerDispatchesSyntheticSourceSilent2369WithSpecificSourceEvidence()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        // This handler treats the payload as opaque; use a fresh non-capture pattern.
        var payload = Enumerable.Range(0, 74).Select(index => (byte)(17 + index * 3)).ToArray();
        var writer = new PacketWriter(WorldPacketOpcodes.LegacySourceSilent2369, payloadLength: 74);
        payload.AsSpan().CopyTo(writer.Payload);

        await handler.HandleFrameAsync(CreateConnection(sent), ReadFrame(writer.ToArray()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains(
            "WorldServer source-silent legacy packet session=none opcode=2369 payloadLength=74.",
            output);
        Assert.Contains("sourceName=unmapped", output);
        Assert.Contains("opcode 2369 is absent from MrRayzo.Game.GamePackets", output);
        Assert.Contains("captured 4 source-silent frames with payloadLength=74", output);
        Assert.Contains(
            "payloadSampleHex=" + Convert.ToHexString(payload.AsSpan(0, 32)) + "...",
            output);
        Assert.DoesNotContain("WorldServer received opcode 2369", output);
        Assert.DoesNotContain("unknown world opcode", output);
    }

    [Theory]
    [InlineData(17, 1)]
    [InlineData(29, 2)]
    [InlineData(43, 3)]
    public async Task HandlerDispatchesSyntheticSourceSilent2433PayloadsWithSpecificSourceEvidence(
        uint operation,
        uint argument)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var frame = CreateProtoFixtureFrame(WorldPacketOpcodes.LegacySourceSilent2433,
            (1, operation), (2, argument));

        await handler.HandleFrameAsync(CreateConnection(sent), frame);

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains(
            "WorldServer source-silent legacy packet session=none opcode=2433 payloadLength=4.",
            output);
        Assert.Contains("sourceName=unmapped", output);
        Assert.Contains("opcode 2433 is absent from MrRayzo.Game.GamePackets", output);
        Assert.Contains("captured 9 source-silent frames with payloadLength=4", output);
        Assert.Contains($"payloadSampleHex={Convert.ToHexString(frame.Payload.ToArray())}", output);
        Assert.DoesNotContain("WorldServer received opcode 2433", output);
        Assert.DoesNotContain("unknown world opcode", output);
    }

    [Fact]
    public async Task HandlerDispatchesLiveSourceSilent2311StartupFrameThroughRegistry()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacySourceSilent2311, payloadLength: 516);

        await handler.HandleFrameAsync(CreateConnection(sent), ReadFrame(writer.ToArray()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains(
            "WorldServer source-silent legacy packet session=none opcode=2311 payloadLength=516.",
            output);
        Assert.Contains("sourceName=unmapped", output);
        Assert.Contains("opcode 2311 has no [Packet(2311)] inbound handler", output);
        Assert.Contains("MsgMessage.cs command ttp writes 21 literal payload bytes and calls Finalize(2311)", output);
        Assert.Contains("this serializer does not establish an inbound handler", output);
        Assert.Contains("observed an all-zero 516-byte client frame", output);
        Assert.Contains(
            "payloadSampleHex=0000000000000000000000000000000000000000000000000000000000000000...",
            output);
        Assert.DoesNotContain("WorldServer received opcode 2311", output);
        Assert.DoesNotContain("unknown world opcode", output);
    }

    [Theory]
    [InlineData(WorldPacketOpcodes.LegacyRedEnvelops, "CMsgRedEnvelops")]
    [InlineData(WorldPacketOpcodes.LegacyTaskReward, "CMsgTaskReward")]
    [InlineData(WorldPacketOpcodes.LegacyAuctionItem, "CMsgAuctionItem")]
    [InlineData(WorldPacketOpcodes.LegacyPing, "CMsgPing")]
    [InlineData(WorldPacketOpcodes.LegacyExpPool, "CMsgExpPool")]
    [InlineData(WorldPacketOpcodes.LegacySourceSilent2311, "unmapped")]
    [InlineData(WorldPacketOpcodes.LegacySourceSilent2357, "unmapped")]
    [InlineData(WorldPacketOpcodes.LegacySourceSilent2369, "unmapped")]
    [InlineData(WorldPacketOpcodes.LegacySourceSilent2382, "unmapped")]
    [InlineData(WorldPacketOpcodes.LegacySourceSilent2417, "unmapped")]
    [InlineData(WorldPacketOpcodes.LegacySourceSilent2424, "unmapped")]
    [InlineData(WorldPacketOpcodes.LegacySourceSilent2433, "unmapped")]
    [InlineData(WorldPacketOpcodes.LegacySourceSilent2456, "unmapped")]
    [InlineData(WorldPacketOpcodes.LegacySourceSilent2469, "unmapped")]
    public async Task HandlerDispatchesSourceSilentLiveOpcodesWithSourceEvidence(ushort opcode, string sourceName)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(opcode, payloadLength: 3);
        writer.Payload[0] = 0xAA;
        writer.Payload[1] = 0xBB;
        writer.Payload[2] = 0xCC;

        await handler.HandleFrameAsync(CreateConnection(sent), ReadFrame(writer.ToArray()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains(
            $"WorldServer source-silent legacy packet session=none opcode={opcode} payloadLength=3.",
            output);
        Assert.Contains($"sourceName={sourceName}", output);
        Assert.DoesNotContain($"WorldServer received opcode {opcode}", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyTradeThroughRegistryWithoutUnknownFallback()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            ReadFrame(new LegacyTradePacket(Type: 2).ToFrameBytes()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyTrade session=none type=2;", output);
        Assert.Contains("sourceName=CMsgTrade", output);
        Assert.DoesNotContain("PARITY-GAP unknown world opcode", output);
        Assert.DoesNotContain("WorldServer received opcode 2119", output);
    }

    [Fact]
    public async Task HandlerDispatchesConstructedLegacyMsgTitleStorageThroughRegistryWithoutSession()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            CreateSyntheticLegacyMsgTitleStorageEquipFrame());

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains(
            "WorldServer skipped LegacyMsgTitleStorage session=none action=Equip dwParam1=0 dwParam2=8007 dwParam3=8007; no character session is available.",
            output);
        Assert.Contains("sourceName=CMsgTitleStorage", output);
        Assert.Contains("MsgTitleStorage defines [Packet(2313)]", output);
        Assert.Contains("Finalize(2313)", output);
        Assert.DoesNotContain("PARITY-GAP unknown world opcode", output);
        Assert.DoesNotContain("WorldServer received opcode 2313", output);
    }

    [Fact]
    public async Task HandlerAppliesLegacyMsgTitleStorageEquipFromSourcePacket()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-title-storage-dispatch-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            File.WriteAllText(
                Path.Combine(dbRoot, "title_type.txt"),
                "8007@@8007@@Synthetic Wing@@0@@0@@0@@0@@500@@");
            using var log = new StringWriter();
            var sent = new List<byte[]>();
            var connection = CreateConnection(sent);
            var sessions = new WorldSessionStore();
            var snapshotPath = WriteTitleStorageSnapshot(
                usersRoot,
                "1,8007,0,",
                """
                SpecialWingID=0
                SpecialTitleID=0
                SpecialTitleScore=0
                """);
            var session = CreateTitleStorageSession(usersRoot, "1,8007,0,");
            session = session with { Character = CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath), snapshotPath) };
            sessions.Set(connection, session);
            var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            var context = new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions) { TradeRecovery = new LegacyTradeRecoveryState(usersRoot, null) };

            await registry.HandleAsync(context, CreateSyntheticLegacyMsgTitleStorageEquipFrame());

            Assert.Equal(2, sent.Count);
            Assert.True(LegacyTitleStoragePacket.TryParse(ReadFrame(sent[0]), out var echo));
            Assert.Equal(LegacyTitleStorageAction.Equip, echo.Action);
            Assert.Equal(8007U, echo.DwParam2);
            Assert.Equal(8007U, echo.DwParam3);
            Assert.True(LegacyTitleStoragePacket.TryParse(ReadFrame(sent[1]), out var useTitle));
            Assert.Equal(LegacyTitleStorageAction.UseTitle, useTitle.Action);
            Assert.Equal(new LegacyTitleStorageTitle(8007, 8007, DwParam1: 1), useTitle.Title);
            Assert.True(sessions.TryGet(connection, out var updated));
            Assert.NotNull(updated);
            Assert.Equal("80078007", updated.Character.Attributes["SpecialWingID"]);
            Assert.Equal("1,8007,1,", updated.Character.Attributes["SpecialTitles"]);
            var persisted = CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath));
            Assert.Equal("80078007", persisted.Attributes["SpecialWingID"]);
            Assert.Equal("0", persisted.Attributes["SpecialTitleID"]);
            Assert.Equal("0", persisted.Attributes["SpecialTitleScore"]);
            Assert.Equal("1,8007,1,", persisted.Attributes["SpecialTitles"]);
            var activeState = LegacyTitleStoragePacket.ResolveActiveSpecialTitleState(persisted, usersRoot);
            Assert.Equal(80078007UL, activeState.SpecialWingId);

            var output = log.ToString();
            Assert.Contains("WorldServer handled LegacyMsgTitleStorage", output);
            Assert.Contains("WorldServer persisted LegacyMsgTitleStorage", output);
            Assert.Contains("action=Equip", output);
            Assert.Contains("sourceName=CMsgTitleStorage", output);
            Assert.DoesNotContain("PARITY-GAP unknown world opcode", output);
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerPersistsLegacyMsgTitleStorageUnEquipFromSourcePacket()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-title-storage-unequip-dispatch-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            File.WriteAllText(
                Path.Combine(dbRoot, "title_type.txt"),
                "8007@@8007@@Synthetic Wing@@0@@0@@0@@0@@500@@");
            var snapshotPath = WriteTitleStorageSnapshot(
                usersRoot,
                "1,8007,1,",
                """
                SpecialWingID=80078007
                SpecialTitleID=0
                SpecialTitleScore=0
                """);
            using var log = new StringWriter();
            var sent = new List<byte[]>();
            var connection = CreateConnection(sent);
            var sessions = new WorldSessionStore();
            var session = CreateTitleStorageSession(
                usersRoot,
                "1,8007,1,",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SpecialWingID"] = "80078007",
                    ["SpecialTitleID"] = "0",
                    ["SpecialTitleScore"] = "0",
                });
            session = session with { Character = CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath), snapshotPath) };
            sessions.Set(connection, session);
            var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            var context = new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions) { TradeRecovery = new LegacyTradeRecoveryState(usersRoot, null) };
            var request = new LegacyTitleStoragePacket(
                LegacyTitleStorageAction.UnEquip,
                DwParam1: 0,
                DwParam2: 8007,
                DwParam3: 8007);

            await registry.HandleAsync(context, ReadFrame(request.ToFrameBytes()));

            Assert.Single(sent);
            Assert.True(LegacyTitleStoragePacket.TryParse(ReadFrame(sent[0]), out var echo));
            Assert.Equal(LegacyTitleStorageAction.UnEquip, echo.Action);
            Assert.True(sessions.TryGet(connection, out var updated));
            Assert.NotNull(updated);
            Assert.Equal("0", updated.Character.Attributes["SpecialWingID"]);
            Assert.Equal("1,8007,0,", updated.Character.Attributes["SpecialTitles"]);
            var persisted = CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath));
            Assert.Equal("0", persisted.Attributes["SpecialWingID"]);
            Assert.Equal("0", persisted.Attributes["SpecialTitleID"]);
            Assert.Equal("0", persisted.Attributes["SpecialTitleScore"]);
            Assert.Equal("1,8007,0,", persisted.Attributes["SpecialTitles"]);
            var activeState = LegacyTitleStoragePacket.ResolveActiveSpecialTitleState(persisted, usersRoot);
            Assert.Equal(0UL, activeState.SpecialWingId);

            var output = log.ToString();
            Assert.Contains("WorldServer persisted LegacyMsgTitleStorage", output);
            Assert.Contains("action=UnEquip", output);
            Assert.Contains("sourceName=CMsgTitleStorage", output);
            Assert.DoesNotContain("PARITY-GAP unknown world opcode", output);
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerDispatchesLegacyCaptureTheFlagInitializeRankAsInactiveResponse()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new LegacyCaptureTheFlagUpdatePacket(
            LegacyCaptureTheFlagUpdateMode.InitializeRank,
            Parameter: 0,
            Parameter2: 0,
            Count: 0);

        await handler.HandleFrameAsync(CreateConnection(sent), ReadFrame(packet.ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyCaptureTheFlagUpdatePacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(LegacyCaptureTheFlagUpdateMode.InitializeRank, response.Mode);
        Assert.Equal(0U, response.Parameter);
        Assert.Equal(0, response.Parameter2);
        Assert.Equal(1, response.Count);

        var output = log.ToString();
        Assert.Contains(
            "WorldServer sent LegacyCaptureTheFlagUpdate InitializeRank session=none active=0; source MsgCaptureTheFlagUpdate.InitializeRank inactive CaptureTheFlag state modeled.",
            output);
        Assert.DoesNotContain("WorldServer deferred LegacyCaptureTheFlagUpdate", output);
        Assert.DoesNotContain("capture-the-flag source action is not modeled", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
        Assert.DoesNotContain("WorldServer received opcode 2062", output);
    }

    [Fact]
    public async Task HandlerSkipsLegacyCaptureTheFlagPlantFlagWithoutSession()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new LegacyCaptureTheFlagUpdatePacket(
            LegacyCaptureTheFlagUpdateMode.RemoveFlagEffect,
            Parameter: 1_400_123,
            Parameter2: 0,
            Count: 1);

        await handler.HandleFrameAsync(CreateConnection(sent), ReadFrame(packet.ToFrameBytes()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains(
            "WorldServer skipped LegacyCaptureTheFlagUpdate PlantFlag session=none mode=RemoveFlagEffect parameter=1400123 parameter2=0 count=1; no character session is available.",
            output);
        Assert.DoesNotContain("WorldServer deferred LegacyCaptureTheFlagUpdate", output);
        Assert.DoesNotContain("capture-the-flag source action is not modeled", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
        Assert.DoesNotContain("WorldServer received opcode 2062", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyCaptureTheFlagRemoveFlagEffectAsPlantFlag()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateCaptureTheFlagSession(
            statusFlags: BuildStatusFlags(LegacyFocusedFlag, LegacyCaptureTheFlagCarrierFlag),
            extraAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GuildID"] = "42",
                ["CaptureTheFlagBases"] = "9001,835,596,42,1,1",
                ["CaptureTheFlagExploits"] = "7",
                ["CaptureTheFlagGuildExploits"] = "40",
            });
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions);
        var packet = new LegacyCaptureTheFlagUpdatePacket(
            LegacyCaptureTheFlagUpdateMode.RemoveFlagEffect,
            Parameter: 1_400_123,
            Parameter2: 0,
            Count: 1);

        await registry.HandleAsync(context, ReadFrame(packet.ToFrameBytes()));

        Assert.Equal(4, sent.Count);
        Assert.True(LegacyCaptureTheFlagRankingPacket.TryParse(ReadFrame(sent[0]), out var ranking));
        Assert.Equal(LegacyCaptureTheFlagRankingAction.UpdatePoint, ranking.Action);
        Assert.Equal(1U, ranking.Page);
        Assert.Equal(27U, ranking.Parameter3);

        Assert.True(LegacyCaptureTheFlagUpdatePacket.TryParse(ReadFrame(sent[1]), out var timer));
        Assert.Equal(LegacyCaptureTheFlagUpdateMode.GenerateTimer, timer.Mode);
        Assert.Equal(0U, timer.Parameter);
        Assert.Equal(unchecked((ushort)1_400_123U), timer.Parameter2);
        Assert.Equal(1, timer.Count);

        Assert.True(LegacyCaptureTheFlagUpdatePacket.TryParse(ReadFrame(sent[2]), out var effect));
        Assert.Equal(LegacyCaptureTheFlagUpdateMode.GenerateEffect, effect.Mode);
        Assert.Equal(1_400_123U, effect.Parameter);

        Assert.True(LegacyCaptureTheFlagUpdatePacket.TryParse(ReadFrame(sent[3]), out var removeEffect));
        Assert.Equal(LegacyCaptureTheFlagUpdateMode.RemoveFlagEffect, removeEffect.Mode);
        Assert.Equal(1_400_123U, removeEffect.Parameter);
        Assert.Equal(1, removeEffect.Count);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("27", updated.Character.Attributes["CaptureTheFlagExploits"]);
        Assert.Equal("60", updated.Character.Attributes["CaptureTheFlagGuildExploits"]);
        Assert.True(HasLegacyStatusFlag(updated.Character.Attributes["StatusFlags"], LegacyFocusedFlag));
        Assert.False(HasLegacyStatusFlag(updated.Character.Attributes["StatusFlags"], LegacyCaptureTheFlagCarrierFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacyCaptureTheFlagUpdate PlantFlag", output);
        Assert.Contains("baseUid=9001", output);
        Assert.Contains("award=20", output);
        Assert.Contains("x2=1", output);
        Assert.Contains("exploits=27", output);
        Assert.Contains("source MsgCaptureTheFlag.PlantTheFlag modeled", output);
        Assert.DoesNotContain("WorldServer deferred LegacyCaptureTheFlagUpdate", output);
        Assert.DoesNotContain("capture-the-flag source action is not modeled", output);
        Assert.DoesNotContain("WorldServer received opcode 2062", output);
    }

    [Fact]
    public async Task HandlerSkipsLegacyCaptureTheFlagPlantFlagWhenSourceConditionsFail()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var statusFlags = BuildStatusFlags(LegacyCaptureTheFlagCarrierFlag);
        var session = CreateCaptureTheFlagSession(
            statusFlags,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GuildID"] = "42",
                ["CaptureTheFlagBases"] = "9001,835,596,99,0,1",
                ["CaptureTheFlagExploits"] = "7",
            });
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions);
        var packet = new LegacyCaptureTheFlagUpdatePacket(
            LegacyCaptureTheFlagUpdateMode.RemoveFlagEffect,
            Parameter: 1_400_123,
            Parameter2: 0,
            Count: 1);

        await registry.HandleAsync(context, ReadFrame(packet.ToFrameBytes()));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var unchanged));
        Assert.NotNull(unchanged);
        Assert.Equal(statusFlags, unchanged.Character.Attributes["StatusFlags"]);
        Assert.Equal("7", unchanged.Character.Attributes["CaptureTheFlagExploits"]);

        var output = log.ToString();
        Assert.Contains("WorldServer skipped LegacyCaptureTheFlagUpdate PlantFlag", output);
        Assert.Contains("source MsgCaptureTheFlag.PlantTheFlag conditions were not met", output);
        Assert.DoesNotContain("WorldServer deferred LegacyCaptureTheFlagUpdate", output);
        Assert.DoesNotContain("capture-the-flag source action is not modeled", output);
        Assert.DoesNotContain("WorldServer received opcode 2062", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyCaptureTheFlagUpdatePacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyCaptureTheFlagUpdate, payloadLength: 3);
        writer.Payload[0] = 0xAA;
        writer.Payload[1] = 0xBB;
        writer.Payload[2] = 0xCC;

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyCaptureTheFlagUpdate packet session=none.", output);
        Assert.Contains("opcode=2062 payloadLength=3 payloadSampleHex=AABBCC", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
        Assert.DoesNotContain("WorldServer received opcode 2062", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyTeamElitePkBracketsRequestInformationAsEmptySourceEcho()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new LegacyTeamElitePkBracketsPacket(
            LegacyTeamElitePkBracketAction.RequestInformation,
            Page: 1,
            PacketNumber: 3,
            MatchCount: 0,
            Group: 2,
            LegacyTeamElitePkBracketGuiType.Top8Ranking,
            TimeLeft: 15,
            PacketNumber2: 9,
            TotalMatches: 0);

        await handler.HandleFrameAsync(CreateConnection(sent), ReadFrame(packet.ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyTeamElitePkBracketsPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(packet with { PacketNumber2 = 0 }, response);

        var output = log.ToString();
        Assert.Contains(
            "WorldServer echoed source-backed LegacyTeamElitePkBrackets session=none action=RequestInformation page=1 packetNumber=3 group=2 gui=Top8Ranking matches=0 totalMatches=0 parsedMatchRows=0; no live team elite tournament matches are modeled.",
            output);
        Assert.DoesNotContain("WorldServer deferred LegacyTeamElitePkBrackets", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
        Assert.DoesNotContain("WorldServer received opcode 2198", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyTeamElitePkBracketsStaticUpdateAsSourceEchoNoOp()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var packet = new LegacyTeamElitePkBracketsPacket(
            LegacyTeamElitePkBracketAction.StaticUpdate,
            Page: 1,
            PacketNumber: 0,
            MatchCount: 1,
            Group: 2,
            LegacyTeamElitePkBracketGuiType.Top8Ranking,
            TimeLeft: 0,
            PacketNumber2: 0,
            TotalMatches: 1);

        await handler.HandleFrameAsync(CreateConnection(sent), ReadFrame(packet.ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyTeamElitePkBracketsPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(packet with { PacketNumber2 = 0 }, response);

        var output = log.ToString();
        Assert.Contains(
            "WorldServer echoed source-backed LegacyTeamElitePkBrackets session=none action=StaticUpdate page=1 packetNumber=0 group=2 gui=Top8Ranking matches=1 totalMatches=1 parsedMatchRows=0; no live team elite tournament matches are modeled.",
            output);
        Assert.DoesNotContain("WorldServer deferred LegacyTeamElitePkBrackets", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
        Assert.DoesNotContain("WorldServer received opcode 2198", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyTeamElitePkBracketsPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyTeamElitePkBrackets, payloadLength: 2);
        writer.Payload[0] = 0x04;
        writer.Payload[1] = 0x00;

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyTeamElitePkBrackets packet session=none.", output);
        Assert.Contains("opcode=2198 payloadLength=2 payloadSampleHex=0400", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
        Assert.DoesNotContain("WorldServer received opcode 2198", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyApprenticeInformationQueryThroughRegistry()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateTitleStorageSession(
            Path.Combine(Path.GetTempPath(), "wolf-apprentice-dispatch-" + Guid.NewGuid(), "Users"),
            "");
        sessions.Set(connection, session);
        var worldData = LegacyItemWorldDataFixture.Create();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await registry.HandleAsync(
            context,
            ReadFrame(CreateEmptyFrame(WorldPacketOpcodes.LegacyApprenticeInformation)));

        Assert.Empty(sent);

        var output = log.ToString();
        Assert.Contains(
            "WorldServer handled LegacyApprenticeInformation",
            output);
        Assert.Contains("payloadLength=0 responseFrames=0", output);
        Assert.DoesNotContain("WorldServer deferred LegacyApprenticeInformation", output);
        Assert.DoesNotContain("WorldServer received opcode 2200", output);
    }

    [Fact]
    public async Task HandlerSourceBackedNoOpsLegacyApprenticeInformationMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateTitleStorageSession(Path.GetTempPath(), "");
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(connection, log, session);

        await registry.HandleAsync(context, ReadFrame(CreateLegacyApprenticeInformationMutationPacket().ToFrameBytes()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer source-backed no-op LegacyApprenticeInformation", output);
        Assert.Contains("mode=Apprentice", output);
        Assert.Contains("mentorId=1400123", output);
        Assert.DoesNotContain("WorldServer deferred LegacyApprenticeInformation", output);
        Assert.DoesNotContain("WorldServer received opcode 2200", output);
    }

    [Fact]
    public async Task HandlerLegacyMailListSendsSourceBackedMailboxPage()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var root = Path.Combine(Path.GetTempPath(), "wolf-mail-list-handler-" + Guid.NewGuid());
        try
        {
            var store = new LegacyMailboxStore(root);
            store.Add(
                1_400_123,
                new LegacyMailboxEntry
                {
                    Title = "STR_SYSTEM_NAME@@",
                    SenderName = "STR_SYSTEM_NAME@@",
                    Content = "STR_SYSTEM_NAME@@",
                    Id = 4,
                    Money = 10_000_000,
                    EMoney = 800_000,
                    ItemId = 0,
                    EmoneyRecordType = 1,
                    Flag = 2,
                    Timer = DateTime.Now.AddHours(1).Ticks
                });
            var session = CreateTitleStorageSession(Path.Combine(root, "Users"), "");
            var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            var context = new WorldPacketHandlerContext(
                CreateConnection(sent),
                log,
                session,
                Mailboxes: store);

            await registry.HandleAsync(context, ReadFrame(CreateEmptyFrame(WorldPacketOpcodes.LegacyMailList)));

            var responseBytes = Assert.Single(sent);
            Assert.True(LegacyMailListPacket.TryParse(ReadFrame(responseBytes), out var response));
            Assert.Equal(LegacyMailListType.FullPage, response.Type);
            Assert.Equal(1, response.Count);
            var mail = Assert.Single(response.Mails);
            Assert.Equal(4UL, mail.Id);
            Assert.Equal(10_000_000UL, mail.Money);
            Assert.Equal(800_000UL, mail.EMoney);
            Assert.Equal(2UL, mail.Flag);
            Assert.Equal("STR_SYSTEM_NAME@@", mail.Title);
            Assert.Equal("STR_SYSTEM_NAME@@", mail.SenderName);
            Assert.True(mail.ExpirationDate > 0);

            var output = log.ToString();
            Assert.Contains("WorldServer handled LegacyMailList", output);
            Assert.Contains("mailCount=1", output);
            Assert.Contains("responseFrames=1", output);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerLegacyMailboxModeContentSendsStoredMailContent()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var root = Path.Combine(Path.GetTempPath(), "wolf-mail-content-handler-" + Guid.NewGuid());
        try
        {
            var store = new LegacyMailboxStore(root);
            store.Add(
                1_400_123,
                new LegacyMailboxEntry
                {
                    Title = "STR_SYSTEM_NAME@@",
                    SenderName = "STR_SYSTEM_NAME@@",
                    Content = "Prize details",
                    Id = 7,
                    Money = 10_000_000,
                    EMoney = 800_000,
                    ItemId = 0,
                    EmoneyRecordType = 1,
                    Flag = 2,
                    Timer = DateTime.Now.AddHours(1).Ticks
                });
            var session = CreateTitleStorageSession(Path.Combine(root, "Users"), "");
            var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            var context = new WorldPacketHandlerContext(
                CreateConnection(sent),
                log,
                session,
                Mailboxes: store);

            await registry.HandleAsync(
                context,
                ReadFrame(new LegacyMailboxModePacket(
                    LegacyMailboxModeType.Content,
                    Id: 7,
                    Items: []).ToFrameBytes()));

            Assert.Equal(2, sent.Count);
            Assert.True(LegacyMailContentPacket.TryParse(ReadFrame(sent[0]), out var response));
            Assert.Equal(7U, response.MailId);
            Assert.Equal("Prize details", response.Content);
            AssertMailboxAck(sent[1], LegacyMailboxModeType.Content, 7, []);

            var output = log.ToString();
            Assert.Contains("WorldServer handled LegacyMailboxMode", output);
            Assert.Contains("response=content", output);
            Assert.Contains("ack=True", output);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerLegacyMailboxModeClaimMoneyMutatesWalletAndClearsStoredMoney()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var root = Path.Combine(Path.GetTempPath(), "wolf-mail-claim-money-handler-" + Guid.NewGuid());
        try
        {
            var store = new LegacyMailboxStore(root);
            store.Add(
                1_400_123,
                new LegacyMailboxEntry
                {
                    Title = "STR_SYSTEM_NAME@@",
                    SenderName = "STR_SYSTEM_NAME@@",
                    Content = "Prize details",
                    Id = 8,
                    Money = 123,
                    EMoney = 45,
                    ItemId = 0,
                    EmoneyRecordType = 1,
                    Flag = 0,
                    Timer = DateTime.Now.AddHours(1).Ticks
                });
            var usersRoot = Path.Combine(root, "Users");
            var itemsRoot = Path.Combine(root, "PlayersItems");
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(itemsRoot);
            var snapshotPath = WriteTitleStorageSnapshot(usersRoot, "");
            var itemLoader = new PlayerItemSnapshotLoader(itemsRoot);
            itemLoader.SaveByCharacterUid(1_400_123, PlayerItemSnapshot.Empty);
            store.Save(1_400_123);
            var session = CreateTitleStorageSession(usersRoot, "") with
            {
                Character = CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath), snapshotPath),
                PlayerItems = itemLoader.LoadByCharacterUid(1_400_123),
            };
            var connection = CreateConnection(sent);
            var sessions = new WorldSessionStore();
            sessions.Set(connection, session);
            var recovery = new LegacyTradeRecoveryState(usersRoot, itemsRoot, null,
                Path.Combine(root, "PlayersMailList"));
            Assert.True(recovery.TryEnsureInitialized(out var recoveryReason), recoveryReason);
            Assert.True(recovery.IsReadyForMailbox(usersRoot, itemsRoot, Path.Combine(root, "PlayersMailList")));
            var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            var context = new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions,
                Mailboxes: store,
                PlayerItemsRoot: itemsRoot)
            {
                Trades = new LegacyTradeStateStore(),
                TradeOperations = new WorldOperationBarrier(),
                TradeRecovery = recovery,
            };

            await registry.HandleAsync(
                context,
                ReadFrame(new LegacyMailboxModePacket(
                    LegacyMailboxModeType.ClaimMoney,
                    Id: 8,
                    Items: []).ToFrameBytes()));

            Assert.Equal(5, sent.Count);
            AssertSystemTalk(sent[0], "STR_PICK_MONEY@@123@@");
            AssertSingleUpdate(sent[1], LegacyMsgUpdateDataType.Money, 98_602_440_079);
            AssertSystemTalk(sent[2], "You received 45 ConquerPoints.");
            AssertSingleUpdate(sent[3], LegacyMsgUpdateDataType.ConquerPoints, 639_813_245);
            AssertMailboxAck(sent[4], LegacyMailboxModeType.ClaimMoney, 8, []);

            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal(98_602_440_079, updatedSession.Character.Money);
            Assert.Equal(639_813_245, updatedSession.Character.ConquerPoints);

            var persisted = Assert.Single(new LegacyMailboxStore(root).Load(1_400_123));
            Assert.Equal(0UL, persisted.Money);
            Assert.Equal(0U, persisted.EMoney);

            var output = log.ToString();
            Assert.Contains("WorldServer handled LegacyMailboxMode", output);
            Assert.Contains("type=ClaimMoney", output);
            Assert.Contains("money=123", output);
            Assert.Contains("eMoney=45", output);
            Assert.Contains("ack=True", output);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerLegacyMailboxModeClaimMoneyPersistsWalletThroughLiveHandler()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-mail-claim-money-live-handler-" + Guid.NewGuid());
        var usersRoot = Path.Combine(dbRoot, "Users");
        try
        {
            Directory.CreateDirectory(usersRoot);
            var snapshotPath = WriteTitleStorageSnapshot(usersRoot, "");
            var itemsRoot = Path.Combine(dbRoot, "PlayersItems");
            Directory.CreateDirectory(itemsRoot);
            new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(1_400_123, PlayerItemSnapshot.Empty);
            var store = new LegacyMailboxStore(dbRoot);
            store.Add(
                1_400_123,
                new LegacyMailboxEntry
                {
                    Title = "STR_SYSTEM_NAME@@",
                    SenderName = "STR_SYSTEM_NAME@@",
                    Content = "Prize details",
                    Id = 8,
                    Money = 123,
                    EMoney = 45,
                    ItemId = 0,
                    EmoneyRecordType = 1,
                    Flag = 0,
                    Timer = DateTime.Now.AddHours(1).Ticks
                });
            store.Save(1_400_123);

            using var log = new StringWriter();
            var sent = new List<byte[]>();
            var connection = CreateConnection(sent);
            var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot, itemsRoot), log);

            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new LegacyMsgLoginGamePacket(1_400_123).ToFrameBytes()));
            var countBeforeClaim = sent.Count;

            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new LegacyMailboxModePacket(
                    LegacyMailboxModeType.ClaimMoney,
                    Id: 8,
                    Items: []).ToFrameBytes()));

            var claimFrames = sent.Skip(countBeforeClaim).ToArray();
            Assert.Equal(5, claimFrames.Length);
            AssertSystemTalk(claimFrames[0], "STR_PICK_MONEY@@123@@");
            AssertSingleUpdate(claimFrames[1], LegacyMsgUpdateDataType.Money, 98_602_440_079);
            AssertSystemTalk(claimFrames[2], "You received 45 ConquerPoints.");
            AssertSingleUpdate(claimFrames[3], LegacyMsgUpdateDataType.ConquerPoints, 639_813_245);
            AssertMailboxAck(claimFrames[4], LegacyMailboxModeType.ClaimMoney, 8, []);

            var persistedCharacter = CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath));
            Assert.Equal(98_602_440_079, persistedCharacter.Money);
            Assert.Equal(639_813_245, persistedCharacter.ConquerPoints);
            Assert.Equal("98602440079", persistedCharacter.Attributes["Money"]);
            Assert.Equal("639813245", persistedCharacter.Attributes["ConquerPoints"]);

            var persistedMail = Assert.Single(new LegacyMailboxStore(dbRoot).Load(1_400_123));
            Assert.Equal(0UL, persistedMail.Money);
            Assert.Equal(0U, persistedMail.EMoney);

            var output = log.ToString();
            Assert.Contains("WorldServer handled LegacyMailboxMode", output);
            Assert.Contains("type=ClaimMoney", output);
            Assert.Contains("money=123", output);
            Assert.Contains("eMoney=45", output);
            Assert.DoesNotContain("PARITY-GAP category=runtime severity=error gap=character-scalar-persistence", output);
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerLegacyMailboxModeDeleteRemovesOnlyEmptyMail()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var root = Path.Combine(Path.GetTempPath(), "wolf-mail-delete-handler-" + Guid.NewGuid());
        try
        {
            var store = new LegacyMailboxStore(root);
            store.Add(
                1_400_123,
                new LegacyMailboxEntry
                {
                    Title = "Empty",
                    SenderName = "System",
                    Content = "Delete me",
                    Id = 9,
                    Money = 0,
                    EMoney = 0,
                    ItemId = 0,
                    EmoneyRecordType = 0,
                    Flag = 0,
                    Timer = DateTime.Now.AddHours(1).Ticks
                });
            store.Add(
                1_400_123,
                new LegacyMailboxEntry
                {
                    Title = "Blocked",
                    SenderName = "System",
                    Content = "Not yet",
                    Id = 10,
                    Money = 1,
                    EMoney = 0,
                    ItemId = 0,
                    EmoneyRecordType = 0,
                    Flag = 0,
                    Timer = DateTime.Now.AddHours(1).Ticks
                });
            var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            var context = CreateOwnedMailboxDeletionContext(root, CreateConnection(sent), log, store);

            await registry.HandleAsync(
                context,
                ReadFrame(new LegacyMailboxModePacket(
                    LegacyMailboxModeType.Delete,
                    Id: 10,
                    Items: []).ToFrameBytes()));
            Assert.Empty(sent);
            Assert.True(store.TryGet(1_400_123, 10, out _));

            await registry.HandleAsync(
                context,
                ReadFrame(new LegacyMailboxModePacket(
                    LegacyMailboxModeType.Delete,
                    Id: 9,
                    Items: []).ToFrameBytes()));

            var ack = Assert.Single(sent);
            AssertMailboxAck(ack, LegacyMailboxModeType.Delete, 9, []);
            var persisted = new LegacyMailboxStore(root).Load(1_400_123);
            Assert.DoesNotContain(persisted, mail => mail.Id == 9);
            Assert.Contains(persisted, mail => mail.Id == 10);

            var output = log.ToString();
            Assert.Contains("type=Delete id=10 removed=False", output);
            Assert.Contains("type=Delete id=9 removed=True ack=True", output);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerLegacyMailboxModeDeleteClaimRemovesEachEmptySelectedMail()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var root = Path.Combine(Path.GetTempPath(), "wolf-mail-delete-claim-handler-" + Guid.NewGuid());
        try
        {
            var store = new LegacyMailboxStore(root);
            store.Add(
                1_400_123,
                new LegacyMailboxEntry
                {
                    Title = "First",
                    SenderName = "System",
                    Content = "Delete me",
                    Id = 14,
                    Timer = DateTime.Now.AddHours(1).Ticks
                });
            store.Add(
                1_400_123,
                new LegacyMailboxEntry
                {
                    Title = "Blocked",
                    SenderName = "System",
                    Content = "Not yet",
                    Id = 15,
                    Money = 1,
                    Timer = DateTime.Now.AddHours(1).Ticks
                });
            store.Add(
                1_400_123,
                new LegacyMailboxEntry
                {
                    Title = "Second",
                    SenderName = "System",
                    Content = "Delete me too",
                    Id = 16,
                    Timer = DateTime.Now.AddHours(1).Ticks
                });
            var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            var context = CreateOwnedMailboxDeletionContext(root, CreateConnection(sent), log, store);

            await registry.HandleAsync(
                context,
                ReadFrame(new LegacyMailboxModePacket(
                    LegacyMailboxModeType.DeleteClaim,
                    Id: 0,
                    Items: [14, 15, 16]).ToFrameBytes()));

            Assert.Equal(2, sent.Count);
            AssertMailboxAck(sent[0], LegacyMailboxModeType.DeleteClaim, 0, [14, 15, 16]);
            AssertMailboxAck(sent[1], LegacyMailboxModeType.DeleteClaim, 0, [14, 15, 16]);
            var persisted = new LegacyMailboxStore(root).Load(1_400_123);
            Assert.DoesNotContain(persisted, mail => mail.Id == 14);
            Assert.Contains(persisted, mail => mail.Id == 15);
            Assert.DoesNotContain(persisted, mail => mail.Id == 16);

            var output = log.ToString();
            Assert.Contains("type=DeleteClaim", output);
            Assert.Contains("requested=3", output);
            Assert.Contains("removed=2", output);
            Assert.Contains("ackFrames=2", output);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerLegacyMailboxModeClaimRecordTypeGrantsRankPackageAndClearsFlag()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var root = Path.Combine(Path.GetTempPath(), "wolf-mail-claim-package-handler-" + Guid.NewGuid());
        try
        {
            var store = new LegacyMailboxStore(root);
            store.Add(
                1_400_123,
                new LegacyMailboxEntry
                {
                    Title = "STR_SYSTEM_NAME@@",
                    SenderName = "STR_SYSTEM_NAME@@",
                    Content = "STR_SYSTEM_NAME@@",
                    Id = 11,
                    Money = 0,
                    EMoney = 0,
                    ItemId = 0,
                    EmoneyRecordType = 1,
                    Flag = 1,
                    Timer = DateTime.Now.AddHours(1).Ticks
                });
            var session = CreateTitleStorageSession(Path.Combine(root, "Users"), "");
            var connection = CreateConnection(sent);
            var sessions = new WorldSessionStore();
            sessions.Set(connection, session);
            var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            var context = new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions,
                Mailboxes: store);

            await registry.HandleAsync(
                context,
                ReadFrame(new LegacyMailboxModePacket(
                    LegacyMailboxModeType.ClaimRecordType,
                    Id: 11,
                    Items: []).ToFrameBytes()));

            Assert.Equal(9, sent.Count);
            var itemPackets = sent.Take(6)
                .Select(bytes =>
                {
                    Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var item));
                    return item;
                })
                .ToArray();
            Assert.Equal(
                new uint[] { 3_324_226, 4_050_001, 4_040_001, 3_009_003, 3_316_793, 3_330_770 },
                itemPackets.Select(item => item.ItemId).ToArray());
            Assert.Equal(
                new ushort[] { 1, 200, 200, 5, 10, 10 },
                itemPackets.Select(item => item.StackSize).ToArray());
            AssertSingleUpdate(sent[6], LegacyMsgUpdateDataType.ClassExperience, 1_000);
            Assert.True(LegacyDaoQiPacket.TryParse(ReadFrame(sent[7]), out var piratePoints));
            Assert.Equal(LegacyDaoQiType.PiratePoints, piratePoints.Type);
            Assert.Equal(50U, piratePoints.PirateExp);
            AssertMailboxAck(sent[8], LegacyMailboxModeType.ClaimRecordType, 11, []);

            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal(6, updatedSession.PlayerItems.InventoryItems.Count);
            Assert.Equal("1000", updatedSession.Character.Attributes["ClassExperience"]);
            Assert.NotNull(updatedSession.CombatGear);
            Assert.Equal(50U, updatedSession.CombatGear!.PirateExp);
            Assert.Equal(50U, updatedSession.CombatGear.AirPowers.Points);

            var persisted = Assert.Single(new LegacyMailboxStore(root).Load(1_400_123));
            Assert.Equal(0U, persisted.Flag);

            var output = log.ToString();
            Assert.Contains("type=ClaimRecordType", output);
            Assert.Contains("rank=1", output);
            Assert.Contains("itemStacks=6", output);
            Assert.Contains("ack=True", output);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerLegacyMailboxModeClaimRecordTypePersistsPackageRewardsThroughLiveHandler()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-mail-claim-package-live-handler-" + Guid.NewGuid());
        var usersRoot = Path.Combine(dbRoot, "Users");
        var itemsRoot = Path.Combine(dbRoot, "PlayersItems");
        var combatGearRoot = Path.Combine(dbRoot, "PlayersCombatGear");
        try
        {
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(itemsRoot);
            Directory.CreateDirectory(combatGearRoot);
            var snapshotPath = WriteTitleStorageSnapshot(
                usersRoot,
                "",
                "ClassExperience=0");
            new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(
                1_400_123,
                PlayerItemSnapshot.Empty);
            WriteCombatGearSnapshot(combatGearRoot, pirateExp: 0, airPowerPoints: 0);
            var store = new LegacyMailboxStore(dbRoot);
            store.Add(
                1_400_123,
                new LegacyMailboxEntry
                {
                    Title = "STR_SYSTEM_NAME@@",
                    SenderName = "STR_SYSTEM_NAME@@",
                    Content = "STR_SYSTEM_NAME@@",
                    Id = 11,
                    Money = 0,
                    EMoney = 0,
                    ItemId = 0,
                    EmoneyRecordType = 1,
                    Flag = 1,
                    Timer = DateTime.Now.AddHours(1).Ticks
                });
            store.Save(1_400_123);

            using var log = new StringWriter();
            var sent = new List<byte[]>();
            var connection = CreateConnection(sent);
            var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), log);

            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new LegacyMsgLoginGamePacket(1_400_123).ToFrameBytes()));
            var countBeforeClaim = sent.Count;

            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new LegacyMailboxModePacket(
                    LegacyMailboxModeType.ClaimRecordType,
                    Id: 11,
                    Items: []).ToFrameBytes()));

            var claimFrames = sent.Skip(countBeforeClaim).ToArray();
            Assert.Equal(9, claimFrames.Length);
            AssertSingleUpdate(claimFrames[6], LegacyMsgUpdateDataType.ClassExperience, 1_000);
            Assert.True(LegacyDaoQiPacket.TryParse(ReadFrame(claimFrames[7]), out var piratePoints));
            Assert.Equal(LegacyDaoQiType.PiratePoints, piratePoints.Type);
            Assert.Equal(50U, piratePoints.PirateExp);
            AssertMailboxAck(claimFrames[8], LegacyMailboxModeType.ClaimRecordType, 11, []);

            var persistedCharacter = CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath));
            Assert.Equal("1000", persistedCharacter.Attributes["ClassExperience"]);

            var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(1_400_123);
            Assert.Equal(
                new uint[] { 3_324_226, 4_050_001, 4_040_001, 3_009_003, 3_316_793, 3_330_770 },
                persistedItems.InventoryItems.Select(item => item.ItemId).ToArray());

            var persistedCombatGear = new CombatGearSnapshotLoader(combatGearRoot).LoadByCharacterUid(1_400_123);
            Assert.Equal(50U, persistedCombatGear.PirateExp);
            Assert.Equal(50U, persistedCombatGear.AirPowers.Points);

            var persistedMail = Assert.Single(new LegacyMailboxStore(dbRoot).Load(1_400_123));
            Assert.Equal(0U, persistedMail.Flag);

            var output = log.ToString();
            Assert.Contains("WorldServer handled LegacyMailboxMode", output);
            Assert.Contains("type=ClaimRecordType", output);
            Assert.DoesNotContain("PARITY-GAP category=runtime severity=error gap=player-items-persistence", output);
            Assert.DoesNotContain("PARITY-GAP category=runtime severity=error gap=combat-gear-persistence", output);
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerLegacyMailboxModeClaimAllRefusesUnownedPackageBeforeEitherMailChanges()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var root = Path.Combine(Path.GetTempPath(), "wolf-mail-claim-all-handler-" + Guid.NewGuid());
        try
        {
            var store = new LegacyMailboxStore(root);
            store.Add(
                1_400_123,
                new LegacyMailboxEntry
                {
                    Title = "Wallet",
                    SenderName = "System",
                    Content = "Money",
                    Id = 12,
                    Money = 50,
                    EMoney = 5,
                    ItemId = 0,
                    EmoneyRecordType = 0,
                    Flag = 0,
                    Timer = DateTime.Now.AddHours(1).Ticks
                });
            store.Add(
                1_400_123,
                new LegacyMailboxEntry
                {
                    Title = "Package",
                    SenderName = "System",
                    Content = "Rank",
                    Id = 13,
                    Money = 0,
                    EMoney = 0,
                    ItemId = 0,
                    EmoneyRecordType = 1,
                    Flag = 6,
                    Timer = DateTime.Now.AddHours(1).Ticks
                });
            var users = Path.Combine(root, "Users");
            var items = Path.Combine(root, "PlayersItems");
            Directory.CreateDirectory(users);
            Directory.CreateDirectory(items);
            var characterPath = WriteTitleStorageSnapshot(users, "");
            var itemLoader = new PlayerItemSnapshotLoader(items);
            itemLoader.SaveByCharacterUid(1_400_123, PlayerItemSnapshot.Empty);
            store.Save(1_400_123);
            var session = CreateTitleStorageSession(users, "") with
            {
                Character = CharacterSnapshotParser.ParseText(File.ReadAllText(characterPath), "1400123.ini"),
                PlayerItems = itemLoader.LoadByCharacterUid(1_400_123),
            };
            var recovery = new LegacyTradeRecoveryState(users, items, null, Path.Combine(root, "PlayersMailList"));
            Assert.True(recovery.TryEnsureInitialized(out var readyReason), readyReason);
            Assert.True(recovery.IsReadyForMailbox(users, items, Path.Combine(root, "PlayersMailList")));
            var beforeFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes);
            var connection = CreateConnection(sent);
            var sessions = new WorldSessionStore();
            sessions.Set(connection, session);
            var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            var context = new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions,
                Mailboxes: store, PlayerItemsRoot: items)
            {
                Trades = new LegacyTradeStateStore(),
                TradeOperations = new WorldOperationBarrier(),
                TradeRecovery = recovery,
            };

            await registry.HandleAsync(
                context,
                ReadFrame(new LegacyMailboxModePacket(
                    LegacyMailboxModeType.ClaimAll,
                    Id: 0,
                    Items: [12, 13]).ToFrameBytes()));

            Assert.Empty(sent);
            Assert.True(sessions.TryGet(connection, out var current));
            Assert.Same(session, current);
            Assert.Equal(98_602_439_956, current!.Character.Money);
            Assert.Equal(639_813_200, current.Character.ConquerPoints);
            Assert.Empty(current.PlayerItems.InventoryItems);
            Assert.Equal(beforeFiles.Keys.Order(StringComparer.Ordinal),
                Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            foreach (var (path, bytes) in beforeFiles) Assert.Equal(bytes, File.ReadAllBytes(path));
            var persisted = new LegacyMailboxStore(root).Load(1_400_123).OrderBy(mail => mail.Id).ToArray();
            Assert.Equal([12U, 13U], persisted.Select(mail => mail.Id).ToArray());
            Assert.Equal(50UL, persisted[0].Money);
            Assert.Equal(5U, persisted[0].EMoney);
            Assert.Equal(0U, persisted[0].Flag);
            Assert.Equal(0UL, persisted[1].Money);
            Assert.Equal(0U, persisted[1].EMoney);
            Assert.Equal(6U, persisted[1].Flag);
            Assert.False(LegacyTradeJournal.HasPending(users));
            Assert.Contains("mailboxClaimPackageOwnersUnresolved", log.ToString());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerRejectsMalformedLegacyApprenticeInformationPayload()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyApprenticeInformation, payloadLength: 3);
        writer.Payload[0] = 0xAA;
        writer.Payload[1] = 0xBB;
        writer.Payload[2] = 0xCC;

        await handler.HandleFrameAsync(CreateConnection(sent), ReadFrame(writer.ToArray()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyApprenticeInformation packet session=none.", output);
        Assert.Contains("opcode=2200 payloadLength=3 payloadSampleHex=AABBCC", output);
        Assert.DoesNotContain("WorldServer deferred LegacyApprenticeInformation", output);
        Assert.DoesNotContain("WorldServer received opcode 2200", output);
    }

    [Fact]
    public async Task HandlerDispatchesLegacyLotteryListThroughRegistryWithoutSourceSilent()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            ReadFrame(LegacyLotteryListPacket.Query().ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyLotteryListPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(0U, response.Type);
        Assert.Empty(response.Items);

        var output = log.ToString();
        Assert.Contains(
            "WorldServer handled LegacyLotteryList session=none type=0 pages=1 items=0.",
            output);
        Assert.DoesNotContain("WorldServer deferred LegacyLotteryList session=none type=0", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
        Assert.DoesNotContain("WorldServer received opcode 2432", output);
    }

    [Fact]
    public async Task HandlerTreatsNonzeroLegacyLotteryListTypeAsSourceBackedNoOp()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            ReadFrame(new LegacyLotteryListPacket(Type: 1, Items: []).ToFrameBytes()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains(
            "WorldServer source-backed no-op LegacyLotteryList session=none type=1 items=0; " +
            "source MsgLotteryList.Process returns without sending award pages when Type != 0.",
            output);
        Assert.DoesNotContain("WorldServer deferred LegacyLotteryList", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
        Assert.DoesNotContain("WorldServer received opcode 2432", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyLotteryListPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyLotteryList, payloadLength: 1);
        writer.Payload[0] = 0x08;

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyLotteryList packet session=none.", output);
        Assert.Contains("opcode=2432 payloadLength=1 payloadSampleHex=08", output);
        Assert.DoesNotContain("source-silent legacy packet", output);
        Assert.DoesNotContain("WorldServer received opcode 2432", output);
    }

    [Fact]
    public async Task HandlerLegacyLeagueOptListSendsEmptyAllegianceListForActiveSession()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateTitleStorageSession(Path.GetTempPath(), "");
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(connection, log, session);

        await registry.HandleAsync(context, ReadFrame(CreateLegacyLeagueOptQuery(index: 2)));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyLeagueAllegianceListPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(0U, response.Count);
        Assert.Equal(2, response.Page);
        Assert.Equal(0, response.PageCount);
        Assert.Equal(1, response.DwParam);

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyLeagueOpt List", output);
        Assert.Contains("page=2 total=0 entries=0", output);
        Assert.DoesNotContain("WorldServer deferred LegacyLeagueOpt", output);
        Assert.DoesNotContain("WorldServer received opcode 2157", output);
    }

    [Fact]
    public async Task HandlerLegacyLeagueOptListSendsSourceBackedAllegiancePage()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-leagueopt-dispatch-" + Guid.NewGuid());
        var usersRoot = Path.Combine(dbRoot, "Users");
        var unionsRoot = Path.Combine(dbRoot, "Unions");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(unionsRoot);
        WriteLegacyLeagueUnion(unionsRoot, uid: 1084, name: "UnbeatableArmy", emperor: "~Super.Shiro~", goldBricks: 10, treasury: 100, recruitDeclaration: "First");
        WriteLegacyLeagueUnion(unionsRoot, uid: 1088, name: "SilmoV", emperor: "SilmoV", goldBricks: 90, treasury: 900, recruitDeclaration: "Second");
        WriteLegacyLeagueUnion(unionsRoot, uid: 1097, name: "UnitedKingdoM", emperor: "Crunchy<3", goldBricks: 50, treasury: 500, recruitDeclaration: "Third");
        WriteLegacyLeagueUnion(unionsRoot, uid: 1101, name: "ServerLegandary", emperor: "~Super.Shiro~", goldBricks: 20, treasury: 200, recruitDeclaration: "Fourth");

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateTitleStorageSession(usersRoot, "");
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(connection, log, session);

        await registry.HandleAsync(context, ReadFrame(CreateLegacyLeagueOptQuery(index: 0)));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyLeagueAllegianceListPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(4U, response.Count);
        Assert.Equal(0, response.Page);
        Assert.Equal(3, response.PageCount);
        Assert.Equal(1, response.DwParam);
        Assert.Collection(
            response.Entries,
            entry =>
            {
                Assert.Equal(1088U, entry.Uid);
                Assert.Equal(90U, entry.GoldBricks);
                Assert.Equal(900UL, entry.Fund);
                Assert.Equal("SilmoV", entry.Name);
                Assert.Equal("SilmoV", entry.LeaderName);
                Assert.Equal("Second", entry.RecruitDeclaration);
            },
            entry =>
            {
                Assert.Equal(1097U, entry.Uid);
                Assert.Equal(50U, entry.GoldBricks);
                Assert.Equal("UnitedKingdoM", entry.Name);
            },
            entry =>
            {
                Assert.Equal(1101U, entry.Uid);
                Assert.Equal(20U, entry.GoldBricks);
                Assert.Equal("ServerLegandary", entry.Name);
            });

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyLeagueOpt List", output);
        Assert.Contains("page=0 total=4 entries=3 source=Unions", output);
        Assert.DoesNotContain("WorldServer deferred LegacyLeagueOpt", output);
        Assert.DoesNotContain("WorldServer received opcode 2157", output);
    }

    [Fact]
    public async Task HandlerLegacyLeagueOptListNoOpsForUnionSession()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateTitleStorageSession(
            Path.GetTempPath(),
            "",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UnionUID"] = "42",
            });
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(connection, log, session);

        await registry.HandleAsync(context, ReadFrame(CreateLegacyLeagueOptQuery()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer skipped LegacyLeagueOpt List", output);
        Assert.Contains("character is already in a union", output);
        Assert.DoesNotContain("WorldServer handled LegacyLeagueOpt List", output);
        Assert.DoesNotContain("WorldServer deferred LegacyLeagueOpt", output);
        Assert.DoesNotContain("WorldServer received opcode 2157", output);
    }

    [Fact]
    public async Task HandlerDefersUnsupportedLegacyLeagueOptActions()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateTitleStorageSession(Path.GetTempPath(), "");
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(connection, log, session);

        await registry.HandleAsync(context, ReadFrame(CreateLegacyLeagueOptQuery(action: 5)));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer deferred LegacyLeagueOpt", output);
        Assert.Contains("action=5", output);
        Assert.DoesNotContain("WorldServer handled LegacyLeagueOpt List", output);
        Assert.DoesNotContain("WorldServer received opcode 2157", output);
    }

    public static IEnumerable<object[]> ClickedUiPacketDispatchCases()
    {
        yield return [new LegacyRankMemberShowPacket(0, []).ToFrameBytes(), WorldPacketOpcodes.LegacyRankMemberShow, "WorldServer skipped LegacyRankMemberShow session=none type=0", false];
        yield return [CreateLegacyQuestListQuery(), WorldPacketOpcodes.LegacyQuestList, "WorldServer skipped LegacyQuestList session=none mode=Review", false];
        yield return [new LegacyClanPacket(1, 0, 0, []).ToFrameBytes(), WorldPacketOpcodes.LegacyClan, "WorldServer skipped LegacyClan session=none mode=1", false];
        yield return [CreateLegacyFamilyOccupyQuery(), WorldPacketOpcodes.LegacyFamilyOccupy, "WorldServer skipped LegacyFamilyOccupy session=none type=ShowInformation", false];
        yield return [CreateLegacyInnerStrengthOptQuery(), WorldPacketOpcodes.LegacyInnerStrengthOpt, "WorldServer deferred LegacyInnerStrengthOpt session=none action=OpenInner", false];
        yield return [CreateLegacyLeagueOptQuery(), WorldPacketOpcodes.LegacyLeagueOpt, "WorldServer skipped LegacyLeagueOpt List session=none index=0", false];
        yield return [CreateEmptyFrame(WorldPacketOpcodes.MsgArenaInfo), WorldPacketOpcodes.MsgArenaInfo, "WorldServer skipped MsgArenaInfo session=none", false];
        yield return [CreateEmptyFrame(WorldPacketOpcodes.LegacyApprenticeInformation), WorldPacketOpcodes.LegacyApprenticeInformation, "WorldServer skipped LegacyApprenticeInformation session=none payloadLength=0", false];
        yield return [CreateEmptyFrame(WorldPacketOpcodes.LegacyItemRefineRecord), WorldPacketOpcodes.LegacyItemRefineRecord, "WorldServer handled LegacyItemRefineRecord session=none points=54", true];
        yield return [CreateEmptyFrame(WorldPacketOpcodes.LegacyGuildList), WorldPacketOpcodes.LegacyGuildList, "WorldServer skipped LegacyGuildList session=none", false];
    }

    [Theory]
    [MemberData(nameof(ClickedUiPacketDispatchCases))]
    public async Task HandlerDispatchesClickedUiPacketsThroughRegistry(
        byte[] frameBytes,
        ushort opcode,
        string expectedLog,
        bool expectResponse)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(CreateConnection(sent), ReadFrame(frameBytes));

        var output = log.ToString();
        Assert.Contains(expectedLog, output);
        Assert.DoesNotContain($"WorldServer received opcode {opcode}", output);
        if (expectResponse)
        {
            Assert.NotEmpty(sent);
        }
        else
        {
            Assert.Empty(sent);
        }
    }

    [Fact]
    public async Task HandlerDispatchesLegacyDaoQiThroughRegistryWithoutSession()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(),
            ReadFrame(LegacyDaoQiPacket.Info(
                taoistReiki: 0,
                lottery: 0,
                masteryPoints: 0,
                pirateExp: 0).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains(
            "WorldServer skipped LegacyDaoQi session=none type=Info position=0; no character session is available.",
            output);
        Assert.DoesNotContain("WorldServer received opcode 2451", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyDaoQiPacket()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyDaoQi, payloadLength: 1);
        writer.Payload[0] = 0x08;

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyDaoQi packet session=none.", output);
        Assert.DoesNotContain("WorldServer received opcode 2451", output);
    }

    [Fact]
    public async Task HandlerLogsUnknownOpcodeAsParityGapWithFrameDiagnostic()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var writer = new PacketWriter(4999, payloadLength: 2);
        writer.Payload[0] = 0xAA;
        writer.Payload[1] = 0xBB;

        await handler.HandleFrameAsync(CreateConnection(), ReadFrame(writer.ToArray()));

        var output = log.ToString();
        Assert.Contains("PARITY-GAP category=unsupported-packet severity=warning gap=unsupported-packet", output);
        Assert.Contains("dedupe=unsupported-packet:unsupported-packet:opcode=4999:packet=World", output);
        Assert.Contains(
            "PARITY-GAP unknown world opcode session=none opcode=4999 payloadLength=2 payloadSampleHex=AABB from 127.0.0.1:9958",
            output);
    }

    private static ConquerTcpConnection CreateConnection()
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958));
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame CreateMalformedMsgAttackFrame()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.MsgAttack, payloadLength: 0);
        return ReadFrame(writer.ToArray());
    }

    private static PacketFrame CreateSyntheticMsgAttackFrame()
    {
        return CreateProtoFixtureFrame(WorldPacketOpcodes.MsgAttack,
            (4, 1_400_123), (5, 500_123), (7, 91), (8, 113),
            (13, (uint)MsgAttackType.Physical), (15, 321),
            (17, 0), (21, 0), (25, 0), (26, 0));
    }

    private static PacketFrame CreateSyntheticLegacyMsgTitleStorageEquipFrame()
    {
        return CreateProtoFixtureFrame(WorldPacketOpcodes.LegacyMsgTitleStorage,
            (1, (uint)LegacyTitleStorageAction.Equip), (2, 0), (3, 8007), (4, 8007));
    }

    private static PacketFrame CreateProtoFixtureFrame(ushort opcode, params (int Number, ulong Value)[] fields)
    {
        var payload = new List<byte>();
        foreach (var field in fields)
        {
            WriteProtoVarintField(payload, field.Number, field.Value);
        }

        var writer = new PacketWriter(opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return ReadFrame(writer.ToArray());
    }

    private static WorldPacketHandlerContext CreateOwnedMailboxDeletionContext(string root,
        ConquerTcpConnection connection, TextWriter log, LegacyMailboxStore mailboxes)
    {
        var usersRoot = Path.Combine(root, "Users");
        var itemsRoot = Path.Combine(root, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        var seed = CreateTitleStorageSession(usersRoot, "");
        var snapshotPath = WriteTitleStorageSnapshot(usersRoot, "");
        var character = CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath), snapshotPath);
        Assert.Equal(seed.Character.Uid, character.Uid);
        var uid = checked((uint)character.Uid);
        var itemLoader = new PlayerItemSnapshotLoader(itemsRoot);
        itemLoader.SaveByCharacterUid(uid, seed.PlayerItems);
        mailboxes.Save(uid);
        var session = seed with { Character = character, PlayerItems = itemLoader.LoadByCharacterUid(uid) };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var mailboxRoot = Path.Combine(root, "PlayersMailList");
        var recovery = new LegacyTradeRecoveryState(usersRoot, itemsRoot, null, mailboxRoot);
        Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
        Assert.True(recovery.IsReadyForMailbox(usersRoot, itemsRoot, mailboxRoot));
        return new WorldPacketHandlerContext(connection, log, session, Sessions: sessions,
            PlayerItemsRoot: itemsRoot, Mailboxes: mailboxes)
        {
            Trades = new LegacyTradeStateStore(),
            TradeOperations = new WorldOperationBarrier(),
            TradeRecovery = recovery,
        };
    }

    private static string WriteTitleStorageSnapshot(
        string usersRoot,
        string specialTitles,
        string extraAttributes = "")
    {
        var path = Path.Combine(usersRoot, "1400123.ini");
        File.WriteAllText(
            path,
            $$"""
            [Character]
            UID=1400123
            Name=FixturePlayer
            Body=1008
            Face=154
            Haire=27
            Class=7051
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level=140
            Experience=0
            Map=1015
            X=835
            Y=596
            PMap=1000
            PMapX=500
            PMapY=650
            Strength=0
            Agility=36
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=55989
            MinMana=5000
            Money=98602439956
            ConquerPoints=639813200
            BoundConquerPoints=0
            BankCps=0
            PkPoints=0
            SpecialTitles={{specialTitles}}
            {{extraAttributes}}
            Online=1
            """);
        return path;
    }

    private static void WriteCombatGearSnapshot(string combatGearRoot, uint pirateExp, uint airPowerPoints)
    {
        File.WriteAllText(
            Path.Combine(combatGearRoot, "1400123.ini"),
            $$"""
            [CombatGear]
            MasteryPoints=0
            TaoistReiki=0
            PirateExp={{pirateExp}}
            Lottery=0
            Count=0
            [AirPowers]
            SenseLevel=0
            ArmedLevel=0
            OverLoadLevel=0
            Points={{airPowerPoints}}
            SenseAipPower=0
            ArmedAipPower=0
            OverLoadAipPower=0
            [BlackJadeBag]
            Count=0
            """);
    }

    private static WorldSession CreateTitleStorageSession(
        string usersRoot,
        string specialTitles,
        IReadOnlyDictionary<string, string>? extraAttributes = null)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SpecialTitles"] = specialTitles,
        };
        if (extraAttributes is not null)
        {
            foreach (var pair in extraAttributes)
            {
                attributes[pair.Key] = pair.Value;
            }
        }

        return new WorldSession(
            AccountId: 1_400_123,
            Username: "guest",
            Character: new CharacterSnapshot(
                Uid: 1_400_123,
                Name: "FixturePlayer",
                Body: 1008,
                Face: 154,
                Hair: 27,
                Class: 7051,
                FirstClass: 0,
                SecondClass: 7050,
                Reborn: 2,
                Level: 140,
                Experience: 0,
                MapId: 1015,
                X: 835,
                Y: 596,
                PreviousMapId: 1000,
                PreviousMapX: 500,
                PreviousMapY: 650,
                Strength: 0,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 55_989,
                MinMana: 5_000,
                Money: 98_602_439_956,
                ConquerPoints: 639_813_200,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static WorldSession CreateCaptureTheFlagSession(
        string statusFlags,
        IReadOnlyDictionary<string, string> extraAttributes)
    {
        var session = CreateTitleStorageSession(Path.GetTempPath(), "", extraAttributes);
        var attributes = new Dictionary<string, string>(
            session.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = statusFlags,
        };

        return session with
        {
            Character = session.Character with
            {
                MapId = 2057,
                X = 835,
                Y = 596,
                Attributes = attributes,
            },
        };
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[10];
        foreach (var flag in flags)
        {
            var wordIndex = flag / 32;
            var bitIndex = flag % 32;
            words[wordIndex] |= 1UL << bitIndex;
        }

        return string.Join('/', words) + "/";
    }

    private static bool HasLegacyStatusFlag(string raw, int flag)
    {
        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        var words = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return wordIndex < words.Length &&
            ulong.TryParse(words[wordIndex], out var word) &&
            (word & (1UL << bitIndex)) != 0;
    }

    private static bool HasLegacyStatusFlag(IReadOnlyList<ulong> words, int flag)
    {
        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        return wordIndex < words.Count &&
            (words[wordIndex] & (1UL << bitIndex)) != 0;
    }

    private static byte[] CreateLegacyQuestListQuery()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyQuestList, LegacyQuestListPacket.QueryPayloadLength);
        writer.WriteUInt16(0, (ushort)LegacyQuestListMode.Review);
        writer.WriteUInt16(2, 0);
        writer.WriteUInt32(4, 0);
        writer.WriteUInt32(8, (uint)LegacyQuestStatus.Available);
        writer.WriteUInt32(12, 0);
        return writer.ToArray();
    }

    private static byte[] CreateLegacyFamilyOccupyQuery()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyFamilyOccupy, LegacyFamilyOccupyPacket.QueryPayloadLength);
        writer.WriteUInt32(0, (uint)LegacyFamilyOccupyType.ShowInformation);
        writer.WriteUInt32(4, 1024);
        return writer.ToArray();
    }

    private static byte[] CreateLegacyInnerStrengthOptQuery()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyInnerStrengthOpt, LegacyInnerPowerOptPacket.PayloadLength);
        writer.Payload[0] = (byte)LegacyInnerPowerAction.OpenInner;
        writer.WriteUInt32(1, 1_400_123);
        writer.WriteUInt32(5, 0);
        return writer.ToArray();
    }

    private static byte[] CreateLegacyLeagueOptQuery(
        ushort action = LegacyLeagueOptPacket.ListAction,
        uint index = 0)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyLeagueOpt, LegacyLeagueOptPacket.MinimumPayloadLength);
        writer.WriteUInt16(0, action);
        writer.WriteUInt32(2, 0);
        writer.WriteUInt32(6, 0);
        writer.WriteUInt32(10, index);
        return writer.ToArray();
    }

    private static LegacyApprenticeInformationPacket CreateLegacyApprenticeInformationMutationPacket()
    {
        return new LegacyApprenticeInformationPacket(
            LegacyApprenticeInformationAction.Apprentice,
            MentorId: 1_400_123,
            ApprenticeId: 1_293_616,
            Mesh: 7_300,
            SharedBattlePower: 0,
            EnroleDate: 202_603,
            Level: 55,
            Class: 7_051,
            PkPoints: 0,
            Online: 1,
            Unknown2: 0,
            ApprenticeExperience: 12_345,
            ApprenticeBlessing: 0,
            ApprenticeComposing: 0,
            MentorName: "FixturePlayer",
            ApprenticeName: "Tester[PM]",
            ApprenticeSpouse: "NULL",
            PersonalSignature: "NULL");
    }

    private static void WriteLegacyLeagueUnion(
        string unionsRoot,
        uint uid,
        string name,
        string emperor,
        uint goldBricks,
        ulong treasury,
        string recruitDeclaration)
    {
        File.WriteAllText(
            Path.Combine(unionsRoot, uid + ".ini"),
            $$"""
            [Info]
            UID={{uid}}
            Name={{name}}
            Emperor={{emperor}}
            GoldBrick={{goldBricks}}
            Treasury={{treasury}}
            Stipend=0
            EmperrorUID=0
            IsKingdom=0
            PlunderID=0
            InvadingID=0
            Bulletin=None
            Title=
            PlunderTarget=None
            InvadingUnion=None
            RecruitDeclaration={{recruitDeclaration}}
            """);
    }

    private static byte[] CreateMsgMovementWithUnknownVarintField()
    {
        var payload = new List<byte>();
        WriteProtoVarintField(payload, fieldNumber: 1, value: 5);
        WriteProtoVarintField(payload, fieldNumber: 2, value: 1_400_123);
        WriteProtoVarintField(payload, fieldNumber: 3, value: (uint)MsgMovementMode.Walk);
        WriteProtoVarintField(payload, fieldNumber: 4, value: 1234);
        WriteProtoVarintField(payload, fieldNumber: 6, value: 99);

        var writer = new PacketWriter(WorldPacketOpcodes.MsgMovement, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static byte[] CreateMsgActionWithUnknownVarintField()
    {
        var payload = new List<byte>();
        WriteProtoVarintField(payload, fieldNumber: 1, value: 1_400_123);
        WriteProtoVarintField(payload, fieldNumber: 9, value: 1234);
        WriteProtoVarintField(payload, fieldNumber: 12, value: (ushort)MsgActionType.Revive);
        WriteProtoVarintField(payload, fieldNumber: 13, value: 0);
        WriteProtoVarintField(payload, fieldNumber: 14, value: 830);
        WriteProtoVarintField(payload, fieldNumber: 15, value: 584);
        WriteProtoVarintField(payload, fieldNumber: 25, value: 99);

        var writer = new PacketWriter(WorldPacketOpcodes.MsgAction, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static void WriteProtoVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteProtoVarint(payload, ((ulong)fieldNumber << 3) | 0);
        WriteProtoVarint(payload, value);
    }

    private static void WriteProtoVarint(List<byte> payload, ulong value)
    {
        do
        {
            var next = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                next |= 0x80;
            }

            payload.Add(next);
        }
        while (value != 0);
    }

    private static byte[] CreateEmptyFrame(ushort opcode)
    {
        return new PacketWriter(opcode, payloadLength: 0).ToArray();
    }

    private static class LegacyExchangeShopBuyDispatchProbeHandlers
    {
        [PacketHandler(WorldPacketOpcodes.LegacyExchangeShopBuy)]
        private static async ValueTask HandleLegacyExchangeShopBuyAsync(
            WorldPacketHandlerContext context,
            PacketFrame frame,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!LegacyExchangeShopBuyPacket.TryParse(frame, out var packet))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer rejected malformed LegacyExchangeShopBuy packet session=none.")
                    .ConfigureAwait(false);
                return;
            }

            await context.Log.WriteLineAsync(
                    "WorldServer classified LegacyExchangeShopBuy " +
                    $"session={(context.Session is null ? "none" : context.Session.AccountId)} " +
                    $"type={packet.Type} " +
                    $"npcId={packet.NpcID} " +
                    $"shopId={packet.ShopID} " +
                    $"count={packet.Count} " +
                    $"index={packet.Index} " +
                    $"items={packet.Items.Count}.")
                .ConfigureAwait(false);
        }
    }



    private static void AssertSystemTalk(byte[] bytes, string expectedMessage)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, message.ChatType);
        Assert.Equal(expectedMessage, message.Content[3]);
    }

    private static void AssertSingleUpdate(
        byte[] bytes,
        LegacyMsgUpdateDataType expectedType,
        ulong expectedValue)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        var entry = Assert.Single(update.Updates);
        Assert.Equal(expectedType, entry.Type);
        Assert.Equal(expectedValue, entry.Value);
    }

    private static void AssertMailboxAck(
        byte[] bytes,
        LegacyMailboxModeType expectedType,
        uint expectedId,
        IReadOnlyList<ulong> expectedItems)
    {
        Assert.True(LegacyMailboxModePacket.TryParse(ReadFrame(bytes), out var ack));
        Assert.Equal(expectedType, ack.Type);
        Assert.Equal(expectedId, ack.Id);
        Assert.Equal(expectedItems, ack.Items);
    }

    private static void AssertMailListRefresh(
        byte[] bytes,
        ulong mailId,
        ulong money,
        ulong eMoney,
        ulong flag)
    {
        Assert.True(LegacyMailListPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyMailListType.ClaimAllUpdate, packet.Type);
        Assert.Equal(1, packet.Count);
        var mail = Assert.Single(packet.Mails);
        Assert.Equal(mailId, mail.Id);
        Assert.Equal(money, mail.Money);
        Assert.Equal(eMoney, mail.EMoney);
        Assert.Equal(flag, mail.Flag);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
