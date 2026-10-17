using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTopInHoursInvitationTests
{
    private const uint LegacyYellowColor = 0x00FFFF00;
    private const uint LegacyCenterChatType = 2_011;
    private const string TopInHoursMinute32Message =
        " TopInHours [xx:32] has begun! Would you like to join?";

    [Fact]
    public void TopInHoursInvitationMinute32WritesLegacyYellowCenterMessageShape()
    {
        var bytes = LegacyMsgMessagePacket.TopInHoursInvitation(32).ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var packet));
        Assert.Equal(105, bytes.Length);
        Assert.Equal((ushort)2179, frame.Header.Opcode);
        Assert.Equal(
        [
            0x69, 0x00, 0x83, 0x08,
            0x08, 0x80, 0xFE, 0xFF, 0x07,
            0x10, 0xDB, 0x0F
        ], bytes[..12]);
        Assert.Equal(LegacyYellowColor, packet.Color);
        Assert.Equal(LegacyCenterChatType, packet.ChatType);
        Assert.Equal(0U, packet.MessageUid1);
        Assert.Equal(0U, packet.MessageUid2);
        Assert.Equal(0U, packet.ServerId);
        Assert.Equal(0U, packet.Mesh);
        Assert.Equal(0U, packet.UnionType);
        Assert.Equal(1U, packet.Count);
        Assert.Equal(0U, packet.New1);
        Assert.Equal(0U, packet.New2);
        Assert.Equal(
        [
            "SYSTEM",
            "ALL",
            string.Empty,
            TopInHoursMinute32Message,
            string.Empty,
            string.Empty,
            string.Empty
        ], packet.Content);
    }

    [Fact]
    public void RuntimeTickerEmitsTopInHoursInvitationOnceAtMinute32Second6()
    {
        var start = new DateTimeOffset(2026, 6, 5, 8, 32, 5, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(),
            start,
            hasOverwhelmRune: false,
            hasRevelatorStartupEffect: false);

        _ = ticker.CreateDueFrameBytes(start);

        var frames = ticker.CreateDueFrameBytes(start.AddSeconds(1));

        var bytes = Assert.Single(frames);
        AssertTopInHoursMinute32Invitation(bytes);
        Assert.Empty(ticker.CreateDueFrameBytes(start.AddSeconds(2)));
    }

    private static void AssertTopInHoursMinute32Invitation(byte[] bytes)
    {
        var frame = ReadFrame(bytes);

        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var packet));
        Assert.Equal((ushort)2179, frame.Header.Opcode);
        Assert.Equal(LegacyYellowColor, packet.Color);
        Assert.Equal(LegacyCenterChatType, packet.ChatType);
        Assert.Equal(0U, packet.MessageUid1);
        Assert.Equal(0U, packet.MessageUid2);
        Assert.Equal(0U, packet.ServerId);
        Assert.Equal(0U, packet.Mesh);
        Assert.Equal(0U, packet.UnionType);
        Assert.Equal(1U, packet.Count);
        Assert.Equal(0U, packet.New1);
        Assert.Equal(0U, packet.New2);
        Assert.Equal(
        [
            "SYSTEM",
            "ALL",
            string.Empty,
            TopInHoursMinute32Message,
            string.Empty,
            string.Empty,
            string.Empty
        ], packet.Content);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static CharacterSnapshot CreateSnapshot()
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "HeroOne",
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
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
