using System.Buffers.Binary;
using System.Text;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativePacket
{
    private unsafe int ValidatePhysicalWrite(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (IsDisposed || Memory == null) throw new ObjectDisposedException(nameof(LegacyNativePacket));
        // Position narrows pointer subtraction to int. Validate full addresses
        // first so an invalid cursor cannot wrap into an apparently valid span.
        var origin = (nuint)Memory;
        var cursor = (nuint)stream;
        if (cursor < origin || cursor - origin > (nuint)Capacity)
            throw new InvalidOperationException("Packet write cursor is outside its allocation.");
        var position = (int)(cursor - origin);
        if (count > Capacity - position)
            throw new InvalidOperationException("Packet write exceeds its allocation.");
        return position;
    }

    private unsafe void WritePhysicalScalar(ulong value, int width)
    {
        var position = ValidatePhysicalWrite(width);
        for (var index = 0; index < width; index++)
            Memory[position + index] = (byte)(value >> (8 * index));
        stream = Memory + position + width;
    }

    private unsafe void WritePhysicalBytes(ReadOnlySpan<byte> prepared)
    {
        var position = ValidatePhysicalWrite(prepared.Length);
        prepared.CopyTo(new Span<byte>(Memory + position, prepared.Length));
        stream = Memory + position + prepared.Length;
    }

    private static byte[] CapturePhysicalSeal(bool required)
    {
        var captured = seal;
        if (captured is null)
        {
            if (required) throw new InvalidOperationException("Packet seal is unavailable.");
            return Array.Empty<byte>();
        }
        if (captured.Length != TQ_SEALSIZE)
            throw new InvalidOperationException("Packet seal has an invalid character width.");
        // Native fixed strings copy this many UTF8 bytes based on character
        // count; retain the first eight bytes even if they split a code point.
        return Encoding.UTF8.GetBytes(captured).AsSpan(0, TQ_SEALSIZE).ToArray();
    }

    private unsafe void FinalizePhysicalFrame(ushort type)
    {
        var declaredLength = ValidatePhysicalWrite(0);
        var capturedSeal = CapturePhysicalSeal(required: false);
        if (declaredLength < 4 || declaredLength > ushort.MaxValue)
            throw new InvalidOperationException("Packet frame has an invalid UInt16 declared length.");
        if (capturedSeal.Length > Capacity - declaredLength)
            throw new InvalidOperationException("Packet frame and seal exceed its allocation.");
        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(header, (ushort)declaredLength);
        BinaryPrimitives.WriteUInt16LittleEndian(header[2..], type);
        // All ordinary preparation and complete frame checks precede writes.
        header.CopyTo(new Span<byte>(Memory, 4));
        capturedSeal.AsSpan().CopyTo(new Span<byte>(Memory + declaredLength, capturedSeal.Length));
        Size = declaredLength + capturedSeal.Length;
        stream = Memory + 4;
    }
}
