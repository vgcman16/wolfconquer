using System.Buffers.Binary;
using System.Globalization;

internal static partial class Program
{
    private const string ClientPeSymbolAuditUsage =
        "Usage: WolfConquer.DataTool client-pe-symbol-audit [<exePath> --import-dll <dllName>] [--dll <dllPath>] [--compare] [--shim-summary] [--emit-def] [--proxy-module <moduleName>] [--max-imports N] [--max-exports N]";

    private const int ClientPeSymbolAuditDefaultMaxImports = 256;
    private const int ClientPeSymbolAuditDefaultMaxExports = 512;
    private const int ClientPeSymbolAuditMaxImportsLimit = 4096;
    private const int ClientPeSymbolAuditMaxExportsLimit = 8192;
    private const int ClientPeSymbolAuditDescriptorLimit = 512;

    static int ClientPeSymbolAudit(string[] args)
    {
        ClientPeSymbolAuditOptions options;
        try
        {
            options = ResolveClientPeSymbolAuditOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ClientPeSymbolAuditUsage);
            return 1;
        }

        if (options.ExecutablePath.Length > 0 && !File.Exists(options.ExecutablePath))
        {
            Console.Error.WriteLine($"Client executable not found: {options.ExecutablePath}");
            return 1;
        }

        if (options.DllPath.Length > 0 && !File.Exists(options.DllPath))
        {
            Console.Error.WriteLine($"PE DLL not found: {options.DllPath}");
            return 1;
        }

