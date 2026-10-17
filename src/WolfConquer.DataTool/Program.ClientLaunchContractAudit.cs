using System.Globalization;

internal static partial class Program
{
    private const string ClientLaunchContractAuditUsage =
        "Usage: WolfConquer.DataTool client-launch-contract-audit <oldClientRoot> <currentClientRoot> [--old-label label] [--current-label label] [--candidate relativePath] [...] [--workdir [label=]relativePath] [...] [--symbol-dll relativePath] [...] [--string text] [--no-default-strings] [--max-string-offsets N] [--max-import-dlls N]";

    private static readonly string[] ClientLaunchContractDefaultCandidates =
    [
        "play.exe",
        "._cache_play.exe",
        "._cache_Conquer.exe",
        "Env_DX9/._cache_Conquer.exe",
    ];

    private static readonly ClientLaunchContractWorkDir[] ClientLaunchContractDefaultWorkDirs =
    [
        new("root", "."),
        new("dx9", "Env_DX9"),
    ];

    private static readonly string[] ClientLaunchContractDefaultSymbolDlls =
    [
        "Env_DX9/DataThread.dll",
        "Env_DX9/license.dll",
    ];

    static int ClientLaunchContractAudit(string[] args)
    {
        ClientLaunchContractAuditOptions options;
        try
        {
            options = ResolveClientLaunchContractAuditOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ClientLaunchContractAuditUsage);
            return 1;
        }

        if (!Directory.Exists(options.OldRoot))
        {
            Console.Error.WriteLine($"Old client root not found: {options.OldRoot}");
            return 1;
        }

        if (!Directory.Exists(options.CurrentRoot))
        {
            Console.Error.WriteLine($"Current client root not found: {options.CurrentRoot}");
            return 1;
        }

