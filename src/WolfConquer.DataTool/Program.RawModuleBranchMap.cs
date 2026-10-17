using System.Buffers.Binary;
using System.Globalization;

internal static partial class Program
{
    const int DefaultRawModuleBranchSpineSteps = 32;
    const int DefaultRawModuleBranchSpinePreludeBytes = 16;

    static int RawModuleBranchMap(string[] args)
    {
        RawModuleBranchMapOptions options;
        try
        {
            options = ResolveRawModuleBranchMapOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var bytes = File.ReadAllBytes(options.RawModulePath);
            if (bytes.LongLength == 0)
            {
                throw new InvalidDataException("Raw module is empty.");
            }

            var maps = BuildRawModuleBranchMaps(options, bytes);

            Console.WriteLine("Raw module branch map:");
            Console.WriteLine($"Path: {options.RawModulePath}");
            Console.WriteLine($"Module: {options.ModuleName}");
            Console.WriteLine($"Base: {FormatHexAddress(options.BaseAddress)}");
            Console.WriteLine($"Raw bytes: {bytes.LongLength}");
            Console.WriteLine($"Follow depth: {options.FollowDepth}");
            Console.WriteLine($"Probes: {maps.Count}");
            foreach (var map in maps)
            {
                Console.WriteLine(
                    $"Probe: {map.ModuleName}+{map.Rva} " +
                    $"offset={map.Rva} address={map.Address} bytes={map.ByteCount}");
                if (!options.SummaryOnly)
                {
                    Console.WriteLine($"Instructions decoded: {map.Instructions.Count}");
                    foreach (var instruction in map.Instructions)
                    {
                        var moduleText = string.IsNullOrWhiteSpace(instruction.ModuleRva)
                            ? string.Empty
                            : $" ({instruction.ModuleName}+{instruction.ModuleRva})";
                        var targetText = string.IsNullOrWhiteSpace(instruction.TargetText)
                            ? string.Empty
                            : $" -> {instruction.TargetText}";
                        Console.WriteLine($"  {instruction.Address}{moduleText}: {instruction.Text}{targetText}");
                    }
                }

                Console.WriteLine($"Branch targets: {FormatEvidenceMapValues(map.LeadingBranchTargets)}");
            }

            return 0;
        }
        catch (Exception ex) when (
            ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException)
        {
            Console.Error.WriteLine($"Raw module branch map failed: {ex.Message}");
            return 1;
        }
    }

    static int RawModuleBranchSpine(string[] args)
    {
        RawModuleBranchSpineOptions options;
        try
        {
            options = ResolveRawModuleBranchSpineOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var bytes = File.ReadAllBytes(options.RawModulePath);
            if (bytes.LongLength == 0)
            {
                throw new InvalidDataException("Raw module is empty.");
            }

            if (options.Rva < 0 || options.Rva >= bytes.LongLength)
            {
                throw new InvalidDataException(
                    $"RVA {FormatHexAddress(options.Rva)} is outside raw module bounds 0x0-{FormatHexAddress(bytes.LongLength - 1)}.");
            }

            var spine = BuildRawModuleBranchSpine(options, bytes);

            Console.WriteLine("Raw module branch spine:");
            Console.WriteLine($"Path: {options.RawModulePath}");
            Console.WriteLine($"Module: {options.ModuleName}");
            Console.WriteLine($"Base: {FormatHexAddress(options.BaseAddress)}");
            Console.WriteLine($"Raw bytes: {bytes.LongLength}");
            Console.WriteLine($"Start: {FormatRawModuleBranchSpineModuleRva(options.ModuleName, options.Rva)}");
            Console.WriteLine($"Steps: {options.Steps}");
            Console.WriteLine($"Prelude bytes: {options.PreludeBytes}");
            foreach (var step in spine.Steps)
            {
                var jumpText = step.JumpRva == step.FromRva
                    ? string.Empty
                    : $" (jmp at {FormatRawModuleBranchSpineModuleRva(options.ModuleName, step.JumpRva)})";
                var targetText = step.TargetInModule
                    ? FormatRawModuleBranchSpineModuleRva(options.ModuleName, step.TargetRva)
                    : FormatHexAddress(options.BaseAddress + step.TargetRva);
                Console.WriteLine(
                    $"Step {step.Index}: {FormatRawModuleBranchSpineModuleRva(options.ModuleName, step.FromRva)} -> {targetText}{jumpText}");
            }

            Console.WriteLine($"Stop: {spine.StopReason}");
            return 0;
        }
        catch (Exception ex) when (
            ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException)
        {
            Console.Error.WriteLine($"Raw module branch spine failed: {ex.Message}");
            return 1;
        }
    }