        try
        {
            var report = BuildClientPeSymbolAuditReport(options);
            PrintClientPeSymbolAuditReport(report);
            return 0;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            Console.Error.WriteLine($"Client PE symbol audit failed: {ex.Message}");
            return 1;
        }
    }

    private static ClientPeSymbolAuditOptions ResolveClientPeSymbolAuditOptions(string[] args)
    {
        var executablePath = string.Empty;
        var importDllName = string.Empty;
        var dllPath = string.Empty;
        var compare = false;
        var shimSummary = false;
        var emitDef = false;
        var proxyModuleName = string.Empty;
        var maxImports = ClientPeSymbolAuditDefaultMaxImports;
        var maxExports = ClientPeSymbolAuditDefaultMaxExports;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--import-dll", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-pe-symbol-audit --import-dll requires a DLL name.");
                }

                importDllName = args[++index].Trim();
                continue;
            }

            const string importDllPrefix = "--import-dll=";
            if (argument.StartsWith(importDllPrefix, StringComparison.OrdinalIgnoreCase))
            {
                importDllName = argument[importDllPrefix.Length..].Trim();
                continue;
            }

            if (string.Equals(argument, "--dll", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-pe-symbol-audit --dll requires a path.");
                }

                dllPath = Path.GetFullPath(args[++index]);
                continue;
            }

            const string dllPrefix = "--dll=";
            if (argument.StartsWith(dllPrefix, StringComparison.OrdinalIgnoreCase))
            {
                dllPath = Path.GetFullPath(argument[dllPrefix.Length..]);
                continue;
            }

            if (string.Equals(argument, "--compare", StringComparison.OrdinalIgnoreCase))
            {
                compare = true;
                continue;
            }

            if (string.Equals(argument, "--shim-summary", StringComparison.OrdinalIgnoreCase))
            {
                shimSummary = true;
                continue;
            }

            if (string.Equals(argument, "--emit-def", StringComparison.OrdinalIgnoreCase))
            {
                emitDef = true;
                continue;
            }

            if (string.Equals(argument, "--proxy-module", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-pe-symbol-audit --proxy-module requires a module name.");
                }

                proxyModuleName = NormalizeClientPeSymbolProxyModuleName(args[++index]);
                if (proxyModuleName.Length == 0)
                {
                    throw new ArgumentException("client-pe-symbol-audit --proxy-module requires a non-empty module name.");
                }

                continue;
            }

            const string proxyModulePrefix = "--proxy-module=";
            if (argument.StartsWith(proxyModulePrefix, StringComparison.OrdinalIgnoreCase))
            {
                proxyModuleName = NormalizeClientPeSymbolProxyModuleName(argument[proxyModulePrefix.Length..]);
                if (proxyModuleName.Length == 0)
                {
                    throw new ArgumentException("client-pe-symbol-audit --proxy-module requires a non-empty module name.");
                }

                continue;
            }

            if (string.Equals(argument, "--max-imports", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-pe-symbol-audit --max-imports requires a count.");
                }

                maxImports = ParseClientPeSymbolAuditInteger(
                    args[++index],
                    "--max-imports",
                    minimum: 1,
                    maximum: ClientPeSymbolAuditMaxImportsLimit);
                continue;
            }

            const string maxImportsPrefix = "--max-imports=";
            if (argument.StartsWith(maxImportsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                maxImports = ParseClientPeSymbolAuditInteger(
                    argument[maxImportsPrefix.Length..],
                    "--max-imports",
                    minimum: 1,
                    maximum: ClientPeSymbolAuditMaxImportsLimit);
                continue;
            }

            if (string.Equals(argument, "--max-exports", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-pe-symbol-audit --max-exports requires a count.");
                }

                maxExports = ParseClientPeSymbolAuditInteger(
                    args[++index],
                    "--max-exports",
                    minimum: 1,
                    maximum: ClientPeSymbolAuditMaxExportsLimit);
                continue;
            }

            const string maxExportsPrefix = "--max-exports=";
            if (argument.StartsWith(maxExportsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                maxExports = ParseClientPeSymbolAuditInteger(
                    argument[maxExportsPrefix.Length..],
                    "--max-exports",
                    minimum: 1,
                    maximum: ClientPeSymbolAuditMaxExportsLimit);
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown client-pe-symbol-audit option: {argument}");
            }

            if (executablePath.Length > 0)
            {
                throw new ArgumentException($"Unexpected client-pe-symbol-audit argument: {argument}");
            }

            executablePath = Path.GetFullPath(argument);
        }

        if (executablePath.Length == 0 && dllPath.Length == 0)
        {
            throw new ArgumentException("client-pe-symbol-audit requires an executable path and/or --dll path.");
        }

        if (executablePath.Length > 0 && importDllName.Length == 0)
        {
            throw new ArgumentException("client-pe-symbol-audit requires --import-dll when an executable path is supplied.");
        }

        if (compare && (executablePath.Length == 0 || importDllName.Length == 0 || dllPath.Length == 0))
        {
            throw new ArgumentException("client-pe-symbol-audit --compare requires an executable path, --import-dll, and --dll.");
        }

        if ((shimSummary || emitDef) && (executablePath.Length == 0 || importDllName.Length == 0 || dllPath.Length == 0))
        {
            throw new ArgumentException("client-pe-symbol-audit shim output requires an executable path, --import-dll, and --dll.");
        }

        return new ClientPeSymbolAuditOptions(
            executablePath,
            importDllName,
            dllPath,
            compare,
            shimSummary,
            emitDef,
            proxyModuleName,
            maxImports,
            maxExports);
    }

    private static int ParseClientPeSymbolAuditInteger(
        string value,
        string optionName,
        int minimum,
        int maximum)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum ||
            parsed > maximum)
        {
            throw new ArgumentException(
                $"client-pe-symbol-audit {optionName} must be between {minimum.ToString(CultureInfo.InvariantCulture)} and {maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        return parsed;
    }

    private static ClientPeSymbolAuditReport BuildClientPeSymbolAuditReport(ClientPeSymbolAuditOptions options)
    {
        var executablePe = ClientPeSymbolPeHeaders.Skipped();
        var imports = ClientPeSymbolImportSet.Empty(options.ImportDllName);
        if (options.ExecutablePath.Length > 0)
        {
            var bytes = File.ReadAllBytes(options.ExecutablePath);
            executablePe = ReadClientPeSymbolHeaders(bytes);
            if (executablePe.IsPe)
            {
                imports = ReadClientPeSymbolImportsFromDll(
                    bytes,
                    executablePe,
                    options.ImportDllName,
                    options.MaxImports);
            }
        }

        var dllPe = ClientPeSymbolPeHeaders.Skipped();
        var exports = ClientPeSymbolExportSet.Empty();
        if (options.DllPath.Length > 0)
        {
            var bytes = File.ReadAllBytes(options.DllPath);
            dllPe = ReadClientPeSymbolHeaders(bytes);
            if (dllPe.IsPe)
            {
                exports = ReadClientPeSymbolExports(bytes, dllPe, options.MaxExports);
            }
        }

        var compare = BuildClientPeSymbolCompareReport(options.Compare, executablePe, dllPe, imports, exports);
        var shimPlan = BuildClientPeSymbolShimPlan(options, executablePe, dllPe, imports, exports);
        return new ClientPeSymbolAuditReport(options, executablePe, imports, dllPe, exports, compare, shimPlan);
    }

    private static ClientPeSymbolPeHeaders ReadClientPeSymbolHeaders(byte[] bytes)
    {
        try
        {
            var data = bytes.AsSpan();
            if (data.Length < 64)
            {
                return ClientPeSymbolPeHeaders.NotPe("too-small-for-dos-header");
            }

            if (data[0] != (byte)'M' || data[1] != (byte)'Z')
            {
                return ClientPeSymbolPeHeaders.NotPe("missing-mz-signature");
            }

            var peOffset = ReadClientLaunchPeUInt32(data, 0x3C);
            if (peOffset > int.MaxValue)
            {
                return ClientPeSymbolPeHeaders.NotPe("pe-header-offset-out-of-range");
            }

            var peHeaderOffset = checked((int)peOffset);
            EnsureClientLaunchPeRange(data, peHeaderOffset, 24, "PE header");
            if (ReadClientLaunchPeUInt32(data, peHeaderOffset) != 0x00004550)
            {
                return ClientPeSymbolPeHeaders.NotPe("missing-pe-signature");
            }

            var coffOffset = checked(peHeaderOffset + 4);
            var machine = ReadClientLaunchPeUInt16(data, coffOffset);
            var sectionCount = ReadClientLaunchPeUInt16(data, coffOffset + 2);
            var optionalHeaderSize = ReadClientLaunchPeUInt16(data, coffOffset + 16);
            var optionalOffset = checked(coffOffset + 20);
            EnsureClientLaunchPeRange(data, optionalOffset, optionalHeaderSize, "optional header");

            var optionalMagic = ReadClientLaunchPeUInt16(data, optionalOffset);
            var isPe32Plus = optionalMagic switch
            {
                0x10B => false,
                0x20B => true,
                _ => throw new InvalidDataException($"unsupported-optional-header-magic-0x{optionalMagic:X4}"),
            };

            var subsystem = optionalHeaderSize >= 70
                ? ReadClientLaunchPeUInt16(data, optionalOffset + 68)
                : (ushort)0;
            var sizeOfHeaders = optionalHeaderSize >= 64
                ? ReadClientLaunchPeUInt32(data, optionalOffset + 60)
                : 0U;
            var dataDirectoryOffset = isPe32Plus ? optionalOffset + 112 : optionalOffset + 96;
            var dataDirectoryCountOffset = isPe32Plus ? optionalOffset + 108 : optionalOffset + 92;
            var dataDirectoryCount = dataDirectoryCountOffset + sizeof(uint) <= optionalOffset + optionalHeaderSize
                ? ReadClientLaunchPeUInt32(data, dataDirectoryCountOffset)
                : 0U;

            var exportTableRva = 0U;
            var exportTableSize = 0U;
            var importTableRva = 0U;
            var importTableSize = 0U;
            if (dataDirectoryOffset + 8 <= optionalOffset + optionalHeaderSize && dataDirectoryCount > 0)
            {
                exportTableRva = ReadClientLaunchPeUInt32(data, dataDirectoryOffset);
                exportTableSize = ReadClientLaunchPeUInt32(data, dataDirectoryOffset + 4);
            }

            if (dataDirectoryOffset + 16 <= optionalOffset + optionalHeaderSize && dataDirectoryCount > 1)
            {
                importTableRva = ReadClientLaunchPeUInt32(data, dataDirectoryOffset + 8);
                importTableSize = ReadClientLaunchPeUInt32(data, dataDirectoryOffset + 12);
            }

            var sections = ReadClientLaunchPeSections(data, checked(optionalOffset + optionalHeaderSize), sectionCount);
            return new ClientPeSymbolPeHeaders(
                IsPe: true,
                IsSkipped: false,
                Reason: string.Empty,
                Machine: FormatClientLaunchPeMachine(machine),
                Subsystem: FormatClientLaunchPeSubsystem(subsystem),
                IsPe32Plus: isPe32Plus,
                SizeOfHeaders: sizeOfHeaders,
                Sections: sections,
                ImportTableRva: importTableRva,
                ImportTableSize: importTableSize,
                ExportTableRva: exportTableRva,
                ExportTableSize: exportTableSize);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException or OverflowException)
        {
            return ClientPeSymbolPeHeaders.NotPe(ex.Message);
        }
    }

    private static ClientPeSymbolImportSet ReadClientPeSymbolImportsFromDll(
        byte[] bytes,
        ClientPeSymbolPeHeaders pe,
        string requestedDllName,
        int maxImports)
    {
        if (pe.ImportTableRva == 0)
        {
            return ClientPeSymbolImportSet.Empty(requestedDllName);
        }

        var data = bytes.AsSpan();
        if (!TryMapClientLaunchPeRvaToOffset(data, pe.Sections, pe.SizeOfHeaders, pe.ImportTableRva, out var importOffset))
        {
            throw new InvalidDataException($"import-table-rva-unmapped-{FormatScanHex(pe.ImportTableRva)}");
        }

        var maxDescriptors = pe.ImportTableSize > 0
            ? Math.Min(ClientPeSymbolAuditDescriptorLimit, checked((int)(pe.ImportTableSize / 20)))
            : ClientPeSymbolAuditDescriptorLimit;
        if (maxDescriptors == 0)
        {
            maxDescriptors = ClientPeSymbolAuditDescriptorLimit;
        }

        for (var descriptorIndex = 0; descriptorIndex < maxDescriptors; descriptorIndex++)
        {
            var descriptorOffset = checked(importOffset + descriptorIndex * 20);
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

            if (!TryMapClientLaunchPeRvaToOffset(data, pe.Sections, pe.SizeOfHeaders, nameRva, out var nameOffset))
            {
                throw new InvalidDataException($"import-name-rva-unmapped-{FormatScanHex(nameRva)}");
            }

            var importDllName = ReadClientLaunchPeAsciiNullTerminated(data, nameOffset);
            if (!string.Equals(importDllName, requestedDllName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var thunkRva = originalFirstThunk != 0 ? originalFirstThunk : firstThunk;
            if (thunkRva == 0)
            {
                return new ClientPeSymbolImportSet(requestedDllName, IsPresent: true, Imports: [], IsTruncated: false);
            }

            var importRead = ReadClientPeSymbolImportThunks(data, pe, thunkRva, maxImports);
            return new ClientPeSymbolImportSet(
                requestedDllName,
                IsPresent: true,
                Imports: importRead.Imports,
                IsTruncated: importRead.IsTruncated);
        }

        return ClientPeSymbolImportSet.Empty(requestedDllName);
    }

    private static ClientPeSymbolImportReadResult ReadClientPeSymbolImportThunks(
        ReadOnlySpan<byte> data,
        ClientPeSymbolPeHeaders pe,
        uint thunkRva,
        int maxImports)
    {
        if (!TryMapClientLaunchPeRvaToOffset(data, pe.Sections, pe.SizeOfHeaders, thunkRva, out var thunkOffset))
        {
            throw new InvalidDataException($"import-thunk-rva-unmapped-{FormatScanHex(thunkRva)}");
        }

        var imports = new List<ClientPeSymbolImport>();
        var thunkSize = pe.IsPe32Plus ? sizeof(ulong) : sizeof(uint);
        for (var index = 0; index < maxImports; index++)
        {
            var entryOffset = checked(thunkOffset + index * thunkSize);
            EnsureClientLaunchPeRange(data, entryOffset, thunkSize, "import thunk");
            var thunkValue = pe.IsPe32Plus
                ? ReadClientPeSymbolUInt64(data, entryOffset)
                : ReadClientLaunchPeUInt32(data, entryOffset);
            if (thunkValue == 0)
            {
                return new ClientPeSymbolImportReadResult(imports, IsTruncated: false);
            }

            var ordinalFlag = pe.IsPe32Plus ? 0x8000000000000000UL : 0x80000000UL;
            if ((thunkValue & ordinalFlag) != 0)
            {
                imports.Add(new ClientPeSymbolImport(
                    Name: string.Empty,
                    Hint: null,
                    Ordinal: checked((ushort)(thunkValue & 0xFFFF)),
                    IsOrdinal: true));
                continue;
            }

            if (thunkValue > uint.MaxValue)
            {
                throw new InvalidDataException($"import-by-name-rva-out-of-range-{FormatScanHex(thunkValue)}");
            }

            var hintNameRva = checked((uint)thunkValue);
            if (!TryMapClientLaunchPeRvaToOffset(data, pe.Sections, pe.SizeOfHeaders, hintNameRva, out var hintNameOffset))
            {
                throw new InvalidDataException($"import-by-name-rva-unmapped-{FormatScanHex(hintNameRva)}");
            }

            var hint = ReadClientLaunchPeUInt16(data, hintNameOffset);
            var name = ReadClientLaunchPeAsciiNullTerminated(data, hintNameOffset + sizeof(ushort));
            imports.Add(new ClientPeSymbolImport(
                Name: name,
                Hint: hint,
                Ordinal: null,
                IsOrdinal: false));
        }

        return new ClientPeSymbolImportReadResult(imports, IsTruncated: true);
    }

    private static ClientPeSymbolExportSet ReadClientPeSymbolExports(
        byte[] bytes,
        ClientPeSymbolPeHeaders pe,
        int maxExports)
    {
        if (pe.ExportTableRva == 0)
        {
            return ClientPeSymbolExportSet.Empty();
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
        var namePointerTableRva = ReadClientLaunchPeUInt32(data, exportOffset + 32);
        var ordinalTableRva = ReadClientLaunchPeUInt32(data, exportOffset + 36);

        if (functionCount > int.MaxValue || nameCount > int.MaxValue)
        {
            throw new InvalidDataException("export-table-count-out-of-range");
        }

        var namedExports = new List<ClientPeSymbolExport>();
        var namedOrdinalIndexes = new HashSet<uint>();
        if (nameCount > 0)
        {
            if (!TryMapClientLaunchPeRvaToOffset(data, pe.Sections, pe.SizeOfHeaders, namePointerTableRva, out var namePointerOffset))
            {
                throw new InvalidDataException($"export-name-pointer-table-rva-unmapped-{FormatScanHex(namePointerTableRva)}");
            }

            if (!TryMapClientLaunchPeRvaToOffset(data, pe.Sections, pe.SizeOfHeaders, ordinalTableRva, out var ordinalTableOffset))
            {
                throw new InvalidDataException($"export-ordinal-table-rva-unmapped-{FormatScanHex(ordinalTableRva)}");
            }

            if (!TryMapClientLaunchPeRvaToOffset(data, pe.Sections, pe.SizeOfHeaders, functionTableRva, out var functionTableOffset))
            {
                throw new InvalidDataException($"export-function-table-rva-unmapped-{FormatScanHex(functionTableRva)}");
            }

            var namesToInspect = checked((int)nameCount);
            for (var index = 0; index < namesToInspect; index++)
            {
                var ordinalIndex = ReadClientLaunchPeUInt16(data, checked(ordinalTableOffset + index * sizeof(ushort)));
                if (ordinalIndex >= functionCount)
                {
                    throw new InvalidDataException($"export-ordinal-index-out-of-range-{ordinalIndex.ToString(CultureInfo.InvariantCulture)}");
                }

                namedOrdinalIndexes.Add(ordinalIndex);
                if (index >= maxExports)
                {
                    continue;
                }

                var nameRva = ReadClientLaunchPeUInt32(data, checked(namePointerOffset + index * sizeof(uint)));
                if (!TryMapClientLaunchPeRvaToOffset(data, pe.Sections, pe.SizeOfHeaders, nameRva, out var nameOffset))
                {
                    throw new InvalidDataException($"export-name-rva-unmapped-{FormatScanHex(nameRva)}");
                }

                var functionRva = ReadClientLaunchPeUInt32(data, checked(functionTableOffset + ordinalIndex * sizeof(uint)));
                var forwarder = TryReadClientPeSymbolForwarder(data, pe, functionRva);
                namedExports.Add(new ClientPeSymbolExport(
                    Name: ReadClientLaunchPeAsciiNullTerminated(data, nameOffset),
                    Ordinal: checked((uint)(ordinalBase + ordinalIndex)),
                    Rva: functionRva,
                    Forwarder: forwarder));
            }
        }

        var ordinalOnlyExports = CountClientPeSymbolOrdinalOnlyExports(
            data,
            pe,
            functionTableRva,
            functionCount,
            namedOrdinalIndexes);

        return new ClientPeSymbolExportSet(
            namedExports,
            checked((int)functionCount),
            checked((int)nameCount),
            ordinalOnlyExports,
            nameCount > maxExports);
    }

    private static int CountClientPeSymbolOrdinalOnlyExports(
        ReadOnlySpan<byte> data,
        ClientPeSymbolPeHeaders pe,
        uint functionTableRva,
        uint functionCount,
        IReadOnlySet<uint> namedOrdinalIndexes)
    {
        if (functionCount == 0)
        {
            return 0;
        }

        if (!TryMapClientLaunchPeRvaToOffset(data, pe.Sections, pe.SizeOfHeaders, functionTableRva, out var functionTableOffset))
        {
            throw new InvalidDataException($"export-function-table-rva-unmapped-{FormatScanHex(functionTableRva)}");
        }

        var ordinalOnlyCount = 0;
        for (var index = 0U; index < functionCount; index++)
        {
            var functionRva = ReadClientLaunchPeUInt32(data, checked(functionTableOffset + (int)index * sizeof(uint)));
            if (functionRva != 0 && !namedOrdinalIndexes.Contains(index))
            {
                ordinalOnlyCount++;
            }
        }

        return ordinalOnlyCount;
    }

    private static string TryReadClientPeSymbolForwarder(
        ReadOnlySpan<byte> data,
        ClientPeSymbolPeHeaders pe,
        uint functionRva)
    {
        if (pe.ExportTableSize == 0 ||
            functionRva < pe.ExportTableRva ||
            functionRva >= pe.ExportTableRva + pe.ExportTableSize)
        {
            return string.Empty;
        }

        if (!TryMapClientLaunchPeRvaToOffset(data, pe.Sections, pe.SizeOfHeaders, functionRva, out var forwarderOffset))
        {
            return string.Empty;
        }

        return ReadClientLaunchPeAsciiNullTerminated(data, forwarderOffset);
    }

    private static ClientPeSymbolCompareReport BuildClientPeSymbolCompareReport(
        bool enabled,
        ClientPeSymbolPeHeaders executablePe,
        ClientPeSymbolPeHeaders dllPe,
        ClientPeSymbolImportSet imports,
        ClientPeSymbolExportSet exports)
    {
        if (!enabled)
        {
            return ClientPeSymbolCompareReport.Disabled;
        }

        if (!executablePe.IsPe)
        {
            return ClientPeSymbolCompareReport.Skipped("executable-not-pe");
        }

        if (!dllPe.IsPe)
        {
            return ClientPeSymbolCompareReport.Skipped("dll-not-pe");
        }

        var exportNames = exports.NamedExports
            .Select(static export => export.Name)
            .ToHashSet(StringComparer.Ordinal);
        var namedImports = imports.Imports
            .Where(static import => !import.IsOrdinal && import.Name.Length > 0)
            .ToArray();
        var missingNames = namedImports
            .Select(static import => import.Name)
            .Distinct(StringComparer.Ordinal)
            .Where(name => !exportNames.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var matchedNames = namedImports
            .Select(static import => import.Name)
            .Distinct(StringComparer.Ordinal)
            .Count(exportNames.Contains);

        return new ClientPeSymbolCompareReport(
            Enabled: true,
            Status: "ok",
            Reason: string.Empty,
            MatchedNamedImportCount: matchedNames,
            MissingNamedImportCount: missingNames.Length,
            OrdinalImportCount: imports.OrdinalImportCount,
            MissingNames: missingNames);
    }

    private static ClientPeSymbolShimPlan BuildClientPeSymbolShimPlan(
        ClientPeSymbolAuditOptions options,
        ClientPeSymbolPeHeaders executablePe,
        ClientPeSymbolPeHeaders dllPe,
        ClientPeSymbolImportSet imports,
        ClientPeSymbolExportSet exports)
    {
        if (!options.EmitShimSummary && !options.EmitDef)
        {
            return ClientPeSymbolShimPlan.Disabled;
        }

        var proxyModuleName = ResolveClientPeSymbolProxyModuleName(
            options.ImportDllName,
            options.DllPath,
            options.ProxyModuleName);

        if (!executablePe.IsPe)
        {
            return ClientPeSymbolShimPlan.Skipped(options.ImportDllName, proxyModuleName, "executable-not-pe");
        }

        if (!dllPe.IsPe)
        {
            return ClientPeSymbolShimPlan.Skipped(options.ImportDllName, proxyModuleName, "dll-not-pe");
        }

        if (!imports.IsPresent)
        {
            return ClientPeSymbolShimPlan.Skipped(options.ImportDllName, proxyModuleName, "import-dll-not-present");
        }

        var exportNames = exports.NamedExports
            .Select(static export => export.Name)
            .ToHashSet(StringComparer.Ordinal);
        var namedImportNames = imports.Imports
            .Where(static import => !import.IsOrdinal && import.Name.Length > 0)
            .Select(static import => import.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var proxyExports = namedImportNames
            .Where(exportNames.Contains)
            .Select(name => new ClientPeSymbolProxyExport(name, $"{proxyModuleName}.{name}"))
            .ToArray();
        var missingNames = namedImportNames
            .Where(name => !exportNames.Contains(name))
            .ToArray();
        var reasons = new List<string>();
        if (imports.IsTruncated)
        {
            reasons.Add("imports-truncated");
        }

        if (exports.IsTruncated)
        {
            reasons.Add("exports-truncated");
        }

        return new ClientPeSymbolShimPlan(
            Enabled: true,
            Status: reasons.Count == 0 ? "ok" : "partial",
            Reason: string.Join(",", reasons),
            LibraryName: options.ImportDllName,
            ProxyModuleName: proxyModuleName,
            ProxyExports: proxyExports,
            MissingNames: missingNames,
            OrdinalImportCount: imports.OrdinalImportCount);
    }

    private static string ResolveClientPeSymbolProxyModuleName(
        string importDllName,
        string dllPath,
        string explicitProxyModuleName)
    {
        if (explicitProxyModuleName.Length > 0)
        {
            return explicitProxyModuleName;
        }

        var donorModuleName = NormalizeClientPeSymbolProxyModuleName(Path.GetFileName(dllPath));
        if (donorModuleName.Length == 0)
        {
            donorModuleName = "donor";
        }

        var importModuleName = NormalizeClientPeSymbolProxyModuleName(importDllName);
        if (importModuleName.Length > 0 &&
            string.Equals(donorModuleName, importModuleName, StringComparison.OrdinalIgnoreCase))
        {
            return donorModuleName + "_donor";
        }

        return donorModuleName;
    }

    private static string NormalizeClientPeSymbolProxyModuleName(string value)
    {
        var moduleName = Path.GetFileName(value.Trim());
        if (moduleName.Length == 0)
        {
            return string.Empty;
        }

        return moduleName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(moduleName)
            : moduleName;
    }

    private static ulong ReadClientPeSymbolUInt64(ReadOnlySpan<byte> data, int offset)
    {
        EnsureClientLaunchPeRange(data, offset, sizeof(ulong), "uint64");
        return BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset, sizeof(ulong)));
    }

    private static void PrintClientPeSymbolAuditReport(ClientPeSymbolAuditReport report)
    {
        Console.WriteLine("Client PE symbol audit:");
        Console.WriteLine("Mode: no-launch no-mutation");
        Console.WriteLine($"Executable: {FormatClientPeSymbolAuditOptionalPath(report.Options.ExecutablePath)}");
        Console.WriteLine($"Requested import DLL: {FormatClientPeSymbolAuditOptionalText(report.Options.ImportDllName)}");
        Console.WriteLine($"DLL: {FormatClientPeSymbolAuditOptionalPath(report.Options.DllPath)}");
        Console.WriteLine($"Max imports: {report.Options.MaxImports.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Max exports: {report.Options.MaxExports.ToString(CultureInfo.InvariantCulture)}");
        PrintClientPeSymbolPeLine("Executable PE", report.ExecutablePe);
        if (!report.ExecutablePe.IsSkipped && report.Options.ImportDllName.Length > 0)
        {
            Console.WriteLine(
                $"Imports from {report.Imports.DllName}: present={FormatClientLaunchEvidenceBool(report.Imports.IsPresent)} " +
                $"total={report.Imports.Imports.Count.ToString(CultureInfo.InvariantCulture)} " +
                $"named={report.Imports.NamedImportCount.ToString(CultureInfo.InvariantCulture)} " +
                $"ordinal={report.Imports.OrdinalImportCount.ToString(CultureInfo.InvariantCulture)} " +
                $"truncated={FormatClientLaunchEvidenceBool(report.Imports.IsTruncated)}");
            foreach (var import in report.Imports.Imports)
            {
                if (import.IsOrdinal)
                {
                    Console.WriteLine($"  import ordinal={import.Ordinal!.Value.ToString(CultureInfo.InvariantCulture)}");
                    continue;
                }

                Console.WriteLine(
                    $"  import name={import.Name} hint={import.Hint!.Value.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        PrintClientPeSymbolPeLine("DLL PE", report.DllPe);
        if (!report.DllPe.IsSkipped)
        {
            Console.WriteLine(
                $"Exports: total={report.Exports.TotalExportCount.ToString(CultureInfo.InvariantCulture)} " +
                $"named={report.Exports.TotalNamedExportCount.ToString(CultureInfo.InvariantCulture)} " +
                $"ordinalOnly={report.Exports.OrdinalOnlyExportCount.ToString(CultureInfo.InvariantCulture)} " +
                $"truncated={FormatClientLaunchEvidenceBool(report.Exports.IsTruncated)}");
            foreach (var export in report.Exports.NamedExports)
            {
                var forwarder = export.Forwarder.Length == 0 ? string.Empty : $" forwarder={export.Forwarder}";
                Console.WriteLine(
                    $"  export name={export.Name} ordinal={export.Ordinal.ToString(CultureInfo.InvariantCulture)} " +
                    $"rva={FormatScanHex(export.Rva)}{forwarder}");
            }
        }

        if (!report.Compare.Enabled)
        {
            Console.WriteLine("Compare: enabled=False");
        }
        else
        {
            Console.WriteLine(
                $"Compare: enabled=True status={report.Compare.Status} " +
                $"matchedNamedImports={report.Compare.MatchedNamedImportCount.ToString(CultureInfo.InvariantCulture)} " +
                $"missingNamedImports={report.Compare.MissingNamedImportCount.ToString(CultureInfo.InvariantCulture)} " +
                $"ordinalImports={report.Compare.OrdinalImportCount.ToString(CultureInfo.InvariantCulture)}" +
                (report.Compare.Reason.Length == 0 ? string.Empty : $" reason={report.Compare.Reason}"));
            foreach (var name in report.Compare.MissingNames)
            {
                Console.WriteLine($"  missing name={name}");
            }
        }

        PrintClientPeSymbolShimPlan(report);
    }

    private static void PrintClientPeSymbolShimPlan(ClientPeSymbolAuditReport report)
    {
        if (!report.ShimPlan.Enabled)
        {
            return;
        }

        Console.WriteLine(
            $"Shim plan: enabled=True status={report.ShimPlan.Status} " +
            $"library={report.ShimPlan.LibraryName} " +
            $"donorForwardModule={report.ShimPlan.ProxyModuleName}.dll " +
            $"proxyableNamedImports={report.ShimPlan.ProxyExports.Count.ToString(CultureInfo.InvariantCulture)} " +
            $"missingNamedImports={report.ShimPlan.MissingNames.Count.ToString(CultureInfo.InvariantCulture)} " +
            $"ordinalImports={report.ShimPlan.OrdinalImportCount.ToString(CultureInfo.InvariantCulture)}" +
            (report.ShimPlan.Reason.Length == 0 ? string.Empty : $" reason={report.ShimPlan.Reason}"));
        foreach (var proxyExport in report.ShimPlan.ProxyExports)
        {
            Console.WriteLine($"  proxy name={proxyExport.Name} forwarder={proxyExport.Forwarder}");
        }

        foreach (var name in report.ShimPlan.MissingNames)
        {
            Console.WriteLine($"  missing shim name={name}");
        }

        if (report.ShimPlan.OrdinalImportCount > 0)
        {
            Console.WriteLine(
                $"  ordinal imports require manual shim exports: count={report.ShimPlan.OrdinalImportCount.ToString(CultureInfo.InvariantCulture)}");
        }

        if (report.Options.EmitDef)
        {
            PrintClientPeSymbolDefPlan(report);
        }
    }

    private static void PrintClientPeSymbolDefPlan(ClientPeSymbolAuditReport report)
    {
        Console.WriteLine("DEF proxy export plan:");
        Console.WriteLine("; generated by client-pe-symbol-audit; review before native shim build");
        Console.WriteLine($"; source executable: {report.Options.ExecutablePath}");
        Console.WriteLine($"; donor DLL path: {report.Options.DllPath}");
        Console.WriteLine($"; donor forward module: {report.ShimPlan.ProxyModuleName}.dll");
        Console.WriteLine($"LIBRARY {NormalizeClientPeSymbolProxyModuleName(report.ShimPlan.LibraryName)}");
        Console.WriteLine("EXPORTS");
        foreach (var proxyExport in report.ShimPlan.ProxyExports)
        {
            Console.WriteLine($"  {proxyExport.Name}={proxyExport.Forwarder}");
        }

        foreach (var name in report.ShimPlan.MissingNames)
        {
            Console.WriteLine($"  {name} ; missing from donor: implement in shim");
        }

        if (report.ShimPlan.OrdinalImportCount > 0)
        {
            Console.WriteLine(
                $"  ; {report.ShimPlan.OrdinalImportCount.ToString(CultureInfo.InvariantCulture)} ordinal import(s) require manual NONAME exports or stub mapping");
        }
    }

    private static void PrintClientPeSymbolPeLine(string label, ClientPeSymbolPeHeaders pe)
    {
        if (pe.IsSkipped)
        {
            Console.WriteLine($"{label}: <skipped>");
            return;
        }

        if (!pe.IsPe)
        {
            Console.WriteLine($"{label}: valid=False reason={pe.Reason}");
            return;
        }

        Console.WriteLine($"{label}: valid=True machine={pe.Machine} subsystem={pe.Subsystem}");
    }

    private static string FormatClientPeSymbolAuditOptionalPath(string value)
    {
        return value.Length == 0 ? "<none>" : value;
    }

    private static string FormatClientPeSymbolAuditOptionalText(string value)
    {
        return value.Length == 0 ? "<none>" : value;
    }
}

internal sealed record ClientPeSymbolAuditOptions(
    string ExecutablePath,
    string ImportDllName,
    string DllPath,
    bool Compare,
    bool EmitShimSummary,
    bool EmitDef,
    string ProxyModuleName,
    int MaxImports,
    int MaxExports);

internal sealed record ClientPeSymbolAuditReport(
    ClientPeSymbolAuditOptions Options,
    ClientPeSymbolPeHeaders ExecutablePe,
    ClientPeSymbolImportSet Imports,
    ClientPeSymbolPeHeaders DllPe,
    ClientPeSymbolExportSet Exports,
    ClientPeSymbolCompareReport Compare,
    ClientPeSymbolShimPlan ShimPlan);

internal sealed record ClientPeSymbolPeHeaders(
    bool IsPe,
    bool IsSkipped,
    string Reason,
    string Machine,
    string Subsystem,
    bool IsPe32Plus,
    uint SizeOfHeaders,
    IReadOnlyList<ClientLaunchPeSection> Sections,
    uint ImportTableRva,
    uint ImportTableSize,
    uint ExportTableRva,
    uint ExportTableSize)
{
    public static ClientPeSymbolPeHeaders Skipped()
    {
        return new ClientPeSymbolPeHeaders(
            IsPe: false,
            IsSkipped: true,
            Reason: string.Empty,
            Machine: string.Empty,
            Subsystem: string.Empty,
            IsPe32Plus: false,
            SizeOfHeaders: 0,
            Sections: [],
            ImportTableRva: 0,
            ImportTableSize: 0,
            ExportTableRva: 0,
            ExportTableSize: 0);
    }

    public static ClientPeSymbolPeHeaders NotPe(string reason)
    {
        return new ClientPeSymbolPeHeaders(
            IsPe: false,
            IsSkipped: false,
            Reason: reason,
            Machine: string.Empty,
            Subsystem: string.Empty,
            IsPe32Plus: false,
            SizeOfHeaders: 0,
            Sections: [],
            ImportTableRva: 0,
            ImportTableSize: 0,
            ExportTableRva: 0,
            ExportTableSize: 0);
    }
}

internal sealed record ClientPeSymbolImportSet(
    string DllName,
    bool IsPresent,
    IReadOnlyList<ClientPeSymbolImport> Imports,
    bool IsTruncated)
{
    public int NamedImportCount => Imports.Count(static import => !import.IsOrdinal);

    public int OrdinalImportCount => Imports.Count(static import => import.IsOrdinal);

    public static ClientPeSymbolImportSet Empty(string dllName)
    {
        return new ClientPeSymbolImportSet(dllName, IsPresent: false, Imports: [], IsTruncated: false);
    }
}

internal sealed record ClientPeSymbolImport(
    string Name,
    ushort? Hint,
    ushort? Ordinal,
    bool IsOrdinal);

internal sealed record ClientPeSymbolImportReadResult(
    IReadOnlyList<ClientPeSymbolImport> Imports,
    bool IsTruncated);

internal sealed record ClientPeSymbolExportSet(
    IReadOnlyList<ClientPeSymbolExport> NamedExports,
    int TotalExportCount,
    int TotalNamedExportCount,
    int OrdinalOnlyExportCount,
    bool IsTruncated)
{
    public static ClientPeSymbolExportSet Empty()
    {
        return new ClientPeSymbolExportSet(
            NamedExports: [],
            TotalExportCount: 0,
            TotalNamedExportCount: 0,
            OrdinalOnlyExportCount: 0,
            IsTruncated: false);
    }
}

internal sealed record ClientPeSymbolExport(
    string Name,
    uint Ordinal,
    uint Rva,
    string Forwarder);

internal sealed record ClientPeSymbolCompareReport(
    bool Enabled,
    string Status,
    string Reason,
    int MatchedNamedImportCount,
    int MissingNamedImportCount,
    int OrdinalImportCount,
    IReadOnlyList<string> MissingNames)
{
    public static readonly ClientPeSymbolCompareReport Disabled = new(
        Enabled: false,
        Status: "disabled",
        Reason: string.Empty,
        MatchedNamedImportCount: 0,
        MissingNamedImportCount: 0,
        OrdinalImportCount: 0,
        MissingNames: []);

    public static ClientPeSymbolCompareReport Skipped(string reason)
    {
        return new ClientPeSymbolCompareReport(
            Enabled: true,
            Status: "skipped",
            Reason: reason,
            MatchedNamedImportCount: 0,
            MissingNamedImportCount: 0,
            OrdinalImportCount: 0,
            MissingNames: []);
    }
}

internal sealed record ClientPeSymbolShimPlan(
    bool Enabled,
    string Status,
    string Reason,
    string LibraryName,
    string ProxyModuleName,
    IReadOnlyList<ClientPeSymbolProxyExport> ProxyExports,
    IReadOnlyList<string> MissingNames,
    int OrdinalImportCount)
{
    public static readonly ClientPeSymbolShimPlan Disabled = new(
        Enabled: false,
        Status: "disabled",
        Reason: string.Empty,
        LibraryName: string.Empty,
        ProxyModuleName: string.Empty,
        ProxyExports: [],
        MissingNames: [],
        OrdinalImportCount: 0);

    public static ClientPeSymbolShimPlan Skipped(
        string libraryName,
        string proxyModuleName,
        string reason)
    {
        return new ClientPeSymbolShimPlan(
            Enabled: true,
            Status: "skipped",
            Reason: reason,
            LibraryName: libraryName,
            ProxyModuleName: proxyModuleName,
            ProxyExports: [],
            MissingNames: [],
            OrdinalImportCount: 0);
    }
}

internal sealed record ClientPeSymbolProxyExport(
    string Name,
    string Forwarder);
