using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class VisibleDx8D13TrainingVitalityRetreatInfoPacketTests
{
    private const uint SelfUid = 1_293_600;
    private const string RetreatedPower = "1-11|2-22|3-33|4-44";
    private const string ExactAction7Hex =
        "180049090000000007000000000000000000000000000000";

    [Fact]
    public void ExactD13Action7RequestUsesTheProvenZeroed24ByteLayout()
    {
        var packet = new VisibleDx8D13TrainingVitalityPacket(
            0,
            (ushort)VisibleDx8D13TrainingVitalityAction.RetreatChiPowers,
            0,
            0,
            0,
            0,
            0);

        Assert.Equal(ExactAction7Hex, Convert.ToHexString(packet.ToFrameBytes()));
    }

    [Fact]
    public void EmptySnapshotRoundTripsAsCountOnlyFrame()
    {
        var packet = VisibleDx8D13TrainingVitalityProtectInfoPacket.FromSnapshot(
            CreateSnapshot(retreatCount: 0));
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal((ushort)VisibleDx8D13TrainingVitalityProtectInfoPacket.Opcode, frame.Header.Opcode);
        Assert.Equal((ushort)2196, frame.Header.Opcode);
        Assert.Equal(8, bytes.Length);
        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(0U, reader.ReadUInt32(0));
        Assert.True(VisibleDx8D13TrainingVitalityProtectInfoPacket.TryParse(frame, out var parsed));
        Assert.Empty(parsed.Records);
        Assert.Equal(bytes, parsed.ToFrameBytes());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void OneToFourRecordsRoundTripWithExactCountAndFrameLength(int retreatCount)
    {
        var packet = VisibleDx8D13TrainingVitalityProtectInfoPacket.FromSnapshot(
            CreateSnapshot(retreatCount));
        var bytes = packet.ToFrameBytes();

        Assert.Equal(8 + (21 * retreatCount), bytes.Length);
        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal((uint)retreatCount, reader.ReadUInt32(0));
        Assert.True(
            VisibleDx8D13TrainingVitalityProtectInfoPacket.TryParse(
                ReadFrame(bytes),
                out var parsed));
        Assert.Equal(retreatCount, parsed.Records.Count);
        Assert.Equal(bytes, parsed.ToFrameBytes());
    }

    [Fact]
    public void RecordStartsAtPayloadOffsetFourAndUsesExact21ByteLayout()
    {
        var bytes = VisibleDx8D13TrainingVitalityProtectInfoPacket
            .FromSnapshot(CreateSnapshot(retreatCount: 1))
            .ToFrameBytes();

        Assert.Equal(29, bytes.Length);
        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(1U, reader.ReadUInt32(0));
        Assert.Equal((byte)LegacyChiPowerType.Dragon, reader.Payload[4]);
        Assert.Equal(0U, reader.ReadUInt32(5));
        Assert.Equal(10_011, reader.ReadInt32(9));
        Assert.Equal(20_022, reader.ReadInt32(13));
        Assert.Equal(30_033, reader.ReadInt32(17));
        Assert.Equal(40_044, reader.ReadInt32(21));
    }

    [Fact]
    public void PermanentDateTimeBinaryOnePacksExpiryAsZero()
    {
        var bytes = VisibleDx8D13TrainingVitalityProtectInfoPacket
            .FromSnapshot(CreateSnapshot(retreatCount: 1, dragonTime: "1"))
            .ToFrameBytes();

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(0U, reader.ReadUInt32(5));
    }

    [Fact]
    public void NormalDateTimeBinaryPacksExpiryAsYyMmDdHhMm()
    {
        var expiry = new DateTime(2026, 7, 12, 14, 35, 0, DateTimeKind.Unspecified);
        var bytes = VisibleDx8D13TrainingVitalityProtectInfoPacket
            .FromSnapshot(
                CreateSnapshot(
                    retreatCount: 1,
                    dragonTime: expiry.ToBinary().ToString(CultureInfo.InvariantCulture)))
            .ToFrameBytes();

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(2_607_121_435U, reader.ReadUInt32(5));
    }

    [Fact]
    public async Task Action7ReturnsOneProtectInfoFrameWithoutMutatingSession()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var handler = CreateHandler(log);
        var session = CreateSession(CreateSnapshot(retreatCount: 1));
        GetSessions(handler).Set(connection, session);

        await handler.HandleFrameAsync(connection, Action7Packet());

        var response = Assert.Single(sent);
        var frame = ReadFrame(response);
        Assert.Equal((ushort)2196, frame.Header.Opcode);
        Assert.True(
            VisibleDx8D13TrainingVitalityProtectInfoPacket.TryParse(
                frame,
                out var info));
        Assert.Single(info.Records);
        AssertStoredUnchanged(handler, connection, session);
    }

    [Fact]
    public async Task Action7WithNoRetreatStateReturnsOneEmptyProtectInfoFrame()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var handler = CreateHandler(log);
        var session = CreateSession(CreateSnapshot(retreatCount: 0));
        GetSessions(handler).Set(connection, session);

        await handler.HandleFrameAsync(connection, Action7Packet());

        var response = Assert.Single(sent);
        Assert.True(
            VisibleDx8D13TrainingVitalityProtectInfoPacket.TryParse(
                ReadFrame(response),
                out var info));
        Assert.Empty(info.Records);
        AssertStoredUnchanged(handler, connection, session);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Action7WithCorruptPersistedRetreatStateFailsClosed(bool corruptTime)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var handler = CreateHandler(log);
        var snapshot = CreateSnapshot(retreatCount: 1);
        var attributes = new Dictionary<string, string>(snapshot.Attributes, StringComparer.OrdinalIgnoreCase);
        if (corruptTime)
        {
            attributes["DragonTime"] = "not-a-binary-time";
        }
        else
        {
            attributes.Remove("RetreatedDragon");
        }

        var session = CreateSession(snapshot with { Attributes = attributes });
        GetSessions(handler).Set(connection, session);

        await handler.HandleFrameAsync(connection, Action7Packet());

        Assert.Empty(sent);
        AssertStoredUnchanged(handler, connection, session);
        Assert.Contains("mutation=none reason=retreatInfoStateInvalid:", log.ToString());
    }

    public static TheoryData<uint, byte, byte, uint, uint, uint> InvalidAuxiliaryFields => new()
    {
        { SelfUid, 0, 0, 0, 0, 0 },
        { 0, 1, 0, 0, 0, 0 },
        { 0, 0, 1, 0, 0, 0 },
        { 0, 0, 0, 1, 0, 0 },
        { 0, 0, 0, 0, 1, 0 },
        { 0, 0, 0, 0, 0, 1 },
    };

    [Theory]
    [MemberData(nameof(InvalidAuxiliaryFields))]
    public async Task Action7RejectsEveryNonzeroAuxiliaryFieldWithoutResponseOrMutation(
        uint uid,
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
        var session = CreateSession(CreateSnapshot(retreatCount: 1));
        GetSessions(handler).Set(connection, session);
        var request = new VisibleDx8D13TrainingVitalityPacket(
            uid,
            (ushort)VisibleDx8D13TrainingVitalityAction.RetreatChiPowers,
            power,
            lockedFlags,
            reserved0,
            reserved1,
            reserved2);

        await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

        Assert.Empty(sent);
        AssertStoredUnchanged(handler, connection, session);
    }

    [Fact]
    public async Task Action7WithoutAuthenticatedSessionSendsNothing()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = CreateHandler(log);

        await handler.HandleFrameAsync(CreateConnection(sent), Action7Packet());

        Assert.Empty(sent);
    }

    [Fact]
    public async Task MalformedAction7FrameIsConsumedWithoutResponseOrDisconnect()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var disconnectCount = 0;
        var connection = CreateConnection(sent, () => disconnectCount++);
        var handler = CreateHandler(log);
        var bytes = Convert.FromHexString(ExactAction7Hex);
        Array.Resize(ref bytes, bytes.Length - 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
            bytes,
            (ushort)bytes.Length);

        await handler.HandleFrameAsync(connection, ReadFrame(bytes));

        Assert.Equal(0, disconnectCount);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task RootD11DoesNotReuseD13Action7OrProtectInfoLayout()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var disconnectCount = 0;
        var connection = CreateConnection(sent, () => disconnectCount++);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.RootD11_7899,
            log: log);

        await handler.HandleFrameAsync(connection, Action7Packet());

        Assert.Equal(0, disconnectCount);
        Assert.Empty(sent);
        Assert.Contains("PARITY-GAP unknown world opcode", log.ToString());
        Assert.DoesNotContain("CMsgTrainingVitalityProtectInfo", log.ToString());
    }

    private static WorldServerFrameHandler CreateHandler(TextWriter log)
    {
        return new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            log: log);
    }

    private static PacketFrame Action7Packet()
    {
        return ReadFrame(Convert.FromHexString(ExactAction7Hex));
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

    private static WorldSession CreateSession(CharacterSnapshot character)
    {
        return new WorldSession(
            AccountId: character.Uid,
            Username: character.Name.ToLowerInvariant(),
            Character: character,
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateSnapshot(int retreatCount, string dragonTime = "1")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var retreats = new[]
        {
            (TimeKey: "DragonTime", PowerKey: "RetreatedDragon"),
            (TimeKey: "PhoenixTime", PowerKey: "RetreatedPhoenix"),
            (TimeKey: "TigerTime", PowerKey: "RetreatedTiger"),
            (TimeKey: "TurtleTime", PowerKey: "RetreatedTurtle"),
        };

        for (var index = 0; index < retreatCount; index++)
        {
            attributes[retreats[index].TimeKey] = index == 0 ? dragonTime : "1";
            attributes[retreats[index].PowerKey] = RetreatedPower;
        }

        return new CharacterSnapshot(
            Uid: SelfUid,
            Name: "Chi",
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
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(
            PacketFrameDecoder.TryRead(
                new ReadOnlySequence<byte>(bytes),
                out var frame,
                out _,
                out _));
        return frame;
    }
}
