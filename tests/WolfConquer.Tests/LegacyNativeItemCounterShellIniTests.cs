using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[CollectionDefinition("NativeItemCounterIniAdapter", DisableParallelization = true)]
public sealed class NativeItemCounterIniAdapterCollection { }

public sealed class WindowsNativeIniFactAttribute : FactAttribute
{
    public WindowsNativeIniFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires the actual Windows profile APIs.";
    }
}

public sealed class WindowsNativeIniTheoryAttribute : TheoryAttribute
{
    public WindowsNativeIniTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires the actual Windows profile APIs.";
    }
}

// Serial isolation protects temporary parser substitutions, not runtime counter locking.
[Collection("NativeItemCounterIniAdapter")]
public sealed class LegacyNativeItemCounterShellIniTests
{
    [Fact]
    public void ConstructorConcatenatesRawDatabaseLocationWithoutPathNormalization()
    {
        var calls = 0;
        var ini = new LegacyNativeItemCounterShellIni("shell.ini", () =>
        {
            calls++;
            return @"C:\not-a-real-config";
        });
        Assert.Equal(@"C:\not-a-real-configshell.ini", ini.FileName);
        Assert.Equal(1, calls);
        // Deliberately no native read/write on this inert constructor witness.
    }

    [Fact]
    public void OverridePathDoesNotEvaluateDatabaseLocationGetter()
    {
        var ini = new LegacyNativeItemCounterShellIni("inert-only.ini",
            () => throw new InvalidOperationException("must not read"), overridePath: true);
        Assert.Equal("inert-only.ini", ini.FileName);
    }

    [Fact]
    public void ParameterlessAndNullConstructorOperandsKeepNativeFieldSemantics()
    {
        Assert.Null(new LegacyNativeItemCounterShellIni().FileName);
        Assert.Null(new LegacyNativeItemCounterShellIni(null, null!, overridePath: true).FileName);
        Assert.Equal("", new LegacyNativeItemCounterShellIni(null, () => null).FileName);
        // Null/relative names are NEVER passed to a native API in this test.
    }

    [Fact]
    public void ConstructorDatabaseLocationExceptionEscapesWithoutReadDefaultCatch()
    {
        var expected = new InvalidOperationException("database producer");
        var actual = Assert.Throws<InvalidOperationException>(() =>
            new LegacyNativeItemCounterShellIni("inert-only.ini", () => throw expected));
        Assert.Same(expected, actual);
    }

    [WindowsNativeIniFact]
    public void PhysicalMissingFileAndMissingKeyReturnNativeDefaultZero()
    {
        using var owned = new OwnedIniFiles();
        Assert.Equal(0u, owned.Adapter(owned.NewPath()).ReadUInt32("Database", "ItemUID"));
        var path = owned.Seed("[Database]\r\nOther=5\r\n");
        Assert.Equal("0", owned.NativeRead(path, "Database", "ItemUID", 15));
        Assert.Equal(0u, owned.Adapter(path).ReadUInt32("Database", "ItemUID"));
    }

    [WindowsNativeIniTheory]
    [InlineData("0", 0u)]
    [InlineData("4294967295", uint.MaxValue)]
    [InlineData("-1", 0u)]
    [InlineData("4294967296", 0u)]
    [InlineData("not-a-number", 0u)]
    [InlineData("\"17\"", 17u)]
    [InlineData("'19'", 19u)]
    [InlineData(" +21 ", 21u)]
    [InlineData("", 0u)]
    public void PhysicalAsciiIniUsesNativeProfileQuotesAndUInt32ParseCatch(string text, uint expected)
    {
        using var owned = new OwnedIniFiles();
        var path = owned.Seed("[Database]\r\nItemUID=" + text + "\r\n");
        Assert.Equal(expected, owned.Adapter(path).ReadUInt32("dAtAbAsE", "iTeMuId"));
    }

    [WindowsNativeIniFact]
    public void PhysicalUtf16IniAndUnicodeSectionKeyUseTheWideApi()
    {
        using var owned = new OwnedIniFiles();
        var path = owned.Seed("[数据库]\r\n物品=4294967295\r\n", unicode: true);
        Assert.Equal(new byte[] { 255, 254 }, File.ReadAllBytes(path)[..2]);
        Assert.Equal(uint.MaxValue, owned.Adapter(path).ReadUInt32("数据库", "物品"));
    }