    static int RawModuleBranchPatchSpec(string[] args)
    {
        RawModuleBranchPatchSpecOptions options;
        try
        {
            options = ResolveRawModuleBranchPatchSpecOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var bytes = File.ReadAllBytes(options.RawModulePath);
            if (bytes.LongLength == 0)
            {
                throw new InvalidDataException("Raw module is empty.");
            }

            var spec = BuildRawModuleBranchPatchSpec(options, bytes);
            Console.WriteLine(
                $"{options.Name},{options.ModuleName},{FormatHexAddress(options.SourceRva)}," +
                $"{Convert.ToHexString(spec.ExpectedBytes)},{Convert.ToHexString(spec.PatchBytes)}");
            return 0;
        }
        catch (Exception ex) when (
            ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException)
        {
            Console.Error.WriteLine($"Raw module branch patch spec failed: {ex.Message}");
            return 1;
        }
    }

    static RawModuleBranchMapOptions ResolveRawModuleBranchMapOptions(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool raw-module-branch-map <rawModulePath> --rva hexRva [--rva hexRva ...] [--length N] [--module-name name] [--base hexAddress] [--follow-depth N] [--summary-only]");
        }

        var rawModulePath = Path.GetFullPath(args[1]);
        if (!File.Exists(rawModulePath))
        {
            throw new ArgumentException($"Raw module path not found: {rawModulePath}");
        }

        var rvas = new List<long>();
        var length = 128;
        var moduleName = Path.GetFileNameWithoutExtension(rawModulePath);
        var baseAddress = 0L;
        var followDepth = 0;
        var summaryOnly = false;

