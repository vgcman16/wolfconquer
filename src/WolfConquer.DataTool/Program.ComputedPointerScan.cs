using System.Buffers.Binary;
using System.Globalization;
using System.Text;

internal static partial class Program
{
    static int ComputedPointerScan(string[] args)
    {
        ComputedPointerScanOptions options;
        try
        {
            options = ResolveComputedPointerScanOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            Console.WriteLine("Computed pointer scan:");
            Console.WriteLine($"Targets: {options.Targets.Count}");
            foreach (var target in options.Targets)
            {
                Console.WriteLine($"Target: name={target.Name} bytes={FormatScanBytes(target.Bytes)}");
            }

            foreach (var path in options.Paths)
            {
                var bytes = File.ReadAllBytes(path);
                var minidump = ReadMinidumpMappingIfPresent(bytes);
                Console.WriteLine(
                    $"File: path={path} length={bytes.Length.ToString(CultureInfo.InvariantCulture)} " +
                    $"minidump={minidump is not null} modules={minidump?.Modules.Count ?? 0} " +
                    $"memoryRanges={minidump?.MemoryRanges.Count ?? 0}");

                foreach (var target in options.Targets)
                {
                    var offsets = FindBytePatternOffsets(bytes, target.Bytes);
                    Console.WriteLine(
                        $"Hit: target={target.Name} count={offsets.Count.ToString(CultureInfo.InvariantCulture)} " +
                        $"offsets={FormatScanOffsets(offsets)}");

                    foreach (var offset in offsets)
                    {
                        PrintComputedPointerHitContext(bytes, target, offset, options.ContextBytes);
                    }

                    if (minidump is null)
                    {
                        continue;
                    }

                    foreach (var offset in offsets)
                    {
                        var mappedAddress = minidump.MapFileOffset(offset);
                        if (!mappedAddress.HasValue)
                        {
                            Console.WriteLine(
                                $"Map: target={target.Name} offset={FormatScanHex(offset)} va=<unmapped> " +
                                "module=<none> rva=<none>");
                            continue;
                        }

                        var module = minidump.FindModule(mappedAddress.Value);
                        Console.WriteLine(
                            $"Map: target={target.Name} offset={FormatScanHex(offset)} " +
                            $"va={FormatScanHex(mappedAddress.Value)} module={FormatScanModule(module)} " +
                            $"rva={FormatScanRva(mappedAddress.Value, module)}");
                    }
                }
            }

            return 0;
        }
        catch (Exception ex) when (
            ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Computed pointer scan failed: {ex.Message}");
            return 1;
        }
    }

    static ComputedPointerScanOptions ResolveComputedPointerScanOptions(string[] args)
    {
        var targets = new List<ComputedPointerScanTarget>();
        var paths = new List<string>();
        var contextBytes = 0;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--target", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException(ComputedPointerScanUsage);
                }

