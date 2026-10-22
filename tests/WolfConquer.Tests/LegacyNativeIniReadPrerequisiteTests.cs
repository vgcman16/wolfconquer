using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Same serial collection as the retained uint adapter controls: all mutable parsers
// and shared ServerConfig state are restored, without adding runtime locking.
[Collection("NativeItemCounterIniAdapter")]
public sealed class LegacyNativeIniReadPrerequisiteTests
{
    [Fact]
    public void NativeConstructorReadsTheLiveConfigForEachNewInstanceWithoutNormalization()
    {
        var before = LegacyNativeServerConfig.DbLocation;
        try
        {
            LegacyNativeServerConfig.DbLocation = @"C:\native-db";
            var first = new LegacyNativeItemCounterShellIni("shell.ini");
            LegacyNativeServerConfig.DbLocation = @"D:\next\";
            var second = new LegacyNativeItemCounterShellIni("shell.ini");
            Assert.Equal(@"C:\native-dbshell.ini", first.FileName);
            Assert.Equal(@"D:\next\shell.ini", second.FileName);
            Assert.Equal(@"C:\native-dbshell.ini", first.FileName);
        }
        finally { LegacyNativeServerConfig.DbLocation = before; }
    }

    [Fact]
    public void NativeOverrideAndNullOperandsPreserveTheOriginalFieldSemantics()
    {
        var before = LegacyNativeServerConfig.DbLocation;
        try
        {
            LegacyNativeServerConfig.DbLocation = "unused-prefix";
            Assert.Equal("..\\inert-only.ini",
                new LegacyNativeItemCounterShellIni("..\\inert-only.ini", overridePath: true).FileName);
            Assert.Null(new LegacyNativeItemCounterShellIni(null, overridePath: true).FileName);
            LegacyNativeServerConfig.DbLocation = null!;
            Assert.Equal("", new LegacyNativeItemCounterShellIni(null).FileName);
        }
        finally { LegacyNativeServerConfig.DbLocation = before; }
    }

    [Fact]
    public void ParameterlessAndExplicitCompatibilityConstructorsRemainUnchanged()
    {
        Assert.Null(new LegacyNativeItemCounterShellIni().FileName);
        var calls = 0;
        var ini = new LegacyNativeItemCounterShellIni("suffix", () =>
        {
            calls++;
            return "explicit-prefix";
        });
        Assert.Equal("explicit-prefixsuffix", ini.FileName);
        Assert.Equal(1, calls);
        Assert.Equal("override", new LegacyNativeItemCounterShellIni(
            "override", () => throw new InvalidOperationException(), overridePath: true).FileName);
    }

    [Fact]
    public void NativeReadSignaturesAndParserFieldsKeepTheirRealTypesAndMutability()
    {
        var type = typeof(LegacyNativeItemCounterShellIni);
        Assert.Equal(typeof(string), type.GetMethod("ReadString",
            [typeof(string), typeof(string), typeof(string)])!.ReturnType);
        Assert.Equal(typeof(string), type.GetMethod("ReadString",
            [typeof(string), typeof(string), typeof(string), typeof(int)])!.ReturnType);
        Assert.Equal(typeof(ushort), type.GetMethod("ReadUInt16")!.ReturnType);
        Assert.Equal(typeof(bool), type.GetMethod("ReadBool")!.ReturnType);
        var ushortParser = type.GetField("ToUInt16", BindingFlags.Public | BindingFlags.Static)!;
        var boolParser = type.GetField("ToBool", BindingFlags.Public | BindingFlags.Static)!;
        Assert.Equal(typeof(Func<string, ushort>), ushortParser.FieldType);
        Assert.Equal(typeof(Func<string, bool>), boolParser.FieldType);
        Assert.False(ushortParser.IsInitOnly);
        Assert.False(boolParser.IsInitOnly);
    }

    [Fact]
    public void DefaultFormattingFaultIsCaughtBeforeCallbackOrAnyNativeRead()
    {
        var value = new ThrowingDefault();
        var calls = 0;
        var ini = new LegacyNativeItemCounterShellIni();
        Assert.Same(value, ini.ReadValue("unused", "unused", value, _ =>
        {
            calls++;
            return new ThrowingDefault();
        }));
        Assert.Same(value, ini.ReadValue("unused", "unused", value, _ =>
        {
            calls++;
            return new ThrowingDefault();
        }, 9));
        Assert.Null(ini.ReadValue<string?>("unused", "unused", null, _ =>
        {
            calls++;
            return "must-not-run";
        }));
        Assert.Equal(0, calls);
        Assert.Null(ini.FileName); // No null/relative filename reaches PInvoke.
    }

