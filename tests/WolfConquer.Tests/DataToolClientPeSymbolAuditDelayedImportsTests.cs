using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolClientPeSymbolAuditDelayedImportsTests
{
    private const int OptionalHeaderOffset = 0x98;
    private const int RawSectionOffset = 0x200;
    private const int RawSectionEnd = 0x2200;
    private const int DelayDescriptorOffset = 0x400;
    private const int DelayLookupOffset = 0x800;
    private const int DelayAddressOffset = 0xA00;
    private const uint ImageBase32 = 0x400000;

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ReadsNamedAndOrdinalDelayImportsFromActualPeBytes(bool legacyVa, bool pe32Plus)
    {
        var bytes = CreatePeFixture(
            normal: [],
            delayed: [new ImportDll("TQPlat.dll", [Symbol.Ordinal(21), Symbol.Named("DecodeItemType", 7)])],
            legacyVa,
            pe32Plus);

        var result = await RunFixtureAsync(bytes, "--import-dll", "tqplat.DLL");

        AssertSuccess(result);
        Assert.Contains("Mode: no-launch no-mutation", result.StandardOutput);
        Assert.Contains($"Executable PE: valid=True machine={(pe32Plus ? "Amd64" : "I386")}", result.StandardOutput);
        Assert.Contains("Imports from tqplat.DLL: present=True total=2 named=1 ordinal=1 truncated=False", result.StandardOutput);
        Assert.Contains("  import ordinal=21", result.StandardOutput);
        Assert.Contains("  import name=DecodeItemType hint=7", result.StandardOutput);
    }

    [Fact]
    public async Task MergesAllMatchingNormalAndDelayDescriptorsWithoutDeduplicatingImportedLocations()
    {
        var bytes = CreatePeFixture(
            normal: [new ImportDll("TQPlat.dll", [Symbol.Ordinal(21), Symbol.Named("SharedFunction", 1)])],
            delayed:
            [
                new ImportDll("TQPlat.dll", [Symbol.Ordinal(21), Symbol.Named("DelayFunction", 2)]),
                new ImportDll("tqplat.DLL", [Symbol.Ordinal(9)]),
                new ImportDll("unrelated.dll", [Symbol.Ordinal(99)]),
            ]);

        var result = await RunFixtureAsync(bytes, "--import-dll", "TQPlat.dll");

        AssertSuccess(result);
        Assert.Contains("Imports from TQPlat.dll: present=True total=5 named=2 ordinal=3 truncated=False", result.StandardOutput);
        Assert.Equal(2, CountLines(result.StandardOutput, "  import ordinal=21"));
        Assert.Contains("  import ordinal=9", result.StandardOutput);
        Assert.Contains("  import name=SharedFunction hint=1", result.StandardOutput);
        Assert.Contains("  import name=DelayFunction hint=2", result.StandardOutput);
        Assert.DoesNotContain("  import ordinal=99", result.StandardOutput);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task ImportLimitIsOneBudgetAcrossNormalAndAllDelayedDescriptors(int limit)
    {
        var bytes = CreatePeFixture(
            normal: [new ImportDll("TQPlat.dll", [Symbol.Ordinal(1), Symbol.Ordinal(2)])],
            delayed:
            [
                new ImportDll("TQPlat.dll", [Symbol.Ordinal(3), Symbol.Ordinal(4)]),
                new ImportDll("TQPlat.dll", [Symbol.Ordinal(5)]),
            ]);

        var result = await RunFixtureAsync(bytes, "--import-dll", "TQPlat.dll", "--max-imports", limit.ToString());

        AssertSuccess(result);
        Assert.Contains($"Imports from TQPlat.dll: present=True total={limit} named=0 ordinal={limit} truncated=True", result.StandardOutput);
        Assert.Equal(limit, result.StandardOutput.Split('\n').Count(static line => line.StartsWith("  import ordinal=", StringComparison.Ordinal)));
        Assert.DoesNotContain("  import ordinal=5", result.StandardOutput);
    }

    [Fact]
    public async Task CompareDoesNotCallAnAbsentImportDllAnEmptySuccessfulMatch()
    {
        var bytes = CreatePeFixture(
            normal: [],
            delayed: [new ImportDll("unrelated.dll", [Symbol.Ordinal(21)])]);

        var result = await RunFixtureAsync(bytes, "--import-dll", "TQPlat.dll", "--compare", "--shim-summary");

        AssertSuccess(result);
        Assert.Contains("Imports from TQPlat.dll: present=False total=0 named=0 ordinal=0 truncated=False", result.StandardOutput);
        Assert.Contains("Compare: enabled=True status=skipped", result.StandardOutput);
        Assert.Contains("reason=import-dll-not-present", result.StandardOutput);
        Assert.DoesNotContain("Compare: enabled=True status=ok", result.StandardOutput);
        Assert.Contains("Shim plan: enabled=True status=skipped", result.StandardOutput);
    }

    [Fact]
    public async Task RejectsPe32PlusNameThunkReservedBitEvenWhenTheHighRvaHasRawBacking()
    {
        var bytes = CreatePeFixture(
            normal: [],
            delayed: [new ImportDll("TQPlat.dll", [Symbol.Named("OriginalLowRvaName", 7)])],
            pe32Plus: true);
        const uint highNameRva = 0x80003000;
        const int highNameRawOffset = 0x1800;
        var firstSection = OptionalHeaderOffset + 0xF0;
        var highSection = firstSection + 40;
        WriteUInt16(bytes, 0x86, 2);
        WriteUInt32(bytes, OptionalHeaderOffset + 56, 0x80004000);
        // Keep the ordinary directory/INT/IAT and DLL name in the first section.
        // The second, nonoverlapping raw range makes the malformed name RVA map
        // successfully, so only the reserved-bit guard can reject this fixture.
        WriteUInt32(bytes, firstSection + 8, highNameRawOffset - RawSectionOffset);
        WriteUInt32(bytes, firstSection + 16, highNameRawOffset - RawSectionOffset);
        Encoding.ASCII.GetBytes(".high\0\0\0").CopyTo(bytes, highSection);
        WriteUInt32(bytes, highSection + 8, 0x200);
        WriteUInt32(bytes, highSection + 12, highNameRva);
        WriteUInt32(bytes, highSection + 16, 0x200);
        WriteUInt32(bytes, highSection + 20, highNameRawOffset);
        WriteUInt32(bytes, highSection + 36, 0x40000040);
        WriteUInt16(bytes, highNameRawOffset, 7);
        var nameCursor = highNameRawOffset + 2;
        WriteAscii(bytes, ref nameCursor, "RawBackedButReservedRva");
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(DelayLookupOffset, 8), highNameRva);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(DelayAddressOffset, 8), highNameRva);

        var result = await RunFixtureAsync(bytes, "--import-dll", "TQPlat.dll", "--compare");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("Client PE symbol audit failed:", result.StandardError);
        Assert.Contains("reserved", result.StandardError);
        Assert.DoesNotContain("range-unmapped", result.StandardError);
        Assert.DoesNotContain("Unhandled exception", result.StandardError);
    }

    [Theory]
    [InlineData("directory-unmapped")]
    [InlineData("directory-too-short")]
    [InlineData("descriptor-crosses-file")]
    [InlineData("descriptor-terminator-outside-directory")]
    [InlineData("unknown-attributes")]
    [InlineData("dll-name-unmapped")]
    [InlineData("lookup-unmapped")]
    [InlineData("address-unmapped")]
    [InlineData("lookup-missing")]
    [InlineData("address-missing")]
    [InlineData("address-ended-before-lookup")]
    [InlineData("address-nonzero-after-lookup-terminator")]
    [InlineData("hint-name-unmapped")]
    [InlineData("lookup-terminator-outside-section")]
    [InlineData("address-terminator-outside-section")]
    [InlineData("legacy-va-below-image-base")]
    public async Task MalformedDelayImportsFailClosedRatherThanReportingAbsentOrEmpty(string malformed)
    {
        var legacyVa = malformed == "legacy-va-below-image-base";
        var bytes = CreatePeFixture(
            normal: [],
            delayed: [new ImportDll("TQPlat.dll", [Symbol.Ordinal(21)])],
            legacyVa);
        var directoryOffset = OptionalHeaderOffset + 96 + 13 * 8;
        switch (malformed)
        {
            case "directory-unmapped":
                WriteUInt32(bytes, directoryOffset, 0xFFFFFF00);
                break;
            case "directory-too-short":
                WriteUInt32(bytes, directoryOffset + 4, 31);
                break;
            case "descriptor-crosses-file":
                bytes.AsSpan(DelayDescriptorOffset, 16).CopyTo(bytes.AsSpan(RawSectionEnd - 16, 16));
                WriteUInt32(bytes, directoryOffset, ToRva(RawSectionEnd - 16));
                break;
            case "descriptor-terminator-outside-directory":
                WriteUInt32(bytes, directoryOffset + 4, 32);
                break;
            case "unknown-attributes":
                WriteUInt32(bytes, DelayDescriptorOffset, 3);
                break;
            case "dll-name-unmapped":
                WriteUInt32(bytes, DelayDescriptorOffset + 4, 0xFFFFFF00);
                break;
            case "lookup-unmapped":
                WriteUInt32(bytes, DelayDescriptorOffset + 16, 0xFFFFFF00);
                break;
            case "address-unmapped":
                WriteUInt32(bytes, DelayDescriptorOffset + 12, 0xFFFFFF00);
                break;
            case "lookup-missing":
                WriteUInt32(bytes, DelayDescriptorOffset + 16, 0);
                break;
            case "address-missing":
                WriteUInt32(bytes, DelayDescriptorOffset + 12, 0);
                break;
            case "address-ended-before-lookup":
                WriteUInt32(bytes, DelayAddressOffset, 0);
                break;
            case "address-nonzero-after-lookup-terminator":
                WriteUInt32(bytes, DelayAddressOffset + 4, 0x80000016);
                break;
            case "hint-name-unmapped":
                WriteUInt32(bytes, DelayLookupOffset, 0x7FFFFFF0);
                break;
            case "lookup-terminator-outside-section":
                WriteUInt32(bytes, DelayDescriptorOffset + 16, ToRva(RawSectionEnd - 4));
                WriteUInt32(bytes, RawSectionEnd - 4, 0x80000015);
                break;
            case "address-terminator-outside-section":
                WriteUInt32(bytes, DelayDescriptorOffset + 12, ToRva(RawSectionEnd - 4));
                WriteUInt32(bytes, RawSectionEnd - 4, 0x80000015);
                break;
            case "legacy-va-below-image-base":
                WriteUInt32(bytes, DelayDescriptorOffset + 4, ImageBase32 - 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(malformed));
        }

        var result = await RunFixtureAsync(bytes, "--import-dll", "TQPlat.dll", "--compare");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("Client PE symbol audit failed:", result.StandardError);
        Assert.DoesNotContain("Unhandled exception", result.StandardError);
    }

    private static byte[] CreatePeFixture(
        IReadOnlyList<ImportDll> normal,
        IReadOnlyList<ImportDll> delayed,
        bool legacyVa = false,
        bool pe32Plus = false)
    {
        Assert.False(legacyVa && pe32Plus, "The legacy descriptor's DWORD VAs cannot represent the 64-bit fixture image base.");
        var bytes = new byte[RawSectionEnd];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        WriteUInt32(bytes, 0x3C, 0x80);
        WriteUInt32(bytes, 0x80, 0x00004550);
        WriteUInt16(bytes, 0x84, (ushort)(pe32Plus ? 0x8664 : 0x014C));
        WriteUInt16(bytes, 0x86, 1);
        var optionalSize = pe32Plus ? 0xF0 : 0xE0;
        WriteUInt16(bytes, 0x94, (ushort)optionalSize);
        WriteUInt16(bytes, OptionalHeaderOffset, (ushort)(pe32Plus ? 0x20B : 0x10B));
        WriteUInt32(bytes, OptionalHeaderOffset + 16, 0x1000);
        if (pe32Plus)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(OptionalHeaderOffset + 24, 8), 0x140000000);
        }
        else
        {
            WriteUInt32(bytes, OptionalHeaderOffset + 20, 0x1000);
            WriteUInt32(bytes, OptionalHeaderOffset + 24, 0x2000);
            WriteUInt32(bytes, OptionalHeaderOffset + 28, ImageBase32);
        }

        WriteUInt32(bytes, OptionalHeaderOffset + 32, 0x1000);
        WriteUInt32(bytes, OptionalHeaderOffset + 36, 0x200);
        WriteUInt32(bytes, OptionalHeaderOffset + 56, 0x3000);
        WriteUInt32(bytes, OptionalHeaderOffset + 60, RawSectionOffset);
        WriteUInt16(bytes, OptionalHeaderOffset + 68, 2);
        WriteUInt32(bytes, OptionalHeaderOffset + (pe32Plus ? 108 : 92), 16);
        var directories = OptionalHeaderOffset + (pe32Plus ? 112 : 96);
        if (normal.Count > 0)
        {
            WriteUInt32(bytes, directories + 8, ToRva(0x300));
            WriteUInt32(bytes, directories + 12, checked((uint)((normal.Count + 1) * 20)));
        }

        if (delayed.Count > 0)
        {
            WriteUInt32(bytes, directories + 13 * 8, ToRva(DelayDescriptorOffset));
            WriteUInt32(bytes, directories + 13 * 8 + 4, checked((uint)((delayed.Count + 1) * 32)));
        }

        var section = OptionalHeaderOffset + optionalSize;
        Encoding.ASCII.GetBytes(".rdata\0\0").CopyTo(bytes, section);
        WriteUInt32(bytes, section + 8, RawSectionEnd - RawSectionOffset);
        WriteUInt32(bytes, section + 12, 0x1000);
        WriteUInt32(bytes, section + 16, RawSectionEnd - RawSectionOffset);
        WriteUInt32(bytes, section + 20, RawSectionOffset);
        WriteUInt32(bytes, section + 36, 0x40000040);

        var dllNameCursor = 0x1000;
        var hintNameCursor = 0x1200;
        WriteImportTables(normal, isDelayed: false, lookupStart: 0x600, addressStart: 0x700);
        WriteImportTables(delayed, isDelayed: true, lookupStart: DelayLookupOffset, addressStart: DelayAddressOffset);
        return bytes;

        void WriteImportTables(IReadOnlyList<ImportDll> imports, bool isDelayed, int lookupStart, int addressStart)
        {
            var width = pe32Plus ? 8 : 4;
            var lookupCursor = lookupStart;
            var addressCursor = addressStart;
            for (var index = 0; index < imports.Count; index++)
            {
                var import = imports[index];
                var descriptor = (isDelayed ? DelayDescriptorOffset : 0x300) + index * (isDelayed ? 32 : 20);
                if (isDelayed)
                {
                    WriteUInt32(bytes, descriptor, legacyVa ? 0U : 1U);
                    WriteUInt32(bytes, descriptor + 4, DescriptorPointer(dllNameCursor));
                    WriteUInt32(bytes, descriptor + 12, DescriptorPointer(addressCursor));
                    WriteUInt32(bytes, descriptor + 16, DescriptorPointer(lookupCursor));
                }
                else
                {
                    WriteUInt32(bytes, descriptor, ToRva(lookupCursor));
                    WriteUInt32(bytes, descriptor + 12, ToRva(dllNameCursor));
                    WriteUInt32(bytes, descriptor + 16, ToRva(addressCursor));
                }

                WriteAscii(bytes, ref dllNameCursor, import.DllName);
                foreach (var symbol in import.Symbols)
                {
                    ulong thunk;
                    if (symbol.OrdinalValue is ushort ordinal)
                    {
                        thunk = (pe32Plus ? 0x8000000000000000UL : 0x80000000UL) | ordinal;
                    }
                    else
                    {
                        thunk = isDelayed && legacyVa ? DescriptorPointer(hintNameCursor) : ToRva(hintNameCursor);
                        WriteUInt16(bytes, hintNameCursor, symbol.Hint);
                        hintNameCursor += 2;
                        WriteAscii(bytes, ref hintNameCursor, symbol.Name);
                    }

                    if (pe32Plus)
                    {
                        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(lookupCursor, 8), thunk);
                        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(addressCursor, 8), thunk);
                    }
                    else
                    {
                        WriteUInt32(bytes, lookupCursor, checked((uint)thunk));
                        WriteUInt32(bytes, addressCursor, checked((uint)thunk));
                    }

                    lookupCursor += width;
                    addressCursor += width;
                }

                lookupCursor += width;
                addressCursor += width;
            }
        }

        uint DescriptorPointer(int offset) => legacyVa ? checked(ImageBase32 + ToRva(offset)) : ToRva(offset);
    }

    private static async Task<ConsoleResult> RunFixtureAsync(byte[] bytes, params string[] options)
    {
        var root = Path.Combine(Path.GetTempPath(), "wolfconquer-delay-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var exePath = Path.Combine(root, "fixture.exe");
            var dllPath = Path.Combine(root, "donor.dll");
            var donor = CreatePeFixture([], []);
            await File.WriteAllBytesAsync(exePath, bytes);
            await File.WriteAllBytesAsync(dllPath, donor);
            using var process = TestProjectProcess.Create(FindRepositoryRoot(), "WolfConquer.DataTool");
            process.StartInfo.ArgumentList.Add("client-pe-symbol-audit");
            process.StartInfo.ArgumentList.Add(exePath);
            foreach (var option in options)
            {
                process.StartInfo.ArgumentList.Add(option);
            }

            if (options.Contains("--compare") || options.Contains("--shim-summary"))
            {
                process.StartInfo.ArgumentList.Add("--dll");
                process.StartInfo.ArgumentList.Add(dllPath);
            }

            Assert.True(process.Start(), "Could not start DataTool.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                Assert.Fail($"DataTool did not exit. stdout: {await output} stderr: {await error}");
            }

            Assert.Equal(bytes, await File.ReadAllBytesAsync(exePath));
            Assert.Equal(donor, await File.ReadAllBytesAsync(dllPath));
            return new ConsoleResult(process.ExitCode, await output, await error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertSuccess(ConsoleResult result)
    {
        Assert.True(result.ExitCode == 0, $"Exit {result.ExitCode}. stdout: {result.StandardOutput} stderr: {result.StandardError}");
        Assert.Equal(string.Empty, result.StandardError);
    }

    private static int CountLines(string value, string line) =>
        value.Split('\n').Count(candidate => string.Equals(candidate.TrimEnd('\r'), line, StringComparison.Ordinal));

    private static uint ToRva(int rawOffset) => checked((uint)(0x1000 + rawOffset - RawSectionOffset));

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private static void WriteAscii(byte[] bytes, ref int offset, string value)
    {
        var encoded = Encoding.ASCII.GetBytes(value);
        encoded.CopyTo(bytes, offset);
        offset += encoded.Length;
        bytes[offset++] = 0;
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(sourcePath)!); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln.");
    }

    private sealed record ImportDll(string DllName, IReadOnlyList<Symbol> Symbols);

    private sealed record Symbol(string Name, ushort Hint, ushort? OrdinalValue)
    {
        public static Symbol Named(string name, ushort hint) => new(name, hint, null);
        public static Symbol Ordinal(ushort value) => new(string.Empty, 0, value);
    }

    private sealed record ConsoleResult(int ExitCode, string StandardOutput, string StandardError);
}
