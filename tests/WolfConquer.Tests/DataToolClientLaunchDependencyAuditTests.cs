using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolClientLaunchDependencyAuditTests
{
    private const string Usage =
        "WolfConquer.DataTool client-launch-dependency-audit <exePath> --workdir label=path [--workdir label=path ...] [--client-root path] [--max-import-dlls N] [--recursive] [--max-recursive-depth N]";

    [Fact]
    public async Task HelpMentionsClientLaunchDependencyAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(Usage, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task AuditReportsSystemAndLocalImportResolutionWithoutLaunching()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var exeDir = Path.Combine(tempRoot, "exe");
            var workDir = Path.Combine(tempRoot, "work");
            var otherWorkDir = Path.Combine(tempRoot, "other-work");
            var clientRoot = Path.Combine(tempRoot, "client-root");
            var exePath = Path.Combine(exeDir, "play.exe");

            await WriteBytesAsync(
                exePath,
                CreateMinimalPeFixture(
                    "kernel32.dll",
                    "local-a.dll",
                    "work-only.dll",
                    "client-only.dll",
                    "missing-plugin.dll"));
            await WriteBytesAsync(Path.Combine(exeDir, "local-a.dll"), "local fixture");
            await WriteBytesAsync(Path.Combine(workDir, "work-only.dll"), "work fixture");
            await WriteBytesAsync(Path.Combine(clientRoot, "client-only.dll"), "client fixture");
            Directory.CreateDirectory(otherWorkDir);

            var result = await RunDataToolAsync(
                "client-launch-dependency-audit",
                exePath,
                "--workdir",
                "work=" + workDir,
                "--workdir",
                "other=" + otherWorkDir,
                "--client-root",
                clientRoot,
                "--max-import-dlls",
                "16");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client launch dependency audit:", result.StandardOutput);
            Assert.Contains("Mode: no-launch import-resolution", result.StandardOutput);
            Assert.Contains($"Executable: {Path.GetFullPath(exePath)}", result.StandardOutput);
            Assert.Contains($"Executable directory: {Path.GetFullPath(exeDir)}", result.StandardOutput);
            Assert.Contains($"Client root: {Path.GetFullPath(clientRoot)}", result.StandardOutput);
            Assert.Contains("Working directories: 2", result.StandardOutput);
            Assert.Contains($"  work={Path.GetFullPath(workDir)}", result.StandardOutput);
            Assert.Contains($"  other={Path.GetFullPath(otherWorkDir)}", result.StandardOutput);
            Assert.Contains("PE: valid=True machine=I386 subsystem=WindowsGui importDllCount=5", result.StandardOutput);
            Assert.Contains("Local unresolved imports: 1", result.StandardOutput);
            Assert.Contains("Local resolved imports: 3", result.StandardOutput);
            Assert.Contains("System imports: 1", result.StandardOutput);
            Assert.Contains("Recursive local dependency audit: enabled=False maxDepth=3", result.StandardOutput);
            Assert.Contains("client-only.dll: status=resolved-local system=False exeDir=False clientRoot=True workdirs=work:False,other:False", result.StandardOutput);
            Assert.Contains("local-a.dll: status=resolved-local system=False exeDir=True clientRoot=False workdirs=work:False,other:False", result.StandardOutput);
            Assert.Contains("work-only.dll: status=resolved-local system=False exeDir=False clientRoot=False workdirs=work:True,other:False", result.StandardOutput);
            Assert.Contains("missing-plugin.dll: status=missing-local system=False exeDir=False clientRoot=False workdirs=work:False,other:False", result.StandardOutput);
            Assert.Contains("kernel32.dll: status=system system=True exeDir=False clientRoot=False workdirs=work:False,other:False", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task AuditClassifiesKnownWindowsAndDirectXImportsAsSystemWithoutMaskingMissingLocalDlls()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var exeDir = Path.Combine(tempRoot, "exe");
            var workDir = Path.Combine(tempRoot, "work");
            var exePath = Path.Combine(exeDir, "play.exe");

            await WriteBytesAsync(
                exePath,
                CreateMinimalPeFixture(
                    "d3d8.dll",
                    "RPCRT4.dll",
                    "USP10.dll",
                    "wolfconquer-missing-game-hook.dll"));
            Directory.CreateDirectory(workDir);

            var result = await RunDataToolAsync(
                "client-launch-dependency-audit",
                exePath,
                "--workdir",
                "work=" + workDir,
                "--max-import-dlls",
                "16");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Local unresolved imports: 1", result.StandardOutput);
            Assert.Contains("Local resolved imports: 0", result.StandardOutput);
            Assert.Contains("System imports: 3", result.StandardOutput);
            Assert.Contains("d3d8.dll: status=system system=True exeDir=False clientRoot=False workdirs=work:False", result.StandardOutput);
            Assert.Contains("RPCRT4.dll: status=system system=True exeDir=False clientRoot=False workdirs=work:False", result.StandardOutput);
            Assert.Contains("USP10.dll: status=system system=True exeDir=False clientRoot=False workdirs=work:False", result.StandardOutput);
            Assert.Contains(
                "wolfconquer-missing-game-hook.dll: status=missing-local system=False exeDir=False clientRoot=False workdirs=work:False",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RecursiveAuditReportsTransitiveLocalImportGapsWithoutLaunching()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var exeDir = Path.Combine(tempRoot, "exe");
            var workDir = Path.Combine(tempRoot, "work");
            var exePath = Path.Combine(exeDir, "play.exe");
            var localDllPath = Path.Combine(exeDir, "local-a.dll");

            await WriteBytesAsync(exePath, CreateMinimalPeFixture("local-a.dll"));
            await WriteBytesAsync(
                localDllPath,
                CreateMinimalPeFixture(
                    "d3d8.dll",
                    "RPCRT4.dll",
                    "USP10.dll",
                    "missing-child.dll"));
            Directory.CreateDirectory(workDir);

            var result = await RunDataToolAsync(
                "client-launch-dependency-audit",
                exePath,
                "--workdir",
                "work=" + workDir,
                "--recursive",
                "--max-recursive-depth",
                "2");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Recursive local dependency audit: enabled=True maxDepth=2", result.StandardOutput);
            Assert.Contains("Recursive local files visited: 1", result.StandardOutput);
            Assert.Contains("Recursive local unresolved imports: 1", result.StandardOutput);
            Assert.Contains(
                $"recursive depth=1 owner={Path.GetFullPath(localDllPath)} import=missing-child.dll status=missing-local system=False resolved=<none>",
                result.StandardOutput);
            Assert.Contains(
                $"recursive depth=1 owner={Path.GetFullPath(localDllPath)} import=d3d8.dll status=system system=True resolved=<none>",
                result.StandardOutput);
            Assert.Contains(
                $"recursive depth=1 owner={Path.GetFullPath(localDllPath)} import=RPCRT4.dll status=system system=True resolved=<none>",
                result.StandardOutput);
            Assert.Contains(
                $"recursive depth=1 owner={Path.GetFullPath(localDllPath)} import=USP10.dll status=system system=True resolved=<none>",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RecursiveAuditReportsOrdinalOnlyExportSatisfactionAndMissingOrdinals()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var satisfiedDir = Path.Combine(tempRoot, "satisfied");
            var missingDir = Path.Combine(tempRoot, "missing");
            var workDir = Path.Combine(tempRoot, "work");
            Directory.CreateDirectory(workDir);

            var satisfiedExePath = await WriteOrdinalDependencyFixtureAsync(
                satisfiedDir,
                PeExportFixture.OrdinalOnly(4));
            var missingExePath = await WriteOrdinalDependencyFixtureAsync(
                missingDir,
                PeExportFixture.OrdinalOnly(1));

            var satisfied = await RunDataToolAsync(
                "client-launch-dependency-audit",
                satisfiedExePath,
                "--workdir",
                "work=" + workDir,
                "--recursive",
                "--max-recursive-depth",
                "2",
                "--max-import-dlls",
                "16");
            var missing = await RunDataToolAsync(
                "client-launch-dependency-audit",
                missingExePath,
                "--workdir",
                "work=" + workDir,
                "--recursive",
                "--max-recursive-depth",
                "2",
                "--max-import-dlls",
                "16");

            Assert.Equal(0, satisfied.ExitCode);
            Assert.Contains("Recursive local symbol mismatches: 0", satisfied.StandardOutput);
            Assert.Contains(
                "import=license.dll status=resolved-local system=False " +
                $"resolved={Path.GetFullPath(Path.Combine(satisfiedDir, "license.dll"))}" +
                " symbols=ok matched=0 missing=0 ordinal=1 ordinalMatched=1 ordinalOnlyMatched=1 ordinalMissing=0",
                satisfied.StandardOutput);
            Assert.Equal(string.Empty, satisfied.StandardError);

            Assert.Equal(0, missing.ExitCode);
            Assert.Contains("Recursive local symbol mismatches: 1", missing.StandardOutput);
            Assert.Contains(
                "import=license.dll status=resolved-local system=False " +
                $"resolved={Path.GetFullPath(Path.Combine(missingDir, "license.dll"))}" +
                " symbols=missing-ordinal-imports matched=0 missing=0 ordinal=1 ordinalMatched=0 ordinalOnlyMatched=0 ordinalMissing=1 missingOrdinals=4",
                missing.StandardOutput);
            Assert.Equal(string.Empty, missing.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task MissingWorkdirReturnsUsageAndExitOne()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var exePath = Path.Combine(tempRoot, "play.exe");
            await WriteBytesAsync(exePath, CreateMinimalPeFixture("local-a.dll"));

            var result = await RunDataToolAsync("client-launch-dependency-audit", exePath);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Contains("client-launch-dependency-audit requires at least one --workdir label=path.", result.StandardError);
            Assert.Contains("Usage: " + Usage, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static byte[] CreateMinimalPeFixture(params string[] importDllNames)
    {
        const int peOffset = 0x80;
        const int optionalHeaderSize = 0xE0;
        const int rawSectionOffset = 0x200;
        const int importDescriptorOffset = 0x300;
        const int importNameOffset = 0x400;

        var bytes = new byte[0x1000];
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
        WriteUInt32(bytes, optionalOffset + 56, 0x2000);
        WriteUInt32(bytes, optionalOffset + 60, 0x200);
        WriteUInt16(bytes, optionalOffset + 68, 2);
        WriteUInt32(bytes, optionalOffset + 92, 16);
        WriteUInt32(bytes, optionalOffset + 104, ToRva(importDescriptorOffset, rawSectionOffset));
        WriteUInt32(bytes, optionalOffset + 108, (uint)((importDllNames.Length + 1) * 20));

        var sectionOffset = optionalOffset + optionalHeaderSize;
        Encoding.ASCII.GetBytes(".rdata\0\0").CopyTo(bytes, sectionOffset);
        WriteUInt32(bytes, sectionOffset + 8, 0x800);
        WriteUInt32(bytes, sectionOffset + 12, 0x1000);
        WriteUInt32(bytes, sectionOffset + 16, 0x800);
        WriteUInt32(bytes, sectionOffset + 20, rawSectionOffset);

        var nameOffset = importNameOffset;
        for (var index = 0; index < importDllNames.Length; index++)
        {
            var descriptorOffset = importDescriptorOffset + index * 20;
            WriteUInt32(bytes, descriptorOffset + 12, ToRva(nameOffset, rawSectionOffset));
            var nameBytes = Encoding.ASCII.GetBytes(importDllNames[index] + "\0");
            nameBytes.CopyTo(bytes, nameOffset);
            nameOffset += nameBytes.Length;
        }

        return bytes;
    }

    private static async Task<string> WriteOrdinalDependencyFixtureAsync(
        string directory,
        params PeExportFixture[] licenseExports)
    {
        var exePath = Path.Combine(directory, "play.exe");
        await WriteBytesAsync(
            exePath,
            CreatePeFixture(
                [new PeImportDllFixture("DataThread.dll", [])],
                []));
        await WriteBytesAsync(
            Path.Combine(directory, "DataThread.dll"),
            CreatePeFixture(
                [new PeImportDllFixture("license.dll", [PeImportSymbolFixture.ByOrdinal(4)])],
                []));
        await WriteBytesAsync(
            Path.Combine(directory, "license.dll"),
            CreatePeFixture(
                [],
                licenseExports));

        return exePath;
    }

    private static byte[] CreatePeFixture(
        IReadOnlyList<PeImportDllFixture> imports,
        IReadOnlyList<PeExportFixture> exports)
    {
        const int peOffset = 0x80;
        const int optionalHeaderSize = 0xE0;
        const int rawSectionOffset = 0x200;
        const int rawSectionSize = 0x1800;
        const int exportDirectoryOffset = 0x300;
        const int exportDllNameOffset = 0x360;
        const int exportFunctionTableOffset = 0x380;
        const int exportNamePointerTableOffset = 0x420;
        const int exportOrdinalTableOffset = 0x4A0;
        const int exportNameOffset = 0x500;
        const int importDescriptorOffset = 0x700;
        const int importLookupOffset = 0x780;
        const int importAddressOffset = 0x880;
        const int importHintNameOffset = 0x980;
        const int importDllNameOffset = 0xB00;

        var bytes = new byte[0x2000];
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
        if (exports.Count > 0)
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
            importHintNameOffset,
            importDllNameOffset);
        WriteExports(
            bytes,
            exports,
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
        IReadOnlyList<PeExportFixture> exports,
        int rawSectionOffset,
        int exportDirectoryOffset,
        int exportDllNameOffset,
        int exportFunctionTableOffset,
        int exportNamePointerTableOffset,
        int exportOrdinalTableOffset,
        int exportNameOffset)
    {
        if (exports.Count == 0)
        {
            return;
        }

        const ushort ordinalBase = 1;
        var namedExports = exports
            .Where(static export => export.Name.Length > 0)
            .OrderBy(static export => export.Name, StringComparer.Ordinal)
            .ToArray();
        var functionCount = exports.Max(static export => export.Ordinal) - ordinalBase + 1;
        var exportNameCursor = exportNameOffset;
        WriteUInt32(bytes, exportDirectoryOffset + 12, ToRva(exportDllNameOffset, rawSectionOffset));
        WriteUInt32(bytes, exportDirectoryOffset + 16, ordinalBase);
        WriteUInt32(bytes, exportDirectoryOffset + 20, (uint)functionCount);
        WriteUInt32(bytes, exportDirectoryOffset + 24, (uint)namedExports.Length);
        WriteUInt32(bytes, exportDirectoryOffset + 28, ToRva(exportFunctionTableOffset, rawSectionOffset));
        WriteUInt32(bytes, exportDirectoryOffset + 32, ToRva(exportNamePointerTableOffset, rawSectionOffset));
        WriteUInt32(bytes, exportDirectoryOffset + 36, ToRva(exportOrdinalTableOffset, rawSectionOffset));
        var dllNameCursor = exportDllNameOffset;
        WriteAsciiNullTerminated(bytes, ref dllNameCursor, "fixture.dll");

        foreach (var export in exports)
        {
            var ordinalIndex = export.Ordinal - ordinalBase;
            WriteUInt32(bytes, exportFunctionTableOffset + ordinalIndex * sizeof(uint), (uint)(0x1500 + export.Ordinal * 0x10));
        }

        for (var index = 0; index < namedExports.Length; index++)
        {
            var export = namedExports[index];
            WriteUInt32(bytes, exportNamePointerTableOffset + index * sizeof(uint), ToRva(exportNameCursor, rawSectionOffset));
            WriteUInt16(bytes, exportOrdinalTableOffset + index * sizeof(ushort), (ushort)(export.Ordinal - ordinalBase));
            WriteAsciiNullTerminated(bytes, ref exportNameCursor, export.Name);
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

    private static async Task WriteBytesAsync(string path, string contents)
    {
        await WriteBytesAsync(path, Encoding.ASCII.GetBytes(contents));
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
            "wolfconquer-client-launch-dependency-audit-" + Guid.NewGuid().ToString("N"));
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
        public static PeImportSymbolFixture ByOrdinal(ushort ordinal)
        {
            return new PeImportSymbolFixture(string.Empty, Hint: 0, ordinal);
        }
    }

    private sealed record PeExportFixture(ushort Ordinal, string Name)
    {
        public static PeExportFixture OrdinalOnly(ushort ordinal)
        {
            return new PeExportFixture(ordinal, string.Empty);
        }
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