        try
        {
            var report = BuildClientLaunchContractAuditReport(options);
            PrintClientLaunchContractAuditReport(report);
            return 0;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            Console.Error.WriteLine($"Client launch contract audit failed: {ex.Message}");
            return 1;
        }
    }

    private static ClientLaunchContractAuditOptions ResolveClientLaunchContractAuditOptions(string[] args)
    {
        if (args.Length < 3)
        {
            throw new ArgumentException("client-launch-contract-audit requires old and current client roots.");
        }

        var oldRoot = Path.GetFullPath(args[1]);
        var currentRoot = Path.GetFullPath(args[2]);
        var oldLabel = "old";
        var currentLabel = "7899";
        var candidates = new List<string>();
        var workDirs = new List<ClientLaunchContractWorkDir>();
        var symbolDlls = new List<string>();
        var strings = new List<string>();
        var includeDefaultStrings = true;
        var maxStringOffsets = ClientLaunchEvidenceDefaultMaxStringOffsets;
        var maxImportDlls = ClientLaunchEvidenceDefaultMaxImportDlls;

        for (var index = 3; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--old-label", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-launch-contract-audit --old-label requires a label.");
                }

                oldLabel = NormalizeClientLaunchContractLabel(args[++index], "--old-label");
                continue;
            }

            const string oldLabelPrefix = "--old-label=";
            if (argument.StartsWith(oldLabelPrefix, StringComparison.OrdinalIgnoreCase))
            {
                oldLabel = NormalizeClientLaunchContractLabel(argument[oldLabelPrefix.Length..], "--old-label");
                continue;
            }

            if (string.Equals(argument, "--current-label", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-launch-contract-audit --current-label requires a label.");
                }

                currentLabel = NormalizeClientLaunchContractLabel(args[++index], "--current-label");
                continue;
            }

            const string currentLabelPrefix = "--current-label=";
            if (argument.StartsWith(currentLabelPrefix, StringComparison.OrdinalIgnoreCase))
            {
                currentLabel = NormalizeClientLaunchContractLabel(argument[currentLabelPrefix.Length..], "--current-label");
                continue;
            }

            if (string.Equals(argument, "--candidate", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-launch-contract-audit --candidate requires a relative path.");
                }

                AddClientLaunchContractRelativePath(candidates, args[++index], "--candidate", allowDot: false);
                continue;
            }

            const string candidatePrefix = "--candidate=";
            if (argument.StartsWith(candidatePrefix, StringComparison.OrdinalIgnoreCase))
            {
                AddClientLaunchContractRelativePath(candidates, argument[candidatePrefix.Length..], "--candidate", allowDot: false);
                continue;
            }

            if (string.Equals(argument, "--workdir", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "--working-directory", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"client-launch-contract-audit {argument} requires [label=]relativePath.");
                }

                AddClientLaunchContractWorkDir(workDirs, args[++index]);
                continue;
            }

            const string workDirPrefix = "--workdir=";
            if (argument.StartsWith(workDirPrefix, StringComparison.OrdinalIgnoreCase))
            {
                AddClientLaunchContractWorkDir(workDirs, argument[workDirPrefix.Length..]);
                continue;
            }

            if (string.Equals(argument, "--symbol-dll", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-launch-contract-audit --symbol-dll requires a relative path.");
                }

                AddClientLaunchContractRelativePath(symbolDlls, args[++index], "--symbol-dll", allowDot: false);
                continue;
            }

            const string symbolDllPrefix = "--symbol-dll=";
            if (argument.StartsWith(symbolDllPrefix, StringComparison.OrdinalIgnoreCase))
            {
                AddClientLaunchContractRelativePath(symbolDlls, argument[symbolDllPrefix.Length..], "--symbol-dll", allowDot: false);
                continue;
            }

            if (string.Equals(argument, "--string", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "--term", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"client-launch-contract-audit {argument} requires text.");
                }

                AddClientLaunchContractString(strings, args[++index]);
                continue;
            }

            const string stringPrefix = "--string=";
            if (argument.StartsWith(stringPrefix, StringComparison.OrdinalIgnoreCase))
            {
                AddClientLaunchContractString(strings, argument[stringPrefix.Length..]);
                continue;
            }

            const string termPrefix = "--term=";
            if (argument.StartsWith(termPrefix, StringComparison.OrdinalIgnoreCase))
            {
                AddClientLaunchContractString(strings, argument[termPrefix.Length..]);
                continue;
            }

            if (string.Equals(argument, "--no-default-strings", StringComparison.OrdinalIgnoreCase))
            {
                includeDefaultStrings = false;
                continue;
            }

            if (string.Equals(argument, "--max-string-offsets", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-launch-contract-audit --max-string-offsets requires a count.");
                }

                maxStringOffsets = ParseClientLaunchEvidenceInteger(
                    args[++index],
                    "--max-string-offsets",
                    minimum: 0,
                    maximum: ClientLaunchEvidenceMaxStringOffsetsLimit);
                continue;
            }

            const string maxStringOffsetsPrefix = "--max-string-offsets=";
            if (argument.StartsWith(maxStringOffsetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                maxStringOffsets = ParseClientLaunchEvidenceInteger(
                    argument[maxStringOffsetsPrefix.Length..],
                    "--max-string-offsets",
                    minimum: 0,
                    maximum: ClientLaunchEvidenceMaxStringOffsetsLimit);
                continue;
            }

            if (string.Equals(argument, "--max-import-dlls", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-launch-contract-audit --max-import-dlls requires a count.");
                }

                maxImportDlls = ParseClientLaunchEvidenceInteger(
                    args[++index],
                    "--max-import-dlls",
                    minimum: 1,
                    maximum: ClientLaunchEvidenceMaxImportDllsLimit);
                continue;
            }

            const string maxImportDllsPrefix = "--max-import-dlls=";
            if (argument.StartsWith(maxImportDllsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                maxImportDlls = ParseClientLaunchEvidenceInteger(
                    argument[maxImportDllsPrefix.Length..],
                    "--max-import-dlls",
                    minimum: 1,
                    maximum: ClientLaunchEvidenceMaxImportDllsLimit);
                continue;
            }

            throw new ArgumentException($"Unknown client-launch-contract-audit option: {argument}");
        }

        if (candidates.Count == 0)
        {
            foreach (var relativePath in ClientLaunchContractDefaultCandidates)
            {
                AddClientLaunchContractRelativePath(candidates, relativePath, "--candidate", allowDot: false);
            }
        }

        if (workDirs.Count == 0)
        {
            workDirs.AddRange(ClientLaunchContractDefaultWorkDirs);
        }

        if (symbolDlls.Count == 0)
        {
            foreach (var relativePath in ClientLaunchContractDefaultSymbolDlls)
            {
                AddClientLaunchContractRelativePath(symbolDlls, relativePath, "--symbol-dll", allowDot: false);
            }
        }

        if (includeDefaultStrings)
        {
            foreach (var value in ClientLaunchEvidenceDefaultStrings)
            {
                AddClientLaunchContractString(strings, value);
            }

            foreach (var value in GeneratedBufferStringScanDefaultTerms)
            {
                AddClientLaunchContractString(strings, value);
            }
        }

        return new ClientLaunchContractAuditOptions(
            oldRoot,
            currentRoot,
            oldLabel,
            currentLabel,
            candidates,
            workDirs,
            symbolDlls,
            strings,
            maxStringOffsets,
            maxImportDlls);
    }

    private static string NormalizeClientLaunchContractLabel(string value, string optionName)
    {
        var label = value.Trim();
        if (label.Length == 0 || label.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"client-launch-contract-audit {optionName} must be non-empty and contain no whitespace.");
        }

        return label;
    }

    private static void AddClientLaunchContractWorkDir(List<ClientLaunchContractWorkDir> workDirs, string value)
    {
        var separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
        string label;
        string relativePath;
        if (separatorIndex > 0 && separatorIndex < value.Length - 1)
        {
            label = NormalizeClientLaunchContractLabel(value[..separatorIndex], "--workdir label");
            relativePath = NormalizeClientLaunchContractRelativePath(value[(separatorIndex + 1)..], "--workdir", allowDot: true);
        }
        else
        {
            relativePath = NormalizeClientLaunchContractRelativePath(value, "--workdir", allowDot: true);
            label = relativePath == "."
                ? "root"
                : NormalizeClientLaunchContractLabel(Path.GetFileName(relativePath), "--workdir label").ToLowerInvariant();
        }

        if (workDirs.Any(workDir => string.Equals(workDir.Label, label, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        workDirs.Add(new ClientLaunchContractWorkDir(label, relativePath));
    }

    private static void AddClientLaunchContractRelativePath(
        List<string> relativePaths,
        string value,
        string optionName,
        bool allowDot)
    {
        var relativePath = NormalizeClientLaunchContractRelativePath(value, optionName, allowDot);
        if (!relativePaths.Contains(relativePath, StringComparer.OrdinalIgnoreCase))
        {
            relativePaths.Add(relativePath);
        }
    }

    private static string NormalizeClientLaunchContractRelativePath(string value, string optionName, bool allowDot)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException($"client-launch-contract-audit {optionName} requires a non-empty relative path.");
        }

        if (Path.IsPathRooted(trimmed) || trimmed.IndexOf(Path.VolumeSeparatorChar, StringComparison.Ordinal) >= 0)
        {
            throw new ArgumentException($"client-launch-contract-audit {optionName} must be relative to each client root.");
        }

        var parts = trimmed
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Where(static part => part != ".")
            .ToArray();
        if (parts.Length == 0)
        {
            if (allowDot)
            {
                return ".";
            }

            throw new ArgumentException($"client-launch-contract-audit {optionName} requires a file path below each client root.");
        }

        if (parts.Any(static part => part == ".."))
        {
            throw new ArgumentException($"client-launch-contract-audit {optionName} cannot escape the client root.");
        }

        return Path.Combine(parts);
    }

    private static void AddClientLaunchContractString(List<string> strings, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("client-launch-contract-audit string terms must be non-empty.");
        }

        if (value.Any(c => c > sbyte.MaxValue))
        {
            throw new ArgumentException("client-launch-contract-audit string terms must be ASCII so ASCII and UTF-16LE scans are comparable.");
        }

        if (!strings.Contains(value, StringComparer.Ordinal))
        {
            strings.Add(value);
        }
    }

    private static ClientLaunchContractAuditReport BuildClientLaunchContractAuditReport(
        ClientLaunchContractAuditOptions options)
    {
        var oldRoot = new ClientLaunchContractRoot(options.OldLabel, options.OldRoot);
        var currentRoot = new ClientLaunchContractRoot(options.CurrentLabel, options.CurrentRoot);
        var candidates = options.CandidateRelativePaths
            .Select(relativePath => BuildClientLaunchContractCandidateReport(options, oldRoot, currentRoot, relativePath))
            .ToArray();
        var symbols = candidates
            .SelectMany(candidate => options.SymbolDllRelativePaths.Select(symbolDll =>
                BuildClientLaunchContractSymbolReport(options, oldRoot, currentRoot, candidate.RelativePath, symbolDll)))
            .ToArray();

        return new ClientLaunchContractAuditReport(options, candidates, symbols);
    }

    private static ClientLaunchContractCandidateReport BuildClientLaunchContractCandidateReport(
        ClientLaunchContractAuditOptions options,
        ClientLaunchContractRoot oldRoot,
        ClientLaunchContractRoot currentRoot,
        string relativePath)
    {
        var oldSide = BuildClientLaunchContractCandidateSide(options, oldRoot, relativePath);
        var currentSide = BuildClientLaunchContractCandidateSide(options, currentRoot, relativePath);
        var dependencyDifferences = BuildClientLaunchContractDependencyDifferences(oldSide.Dependency, currentSide.Dependency);
        var stringDifferences = BuildClientLaunchContractStringDifferences(oldSide.Evidence, currentSide.Evidence);

        return new ClientLaunchContractCandidateReport(
            relativePath,
            oldSide,
            currentSide,
            dependencyDifferences,
            stringDifferences);
    }

    private static ClientLaunchContractCandidateSide BuildClientLaunchContractCandidateSide(
        ClientLaunchContractAuditOptions options,
        ClientLaunchContractRoot root,
        string relativePath)
    {
        var path = ResolveClientLaunchContractPath(root.Path, relativePath);
        if (!File.Exists(path))
        {
            return new ClientLaunchContractCandidateSide(root.Label, path, Exists: false, Evidence: null, Dependency: null);
        }

        var evidenceOptions = new ClientLaunchEvidenceOptions(
            [new ClientLaunchEvidenceInput(root.Label, path)],
            options.Strings,
            options.MaxStringOffsets,
            options.MaxImportDlls);
        var evidence = BuildClientLaunchEvidenceReport(evidenceOptions.Inputs[0], evidenceOptions);
        var workDirs = options.WorkingDirectories
            .Select(workDir => new ClientLaunchDependencyWorkingDirectory(
                workDir.Label,
                ResolveClientLaunchContractPath(root.Path, workDir.RelativePath)))
            .ToArray();
        var dependency = BuildClientLaunchDependencyAuditReport(
            new ClientLaunchDependencyAuditOptions(
                path,
                workDirs,
                root.Path,
                options.MaxImportDlls,
                Recursive: false,
                MaxRecursiveDepth: 1));

        return new ClientLaunchContractCandidateSide(root.Label, path, Exists: true, evidence, dependency);
    }

    private static IReadOnlyList<ClientLaunchContractDependencyDifference> BuildClientLaunchContractDependencyDifferences(
        ClientLaunchDependencyAuditReport? oldDependency,
        ClientLaunchDependencyAuditReport? currentDependency)
    {
        var oldByName = oldDependency?.Imports.ToDictionary(import => import.Name, StringComparer.OrdinalIgnoreCase) ??
            new Dictionary<string, ClientLaunchDependencyImportReport>(StringComparer.OrdinalIgnoreCase);
        var currentByName = currentDependency?.Imports.ToDictionary(import => import.Name, StringComparer.OrdinalIgnoreCase) ??
            new Dictionary<string, ClientLaunchDependencyImportReport>(StringComparer.OrdinalIgnoreCase);
        var names = oldByName.Keys
            .Concat(currentByName.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);
        var differences = new List<ClientLaunchContractDependencyDifference>();

        foreach (var name in names)
        {
            oldByName.TryGetValue(name, out var oldImport);
            currentByName.TryGetValue(name, out var currentImport);
            var oldStatus = FormatClientLaunchContractDependencyStatus(oldImport);
            var currentStatus = FormatClientLaunchContractDependencyStatus(currentImport);
            if (!string.Equals(oldStatus, currentStatus, StringComparison.OrdinalIgnoreCase))
            {
                differences.Add(new ClientLaunchContractDependencyDifference(name, oldStatus, currentStatus));
            }
        }

        return differences;
    }

    private static string FormatClientLaunchContractDependencyStatus(ClientLaunchDependencyImportReport? report)
    {
        if (report is null)
        {
            return "not-imported";
        }

        if (report.IsSystemImport)
        {
            return "system";
        }

        if (string.Equals(report.Status, "missing-local", StringComparison.OrdinalIgnoreCase))
        {
            return "missing-local";
        }

        var locations = new List<string>();
        if (report.ExecutableDirectoryExists)
        {
            locations.Add("exe-dir");
        }

        locations.AddRange(report.WorkingDirectories
            .Where(static workDir => workDir.Exists)
            .Select(static workDir => "workdir:" + workDir.Label));

        if (report.ClientRootExists)
        {
            locations.Add("client-root");
        }

        return locations.Count == 0
            ? report.Status
            : report.Status + "/" + string.Join("+", locations);
    }

    private static IReadOnlyList<ClientLaunchContractStringDifference> BuildClientLaunchContractStringDifferences(
        ClientLaunchEvidenceReport? oldEvidence,
        ClientLaunchEvidenceReport? currentEvidence)
    {
        var oldHits = oldEvidence?.StringHits.ToDictionary(
            hit => hit.Term + "\t" + hit.EncodingName,
            StringComparer.Ordinal) ??
            new Dictionary<string, ClientLaunchEvidenceStringHit>(StringComparer.Ordinal);
        var currentHits = currentEvidence?.StringHits.ToDictionary(
            hit => hit.Term + "\t" + hit.EncodingName,
            StringComparer.Ordinal) ??
            new Dictionary<string, ClientLaunchEvidenceStringHit>(StringComparer.Ordinal);
        var keys = oldHits.Keys
            .Concat(currentHits.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        var differences = new List<ClientLaunchContractStringDifference>();

        foreach (var key in keys)
        {
            oldHits.TryGetValue(key, out var oldHit);
            currentHits.TryGetValue(key, out var currentHit);
            var oldPresent = oldHit?.Present ?? false;
            var currentPresent = currentHit?.Present ?? false;
            if (oldPresent == currentPresent)
            {
                continue;
            }

            var separatorIndex = key.IndexOf('\t', StringComparison.Ordinal);
            differences.Add(new ClientLaunchContractStringDifference(
                key[..separatorIndex],
                key[(separatorIndex + 1)..],
                oldPresent,
                currentPresent));
        }

        return differences;
    }

    private static ClientLaunchContractSymbolReport BuildClientLaunchContractSymbolReport(
        ClientLaunchContractAuditOptions options,
        ClientLaunchContractRoot oldRoot,
        ClientLaunchContractRoot currentRoot,
        string candidateRelativePath,
        string symbolDllRelativePath)
    {
        var importDllName = Path.GetFileName(symbolDllRelativePath);
        var oldSide = BuildClientLaunchContractSymbolSide(options, oldRoot, candidateRelativePath, symbolDllRelativePath, importDllName);
        var currentSide = BuildClientLaunchContractSymbolSide(options, currentRoot, candidateRelativePath, symbolDllRelativePath, importDllName);
        var missingNameDifferences = BuildClientLaunchContractMissingNameDifferences(oldSide.Symbols, currentSide.Symbols);

        return new ClientLaunchContractSymbolReport(
            candidateRelativePath,
            symbolDllRelativePath,
            importDllName,
            oldSide,
            currentSide,
            missingNameDifferences);
    }

    private static ClientLaunchContractSymbolSide BuildClientLaunchContractSymbolSide(
        ClientLaunchContractAuditOptions options,
        ClientLaunchContractRoot root,
        string candidateRelativePath,
        string symbolDllRelativePath,
        string importDllName)
    {
        var executablePath = ResolveClientLaunchContractPath(root.Path, candidateRelativePath);
        var dllPath = ResolveClientLaunchContractPath(root.Path, symbolDllRelativePath);
        var executableExists = File.Exists(executablePath);
        var dllExists = File.Exists(dllPath);
        if (!executableExists || !dllExists)
        {
            return new ClientLaunchContractSymbolSide(
                root.Label,
                executablePath,
                executableExists,
                dllPath,
                dllExists,
                Symbols: null);
        }

        var symbols = BuildClientPeSymbolAuditReport(
            new ClientPeSymbolAuditOptions(
                executablePath,
                importDllName,
                dllPath,
                Compare: true,
                EmitShimSummary: false,
                EmitDef: false,
                ProxyModuleName: string.Empty,
                MaxImports: Math.Min(options.MaxImportDlls, ClientPeSymbolAuditMaxImportsLimit),
                MaxExports: ClientPeSymbolAuditDefaultMaxExports));

        return new ClientLaunchContractSymbolSide(
            root.Label,
            executablePath,
            executableExists,
            dllPath,
            dllExists,
            symbols);
    }

    private static IReadOnlyList<ClientLaunchContractMissingNameDifference> BuildClientLaunchContractMissingNameDifferences(
        ClientPeSymbolAuditReport? oldSymbols,
        ClientPeSymbolAuditReport? currentSymbols)
    {
        var oldMissing = oldSymbols?.Compare.MissingNames.ToHashSet(StringComparer.Ordinal) ??
            new HashSet<string>(StringComparer.Ordinal);
        var currentMissing = currentSymbols?.Compare.MissingNames.ToHashSet(StringComparer.Ordinal) ??
            new HashSet<string>(StringComparer.Ordinal);
        var names = oldMissing
            .Concat(currentMissing)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        var differences = new List<ClientLaunchContractMissingNameDifference>();

        foreach (var name in names)
        {
            var oldHas = oldMissing.Contains(name);
            var currentHas = currentMissing.Contains(name);
            if (oldHas != currentHas)
            {
                differences.Add(new ClientLaunchContractMissingNameDifference(name, oldHas, currentHas));
            }
        }

        return differences;
    }

    private static string ResolveClientLaunchContractPath(string rootPath, string relativePath)
    {
        var fullRoot = Path.GetFullPath(rootPath);
        var combined = relativePath == "."
            ? fullRoot
            : Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        var relativeToRoot = Path.GetRelativePath(fullRoot, combined);
        if (relativeToRoot.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativeToRoot))
        {
            throw new ArgumentException($"Resolved launch-contract path escaped the client root: {relativePath}");
        }

        return combined;
    }

    private static void PrintClientLaunchContractAuditReport(ClientLaunchContractAuditReport report)
    {
        Console.WriteLine("Client launch contract audit:");
        Console.WriteLine("Mode: no-launch no-mutation bounded-root-compare");
        Console.WriteLine($"Old root: label={report.Options.OldLabel} path={report.Options.OldRoot}");
        Console.WriteLine($"Current root: label={report.Options.CurrentLabel} path={report.Options.CurrentRoot}");
        Console.WriteLine($"Candidates: {string.Join(", ", report.Options.CandidateRelativePaths)}");
        Console.WriteLine($"Working directories: {string.Join(", ", report.Options.WorkingDirectories.Select(workDir => workDir.Label + "=" + workDir.RelativePath))}");
        Console.WriteLine($"Symbol DLLs: {string.Join(", ", report.Options.SymbolDllRelativePaths)}");
        Console.WriteLine($"Strings: {(report.Options.Strings.Count == 0 ? "<none>" : string.Join(", ", report.Options.Strings))}");
        Console.WriteLine($"Max string offsets: {report.Options.MaxStringOffsets.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Max import DLLs: {report.Options.MaxImportDlls.ToString(CultureInfo.InvariantCulture)}");

        foreach (var candidate in report.Candidates)
        {
            PrintClientLaunchContractCandidate(candidate);
        }

        foreach (var symbol in report.Symbols)
        {
            if (!symbol.Old.DllExists && !symbol.Current.DllExists)
            {
                continue;
            }

            PrintClientLaunchContractSymbol(symbol);
        }
    }

    private static void PrintClientLaunchContractCandidate(ClientLaunchContractCandidateReport candidate)
    {
        Console.WriteLine($"Candidate: {candidate.RelativePath}");
        PrintClientLaunchContractCandidateSide(candidate.Old);
        PrintClientLaunchContractCandidateSide(candidate.Current);
        Console.WriteLine(
            $"  comparison: sameExists={FormatClientLaunchEvidenceBool(candidate.Old.Exists == candidate.Current.Exists)} " +
            $"sameSHA256={FormatClientLaunchEvidenceBool(SameClientLaunchContractEvidence(candidate, evidence => evidence.Sha256))} " +
            $"sameImportDllSet={FormatClientLaunchEvidenceBool(SameClientLaunchContractEvidence(candidate, evidence => NormalizeClientLaunchEvidenceSet(evidence.Pe.ImportDllNames)))} " +
            $"sameStringPresence={FormatClientLaunchEvidenceBool(SameClientLaunchContractStringPresence(candidate))}");
        Console.WriteLine($"  Dependency differences: {candidate.DependencyDifferences.Count.ToString(CultureInfo.InvariantCulture)}");
        foreach (var difference in candidate.DependencyDifferences)
        {
            Console.WriteLine($"    import={difference.Name} {candidate.Old.Label}={difference.OldStatus} {candidate.Current.Label}={difference.CurrentStatus}");
        }

        Console.WriteLine($"  String differences: {candidate.StringDifferences.Count.ToString(CultureInfo.InvariantCulture)}");
        foreach (var difference in candidate.StringDifferences)
        {
            Console.WriteLine(
                $"    term={difference.Term} encoding={difference.EncodingName} " +
                $"{candidate.Old.Label}={FormatClientLaunchEvidenceBool(difference.OldPresent)} " +
                $"{candidate.Current.Label}={FormatClientLaunchEvidenceBool(difference.CurrentPresent)}");
        }
    }

    private static void PrintClientLaunchContractCandidateSide(ClientLaunchContractCandidateSide side)
    {
        if (!side.Exists)
        {
            Console.WriteLine($"  {side.Label}: exists=False path={side.Path}");
            return;
        }

        var evidence = side.Evidence!;
        var dependency = side.Dependency!;
        Console.WriteLine(
            $"  {side.Label}: exists=True path={side.Path} size={evidence.SizeBytes.ToString(CultureInfo.InvariantCulture)} " +
            $"sha256={evidence.Sha256} pe={FormatClientLaunchEvidenceBool(evidence.Pe.IsPe)} " +
            $"importDllCount={evidence.Pe.ImportDllNames.Count.ToString(CultureInfo.InvariantCulture)} " +
            $"localMissing={dependency.LocalMissingCount.ToString(CultureInfo.InvariantCulture)} " +
            $"localResolved={dependency.LocalResolvedCount.ToString(CultureInfo.InvariantCulture)} " +
            $"systemImports={dependency.SystemImportCount.ToString(CultureInfo.InvariantCulture)}");
    }

    private static bool SameClientLaunchContractEvidence(
        ClientLaunchContractCandidateReport candidate,
        Func<ClientLaunchEvidenceReport, string> selector)
    {
        if (candidate.Old.Evidence is null || candidate.Current.Evidence is null)
        {
            return candidate.Old.Evidence is null && candidate.Current.Evidence is null;
        }

        return string.Equals(selector(candidate.Old.Evidence), selector(candidate.Current.Evidence), StringComparison.Ordinal);
    }

    private static bool SameClientLaunchContractStringPresence(ClientLaunchContractCandidateReport candidate)
    {
        if (candidate.Old.Evidence is null || candidate.Current.Evidence is null)
        {
            return candidate.Old.Evidence is null && candidate.Current.Evidence is null;
        }

        return string.Equals(
            NormalizeClientLaunchEvidenceStringPresence(candidate.Old.Evidence),
            NormalizeClientLaunchEvidenceStringPresence(candidate.Current.Evidence),
            StringComparison.Ordinal);
    }

    private static void PrintClientLaunchContractSymbol(ClientLaunchContractSymbolReport symbol)
    {
        Console.WriteLine($"Symbol contract: candidate={symbol.CandidateRelativePath} dll={symbol.SymbolDllRelativePath} importDll={symbol.ImportDllName}");
        PrintClientLaunchContractSymbolSide(symbol.Old);
        PrintClientLaunchContractSymbolSide(symbol.Current);
        Console.WriteLine($"  sameMissingNames={FormatClientLaunchEvidenceBool(symbol.MissingNameDifferences.Count == 0)}");
        Console.WriteLine($"  Missing named import differences: {symbol.MissingNameDifferences.Count.ToString(CultureInfo.InvariantCulture)}");
        foreach (var difference in symbol.MissingNameDifferences)
        {
            Console.WriteLine(
                $"    missing name={difference.Name} " +
                $"{symbol.Old.Label}={FormatClientLaunchEvidenceBool(difference.OldMissing)} " +
                $"{symbol.Current.Label}={FormatClientLaunchEvidenceBool(difference.CurrentMissing)}");
        }
    }

    private static void PrintClientLaunchContractSymbolSide(ClientLaunchContractSymbolSide side)
    {
        if (side.Symbols is null)
        {
            Console.WriteLine(
                $"  {side.Label}: exeExists={FormatClientLaunchEvidenceBool(side.ExecutableExists)} " +
                $"dllExists={FormatClientLaunchEvidenceBool(side.DllExists)} status=not-run");
            return;
        }

        Console.WriteLine(
            $"  {side.Label}: exeExists=True dllExists=True status={side.Symbols.Compare.Status} " +
            $"importPresent={FormatClientLaunchEvidenceBool(side.Symbols.Imports.IsPresent)} " +
            $"matchedNamedImports={side.Symbols.Compare.MatchedNamedImportCount.ToString(CultureInfo.InvariantCulture)} " +
            $"missingNamedImports={side.Symbols.Compare.MissingNamedImportCount.ToString(CultureInfo.InvariantCulture)} " +
            $"ordinalImports={side.Symbols.Compare.OrdinalImportCount.ToString(CultureInfo.InvariantCulture)} " +
            $"exports={side.Symbols.Exports.TotalExportCount.ToString(CultureInfo.InvariantCulture)}");
    }
}

