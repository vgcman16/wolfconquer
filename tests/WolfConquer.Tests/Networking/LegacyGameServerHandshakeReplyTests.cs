using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed class LegacyGameServerHandshakeReplyTests
{
    private const int SegmentLengthOffset = 11;
    private const int SegmentLength = 17;
    private const int KeyLengthOffset = SegmentLengthOffset + sizeof(int) + SegmentLength;
    private const int KeyBytesOffset = KeyLengthOffset + sizeof(int);
    private static readonly byte[] TqClientSeal = Encoding.ASCII.GetBytes("TQClient");

    [Fact]
    public void ExtractsAsciiPublicKeyFromCompleteReplyWithTrailingTqClientSeal()
    {
        var publicKeyBytes = Encoding.ASCII.GetBytes("CLIENTPUBLICKEY");
        var reply = CreateReply(publicKeyBytes);

        var parsed = LegacyGameServerHandshakeReply.TryReadPublicKey(reply, out var publicKey);

        Assert.True(parsed);
        Assert.Equal("CLIENTPUBLICKEY", publicKey);
    }

    [Fact]
    public void ReturnsFalseForIncompleteBufferBeforeKeyBytesOrTrailingSealArePresent()
    {
        var publicKeyBytes = Encoding.ASCII.GetBytes("CLIENTPUBLICKEY");
        var completeReply = CreateReply(publicKeyBytes);
        var missingLastKeyByte = completeReply.AsSpan(0, KeyBytesOffset + publicKeyBytes.Length - 1);
        var missingTrailingSeal = completeReply.AsSpan(0, KeyBytesOffset + publicKeyBytes.Length);

        Assert.False(LegacyGameServerHandshakeReply.TryReadPublicKey(missingLastKeyByte, out _));
        Assert.False(LegacyGameServerHandshakeReply.TryReadPublicKey(missingTrailingSeal, out _));
    }

    [Fact]
    public void ReturnsFalseForMissingOrZeroKeyLength()
    {
        var missingKeyLength = CreateReplyMissingKeyLength();
        var zeroKeyLength = CreateReply(Encoding.ASCII.GetBytes("IGNORED"), keyLength: 0);

        Assert.False(LegacyGameServerHandshakeReply.TryReadPublicKey(missingKeyLength, out _));
        Assert.False(LegacyGameServerHandshakeReply.TryReadPublicKey(zeroKeyLength, out _));
    }

    [Fact]
    public void PreservesStrictLegacyBoundaryWhereKeyLengthMustBeLessThanRemainingBytes()
    {
        var publicKeyBytes = Encoding.ASCII.GetBytes("CLIENTPUBLICKEY");
        var reply = CreateReply(publicKeyBytes);
        var remainingFromKeyLengthOffset = reply.Length - KeyLengthOffset;
        BinaryPrimitives.WriteInt32LittleEndian(reply.AsSpan(KeyLengthOffset, sizeof(int)), remainingFromKeyLengthOffset);

        Assert.False(LegacyGameServerHandshakeReply.TryReadPublicKey(reply, out _));
    }

    private static byte[] CreateReply(byte[] publicKey, int? keyLength = null)
    {
        var reply = new byte[KeyBytesOffset + publicKey.Length + TqClientSeal.Length];
        BinaryPrimitives.WriteInt32LittleEndian(reply.AsSpan(SegmentLengthOffset, sizeof(int)), SegmentLength);
        BinaryPrimitives.WriteInt32LittleEndian(reply.AsSpan(KeyLengthOffset, sizeof(int)), keyLength ?? publicKey.Length);
        publicKey.CopyTo(reply.AsSpan(KeyBytesOffset));
        TqClientSeal.CopyTo(reply.AsSpan(KeyBytesOffset + publicKey.Length));
        return reply;
    }

    private static byte[] CreateReplyMissingKeyLength()
    {
        var reply = new byte[KeyLengthOffset];
        BinaryPrimitives.WriteInt32LittleEndian(reply.AsSpan(SegmentLengthOffset, sizeof(int)), SegmentLength);
        return reply;
    }
}
