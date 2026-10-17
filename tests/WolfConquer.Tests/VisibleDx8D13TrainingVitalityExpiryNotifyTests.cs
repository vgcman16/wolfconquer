using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class VisibleDx8D13TrainingVitalityExpiryNotifyTests
{
    private static readonly DateTime Now =
        new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ExactD13ConstantsMatchTheProvenClientContract()
    {
        Assert.Equal((ushort)2268, VisibleDx8D13TrainingVitalityExpiryNotifyPacket.Opcode);
        Assert.Equal((byte)1, VisibleDx8D13TrainingVitalityExpiryNotifyPacket.ExpiredState);
        Assert.Equal(4, VisibleDx8D13TrainingVitalityExpiryNotifyPacket.MaximumEntryCount);
    }

    [Fact]
    public void ExactD13WireUsesCountAtFourAndTwoBytesPerEntry()
    {
        var packet = new VisibleDx8D13TrainingVitalityExpiryNotifyPacket(
        [
            Expired(LegacyChiPowerType.Dragon),
            Expired(LegacyChiPowerType.Tiger),
        ]);

        var bytes = packet.ToFrameBytes();

        Assert.Equal(12, bytes.Length);
        Assert.Equal((ushort)12, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)));
        Assert.Equal((ushort)2268, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        Assert.Equal(2U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)));
        Assert.Equal((byte)LegacyChiPowerType.Dragon, bytes[8]);
        Assert.Equal((byte)1, bytes[9]);
        Assert.Equal((byte)LegacyChiPowerType.Tiger, bytes[10]);
        Assert.Equal((byte)1, bytes[11]);
        Assert.Equal("0C00DC080200000001010301", Convert.ToHexString(bytes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ZeroThroughFourEntriesRoundTripWithoutLayoutDrift(int entryCount)
    {
        var entries = Enumerable.Range(1, entryCount)
            .Select(value => Expired((LegacyChiPowerType)value))
            .ToArray();
        var bytes = new VisibleDx8D13TrainingVitalityExpiryNotifyPacket(entries).ToFrameBytes();

        Assert.Equal(8 + (2 * entryCount), bytes.Length);
        Assert.True(
            VisibleDx8D13TrainingVitalityExpiryNotifyPacket.TryParse(
                ReadFrame(bytes),
                out var parsed));
        Assert.Equal(entries, parsed.Entries);
        Assert.Equal(bytes, parsed.ToFrameBytes());
    }

    [Fact]
    public void ParserRejectsWrongOpcodeCountLengthAndInvalidEntries()
    {
        Assert.False(TryParseHex("0A00DD08010000000101"));
        Assert.False(TryParseHex("0A00DC08020000000101"));
        Assert.False(TryParseHex("0800DC0805000000"));
        Assert.False(TryParseHex("0A00DC08010000000001"));
        Assert.False(TryParseHex("0A00DC08010000000100"));
        Assert.False(TryParseHex("0C00DC080200000001010101"));
    }

    [Fact]
    public void SerializerRejectsMoreThanFourOrSemanticallyInvalidEntries()
    {
        var tooMany = Enumerable.Range(0, 5)
            .Select(index => Expired((LegacyChiPowerType)((index % 4) + 1)))
            .ToArray();
        var invalidState = new VisibleDx8D13TrainingVitalityExpiryNotifyPacket(
        [
            new VisibleDx8D13TrainingVitalityExpiryNotifyEntry(
                LegacyChiPowerType.Dragon,
                State: 0),
        ]);
        var duplicatePower = new VisibleDx8D13TrainingVitalityExpiryNotifyPacket(
        [
            Expired(LegacyChiPowerType.Phoenix),
            Expired(LegacyChiPowerType.Phoenix),
        ]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new VisibleDx8D13TrainingVitalityExpiryNotifyPacket(tooMany).ToFrameBytes());
        Assert.Throws<ArgumentOutOfRangeException>(() => invalidState.ToFrameBytes());
        Assert.Throws<ArgumentOutOfRangeException>(() => duplicatePower.ToFrameBytes());
    }

    [Fact]
    public void VipFourIsTheMinimumLevelThatCanReceiveExpiryNotification()
    {
        var expiredTime = BinaryTime(Now.AddMinutes(-1));

        Assert.False(
            VisibleDx8D13TrainingVitalityExpiryNotifyPacket.TryCreateFromSnapshot(
                CreateSnapshot(3, ("DragonTime", expiredTime)),
                Now,
                out _,
                out _));
        Assert.True(
            VisibleDx8D13TrainingVitalityExpiryNotifyPacket.TryCreateFromSnapshot(
                CreateSnapshot(4, ("DragonTime", expiredTime)),
                Now,
                out var packet,
                out _));
        Assert.Equal(
            [Expired(LegacyChiPowerType.Dragon)],
            packet.Entries);
    }

    [Fact]
    public void ExpiredRetreatsUseDragonPhoenixTigerTurtleOrderRegardlessOfStorageOrder()
    {
        var expiredTime = BinaryTime(Now.AddHours(-1));
        var snapshot = CreateSnapshot(
            6,
            ("TurtleTime", expiredTime),
            ("TigerTime", expiredTime),
            ("PhoenixTime", expiredTime),
            ("DragonTime", expiredTime));

        Assert.True(
            VisibleDx8D13TrainingVitalityExpiryNotifyPacket.TryCreateFromSnapshot(
                snapshot,
                Now,
                out var packet,
                out _));

        Assert.Equal(
        [
            Expired(LegacyChiPowerType.Dragon),
            Expired(LegacyChiPowerType.Phoenix),
            Expired(LegacyChiPowerType.Tiger),
            Expired(LegacyChiPowerType.Turtle),
        ],
            packet.Entries);
    }

    [Fact]
    public void FutureRetreatsAreOmittedWhileExpiredRetreatsAreReported()
    {
        var snapshot = CreateSnapshot(
            4,
            ("DragonTime", BinaryTime(Now.AddMinutes(-1))),
            ("PhoenixTime", BinaryTime(Now.AddMinutes(1))));

        Assert.True(
            VisibleDx8D13TrainingVitalityExpiryNotifyPacket.TryCreateFromSnapshot(
                snapshot,
                Now,
                out var packet,
                out _));
        Assert.Equal(
            [Expired(LegacyChiPowerType.Dragon)],
            packet.Entries);
    }

    [Fact]
    public void PermanentBinarySentinelOneIsNeverReportedAsExpired()
    {
        Assert.False(
            VisibleDx8D13TrainingVitalityExpiryNotifyPacket.TryCreateFromSnapshot(
                CreateSnapshot(4, ("DragonTime", "1")),
                Now,
                out var packet,
                out _));
        Assert.Empty(packet.Entries);
    }

    [Fact]
    public void SnapshotWithNoExpiredTimedRetreatsProducesNoPacket()
    {
        var snapshot = CreateSnapshot(
            4,
            ("DragonTime", "0"),
            ("PhoenixTime", BinaryTime(Now.AddMinutes(1))));

        Assert.False(
            VisibleDx8D13TrainingVitalityExpiryNotifyPacket.TryCreateFromSnapshot(
                snapshot,
                Now,
                out var packet,
                out _));
        Assert.Empty(packet.Entries);
    }

    [Theory]
    [InlineData("not-a-binary-time")]
    [InlineData("9223372036854775807")]
    public void MalformedRetreatTimeFailsClosedEvenAfterAValidExpiredEntry(string malformedTime)
    {
        var snapshot = CreateSnapshot(
            4,
            ("DragonTime", BinaryTime(Now.AddMinutes(-1))),
            ("PhoenixTime", malformedTime));

        Assert.False(
            VisibleDx8D13TrainingVitalityExpiryNotifyPacket.TryCreateFromSnapshot(
                snapshot,
                Now,
                out var packet,
                out _));
        Assert.Empty(packet.Entries);
    }

    [Fact]
    public void ProtectRequestAndResponseKeepDistinctOpcodesWithTheSameExactLayout()
    {
        var packet = new VisibleDx8D13TrainingVitalityProtectPacket(
            (ushort)VisibleDx8D13TrainingVitalityProtectAction.Restore,
            (byte)LegacyChiPowerType.Tiger,
            Reserved: 0x11223344);

        var requestBytes = packet.ToFrameBytes();
        var responseBytes = packet.ToResponseFrameBytes();

        Assert.Equal("0B00DC0804000344332211", Convert.ToHexString(requestBytes));
        Assert.Equal("0B00FE0804000344332211", Convert.ToHexString(responseBytes));
        Assert.Equal(11, requestBytes.Length);
        Assert.Equal(11, responseBytes.Length);
        Assert.Equal(requestBytes.AsSpan(4).ToArray(), responseBytes.AsSpan(4).ToArray());
        Assert.Equal((ushort)2268, ReadFrame(requestBytes).Header.Opcode);
        Assert.Equal((ushort)2302, ReadFrame(responseBytes).Header.Opcode);
        Assert.True(
            VisibleDx8D13TrainingVitalityProtectPacket.TryParse(
                ReadFrame(requestBytes),
                out var parsedRequest));
        Assert.True(
            VisibleDx8D13TrainingVitalityProtectPacket.TryParseResponse(
                ReadFrame(responseBytes),
                out var parsedResponse));
        Assert.Equal(packet, parsedRequest);
        Assert.Equal(packet, parsedResponse);
        Assert.False(
            VisibleDx8D13TrainingVitalityProtectPacket.TryParse(
                ReadFrame(responseBytes),
                out _));
    }

    private static VisibleDx8D13TrainingVitalityExpiryNotifyEntry Expired(
        LegacyChiPowerType powerType)
    {
        return new VisibleDx8D13TrainingVitalityExpiryNotifyEntry(
            powerType,
            VisibleDx8D13TrainingVitalityExpiryNotifyPacket.ExpiredState);
    }

    private static string BinaryTime(DateTime value)
    {
        return value.ToBinary().ToString(CultureInfo.InvariantCulture);
    }

    private static CharacterSnapshot CreateSnapshot(
        int vipLevel,
        params (string Key, string Value)[] retreatTimes)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VipLevel"] = vipLevel.ToString(CultureInfo.InvariantCulture),
        };
        foreach (var (key, value) in retreatTimes)
        {
            attributes[key] = value;
        }

        return new CharacterSnapshot(
            Uid: 1_293_600,
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

    private static bool TryParseHex(string frameHex)
    {
        return VisibleDx8D13TrainingVitalityExpiryNotifyPacket.TryParse(
            ReadFrame(Convert.FromHexString(frameHex)),
            out _);
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

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task HandlerVisibleDx8D13LoginSendsExactExpiredChiNotification()
    {
        var now = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(
            1_293_600,
            uid: 1_293_600,
            extraCharacterLines: CreateExpiryLoginLines(now));
        var sent = new List<byte[]>();
        var handler = CreateExpiryLoginHandler(
            ConquerProtocolProfile.VisibleDx8D13,
            now);

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new LegacyMsgLoginGamePacket(1_293_600).ToFrameBytes()));

        var expiryFrame = Assert.Single(sent, bytes => ReadFrame(bytes).Header.Opcode ==
                VisibleDx8D13TrainingVitalityExpiryNotifyPacket.Opcode);
        Assert.True(
            VisibleDx8D13TrainingVitalityExpiryNotifyPacket.TryParse(
                ReadFrame(expiryFrame),
                out var notification));
        var entry = Assert.Single(notification.Entries);
        Assert.Equal(LegacyChiPowerType.Dragon, entry.PowerType);
        Assert.Equal(VisibleDx8D13TrainingVitalityExpiryNotifyPacket.ExpiredState, entry.State);
    }

    [Fact]
    public async Task HandlerRootD11LoginDoesNotReceiveD13ChiExpiryNotification()
    {
        var now = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(
            1_293_600,
            uid: 1_293_600,
            extraCharacterLines: CreateExpiryLoginLines(now));
        var sent = new List<byte[]>();
        var handler = CreateExpiryLoginHandler(
            ConquerProtocolProfile.RootD11_7899,
            now);

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new LegacyMsgLoginGamePacket(1_293_600).ToFrameBytes()));

        Assert.DoesNotContain(
            sent,
            bytes => ReadFrame(bytes).Header.Opcode ==
                VisibleDx8D13TrainingVitalityExpiryNotifyPacket.Opcode);
    }

    private WorldServerFrameHandler CreateExpiryLoginHandler(
        ConquerProtocolProfile profile,
        DateTimeOffset now)
    {
        return new WorldServerFrameHandler(
            snapshotLoader: new CharacterSnapshotLoader(_usersRoot),
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: false,
                TickInterval: TimeSpan.FromSeconds(1),
                TimeProvider: new D13ExpiryFixedTimeProvider(now)),
            log: null,
            protocolProfile: profile);
    }

    private static string CreateExpiryLoginLines(DateTimeOffset now)
    {
        return string.Join(
            "\r\n",
            "VipLevel=4",
            "DragonTime=" + now.UtcDateTime.AddMinutes(-1).ToBinary()
                .ToString(CultureInfo.InvariantCulture),
            "PhoenixTime=1",
            "TigerTime=" + now.UtcDateTime.AddMinutes(1).ToBinary()
                .ToString(CultureInfo.InvariantCulture),
            "TurtleTime=0");
    }

    private sealed class D13ExpiryFixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }

}
