using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WolfConquer.Tests;

public sealed partial class DataToolClientPeDependencyAuditTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NormalThenDelayDescriptorsRetainSourceOrderDuplicatesAndOriginalCase(bool plus)
    {
        byte[] bytes = Image(["TQPlat.dll", "KERNEL32.dll", "TQPlat.dll", "extensionless", "winspool.drv"],
            ["tqplat.DLL", "graphic.dll", "internal space.lib", "-safe+name"], plus);
        using var f = new AuditFixture(bytes);
        using var json = Success(await f.Run(f.Path));
        var root = json.RootElement;
        Keys(root, "schemaVersion", "path", "length", "sha256", "peKind", "modules", "limitation");
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(f.Path, root.GetProperty("path").GetString());
        Assert.Equal(bytes.Length, root.GetProperty("length").GetInt64());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), root.GetProperty("sha256").GetString(), ignoreCase: true);
        Assert.Equal(plus ? "PE32+" : "PE32", root.GetProperty("peKind").GetString());
        var modules = root.GetProperty("modules").EnumerateArray().ToArray();
        Assert.Equal(new[] { "TQPlat.dll", "KERNEL32.dll", "TQPlat.dll", "extensionless", "winspool.drv", "tqplat.DLL", "graphic.dll", "internal space.lib", "-safe+name" },
            modules.Select(m => m.GetProperty("name").GetString()));
        Assert.Equal(new[] { "normal", "normal", "normal", "normal", "normal", "delay", "delay", "delay", "delay" }, modules.Select(m => m.GetProperty("kind").GetString()));
        foreach (var module in modules) Keys(module, "name", "kind");
        string limitation = root.GetProperty("limitation").GetString()!;
        Assert.Contains("static", limitation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("runtime", limitation, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyVaDelayDescriptorUsesActualImageBaseIncludingLowBasePe32Plus(bool plus)
    {
        using var f = new AuditFixture(Image(delayed: ["LegacyVA.dll"], plus: plus, legacyVa: true));
        using var json = Success(await f.Run(f.Path));
        var module = Assert.Single(json.RootElement.GetProperty("modules").EnumerateArray());
        Assert.Equal("LegacyVA.dll", module.GetProperty("name").GetString());
        Assert.Equal("delay", module.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task AbsentNormalAndDelayedDirectoriesAreAnExplicitEmptyArrayForBothPeKinds()
    {
        foreach (bool plus in new[] { false, true })
        {
            using var f = new AuditFixture(Image(plus: plus));
            using var json = Success(await f.Run(f.Path));
            Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("modules").ValueKind);
            Assert.Empty(json.RootElement.GetProperty("modules").EnumerateArray());
        }
    }

    [Fact]
    public async Task NamesOnlyAuditDoesNotDereferenceNormalOrDelayedSymbolTables()
    {
        byte[] bytes = Image(["Normal.dll"], ["Delayed.dll"]);
        U32(bytes, Normal, uint.MaxValue); U32(bytes, Normal + 16, uint.MaxValue);
        U32(bytes, Delay + 12, uint.MaxValue); U32(bytes, Delay + 16, uint.MaxValue);
        using var f = new AuditFixture(bytes);
        using var json = Success(await f.Run(f.Path));
        Assert.Equal(2, json.RootElement.GetProperty("modules").GetArrayLength());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirstNullDescriptorTerminatesArrayAndRemainingDeclaredSlotsAreNotModules(bool delayed)
    {
        byte[] bytes = delayed ? Image(delayed: ["Kept.dll"]) : Image(["Kept.dll"]);
        int width = delayed ? 32 : 20, table = delayed ? Delay : Normal;
        int directory = Directories(false) + (delayed ? 13 * 8 : 8);
        U32(bytes, directory + 4, (uint)(3 * width + 7)); // Real PE directory sizes may include unaligned trailing padding.
        Array.Copy(bytes, table, bytes, table + 2 * width, width);
        using var f = new AuditFixture(bytes);
        using var json = Success(await f.Run(f.Path));
        Assert.Single(json.RootElement.GetProperty("modules").EnumerateArray());
    }

    [Theory]
    [InlineData("dos")]
    [InlineData("pe")]
    [InlineData("optional-truncated")]
    [InlineData("raw-range")]
    [InlineData("virtual-range")]
    [InlineData("sections-overlap")]
    [InlineData("normal-zero-pair")]
    [InlineData("delay-zero-pair")]
    [InlineData("normal-truncated")]
    [InlineData("delay-truncated")]
    [InlineData("normal-no-terminator")]
    [InlineData("delay-no-terminator")]
    [InlineData("delay-reserved-attributes")]
    [InlineData("delay-va-underflow")]
    public async Task MalformedSignaturesSectionsAndDescriptorRangesFailClosed(string kind)
    {
        byte[] bytes = Image(["Normal.dll"], ["Delayed.dll"]);
        int dirs = Directories(false);
        switch (kind)
        {
            case "dos": bytes[0] = 0; break;
            case "pe": bytes[0x80] = 0; break;
            case "optional-truncated": U16(bytes, 0x94, ushort.MaxValue); break;
            case "raw-range": U32(bytes, Section(false) + 20, 0x1500); break;
            case "virtual-range": U32(bytes, Section(false) + 12, 0xFFFFFFF0); break;
            case "sections-overlap": U16(bytes, 0x86, 2); Array.Copy(bytes, Section(false), bytes, Section(false) + 40, 40); break;
            case "normal-zero-pair": U32(bytes, dirs + 8, 0); break;
            case "delay-zero-pair": U32(bytes, dirs + 13 * 8 + 4, 0); break;
            case "normal-truncated": U32(bytes, dirs + 12, 39); break;
            case "delay-truncated": U32(bytes, dirs + 13 * 8 + 4, 63); break;
            case "normal-no-terminator": U32(bytes, dirs + 12, 20); break;
            case "delay-no-terminator": U32(bytes, dirs + 13 * 8 + 4, 32); break;
            case "delay-reserved-attributes": U32(bytes, Delay, 3); break;
            case "delay-va-underflow": U32(bytes, Delay, 0); U32(bytes, Delay + 4, 0x3FFFFF); break;
            default: throw new InvalidOperationException("Unknown independent fixture mutation.");
        }
        using var f = new AuditFixture(bytes);
        Failure(await f.Run(f.Path)); // Even the valid first module must not leak partial JSON.
    }

    [Theory]
    [InlineData("../escape.dll")]
    [InlineData("C:\\escape.dll")]
    [InlineData("folder/escape.dll")]
    [InlineData(" bad.dll")]
    [InlineData("CON.dll")]
    [InlineData("bad?.dll")]
    [InlineData("bad.dll.")]
    public async Task UnsafeOrNonBasenameModuleStringsAreRefused(string name)
    {
        using var f = new AuditFixture(Image([name]));
        Failure(await f.Run(f.Path));
    }

    [Fact]
    public async Task ModuleNameBoundIncludesItsNullTerminatorNotOnlyPrintableCharacters()
    {
        using (var accepted = new AuditFixture(Image([new string('a', 251) + ".dll"])))
        {
            using var json = Success(await accepted.Run(accepted.Path));
            Assert.Equal(255, Assert.Single(json.RootElement.GetProperty("modules").EnumerateArray()).GetProperty("name").GetString()!.Length);
        }
        using var refused = new AuditFixture(Image([new string('a', 252) + ".dll"]));
        Failure(await refused.Run(refused.Path)); // Both byte strings fit the same mapped section; only the bounded NUL differs.
    }

    [Theory]
    [InlineData("padding")]
    [InlineData("overlay")]
    [InlineData("cross-section")]
    [InlineData("unterminated")]
    public async Task ModuleStringsMustTerminateInsideOneLogicalRawBackedRange(string kind)
    {
        byte[] bytes = Image(["Original.dll"]);
        int name = Names;
        switch (kind)
        {
            case "padding": U32(bytes, Section(false) + 8, 0x500); break;
            case "overlay": name = 0x1480; Encoding.ASCII.GetBytes("Outside.dll\0").CopyTo(bytes, name); break;
            case "cross-section": name = RawEnd - 8; Encoding.ASCII.GetBytes("late.dll\0").CopyTo(bytes, name); break;
            case "unterminated": bytes.AsSpan(name, 256).Fill((byte)'A'); break;
        }
        U32(bytes, Normal + 12, Rva(name));
        using var f = new AuditFixture(bytes);
        Failure(await f.Run(f.Path));
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("missing")]
    [InlineData("extension")]
    [InlineData("directory")]
    [InlineData("unc")]
    public async Task OnlyExplicitOrdinaryLocalBinaryPathsAreAccepted(string kind)
    {
        using var f = new AuditFixture(Image(), kind == "extension" ? "synthetic.txt" : "synthetic.exe");
        string input = kind switch
        {
            "relative" => Path.GetFileName(f.Path), // Exists in child's CWD: success cannot hide behind a missing-file error.
            "missing" => Path.Combine(f.Root, "missing.exe"),
            "directory" => Directory.CreateDirectory(Path.Combine(f.Root, "directory.exe")).FullName,
            "unc" => "\\\\invalid.example.test\\share\\synthetic.exe",
            _ => f.Path
        };
        Failure(await f.Run(input));
    }

    [Fact]
    public async Task MissingAndUnexpectedArgumentsHaveNoSuccessOutput()
    {
        using var f = new AuditFixture(Image());
        Failure(await f.Run()); Failure(await f.Run(f.Path, "--unknown"));
    }

    private static JsonDocument Success(AuditResult result)
    {
        Assert.True(result.ExitCode == 0, $"Exit {result.ExitCode}: {result.Error}");
        Assert.Equal(string.Empty, result.Error);
        return JsonDocument.Parse(result.Output); // Reject leading/trailing non-JSON hints or partial reports.
    }
    private static void Failure(AuditResult result)
    { Assert.NotEqual(0, result.ExitCode); Assert.Equal(string.Empty, result.Output); Assert.False(string.IsNullOrWhiteSpace(result.Error)); }
    private static void Keys(JsonElement value, params string[] keys) =>
        Assert.Equal(keys.Order(StringComparer.Ordinal), value.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
}
