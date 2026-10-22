internal static partial class Program
{
    private static ClientPeSymbolPeHeaders ReadClientPeDependencyHeaders(byte[] bytes)
    {
        var data = bytes.AsSpan();
        EnsureClientLaunchPeRange(data, 0, 64, "DOS header");
        if (data[0] != 'M' || data[1] != 'Z') throw new InvalidDataException("Input is not a DOS/PE image.");
        uint peRva = ReadClientLaunchPeUInt32(data, 0x3C);
        if (peRva < 64 || peRva > int.MaxValue) throw new InvalidDataException("Invalid PE header offset.");
        int coff = checked((int)peRva + 4);
        EnsureClientLaunchPeRange(data, (int)peRva, 24, "PE header");
        if (ReadClientLaunchPeUInt32(data, (int)peRva) != 0x4550) throw new InvalidDataException("Missing PE signature.");
        ushort sections = ReadClientLaunchPeUInt16(data, coff + 2);
        ushort optionalSize = ReadClientLaunchPeUInt16(data, coff + 16);
        int optional = checked(coff + 20);
        EnsureClientLaunchPeRange(data, optional, optionalSize, "optional header");
        if (sections is < 1 or > 96 || optionalSize < 2) throw new InvalidDataException("Unsupported PE section count or optional header.");
        int minimum = ReadClientLaunchPeUInt16(data, optional) switch
        {
            0x10B => 96,
            0x20B => 112,
            _ => throw new InvalidDataException("Only PE32 and PE32+ are supported.")
        };
        if (optionalSize < minimum) throw new InvalidDataException("Truncated PE optional header.");
        uint directoryCount = ReadClientLaunchPeUInt32(data, optional + minimum - 4);
        if (directoryCount > 16 || (ulong)minimum + (ulong)directoryCount * 8 > optionalSize)
            throw new InvalidDataException("PE data directories exceed the optional header.");
        uint headerSize = ReadClientLaunchPeUInt32(data, optional + 60);
        uint imageSize = ReadClientLaunchPeUInt32(data, optional + 56);
        int table = checked(optional + optionalSize);
        EnsureClientLaunchPeRange(data, table, sections * 40, "section table");
        if (headerSize < (ulong)table + (uint)sections * 40 || headerSize > data.Length || imageSize < headerSize)
            throw new InvalidDataException("Invalid PE header/image size.");
        var pe = ReadClientPeSymbolHeaders(bytes);
        if (!pe.IsPe) throw new InvalidDataException("PE headers rejected: " + pe.Reason);
        for (int index = 0; index < pe.Sections.Count; index++)
        {
            var section = pe.Sections[index];
            ulong logicalSize = section.VirtualSize == 0 ? section.SizeOfRawData : section.VirtualSize;
            if (section.SizeOfRawData != 0 && (section.PointerToRawData < headerSize ||
                (ulong)section.PointerToRawData + section.SizeOfRawData > (ulong)data.Length))
                throw new InvalidDataException("PE section raw range is invalid.");
            if (logicalSize != 0 && (section.VirtualAddress < headerSize || (ulong)section.VirtualAddress + logicalSize > imageSize))
                throw new InvalidDataException("PE section virtual range is invalid.");
            for (int priorIndex = 0; priorIndex < index; priorIndex++)
            {
                var prior = pe.Sections[priorIndex];
                ulong priorLogical = prior.VirtualSize == 0 ? prior.SizeOfRawData : prior.VirtualSize;
                if (ClientPeDependencyRangesOverlap(section.PointerToRawData, section.SizeOfRawData, prior.PointerToRawData, prior.SizeOfRawData) ||
                    ClientPeDependencyRangesOverlap(section.VirtualAddress, logicalSize, prior.VirtualAddress, priorLogical))
                    throw new InvalidDataException("Overlapping PE section ranges are ambiguous.");
            }
        }
        return pe;
    }

    private static bool ClientPeDependencyRangesOverlap(ulong first, ulong firstLength, ulong second, ulong secondLength) =>
        firstLength != 0 && secondLength != 0 && first < second + secondLength && second < first + firstLength;

    private static int MapClientPeDependencyRange(ReadOnlySpan<byte> data, ClientPeSymbolPeHeaders pe, uint rva, uint length)
    {
        if (length == 0) throw new InvalidDataException("An empty PE range cannot identify import data.");
        if (rva < pe.SizeOfHeaders && length <= pe.SizeOfHeaders - rva && (ulong)rva + length <= (ulong)data.Length)
            return checked((int)rva);
        foreach (var section in pe.Sections)
        {
            uint logicalSize = section.VirtualSize == 0 ? section.SizeOfRawData : section.VirtualSize;
            if (rva < section.VirtualAddress) continue;
            uint delta = rva - section.VirtualAddress;
            if (delta >= section.SizeOfRawData || length > section.SizeOfRawData - delta ||
                delta >= logicalSize || length > logicalSize - delta) continue;
            ulong offset = (ulong)section.PointerToRawData + delta;
            if (offset <= int.MaxValue && offset + length <= (ulong)data.Length) return (int)offset;
        }
        throw new InvalidDataException("Import range is not backed by one logical PE header/section range (padding/overlay refused).");
    }
}
