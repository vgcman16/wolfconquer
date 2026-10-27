using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Source-shaped framing for privately prepared native component notifications.
// This is not a new dispatcher, transport binding or client-schema assertion.
internal static class LegacyNativePreparedPacketFrames
{
    internal static string? CaptureSeal()
    {
        string? seal = LegacyNativePacket.SealString;
        ValidateSeal(seal);
        return seal;
    }

    internal static byte[] Frame(ushort opcode, byte[] payload, string? seal)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidateSeal(seal);
        int sealSize = seal is null ? 0 : 8;
        long total = 4L + payload.Length + sealSize;
        if (total > 1032) throw new InvalidDataException("Prepared packet exceeds the native Send bound.");
        var frame = new byte[(int)total];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)(payload.Length + 4));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), opcode);
        payload.CopyTo(frame, 4);
        // Native Write(string,8) copies the first eight UTF8 bytes of an
        // eight-character seal. Capture once; do not reread global SealSize.
        if (seal is not null) Encoding.UTF8.GetBytes(seal).AsSpan(0, 8).CopyTo(frame.AsSpan(4 + payload.Length));
        return frame;
    }

    private static void ValidateSeal(string? seal)
    {
        if (seal is not null && seal.Length != 8)
            throw new InvalidDataException("Prepared packet requires an eight-character native seal.");
    }
}
