using System.Globalization;

internal static partial class Program
{
    private const string MinidumpModuleCarveUsage =
        "Usage: WolfConquer.DataTool minidump-module-carve <dumpPath> <moduleName> <outputPath> [--fill hexByte]";

    static int MinidumpModuleCarve(string[] args)
    {
        MinidumpModuleCarveOptions options;
        try
        {
            options = ResolveMinidumpModuleCarveOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var result = CarveMinidumpModule(options);
            Console.WriteLine("Minidump module carve:");
            Console.WriteLine($"Dump: {options.DumpPath}");
            Console.WriteLine($"Module: {result.Module.Name}");
            Console.WriteLine($"Base: {FormatScanHex(result.Module.BaseAddress)}");
            Console.WriteLine($"Size: {FormatScanHex(result.Module.Size)} ({result.Module.Size.ToString(CultureInfo.InvariantCulture)} bytes)");
            Console.WriteLine($"Memory ranges copied: {result.CopiedRangeCount.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Bytes copied: {result.CopiedBytes.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Fill byte: 0x{options.FillByte:X2}");
            Console.WriteLine($"Output: {options.OutputPath}");
            return 0;
        }
        catch (Exception ex) when (
            ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Minidump module carve failed: {ex.Message}");
            return 1;
        }
    }

    static MinidumpModuleCarveOptions ResolveMinidumpModuleCarveOptions(string[] args)
    {
        if (args.Length < 4)
        {
            throw new ArgumentException(MinidumpModuleCarveUsage);
        }

        var dumpPath = Path.GetFullPath(args[1]);
        var moduleName = args[2].Trim();
        var outputPath = Path.GetFullPath(args[3]);
        var fillByte = (byte)0;

        if (moduleName.Length == 0)
        {
            throw new ArgumentException("minidump-module-carve moduleName cannot be empty.");
        }

        for (var index = 4; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--fill", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("minidump-module-carve --fill requires one hex byte.");
                }

                fillByte = ParseMinidumpModuleFillByte(args[++index]);
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown minidump-module-carve option: {argument}");
            }

            throw new ArgumentException(MinidumpModuleCarveUsage);
        }

        return new MinidumpModuleCarveOptions(dumpPath, moduleName, outputPath, fillByte);
    }

    static byte ParseMinidumpModuleFillByte(string value)
    {
        var bytes = ParseHexBytes(value).ToArray();
        if (bytes.Length != 1)
        {
            throw new ArgumentException("minidump-module-carve --fill must be exactly one byte.");
        }

        return bytes[0];
    }

    static MinidumpModuleCarveResult CarveMinidumpModule(MinidumpModuleCarveOptions options)
    {
        var dumpBytes = File.ReadAllBytes(options.DumpPath);
        var mapping = ReadMinidumpMappingIfPresent(dumpBytes);
        if (mapping is null)
        {
            throw new InvalidDataException("Input file is not a minidump.");
        }

        var module = mapping.Modules.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, options.ModuleName, StringComparison.OrdinalIgnoreCase));
        if (module is null)
        {
            var knownModules = string.Join(", ", mapping.Modules.Select(candidate => candidate.Name));
            throw new InvalidDataException(
                $"Minidump module was not found: {options.ModuleName}. Known modules: {knownModules}");
        }

        if (module.Size > int.MaxValue)
        {
            throw new InvalidDataException(
                $"Minidump module is too large to carve into a single file: {module.Size.ToString(CultureInfo.InvariantCulture)} bytes.");
        }

        var outputBytes = new byte[(int)module.Size];
        if (options.FillByte != 0)
        {
            Array.Fill(outputBytes, options.FillByte);
        }

        var moduleStart = module.BaseAddress;
        var moduleEnd = checked(module.BaseAddress + module.Size);
        var copiedRangeCount = 0;
        ulong copiedBytes = 0;

        foreach (var range in mapping.MemoryRanges)
        {
            var rangeStart = range.StartAddress;
            var rangeEnd = checked(range.StartAddress + range.Size);
            var overlapStart = Math.Max(moduleStart, rangeStart);
            var overlapEnd = Math.Min(moduleEnd, rangeEnd);
            if (overlapEnd <= overlapStart)
            {
                continue;
            }

            var sourceOffset = checked(range.FileOffset + (overlapStart - rangeStart));
            var destinationOffset = checked(overlapStart - moduleStart);
            var copyLength = checked(overlapEnd - overlapStart);

            Buffer.BlockCopy(
                dumpBytes,
                checked((int)sourceOffset),
                outputBytes,
                checked((int)destinationOffset),
                checked((int)copyLength));

            copiedRangeCount++;
            copiedBytes = checked(copiedBytes + copyLength);
        }

        var outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllBytes(options.OutputPath, outputBytes);
        return new MinidumpModuleCarveResult(module, copiedRangeCount, copiedBytes);
    }
}

internal sealed record MinidumpModuleCarveOptions(
    string DumpPath,
    string ModuleName,
    string OutputPath,
    byte FillByte);

internal sealed record MinidumpModuleCarveResult(
    ComputedPointerMinidumpModule Module,
    int CopiedRangeCount,
    ulong CopiedBytes);
