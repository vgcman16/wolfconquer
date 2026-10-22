using System.Text;

internal static partial class Program
{
    private const int ClientPeSymbolDelayDescriptorSize = 32;

    private static ClientPeSymbolImportSet ReadClientPeSymbolImportsFromDll(
        byte[] bytes,
        ClientPeSymbolPeHeaders pe,
        string requestedDllName,
        int maxImports)
    {
        var normal = ReadClientPeSymbolNormalImportsFromDll(bytes, pe, requestedDllName, maxImports);
        var delayed = ReadClientPeSymbolDelayedImportsFromDll(
            bytes,
            pe,
            requestedDllName,
            Math.Max(0, maxImports - normal.Imports.Count));
        return new ClientPeSymbolImportSet(
            requestedDllName,
            IsPresent: normal.IsPresent || delayed.IsPresent,
            Imports: normal.Imports.Concat(delayed.Imports).ToArray(),
            IsTruncated: normal.IsTruncated || delayed.IsTruncated);
    }

    private static ClientPeSymbolImportSet ReadClientPeSymbolDelayedImportsFromDll(
        byte[] bytes,
        ClientPeSymbolPeHeaders pe,
        string requestedDllName,
        int maxImports)
    {
        if ((pe.DelayImportTableRva | pe.DelayImportTableSize) == 0)
        {
            return ClientPeSymbolImportSet.Empty(requestedDllName);
        }

        if (pe.DelayImportTableRva == 0 || pe.DelayImportTableSize == 0)
        {
            throw new InvalidDataException("delay-import-directory-rva-size-mismatch");
        }

        if (pe.DelayImportTableSize % ClientPeSymbolDelayDescriptorSize != 0)
        {
            throw new InvalidDataException("delay-import-directory-size-not-descriptor-aligned");
        }

        var data = bytes.AsSpan();
        MapClientPeSymbolDelayedRange(data, pe, pe.DelayImportTableRva, pe.DelayImportTableSize, "directory");
        var descriptorCount = Math.Min(
            pe.DelayImportTableSize / ClientPeSymbolDelayDescriptorSize,
            ClientPeSymbolAuditDescriptorLimit);
        var imports = new List<ClientPeSymbolImport>();
        var present = false;
        var truncated = false;
        for (var index = 0; index < descriptorCount; index++)
        {
            var descriptorRva = checked(pe.DelayImportTableRva + (uint)index * ClientPeSymbolDelayDescriptorSize);
            var descriptorOffset = MapClientPeSymbolDelayedRange(
                data, pe, descriptorRva, ClientPeSymbolDelayDescriptorSize, "descriptor");
            var attributes = ReadClientLaunchPeUInt32(data, descriptorOffset);
            var nameAddress = ReadClientLaunchPeUInt32(data, descriptorOffset + 4);
            var moduleHandleAddress = ReadClientLaunchPeUInt32(data, descriptorOffset + 8);
            var addressTableAddress = ReadClientLaunchPeUInt32(data, descriptorOffset + 12);
            var nameTableAddress = ReadClientLaunchPeUInt32(data, descriptorOffset + 16);
            var boundTableAddress = ReadClientLaunchPeUInt32(data, descriptorOffset + 20);
            var unloadTableAddress = ReadClientLaunchPeUInt32(data, descriptorOffset + 24);
            var timeDateStamp = ReadClientLaunchPeUInt32(data, descriptorOffset + 28);
            if ((attributes | nameAddress | moduleHandleAddress | addressTableAddress |
                nameTableAddress | boundTableAddress | unloadTableAddress | timeDateStamp) == 0)
            {
                return new ClientPeSymbolImportSet(requestedDllName, present, imports, truncated);
            }

            // IMAGE_DELAYLOAD_DESCRIPTOR bit 0 selects RVAs; earlier descriptors store VAs.
            // All remaining attribute bits are reserved by the Microsoft PE specification.
            if ((attributes & ~1U) != 0)
            {
                throw new InvalidDataException("delay-import-attributes-unsupported");
            }

            var rvaBased = (attributes & 1) != 0;
            var nameRva = ResolveClientPeSymbolDelayedAddress(pe, nameAddress, rvaBased, "dll-name");
            if (nameRva == 0)
            {
                throw new InvalidDataException("delay-import-dll-name-missing");
            }

            var dllName = ReadClientPeSymbolDelayedAscii(data, pe, nameRva, "dll-name");
            if (dllName.Length == 0)
            {
                throw new InvalidDataException("delay-import-dll-name-empty");
            }

            var nameTableRva = ResolveClientPeSymbolDelayedAddress(pe, nameTableAddress, rvaBased, "name-table");
            var addressTableRva = ResolveClientPeSymbolDelayedAddress(pe, addressTableAddress, rvaBased, "address-table");
            if (nameTableRva == 0)
            {
                throw new InvalidDataException("delay-import-name-table-missing");
            }

            if (addressTableRva == 0)
            {
                throw new InvalidDataException("delay-import-address-table-missing");
            }

            var thunkSize = pe.IsPe32Plus ? sizeof(ulong) : sizeof(uint);
            MapClientPeSymbolDelayedRange(data, pe, nameTableRva, (uint)thunkSize, "name-table");
            MapClientPeSymbolDelayedRange(data, pe, addressTableRva, (uint)thunkSize, "address-table");
            if (!string.Equals(dllName, requestedDllName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            present = true;
            var read = ReadClientPeSymbolDelayedImportThunks(
                data, pe, nameTableRva, addressTableRva, rvaBased, maxImports - imports.Count);
            imports.AddRange(read.Imports);
            truncated |= read.IsTruncated;
        }

        throw new InvalidDataException("delay-import-descriptors-not-null-terminated-within-directory");
    }

    private static ClientPeSymbolImportReadResult ReadClientPeSymbolDelayedImportThunks(
        ReadOnlySpan<byte> data,
        ClientPeSymbolPeHeaders pe,
        uint nameTableRva,
        uint addressTableRva,
        bool rvaBased,
        int maxImports)
    {
        var imports = new List<ClientPeSymbolImport>();
        var thunkSize = pe.IsPe32Plus ? sizeof(ulong) : sizeof(uint);
        var ordinalFlag = pe.IsPe32Plus ? 0x8000000000000000UL : 0x80000000UL;
        for (var index = 0; index <= maxImports; index++)
        {
            var nameThunkRva = checked(nameTableRva + (uint)index * (uint)thunkSize);
            var addressThunkRva = checked(addressTableRva + (uint)index * (uint)thunkSize);
            var nameOffset = MapClientPeSymbolDelayedRange(data, pe, nameThunkRva, (uint)thunkSize, "name-thunk");
            var addressOffset = MapClientPeSymbolDelayedRange(data, pe, addressThunkRva, (uint)thunkSize, "address-thunk");
            var thunk = pe.IsPe32Plus
                ? ReadClientPeSymbolUInt64(data, nameOffset)
                : ReadClientLaunchPeUInt32(data, nameOffset);
            var addressThunk = pe.IsPe32Plus
                ? ReadClientPeSymbolUInt64(data, addressOffset)
                : ReadClientLaunchPeUInt32(data, addressOffset);
            if (thunk == 0)
            {
                if (addressThunk != 0)
                {
                    throw new InvalidDataException("delay-import-iat-int-terminator-mismatch");
                }

                return new ClientPeSymbolImportReadResult(imports, IsTruncated: false);
            }

            if (addressThunk == 0)
            {
                throw new InvalidDataException("delay-import-iat-ended-before-int");
            }

            if (index == maxImports)
            {
                return new ClientPeSymbolImportReadResult(imports, IsTruncated: true);
            }

            if ((thunk & ordinalFlag) != 0)
            {
                if ((thunk & ~(ordinalFlag | 0xFFFFUL)) != 0)
                {
                    throw new InvalidDataException("delay-import-ordinal-reserved-bits-set");
                }

                imports.Add(new ClientPeSymbolImport(string.Empty, null, (ushort)(thunk & 0xFFFF), IsOrdinal: true));
                continue;
            }

            // RVA-based PE32+ name thunks reserve bits 62..31, not just 62..32.
            // Legacy VA descriptors use an address and are resolved separately.
            if (pe.IsPe32Plus && rvaBased && thunk > 0x7FFFFFFFUL)
            {
                throw new InvalidDataException("delay-import-hint-name-reserved-bits-set");
            }

            var hintNameRva = ResolveClientPeSymbolDelayedAddress(pe, thunk, rvaBased, "hint-name");
            var hintOffset = MapClientPeSymbolDelayedRange(data, pe, hintNameRva, sizeof(ushort), "hint");
            var hint = ReadClientLaunchPeUInt16(data, hintOffset);
            var name = ReadClientPeSymbolDelayedAscii(data, pe, checked(hintNameRva + sizeof(ushort)), "symbol-name");
            if (name.Length == 0)
            {
                throw new InvalidDataException("delay-import-symbol-name-empty");
            }

            imports.Add(new ClientPeSymbolImport(name, hint, null, IsOrdinal: false));
        }

        throw new InvalidDataException("delay-import-thunk-read-did-not-terminate");
    }

    private static uint ResolveClientPeSymbolDelayedAddress(
        ClientPeSymbolPeHeaders pe,
        ulong address,
        bool rvaBased,
        string description)
    {
        if (address == 0)
        {
            return 0;
        }

        if (!rvaBased)
        {
            if (address < pe.ImageBase)
            {
                throw new InvalidDataException($"delay-import-{description}-va-before-image-base");
            }

            address -= pe.ImageBase;
        }

        if (address > uint.MaxValue)
        {
            throw new InvalidDataException($"delay-import-{description}-rva-out-of-range");
        }

        return (uint)address;
    }

    private static int MapClientPeSymbolDelayedRange(
        ReadOnlySpan<byte> data,
        ClientPeSymbolPeHeaders pe,
        uint rva,
        uint length,
        string description)
    {
        if (rva < pe.SizeOfHeaders && length <= pe.SizeOfHeaders - rva &&
            (ulong)rva + length <= (ulong)data.Length)
        {
            return checked((int)rva);
        }

        foreach (var section in pe.Sections)
        {
            if (rva < section.VirtualAddress)
            {
                continue;
            }

            var delta = rva - section.VirtualAddress;
            if (delta >= section.SizeOfRawData || length > section.SizeOfRawData - delta)
            {
                continue;
            }

            var offset = (ulong)section.PointerToRawData + delta;
            if (offset <= int.MaxValue && offset + length <= (ulong)data.Length)
            {
                return (int)offset;
            }
        }

        throw new InvalidDataException($"delay-import-{description}-range-unmapped-{FormatScanHex(rva)}");
    }

    private static string ReadClientPeSymbolDelayedAscii(
        ReadOnlySpan<byte> data,
        ClientPeSymbolPeHeaders pe,
        uint rva,
        string description)
    {
        var start = MapClientPeSymbolDelayedRange(data, pe, rva, 1, description);
        for (var length = 0; length < ClientLaunchEvidenceImportNameLimit; length++)
        {
            var offset = MapClientPeSymbolDelayedRange(data, pe, checked(rva + (uint)length), 1, description);
            if (offset != start + length)
            {
                throw new InvalidDataException($"delay-import-{description}-noncontiguous-file-range");
            }

            if (data[offset] == 0)
            {
                return Encoding.ASCII.GetString(data.Slice(start, length));
            }

            if (data[offset] > 0x7F)
            {
                throw new InvalidDataException($"delay-import-{description}-non-ascii");
            }
        }

        throw new InvalidDataException($"delay-import-{description}-not-null-terminated");
    }
}