    [WindowsNativeIniFact]
    public void PhysicalReadUsesExactlyFourteenPayloadCharactersBeforeParsing()
    {
        using var owned = new OwnedIniFiles();
        const string longValue = "00000000000042TRAILING";
        var path = owned.Seed("[Database]\r\nItemUID=" + longValue + "\r\n");
        Assert.Equal(longValue, owned.NativeRead(path, "Database", "ItemUID", 64));
        Assert.Equal("00000000000042", owned.NativeRead(path, "Database", "ItemUID", 15));
        Assert.Equal(42u, owned.Adapter(path).ReadUInt32("Database", "ItemUID"));
    }

    [WindowsNativeIniFact]
    public void PhysicalReadUsesMutableParserAndCatchesParserFailureIncludingNull()
    {
        using var owned = new OwnedIniFiles();
        var path = owned.Seed("[Database]\r\nItemUID=000123\r\n");
        var ini = owned.Adapter(path);
        var original = LegacyNativeItemCounterShellIni.ToUInt32;
        string? seen = null;
        try
        {
            LegacyNativeItemCounterShellIni.ToUInt32 = text =>
            {
                seen = text;
                throw new InvalidOperationException("parse callback");
            };
            Assert.Equal(0u, ini.ReadUInt32("Database", "ItemUID"));
            Assert.Equal("000123", seen);
            LegacyNativeItemCounterShellIni.ToUInt32 = _ => 77u;
            Assert.Equal(77u, ini.ReadUInt32("Database", "ItemUID"));
            LegacyNativeItemCounterShellIni.ToUInt32 = null!;
            Assert.Equal(0u, ini.ReadUInt32("Database", "ItemUID"));
        }
        finally { LegacyNativeItemCounterShellIni.ToUInt32 = original; }
    }

