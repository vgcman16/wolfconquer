using System.Buffers.Binary;
using System.Text;

namespace WolfConquer.Core.Networking;

public static class LegacyGameServerHandshakeReply
{
    public const int LengthFieldOffset = 11;
    public const int KeyLengthFieldAdjustment = 15;
    public const string Seal = "TQClient";

    private static readonly byte[] SealBytes = Encoding.ASCII.GetBytes(Seal);

    public static bool TryReadPublicKey(ReadOnlySpan<byte> payload, out string publicKey)
    {
        publicKey = string.Empty;

        if (payload.Length < LengthFieldOffset + sizeof(int) || !ContainsSeal(payload))
        {
            return false;
        }

        var segmentLength = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(LengthFieldOffset, sizeof(int)));
        var keyLengthOffset = (long)segmentLength + KeyLengthFieldAdjustment;
        if (keyLengthOffset <= 0 || keyLengthOffset >= payload.Length || keyLengthOffset > int.MaxValue)
        {
            return false;
        }

        var keyLengthOffsetInt = (int)keyLengthOffset;
        var remainingFromKeyLength = payload.Length - keyLengthOffsetInt;
        if (remainingFromKeyLength < sizeof(int))
        {
            return false;
        }

        var keyLength = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(keyLengthOffsetInt, sizeof(int)));
        if (keyLength <= 0 || keyLength >= remainingFromKeyLength)
        {
            return false;
        }

        var keyOffset = keyLengthOffsetInt + sizeof(int);
        if (keyLength > payload.Length - keyOffset)
        {
            return false;
        }

        var keyBytes = payload.Slice(keyOffset, keyLength);
        var nullIndex = keyBytes.IndexOf((byte)0);
        if (nullIndex >= 0)
        {
            keyBytes = keyBytes[..nullIndex];
        }

        if (keyBytes.IsEmpty)
        {
            return false;
        }

        publicKey = Encoding.UTF8.GetString(keyBytes);
        return true;
    }

    private static bool ContainsSeal(ReadOnlySpan<byte> payload)
    {
        return payload.IndexOf(SealBytes) >= 0;
    }
}
