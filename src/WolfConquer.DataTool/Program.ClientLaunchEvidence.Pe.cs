using System.Buffers.Binary;
using System.Text;

internal static partial class Program
{
    private static ClientLaunchEvidencePeInfo AnalyzeClientLaunchEvidencePe(byte[] bytes, int maxImportDlls)
    {
        try
        {
            var data = bytes.AsSpan();
            if (data.Length < 64)
            {
                return ClientLaunchEvidencePeInfo.NotPe("too-small-for-dos-header");
            }

            if (data[0] != (byte)'M' || data[1] != (byte)'Z')
            {
                return ClientLaunchEvidencePeInfo.NotPe("missing-mz-signature");
            }

            var peOffset = ReadClientLaunchPeUInt32(data, 0x3C);
            if (peOffset > int.MaxValue)
            {
                return ClientLaunchEvidencePeInfo.NotPe("pe-header-offset-out-of-range");
            }

            var peHeaderOffset = checked((int)peOffset);
            EnsureClientLaunchPeRange(data, peHeaderOffset, 24, "PE header");
            if (ReadClientLaunchPeUInt32(data, peHeaderOffset) != 0x00004550)
            {
                return ClientLaunchEvidencePeInfo.NotPe("missing-pe-signature");
            }

            var coffOffset = checked(peHeaderOffset + 4);
            var machine = ReadClientLaunchPeUInt16(data, coffOffset);
            var sectionCount = ReadClientLaunchPeUInt16(data, coffOffset + 2);
            var optionalHeaderSize = ReadClientLaunchPeUInt16(data, coffOffset + 16);
            var optionalOffset = checked(coffOffset + 20);
            EnsureClientLaunchPeRange(data, optionalOffset, optionalHeaderSize, "optional header");

            var optionalMagic = ReadClientLaunchPeUInt16(data, optionalOffset);
            var dataDirectoryOffset = optionalMagic switch
            {
                0x10B => optionalOffset + 96,
                0x20B => optionalOffset + 112,
                _ => 0,
            };

            var subsystem = optionalHeaderSize >= 70
                ? ReadClientLaunchPeUInt16(data, optionalOffset + 68)
                : (ushort)0;
            var sizeOfHeaders = optionalHeaderSize >= 64
                ? ReadClientLaunchPeUInt32(data, optionalOffset + 60)
                : 0U;

            var sections = ReadClientLaunchPeSections(data, checked(optionalOffset + optionalHeaderSize), sectionCount);
            var importTableRva = 0U;
            var importTableSize = 0U;
            if (dataDirectoryOffset > 0 && dataDirectoryOffset + 16 <= optionalOffset + optionalHeaderSize)
            {
                var dataDirectoryCountOffset = optionalMagic == 0x10B ? optionalOffset + 92 : optionalOffset + 108;
                var dataDirectoryCount = ReadClientLaunchPeUInt32(data, dataDirectoryCountOffset);
                if (dataDirectoryCount > 1)
                {
                    importTableRva = ReadClientLaunchPeUInt32(data, dataDirectoryOffset + 8);
                    importTableSize = ReadClientLaunchPeUInt32(data, dataDirectoryOffset + 12);
                }
            }

            var importError = string.Empty;
            var imports = Array.Empty<string>();
            if (importTableRva != 0 && maxImportDlls > 0)
            {
                try
                {
                    imports = ReadClientLaunchPeImportDllNames(
                        data,
                        sections,
                        sizeOfHeaders,
                        importTableRva,
                        importTableSize,
                        maxImportDlls);
                }
                catch (InvalidDataException ex)
                {
                    importError = ex.Message;
                }
            }

            return new ClientLaunchEvidencePeInfo(
                IsPe: true,
                Reason: string.Empty,
                Machine: FormatClientLaunchPeMachine(machine),
                Subsystem: FormatClientLaunchPeSubsystem(subsystem),
                ImportDllNames: imports,
                ImportDllNamesTruncated: importTableRva != 0 && (maxImportDlls == 0 || imports.Length >= maxImportDlls),
                ImportReadError: importError);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException or OverflowException)
        {
            return ClientLaunchEvidencePeInfo.NotPe(ex.Message);
        }
    }

    private static ClientLaunchPeSection[] ReadClientLaunchPeSections(
        ReadOnlySpan<byte> data,
        int sectionOffset,
        ushort sectionCount)
    {
        if (sectionCount == 0)
        {
            return [];
        }

        var sections = new ClientLaunchPeSection[sectionCount];
        for (var index = 0; index < sectionCount; index++)
        {
            var offset = checked(sectionOffset + index * 40);
            EnsureClientLaunchPeRange(data, offset, 40, "section header");
            sections[index] = new ClientLaunchPeSection(
                ReadClientLaunchPeUInt32(data, offset + 8),
                ReadClientLaunchPeUInt32(data, offset + 12),
                ReadClientLaunchPeUInt32(data, offset + 16),
                ReadClientLaunchPeUInt32(data, offset + 20));
        }

        return sections;
    }

