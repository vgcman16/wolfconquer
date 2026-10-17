using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRuntimeUpdateTickerTests
{
    [Fact]
    public void FirstDueTickEmitsXpStaminaThenStatusVector()
    {
        var now = new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(),
            now,
            hasOverwhelmRune: true,
            hasRevelatorStartupEffect: true);

        var frames = ticker.CreateDueFrameBytes(now);

        Assert.Equal(3, frames.Count);
        Assert.Equal(CreateSyntheticUpdateFrame(type: 27, value: 1), frames[0]);
        Assert.Equal(CreateSyntheticUpdateFrame(type: 8, value: 5), frames[1]);
        var expectedFlags = new ulong[18];
        expectedFlags[7] = 1UL << 19;
        expectedFlags[12] = 1UL << 24;
        Assert.Equal(CreateSyntheticUpdateFrame(type: 25, value: 0, expectedFlags), frames[2]);

        AssertSingleUpdate(frames[0], LegacyMsgUpdateDataType.XPCircle, 1);
        AssertSingleUpdate(frames[1], LegacyMsgUpdateDataType.Stamina, 5);
        var status = AssertSingleParsedUpdate(frames[2]);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, status.Type);
        Assert.Equal(LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength, status.Values!.Count);
        Assert.True(IsLegacyStatusFlagSet(status.Values, LegacyRuntimeUpdateTicker.OverwhelmFlag));
        Assert.True(IsLegacyStatusFlagSet(status.Values, LegacyRuntimeUpdateTicker.RevelatorFlag));
    }

    [Fact]
    public void LaterTicksFollowStaminaAndXpScheduleWithoutDuplicateStatusVector()
    {
        var start = new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(),
            start,
            hasOverwhelmRune: true,
            hasRevelatorStartupEffect: true);

        _ = ticker.CreateDueFrameBytes(start);

        Assert.Empty(ticker.CreateDueFrameBytes(start.AddSeconds(2)));
        AssertSingleUpdate(
            Assert.Single(ticker.CreateDueFrameBytes(start.AddSeconds(3))),
            LegacyMsgUpdateDataType.Stamina,
            10);
        AssertSingleUpdate(
            Assert.Single(ticker.CreateDueFrameBytes(start.AddSeconds(6))),
            LegacyMsgUpdateDataType.Stamina,
            15);
        AssertSingleUpdate(
            Assert.Single(ticker.CreateDueFrameBytes(start.AddSeconds(8))),
            LegacyMsgUpdateDataType.XPCircle,
            2);
        AssertSingleUpdate(
            Assert.Single(ticker.CreateDueFrameBytes(start.AddSeconds(9))),
            LegacyMsgUpdateDataType.Stamina,
            20);
    }

    [Fact]
    public void StatusVectorOnlyContainsSourceBackedFlagsThatAreEnabled()
    {
        var now = new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(),
            now,
            hasOverwhelmRune: false,
            hasRevelatorStartupEffect: true);

        var frames = ticker.CreateDueFrameBytes(now);

        var status = AssertSingleParsedUpdate(frames[2]);
        Assert.False(IsLegacyStatusFlagSet(status.Values!, LegacyRuntimeUpdateTicker.OverwhelmFlag));
        Assert.True(IsLegacyStatusFlagSet(status.Values!, LegacyRuntimeUpdateTicker.RevelatorFlag));
    }

    [Fact]
    public void RuntimeStatusVectorPreservesExistingAuthoritativeFlags()
    {
        const int existingFlag = 0x78;
        var now = new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero);
        var flags = new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength];
        flags[existingFlag / 32] = 1UL << (existingFlag % 32);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(new Dictionary<string, string>
            {
                ["StatusFlags"] = string.Join('/', flags) + "/",
            }),
            now,
            hasOverwhelmRune: true,
            hasRevelatorStartupEffect: true);

        var frames = ticker.CreateDueFrameBytes(now);

        var status = AssertSingleParsedUpdate(frames[2]);
        Assert.True(IsLegacyStatusFlagSet(status.Values!, existingFlag));
        Assert.True(IsLegacyStatusFlagSet(status.Values!, LegacyRuntimeUpdateTicker.OverwhelmFlag));
        Assert.True(IsLegacyStatusFlagSet(status.Values!, LegacyRuntimeUpdateTicker.RevelatorFlag));
    }

    [Fact]
    public void OverwhelmOnlyStatusVectorDecodesAsLegacyOverwhelmFlag()
    {
        var flags = new ulong[18];
        flags[7] = 1UL << 19;
        var bytes = CreateSyntheticUpdateFrame(type: 25, value: 0, flags);

        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));

        Assert.Equal(900_301U, packet.Uid);
        var status = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, status.Type);
        Assert.Equal(LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength, status.Values!.Count);
        Assert.Equal(0x80000UL, status.Values[7]);
        Assert.True(IsLegacyStatusFlagSet(status.Values, LegacyRuntimeUpdateTicker.OverwhelmFlag));
        Assert.False(IsLegacyStatusFlagSet(status.Values, LegacyRuntimeUpdateTicker.RevelatorFlag));
    }

    [Fact]
    public void NoStatusVectorIsSentWhenNoRuntimeStatusFlagIsEnabled()
    {
        var now = new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(),
            now,
            hasOverwhelmRune: false,
            hasRevelatorStartupEffect: false);

        var frames = ticker.CreateDueFrameBytes(now);

        Assert.Equal(2, frames.Count);
        AssertSingleUpdate(frames[0], LegacyMsgUpdateDataType.XPCircle, 1);
        AssertSingleUpdate(frames[1], LegacyMsgUpdateDataType.Stamina, 5);
    }

    [Fact]
    public void XpListActivationEmitsLegacyXpCircleStatusFlagXpListAndEffectBurst()
    {
        var start = new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(),
            start,
            hasOverwhelmRune: false,
            hasRevelatorStartupEffect: false);

        IReadOnlyList<byte[]> frames = [];
        for (var count = 1; count <= 100; count++)
        {
            frames = ticker.CreateDueFrameBytes(start.AddSeconds((count - 1) * 8));
        }

        Assert.Equal(4, frames.Count);
        AssertSingleUpdate(frames[0], LegacyMsgUpdateDataType.XPCircle, 100);

        var status = AssertSingleParsedUpdate(frames[1]);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, status.Type);
        Assert.True(IsLegacyStatusFlagSet(status.Values!, LegacyRuntimeUpdateTicker.XpListFlag));
        AssertSingleUpdate(frames[2], LegacyMsgUpdateDataType.XPList, 1);

        Assert.True(LegacyStringPacket.TryParse(ReadFrame(frames[3]), out var effect));
        Assert.Equal(900_301U, effect.Uid);
        Assert.Equal(LegacyStringId.Effect, effect.Id);
        Assert.Equal(["xp"], effect.Strings);
    }

    [Fact]
    public void XpListActivationCarriesAuthoritativeSessionMutation()
    {
        var start = new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(new Dictionary<string, string> { ["XPCount"] = "99" }),
            start,
            hasOverwhelmRune: false,
            hasRevelatorStartupEffect: false);

        var updates = ticker.CreateDueUpdates(start);

        var circleUpdate = Assert.Single(
            updates,
            update => update.RuntimeXpCount == 100UL && update.RuntimeXpListActive is null);
        AssertSingleUpdate(circleUpdate.FrameBytes, LegacyMsgUpdateDataType.XPCircle, 100);
        var activationUpdate = Assert.Single(updates, update => update.RuntimeXpListActive == true);
        Assert.Equal(0UL, activationUpdate.RuntimeXpCount);
    }

    [Fact]
    public void ConsumedSessionXpListFlagResumesXpTicksWithoutWaitingForExpiry()
    {
        var start = new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(new Dictionary<string, string> { ["XPCount"] = "99" }),
            start,
            hasOverwhelmRune: false,
            hasRevelatorStartupEffect: false);
        _ = ticker.CreateDueUpdates(start);

        var consumedSnapshot = CreateSnapshot(new Dictionary<string, string> { ["XPCount"] = "0" });
        var updates = ticker.CreateDueUpdates(start.AddSeconds(8), consumedSnapshot);

        var update = Assert.Single(updates, candidate => candidate.RuntimeXpCount == 1UL);
        AssertSingleUpdate(update.FrameBytes, LegacyMsgUpdateDataType.XPCircle, 1);
        Assert.Equal(1UL, update.RuntimeXpCount);
        Assert.Null(update.RuntimeXpListActive);
    }

    [Fact]
    public void XpListFlagSuppressesXpTicksUntilLegacyRemovalBurst()
    {
        var start = new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(),
            start,
            hasOverwhelmRune: false,
            hasRevelatorStartupEffect: false);
        DateTimeOffset activationTime = default;
        for (var count = 1; count <= 100; count++)
        {
            activationTime = start.AddSeconds((count - 1) * 8);
            _ = ticker.CreateDueFrameBytes(activationTime);
        }

        Assert.Empty(ticker.CreateDueFrameBytes(activationTime.AddSeconds(8)));
        Assert.Empty(ticker.CreateDueFrameBytes(activationTime.AddSeconds(16)));

        var removalFrames = ticker.CreateDueFrameBytes(activationTime.AddSeconds(20));

        Assert.Equal(2, removalFrames.Count);
        AssertSingleUpdate(removalFrames[0], LegacyMsgUpdateDataType.XPList, 0);
        var status = AssertSingleParsedUpdate(removalFrames[1]);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, status.Type);
        Assert.False(IsLegacyStatusFlagSet(status.Values!, LegacyRuntimeUpdateTicker.XpListFlag));

        AssertSingleUpdate(
            Assert.Single(ticker.CreateDueFrameBytes(activationTime.AddSeconds(24))),
            LegacyMsgUpdateDataType.XPCircle,
            1);
    }

    [Fact]
    public void XpListExpiryCarriesAuthoritativeSessionMutation()
    {
        var start = new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero);
        var activeFlags = new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength];
        activeFlags[LegacyRuntimeUpdateTicker.XpListFlag / 32] =
            1UL << (LegacyRuntimeUpdateTicker.XpListFlag % 32);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(new Dictionary<string, string>
            {
                ["XPCount"] = "0",
                ["StatusFlags"] = string.Join('/', activeFlags) + "/",
            }),
            start,
            hasOverwhelmRune: false,
            hasRevelatorStartupEffect: false);

        var updates = ticker.CreateDueUpdates(start.AddSeconds(20), CreateSnapshot(new Dictionary<string, string>
        {
            ["XPCount"] = "0",
            ["StatusFlags"] = string.Join('/', activeFlags) + "/",
        }));

        var expiryUpdate = Assert.Single(updates, update => update.RuntimeXpListActive == false);
        Assert.Equal(0UL, expiryUpdate.RuntimeXpCount);
    }

    [Fact]
    public void OnlinePointsTalkMessageIsSentEveryTenMinutes()
    {
        var start = new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(),
            start,
            hasOverwhelmRune: false,
            hasRevelatorStartupEffect: false);

        _ = ticker.CreateDueFrameBytes(start);
        _ = ticker.CreateDueFrameBytes(start.AddSeconds(599));
        var frames = ticker.CreateDueFrameBytes(start.AddMinutes(10));

        var frame = Assert.Single(frames);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(frame), out var message));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, message.ChatType);
        Assert.Equal(
        [
            "SYSTEM",
            "ALL",
            string.Empty,
            "You get 1 Online Points.",
            string.Empty,
            string.Empty,
            string.Empty
        ], message.Content);

        Assert.Empty(ticker.CreateDueFrameBytes(start.AddMinutes(10).AddSeconds(1)));
        _ = ticker.CreateDueFrameBytes(start.AddMinutes(19).AddSeconds(59));
        Assert.True(LegacyMsgMessagePacket.TryParse(
            ReadFrame(Assert.Single(ticker.CreateDueFrameBytes(start.AddMinutes(20)))),
            out _));
    }

    private static byte[] CreateSyntheticUpdateFrame(uint type, ulong value, ulong[]? flags = null)
    {
        // Independent protobuf construction for the synthetic character, not the production update encoder.
        var update = new List<byte>();
        WriteVarint(update, 1UL << 3);
        WriteVarint(update, type);
        if (value != 0)
        {
            WriteVarint(update, 2UL << 3);
            WriteVarint(update, value);
        }

        foreach (var flag in flags ?? [])
        {
            WriteVarint(update, 3UL << 3);
            WriteVarint(update, flag);
        }

        var payload = new List<byte>();
        WriteVarint(payload, 1UL << 3);
        WriteVarint(payload, 900_301);
        WriteVarint(payload, (2UL << 3) | 2);
        WriteVarint(payload, (ulong)update.Count);
        payload.AddRange(update);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyMsgUpdate, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();

        static void WriteVarint(List<byte> bytes, ulong number)
        {
            while (number >= 128)
            {
                bytes.Add((byte)((number % 128) | 128));
                number /= 128;
            }

            bytes.Add((byte)number);
        }
    }

    private static void AssertSingleUpdate(
        byte[] bytes,
        LegacyMsgUpdateDataType type,
        ulong value)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(900_301U, parsed.Uid);
        Assert.Equal([new LegacyMsgUpdateEntry(type, value)], parsed.Updates);
    }

    private static LegacyMsgUpdateEntry AssertSingleParsedUpdate(byte[] bytes)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(900_301U, parsed.Uid);
        return Assert.Single(parsed.Updates);
    }

    private static bool IsLegacyStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var index = flag / 32;
        var bit = flag % 32;
        return index < values.Count && (values[index] & (1UL << bit)) != 0;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static CharacterSnapshot CreateSnapshot(
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new CharacterSnapshot(
            Uid: 900_301,
            Name: "SyntheticTicker",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: 140,
            Experience: 0,
            MapId: 1002,
            X: 705,
            Y: 577,
            PreviousMapId: 1002,
            PreviousMapX: 705,
            PreviousMapY: 577,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 61_229,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "",
            Attributes: new Dictionary<string, string>(
                attributes ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
