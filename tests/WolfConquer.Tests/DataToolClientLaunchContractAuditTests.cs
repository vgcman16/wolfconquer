using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolClientLaunchContractAuditTests
{
    private const string Usage =
        "WolfConquer.DataTool client-launch-contract-audit <oldClientRoot> <currentClientRoot> [--old-label label] [--current-label label] [--candidate relativePath] [...] [--workdir [label=]relativePath] [...] [--symbol-dll relativePath] [...] [--string text] [--no-default-strings] [--max-string-offsets N] [--max-import-dlls N]";

    [Fact]
    public async Task HelpMentionsClientLaunchContractAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(Usage, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task AuditComparesRootCacheDependenciesStringsAndSymbolsWithoutLaunching()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var oldRoot = Path.Combine(tempRoot, "old-client");
            var currentRoot = Path.Combine(tempRoot, "active-7899");
            var rootCache = "._cache_play.exe";
            var dx9Cache = Path.Combine("Env_DX9", "._cache_Conquer.exe");
            var dataThread = Path.Combine("Env_DX9", "DataThread.dll");

            await WriteBytesAsync(
                Path.Combine(oldRoot, rootCache),
                CreatePeFixture(
                    imports:
                    [
                        new PeImportDllFixture("kernel32.dll", [PeImportSymbolFixture.Named("ExitProcess", hint: 0)]),
                        new PeImportDllFixture("old-only.dll", [PeImportSymbolFixture.Named("OldOnly", hint: 1)]),
                    ],
                    exportNames: [],
                    asciiPayloads: ["CLuaVM::requirebuffer"]));
            await WriteBytesAsync(
                Path.Combine(currentRoot, rootCache),
                CreatePeFixture(
                    imports:
                    [
                        new PeImportDllFixture("kernel32.dll", [PeImportSymbolFixture.Named("ExitProcess", hint: 0)]),
                        new PeImportDllFixture("current-only.dll", [PeImportSymbolFixture.Named("CurrentOnly", hint: 1)]),
                    ],
                    exportNames: [],
                    asciiPayloads: ["ToStringEx"]));
            await WriteBytesAsync(
                Path.Combine(oldRoot, dx9Cache),
                CreatePeFixture(
                    imports:
                    [
                        new PeImportDllFixture(
                            "DataThread.dll",
                            [
                                PeImportSymbolFixture.Named("SharedEntry", hint: 1),
                                PeImportSymbolFixture.Named("OldOnlyEntry", hint: 2),
                            ]),
                    ],
                    exportNames: [],
                    asciiPayloads: ["GameInfo.ini"]));
            await WriteBytesAsync(
                Path.Combine(currentRoot, dx9Cache),
                CreatePeFixture(
                    imports:
                    [
                        new PeImportDllFixture(
                            "DataThread.dll",
                            [
                                PeImportSymbolFixture.Named("SharedEntry", hint: 1),
                                PeImportSymbolFixture.Named("CurrentPresentEntry", hint: 2),
                            ]),
                    ],
                    exportNames: [],
                    asciiPayloads: ["GameInfo.ini"]));
            await WriteBytesAsync(
                Path.Combine(oldRoot, dataThread),
                CreatePeFixture(imports: [], exportNames: ["SharedEntry"], asciiPayloads: []));
            await WriteBytesAsync(
                Path.Combine(currentRoot, dataThread),
                CreatePeFixture(imports: [], exportNames: ["SharedEntry", "CurrentPresentEntry"], asciiPayloads: []));

            var result = await RunDataToolAsync(
                "client-launch-contract-audit",
                oldRoot,
                currentRoot,
                "--old-label",
                "old1000",
                "--current-label",
                "active7899",
                "--candidate",
                rootCache,
                "--candidate",
                dx9Cache,
                "--symbol-dll",
                dataThread,
                "--no-default-strings",
                "--string",
                "CLuaVM::requirebuffer",
                "--string",
                "ToStringEx",
                "--max-string-offsets",
                "4",
                "--max-import-dlls",
                "16");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client launch contract audit:", result.StandardOutput);
            Assert.Contains("Mode: no-launch no-mutation bounded-root-compare", result.StandardOutput);
            Assert.Contains($"Old root: label=old1000 path={Path.GetFullPath(oldRoot)}", result.StandardOutput);
            Assert.Contains($"Current root: label=active7899 path={Path.GetFullPath(currentRoot)}", result.StandardOutput);
            Assert.Contains($"Candidate: {rootCache}", result.StandardOutput);
            Assert.Contains($"Candidate: {dx9Cache}", result.StandardOutput);
            Assert.Contains("comparison: sameExists=True sameSHA256=False sameImportDllSet=False sameStringPresence=False", result.StandardOutput);
            Assert.Contains("import=current-only.dll old1000=not-imported active7899=missing-local", result.StandardOutput);
            Assert.Contains("import=old-only.dll old1000=missing-local active7899=not-imported", result.StandardOutput);
            Assert.Contains("term=CLuaVM::requirebuffer encoding=ascii old1000=True active7899=False", result.StandardOutput);
            Assert.Contains("term=ToStringEx encoding=ascii old1000=False active7899=True", result.StandardOutput);
            Assert.Contains($"Symbol contract: candidate={dx9Cache} dll={dataThread} importDll=DataThread.dll", result.StandardOutput);
            Assert.Contains("old1000: exeExists=True dllExists=True status=ok importPresent=True matchedNamedImports=1 missingNamedImports=1 ordinalImports=0 exports=1", result.StandardOutput);
            Assert.Contains("active7899: exeExists=True dllExists=True status=ok importPresent=True matchedNamedImports=2 missingNamedImports=0 ordinalImports=0 exports=2", result.StandardOutput);
            Assert.Contains("sameMissingNames=False", result.StandardOutput);
            Assert.Contains("missing name=OldOnlyEntry old1000=True active7899=False", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CandidatePathsCannotEscapeClientRoots()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var oldRoot = Path.Combine(tempRoot, "old-client");
            var currentRoot = Path.Combine(tempRoot, "active-7899");
            Directory.CreateDirectory(oldRoot);
            Directory.CreateDirectory(currentRoot);

            var result = await RunDataToolAsync(
                "client-launch-contract-audit",
                oldRoot,
                currentRoot,
                "--candidate",
                "..\\outside.exe");

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Contains("client-launch-contract-audit --candidate cannot escape the client root.", result.StandardError);
            Assert.Contains("Usage: " + Usage, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static byte[] CreatePeFixture(
        IReadOnlyList<PeImportDllFixture> imports,
        IReadOnlyList<string> exportNames,
        IReadOnlyList<string> asciiPayloads)
    {
        const int peOffset = 0x80;
        const int optionalHeaderSize = 0xE0;
        const int rawSectionOffset = 0x200;
        const int rawSectionSize = 0x3000;
        const int importDescriptorOffset = 0x300;
        const int importLookupOffset = 0x500;
        const int importAddressOffset = 0x700;
        const int importNameOffset = 0x900;
        const int importDllNameOffset = 0xC00;
        const int exportDirectoryOffset = 0xF00;
        const int exportDllNameOffset = 0xF80;
        const int exportFunctionTableOffset = 0x1000;
        const int exportNamePointerTableOffset = 0x1080;
        const int exportOrdinalTableOffset = 0x1100;
        const int exportNameOffset = 0x1180;
        const int payloadOffset = 0x1800;

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
        WriteUInt32(bytes, optionalOffset + 56, 0x4000);
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
        WriteAsciiPayloads(bytes, payloadOffset, asciiPayloads);

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

    private static void WriteAsciiPayloads(byte[] bytes, int payloadOffset, IReadOnlyList<string> payloads)
    {
        var cursor = payloadOffset;
        foreach (var payload in payloads)
        {
            var payloadBytes = Encoding.ASCII.GetBytes(payload);
            payloadBytes.CopyTo(bytes, cursor);
            cursor += payloadBytes.Length + 1;
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
            "wolfconquer-client-launch-contract-audit-" + Guid.NewGuid().ToString("N"));
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
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
