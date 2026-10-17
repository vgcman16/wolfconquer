using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Packets;

namespace WolfConquer.Tests;

public sealed class LegacyAccountLoginPacketTests
{
    [Fact]
    public void PasswordSeedPacketUsesLegacyEncryptCodeLayout()
    {
        var packet = new LegacyAccountPasswordSeedPacket(0x12345678);
        var bytes = packet.ToFrameBytes();

        Assert.Equal(Convert.FromHexString("0800230478563412"), bytes);
        Assert.True(LegacyAccountPasswordSeedPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void PasswordSeedPacketRejectsWrongLength()
    {
        var writer = new PacketWriter(AccountPacketOpcodes.LegacyPasswordCryptographySeed, payloadLength: 3);

        Assert.False(LegacyAccountPasswordSeedPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public void AuthenticationPacketUsesLegacyCountedFieldLayout()
    {
        var packet = new LegacyAccountAuthenticationPacket("user02", "fixture-pw", "TestSrv");
        var bytes = packet.ToFrameBytes();
        var expected = new byte[472];
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0, 2), 472);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2, 2), 1942);
        expected[4] = 6;
        expected[5] = 10;
        expected[6] = 7;
        Encoding.Latin1.GetBytes("user02").CopyTo(expected, 10);
        // Independently calculated password XOR bytes for the authored fixture-pw input.
        byte[] encryptedPassword = [0x3e, 0x57, 0x98, 0xfd, 0xdc, 0xf0, 0x93, 0xd0, 0x95, 0xb8];
        encryptedPassword.CopyTo(expected, 16);
        Encoding.Latin1.GetBytes("TestSrv").CopyTo(expected, 26);

        Assert.Equal(expected, bytes);
        Assert.True(LegacyAccountAuthenticationPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(packet.Username, parsed.Username);
        Assert.Equal(packet.Password, parsed.Password);
        Assert.Equal(packet.Server, parsed.Server);
        Assert.Equal([packet.Password], parsed.PasswordCandidates);
    }

    [Fact]
    public void AuthenticationPacketStripsEncryptedPasswordNullsBeforeDecrypting()
    {
        var writer = new PacketWriter(
            AccountPacketOpcodes.LegacyAuthentication,
            LegacyAccountAuthenticationPacket.PayloadLength);
        var username = "user03"u8.ToArray();
        var encryptedPassword = Convert.FromHexString("0019");
        var server = "TestSrv"u8.ToArray();

        writer.Payload[0] = (byte)username.Length;
        writer.Payload[1] = (byte)encryptedPassword.Length;
        writer.Payload[2] = (byte)server.Length;
        username.CopyTo(writer.Payload[6..]);
        encryptedPassword.CopyTo(writer.Payload[(6 + username.Length)..]);
        server.CopyTo(writer.Payload[(6 + username.Length + encryptedPassword.Length)..]);

        Assert.True(LegacyAccountAuthenticationPacket.TryParse(ReadFrame(writer.ToArray()), out var parsed));
        Assert.Equal("user03", parsed.Username);
        Assert.Equal("A", parsed.Password);
        Assert.Contains("A", parsed.PasswordCandidates);
        Assert.Equal("TestSrv", parsed.Server);
    }

    [Fact]
    public void AuthenticationPacketKeepsRawPasswordFallbackWhenLegacyNullStripCorruptsCiphertext()
    {
        // Byte 10 produces a NUL in this synthetic 12-byte encrypted password.
        var bytes = new LegacyAccountAuthenticationPacket("user03", "fixture01-1w", "TestSrv").ToFrameBytes();

        Assert.Equal((byte)0, bytes[26]);
        Assert.True(LegacyAccountAuthenticationPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.NotEqual("fixture01-1w", parsed.Password);
        Assert.Contains("fixture01-1w", parsed.PasswordCandidates);
    }

    [Fact]
    public void AuthenticationPacketRejectsWrongLength()
    {
        var writer = new PacketWriter(AccountPacketOpcodes.LegacyAuthentication, payloadLength: LegacyAccountAuthenticationPacket.PayloadLength - 1);

        Assert.False(LegacyAccountAuthenticationPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public void AuthenticationPacketRejectsFieldCountsPastFixedFrame()
    {
        var bytes = new LegacyAccountAuthenticationPacket("user02", "ABCDEF", "TestSrv").ToFrameBytes();
        bytes[4] = byte.MaxValue;
        bytes[5] = byte.MaxValue;
        bytes[6] = byte.MaxValue;

        Assert.False(LegacyAccountAuthenticationPacket.TryParse(ReadFrame(bytes), out _));
    }

    [Fact]
    public void AuthenticationPacketRejectsPasswordCountsPastLegacyKeyLength()
    {
        var bytes = new LegacyAccountAuthenticationPacket("user02", "ABCDEF", "TestSrv").ToFrameBytes();
        bytes[5] = 33;

        Assert.False(LegacyAccountAuthenticationPacket.TryParse(ReadFrame(bytes), out _));
    }

    [Fact]
    public void AuthenticationPacketRejectsPasswordTooLongWhenWriting()
    {
        var packet = new LegacyAccountAuthenticationPacket("user02", new string('a', 33), "TestSrv");

        Assert.Throws<ArgumentOutOfRangeException>(() => packet.ToFrameBytes());
    }

    [Fact]
    public void ForwardPacketUsesLegacySrpConnectLayout()
    {
        var packet = new LegacyAccountForwardPacket(
            Identifier: 0x0013BEED,
            Type: LegacyAccountForwardType.Ready,
            Port: 5818,
            IP: "127.0.0.1");
        var bytes = packet.ToFrameBytes();

        Assert.Equal(LegacyAccountForwardPacket.FrameLength, bytes.Length);
        Assert.Equal(
            Convert.FromHexString("34006506EDBE13000000000002000000BA160000000000003132372E302E302E31"),
            bytes[..33]);
        Assert.All(bytes.Skip(33), value => Assert.Equal(0, value));
        Assert.True(LegacyAccountForwardPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void ForwardPacketRejectsUnknownType()
    {
        var bytes = new LegacyAccountForwardPacket(
            Identifier: 0x0013BEED,
            Type: LegacyAccountForwardType.Ready,
            Port: 5818,
            IP: "127.0.0.1").ToFrameBytes();
        bytes[12] = 0x99;

        Assert.False(LegacyAccountForwardPacket.TryParse(ReadFrame(bytes), out _));
    }

    [Fact]
    public void ForwardPacketRejectsIpTooLongWhenWriting()
    {
        var packet = new LegacyAccountForwardPacket(
            Identifier: 0x0013BEED,
            Type: LegacyAccountForwardType.Ready,
            Port: 5818,
            IP: "12345678901234567");

        Assert.Throws<ArgumentOutOfRangeException>(() => packet.ToFrameBytes());
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
