using System.Globalization;

internal static partial class Program
{
    private const string ClientLaunchDependencyAuditUsage =
        "Usage: WolfConquer.DataTool client-launch-dependency-audit <exePath> --workdir label=path [--workdir label=path ...] [--client-root path] [--max-import-dlls N] [--recursive] [--max-recursive-depth N]";

    static int ClientLaunchDependencyAudit(string[] args)
    {
        ClientLaunchDependencyAuditOptions options;
        try
        {
            options = ResolveClientLaunchDependencyAuditOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ClientLaunchDependencyAuditUsage);
            return 1;
        }

        if (!File.Exists(options.ExecutablePath))
        {
            Console.Error.WriteLine($"Client executable not found: {options.ExecutablePath}");
            return 1;
        }

        foreach (var workDir in options.WorkingDirectories)
        {
            if (!Directory.Exists(workDir.Path))
            {
                Console.Error.WriteLine($"Working directory not found: label={workDir.Label} path={workDir.Path}");
                return 1;
            }
        }

        if (options.ClientRoot.Length > 0 && !Directory.Exists(options.ClientRoot))
        {
            Console.Error.WriteLine($"Client root not found: {options.ClientRoot}");
            return 1;
        }

        try
        {
            var report = BuildClientLaunchDependencyAuditReport(options);
            PrintClientLaunchDependencyAuditReport(report);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Console.Error.WriteLine($"Client launch dependency audit failed: {ex.Message}");
            return 1;
        }
    }

    private static ClientLaunchDependencyAuditOptions ResolveClientLaunchDependencyAuditOptions(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("client-launch-dependency-audit requires a client executable path.");
        }

        var executablePath = Path.GetFullPath(args[1]);
        var workingDirectories = new List<ClientLaunchDependencyWorkingDirectory>();
        var clientRoot = string.Empty;
        var maxImportDlls = ClientLaunchEvidenceMaxImportDllsLimit;
        var recursive = false;
        var maxRecursiveDepth = 3;

        for (var index = 2; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--workdir", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "--working-directory", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"{argument} requires label=path.");
                }

                workingDirectories.Add(ParseClientLaunchDependencyWorkingDirectory(args[++index]));
                continue;
            }

            const string workDirPrefix = "--workdir=";
            if (argument.StartsWith(workDirPrefix, StringComparison.OrdinalIgnoreCase))
            {
                workingDirectories.Add(ParseClientLaunchDependencyWorkingDirectory(argument[workDirPrefix.Length..]));
                continue;
            }

            if (string.Equals(argument, "--client-root", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-launch-dependency-audit --client-root requires a path.");
                }

                clientRoot = Path.GetFullPath(args[++index]);
                continue;
            }

            const string clientRootPrefix = "--client-root=";
            if (argument.StartsWith(clientRootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                clientRoot = Path.GetFullPath(argument[clientRootPrefix.Length..]);
                continue;
            }

            if (string.Equals(argument, "--max-import-dlls", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-launch-dependency-audit --max-import-dlls requires a count.");
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

            if (string.Equals(argument, "--recursive", StringComparison.OrdinalIgnoreCase))
            {
                recursive = true;
                continue;
            }

            if (string.Equals(argument, "--max-recursive-depth", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-launch-dependency-audit --max-recursive-depth requires a count.");
                }

                maxRecursiveDepth = ParseClientLaunchEvidenceInteger(
                    args[++index],
                    "--max-recursive-depth",
                    minimum: 1,
                    maximum: 8);
                continue;
            }

            const string maxRecursiveDepthPrefix = "--max-recursive-depth=";
            if (argument.StartsWith(maxRecursiveDepthPrefix, StringComparison.OrdinalIgnoreCase))
            {
                maxRecursiveDepth = ParseClientLaunchEvidenceInteger(
                    argument[maxRecursiveDepthPrefix.Length..],
                    "--max-recursive-depth",
                    minimum: 1,
                    maximum: 8);
                continue;
            }

            throw new ArgumentException($"Unknown client-launch-dependency-audit option: {argument}");
        }

        if (workingDirectories.Count == 0)
        {
            throw new ArgumentException("client-launch-dependency-audit requires at least one --workdir label=path.");
        }

        return new ClientLaunchDependencyAuditOptions(
            executablePath,
            workingDirectories,
            clientRoot,
            maxImportDlls,
            recursive,
            maxRecursiveDepth);
    }

    private static ClientLaunchDependencyWorkingDirectory ParseClientLaunchDependencyWorkingDirectory(string value)
    {
        var separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= value.Length - 1)
        {
            throw new ArgumentException("client-launch-dependency-audit workdir values must be label=path.");
        }

        var label = value[..separatorIndex].Trim();
        if (label.Length == 0 || label.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("client-launch-dependency-audit workdir labels must be non-empty and contain no whitespace.");
        }

        return new ClientLaunchDependencyWorkingDirectory(
            label,
            Path.GetFullPath(value[(separatorIndex + 1)..]));
    }

    private static ClientLaunchDependencyAuditReport BuildClientLaunchDependencyAuditReport(
        ClientLaunchDependencyAuditOptions options)
    {
        var bytes = File.ReadAllBytes(options.ExecutablePath);
        var pe = AnalyzeClientLaunchEvidencePe(bytes, options.MaxImportDlls);
        var executableDirectory = Path.GetFullPath(Path.GetDirectoryName(options.ExecutablePath) ?? ".");
        var imports = pe.ImportDllNames
            .Select(import => BuildClientLaunchDependencyImportReport(
                import,
                executableDirectory,
                options.WorkingDirectories,
                options.ClientRoot))
            .Select(import => import with
            {
                Symbols = BuildClientLaunchDependencySymbolReport(
                    options.ExecutablePath,
                    import.Name,
                    import.IsSystemImport,
                    import.ResolvedLocalPath,
                    options.MaxImportDlls)
            })
            .OrderBy(report => report.IsSystemImport)
            .ThenBy(report => report.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var recursiveImports = options.Recursive && pe.IsPe
            ? BuildClientLaunchDependencyRecursiveImportReports(
                imports,
                options.WorkingDirectories,
                options.ClientRoot,
                options.MaxImportDlls,
                options.MaxRecursiveDepth)
            : Array.Empty<ClientLaunchDependencyRecursiveImportReport>();

        return new ClientLaunchDependencyAuditReport(
            options.ExecutablePath,
            executableDirectory,
            options.ClientRoot,
            options.WorkingDirectories,
            pe,
            imports,
            options.Recursive,
            options.MaxRecursiveDepth,
            recursiveImports);
    }

    private static ClientLaunchDependencyImportReport BuildClientLaunchDependencyImportReport(
        string importName,
        string executableDirectory,
        IReadOnlyList<ClientLaunchDependencyWorkingDirectory> workingDirectories,
        string clientRoot)
    {
        var isSystem = IsClientLaunchDependencySystemImport(importName);
        var executableDirectoryPath = Path.Combine(executableDirectory, importName);
        var workingDirectoryReports = workingDirectories
            .Select(workDir => new ClientLaunchDependencyDirectoryPresence(
                workDir.Label,
                Path.Combine(workDir.Path, importName),
                File.Exists(Path.Combine(workDir.Path, importName))))
            .ToArray();
        var clientRootPath = clientRoot.Length == 0 ? string.Empty : Path.Combine(clientRoot, importName);

        var foundLocal =
            File.Exists(executableDirectoryPath) ||
            workingDirectoryReports.Any(static report => report.Exists) ||
            (clientRootPath.Length > 0 && File.Exists(clientRootPath));
        var status = isSystem
            ? "system"
            : foundLocal ? "resolved-local" : "missing-local";

        return new ClientLaunchDependencyImportReport(
            importName,
            isSystem,
            status,
            executableDirectoryPath,
            File.Exists(executableDirectoryPath),
            workingDirectoryReports,
            clientRootPath,
            clientRootPath.Length > 0 && File.Exists(clientRootPath),
            ResolveClientLaunchDependencyLocalPath(
                executableDirectoryPath,
                workingDirectoryReports,
                clientRootPath));
    }

    private static string ResolveClientLaunchDependencyLocalPath(
        string executableDirectoryPath,
        IReadOnlyList<ClientLaunchDependencyDirectoryPresence> workingDirectories,
        string clientRootPath)
    {
        if (File.Exists(executableDirectoryPath))
        {
            return executableDirectoryPath;
        }

        foreach (var workDir in workingDirectories)
        {
            if (workDir.Exists)
            {
                return workDir.Path;
            }
        }

        if (clientRootPath.Length > 0 && File.Exists(clientRootPath))
        {
            return clientRootPath;
        }

        return string.Empty;
    }

    private static IReadOnlyList<ClientLaunchDependencyRecursiveImportReport> BuildClientLaunchDependencyRecursiveImportReports(
        IReadOnlyList<ClientLaunchDependencyImportReport> rootImports,
        IReadOnlyList<ClientLaunchDependencyWorkingDirectory> workingDirectories,
        string clientRoot,
        int maxImportDlls,
        int maxDepth)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<ClientLaunchDependencyRecursiveQueueItem>();
        var reports = new List<ClientLaunchDependencyRecursiveImportReport>();

        foreach (var import in rootImports)
        {
            if (import.ResolvedLocalPath.Length == 0)
            {
                continue;
            }

            queue.Enqueue(new ClientLaunchDependencyRecursiveQueueItem(
                import.ResolvedLocalPath,
                Depth: 1,
                Parent: "<root>",
                EdgeName: import.Name));
        }

        while (queue.Count > 0)
        {
            var item = queue.Dequeue();
            var fullPath = Path.GetFullPath(item.Path);
            if (!visited.Add(fullPath) || !File.Exists(fullPath))
            {
                continue;
            }

            var bytes = File.ReadAllBytes(fullPath);
            var pe = AnalyzeClientLaunchEvidencePe(bytes, maxImportDlls);
            if (!pe.IsPe)
            {
                reports.Add(new ClientLaunchDependencyRecursiveImportReport(
                    item.Depth,
                    item.Parent,
                    item.EdgeName,
                    fullPath,
                    "<not-pe>",
                    false,
                    "not-pe",
                    string.Empty,
                    ClientLaunchDependencySymbolReport.Disabled));
                continue;
            }

            var executableDirectory = Path.GetFullPath(Path.GetDirectoryName(fullPath) ?? ".");
            foreach (var importName in pe.ImportDllNames.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase))
            {
                var importReport = BuildClientLaunchDependencyImportReport(
                    importName,
                    executableDirectory,
                    workingDirectories,
                    clientRoot);
                reports.Add(new ClientLaunchDependencyRecursiveImportReport(
                    item.Depth,
                    item.Parent,
                    item.EdgeName,
                    fullPath,
                    importReport.Name,
                    importReport.IsSystemImport,
                    importReport.Status,
                    importReport.ResolvedLocalPath,
                    BuildClientLaunchDependencySymbolReport(
                        fullPath,
                        importReport.Name,
                        importReport.IsSystemImport,
                        importReport.ResolvedLocalPath,
                        maxImportDlls)));

                if (!importReport.IsSystemImport &&
                    importReport.ResolvedLocalPath.Length > 0 &&
                    item.Depth < maxDepth)
                {
                    queue.Enqueue(new ClientLaunchDependencyRecursiveQueueItem(
                        importReport.ResolvedLocalPath,
                        item.Depth + 1,
                        fullPath,
                        importReport.Name));
                }
            }
        }

        return reports
            .OrderBy(report => report.Depth)
            .ThenBy(report => report.OwnerPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(report => report.ImportName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ClientLaunchDependencySymbolReport BuildClientLaunchDependencySymbolReport(
        string ownerPath,
        string importName,
        bool isSystemImport,
        string resolvedLocalPath,
        int maxImports)
    {
        if (isSystemImport || resolvedLocalPath.Length == 0)
        {
            return ClientLaunchDependencySymbolReport.Disabled;
        }

        try
        {
            var ownerBytes = File.ReadAllBytes(ownerPath);
            var ownerPe = ReadClientPeSymbolHeaders(ownerBytes);
            if (!ownerPe.IsPe)
            {
                return ClientLaunchDependencySymbolReport.Skipped(ownerPe.Reason);
            }

            var imports = ReadClientPeSymbolImportsFromDll(ownerBytes, ownerPe, importName, maxImports);
            if (!imports.IsPresent)
            {
                return ClientLaunchDependencySymbolReport.Skipped("import-dll-not-present");
            }

            var resolvedBytes = File.ReadAllBytes(resolvedLocalPath);
            var resolvedPe = ReadClientPeSymbolHeaders(resolvedBytes);
            if (!resolvedPe.IsPe)
            {
                return ClientLaunchDependencySymbolReport.Skipped("resolved-dll-not-pe");
            }

            var exports = ReadClientPeSymbolExports(
                resolvedBytes,
                resolvedPe,
                ClientPeSymbolAuditDefaultMaxExports);
            var compare = BuildClientPeSymbolCompareReport(
                enabled: true,
                ownerPe,
                resolvedPe,
                imports,
                exports);
            var ordinalCoverage = BuildClientLaunchDependencyOrdinalCoverage(
                imports,
                resolvedBytes,
                resolvedPe);
            return ClientLaunchDependencySymbolReport.FromCompare(compare, ordinalCoverage);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or OverflowException)
        {
            return ClientLaunchDependencySymbolReport.Skipped(ex.Message);
        }
    }

    private static ClientLaunchDependencyOrdinalImportCoverage BuildClientLaunchDependencyOrdinalCoverage(
        ClientPeSymbolImportSet imports,
        byte[] resolvedBytes,
        ClientPeSymbolPeHeaders resolvedPe)
    {
        var ordinalImports = imports.Imports
            .Where(static import => import.IsOrdinal && import.Ordinal.HasValue)
            .Select(static import => import.Ordinal!.Value)
            .ToArray();
        if (ordinalImports.Length == 0)
        {
            return ClientLaunchDependencyOrdinalImportCoverage.Empty;
        }

        var exports = ReadClientLaunchDependencyOrdinalExports(resolvedBytes, resolvedPe);
        var matchedCount = 0;
        var ordinalOnlyMatchedCount = 0;
        var missingOrdinals = new List<ushort>();
        foreach (var ordinal in ordinalImports)
        {
            var ordinalValue = (uint)ordinal;
            if (!exports.ExportedOrdinals.Contains(ordinalValue))
            {
                missingOrdinals.Add(ordinal);
                continue;
            }

            matchedCount++;
            if (!exports.NamedOrdinals.Contains(ordinalValue))
            {
                ordinalOnlyMatchedCount++;
            }
        }

        return new ClientLaunchDependencyOrdinalImportCoverage(
            matchedCount,
            ordinalOnlyMatchedCount,
            missingOrdinals.Count,
            missingOrdinals
                .Distinct()
                .Order()
                .ToArray());
    }

    private static ClientLaunchDependencyOrdinalExportSet ReadClientLaunchDependencyOrdinalExports(
        byte[] bytes,
        ClientPeSymbolPeHeaders pe)
    {
        if (pe.ExportTableRva == 0)
        {
            return ClientLaunchDependencyOrdinalExportSet.Empty;
        }

        var data = bytes.AsSpan();
        if (!TryMapClientLaunchPeRvaToOffset(data, pe.Sections, pe.SizeOfHeaders, pe.ExportTableRva, out var exportOffset))
        {
            throw new InvalidDataException($"export-table-rva-unmapped-{FormatScanHex(pe.ExportTableRva)}");
        }

        EnsureClientLaunchPeRange(data, exportOffset, 40, "export directory");
        var ordinalBase = ReadClientLaunchPeUInt32(data, exportOffset + 16);
        var functionCount = ReadClientLaunchPeUInt32(data, exportOffset + 20);
        var nameCount = ReadClientLaunchPeUInt32(data, exportOffset + 24);
        var functionTableRva = ReadClientLaunchPeUInt32(data, exportOffset + 28);
        var ordinalTableRva = ReadClientLaunchPeUInt32(data, exportOffset + 36);
        if (functionCount > int.MaxValue || nameCount > int.MaxValue)
        {
            throw new InvalidDataException("export-table-count-out-of-range");
        }

        var functionCountInt = checked((int)functionCount);
        var nameCountInt = checked((int)nameCount);
        var exportedOrdinals = new HashSet<uint>();
        if (functionCountInt > 0)
        {
            if (!TryMapClientLaunchPeRvaToOffset(data, pe.Sections, pe.SizeOfHeaders, functionTableRva, out var functionTableOffset))
            {
                throw new InvalidDataException($"export-function-table-rva-unmapped-{FormatScanHex(functionTableRva)}");
            }

            for (var index = 0; index < functionCountInt; index++)
            {
                var functionRva = ReadClientLaunchPeUInt32(data, checked(functionTableOffset + index * sizeof(uint)));
                if (functionRva != 0)
                {
                    exportedOrdinals.Add(checked(ordinalBase + (uint)index));
                }
            }
        }

        var namedOrdinals = new HashSet<uint>();
        if (nameCountInt > 0)
        {
            if (!TryMapClientLaunchPeRvaToOffset(data, pe.Sections, pe.SizeOfHeaders, ordinalTableRva, out var ordinalTableOffset))
            {
                throw new InvalidDataException($"export-ordinal-table-rva-unmapped-{FormatScanHex(ordinalTableRva)}");
            }

            for (var index = 0; index < nameCountInt; index++)
            {
                var ordinalIndex = ReadClientLaunchPeUInt16(data, checked(ordinalTableOffset + index * sizeof(ushort)));
                if (ordinalIndex >= functionCount)
                {
                    throw new InvalidDataException($"export-ordinal-index-out-of-range-{ordinalIndex.ToString(CultureInfo.InvariantCulture)}");
                }

                var ordinal = checked(ordinalBase + ordinalIndex);
                if (exportedOrdinals.Contains(ordinal))
                {
                    namedOrdinals.Add(ordinal);
                }
            }
        }

        return new ClientLaunchDependencyOrdinalExportSet(exportedOrdinals, namedOrdinals);
    }

    private static bool IsClientLaunchDependencySystemImport(string importName)
    {
        if (importName.StartsWith("api-ms-win-", StringComparison.OrdinalIgnoreCase) ||
            importName.StartsWith("ext-ms-win-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ClientLaunchDependencySystemDlls.Contains(importName) ||
            IsClientLaunchDependencyInstalledWindowsSystemDll(importName);
    }

    private static bool IsClientLaunchDependencyInstalledWindowsSystemDll(string importName)
    {
        if (!IsClientLaunchDependencyPlainWindowsImportFileName(importName))
        {
            return false;
        }

        foreach (var systemDirectory in GetClientLaunchDependencyWindowsSystemDirectories())
        {
            if (File.Exists(Path.Combine(systemDirectory, importName)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsClientLaunchDependencyPlainWindowsImportFileName(string importName)
    {
        if (importName.Length == 0 ||
            importName.IndexOf('\\') >= 0 ||
            importName.IndexOf('/') >= 0 ||
            !string.Equals(Path.GetFileName(importName), importName, StringComparison.Ordinal) ||
            importName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        var extension = Path.GetExtension(importName);
        return string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".drv", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetClientLaunchDependencyWindowsSystemDirectories()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddClientLaunchDependencyWindowsSystemDirectory(
            paths,
            Environment.GetFolderPath(Environment.SpecialFolder.System));

        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (windowsDirectory.Length > 0)
        {
            AddClientLaunchDependencyWindowsSystemDirectory(paths, Path.Combine(windowsDirectory, "System32"));
            AddClientLaunchDependencyWindowsSystemDirectory(paths, Path.Combine(windowsDirectory, "SysWOW64"));
        }

        return paths.ToArray();
    }

    private static void AddClientLaunchDependencyWindowsSystemDirectory(
        HashSet<string> paths,
        string path)
    {
        if (path.Length > 0 && Directory.Exists(path))
        {
            paths.Add(Path.GetFullPath(path));
        }
    }

    private static readonly HashSet<string> ClientLaunchDependencySystemDlls = new(StringComparer.OrdinalIgnoreCase)
    {
        "advapi32.dll",
        "comctl32.dll",
        "comdlg32.dll",
        "crypt32.dll",
        "d3d8.dll",
        "d3d9.dll",
        "dinput8.dll",
        "dnsapi.dll",
        "gdi32.dll",
        "imm32.dll",
        "kernel32.dll",
        "netapi32.dll",
        "ole32.dll",
        "oleaut32.dll",
        "psapi.dll",
        "rpcrt4.dll",
        "shell32.dll",
        "shlwapi.dll",
        "usp10.dll",
        "user32.dll",
        "uxtheme.dll",
        "wininet.dll",
        "winmm.dll",
        "winspool.drv",
        "ws2_32.dll",
        "dbghelp.dll",
    };

    private static void PrintClientLaunchDependencyAuditReport(ClientLaunchDependencyAuditReport report)
    {
        Console.WriteLine("Client launch dependency audit:");
        Console.WriteLine("Mode: no-launch import-resolution");
        Console.WriteLine($"Executable: {report.ExecutablePath}");
        Console.WriteLine($"Executable directory: {report.ExecutableDirectory}");
        Console.WriteLine($"Client root: {(report.ClientRoot.Length == 0 ? "<none>" : report.ClientRoot)}");
        Console.WriteLine($"Working directories: {report.WorkingDirectories.Count.ToString(CultureInfo.InvariantCulture)}");
        foreach (var workDir in report.WorkingDirectories)
        {
            Console.WriteLine($"  {workDir.Label}={workDir.Path}");
        }

        if (!report.Pe.IsPe)
        {
            Console.WriteLine($"PE: valid=False reason={report.Pe.Reason}");
            return;
        }

        Console.WriteLine(
            $"PE: valid=True machine={report.Pe.Machine} subsystem={report.Pe.Subsystem} " +
            $"importDllCount={report.Imports.Count.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Local unresolved imports: {report.LocalMissingCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Local resolved imports: {report.LocalResolvedCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"System imports: {report.SystemImportCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"Recursive local dependency audit: enabled={FormatClientLaunchEvidenceBool(report.Recursive)} " +
            $"maxDepth={report.MaxRecursiveDepth.ToString(CultureInfo.InvariantCulture)}");
        if (report.Recursive)
        {
            Console.WriteLine($"Recursive local files visited: {report.RecursiveVisitedFileCount.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Recursive local unresolved imports: {report.RecursiveLocalMissingCount.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Recursive local symbol mismatches: {report.RecursiveLocalSymbolMismatchCount.ToString(CultureInfo.InvariantCulture)}");
        }

        foreach (var import in report.Imports)
        {
            Console.WriteLine(
                $"  {import.Name}: status={import.Status} system={FormatClientLaunchEvidenceBool(import.IsSystemImport)} " +
                $"exeDir={FormatClientLaunchEvidenceBool(import.ExecutableDirectoryExists)} " +
                $"clientRoot={FormatClientLaunchEvidenceBool(import.ClientRootExists)} " +
                $"workdirs={FormatClientLaunchDependencyWorkDirs(import.WorkingDirectories)}" +
                FormatClientLaunchDependencySymbolSuffix(import.Symbols));
        }

        foreach (var recursiveImport in report.RecursiveImports)
        {
            Console.WriteLine(
                $"  recursive depth={recursiveImport.Depth.ToString(CultureInfo.InvariantCulture)} " +
                $"owner={recursiveImport.OwnerPath} import={recursiveImport.ImportName} " +
                $"status={recursiveImport.Status} system={FormatClientLaunchEvidenceBool(recursiveImport.IsSystemImport)} " +
                $"resolved={(recursiveImport.ResolvedLocalPath.Length == 0 ? "<none>" : recursiveImport.ResolvedLocalPath)}" +
                FormatClientLaunchDependencySymbolSuffix(recursiveImport.Symbols));
        }
    }

    private static string FormatClientLaunchDependencyWorkDirs(
        IReadOnlyList<ClientLaunchDependencyDirectoryPresence> workingDirectories)
    {
        if (workingDirectories.Count == 0)
        {
            return "<none>";
        }

        return string.Join(
            ",",
            workingDirectories.Select(report =>
                report.Label + ":" + FormatClientLaunchEvidenceBool(report.Exists)));
    }

    private static string FormatClientLaunchDependencySymbolSuffix(ClientLaunchDependencySymbolReport report)
    {
        if (!report.Enabled)
        {
            return string.Empty;
        }

        var suffix =
            $" symbols={report.Status} matched={report.MatchedNamedImportCount.ToString(CultureInfo.InvariantCulture)}" +
            $" missing={report.MissingNamedImportCount.ToString(CultureInfo.InvariantCulture)}" +
            $" ordinal={report.OrdinalImportCount.ToString(CultureInfo.InvariantCulture)}";
        if (report.OrdinalImportCount > 0)
        {
            suffix +=
                $" ordinalMatched={report.MatchedOrdinalImportCount.ToString(CultureInfo.InvariantCulture)}" +
                $" ordinalOnlyMatched={report.OrdinalOnlyMatchedImportCount.ToString(CultureInfo.InvariantCulture)}" +
                $" ordinalMissing={report.MissingOrdinalImportCount.ToString(CultureInfo.InvariantCulture)}";
        }

        if (report.MissingNames.Count > 0)
        {
            suffix += " missingNames=" + string.Join(",", report.MissingNames.Take(8));
        }

        if (report.MissingOrdinals.Count > 0)
        {
            suffix += " missingOrdinals=" + string.Join(
                ",",
                report.MissingOrdinals
                    .Take(8)
                    .Select(static ordinal => ordinal.ToString(CultureInfo.InvariantCulture)));
        }

        return suffix;
    }
}

internal sealed record ClientLaunchDependencyAuditOptions(
    string ExecutablePath,
    IReadOnlyList<ClientLaunchDependencyWorkingDirectory> WorkingDirectories,
    string ClientRoot,
    int MaxImportDlls,
    bool Recursive,
    int MaxRecursiveDepth);

internal sealed record ClientLaunchDependencyWorkingDirectory(string Label, string Path);

internal sealed record ClientLaunchDependencyAuditReport(
    string ExecutablePath,
    string ExecutableDirectory,
    string ClientRoot,
    IReadOnlyList<ClientLaunchDependencyWorkingDirectory> WorkingDirectories,
    ClientLaunchEvidencePeInfo Pe,
    IReadOnlyList<ClientLaunchDependencyImportReport> Imports,
    bool Recursive,
    int MaxRecursiveDepth,
    IReadOnlyList<ClientLaunchDependencyRecursiveImportReport> RecursiveImports)
{
    public int LocalMissingCount => Imports.Count(static import => !import.IsSystemImport && import.Status == "missing-local");

    public int LocalResolvedCount => Imports.Count(static import => !import.IsSystemImport && import.Status == "resolved-local");

    public int SystemImportCount => Imports.Count(static import => import.IsSystemImport);

    public int RecursiveVisitedFileCount => RecursiveImports
        .Select(static import => import.OwnerPath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int RecursiveLocalMissingCount => RecursiveImports.Count(static import => !import.IsSystemImport && import.Status == "missing-local");

    public int RecursiveLocalSymbolMismatchCount => RecursiveImports.Count(static import =>
        import.Symbols.MissingNamedImportCount > 0 ||
        import.Symbols.MissingOrdinalImportCount > 0);
}

internal sealed record ClientLaunchDependencyImportReport(
    string Name,
    bool IsSystemImport,
    string Status,
    string ExecutableDirectoryPath,
    bool ExecutableDirectoryExists,
    IReadOnlyList<ClientLaunchDependencyDirectoryPresence> WorkingDirectories,
    string ClientRootPath,
    bool ClientRootExists,
    string ResolvedLocalPath,
    ClientLaunchDependencySymbolReport Symbols)
{
    public ClientLaunchDependencyImportReport(
        string name,
        bool isSystemImport,
        string status,
        string executableDirectoryPath,
        bool executableDirectoryExists,
        IReadOnlyList<ClientLaunchDependencyDirectoryPresence> workingDirectories,
        string clientRootPath,
        bool clientRootExists,
        string resolvedLocalPath)
        : this(
            name,
            isSystemImport,
            status,
            executableDirectoryPath,
            executableDirectoryExists,
            workingDirectories,
            clientRootPath,
            clientRootExists,
            resolvedLocalPath,
            ClientLaunchDependencySymbolReport.Disabled)
    {
    }
}

internal sealed record ClientLaunchDependencyDirectoryPresence(
    string Label,
    string Path,
    bool Exists);

internal sealed record ClientLaunchDependencyRecursiveQueueItem(
    string Path,
    int Depth,
    string Parent,
    string EdgeName);

internal sealed record ClientLaunchDependencyRecursiveImportReport(
    int Depth,
    string Parent,
    string EdgeName,
    string OwnerPath,
    string ImportName,
    bool IsSystemImport,
    string Status,
    string ResolvedLocalPath,
    ClientLaunchDependencySymbolReport Symbols);

internal sealed record ClientLaunchDependencySymbolReport(
    bool Enabled,
    string Status,
    string Reason,
    int MatchedNamedImportCount,
    int MissingNamedImportCount,
    int OrdinalImportCount,
    int MatchedOrdinalImportCount,
    int OrdinalOnlyMatchedImportCount,
    int MissingOrdinalImportCount,
    IReadOnlyList<string> MissingNames,
    IReadOnlyList<ushort> MissingOrdinals)
{
    public static readonly ClientLaunchDependencySymbolReport Disabled = new(
        Enabled: false,
        Status: "disabled",
        Reason: string.Empty,
        MatchedNamedImportCount: 0,
        MissingNamedImportCount: 0,
        OrdinalImportCount: 0,
        MatchedOrdinalImportCount: 0,
        OrdinalOnlyMatchedImportCount: 0,
        MissingOrdinalImportCount: 0,
        MissingNames: [],
        MissingOrdinals: []);

    public static ClientLaunchDependencySymbolReport Skipped(string reason)
    {
        return new ClientLaunchDependencySymbolReport(
            Enabled: true,
            Status: "skipped",
            Reason: reason,
            MatchedNamedImportCount: 0,
            MissingNamedImportCount: 0,
            OrdinalImportCount: 0,
            MatchedOrdinalImportCount: 0,
            OrdinalOnlyMatchedImportCount: 0,
            MissingOrdinalImportCount: 0,
            MissingNames: [],
            MissingOrdinals: []);
    }

    public static ClientLaunchDependencySymbolReport FromCompare(
        ClientPeSymbolCompareReport compare,
        ClientLaunchDependencyOrdinalImportCoverage ordinalCoverage)
    {
        var status = compare.Status;
        if (compare.Status == "ok" &&
            compare.MissingNamedImportCount > 0 &&
            ordinalCoverage.MissingOrdinalImportCount > 0)
        {
            status = "missing-imports";
        }
        else if (compare.Status == "ok" && compare.MissingNamedImportCount > 0)
        {
            status = "missing-named-imports";
        }
        else if (compare.Status == "ok" && ordinalCoverage.MissingOrdinalImportCount > 0)
        {
            status = "missing-ordinal-imports";
        }

        return new ClientLaunchDependencySymbolReport(
            Enabled: compare.Enabled,
            Status: status,
            Reason: compare.Reason,
            MatchedNamedImportCount: compare.MatchedNamedImportCount,
            MissingNamedImportCount: compare.MissingNamedImportCount,
            OrdinalImportCount: compare.OrdinalImportCount,
            MatchedOrdinalImportCount: ordinalCoverage.MatchedOrdinalImportCount,
            OrdinalOnlyMatchedImportCount: ordinalCoverage.OrdinalOnlyMatchedImportCount,
            MissingOrdinalImportCount: ordinalCoverage.MissingOrdinalImportCount,
            MissingNames: compare.MissingNames,
            MissingOrdinals: ordinalCoverage.MissingOrdinals);
    }
}

internal sealed record ClientLaunchDependencyOrdinalImportCoverage(
    int MatchedOrdinalImportCount,
    int OrdinalOnlyMatchedImportCount,
    int MissingOrdinalImportCount,
    IReadOnlyList<ushort> MissingOrdinals)
{
    public static readonly ClientLaunchDependencyOrdinalImportCoverage Empty = new(
        MatchedOrdinalImportCount: 0,
        OrdinalOnlyMatchedImportCount: 0,
        MissingOrdinalImportCount: 0,
        MissingOrdinals: []);
}

internal sealed record ClientLaunchDependencyOrdinalExportSet(
    IReadOnlySet<uint> ExportedOrdinals,
    IReadOnlySet<uint> NamedOrdinals)
{
    public static readonly ClientLaunchDependencyOrdinalExportSet Empty = new(
        ExportedOrdinals: new HashSet<uint>(),
        NamedOrdinals: new HashSet<uint>());
}