    private static string[] ReadClientLaunchPeImportDllNames(
        ReadOnlySpan<byte> data,
        IReadOnlyList<ClientLaunchPeSection> sections,
        uint sizeOfHeaders,
        uint importTableRva,
        uint importTableSize,
        int maxImportDlls)
    {
        if (!TryMapClientLaunchPeRvaToOffset(data, sections, sizeOfHeaders, importTableRva, out var importOffset))
        {
            throw new InvalidDataException($"import-table-rva-unmapped-{FormatScanHex(importTableRva)}");
        }

        var maxDescriptors = importTableSize > 0
            ? Math.Min(ClientLaunchEvidenceImportDescriptorLimit, checked((int)(importTableSize / 20)))
            : ClientLaunchEvidenceImportDescriptorLimit;
        if (maxDescriptors == 0)
        {
            maxDescriptors = ClientLaunchEvidenceImportDescriptorLimit;
        }

        var names = new List<string>();
        for (var index = 0; index < maxDescriptors; index++)
        {
            var descriptorOffset = checked(importOffset + index * 20);
            EnsureClientLaunchPeRange(data, descriptorOffset, 20, "import descriptor");
            var originalFirstThunk = ReadClientLaunchPeUInt32(data, descriptorOffset);
            var timeDateStamp = ReadClientLaunchPeUInt32(data, descriptorOffset + 4);
            var forwarderChain = ReadClientLaunchPeUInt32(data, descriptorOffset + 8);
            var nameRva = ReadClientLaunchPeUInt32(data, descriptorOffset + 12);
            var firstThunk = ReadClientLaunchPeUInt32(data, descriptorOffset + 16);
            if ((originalFirstThunk | timeDateStamp | forwarderChain | nameRva | firstThunk) == 0)
            {
                break;
            }

            if (nameRva == 0)
            {
                continue;
            }

            if (!TryMapClientLaunchPeRvaToOffset(data, sections, sizeOfHeaders, nameRva, out var nameOffset))
            {
                throw new InvalidDataException($"import-name-rva-unmapped-{FormatScanHex(nameRva)}");
            }

            var name = ReadClientLaunchPeAsciiNullTerminated(data, nameOffset);
            if (name.Length > 0 &&
                !names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(name);
            }

            if (names.Count >= maxImportDlls)
            {
                break;
            }
        }

        return names.ToArray();
    }

    private static bool TryMapClientLaunchPeRvaToOffset(
        ReadOnlySpan<byte> data,
        IReadOnlyList<ClientLaunchPeSection> sections,
        uint sizeOfHeaders,
        uint rva,
        out int offset)
    {
        if (rva < sizeOfHeaders && rva < data.Length)
        {
            offset = checked((int)rva);
            return true;
        }

        foreach (var section in sections)
        {
            var sectionSize = Math.Max(section.VirtualSize, section.SizeOfRawData);
            if (sectionSize == 0 || rva < section.VirtualAddress)
            {
                continue;
            }

            var sectionDelta = rva - section.VirtualAddress;
            if (sectionDelta >= sectionSize)
            {
                continue;
            }

            var mappedOffset = (ulong)section.PointerToRawData + sectionDelta;
            if (mappedOffset <= int.MaxValue && mappedOffset < (ulong)data.Length)
            {
                offset = (int)mappedOffset;
                return true;
            }
        }

        offset = 0;
        return false;
    }

    private static string ReadClientLaunchPeAsciiNullTerminated(ReadOnlySpan<byte> data, int offset)
    {
        EnsureClientLaunchPeRange(data, offset, 1, "import name");
        var maxLength = Math.Min(ClientLaunchEvidenceImportNameLimit, data.Length - offset);
        var length = 0;
        while (length < maxLength && data[offset + length] != 0)
        {
            length++;
        }

        if (length == maxLength)
        {
            throw new InvalidDataException($"import-name-not-null-terminated-{FormatScanHex(offset)}");
        }

        return Encoding.ASCII.GetString(data.Slice(offset, length));
    }

    private static ushort ReadClientLaunchPeUInt16(ReadOnlySpan<byte> data, int offset)
    {
        EnsureClientLaunchPeRange(data, offset, sizeof(ushort), "uint16");
        return BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, sizeof(ushort)));
    }

    private static uint ReadClientLaunchPeUInt32(ReadOnlySpan<byte> data, int offset)
    {
        EnsureClientLaunchPeRange(data, offset, sizeof(uint), "uint32");
        return BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, sizeof(uint)));
    }

    private static void EnsureClientLaunchPeRange(ReadOnlySpan<byte> data, long offset, long length, string description)
    {
        if (offset < 0 || length < 0 || offset > data.Length || length > data.Length - offset)
        {
            throw new InvalidDataException($"PE {description} points outside the file.");
        }
    }

    private static string FormatClientLaunchPeMachine(ushort machine)
    {
        return machine switch
        {
            0x014C => "I386",
            0x8664 => "Amd64",
            0x01C0 => "Arm",
            0x01C4 => "ArmNt",
            0xAA64 => "Arm64",
            0 => "<unknown>",
            _ => $"0x{machine:X4}",
        };
    }

    private static string FormatClientLaunchPeSubsystem(ushort subsystem)
    {
        return subsystem switch
        {
            1 => "Native",
            2 => "WindowsGui",
            3 => "WindowsCui",
            5 => "Os2Cui",
            7 => "PosixCui",
            9 => "WindowsCeGui",
            10 => "EfiApplication",
            11 => "EfiBootServiceDriver",
            12 => "EfiRuntimeDriver",
            13 => "EfiRom",
            14 => "Xbox",
            16 => "WindowsBootApplication",
            0 => "<unknown>",
            _ => $"0x{subsystem:X4}",
        };
    }
}

internal sealed record ClientLaunchPeSection(
    uint VirtualSize,
    uint VirtualAddress,
    uint SizeOfRawData,
    uint PointerToRawData);