    [WindowsNativeIniFact]
    public void MissingPhysicalFileAndKeyReturnTheProvidedStringDefault()
    {
        using var owned = new OwnedProfiles();
        Assert.Equal("missing-default", owned.Adapter(owned.MissingPath())
            .ReadString("Database", "Location", "missing-default"));
        var path = owned.Seed("[Database]\r\nOther=value\r\n");
        Assert.Equal("key-default", owned.Adapter(path).ReadString("Database", "Location", "key-default"));
    }

    [WindowsNativeIniFact]
    public void DefaultStringReadUsesExactlyTwoHundredFiftyFourUtf16PayloadCharacters()
    {
        using var owned = new OwnedProfiles();
        var value = new string('x', 254) + "TRAILING";
        var path = owned.Seed("[Database]\r\nLocation=" + value + "\r\n");
        Assert.Equal(new string('x', 254), owned.Adapter(path).ReadString("Database", "Location", ""));
    }

    [WindowsNativeIniFact]
    public void ExplicitStringSizeUsesSizeMinusOneWithoutAGrowableFallback()
    {
        using var owned = new OwnedProfiles();
        var path = owned.Seed("[Database]\r\nLocation=abcdefghi\r\n");
        Assert.Equal("abcd", owned.Adapter(path).ReadString("Database", "Location", "", 5));
        Assert.Equal("abcdefghi", owned.Adapter(path).ReadString("Database", "Location", "", 32));
    }

    [WindowsNativeIniFact]
    public void WideStringReadRetainsUnicodeAndNativeProfileQuoteSemantics()
    {
        using var owned = new OwnedProfiles();
        var path = owned.Seed("[数据库]\r\n位置=\"保留-路径\"\r\n", unicode: true);
        Assert.Equal("保留-路径", owned.Adapter(path).ReadString("数据库", "位置", "fallback"));
        Assert.Equal(new byte[] { 255, 254 }, File.ReadAllBytes(path)[..2]);
    }

    [WindowsNativeIniFact]
    public void UInt16MissingMalformedNegativeAndOverflowValuesReturnTheCallerDefault()
    {
        using var owned = new OwnedProfiles();
        var path = owned.Seed("[ServerInfo]\r\nGood=65535\r\nBad=no\r\nNegative=-1\r\nOverflow=65536\r\nEmpty=\r\n");
        var ini = owned.Adapter(path);
        Assert.Equal(ushort.MaxValue, ini.ReadUInt16("ServerInfo", "Good", 7));
        foreach (var key in new[] { "Missing", "Bad", "Negative", "Overflow", "Empty" })
            Assert.Equal((ushort)7, ini.ReadUInt16("ServerInfo", key, 7));
        Assert.Equal((ushort)9, owned.Adapter(owned.MissingPath()).ReadUInt16("ServerInfo", "Port", 9));
    }

    [WindowsNativeIniFact]
    public void UInt16BufferDeliversExactlyEightCharactersToTheCapturedMutableParser()
    {
        using var owned = new OwnedProfiles();
        var path = owned.Seed("[ServerInfo]\r\nPort=12345678TRAIL\r\n");
        var before = LegacyNativeItemCounterShellIni.ToUInt16;
        string? seen = null;
        try
        {
            LegacyNativeItemCounterShellIni.ToUInt16 = text =>
            {
                seen = text;
                return 42;
            };
            Assert.Equal((ushort)42, owned.Adapter(path).ReadUInt16("ServerInfo", "Port", 9));
            Assert.Equal("12345678", seen);
        }
        finally { LegacyNativeItemCounterShellIni.ToUInt16 = before; }
    }

