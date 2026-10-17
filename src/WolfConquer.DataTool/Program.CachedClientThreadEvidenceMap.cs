using System.Globalization;
using System.Text.Json;

internal static partial class Program
{
    static int CachedClientThreadEvidenceMap(string[] args)
    {
        CachedClientThreadEvidenceMapOptions options;
        try
        {
            options = ResolveCachedClientThreadEvidenceMapOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var evidence = ReadCachedClientThreadEvidence(options.JsonPath, options.ThreadModuleName);
            if (!string.IsNullOrWhiteSpace(options.ProbeJsonPath))
            {
                var probeEvidence = ReadCachedClientThreadModuleProbeEvidence(options.ProbeJsonPath);
                evidence = evidence with
                {
                    ProbeModules = MergeCachedClientThreadModules(evidence.ProbeModules, probeEvidence.Modules),
                    ModuleMemoryProbes = evidence.ModuleMemoryProbes
                        .Concat(probeEvidence.ModuleMemoryProbes)
                        .ToArray(),
                };
            }

            var map = BuildCachedClientThreadEvidenceMap(evidence, options.LicenseModuleName);

            Console.WriteLine("Cached client thread evidence map:");
            Console.WriteLine($"Path: {options.JsonPath}");
            if (!string.IsNullOrWhiteSpace(options.ProbeJsonPath))
            {
                Console.WriteLine($"Probe path: {options.ProbeJsonPath}");
            }

            Console.WriteLine($"Ready: {map.Ready}");
            Console.WriteLine($"ProcessId: {evidence.ProcessId}");
            Console.WriteLine($"Thread: id={evidence.Thread.ThreadId} state={evidence.Thread.ThreadState} cpuMs={evidence.Thread.TotalProcessorTimeMs}");
            Console.WriteLine(
                $"Instruction: {evidence.Thread.InstructionModuleName}+{evidence.Thread.InstructionRva} " +
                $"ip={evidence.Thread.InstructionPointer} base={evidence.Thread.InstructionBaseAddress}");
            Console.WriteLine($"Bytes: {evidence.Thread.InstructionBytes}");
            Console.WriteLine($"Registers: eax={evidence.Thread.Eax} ebx={evidence.Thread.Ebx} ecx={evidence.Thread.Ecx} edx={evidence.Thread.Edx} esp={evidence.Thread.StackPointer} ebp={evidence.Thread.Ebp}");
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

            Console.WriteLine($"Jump targets: {FormatEvidenceMapValues(map.JumpTargets)}");
            Console.WriteLine($"License immediates: {FormatEvidenceMapValues(map.LicenseImmediates)}");
            if (map.RelocatedLicenseImmediates.Count > 0)
            {
                Console.WriteLine($"Stale/relocated license immediates: {FormatCachedClientRelocatedImmediateValues(map.RelocatedLicenseImmediates)}");
            }

            if (map.ModuleProbeMaps.Count > 0)
            {
                Console.WriteLine($"Module probes decoded: {map.ModuleProbeMaps.Count}");
                foreach (var probeMap in map.ModuleProbeMaps)
                {
                    Console.WriteLine(
                        $"Probe: {probeMap.Name} {probeMap.ModuleName}+{probeMap.Rva} " +
                        $"address={probeMap.Address} bytes={probeMap.ByteCount}");
                    foreach (var instruction in probeMap.Instructions)
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

                Console.WriteLine($"Module probe branch targets: {FormatEvidenceMapValues(map.ModuleProbeBranchTargets)}");
            }

            Console.WriteLine($"Next probe: {map.NextProbe}");
            return map.Ready ? 0 : 1;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Cached client thread evidence map failed: {ex.Message}");
            return 1;
        }
    }

    static CachedClientThreadEvidenceMapOptions ResolveCachedClientThreadEvidenceMapOptions(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool cached-client-thread-evidence-map <patchResultJsonPath> [--module DataThread.dll] [--license-module license.dll]");
        }

        var moduleName = "DataThread.dll";
        var licenseModuleName = "license.dll";
        var probeJsonPath = string.Empty;
        for (var index = 2; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--module", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                moduleName = args[++index];
                continue;
            }

            if (string.Equals(args[index], "--license-module", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                licenseModuleName = args[++index];
                continue;
            }

            if (string.Equals(args[index], "--probe-json", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                probeJsonPath = args[++index];
                continue;
            }

            throw new ArgumentException($"Unknown cached-client-thread-evidence-map argument: {args[index]}");
        }

        return new CachedClientThreadEvidenceMapOptions(
            Path.GetFullPath(args[1]),
            string.IsNullOrWhiteSpace(probeJsonPath) ? string.Empty : Path.GetFullPath(probeJsonPath),
            moduleName,
            licenseModuleName);
    }

    static CachedClientThreadEvidence ReadCachedClientThreadEvidence(string path, string threadModuleName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var payload = document.RootElement.TryGetProperty("Result", out var result)
            ? result
            : document.RootElement;

        var modules = ReadCachedClientThreadModules(payload);
        var threads = ReadCachedClientThreadRows(payload);
        var selectedThread = threads
            .Where(thread =>
                !string.IsNullOrWhiteSpace(thread.InstructionBytes) &&
                string.Equals(thread.InstructionModuleName, threadModuleName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(thread => thread.TotalProcessorTimeMs)
            .FirstOrDefault();

        if (selectedThread is null)
        {
            throw new InvalidDataException($"No thread evidence row found for module {threadModuleName} with instruction bytes.");
        }

        var processId = ReadOptionalInt(payload, "ProcessId") ?? selectedThread.ProcessId;
        var moduleMemoryProbes = ReadCachedClientModuleMemoryProbes(payload);
        return new CachedClientThreadEvidence(path, processId, modules, selectedThread, modules, moduleMemoryProbes);
    }

    static CachedClientThreadModuleProbeEvidence ReadCachedClientThreadModuleProbeEvidence(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var payload = document.RootElement.TryGetProperty("Result", out var result)
            ? result
            : document.RootElement;

        return new CachedClientThreadModuleProbeEvidence(
            path,
            ReadCachedClientThreadModules(payload),
            ReadCachedClientModuleMemoryProbes(payload));
    }

    static IReadOnlyList<CachedClientThreadModule> MergeCachedClientThreadModules(
        IReadOnlyList<CachedClientThreadModule> primary,
        IReadOnlyList<CachedClientThreadModule> probe)
    {
        if (probe.Count == 0)
        {
            return primary;
        }

        var modules = primary.ToList();
        foreach (var probeModule in probe)
        {
            var exists = modules.Any(module =>
                string.Equals(module.ModuleName, probeModule.ModuleName, StringComparison.OrdinalIgnoreCase) &&
                module.BaseAddress == probeModule.BaseAddress);
            if (exists)
            {
                continue;
            }

            modules.Add(probeModule);
        }

        return modules;
    }

    static IReadOnlyList<CachedClientThreadModule> ReadCachedClientThreadModules(JsonElement payload)
    {
        if (!payload.TryGetProperty("ModuleEvidenceAfterObserve", out var modulesElement) ||
            modulesElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var modules = new List<CachedClientThreadModule>();
        foreach (var moduleElement in modulesElement.EnumerateArray())
        {
            var baseAddress = ReadOptionalHexInt64(moduleElement, "BaseAddress");
            var size = ReadOptionalInt64(moduleElement, "ModuleMemorySize");
            if (!baseAddress.HasValue || !size.HasValue)
            {
                continue;
            }

            modules.Add(new CachedClientThreadModule(
                ReadOptionalString(moduleElement, "ModuleName"),
                ReadOptionalString(moduleElement, "FileName"),
                baseAddress.Value,
                size.Value));
        }

        return modules;
    }

    static IReadOnlyList<CachedClientThreadRow> ReadCachedClientThreadRows(JsonElement payload)
    {
        if (!payload.TryGetProperty("ThreadEvidenceAfterObserve", out var threadsElement) ||
            threadsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("ThreadEvidenceAfterObserve is missing or not an array.");
        }

        var threads = new List<CachedClientThreadRow>();
        foreach (var threadElement in threadsElement.EnumerateArray())
        {
            threads.Add(new CachedClientThreadRow(
                ReadOptionalInt(threadElement, "ProcessId") ?? 0,
                ReadOptionalInt(threadElement, "ThreadId") ?? 0,
                ReadOptionalString(threadElement, "ThreadState"),
                ReadOptionalString(threadElement, "InstructionPointer"),
                ReadOptionalString(threadElement, "InstructionModuleName"),
                ReadOptionalString(threadElement, "InstructionBaseAddress"),
                ReadOptionalString(threadElement, "InstructionRva"),
                ReadOptionalString(threadElement, "InstructionBytes"),
                ReadOptionalString(threadElement, "StackPointer"),
                ReadOptionalString(threadElement, "Eax"),
                ReadOptionalString(threadElement, "Ebx"),
                ReadOptionalString(threadElement, "Ecx"),
                ReadOptionalString(threadElement, "Edx"),
                ReadOptionalString(threadElement, "Ebp"),
                ReadOptionalInt64(threadElement, "TotalProcessorTimeMs") ?? 0));
        }

        return threads;
    }

    static IReadOnlyList<CachedClientModuleMemoryProbeRow> ReadCachedClientModuleMemoryProbes(JsonElement payload)
    {
        if (!payload.TryGetProperty("ModuleMemoryProbesAfterObserve", out var probesElement) ||
            probesElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var probes = new List<CachedClientModuleMemoryProbeRow>();
        foreach (var probeElement in probesElement.EnumerateArray())
        {
            probes.Add(new CachedClientModuleMemoryProbeRow(
                ReadOptionalString(probeElement, "Name"),
                ReadOptionalString(probeElement, "ModuleName"),
                ReadOptionalString(probeElement, "Rva"),
                ReadOptionalString(probeElement, "Address"),
                ReadOptionalInt(probeElement, "Length") ?? 0,
                ReadOptionalBool(probeElement, "Read") ?? false,
                ReadOptionalString(probeElement, "Hex"),
                ReadOptionalString(probeElement, "FailureReason")));
        }

        return probes;
    }

    static CachedClientThreadEvidenceMapResult BuildCachedClientThreadEvidenceMap(
        CachedClientThreadEvidence evidence,
        string licenseModuleName)
    {
        var ip = ParseHexInt64(evidence.Thread.InstructionPointer, "InstructionPointer");
        var bytes = ParseHexBytes(evidence.Thread.InstructionBytes);
        var instructions = DecodeCachedClientThreadInstructions(bytes, ip, evidence.Modules);
        var probeModules = evidence.ProbeModules.Count == 0 ? evidence.Modules : evidence.ProbeModules;
        var jumpTargets = instructions
            .Where(instruction => instruction.Kind == CachedClientThreadInstructionKind.Jump)
            .Select(instruction => instruction.TargetText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var licenseImmediates = instructions
            .Where(instruction =>
                instruction.Kind == CachedClientThreadInstructionKind.Immediate &&
                string.Equals(instruction.TargetModuleName, licenseModuleName, StringComparison.OrdinalIgnoreCase))
            .Select(instruction => instruction.TargetText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var relocatedLicenseImmediates = FindCachedClientRelocatedImmediates(
            instructions,
            probeModules,
            evidence.ModuleMemoryProbes,
            licenseModuleName);
        var moduleProbeMaps = evidence.ModuleMemoryProbes
            .Where(probe => probe.Read && !string.IsNullOrWhiteSpace(probe.Hex))
            .Select(probe => BuildCachedClientModuleMemoryProbeMap(probe, probeModules))
            .ToArray();
        var moduleProbeBranchTargets = moduleProbeMaps
            .SelectMany(probe => probe.LeadingBranchTargets)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var confirmedRelocatedLicenseTargets = relocatedLicenseImmediates
            .Where(candidate => candidate.MatchingModuleProbeBytes)
            .Select(FormatCachedClientRelocatedImmediateNextProbe)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var immediateTargets = confirmedRelocatedLicenseTargets.Length > 0
            ? confirmedRelocatedLicenseTargets
            : licenseImmediates;
        var ready = jumpTargets.Length > 0 && immediateTargets.Length > 0;
        var nextProbe = ready
            ? $"Map {FormatEvidenceMapValues(jumpTargets)} and inspect {FormatEvidenceMapValues(immediateTargets)} before patching the ret-only shortcut."
            : "Capture more instruction bytes or a dump stack before choosing a patch target.";
        if (moduleProbeBranchTargets.Length > 0)
        {
            nextProbe += $" Continue read-only module probes at {FormatEvidenceMapValues(moduleProbeBranchTargets)} before patching the protected block.";
        }

        return new CachedClientThreadEvidenceMapResult(
            ready,
            instructions,
            jumpTargets,
            licenseImmediates,
            relocatedLicenseImmediates,
            moduleProbeMaps,
            moduleProbeBranchTargets,
            nextProbe);
    }

    static IReadOnlyList<CachedClientRelocatedImmediate> FindCachedClientRelocatedImmediates(
        IReadOnlyList<CachedClientThreadInstruction> instructions,
        IReadOnlyList<CachedClientThreadModule> modules,
        IReadOnlyList<CachedClientModuleMemoryProbeRow> moduleMemoryProbes,
        string moduleName)
    {
        var moduleCandidates = modules
            .Where(module => string.Equals(module.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (moduleCandidates.Length == 0)
        {
            return [];
        }

        var probes = moduleMemoryProbes
            .Where(probe =>
                string.Equals(probe.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(probe.Rva))
            .ToArray();
        if (probes.Length == 0)
        {
            return [];
        }

        var results = new List<CachedClientRelocatedImmediate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var instruction in instructions)
        {
            if (string.IsNullOrWhiteSpace(instruction.ImmediateAddress) ||
                (!string.IsNullOrWhiteSpace(instruction.TargetModuleName) &&
                 !string.Equals(instruction.TargetModuleName, moduleName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var immediateAddress = ParseHexInt64(instruction.ImmediateAddress, "ImmediateAddress");
            foreach (var probe in probes)
            {
                var candidateRva = ParseHexInt64(probe.Rva, "ProbeRva");
                if (candidateRva < 0)
                {
                    continue;
                }

                var module = moduleCandidates.FirstOrDefault(candidate =>
                    candidateRva < candidate.Size &&
                    IsCachedClientProbeAddressForModuleRva(probe, candidate, candidateRva));
                if (module is null)
                {
                    continue;
                }

                var staleModuleBase = immediateAddress - candidateRva;
                if (staleModuleBase <= 0 ||
                    staleModuleBase == module.BaseAddress ||
                    !IsLikelyCachedClientModuleBase(staleModuleBase))
                {
                    continue;
                }

                var currentAbsoluteAddress = module.BaseAddress + candidateRva;
                var matchingProbeBytes =
                    probe.Read &&
                    !string.IsNullOrWhiteSpace(probe.Hex) &&
                    IsCachedClientProbeAddressForModuleRva(probe, module, candidateRva);
                var key = string.Join(
                    "|",
                    FormatHexAddress(immediateAddress),
                    module.ModuleName,
                    FormatHexAddress(candidateRva));
                if (!seen.Add(key))
                {
                    continue;
                }

                results.Add(new CachedClientRelocatedImmediate(
                    FormatHexAddress(immediateAddress),
                    module.ModuleName,
                    FormatHexAddress(candidateRva),
                    $"{module.ModuleName}+{FormatHexAddress(candidateRva)}",
                    FormatHexAddress(currentAbsoluteAddress),
                    FormatHexAddress(staleModuleBase),
                    matchingProbeBytes));
            }
        }

        return results;
    }

    static bool IsCachedClientProbeAddressForModuleRva(
        CachedClientModuleMemoryProbeRow probe,
        CachedClientThreadModule module,
        long rva)
    {
        if (string.IsNullOrWhiteSpace(probe.Address))
        {
            return true;
        }

        return ParseHexInt64(probe.Address, "ProbeAddress") == module.BaseAddress + rva;
    }

    static bool IsLikelyCachedClientModuleBase(long address)
    {
        return (address & 0xFFFF) == 0;
    }

    static CachedClientModuleMemoryProbeMap BuildCachedClientModuleMemoryProbeMap(
        CachedClientModuleMemoryProbeRow probe,
        IReadOnlyList<CachedClientThreadModule> modules)
    {
        var startAddress = string.IsNullOrWhiteSpace(probe.Address)
            ? ResolveCachedClientModuleProbeAddress(probe, modules)
            : ParseHexInt64(probe.Address, "Address");
        var bytes = ParseHexBytes(probe.Hex);
        var probeModules = PreferCachedClientProbeModule(probe, modules, startAddress);
        var instructions = DecodeCachedClientThreadInstructions(bytes, startAddress, probeModules);
        var leadingBranchTargets = instructions
            .Where(instruction =>
                instruction.Kind == CachedClientThreadInstructionKind.Jump &&
                ParseHexInt64(instruction.Address, "InstructionAddress") - startAddress <= 0x20)
            .Select(instruction => instruction.TargetText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CachedClientModuleMemoryProbeMap(
            probe.Name,
            probe.ModuleName,
            probe.Rva,
            FormatHexAddress(startAddress),
            bytes.Count,
            leadingBranchTargets,
            instructions);
    }

    static IReadOnlyList<CachedClientThreadModule> PreferCachedClientProbeModule(
        CachedClientModuleMemoryProbeRow probe,
        IReadOnlyList<CachedClientThreadModule> modules,
        long startAddress)
    {
        var owner = TryResolveCachedClientModuleProbeOwner(probe, modules, startAddress);
        if (owner is null)
        {
            return modules;
        }

        return modules
            .OrderByDescending(module =>
                string.Equals(module.ModuleName, owner.ModuleName, StringComparison.OrdinalIgnoreCase) &&
                module.BaseAddress == owner.BaseAddress)
            .ToArray();
    }

    static CachedClientThreadModule? TryResolveCachedClientModuleProbeOwner(
        CachedClientModuleMemoryProbeRow probe,
        IReadOnlyList<CachedClientThreadModule> modules,
        long startAddress)
    {
        if (!string.IsNullOrWhiteSpace(probe.Rva))
        {
            var rva = ParseHexInt64(probe.Rva, "Rva");
            var exactOwner = modules.FirstOrDefault(module =>
                string.Equals(module.ModuleName, probe.ModuleName, StringComparison.OrdinalIgnoreCase) &&
                rva >= 0 &&
                rva < module.Size &&
                module.BaseAddress + rva == startAddress);
            if (exactOwner is not null)
            {
                return exactOwner;
            }
        }

        return modules.FirstOrDefault(module =>
            string.Equals(module.ModuleName, probe.ModuleName, StringComparison.OrdinalIgnoreCase) &&
            startAddress >= module.BaseAddress &&
            startAddress < module.BaseAddress + module.Size);
    }

    static long ResolveCachedClientModuleProbeAddress(
        CachedClientModuleMemoryProbeRow probe,
        IReadOnlyList<CachedClientThreadModule> modules)
    {
        var owner = modules.FirstOrDefault(module =>
            string.Equals(module.ModuleName, probe.ModuleName, StringComparison.OrdinalIgnoreCase));
        if (owner is null)
        {
            throw new InvalidDataException($"No module evidence row found for probe module {probe.ModuleName}.");
        }

        return owner.BaseAddress + ParseHexInt64(probe.Rva, "Rva");
    }

    static IReadOnlyList<CachedClientThreadInstruction> DecodeCachedClientThreadInstructions(
        IReadOnlyList<byte> bytes,
        long startAddress,
        IReadOnlyList<CachedClientThreadModule> modules)
    {
        var instructions = new List<CachedClientThreadInstruction>();
        for (var offset = 0; offset < bytes.Count;)
        {
            var address = startAddress + offset;
            var opcode = bytes[offset];
            if (opcode == 0xE9 && offset + 4 < bytes.Count)
            {
                var relative = BitConverter.ToInt32(bytes.Skip(offset + 1).Take(4).ToArray());
                var target = address + 5 + relative;
                var owner = FindCachedClientThreadModule(modules, target);
                instructions.Add(NewCachedClientThreadInstruction(
                    address,
                    owner,
                    $"jmp {FormatHexAddress(target)}",
                    CachedClientThreadInstructionKind.Jump,
                    FormatAddressWithModule(target, owner),
                    owner?.ModuleName,
                    owner is null ? string.Empty : FormatModuleRva(target, owner)));
                offset += 5;
                continue;
            }

            if (opcode == 0xEB && offset + 1 < bytes.Count)
            {
                var relative = unchecked((sbyte)bytes[offset + 1]);
                var target = address + 2 + relative;
                var owner = FindCachedClientThreadModule(modules, target);
                instructions.Add(NewCachedClientThreadInstruction(
                    address,
                    owner,
                    $"jmp {FormatHexAddress(target)}",
                    CachedClientThreadInstructionKind.Jump,
                    FormatAddressWithModule(target, owner),
                    owner?.ModuleName,
                    owner is null ? string.Empty : FormatModuleRva(target, owner)));
                offset += 2;
                continue;
            }

            if (opcode == 0xE3 && offset + 1 < bytes.Count)
            {
                var relative = unchecked((sbyte)bytes[offset + 1]);
                var target = address + 2 + relative;
                var owner = FindCachedClientThreadModule(modules, target);
                instructions.Add(NewCachedClientThreadInstruction(
                    address,
                    owner,
                    $"jecxz {FormatHexAddress(target)}",
                    CachedClientThreadInstructionKind.Jump,
                    FormatAddressWithModule(target, owner),
                    owner?.ModuleName,
                    owner is null ? string.Empty : FormatModuleRva(target, owner)));
                offset += 2;
                continue;
            }

            if (opcode is >= 0x70 and <= 0x7F && offset + 1 < bytes.Count)
            {
                var relative = unchecked((sbyte)bytes[offset + 1]);
                var target = address + 2 + relative;
                var owner = FindCachedClientThreadModule(modules, target);
                instructions.Add(NewCachedClientThreadInstruction(
                    address,
                    owner,
                    $"{ShortConditionalJumpName(opcode)} {FormatHexAddress(target)}",
                    CachedClientThreadInstructionKind.Jump,
                    FormatAddressWithModule(target, owner),
                    owner?.ModuleName,
                    owner is null ? string.Empty : FormatModuleRva(target, owner)));
                offset += 2;
                continue;
            }

            if (opcode == 0x0F && offset + 5 < bytes.Count && bytes[offset + 1] is >= 0x80 and <= 0x8F)
            {
                var relative = BitConverter.ToInt32(bytes.Skip(offset + 2).Take(4).ToArray());
                var target = address + 6 + relative;
                var owner = FindCachedClientThreadModule(modules, target);
                instructions.Add(NewCachedClientThreadInstruction(
                    address,
                    owner,
                    $"{NearConditionalJumpName(bytes[offset + 1])} {FormatHexAddress(target)}",
                    CachedClientThreadInstructionKind.Jump,
                    FormatAddressWithModule(target, owner),
                    owner?.ModuleName,
                    owner is null ? string.Empty : FormatModuleRva(target, owner)));
                offset += 6;
                continue;
            }

            if (opcode is >= 0x50 and <= 0x57)
            {
                instructions.Add(NewCachedClientThreadInstruction(
                    address,
                    FindCachedClientThreadModule(modules, address),
                    $"push {Register32Name(opcode - 0x50)}"));
                offset++;
                continue;
            }

            if (opcode is >= 0x58 and <= 0x5F)
            {
                instructions.Add(NewCachedClientThreadInstruction(
                    address,
                    FindCachedClientThreadModule(modules, address),
                    $"pop {Register32Name(opcode - 0x58)}"));
                offset++;
                continue;
            }

            if (opcode is >= 0xB8 and <= 0xBF && offset + 4 < bytes.Count)
            {
                var immediate = BitConverter.ToUInt32(bytes.Skip(offset + 1).Take(4).ToArray());
                var immediateAddress = (long)immediate;
                var owner = FindCachedClientThreadModule(modules, immediate);
                var kind = owner is null
                    ? CachedClientThreadInstructionKind.Other
                    : CachedClientThreadInstructionKind.Immediate;
                instructions.Add(NewCachedClientThreadInstruction(
                    address,
                    FindCachedClientThreadModule(modules, address),
                    $"mov {Register32Name(opcode - 0xB8)}, {FormatHexAddress(immediate)}",
                    kind,
                    owner is null ? string.Empty : FormatAddressWithModule(immediate, owner),
                    owner?.ModuleName,
                    owner is null ? string.Empty : FormatModuleRva(immediate, owner),
                    FormatHexAddress(immediateAddress)));
                offset += 5;
                continue;
            }

            if (offset + 3 < bytes.Count && opcode == 0x89 && bytes[offset + 1] == 0x4C && bytes[offset + 2] == 0x24)
            {
                instructions.Add(NewCachedClientThreadInstruction(
                    address,
                    FindCachedClientThreadModule(modules, address),
                    $"mov dword ptr [esp + 0x{bytes[offset + 3]:X}], ecx"));
                offset += 4;
                continue;
            }

            if (offset + 2 < bytes.Count && opcode == 0x89 && bytes[offset + 1] == 0x0C && bytes[offset + 2] == 0x24)
            {
                instructions.Add(NewCachedClientThreadInstruction(
                    address,
                    FindCachedClientThreadModule(modules, address),
                    "mov dword ptr [esp], ecx"));
                offset += 3;
                continue;
            }

            if (offset + 2 < bytes.Count && opcode == 0x8B && bytes[offset + 1] == 0x14 && bytes[offset + 2] == 0x24)
            {
                instructions.Add(NewCachedClientThreadInstruction(
                    address,
                    FindCachedClientThreadModule(modules, address),
                    "mov edx, dword ptr [esp]"));
                offset += 3;
                continue;
            }

            if (offset + 5 < bytes.Count && opcode == 0x81 && bytes[offset + 1] == 0xC4)
            {
                var immediate = BitConverter.ToUInt32(bytes.Skip(offset + 2).Take(4).ToArray());
                instructions.Add(NewCachedClientThreadInstruction(
                    address,
                    FindCachedClientThreadModule(modules, address),
                    $"add esp, 0x{immediate:X}"));
                offset += 6;
                continue;
            }

            instructions.Add(NewCachedClientThreadInstruction(
                address,
                FindCachedClientThreadModule(modules, address),
                $"db 0x{opcode:X2}"));
            offset++;
        }

        return instructions;
    }

    static CachedClientThreadInstruction NewCachedClientThreadInstruction(
        long address,
        CachedClientThreadModule? owner,
        string text,
        CachedClientThreadInstructionKind kind = CachedClientThreadInstructionKind.Other,
        string targetText = "",
        string? targetModuleName = null,
        string targetModuleRva = "",
        string immediateAddress = "")
    {
        return new CachedClientThreadInstruction(
            FormatHexAddress(address),
            owner?.ModuleName ?? string.Empty,
            owner is null ? string.Empty : FormatModuleRva(address, owner),
            text,
            kind,
            targetText,
            targetModuleName ?? string.Empty,
            targetModuleRva,
            immediateAddress);
    }

    static CachedClientThreadModule? FindCachedClientThreadModule(IReadOnlyList<CachedClientThreadModule> modules, long address)
    {
        return modules.FirstOrDefault(module =>
            address >= module.BaseAddress &&
            address < module.BaseAddress + module.Size);
    }

    static string FormatAddressWithModule(long address, CachedClientThreadModule? module)
    {
        return module is null
            ? FormatHexAddress(address)
            : $"{FormatHexAddress(address)} ({module.ModuleName}+{FormatModuleRva(address, module)})";
    }

    static string FormatModuleRva(long address, CachedClientThreadModule module)
    {
        return $"0x{address - module.BaseAddress:X}";
    }

    static string FormatHexAddress(long address)
    {
        return $"0x{address:X}";
    }

    static string FormatEvidenceMapValues(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "<none>" : string.Join(", ", values);
    }

    static string FormatCachedClientRelocatedImmediateValues(IReadOnlyList<CachedClientRelocatedImmediate> values)
    {
        return values.Count == 0
            ? "<none>"
            : string.Join("; ", values.Select(FormatCachedClientRelocatedImmediate));
    }

    static string FormatCachedClientRelocatedImmediate(CachedClientRelocatedImmediate immediate)
    {
        return
            $"immediateAddress={immediate.ImmediateAddress} " +
            $"currentModuleCandidate={immediate.CurrentModuleCandidate} " +
            $"candidateRva={immediate.CandidateRva} " +
            $"currentAddress={immediate.CurrentAddress} " +
            $"currentAbsoluteAddress={immediate.CurrentAbsoluteAddress} " +
            $"staleModuleBase={immediate.StaleModuleBase} " +
            $"matchingModuleProbeBytes={immediate.MatchingModuleProbeBytes}";
    }

    static string FormatCachedClientRelocatedImmediateNextProbe(CachedClientRelocatedImmediate immediate)
    {
        return $"{immediate.ImmediateAddress} relocated to {immediate.CurrentAbsoluteAddress} ({immediate.CurrentAddress})";
    }

    static string Register32Name(int index)
    {
        return index switch
        {
            0 => "eax",
            1 => "ecx",
            2 => "edx",
            3 => "ebx",
            4 => "esp",
            5 => "ebp",
            6 => "esi",
            7 => "edi",
            _ => $"r{index}",
        };
    }

    static string ShortConditionalJumpName(byte opcode)
    {
        return NearConditionalJumpName((byte)(opcode + 0x10));
    }

    static string NearConditionalJumpName(byte opcode)
    {
        return opcode switch
        {
            0x80 => "jo",
            0x81 => "jno",
            0x82 => "jb",
            0x83 => "jae",
            0x84 => "je",
            0x85 => "jne",
            0x86 => "jbe",
            0x87 => "ja",
            0x88 => "js",
            0x89 => "jns",
            0x8A => "jp",
            0x8B => "jnp",
            0x8C => "jl",
            0x8D => "jge",
            0x8E => "jle",
            0x8F => "jg",
            _ => "jcc",
        };
    }

    static int? ReadOptionalInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return null;
    }

    static long? ReadOptionalInt64(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        return null;
    }

    static bool? ReadOptionalBool(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        return null;
    }

    static long? ReadOptionalHexInt64(JsonElement element, string name)
    {
        var value = ReadOptionalString(element, name);
        return string.IsNullOrWhiteSpace(value) ? null : ParseHexInt64(value, name);
    }

    static string ReadOptionalString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    }

    static long ParseHexInt64(string value, string name)
    {
        var text = value.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        if (!long.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
        {
            throw new FormatException($"{name} is not a hex address: {value}");
        }

        return result;
    }
}

internal sealed record CachedClientThreadEvidenceMapOptions(
    string JsonPath,
    string ProbeJsonPath,
    string ThreadModuleName,
    string LicenseModuleName);

internal sealed record CachedClientThreadEvidence(
    string Path,
    int ProcessId,
    IReadOnlyList<CachedClientThreadModule> Modules,
    CachedClientThreadRow Thread,
    IReadOnlyList<CachedClientThreadModule> ProbeModules,
    IReadOnlyList<CachedClientModuleMemoryProbeRow> ModuleMemoryProbes);

internal sealed record CachedClientThreadModuleProbeEvidence(
    string Path,
    IReadOnlyList<CachedClientThreadModule> Modules,
    IReadOnlyList<CachedClientModuleMemoryProbeRow> ModuleMemoryProbes);

internal sealed record CachedClientThreadModule(
    string ModuleName,
    string FileName,
    long BaseAddress,
    long Size);

internal sealed record CachedClientThreadRow(
    int ProcessId,
    int ThreadId,
    string ThreadState,
    string InstructionPointer,
    string InstructionModuleName,
    string InstructionBaseAddress,
    string InstructionRva,
    string InstructionBytes,
    string StackPointer,
    string Eax,
    string Ebx,
    string Ecx,
    string Edx,
    string Ebp,
    long TotalProcessorTimeMs);

internal sealed record CachedClientModuleMemoryProbeRow(
    string Name,
    string ModuleName,
    string Rva,
    string Address,
    int Length,
    bool Read,
    string Hex,
    string FailureReason);

internal sealed record CachedClientThreadEvidenceMapResult(
    bool Ready,
    IReadOnlyList<CachedClientThreadInstruction> Instructions,
    IReadOnlyList<string> JumpTargets,
    IReadOnlyList<string> LicenseImmediates,
    IReadOnlyList<CachedClientRelocatedImmediate> RelocatedLicenseImmediates,
    IReadOnlyList<CachedClientModuleMemoryProbeMap> ModuleProbeMaps,
    IReadOnlyList<string> ModuleProbeBranchTargets,
    string NextProbe);

internal sealed record CachedClientRelocatedImmediate(
    string ImmediateAddress,
    string CurrentModuleCandidate,
    string CandidateRva,
    string CurrentAddress,
    string CurrentAbsoluteAddress,
    string StaleModuleBase,
    bool MatchingModuleProbeBytes);

internal sealed record CachedClientModuleMemoryProbeMap(
    string Name,
    string ModuleName,
    string Rva,
    string Address,
    int ByteCount,
    IReadOnlyList<string> LeadingBranchTargets,
    IReadOnlyList<CachedClientThreadInstruction> Instructions);

internal sealed record CachedClientThreadInstruction(
    string Address,
    string ModuleName,
    string ModuleRva,
    string Text,
    CachedClientThreadInstructionKind Kind,
    string TargetText,
    string TargetModuleName,
    string TargetModuleRva,
    string ImmediateAddress);

internal enum CachedClientThreadInstructionKind
{
    Other,
    Jump,
    Immediate,
}