                targets.Add(ParseComputedPointerScanTarget(args[++index]));
                continue;
            }

            if (string.Equals(argument, "--context-bytes", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("computed-pointer-scan --context-bytes requires a byte count.");
                }

                contextBytes = ParseComputedPointerScanContextBytes(args[++index]);
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown computed-pointer-scan option: {argument}");
            }

            paths.Add(Path.GetFullPath(argument));
        }

        if (targets.Count == 0 || paths.Count == 0)
        {
            throw new ArgumentException(ComputedPointerScanUsage);
        }

        return new ComputedPointerScanOptions(targets, paths, contextBytes);
    }

    static ComputedPointerScanTarget ParseComputedPointerScanTarget(string value)
    {
        var separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            throw new ArgumentException("Computed pointer targets must use name=hexbytes.");
        }

        var name = value[..separatorIndex].Trim();
        if (name.Length == 0 || name.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Computed pointer target names must be non-empty and contain no whitespace.");
        }

        var bytes = ParseHexBytes(value[(separatorIndex + 1)..]).ToArray();
        if (bytes.Length == 0)
        {
            throw new ArgumentException("Computed pointer targets must contain at least one byte.");
        }

        return new ComputedPointerScanTarget(name, bytes);
    }

    static int ParseComputedPointerScanContextBytes(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var contextBytes) ||
            contextBytes < 0 ||
            contextBytes > MaxComputedPointerContextBytes)
        {
            throw new ArgumentException(
                $"computed-pointer-scan --context-bytes must be between 0 and {MaxComputedPointerContextBytes.ToString(CultureInfo.InvariantCulture)}.");
        }

        return contextBytes;
    }

    static IReadOnlyList<long> FindBytePatternOffsets(byte[] bytes, byte[] pattern)
    {
        var offsets = new List<long>();
        if (pattern.Length > bytes.Length)
        {
            return offsets;
        }

        var source = bytes.AsSpan();
        var needle = pattern.AsSpan();
        var searchOffset = 0;
        while (searchOffset <= source.Length - needle.Length)
        {
            var relativeOffset = source[searchOffset..].IndexOf(needle);
            if (relativeOffset < 0)
            {
                break;
            }

            var absoluteOffset = searchOffset + relativeOffset;
            offsets.Add(absoluteOffset);
            searchOffset = absoluteOffset + 1;
        }

        return offsets;
    }

    static void PrintComputedPointerHitContext(
        byte[] bytes,
        ComputedPointerScanTarget target,
        long offset,
        int contextBytes)
    {
        if (contextBytes == 0)
        {
            return;
        }

        var hitOffset = checked((int)offset);
        var startOffset = Math.Max(0, hitOffset - contextBytes);
        var hitEndOffset = Math.Min(bytes.Length, hitOffset + target.Bytes.Length);
        var endOffset = Math.Min(bytes.Length, hitEndOffset + contextBytes);
        var context = bytes.AsSpan(startOffset, endOffset - startOffset).ToArray();
        Console.WriteLine(
            $"Context: target={target.Name} offset={FormatScanHex(offset)} " +
            $"startOffset={FormatScanHex(startOffset)} bytes={FormatScanBytes(context)}");
    }

    static ComputedPointerMinidumpMapping? ReadMinidumpMappingIfPresent(byte[] bytes)
    {
        var data = bytes.AsSpan();
        if (data.Length < MinidumpHeaderSize || ReadUInt32(data, 0) != MinidumpSignature)
        {
            return null;
        }

        var streamCount = ReadUInt32(data, 8);
        var streamDirectoryRva = ReadUInt32(data, 12);
        EnsureMinidumpRange(data, streamDirectoryRva, checked(streamCount * MinidumpDirectorySize), "stream directory");

        var modules = Array.Empty<ComputedPointerMinidumpModule>();
        var memoryRanges = new List<ComputedPointerMinidumpMemoryRange>();

        for (var index = 0; index < streamCount; index++)
        {
            var directoryOffset = checked((long)streamDirectoryRva + (long)index * MinidumpDirectorySize);
            var streamType = ReadUInt32(data, directoryOffset);
            var streamSize = ReadUInt32(data, directoryOffset + 4);
            var streamRva = ReadUInt32(data, directoryOffset + 8);
            EnsureMinidumpRange(data, streamRva, streamSize, $"stream {streamType}");

            if (streamType == MinidumpModuleListStream)
            {
                modules = ReadMinidumpModules(data, streamRva, streamSize);
            }
            else if (streamType == MinidumpMemoryListStream)
            {
                memoryRanges.AddRange(ReadMinidumpMemoryList(data, streamRva, streamSize));
            }
            else if (streamType == MinidumpMemory64ListStream)
            {
                memoryRanges.AddRange(ReadMinidumpMemory64List(data, streamRva, streamSize));
            }
        }

        return new ComputedPointerMinidumpMapping(memoryRanges, modules);
    }

    static ComputedPointerMinidumpModule[] ReadMinidumpModules(
        ReadOnlySpan<byte> data,
        long streamRva,
        long streamSize)
    {
        if (streamSize < sizeof(uint))
        {
            throw new InvalidDataException("Minidump module list stream is too small.");
        }

        var moduleCount = ReadUInt32(data, streamRva);
        EnsureMinidumpRange(
            data,
            streamRva + sizeof(uint),
            checked(moduleCount * MinidumpModuleSize),
            "module list");

        var modules = new ComputedPointerMinidumpModule[moduleCount];
        for (var index = 0; index < moduleCount; index++)
        {
            var moduleOffset = streamRva + sizeof(uint) + index * MinidumpModuleSize;
            var baseAddress = ReadUInt64(data, moduleOffset);
            var size = ReadUInt32(data, moduleOffset + 8);
            var nameRva = ReadUInt32(data, moduleOffset + 20);
            modules[index] = new ComputedPointerMinidumpModule(
                baseAddress,
                size,
                ReadMinidumpModuleName(data, nameRva));
        }

        return modules
            .OrderBy(module => module.BaseAddress)
            .ToArray();
    }

    static IReadOnlyList<ComputedPointerMinidumpMemoryRange> ReadMinidumpMemoryList(
        ReadOnlySpan<byte> data,
        long streamRva,
        long streamSize)
    {
        if (streamSize < sizeof(uint))
        {
            throw new InvalidDataException("Minidump memory list stream is too small.");
        }

        var rangeCount = ReadUInt32(data, streamRva);
        EnsureMinidumpRange(
            data,
            streamRva + sizeof(uint),
            checked(rangeCount * MinidumpMemoryDescriptorSize),
            "memory list");

        var ranges = new List<ComputedPointerMinidumpMemoryRange>();
        for (var index = 0; index < rangeCount; index++)
        {
            var descriptorOffset = streamRva + sizeof(uint) + index * MinidumpMemoryDescriptorSize;
            var startAddress = ReadUInt64(data, descriptorOffset);
            var dataSize = ReadUInt32(data, descriptorOffset + 8);
            var fileOffset = ReadUInt32(data, descriptorOffset + 12);
            if (dataSize == 0)
            {
                continue;
            }

            EnsureMinidumpRange(data, fileOffset, dataSize, "memory range");
            ranges.Add(new ComputedPointerMinidumpMemoryRange(startAddress, dataSize, fileOffset));
        }

        return ranges;
    }

    static IReadOnlyList<ComputedPointerMinidumpMemoryRange> ReadMinidumpMemory64List(
        ReadOnlySpan<byte> data,
        long streamRva,
        long streamSize)
    {
        if (streamSize < MinidumpMemory64HeaderSize)
        {
            throw new InvalidDataException("Minidump memory64 list stream is too small.");
        }

        var rangeCount = ReadUInt64(data, streamRva);
        var baseFileOffset = ReadUInt64(data, streamRva + sizeof(ulong));
        var descriptorByteCount = checked((long)(rangeCount * MinidumpMemory64DescriptorSize));
        EnsureMinidumpRange(
            data,
            streamRva + MinidumpMemory64HeaderSize,
            descriptorByteCount,
            "memory64 list");

        var ranges = new List<ComputedPointerMinidumpMemoryRange>();
        var fileOffset = baseFileOffset;
        for (ulong index = 0; index < rangeCount; index++)
        {
            var descriptorOffset = streamRva + MinidumpMemory64HeaderSize + checked((long)index * MinidumpMemory64DescriptorSize);
            var startAddress = ReadUInt64(data, descriptorOffset);
            var dataSize = ReadUInt64(data, descriptorOffset + sizeof(ulong));
            if (dataSize > 0)
            {
                EnsureMinidumpRange(data, fileOffset, dataSize, "memory64 range");
                ranges.Add(new ComputedPointerMinidumpMemoryRange(startAddress, dataSize, fileOffset));
            }

            fileOffset = checked(fileOffset + dataSize);
        }

        return ranges;
    }

    static string ReadMinidumpModuleName(ReadOnlySpan<byte> data, long nameRva)
    {
        if (nameRva == 0)
        {
            return "<unknown>";
        }

        EnsureMinidumpRange(data, nameRva, sizeof(uint), "module name length");
        var byteLength = ReadUInt32(data, nameRva);
        EnsureMinidumpRange(data, nameRva + sizeof(uint), byteLength, "module name");
        var fullName = Encoding.Unicode.GetString(data.Slice(checked((int)nameRva + sizeof(uint)), checked((int)byteLength)));
        return FormatModuleFileName(fullName);
    }

    static string FormatModuleFileName(string fullName)
    {
        var normalized = fullName.TrimEnd('\0');
        var separatorIndex = normalized.LastIndexOfAny(['\\', '/']);
        return separatorIndex >= 0 && separatorIndex + 1 < normalized.Length
            ? normalized[(separatorIndex + 1)..]
            : normalized;
    }

    static void EnsureMinidumpRange(ReadOnlySpan<byte> data, ulong offset, ulong length, string description)
    {
        if (offset > (ulong)data.Length || length > (ulong)data.Length - offset)
        {
            throw new InvalidDataException($"Minidump {description} points outside the file.");
        }
    }

    static void EnsureMinidumpRange(ReadOnlySpan<byte> data, long offset, long length, string description)
    {
        if (offset < 0 || length < 0 || offset > data.Length || length > data.Length - offset)
        {
            throw new InvalidDataException($"Minidump {description} points outside the file.");
        }
    }

    static uint ReadUInt32(ReadOnlySpan<byte> data, long offset)
    {
        EnsureMinidumpRange(data, offset, sizeof(uint), "uint32");
        return BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(checked((int)offset), sizeof(uint)));
    }

    static ulong ReadUInt64(ReadOnlySpan<byte> data, long offset)
    {
        EnsureMinidumpRange(data, offset, sizeof(ulong), "uint64");
        return BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(checked((int)offset), sizeof(ulong)));
    }

    static string FormatScanBytes(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    static string FormatScanOffsets(IReadOnlyList<long> offsets)
    {
        return offsets.Count == 0
            ? "<none>"
            : string.Join(",", offsets.Select(FormatScanHex));
    }

    static string FormatScanHex(long value)
    {
        return $"0x{value:X}";
    }

    static string FormatScanHex(ulong value)
    {
        return $"0x{value:X}";
    }

    static string FormatScanModule(ComputedPointerMinidumpModule? module)
    {
        return module?.Name ?? "<none>";
    }

    static string FormatScanRva(ulong address, ComputedPointerMinidumpModule? module)
    {
        return module is null ? "<none>" : FormatScanHex(address - module.BaseAddress);
    }

    const string ComputedPointerScanUsage =
        "Usage: WolfConquer.DataTool computed-pointer-scan --target name=hexbytes [--target name=hexbytes ...] [--context-bytes N] <path> [path ...]";

    const uint MinidumpSignature = 0x504D444D;
    const uint MinidumpModuleListStream = 4;
    const uint MinidumpMemoryListStream = 5;
    const uint MinidumpMemory64ListStream = 9;
    const int MinidumpHeaderSize = 32;
    const int MinidumpDirectorySize = 12;
    const int MinidumpModuleSize = 108;
    const int MinidumpMemoryDescriptorSize = 16;
    const int MinidumpMemory64HeaderSize = 16;
    const int MinidumpMemory64DescriptorSize = 16;
    const int MaxComputedPointerContextBytes = 65536;
}