        for (var index = 2; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--summary-only", StringComparison.OrdinalIgnoreCase))
            {
                summaryOnly = true;
                continue;
            }

            if (string.Equals(args[index], "--rva", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                rvas.Add(ParseHexInt64(args[++index], "Rva"));
                continue;
            }

            if (string.Equals(args[index], "--length", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                length = int.Parse(args[++index], NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (length <= 0)
                {
                    throw new ArgumentException("Length must be greater than zero.");
                }

                continue;
            }

            if (string.Equals(args[index], "--module-name", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                moduleName = args[++index];
                if (string.IsNullOrWhiteSpace(moduleName))
                {
                    throw new ArgumentException("Module name cannot be empty.");
                }

                continue;
            }

            if (string.Equals(args[index], "--base", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                baseAddress = ParseHexInt64(args[++index], "BaseAddress");
                continue;
            }

            if (string.Equals(args[index], "--follow-depth", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                if (!int.TryParse(args[++index], NumberStyles.Integer, CultureInfo.InvariantCulture, out followDepth) ||
                    followDepth < 0)
                {
                    throw new ArgumentException("follow-depth must be a non-negative integer.");
                }

                continue;
            }

            throw new ArgumentException($"Unknown raw-module-branch-map argument: {args[index]}");
        }

        if (rvas.Count == 0)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool raw-module-branch-map <rawModulePath> --rva hexRva [--rva hexRva ...] [--length N] [--module-name name] [--base hexAddress] [--follow-depth N] [--summary-only]");
        }

        return new RawModuleBranchMapOptions(rawModulePath, moduleName, baseAddress, length, followDepth, summaryOnly, rvas);
    }

    static RawModuleBranchSpineOptions ResolveRawModuleBranchSpineOptions(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(RawModuleBranchSpineUsage());
        }

        var rawModulePath = Path.GetFullPath(args[1]);
        if (!File.Exists(rawModulePath))
        {
            throw new ArgumentException($"Raw module path not found: {rawModulePath}");
        }

        long? rva = null;
        var length = 128;
        var moduleName = Path.GetFileNameWithoutExtension(rawModulePath);
        var baseAddress = 0L;
        var steps = DefaultRawModuleBranchSpineSteps;
        var preludeBytes = DefaultRawModuleBranchSpinePreludeBytes;

        for (var index = 2; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--rva", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                rva = ParseHexInt64(args[++index], "Rva");
                continue;
            }

            if (string.Equals(args[index], "--length", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                length = int.Parse(args[++index], NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (length <= 0)
                {
                    throw new ArgumentException("Length must be greater than zero.");
                }

                continue;
            }

            if (string.Equals(args[index], "--module-name", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                moduleName = args[++index];
                if (string.IsNullOrWhiteSpace(moduleName))
                {
                    throw new ArgumentException("Module name cannot be empty.");
                }

                continue;
            }

            if (string.Equals(args[index], "--base", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                baseAddress = ParseHexInt64(args[++index], "BaseAddress");
                continue;
            }

            if (string.Equals(args[index], "--steps", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                if (!int.TryParse(args[++index], NumberStyles.Integer, CultureInfo.InvariantCulture, out steps) ||
                    steps <= 0)
                {
                    throw new ArgumentException("steps must be a positive integer.");
                }

                continue;
            }

            if (string.Equals(args[index], "--prelude-bytes", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                if (!int.TryParse(args[++index], NumberStyles.Integer, CultureInfo.InvariantCulture, out preludeBytes) ||
                    preludeBytes <= 0)
                {
                    throw new ArgumentException("prelude-bytes must be a positive integer.");
                }

                continue;
            }

            throw new ArgumentException($"Unknown raw-module-branch-spine argument: {args[index]}");
        }

        if (!rva.HasValue)
        {
            throw new ArgumentException(RawModuleBranchSpineUsage());
        }

        return new RawModuleBranchSpineOptions(
            rawModulePath,
            moduleName,
            baseAddress,
            length,
            steps,
            preludeBytes,
            rva.Value);
    }

    static RawModuleBranchPatchSpecOptions ResolveRawModuleBranchPatchSpecOptions(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(RawModuleBranchPatchSpecUsage());
        }

        var rawModulePath = Path.GetFullPath(args[1]);
        if (!File.Exists(rawModulePath))
        {
            throw new ArgumentException($"Raw module path not found: {rawModulePath}");
        }

        var moduleName = Path.GetFileNameWithoutExtension(rawModulePath);
        string? name = null;
        long? sourceRva = null;
        long? targetRva = null;
        int? byteCount = null;
        RawModuleBranchPatchMode? mode = null;

        for (var index = 2; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--module-name", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                moduleName = args[++index];
                if (string.IsNullOrWhiteSpace(moduleName))
                {
                    throw new ArgumentException("Module name cannot be empty.");
                }

                if (moduleName.Contains(',', StringComparison.Ordinal))
                {
                    throw new ArgumentException("Module name cannot contain commas.");
                }

                continue;
            }

            if (string.Equals(args[index], "--name", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                name = args[++index];
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("Patch spec name cannot be empty.");
                }

                if (name.Contains(',', StringComparison.Ordinal))
                {
                    throw new ArgumentException("Patch spec name cannot contain commas.");
                }

                continue;
            }

            if (string.Equals(args[index], "--source-rva", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                sourceRva = ParseHexInt64(args[++index], "SourceRva");
                continue;
            }

            if (string.Equals(args[index], "--target-rva", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                targetRva = ParseHexInt64(args[++index], "TargetRva");
                continue;
            }

            if (string.Equals(args[index], "--bytes", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                if (!int.TryParse(args[++index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedByteCount) ||
                    parsedByteCount <= 0)
                {
                    throw new ArgumentException("bytes must be a positive integer.");
                }

                byteCount = parsedByteCount;
                continue;
            }

            if (string.Equals(args[index], "--mode", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                mode = ParseRawModuleBranchPatchMode(args[++index]);
                continue;
            }

            throw new ArgumentException($"Unknown raw-module-branch-patch-spec argument: {args[index]}");
        }

        if (string.IsNullOrWhiteSpace(name) ||
            !sourceRva.HasValue ||
            !byteCount.HasValue)
        {
            throw new ArgumentException(RawModuleBranchPatchSpecUsage());
        }

        if (sourceRva.Value < 0)
        {
            throw new ArgumentException("source-rva must be non-negative.");
        }

        var resolvedMode = mode ?? InferRawModuleBranchPatchMode(byteCount.Value);
        if (resolvedMode == RawModuleBranchPatchMode.NoOp)
        {
            if (targetRva.HasValue)
            {
                throw new ArgumentException("target-rva is not allowed when --mode no-op; omit --target-rva for no-op patch specs.");
            }
        }
        else if (!targetRva.HasValue)
        {
            throw new ArgumentException("target-rva is required unless --mode no-op.");
        }
        else if (targetRva.Value < 0)
        {
            throw new ArgumentException("target-rva must be non-negative.");
        }

        ValidateRawModuleBranchPatchByteCount(resolvedMode, byteCount.Value);
        return new RawModuleBranchPatchSpecOptions(
            rawModulePath,
            moduleName,
            name,
            sourceRva.Value,
            targetRva,
            byteCount.Value,
            resolvedMode);
    }

    static IReadOnlyList<CachedClientModuleMemoryProbeMap> BuildRawModuleBranchMaps(
        RawModuleBranchMapOptions options,
        byte[] rawModuleBytes)
    {
        var module = new CachedClientThreadModule(
            options.ModuleName,
            options.RawModulePath,
            options.BaseAddress,
            rawModuleBytes.LongLength);

        var maps = new List<CachedClientModuleMemoryProbeMap>();
        var pending = new Queue<RawModuleBranchMapPendingProbe>();
        var seen = new HashSet<long>();
        var queued = new HashSet<long>();
        foreach (var rva in options.Rvas)
        {
            if (queued.Add(rva))
            {
                pending.Enqueue(new RawModuleBranchMapPendingProbe(rva, 0));
            }
        }

        while (pending.Count > 0)
        {
            var pendingProbe = pending.Dequeue();
            var rva = pendingProbe.Rva;
            if (!seen.Add(rva))
            {
                continue;
            }

            if (rva < 0 || rva >= rawModuleBytes.LongLength)
            {
                throw new InvalidDataException(
                    $"RVA {FormatHexAddress(rva)} is outside raw module bounds 0x0-{FormatHexAddress(rawModuleBytes.LongLength - 1)}.");
            }

            var available = checked((int)Math.Min(options.Length, rawModuleBytes.LongLength - rva));
            var slice = new byte[available];
            Array.Copy(rawModuleBytes, rva, slice, 0, available);

            var probe = new CachedClientModuleMemoryProbeRow(
                $"{options.ModuleName}+{FormatHexAddress(rva)}",
                options.ModuleName,
                FormatHexAddress(rva),
                FormatHexAddress(options.BaseAddress + rva),
                available,
                true,
                Convert.ToHexString(slice),
                string.Empty);
            var map = BuildCachedClientModuleMemoryProbeMap(probe, [module]);
            maps.Add(map);

            if (pendingProbe.Depth >= options.FollowDepth)
            {
                continue;
            }

            foreach (var targetRva in GetRawModuleBranchMapFollowTargets(map, options.ModuleName))
            {
                if (targetRva >= 0 &&
                    targetRva < rawModuleBytes.LongLength &&
                    queued.Add(targetRva))
                {
                    pending.Enqueue(new RawModuleBranchMapPendingProbe(targetRva, pendingProbe.Depth + 1));
                }
            }
        }

        return maps;
    }

    static IReadOnlyList<long> GetRawModuleBranchMapFollowTargets(
        CachedClientModuleMemoryProbeMap map,
        string moduleName)
    {
        return map.Instructions
            .Where(instruction =>
                instruction.Kind == CachedClientThreadInstructionKind.Jump &&
                string.Equals(instruction.TargetModuleName, moduleName, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(instruction.TargetModuleRva))
            .Select(instruction => ParseHexInt64(instruction.TargetModuleRva, "TargetRva"))
            .Distinct()
            .ToArray();
    }

    static RawModuleBranchSpine BuildRawModuleBranchSpine(
        RawModuleBranchSpineOptions options,
        byte[] rawModuleBytes)
    {
        var steps = new List<RawModuleBranchSpineStep>();
        var seen = new HashSet<long> { options.Rva };
        var currentRva = options.Rva;

        for (var index = 0; index < options.Steps; index++)
        {
            var jump = FindFirstRawModuleBranchSpineJump(rawModuleBytes, currentRva, options.Length, options.PreludeBytes);
            if (jump is null)
            {
                return new RawModuleBranchSpine(
                    steps,
                    $"no leading unconditional jump found within {options.PreludeBytes} bytes at {FormatRawModuleBranchSpineModuleRva(options.ModuleName, currentRva)}");
            }

            var targetInModule = jump.TargetRva >= 0 && jump.TargetRva < rawModuleBytes.LongLength;
            steps.Add(new RawModuleBranchSpineStep(index, currentRva, jump.JumpRva, jump.TargetRva, targetInModule));
            if (!targetInModule)
            {
                return new RawModuleBranchSpine(
                    steps,
                    $"out-of-module target {FormatHexAddress(options.BaseAddress + jump.TargetRva)}");
            }

            if (!seen.Add(jump.TargetRva))
            {
                return new RawModuleBranchSpine(
                    steps,
                    $"repeat detected at {FormatRawModuleBranchSpineModuleRva(options.ModuleName, jump.TargetRva)}");
            }

            currentRva = jump.TargetRva;
        }

        return new RawModuleBranchSpine(
            steps,
            $"max steps reached ({options.Steps})");
    }

    static RawModuleBranchPatchSpec BuildRawModuleBranchPatchSpec(
        RawModuleBranchPatchSpecOptions options,
        byte[] rawModuleBytes)
    {
        if (options.SourceRva < 0 ||
            options.SourceRva >= rawModuleBytes.LongLength ||
            options.SourceRva > rawModuleBytes.LongLength - options.ByteCount)
        {
            throw new InvalidDataException(
                $"RVA {FormatHexAddress(options.SourceRva)} with {options.ByteCount} byte(s) is outside raw module bounds 0x0-{FormatHexAddress(rawModuleBytes.LongLength - 1)}.");
        }

        var expectedBytes = new byte[options.ByteCount];
        Array.Copy(rawModuleBytes, options.SourceRva, expectedBytes, 0, options.ByteCount);
        var patchBytes = options.Mode == RawModuleBranchPatchMode.NoOp
            ? expectedBytes
            : BuildRawModuleBranchPatchBytes(options.Mode, options.SourceRva, options.TargetRva!.Value);
        return new RawModuleBranchPatchSpec(expectedBytes, patchBytes);
    }

    static byte[] BuildRawModuleBranchPatchBytes(
        RawModuleBranchPatchMode mode,
        long sourceRva,
        long targetRva)
    {
        return mode switch
        {
            RawModuleBranchPatchMode.ShortJump => BuildRawModuleBranchShortJumpPatchBytes(sourceRva, targetRva),
            RawModuleBranchPatchMode.NearJump => BuildRawModuleBranchNearJumpPatchBytes(sourceRva, targetRva, padWithNop: false),
            RawModuleBranchPatchMode.NearJumpPad => BuildRawModuleBranchNearJumpPatchBytes(sourceRva, targetRva, padWithNop: true),
            RawModuleBranchPatchMode.NoOp => throw new ArgumentException("no-op patch specs use the source bytes as the patch bytes."),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown raw module branch patch mode."),
        };
    }

    static byte[] BuildRawModuleBranchShortJumpPatchBytes(long sourceRva, long targetRva)
    {
        var relative = checked(targetRva - (sourceRva + 2));
        if (relative < sbyte.MinValue || relative > sbyte.MaxValue)
        {
            throw new InvalidDataException(
                $"Target RVA {FormatHexAddress(targetRva)} is outside signed byte range for short-jmp from {FormatHexAddress(sourceRva)}.");
        }

        return [0xEB, unchecked((byte)(sbyte)relative)];
    }

    static byte[] BuildRawModuleBranchNearJumpPatchBytes(long sourceRva, long targetRva, bool padWithNop)
    {
        var patchBytes = new byte[padWithNop ? 6 : 5];
        var relative = checked(targetRva - (sourceRva + 5));
        if (relative < int.MinValue || relative > int.MaxValue)
        {
            throw new InvalidDataException(
                $"Target RVA {FormatHexAddress(targetRva)} is outside signed int range for near jump from {FormatHexAddress(sourceRva)}.");
        }

        patchBytes[0] = 0xE9;
        BinaryPrimitives.WriteInt32LittleEndian(patchBytes.AsSpan(1, 4), (int)relative);
        if (padWithNop)
        {
            patchBytes[^1] = 0x90;
        }

        return patchBytes;
    }

    static RawModuleBranchPatchMode InferRawModuleBranchPatchMode(int byteCount)
    {
        return byteCount switch
        {
            2 => RawModuleBranchPatchMode.ShortJump,
            5 => RawModuleBranchPatchMode.NearJump,
            6 => RawModuleBranchPatchMode.NearJumpPad,
            _ => throw new ArgumentException(
                "bytes must be 2, 5, or 6 when mode is omitted; use --mode short-jmp, near-jmp, near-jmp-pad, or no-op."),
        };
    }

    static RawModuleBranchPatchMode ParseRawModuleBranchPatchMode(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "short-jmp" => RawModuleBranchPatchMode.ShortJump,
            "near-jmp" => RawModuleBranchPatchMode.NearJump,
            "near-jmp-pad" => RawModuleBranchPatchMode.NearJumpPad,
            "no-op" => RawModuleBranchPatchMode.NoOp,
            _ => throw new ArgumentException(
                "mode must be short-jmp, near-jmp, near-jmp-pad, or no-op."),
        };
    }

    static void ValidateRawModuleBranchPatchByteCount(RawModuleBranchPatchMode mode, int byteCount)
    {
        var requiredByteCount = mode switch
        {
            RawModuleBranchPatchMode.ShortJump => 2,
            RawModuleBranchPatchMode.NearJump => 5,
            RawModuleBranchPatchMode.NearJumpPad => 6,
            RawModuleBranchPatchMode.NoOp => byteCount,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown raw module branch patch mode."),
        };

        if (byteCount != requiredByteCount)
        {
            throw new ArgumentException(
                $"mode {FormatRawModuleBranchPatchMode(mode)} requires --bytes {requiredByteCount}.");
        }
    }

    static string FormatRawModuleBranchPatchMode(RawModuleBranchPatchMode mode)
    {
        return mode switch
        {
            RawModuleBranchPatchMode.ShortJump => "short-jmp",
            RawModuleBranchPatchMode.NearJump => "near-jmp",
            RawModuleBranchPatchMode.NearJumpPad => "near-jmp-pad",
            RawModuleBranchPatchMode.NoOp => "no-op",
            _ => mode.ToString(),
        };
    }

    static RawModuleBranchSpineJump? FindFirstRawModuleBranchSpineJump(
        byte[] rawModuleBytes,
        long rva,
        int length,
        int preludeBytes)
    {
        var available = checked((int)Math.Min(length, rawModuleBytes.LongLength - rva));
        var window = Math.Min(available, preludeBytes);
        for (var offset = 0; offset < window; offset++)
        {
            var absoluteOffset = rva + offset;
            var absoluteIndex = checked((int)absoluteOffset);
            var opcode = rawModuleBytes[absoluteIndex];
            if (opcode == 0xE9 && offset + 4 < available)
            {
                var relative = BitConverter.ToInt32(rawModuleBytes, absoluteIndex + 1);
                return new RawModuleBranchSpineJump(absoluteOffset, absoluteOffset + 5 + relative);
            }

            if (opcode == 0xEB && offset + 1 < available)
            {
                var relative = unchecked((sbyte)rawModuleBytes[absoluteIndex + 1]);
                return new RawModuleBranchSpineJump(absoluteOffset, absoluteOffset + 2 + relative);
            }
        }

        return null;
    }

    static string FormatRawModuleBranchSpineModuleRva(string moduleName, long rva)
    {
        return $"{moduleName}+{FormatHexAddress(rva)}";
    }

    static string RawModuleBranchSpineUsage()
    {
        return "Usage: WolfConquer.DataTool raw-module-branch-spine <rawModulePath> --rva hexRva [--length N] [--module-name name] [--base hexAddress] [--steps N] [--prelude-bytes N]";
    }

    static string RawModuleBranchPatchSpecUsage()
    {
        return "Usage: WolfConquer.DataTool raw-module-branch-patch-spec <rawModulePath> --module-name name --source-rva hexRva [--target-rva hexRva] --bytes N --name patchName [--mode short-jmp|near-jmp|near-jmp-pad|no-op]";
    }
}

internal sealed record RawModuleBranchMapOptions(
    string RawModulePath,
    string ModuleName,
    long BaseAddress,
    int Length,
    int FollowDepth,
    bool SummaryOnly,
    IReadOnlyList<long> Rvas);

internal sealed record RawModuleBranchMapPendingProbe(long Rva, int Depth);

internal sealed record RawModuleBranchSpineOptions(
    string RawModulePath,
    string ModuleName,
    long BaseAddress,
    int Length,
    int Steps,
    int PreludeBytes,
    long Rva);

internal sealed record RawModuleBranchPatchSpecOptions(
    string RawModulePath,
    string ModuleName,
    string Name,
    long SourceRva,
    long? TargetRva,
    int ByteCount,
    RawModuleBranchPatchMode Mode);

internal sealed record RawModuleBranchSpine(
    IReadOnlyList<RawModuleBranchSpineStep> Steps,
    string StopReason);

internal sealed record RawModuleBranchSpineStep(
    int Index,
    long FromRva,
    long JumpRva,
    long TargetRva,
    bool TargetInModule);

internal sealed record RawModuleBranchSpineJump(
    long JumpRva,
    long TargetRva);

internal sealed record RawModuleBranchPatchSpec(
    byte[] ExpectedBytes,
    byte[] PatchBytes);

internal enum RawModuleBranchPatchMode
{
    ShortJump,
    NearJump,
    NearJumpPad,
    NoOp,
}
