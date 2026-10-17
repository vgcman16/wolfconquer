using System.Buffers.Binary;
using System.Text;

namespace WolfConquer.Core.Networking;

public static class LegacyGameServerHandshakePacket
{
    public const int FixedLength = 75;
    public const int LengthFieldOffset = 11;
    public const int LengthFieldAdjustment = 11;
    public const int HandshakeType = 10;
    public const string LegacyPrimeAscii = LegacyGameServerCipher.DiffieHellmanPrimeHex;
    public const string LegacyGeneratorAscii = LegacyGameServerCipher.DiffieHellmanGeneratorHex;
    public const string Seal = "TQServer";

    public static byte[] Build(ReadOnlySpan<byte> p, ReadOnlySpan<byte> g, ReadOnlySpan<byte> publicKey)
    {
        ValidateNonEmpty(p, nameof(p));
        ValidateNonEmpty(g, nameof(g));
        ValidateNonEmpty(publicKey, nameof(publicKey));

        var length = checked(FixedLength + p.Length + g.Length + publicKey.Length);
        var buffer = new byte[length];
        var offset = LengthFieldOffset;

        WriteInt32(buffer, ref offset, length - LengthFieldAdjustment);
        WriteInt32(buffer, ref offset, HandshakeType);
        offset += 10;

        WriteInt32(buffer, ref offset, 8);
        offset += 8;
        WriteInt32(buffer, ref offset, 8);
        offset += 8;

        WriteLengthPrefixedBlob(buffer, ref offset, p);
        WriteLengthPrefixedBlob(buffer, ref offset, g);
        WriteLengthPrefixedBlob(buffer, ref offset, publicKey);

        offset += 2;
        Encoding.ASCII.GetBytes(Seal).CopyTo(buffer.AsSpan(offset));
        offset += Seal.Length;

        if (offset != buffer.Length)
        {
            throw new InvalidOperationException("Legacy GameServer handshake packet layout length mismatch.");
        }

        return buffer;
    }

    public static byte[] BuildWithLegacyParameters(string asciiPublicKey)
    {
        ArgumentNullException.ThrowIfNull(asciiPublicKey);

        return Build(
            Encoding.ASCII.GetBytes(LegacyPrimeAscii),
            Encoding.ASCII.GetBytes(LegacyGeneratorAscii),
            Encoding.ASCII.GetBytes(asciiPublicKey));
    }

    private static void WriteLengthPrefixedBlob(Span<byte> buffer, ref int offset, ReadOnlySpan<byte> blob)
    {
        WriteInt32(buffer, ref offset, blob.Length);
        blob.CopyTo(buffer[offset..]);
        offset += blob.Length;
    }

    private static void WriteInt32(Span<byte> buffer, ref int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset, sizeof(int)), value);
        offset += sizeof(int);
    }

    private static void ValidateNonEmpty(ReadOnlySpan<byte> value, string paramName)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException("Legacy GameServer handshake DH values cannot be empty.", paramName);
        }
    }
}
