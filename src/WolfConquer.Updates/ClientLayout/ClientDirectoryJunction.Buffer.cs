using System.Buffers.Binary;
using System.Text;

namespace WolfConquer.Updates.ClientLayout;

internal static partial class ClientDirectoryJunction
{
    // Microsoft REPARSE_DATA_BUFFER MountPointReparseBuffer: offsets/lengths are
    // byte counts relative to PathBuffer at byte 16, not WCHAR indices.
    // https://learn.microsoft.com/windows-hardware/drivers/ddi/ntifs/ns-ntifs-_reparse_data_buffer
    private static readonly UnicodeEncoding StrictUnicode = new(false, false, true);

    private static byte[] EncodeMountPoint(string target)
    {
        var substitute = StrictUnicode.GetBytes(@"\??\" + target);
        var print = StrictUnicode.GetBytes(target);
        var buffer = new byte[checked(16 + substitute.Length + 2 + print.Length + 2)];
        if (buffer.Length > MaximumReparseBytes) throw new InvalidDataException("Junction data exceeds the 16 KiB bound.");
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, MountPointTag);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(4), checked((ushort)(buffer.Length - 8)));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(8), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(10), checked((ushort)substitute.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(12), checked((ushort)(substitute.Length + 2)));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(14), checked((ushort)print.Length));
        substitute.CopyTo(buffer, 16);
        print.CopyTo(buffer, 16 + substitute.Length + 2);
        return buffer;
    }

    private static void ValidateMountPoint(byte[] buffer, uint returned, string target)
    {
        if (returned is < 20 or > MaximumReparseBytes || returned > buffer.Length ||
            BinaryPrimitives.ReadUInt32LittleEndian(buffer) != MountPointTag ||
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(4)) + 8 != returned ||
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(6)) != 0)
            throw new InvalidDataException("Expected a bounded exact Microsoft mount-point reparse buffer.");
        var paths = buffer.AsSpan(16, checked((int)returned - 16));
        var substituteOffset = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(8));
        var substituteLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(10));
        var printOffset = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(12));
        var printLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(14));
        var substitute = ReadMountPointName(paths, substituteOffset, substituteLength);
        var print = ReadMountPointName(paths, printOffset, printLength);
        if (substituteLength == 0 || !substitute.Equals(@"\??\" + target, StringComparison.Ordinal) ||
            (print.Length != 0 && !print.Equals(target, StringComparison.Ordinal)))
            throw new InvalidDataException("Existing junction does not name the exact ordinary target.");
        if (printLength != 0 && substituteOffset < printOffset + printLength + 2 && printOffset < substituteOffset + substituteLength + 2)
            throw new InvalidDataException("Mount-point name ranges overlap.");
        if (Math.Max(substituteOffset + substituteLength + 2, printOffset + printLength + 2) != paths.Length)
            throw new InvalidDataException("Mount-point data contains unaccounted trailing bytes.");
    }

    private static string ReadMountPointName(ReadOnlySpan<byte> paths, int offset, int length)
    {
        if ((offset & 1) != 0 || (length & 1) != 0 || offset > paths.Length - 2 ||
            length > paths.Length - offset - 2 || paths[offset + length] != 0 || paths[offset + length + 1] != 0)
            throw new InvalidDataException("Invalid bounded UTF-16 mount-point name range.");
        try
        {
            var name = StrictUnicode.GetString(paths.Slice(offset, length));
            if (name.Contains('\0')) throw new InvalidDataException("Mount-point names contain an embedded terminator.");
            return name;
        }
        catch (DecoderFallbackException error) { throw new InvalidDataException("Mount-point name is invalid UTF-16.", error); }
    }
}