    [WindowsNativeIniFact]
    public void UInt16ParseUsesCurrentCultureRatherThanAnInvariantProvider()
    {
        using var owned = new OwnedProfiles();
        var path = owned.Seed("[ServerInfo]\r\nPort=p42\r\n");
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        var previousCulture = CultureInfo.CurrentCulture;
        var before = LegacyNativeItemCounterShellIni.ToUInt16;
        try
        {
            CultureInfo.CurrentCulture = culture;
            LegacyNativeItemCounterShellIni.ToUInt16 = ushort.Parse;
            Assert.Equal((ushort)42, owned.Adapter(path).ReadUInt16("ServerInfo", "Port", 9));
        }
        finally
        {
            LegacyNativeItemCounterShellIni.ToUInt16 = before;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [WindowsNativeIniFact]
    public void BoolBufferUsesFivePayloadCharactersAndBoolParseNotNumericCoercion()
    {
        using var owned = new OwnedProfiles();
        var path = owned.Seed("[ServerInfo]\r\nFalseLong=FalseSuffix\r\nTrueLong=TrueSuffix\r\nNumeric=1\r\nValid=tRuE\r\n");
        var ini = owned.Adapter(path);
        Assert.False(ini.ReadBool("ServerInfo", "FalseLong", true));
        Assert.False(ini.ReadBool("ServerInfo", "TrueLong", false));
        Assert.True(ini.ReadBool("ServerInfo", "Numeric", true));
        Assert.True(ini.ReadBool("ServerInfo", "Valid", false));
    }

    [WindowsNativeIniFact]
    public void BoolMissingAndMalformedValuesReturnEachCallerSuppliedDefault()
    {
        using var owned = new OwnedProfiles();
        var path = owned.Seed("[ServerInfo]\r\nBad=wrong\r\nEmpty=\r\n");
        var ini = owned.Adapter(path);
        Assert.True(ini.ReadBool("ServerInfo", "Missing", true));
        Assert.False(ini.ReadBool("ServerInfo", "Missing", false));
        Assert.True(ini.ReadBool("ServerInfo", "Bad", true));
        Assert.False(ini.ReadBool("ServerInfo", "Empty", false));
        Assert.True(owned.Adapter(owned.MissingPath()).ReadBool("ServerInfo", "Flag", true));
    }

    [WindowsNativeIniFact]
    public void MutableUInt16AndBoolParserFailuresIncludingNullAreCaughtAsDefaults()
    {
        using var owned = new OwnedProfiles();
        var path = owned.Seed("[ServerInfo]\r\nPort=42\r\nFlag=True\r\n");
        var beforeUInt16 = LegacyNativeItemCounterShellIni.ToUInt16;
        var beforeBool = LegacyNativeItemCounterShellIni.ToBool;
        var calls = 0;
        try
        {
            LegacyNativeItemCounterShellIni.ToUInt16 = _ =>
            {
                calls++;
                throw new InvalidOperationException("ushort parse");
            };
            LegacyNativeItemCounterShellIni.ToBool = _ =>
            {
                calls++;
                throw new InvalidOperationException("bool parse");
            };
            Assert.Equal((ushort)17, owned.Adapter(path).ReadUInt16("ServerInfo", "Port", 17));
            Assert.False(owned.Adapter(path).ReadBool("ServerInfo", "Flag", false));
            Assert.Equal(2, calls);
            LegacyNativeItemCounterShellIni.ToUInt16 = null!;
            LegacyNativeItemCounterShellIni.ToBool = null!;
            Assert.Equal((ushort)18, owned.Adapter(path).ReadUInt16("ServerInfo", "Port", 18));
            Assert.True(owned.Adapter(path).ReadBool("ServerInfo", "Flag", true));
        }
        finally
        {
            LegacyNativeItemCounterShellIni.ToUInt16 = beforeUInt16;
            LegacyNativeItemCounterShellIni.ToBool = beforeBool;
        }
    }

    [WindowsNativeIniFact]
    public void GenericReadValueOverloadsUseNativeDefaultAndExplicitBufferSizes()
    {
        using var owned = new OwnedProfiles();
        var value = new string('x', 260);
        var path = owned.Seed("[Database]\r\nText=" + value + "\r\n");
        var ini = owned.Adapter(path);
        Assert.Equal(new string('x', 254), ini.ReadValue("Database", "Text", "default", text => text));
        Assert.Equal("xxxxx", ini.ReadValue("Database", "Text", "default", text => text, 6));
    }

    [WindowsNativeIniFact]
    public void GenericCallbackFaultReturnsDefaultAfterTheRealStringRead()
    {
        using var owned = new OwnedProfiles();
        var path = owned.Seed("[Database]\r\nText=actual-value\r\n");
        var ini = owned.Adapter(path);
        string? seen = null;
        Assert.Equal("fallback", ini.ReadValue("Database", "Text", "fallback", text =>
        {
            seen = text;
            throw new InvalidOperationException("callback");
        }, 32));
        Assert.Equal("actual-value", seen);
        Assert.Equal("fallback", ini.ReadValue<string>("Database", "Text", "fallback", null!, 32));
        Assert.Equal("actual-value", ini.ReadString("Database", "Text", "fallback"));
    }

    [WindowsNativeIniFact]
    public void MutableFileNameIsRereadForEachNativeStringUShortAndBoolRead()
    {
        using var owned = new OwnedProfiles();
        var first = owned.Seed("[Values]\r\nText=first\r\nPort=11\r\nFlag=True\r\n");
        var second = owned.Seed("[Values]\r\nText=second\r\nPort=22\r\nFlag=False\r\n");
        var ini = owned.Adapter(first);
        Assert.Equal("first", ini.ReadString("Values", "Text", ""));
        Assert.Equal((ushort)11, ini.ReadUInt16("Values", "Port", 0));
        Assert.True(ini.ReadBool("Values", "Flag", false));
        owned.Validate(second);
        ini.FileName = second;
        Assert.Equal("second", ini.ReadString("Values", "Text", ""));
        Assert.Equal((ushort)22, ini.ReadUInt16("Values", "Port", 0));
        Assert.False(ini.ReadBool("Values", "Flag", true));
    }

    private sealed class ThrowingDefault
    {
        public override string ToString() => throw new InvalidOperationException("format before read");
    }

    private sealed class OwnedProfiles : IDisposable
    {
        private readonly string _root;
        private readonly Dictionary<string, byte[]?> _files = new(StringComparer.OrdinalIgnoreCase);

        public OwnedProfiles()
        {
            DirectoryInfo? workspace = new(AppContext.BaseDirectory);
            while (workspace is not null && !File.Exists(Path.Combine(workspace.FullName, "WolfConquer.sln")))
                workspace = workspace.Parent;
            if (workspace is null) throw new InvalidOperationException("A real workspace is required.");
            var parent = Path.GetFullPath(Path.Combine(workspace.FullName, ".artifacts"));
            NoReparse(parent);
            _root = Path.Combine(parent, "goal538-native-ini-owned-" + Guid.NewGuid().ToString("N"));
            if (File.Exists(_root) || Directory.Exists(_root)) throw new IOException("Owned root exists.");
            Directory.CreateDirectory(_root);
            NoReparse(_root);
        }

        public string MissingPath()
        {
            var path = Path.Combine(_root, "profile-" + Guid.NewGuid().ToString("N") + ".ini");
            Validate(path);
            if (File.Exists(path) || Directory.Exists(path)) throw new IOException("Owned path exists.");
            _files.Add(path, null);
            return path;
        }

        public string Seed(string text, bool unicode = false)
        {
            var path = MissingPath();
            Encoding encoding = unicode ? new UnicodeEncoding(false, true) : Encoding.ASCII;
            var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            Assert.Equal(bytes, File.ReadAllBytes(path));
            _files[path] = bytes;
            return path;
        }

        public LegacyNativeItemCounterShellIni Adapter(string path)
        {
            Validate(path);
            if (!_files.ContainsKey(path)) throw new IOException("Not this fixture's path.");
            return new LegacyNativeItemCounterShellIni(path, overridePath: true);
        }

        public void Validate(string path)
        {
            var full = Path.GetFullPath(path);
            if (!string.Equals(full, path, StringComparison.OrdinalIgnoreCase) ||
                !full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Outside this owned root.");
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
            foreach (var entry in Directory.EnumerateFileSystemEntries(_root))
            {
                Validate(entry);
                if (!_files.ContainsKey(entry) || Directory.Exists(entry))
                    throw new IOException("Unexpected fixture content is preserved.");
            }
            foreach (var pair in _files)
            {
                Validate(pair.Key);
                if (pair.Value is null)
                {
                    if (File.Exists(pair.Key)) throw new IOException("Unexpected file at a missing-path control.");
                }
                else
                {
                    var actual = File.ReadAllBytes(pair.Key);
                    if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(actual), SHA256.HashData(pair.Value)))
                        throw new IOException("Changed fixture bytes are preserved.");
                }
            }
            foreach (var pair in _files)
                if (pair.Value is not null) File.Delete(pair.Key);
            Directory.Delete(_root, recursive: false);
        }
    }
}
