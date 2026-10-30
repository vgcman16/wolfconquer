using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativePacket
{
    private unsafe int ValidatePhysicalRead(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (IsDisposed || Memory == null) throw new ObjectDisposedException(nameof(LegacyNativePacket));
        nuint origin = (nuint)Memory;
        nuint cursor = (nuint)stream;
        if (cursor < origin || cursor - origin > (nuint)Capacity)
            throw new InvalidOperationException("Packet read cursor is outside its allocation.");
        if (Size < 0 || Size > Capacity)
            throw new InvalidOperationException("Packet input Size is outside its allocation.");
        int position = (int)(cursor - origin);
        // Size is supplied by the capturing caller, not inferred from spare
        // allocation, an untrusted frame header or a desired read length.
        if (position > Size || count > Size - position)
            throw new EndOfStreamException("Packet read exceeds its supplied input window.");
        return position;
    }

    private unsafe void CompletePhysicalRead(byte* allocation, byte* cursor, int size, int end)
    {
        ValidatePhysicalRead(0);
        if (Memory != allocation || stream != cursor || Size != size)
            throw new InvalidOperationException("Packet input changed while preparing a read.");
        stream = allocation + end;
    }

    private unsafe ulong ReadPhysicalScalar(int width)
    {
        int position = ValidatePhysicalRead(width);
        byte* allocation = Memory;
        byte* cursor = stream;
        int size = Size;
        ReadOnlySpan<byte> input = new(allocation + position, width);
        ulong value = width switch
        {
            1 => input[0],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(input),
            4 => BinaryPrimitives.ReadUInt32LittleEndian(input),
            8 => BinaryPrimitives.ReadUInt64LittleEndian(input),
            _ => throw new ArgumentOutOfRangeException(nameof(width)),
        };
        CompletePhysicalRead(allocation, cursor, size, position + width);
        return value;
    }

    private unsafe byte[] ReadPhysicalBytes(int count)
    {
        int position = ValidatePhysicalRead(count);
        byte* allocation = Memory;
        byte* cursor = stream;
        int size = Size;
        byte[] result = new ReadOnlySpan<byte>(allocation + position, count).ToArray();
        CompletePhysicalRead(allocation, cursor, size, position + count);
        return result;
    }

    private unsafe string ReadPhysicalCString(int count)
    {
        int position = ValidatePhysicalRead(count);
        byte* allocation = Memory;
        byte* cursor = stream;
        int size = Size;
        byte[] input = new ReadOnlySpan<byte>(allocation + position, count).ToArray();
        string result = DecodePhysicalCString(input);
        CompletePhysicalRead(allocation, cursor, size, position + count);
        return result;
    }

    private static string DecodePhysicalCString(ReadOnlySpan<byte> input)
    {
        string value = Encoding.UTF8.GetString(input);
        int nul = value.IndexOf('\0');
        return nul < 0 ? value : value[..nul];
    }

    private unsafe string[] ReadPhysicalStringList()
    {
        int position = ValidatePhysicalRead(1);
        byte* allocation = Memory;
        byte* cursor = stream;
        int size = Size;
        ReadOnlySpan<byte> available = new(allocation + position, size - position);
        int count = available[0];
        var fields = new (int Offset, int Length)[count];
        int consumed = 1;
        for (int index = 0; index < count; index++)
        {
            if (available.Length - consumed < 2)
                throw new EndOfStreamException("Packet string list is missing a complete length.");
            int length = BinaryPrimitives.ReadUInt16LittleEndian(available[consumed..]);
            consumed += 2;
            if (length > available.Length - consumed)
                throw new EndOfStreamException("Packet string list entry exceeds its input window.");
            fields[index] = (consumed, length);
            consumed += length;
        }
        // Complete metadata is valid before copying/decoding any result. A later
        // malformed entry cannot consume an earlier one or publish a partial list.
        byte[] snapshot = available[..consumed].ToArray();
        var result = new string[count];
        for (int index = 0; index < count; index++)
            result[index] = DecodePhysicalCString(snapshot.AsSpan(fields[index].Offset, fields[index].Length));
        CompletePhysicalRead(allocation, cursor, size, position + consumed);
        return result;
    }

    private unsafe void ReadPhysicalInto(void* destination, int count)
    {
        int position = ValidatePhysicalRead(count);
        if (destination == null && count > 0) throw new ArgumentNullException(nameof(destination));
        byte* allocation = Memory;
        byte* cursor = stream;
        int size = Size;
        byte[] snapshot = new ReadOnlySpan<byte>(allocation + position, count).ToArray();
        // The foreign destination's extent/lifetime is the caller's obligation.
        // Snapshotting supports valid overlap; it does not validate arbitrary pointers.
        if (count > 0) Marshal.Copy(snapshot, 0, (nint)destination, count);
        CompletePhysicalRead(allocation, cursor, size, position + count);
    }
}