    [WindowsNativeIniFact]
    public void PhysicalReadUInt32ParseUsesCurrentCultureNotAnInvariantReplacement()
    {
        using var owned = new OwnedIniFiles();
        var path = owned.Seed("[Database]\r\nItemUID=positive9\r\n");
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "positive";
        var oldCulture = CultureInfo.CurrentCulture;
        var oldParser = LegacyNativeItemCounterShellIni.ToUInt32;
        try
        {
            CultureInfo.CurrentCulture = culture;
            LegacyNativeItemCounterShellIni.ToUInt32 = uint.Parse;
            Assert.Equal(9u, owned.Adapter(path).ReadUInt32("Database", "ItemUID"));
        }
        finally
        {
            LegacyNativeItemCounterShellIni.ToUInt32 = oldParser;
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    [WindowsNativeIniTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PhysicalWriteUsesValueToStringAndPreservesExistingFileEncoding(bool unicode)
    {
        using var owned = new OwnedIniFiles();
        var marker = unicode ? "保留" : "keep";
        var path = owned.Seed("[Database]\r\nMarker=" + marker + "\r\n", unicode);
        var ini = owned.Adapter(path);
        ini.WriteUInt32("Database", "ItemUID", uint.MaxValue);
        Assert.Equal(uint.MaxValue.ToString(), owned.NativeRead(path, "Database", "ItemUID", 64));
        Assert.Equal(marker, owned.NativeRead(path, "Database", "Marker", 64));
        Assert.Equal(uint.MaxValue, ini.ReadUInt32("Database", "ItemUID"));
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(unicode, bytes.Length >= 2 && bytes[0] == 255 && bytes[1] == 254);
    }

    [WindowsNativeIniFact]
    public void NativeWriteFailureBoolIsIgnoredAndMissingDirectoryIsNotCreated()
    {
        using var owned = new OwnedIniFiles();
        var path = owned.NewMissingParentPath();
        Assert.Equal(0, owned.NativeWrite(path, "Database", "ItemUID", "41"));
        Assert.Null(Record.Exception(() => owned.Adapter(path).WriteUInt32("Database", "ItemUID", 42u)));
        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [WindowsNativeIniFact]
    public void MutableFilenameSelectsTheCurrentExplicitOwnedPhysicalFile()
    {
        using var owned = new OwnedIniFiles();
        var first = owned.Seed("[Database]\r\nItemUID=11\r\n");
        var second = owned.Seed("[Database]\r\nItemUID=22\r\n");
        var ini = owned.Adapter(first);
        Assert.Equal(11u, ini.ReadUInt32("Database", "ItemUID"));
        owned.Validate(second);
        ini.FileName = second;
        Assert.Equal(22u, ini.ReadUInt32("Database", "ItemUID"));
        ini.WriteUInt32("Database", "ItemUID", 23u);
        Assert.Equal("11", owned.NativeRead(first, "Database", "ItemUID", 64));
        Assert.Equal("23", owned.NativeRead(second, "Database", "ItemUID", 64));
    }

    // Every native call below is confined to newly owned workspace paths, never
    // shell.ini, win.ini, a relative/empty/null filename or a user's existing file.
    private sealed class OwnedIniFiles : IDisposable
    {
        private readonly string _root;
        private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);

        public OwnedIniFiles()
        {
            DirectoryInfo? workspace = new(AppContext.BaseDirectory);
            while (workspace is not null && !File.Exists(Path.Combine(workspace.FullName, "WolfConquer.sln")))
                workspace = workspace.Parent;
            if (workspace is null) throw new InvalidOperationException("Requires a real workspace.");
            var parent = Path.GetFullPath(Path.Combine(workspace.FullName,
                ".artifacts", "verification-direct-triple17490-20261015"));
            NoReparse(parent);
            _root = Path.Combine(parent,
                "root-native-item-counter-ini-adapter-goal516-owned-" + Guid.NewGuid().ToString("N"));
            if (File.Exists(_root) || Directory.Exists(_root)) throw new IOException("Owned root exists.");
            Directory.CreateDirectory(_root);
            NoReparse(_root);
        }

        public string NewPath()
        {
            var path = Path.Combine(_root, "profile-" + Guid.NewGuid().ToString("N") + ".ini");
            Validate(path);
            if (File.Exists(path) || Directory.Exists(path)) throw new IOException("Owned file exists.");
            _files.Add(path);
            return path;
        }

        public string NewMissingParentPath()
        {
            var path = Path.Combine(_root, "absent-" + Guid.NewGuid().ToString("N"),
                "profile-" + Guid.NewGuid().ToString("N") + ".ini");
            Validate(path);
            _files.Add(path);
            return path; // Parent deliberately never created.
        }

        public string Seed(string text, bool unicode = false)
        {
            var path = NewPath();
            Encoding encoding = unicode ? new UnicodeEncoding(false, true) : Encoding.ASCII;
            var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            stream.Write(bytes);
            stream.Flush(true);
            return path;
        }

        public LegacyNativeItemCounterShellIni Adapter(string path)
        {
            Validate(path);
            if (!_files.Contains(path)) throw new IOException("Not this fixture's file.");
            return new(path, () => throw new InvalidOperationException("override only"), overridePath: true);
        }

        public string NativeRead(string path, string section, string key, int size)
        {
            Validate(path);
            if (!_files.Contains(path)) throw new IOException("Not this fixture's file.");
            var buffer = Marshal.AllocHGlobal(size * sizeof(char));
            try
            {
                GetPrivateProfileStringW(section, key, "0", buffer, size, path);
                return Marshal.PtrToStringUni(buffer)!.Trim(default(char));
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        public int NativeWrite(string path, string section, string key, string value)
        {
            Validate(path);
            if (!_files.Contains(path)) throw new IOException("Not this fixture's file.");
            return WritePrivateProfileString(section, key, value, path);
        }

        public void Validate(string path)
        {
            var full = Path.GetFullPath(path);
            if (!string.Equals(full, path, StringComparison.OrdinalIgnoreCase) ||
                !full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Outside owned fixture root.");
            NoReparse(full);
        }

        private static void NoReparse(string path)
        {
            string? full = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(full))
            {
                try
                {
                    if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Reparse path refused.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                full = Path.GetDirectoryName(full);
            }
        }

        public void Dispose()
        {
            NoReparse(_root);
            // Refuse unexpected contents BEFORE deleting anything. Never recurse.
            foreach (var entry in Directory.EnumerateFileSystemEntries(_root))
            {
                Validate(entry);
                if (!_files.Contains(entry) || Directory.Exists(entry))
                    throw new IOException("Unexpected fixture contents preserved.");
            }
            foreach (var path in _files)
            {
                Validate(path);
                if (File.Exists(path)) File.Delete(path);
            }
            Directory.Delete(_root, recursive: false);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetPrivateProfileStringW(
        string section, string key, string defaultValue, IntPtr buffer, int size, string fileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int WritePrivateProfileString(
        string section, string key, string value, string fileName);
}
