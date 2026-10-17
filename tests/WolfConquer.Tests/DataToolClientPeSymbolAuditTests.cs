using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolClientPeSymbolAuditTests
{
    private const string Usage =
        "WolfConquer.DataTool client-pe-symbol-audit [<exePath> --import-dll <dllName>] [--dll <dllPath>] [--compare] [--shim-summary] [--emit-def] [--proxy-module <moduleName>] [--max-imports N] [--max-exports N]";

    [Fact]
    public async Task HelpMentionsClientPeSymbolAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(Usage, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task AuditReportsImportsExportsAndMissingNamesWithoutLaunching()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var exePath = Path.Combine(tempRoot, "root", "._cache_Conquer.exe");
            var dllPath = Path.Combine(tempRoot, "Env_DX9", "AloneFace.dll");
            await WriteBytesAsync(
                exePath,
                CreatePeFixture(
                    imports:
                    [
                        new PeImportDllFixture(
                            "AloneFace.dll",
                            [
                                PeImportSymbolFixture.Named("PresentFunction", hint: 1),
                                PeImportSymbolFixture.Named("MissingFunction", hint: 2),
                                PeImportSymbolFixture.ByOrdinal(7),
                            ]),
                        new PeImportDllFixture(
                            "kernel32.dll",
                            [PeImportSymbolFixture.Named("ExitProcess", hint: 0)]),
                    ],
                    exportNames: []));
            await WriteBytesAsync(
                dllPath,
                CreatePeFixture(
                    imports: [],
                    exportNames: ["PresentFunction", "OtherExport"]));

            var result = await RunDataToolAsync(
                "client-pe-symbol-audit",
                exePath,
                "--import-dll",
                "AloneFace.dll",
                "--dll",
                dllPath,
                "--compare",
                "--max-imports",
                "8",
                "--max-exports",
                "8");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client PE symbol audit:", result.StandardOutput);
            Assert.Contains("Mode: no-launch no-mutation", result.StandardOutput);
            Assert.Contains($"Executable: {Path.GetFullPath(exePath)}", result.StandardOutput);
            Assert.Contains("Requested import DLL: AloneFace.dll", result.StandardOutput);
            Assert.Contains($"DLL: {Path.GetFullPath(dllPath)}", result.StandardOutput);
            Assert.Contains("Executable PE: valid=True machine=I386 subsystem=WindowsGui", result.StandardOutput);
            Assert.Contains("Imports from AloneFace.dll: present=True total=3 named=2 ordinal=1 truncated=False", result.StandardOutput);
            Assert.Contains("  import name=PresentFunction hint=1", result.StandardOutput);
            Assert.Contains("  import name=MissingFunction hint=2", result.StandardOutput);
            Assert.Contains("  import ordinal=7", result.StandardOutput);
            Assert.Contains("DLL PE: valid=True machine=I386 subsystem=WindowsGui", result.StandardOutput);
            Assert.Contains("Exports: total=2 named=2 ordinalOnly=0 truncated=False", result.StandardOutput);
            Assert.Contains("  export name=PresentFunction ordinal=1 rva=0x1500", result.StandardOutput);
            Assert.Contains("  export name=OtherExport ordinal=2 rva=0x1510", result.StandardOutput);
            Assert.Contains("Compare: enabled=True status=ok matchedNamedImports=1 missingNamedImports=1 ordinalImports=1", result.StandardOutput);
            Assert.Contains("  missing name=MissingFunction", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task ShimPlanEmitsMissingSummaryAndDefPlanForDonorDll()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var exePath = Path.Combine(tempRoot, "root-cache", "Conquer.exe");
            var dllPath = Path.Combine(tempRoot, "donor", "graphic.dll");
            await WriteBytesAsync(
                exePath,
                CreatePeFixture(
                    imports:
                    [
                        new PeImportDllFixture(
                            "graphic.dll",
                            [
                                PeImportSymbolFixture.Named("CreateTexture", hint: 3),
                                PeImportSymbolFixture.Named("MissingRootCacheSurface", hint: 4),
                                PeImportSymbolFixture.ByOrdinal(12),
                            ]),
                    ],
                    exportNames: []));
            await WriteBytesAsync(
                dllPath,
                CreatePeFixture(
                    imports: [],
                    exportNames: ["CreateTexture", "UnusedDonorExport"]));

            var result = await RunDataToolAsync(
                "client-pe-symbol-audit",
                exePath,
                "--import-dll",
                "graphic.dll",
                "--dll",
                dllPath,
                "--compare",
                "--shim-summary",
                "--emit-def",
                "--max-imports",
                "8",
                "--max-exports",
                "8");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Compare: enabled=True status=ok matchedNamedImports=1 missingNamedImports=1 ordinalImports=1", result.StandardOutput);
            Assert.Contains(
                "Shim plan: enabled=True status=ok library=graphic.dll donorForwardModule=graphic_donor.dll proxyableNamedImports=1 missingNamedImports=1 ordinalImports=1",
                result.StandardOutput);
            Assert.Contains("  proxy name=CreateTexture forwarder=graphic_donor.CreateTexture", result.StandardOutput);
            Assert.Contains("  missing shim name=MissingRootCacheSurface", result.StandardOutput);
            Assert.Contains("  ordinal imports require manual shim exports: count=1", result.StandardOutput);
            Assert.Contains("DEF proxy export plan:", result.StandardOutput);
            Assert.Contains("; donor forward module: graphic_donor.dll", result.StandardOutput);
            Assert.Contains("LIBRARY graphic", result.StandardOutput);
            Assert.Contains("EXPORTS", result.StandardOutput);
            Assert.Contains("  CreateTexture=graphic_donor.CreateTexture", result.StandardOutput);
            Assert.Contains("  MissingRootCacheSurface ; missing from donor: implement in shim", result.StandardOutput);
            Assert.Contains("  ; 1 ordinal import(s) require manual NONAME exports or stub mapping", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task ShimPlanUsesExplicitProxyModuleWithoutDefOutput()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var exePath = Path.Combine(tempRoot, "root-cache", "Conquer.exe");
            var dllPath = Path.Combine(tempRoot, "donor", "graphic.dll");
            await WriteBytesAsync(
                exePath,
                CreatePeFixture(
                    imports:
                    [
                        new PeImportDllFixture(
                            "graphic.dll",
                            [PeImportSymbolFixture.Named("CreateTexture", hint: 3)]),
                    ],
                    exportNames: []));
            await WriteBytesAsync(
                dllPath,
                CreatePeFixture(
                    imports: [],
                    exportNames: ["CreateTexture"]));

            var result = await RunDataToolAsync(
                "client-pe-symbol-audit",
                exePath,
                "--import-dll=graphic.dll",
                "--dll",
                dllPath,
                "--shim-summary",
                "--proxy-module",
                "graphic7899_real.dll",
                "--max-imports=8",
                "--max-exports=8");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Compare: enabled=False", result.StandardOutput);
            Assert.Contains(
                "Shim plan: enabled=True status=ok library=graphic.dll donorForwardModule=graphic7899_real.dll proxyableNamedImports=1 missingNamedImports=0 ordinalImports=0",
                result.StandardOutput);
            Assert.Contains("  proxy name=CreateTexture forwarder=graphic7899_real.CreateTexture", result.StandardOutput);
            Assert.DoesNotContain("DEF proxy export plan:", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task ExportOnlyReportsDllSymbolsWithoutExecutable()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var dllPath = Path.Combine(tempRoot, "Env_DX9", "AloneFace.dll");
            await WriteBytesAsync(
                dllPath,
                CreatePeFixture(
                    imports: [],
                    exportNames: ["FirstExport", "SecondExport"]));

            var result = await RunDataToolAsync(
                "client-pe-symbol-audit",
                "--dll",
                dllPath,
                "--max-exports",
                "8");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Executable: <none>", result.StandardOutput);
            Assert.Contains("Requested import DLL: <none>", result.StandardOutput);
            Assert.Contains($"DLL: {Path.GetFullPath(dllPath)}", result.StandardOutput);
            Assert.Contains("Executable PE: <skipped>", result.StandardOutput);
            Assert.Contains("DLL PE: valid=True machine=I386 subsystem=WindowsGui", result.StandardOutput);
            Assert.Contains("Exports: total=2 named=2 ordinalOnly=0 truncated=False", result.StandardOutput);
            Assert.Contains("  export name=FirstExport ordinal=1 rva=0x1500", result.StandardOutput);
            Assert.Contains("  export name=SecondExport ordinal=2 rva=0x1510", result.StandardOutput);
            Assert.Contains("Compare: enabled=False", result.StandardOutput);
            Assert.DoesNotContain("Imports from", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CompareRequiresExecutableImportDllAndDll()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var exePath = Path.Combine(tempRoot, "._cache_Conquer.exe");
            await WriteBytesAsync(
                exePath,
                CreatePeFixture(
                    imports: [new PeImportDllFixture("AloneFace.dll", [PeImportSymbolFixture.Named("PresentFunction", hint: 1)])],
                    exportNames: []));

            var result = await RunDataToolAsync(
                "client-pe-symbol-audit",
                exePath,
                "--import-dll",
                "AloneFace.dll",
                "--compare");

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Contains(
                "client-pe-symbol-audit --compare requires an executable path, --import-dll, and --dll.",
                result.StandardError);
            Assert.Contains("Usage: " + Usage, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static byte[] CreatePeFixture(
        IReadOnlyList<PeImportDllFixture> imports,
        IReadOnlyList<string> exportNames)
    {
        const int peOffset = 0x80;
        const int optionalHeaderSize = 0xE0;
        const int rawSectionOffset = 0x200;
        const int rawSectionSize = 0x2000;
        const int importDescriptorOffset = 0x300;
        const int importLookupOffset = 0x500;
        const int importAddressOffset = 0x600;
        const int importNameOffset = 0x700;
        const int importDllNameOffset = 0xA00;
        const int exportDirectoryOffset = 0xC00;
        const int exportDllNameOffset = 0xC80;
        const int exportFunctionTableOffset = 0xD00;
        const int exportNamePointerTableOffset = 0xD80;
        const int exportOrdinalTableOffset = 0xE00;
        const int exportNameOffset = 0xE80;

        var bytes = new byte[rawSectionOffset + rawSectionSize];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        WriteUInt32(bytes, 0x3C, peOffset);

        bytes[peOffset] = (byte)'P';
        bytes[peOffset + 1] = (byte)'E';
        var coffOffset = peOffset + 4;
        WriteUInt16(bytes, coffOffset, 0x014C);
        WriteUInt16(bytes, coffOffset + 2, 1);
        WriteUInt16(bytes, coffOffset + 16, optionalHeaderSize);

        var optionalOffset = coffOffset + 20;
        WriteUInt16(bytes, optionalOffset, 0x10B);
        WriteUInt32(bytes, optionalOffset + 16, 0x1000);
        WriteUInt32(bytes, optionalOffset + 20, 0x1000);
        WriteUInt32(bytes, optionalOffset + 24, 0x2000);
        WriteUInt32(bytes, optionalOffset + 28, 0x400000);
        WriteUInt32(bytes, optionalOffset + 32, 0x1000);
        WriteUInt32(bytes, optionalOffset + 36, 0x200);
        WriteUInt32(bytes, optionalOffset + 56, 0x3000);
        WriteUInt32(bytes, optionalOffset + 60, 0x200);
        WriteUInt16(bytes, optionalOffset + 68, 2);
        WriteUInt32(bytes, optionalOffset + 92, 16);
        if (exportNames.Count > 0)
        {
            WriteUInt32(bytes, optionalOffset + 96, ToRva(exportDirectoryOffset, rawSectionOffset));
            WriteUInt32(bytes, optionalOffset + 100, 0x400);
        }

        if (imports.Count > 0)
        {
            WriteUInt32(bytes, optionalOffset + 104, ToRva(importDescriptorOffset, rawSectionOffset));
            WriteUInt32(bytes, optionalOffset + 108, (uint)((imports.Count + 1) * 20));
        }

        var sectionOffset = optionalOffset + optionalHeaderSize;
        Encoding.ASCII.GetBytes(".rdata\0\0").CopyTo(bytes, sectionOffset);
        WriteUInt32(bytes, sectionOffset + 8, rawSectionSize);
        WriteUInt32(bytes, sectionOffset + 12, 0x1000);
        WriteUInt32(bytes, sectionOffset + 16, rawSectionSize);
        WriteUInt32(bytes, sectionOffset + 20, rawSectionOffset);

        WriteImports(
            bytes,
            imports,
            rawSectionOffset,
            importDescriptorOffset,
            importLookupOffset,
            importAddressOffset,
            importNameOffset,
            importDllNameOffset);
        WriteExports(
            bytes,
            exportNames,
            rawSectionOffset,
            exportDirectoryOffset,
            exportDllNameOffset,
            exportFunctionTableOffset,
            exportNamePointerTableOffset,
            exportOrdinalTableOffset,
            exportNameOffset);

        return bytes;
    }

    private static void WriteImports(
        byte[] bytes,
        IReadOnlyList<PeImportDllFixture> imports,
        int rawSectionOffset,
        int descriptorOffset,
        int lookupOffset,
        int addressOffset,
        int hintNameOffset,
        int dllNameOffset)
    {
        var lookupCursor = lookupOffset;
        var addressCursor = addressOffset;
        var hintNameCursor = hintNameOffset;
        var dllNameCursor = dllNameOffset;

        for (var dllIndex = 0; dllIndex < imports.Count; dllIndex++)
        {
            var import = imports[dllIndex];
            var currentDescriptorOffset = descriptorOffset + dllIndex * 20;
            WriteUInt32(bytes, currentDescriptorOffset, ToRva(lookupCursor, rawSectionOffset));
            WriteUInt32(bytes, currentDescriptorOffset + 12, ToRva(dllNameCursor, rawSectionOffset));
            WriteUInt32(bytes, currentDescriptorOffset + 16, ToRva(addressCursor, rawSectionOffset));
            WriteAsciiNullTerminated(bytes, ref dllNameCursor, import.DllName);

            for (var symbolIndex = 0; symbolIndex < import.Symbols.Count; symbolIndex++)
            {
                var symbol = import.Symbols[symbolIndex];
                uint thunkValue;
                if (symbol.Ordinal is ushort ordinal)
                {
                    thunkValue = 0x80000000U | ordinal;
                }
                else
                {
                    thunkValue = ToRva(hintNameCursor, rawSectionOffset);
                    WriteUInt16(bytes, hintNameCursor, symbol.Hint);
                    hintNameCursor += sizeof(ushort);
                    WriteAsciiNullTerminated(bytes, ref hintNameCursor, symbol.Name);
                }

                WriteUInt32(bytes, lookupCursor + symbolIndex * sizeof(uint), thunkValue);
                WriteUInt32(bytes, addressCursor + symbolIndex * sizeof(uint), thunkValue);
            }

            lookupCursor += (import.Symbols.Count + 1) * sizeof(uint);
            addressCursor += (import.Symbols.Count + 1) * sizeof(uint);
        }
    }

    private static void WriteExports(
        byte[] bytes,
        IReadOnlyList<string> exportNames,
        int rawSectionOffset,
        int exportDirectoryOffset,
        int exportDllNameOffset,
        int exportFunctionTableOffset,
        int exportNamePointerTableOffset,
        int exportOrdinalTableOffset,
        int exportNameOffset)
    {
        if (exportNames.Count == 0)
        {
            return;
        }

        var exportNameCursor = exportNameOffset;
        WriteUInt32(bytes, exportDirectoryOffset + 12, ToRva(exportDllNameOffset, rawSectionOffset));
        WriteUInt32(bytes, exportDirectoryOffset + 16, 1);
        WriteUInt32(bytes, exportDirectoryOffset + 20, (uint)exportNames.Count);
        WriteUInt32(bytes, exportDirectoryOffset + 24, (uint)exportNames.Count);
        WriteUInt32(bytes, exportDirectoryOffset + 28, ToRva(exportFunctionTableOffset, rawSectionOffset));
        WriteUInt32(bytes, exportDirectoryOffset + 32, ToRva(exportNamePointerTableOffset, rawSectionOffset));
        WriteUInt32(bytes, exportDirectoryOffset + 36, ToRva(exportOrdinalTableOffset, rawSectionOffset));
        var dllNameCursor = exportDllNameOffset;
        WriteAsciiNullTerminated(bytes, ref dllNameCursor, "fixture.dll");

        for (var index = 0; index < exportNames.Count; index++)
        {
            WriteUInt32(bytes, exportFunctionTableOffset + index * sizeof(uint), (uint)(0x1500 + index * 0x10));
            WriteUInt32(bytes, exportNamePointerTableOffset + index * sizeof(uint), ToRva(exportNameCursor, rawSectionOffset));
            WriteUInt16(bytes, exportOrdinalTableOffset + index * sizeof(ushort), (ushort)index);
            WriteAsciiNullTerminated(bytes, ref exportNameCursor, exportNames[index]);
        }
    }

    private static uint ToRva(int rawOffset, int rawSectionOffset)
    {
        return checked((uint)(0x1000 + rawOffset - rawSectionOffset));
    }

    private static void WriteAsciiNullTerminated(byte[] bytes, ref int offset, string value)
    {
        var nameBytes = Encoding.ASCII.GetBytes(value);
        nameBytes.CopyTo(bytes, offset);
        offset += nameBytes.Length;
        bytes[offset++] = 0;
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)), value);
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), value);
    }

    private static async Task WriteBytesAsync(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-client-pe-symbol-audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = CreateDataToolProcess(repositoryRoot);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool did not exit. stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static Process CreateDataToolProcess(string repositoryRoot)
    {
        var copiedDataTool = Path.Combine(AppContext.BaseDirectory, "WolfConquer.DataTool.dll");
        if (!File.Exists(copiedDataTool))
        {
            return TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add(copiedDataTool);
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        return process;
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var fromOutputDirectory = TryFindRepositoryRoot(AppContext.BaseDirectory);
        if (fromOutputDirectory is not null)
        {
            return fromOutputDirectory;
        }

        var fromWorkingDirectory = TryFindRepositoryRoot(Environment.CurrentDirectory);
        if (fromWorkingDirectory is not null)
        {
            return fromWorkingDirectory;
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (sourceDirectory is not null)
        {
            var fromSourceDirectory = TryFindRepositoryRoot(sourceDirectory);
            if (fromSourceDirectory is not null)
            {
                return fromSourceDirectory;
            }
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output or working directory.");
    }

    private static string? TryFindRepositoryRoot(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record PeImportDllFixture(string DllName, IReadOnlyList<PeImportSymbolFixture> Symbols);

    private sealed record PeImportSymbolFixture(string Name, ushort Hint, ushort? Ordinal)
    {
        public static PeImportSymbolFixture Named(string name, ushort hint)
        {
            return new PeImportSymbolFixture(name, hint, Ordinal: null);
        }

        public static PeImportSymbolFixture ByOrdinal(ushort ordinal)
        {
            return new PeImportSymbolFixture(string.Empty, Hint: 0, ordinal);
        }
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
