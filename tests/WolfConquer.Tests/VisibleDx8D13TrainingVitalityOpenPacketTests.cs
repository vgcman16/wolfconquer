using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class VisibleDx8D13TrainingVitalityOpenPacketTests
{
    private const uint SelfUid = 1_293_600;
    private const uint TargetUid = 1_293_601;
    private const string DragonPower = "1/5963000/8/2500/1/3/200/0/6/3500/0/2/200/0/";

    [Fact]
    public void D13BuilderActionValuesRemainExact()
    {
        Assert.Equal(0, (ushort)VisibleDx8D13TrainingVitalityAction.Unlock);
        Assert.Equal(1, (ushort)VisibleDx8D13TrainingVitalityAction.Open);
        Assert.Equal(2, (ushort)VisibleDx8D13TrainingVitalityAction.Study);
        Assert.Equal(3, (ushort)VisibleDx8D13TrainingVitalityAction.BuyPoints);
        Assert.Equal(6, (ushort)VisibleDx8D13TrainingVitalityAction.Buy200);
        Assert.Equal(7, (ushort)VisibleDx8D13TrainingVitalityAction.RetreatChiPowers);
    }

    [Fact]
    public void ExactD13FrameParsesAndSerializesWithoutLayoutDrift()
    {
        const string exactFrameHex =
            "180049097856341201000000000000000000000000000000";
        var frame = ReadFrame(Convert.FromHexString(exactFrameHex));

        Assert.True(VisibleDx8D13TrainingVitalityPacket.TryParse(frame, out var packet));
        Assert.Equal(0x12345678U, packet.Uid);
        Assert.Equal((ushort)VisibleDx8D13TrainingVitalityAction.Open, packet.Action);
        Assert.Equal(0, packet.Power);
        Assert.Equal(0, packet.LockedFlags);
        Assert.True(packet.HasZeroReserved);
        Assert.Equal(exactFrameHex, Convert.ToHexString(packet.ToFrameBytes()));
    }

    [Fact]
    public async Task OpenSelfSendsOneExactD13ChiInfoFrameWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var handler = CreateHandler(log);
        var session = CreateSession(SelfUid, "Self");
        GetSessions(handler).Set(connection, session);

        await handler.HandleFrameAsync(connection, OpenPacket(SelfUid));

        var response = Assert.Single(sent);
        Assert.True(VisibleDx8D13TrainingVitalityInfoPacket.TryParse(ReadFrame(response), out var info));
        Assert.Equal(LegacyChiInfoAction.Send, info.Action);
        Assert.Equal(SelfUid, info.Uid);
        Assert.Equal(153_850, info.ChiPoints);
        Assert.Equal((ushort)VisibleDx8D13TrainingVitalityInfoPacket.Opcode, ReadFrame(response).Header.Opcode);
        AssertStoredUnchanged(handler, connection, session);
        Assert.Contains("mutation=none reason=openedSelf", log.ToString());
    }

    [Fact]
    public async Task OpenActiveTargetSendsTargetChiInfoToRequesterWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var requesterConnection = CreateConnection(sent);
        var targetConnection = CreateConnection([]);
        var handler = CreateHandler(log);
        var requester = CreateSession(SelfUid, "Requester");
        var target = CreateSession(TargetUid, "Target", chiPoints: 77_777);
        var sessions = GetSessions(handler);
        sessions.Set(requesterConnection, requester);
        sessions.Set(targetConnection, target);

        await handler.HandleFrameAsync(requesterConnection, OpenPacket(TargetUid));

        var response = Assert.Single(sent);
        Assert.True(VisibleDx8D13TrainingVitalityInfoPacket.TryParse(ReadFrame(response), out var info));
        Assert.Equal(LegacyChiInfoAction.Send, info.Action);
        Assert.Equal(TargetUid, info.Uid);
        Assert.Equal(77_777, info.ChiPoints);
        AssertStoredUnchanged(handler, requesterConnection, requester);
        AssertStoredUnchanged(handler, targetConnection, target);
        Assert.Contains("mutation=none reason=openedActiveTarget", log.ToString());
    }

    public static TheoryData<byte, byte, uint, uint, uint> NonzeroAuxiliaryFields => new()
    {
        { 1, 0, 0, 0, 0 },
        { 0, 1, 0, 0, 0 },
        { 0, 0, 1, 0, 0 },
        { 0, 0, 0, 1, 0 },
        { 0, 0, 0, 0, 1 },
    };

    [Theory]
    [MemberData(nameof(NonzeroAuxiliaryFields))]
    public async Task OpenRejectsEveryUnprovenAuxiliaryFieldWithoutResponseOrMutation(
        byte power,
        byte lockedFlags,
        uint reserved0,
        uint reserved1,
        uint reserved2)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var handler = CreateHandler(log);
        var session = CreateSession(SelfUid, "Self");
        GetSessions(handler).Set(connection, session);
        var packet = new VisibleDx8D13TrainingVitalityPacket(
            SelfUid,
            (ushort)VisibleDx8D13TrainingVitalityAction.Open,
            power,
            lockedFlags,
            reserved0,
            reserved1,
            reserved2);

        await handler.HandleFrameAsync(connection, ReadFrame(packet.ToFrameBytes()));

        Assert.Empty(sent);
        AssertStoredUnchanged(handler, connection, session);
        Assert.Contains("mutation=none reason=auxiliaryFieldsNonzero", log.ToString());
    }

    [Fact]
    public async Task OpenWithoutAuthenticatedSessionSendsNothing()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = CreateHandler(log);

        await handler.HandleFrameAsync(CreateConnection(sent), OpenPacket(SelfUid));

        Assert.Empty(sent);
        Assert.Contains("mutation=none reason=sessionMissing", log.ToString());
    }

    [Fact]
    public async Task OpenMissingActiveTargetSendsNothingAndDoesNotMutateRequester()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var handler = CreateHandler(log);
        var session = CreateSession(SelfUid, "Self");
        GetSessions(handler).Set(connection, session);

        await handler.HandleFrameAsync(connection, OpenPacket(TargetUid));

        Assert.Empty(sent);
        AssertStoredUnchanged(handler, connection, session);
        Assert.Contains("mutation=none reason=targetMissing", log.ToString());
    }

    [Fact]
    public async Task MalformedD13LengthIsConsumedWithoutDisconnect()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var disconnectCount = 0;
        var connection = CreateConnection(sent, () => disconnectCount++);
        var handler = CreateHandler(log);
        var bytes = new VisibleDx8D13TrainingVitalityPacket(
            SelfUid,
            (ushort)VisibleDx8D13TrainingVitalityAction.Open,
            0,
            0,
            0,
            0,
            0).ToFrameBytes();
        Array.Resize(ref bytes, bytes.Length - 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)bytes.Length);

        await handler.HandleFrameAsync(connection, ReadFrame(bytes));

        Assert.Equal(0, disconnectCount);
        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("PROTOCOL-MALFORMED consumed world packet", output);
        Assert.Contains("profile=VisibleDx8D13 session=none opcode=2377", output);
        Assert.Contains("packet=CMsgTrainingVitality action=ignore", output);
    }

    [Theory]
    [InlineData((ushort)VisibleDx8D13TrainingVitalityAction.Unlock)]
    [InlineData((ushort)VisibleDx8D13TrainingVitalityAction.Buy200)]
    public async Task UnfinishedKnownActionIsFencedWithoutResponseOrMutation(ushort action)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var handler = CreateHandler(log);
        var session = CreateSession(SelfUid, "Self");
        GetSessions(handler).Set(connection, session);
        var packet = new VisibleDx8D13TrainingVitalityPacket(SelfUid, action, 0, 0, 0, 0, 0);

        await handler.HandleFrameAsync(connection, ReadFrame(packet.ToFrameBytes()));

        Assert.Empty(sent);
        AssertStoredUnchanged(handler, connection, session);
        Assert.Contains($"action={action}", log.ToString());
        Assert.Contains("knownAction=True mutation=none reason=actionFenced", log.ToString());
    }

    [Fact]
    public async Task RootD11DoesNotReuseD13Opcode2377OrLayout()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var disconnectCount = 0;
        var connection = CreateConnection(sent, () => disconnectCount++);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.RootD11_7899,
            log: log);

        await handler.HandleFrameAsync(connection, OpenPacket(SelfUid));

        Assert.Equal(0, disconnectCount);
        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("PARITY-GAP unknown world opcode", output);
        Assert.Contains("opcode=2377", output);
        Assert.DoesNotContain("CMsgTrainingVitality ", output);
    }

    private static WorldServerFrameHandler CreateHandler(TextWriter log)
    {
        return new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            log: log);
    }

    private static PacketFrame OpenPacket(uint uid)
    {
        return ReadFrame(new VisibleDx8D13TrainingVitalityPacket(
            uid,
            (ushort)VisibleDx8D13TrainingVitalityAction.Open,
            0,
            0,
            0,
            0,
            0).ToFrameBytes());
    }

    private static WorldSessionStore GetSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static void AssertStoredUnchanged(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        WorldSession expected)
    {
        Assert.True(GetSessions(handler).TryGet(connection, out var actual));
        Assert.Same(expected, actual);
    }

    private static ConquerTcpConnection CreateConnection(
        List<byte[]> sent,
        Action? disconnect = null)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9_958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            },
            disconnectAsync: _ =>
            {
                disconnect?.Invoke();
                return ValueTask.CompletedTask;
            });
    }

    private static WorldSession CreateSession(uint uid, string name, int chiPoints = 153_850)
    {
        return new WorldSession(
            AccountId: uid,
            Username: name.ToLowerInvariant(),
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 10,
                FirstClass: 10,
                SecondClass: 0,
                Reborn: 1,
                Level: 140,
                Experience: 0,
                MapId: 1002,
                X: 430,
                Y: 378,
                PreviousMapId: 1002,
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
                Online: 1,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ChiPoints"] = chiPoints.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["Dragon"] = DragonPower,
                },
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }
}