internal sealed record ClientLaunchContractAuditOptions(
    string OldRoot,
    string CurrentRoot,
    string OldLabel,
    string CurrentLabel,
    IReadOnlyList<string> CandidateRelativePaths,
    IReadOnlyList<ClientLaunchContractWorkDir> WorkingDirectories,
    IReadOnlyList<string> SymbolDllRelativePaths,
    IReadOnlyList<string> Strings,
    int MaxStringOffsets,
    int MaxImportDlls);

internal sealed record ClientLaunchContractWorkDir(string Label, string RelativePath);

internal sealed record ClientLaunchContractRoot(string Label, string Path);

internal sealed record ClientLaunchContractAuditReport(
    ClientLaunchContractAuditOptions Options,
    IReadOnlyList<ClientLaunchContractCandidateReport> Candidates,
    IReadOnlyList<ClientLaunchContractSymbolReport> Symbols);

internal sealed record ClientLaunchContractCandidateReport(
    string RelativePath,
    ClientLaunchContractCandidateSide Old,
    ClientLaunchContractCandidateSide Current,
    IReadOnlyList<ClientLaunchContractDependencyDifference> DependencyDifferences,
    IReadOnlyList<ClientLaunchContractStringDifference> StringDifferences);

internal sealed record ClientLaunchContractCandidateSide(
    string Label,
    string Path,
    bool Exists,
    ClientLaunchEvidenceReport? Evidence,
    ClientLaunchDependencyAuditReport? Dependency);

internal sealed record ClientLaunchContractDependencyDifference(
    string Name,
    string OldStatus,
    string CurrentStatus);

internal sealed record ClientLaunchContractStringDifference(
    string Term,
    string EncodingName,
    bool OldPresent,
    bool CurrentPresent);

internal sealed record ClientLaunchContractSymbolReport(
    string CandidateRelativePath,
    string SymbolDllRelativePath,
    string ImportDllName,
    ClientLaunchContractSymbolSide Old,
    ClientLaunchContractSymbolSide Current,
    IReadOnlyList<ClientLaunchContractMissingNameDifference> MissingNameDifferences);

internal sealed record ClientLaunchContractSymbolSide(
    string Label,
    string ExecutablePath,
    bool ExecutableExists,
    string DllPath,
    bool DllExists,
    ClientPeSymbolAuditReport? Symbols);

internal sealed record ClientLaunchContractMissingNameDifference(
    string Name,
    bool OldMissing,
    bool CurrentMissing);