internal sealed record ComputedPointerScanOptions(
    IReadOnlyList<ComputedPointerScanTarget> Targets,
    IReadOnlyList<string> Paths,
    int ContextBytes);

internal sealed record ComputedPointerScanTarget(string Name, byte[] Bytes);

internal sealed record ComputedPointerMinidumpModule(ulong BaseAddress, ulong Size, string Name);

internal sealed record ComputedPointerMinidumpMemoryRange(
    ulong StartAddress,
    ulong Size,
    ulong FileOffset);

internal sealed class ComputedPointerMinidumpMapping(
    IReadOnlyList<ComputedPointerMinidumpMemoryRange> memoryRanges,
    IReadOnlyList<ComputedPointerMinidumpModule> modules)
{
    public IReadOnlyList<ComputedPointerMinidumpMemoryRange> MemoryRanges { get; } = memoryRanges;

    public IReadOnlyList<ComputedPointerMinidumpModule> Modules { get; } = modules;

    public ulong? MapFileOffset(long fileOffset)
    {
        if (fileOffset < 0)
        {
            return null;
        }

        var unsignedOffset = (ulong)fileOffset;
        var range = MemoryRanges.FirstOrDefault(memoryRange =>
            unsignedOffset >= memoryRange.FileOffset &&
            unsignedOffset < memoryRange.FileOffset + memoryRange.Size);

        return range is null
            ? null
            : range.StartAddress + (unsignedOffset - range.FileOffset);
    }

    public ComputedPointerMinidumpModule? FindModule(ulong address)
    {
        return Modules.FirstOrDefault(module =>
            address >= module.BaseAddress &&
            address < module.BaseAddress + module.Size);
    }
}
