using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// These tests require the proposed explicit-profile overloads. They are not
// baseline behavioral RED input; the separate Frame partials use current APIs.
public sealed class LegacyMailListProfileCodecTests
{
    private const string Numeric = "080110e807181920c03e284d30023801";
    private const string D11Text = "420a54657374207469746c654a074d725261797a6f";
    private const string D13Text = "4a0a54657374207469746c6552074d725261797a6f";

    [Theory]
    [InlineData(0, "080010011a25")]
    [InlineData(1, "080110011a25")]
    [InlineData(2, "080210001a25")]
    public void ExplicitProfilesUseIndependentLiteralTagsAndPreserveDefaultBytes(int type, string outer)
    {
        var packet = new LegacyMailListPacket((LegacyMailListType)type, type == 2 ? 0 : 1, [Mail()]);
        var expectedD11 = Convert.FromHexString(outer + Numeric + D11Text);
        var expectedD13 = Convert.FromHexString(outer + Numeric + D13Text);
        Assert.Equal(expectedD11, Read(packet.ToFrameBytes()).Payload.ToArray());
        Assert.Equal(expectedD11, Read(packet.ToFrameBytes(ConquerProtocolProfile.RootD11_7899)).Payload.ToArray());
        Assert.Equal(expectedD13, Read(packet.ToFrameBytes(ConquerProtocolProfile.VisibleDx8D13)).Payload.ToArray());

        Assert.True(LegacyMailListPacket.TryParse(Frame(expectedD11), out var defaultParsed));
        Assert.True(LegacyMailListPacket.TryParse(Frame(expectedD11), ConquerProtocolProfile.RootD11_7899, out var d11));
        Assert.True(LegacyMailListPacket.TryParse(Frame(expectedD13), ConquerProtocolProfile.VisibleDx8D13, out var d13));
        foreach (var parsed in new[] { defaultParsed, d11, d13 })
        {
            Assert.Equal(packet.Type, parsed.Type);
            Assert.Equal(packet.Count, parsed.Count);
            Assert.Equal(Mail(), Assert.Single(parsed.Mails));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("4000")]
    [InlineData("40ac02")]
    [InlineData("40ffffffff0f")]
    public void D13ConsumesOptionalOpaqueNumericEightWithoutInventingAValue(string optionalEight)
    {
        var frame = Nested(Numeric + optionalEight + D13Text);
        Assert.True(LegacyMailListPacket.TryParse(frame, ConquerProtocolProfile.VisibleDx8D13, out var packet));
        Assert.Equal(Mail(), Assert.Single(packet.Mails));
        // The output model has no meaning/value for field8: it is omitted.
        Assert.Equal(Convert.FromHexString("080010011a25" + Numeric + D13Text),
            Read(packet.ToFrameBytes(ConquerProtocolProfile.VisibleDx8D13)).Payload.ToArray());
    }

    [Theory]
    [InlineData("4200")]
    [InlineData("4500000000")]
    [InlineData("4080")]
    public void D13RejectsWrongOrTruncatedNumericEightWireShape(string invalidEight)
    {
        // Keep the malformed varint last so a later field cannot terminate it.
        var frame = Nested(Numeric + D13Text + invalidEight);
        Assert.False(LegacyMailListPacket.TryParse(frame, ConquerProtocolProfile.VisibleDx8D13, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProfilesDoNotGuessTheOtherTextLayout(bool d13Payload)
    {
        var frame = Nested(Numeric + (d13Payload ? D13Text : D11Text));
        var other = d13Payload ? ConquerProtocolProfile.RootD11_7899 : ConquerProtocolProfile.VisibleDx8D13;
        Assert.False(LegacyMailListPacket.TryParse(frame, other, out _));
        if (d13Payload) Assert.False(LegacyMailListPacket.TryParse(frame, out _));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void UnknownProfileCannotFallBackToAValidLayout(int value)
    {
        var profile = (ConquerProtocolProfile)value;
        var packet = LegacyMailListPacket.NewMail(Mail());
        Assert.Throws<ArgumentOutOfRangeException>(() => packet.ToFrameBytes(profile));
        Assert.False(LegacyMailListPacket.TryParse(Nested(Numeric + D13Text), profile, out _));
    }

    private static LegacyMailListEntry Mail() => new(1, 1000, 25, 8000, 77, 2, 1, "Test title", "MrRayzo");

    private static PacketFrame Nested(string hex)
    {
        var mail = Convert.FromHexString(hex);
        Assert.InRange(mail.Length, 0, 127);
        // Literal outer fields: FullPage0, count1, repeated mail3/wire2.
        return Frame([0x08, 0x00, 0x10, 0x01, 0x1a, checked((byte)mail.Length), .. mail]);
    }

    private static PacketFrame Frame(byte[] payload)
    {
        var writer = new PacketWriter(2183, payload.Length);
        payload.CopyTo(writer.Payload);
        return Read(writer.ToArray());
    }

    private static PacketFrame Read(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
