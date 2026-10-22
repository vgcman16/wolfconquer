using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterIniAdapter")]
public sealed class LegacyNativeIniCompleteTests
{
    private sealed class ThrowingValue
    {
        public override string ToString() => throw new InvalidOperationException("format");
    }

    private static LegacyNativeItemCounterShellIni Inert() => new();

    [Fact]
    public void NativeConstantsAndAllElevenMutableParserFieldsPreserveOrder()
    {
        var type = typeof(LegacyNativeItemCounterShellIni);
        Assert.Equal(new[] { 15, 9, 6, 6, 20, 22, 10 }, new[] {
            LegacyNativeItemCounterShellIni.Int32_Size, LegacyNativeItemCounterShellIni.Int16_Size,
            LegacyNativeItemCounterShellIni.Int8_Size, LegacyNativeItemCounterShellIni.Bool_Size,
            LegacyNativeItemCounterShellIni.Double_Size, LegacyNativeItemCounterShellIni.Int64_Size,
            LegacyNativeItemCounterShellIni.Float_Size });
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => !f.IsLiteral).OrderBy(f => f.MetadataToken).ToArray();
        Assert.Equal(new[] { "ToInt32", "ToUInt32", "ToInt16", "ToUInt16", "ToInt8", "ToUInt8",
            "ToBool", "ToDouble", "ToInt64", "ToUInt64", "ToFloat" }, fields.Select(f => f.Name));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
        Assert.Equal(new[] { typeof(int), typeof(uint), typeof(short), typeof(ushort), typeof(sbyte),
            typeof(byte), typeof(bool), typeof(double), typeof(long), typeof(ulong), typeof(float) },
            fields.Select(f => f.FieldType.GenericTypeArguments[1]));
    }

    [Fact]
    public void NativeCompleteMethodShapesRetainInterfaceAndCompatibilityConstructors()
    {
        var type = typeof(LegacyNativeItemCounterShellIni);
        Assert.True(typeof(ILegacyNativeItemCounterShellIni).IsAssignableFrom(type));
        Assert.NotNull(type.GetMethod("ReadUInt32", new[] { typeof(string), typeof(string) }));
        var genuine = type.GetMethod("ReadUInt32", new[] { typeof(string), typeof(string), typeof(uint) })!;
        Assert.Equal(0u, genuine.GetParameters()[2].DefaultValue);
        Assert.NotNull(type.GetConstructor(new[] { typeof(string), typeof(bool) }));
        Assert.NotNull(type.GetConstructor(new[] { typeof(string), typeof(Func<string>), typeof(bool) }));
        Assert.Null(Inert().FileName);
    }

    [Fact]
    public void KeyExistsDefaultLiteralRetainsNulThenZeroSevenF()
    {
        Assert.Equal(new[] { '\0', '0', '7', 'F' }, "\007F".ToCharArray());
        Assert.NotEqual("\u007f", "\007F");
    }

    [Fact]
    public void GenericNullAndThrowingWritesFaultBeforeNativeUse()
    {
        var ini = Inert();
        Assert.Throws<NullReferenceException>(() => ini.Write<string>("unused", "unused", null!));
        var error = Assert.Throws<InvalidOperationException>(() =>
            ini.Write("unused", "unused", new ThrowingValue()));
        Assert.Equal("format", error.Message);
        Assert.Null(ini.FileName);
    }

    [Fact]
    public void NullSectionExistsFaultsBeforeNativeUse()
    {
        Assert.Throws<NullReferenceException>(() => Inert().SectionExists(null!));
    }

    [Fact]
    public void NativePointerAndImportMetadataWidthsRemainLiteral()
    {
        var methods = typeof(LegacyNativeItemCounterShellIni)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
        var imports = methods.Select(m => (Method: m, Import: m.GetCustomAttribute<DllImportAttribute>()))
            .Where(pair => pair.Import is not null).ToArray();
        Assert.Equal(6, imports.Length);
        Assert.All(imports, pair => Assert.Equal("kernel32.dll", pair.Import!.Value));
        var ansi = imports.Single(pair => pair.Method.Name == "GetPrivateProfileStringA");
        Assert.Equal(CharSet.Ansi, ansi.Import!.CharSet);
        Assert.Equal(typeof(uint), ansi.Method.ReturnType);
        Assert.Equal(typeof(void).MakePointerType(), ansi.Method.GetParameters()[2].ParameterType);
        Assert.Equal(typeof(sbyte).MakePointerType(), ansi.Method.GetParameters()[3].ParameterType);
        var write = imports.Single(pair => pair.Method.Name == "WritePrivateProfileStructW").Method;
        Assert.Equal(typeof(bool), write.ReturnType);
        Assert.Equal(UnmanagedType.Bool, write.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()!.Value);
        Assert.All(imports.Where(pair => pair.Method.Name != "GetPrivateProfileStringA"),
            pair => Assert.Equal(CharSet.Unicode, pair.Import!.CharSet));
    }

    [WindowsNativeIniFact]
    public void EveryNumericWidthReadsItsNativeSignedOrUnsignedValue()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var ini = owned.Adapter(owned.Seed("[N]\r\nI32=-2147483648\r\nU32=4294967295\r\nI16=-32768\r\nU16=65535\r\nI8=-128\r\nU8=255\r\nI64=-9223372036854775808\r\nU64=18446744073709551615\r\nB=True\r\n"));
        Assert.Equal(int.MinValue, ini.ReadInt32("N", "I32", 7));
        Assert.Equal(uint.MaxValue, ini.ReadUInt32("N", "U32", 7));
        Assert.Equal(short.MinValue, ini.ReadInt16("N", "I16", 7));
        Assert.Equal(ushort.MaxValue, ini.ReadUInt16("N", "U16", 7));
        Assert.Equal(sbyte.MinValue, ini.ReadSByte("N", "I8", 7));
        Assert.Equal(byte.MaxValue, ini.ReadByte("N", "U8", 7));
        Assert.Equal(long.MinValue, ini.ReadInt64("N", "I64", 7));
        Assert.Equal(ulong.MaxValue, ini.ReadUInt64("N", "U64", 7));
        Assert.True(ini.ReadBool("N", "B", false));
    }

    [WindowsNativeIniFact]
    public void CallerDefaultUInt32AndExistingInterfaceZeroBothSurvive()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var ini = owned.Adapter(owned.MissingPath());
        Assert.Equal(912u, ini.ReadUInt32("N", "Absent", 912u));
        Assert.Equal(0u, ((ILegacyNativeItemCounterShellIni)ini).ReadUInt32("N", "Absent"));
    }

    [WindowsNativeIniFact]
    public void EveryNumericParserReturnsCallerDefaultForMalformedInput()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var ini = owned.Adapter(owned.Seed("[N]\r\nBad=bad\r\n"));
        Assert.Equal(-12, ini.ReadInt32("N", "Bad", -12));
        Assert.Equal(13u, ini.ReadUInt32("N", "Bad", 13u));
        Assert.Equal((short)-14, ini.ReadInt16("N", "Bad", -14));
        Assert.Equal((ushort)15, ini.ReadUInt16("N", "Bad", 15));
        Assert.Equal((sbyte)-16, ini.ReadSByte("N", "Bad", -16));
        Assert.Equal((byte)17, ini.ReadByte("N", "Bad", 17));
        Assert.True(ini.ReadBool("N", "Bad", true));
        Assert.Equal(-18d, ini.ReadDouble("N", "Bad", -18d));
        Assert.Equal(-19L, ini.ReadInt64("N", "Bad", -19L));
        Assert.Equal(20UL, ini.ReadUInt64("N", "Bad", 20UL));
        Assert.Equal(-21f, ini.ReadFloat("N", "Bad", -21f));
    }

    [WindowsNativeIniFact]
    public void MutableAllElevenParsersReceiveExactNativeSizedPayloads()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        using var lease = new LegacyNativeIniCompleteParserLease();
        var ini = owned.Adapter(owned.Seed("[N]\r\nLong=123456789012345678901234567890\r\n"));
        var seen = new List<string>();
        lease.Set("ToInt32", new Func<string, int>(s => { seen.Add(s); return 1; }));
        lease.Set("ToUInt32", new Func<string, uint>(s => { seen.Add(s); return 2; }));
        lease.Set("ToInt16", new Func<string, short>(s => { seen.Add(s); return 3; }));
        lease.Set("ToUInt16", new Func<string, ushort>(s => { seen.Add(s); return 4; }));
        lease.Set("ToInt8", new Func<string, sbyte>(s => { seen.Add(s); return 5; }));
        lease.Set("ToUInt8", new Func<string, byte>(s => { seen.Add(s); return 6; }));
        lease.Set("ToBool", new Func<string, bool>(s => { seen.Add(s); return true; }));
        lease.Set("ToDouble", new Func<string, double>(s => { seen.Add(s); return 8; }));
        lease.Set("ToInt64", new Func<string, long>(s => { seen.Add(s); return 9; }));
        lease.Set("ToUInt64", new Func<string, ulong>(s => { seen.Add(s); return 10; }));
        lease.Set("ToFloat", new Func<string, float>(s => { seen.Add(s); return 11; }));
        Assert.Equal(1, ini.ReadInt32("N", "Long", 0));
        Assert.Equal(2u, ini.ReadUInt32("N", "Long", 0));
        Assert.Equal((short)3, ini.ReadInt16("N", "Long", 0));
        Assert.Equal((ushort)4, ini.ReadUInt16("N", "Long", 0));
        Assert.Equal((sbyte)5, ini.ReadSByte("N", "Long", 0));
        Assert.Equal((byte)6, ini.ReadByte("N", "Long", 0));
        Assert.True(ini.ReadBool("N", "Long", false));
        Assert.Equal(8d, ini.ReadDouble("N", "Long", 0));
        Assert.Equal(9L, ini.ReadInt64("N", "Long", 0));
        Assert.Equal(10UL, ini.ReadUInt64("N", "Long", 0));
        Assert.Equal(11f, ini.ReadFloat("N", "Long", 0));
        Assert.Equal(new[] { 14, 14, 8, 8, 5, 5, 5, 19, 21, 21, 9 }, seen.Select(s => s.Length));
        Assert.All(seen, s => Assert.Equal("123456789012345678901234567890"[..s.Length], s));
    }

    [WindowsNativeIniFact]
    public void ThrowingAndNullParserFieldsRestoreDefaultsAndOriginalDelegates()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var original = LegacyNativeItemCounterShellIni.ToUInt32;
        var ini = owned.Adapter(owned.Seed("[N]\r\nV=44\r\n"));
        using (var lease = new LegacyNativeIniCompleteParserLease())
        {
            lease.Set("ToUInt32", new Func<string, uint>(_ => throw new FormatException()));
            Assert.Equal(42u, ini.ReadUInt32("N", "V", 42u));
            lease.Set("ToUInt32", null);
            Assert.Equal(43u, ini.ReadUInt32("N", "V", 43u));
        }
        Assert.Same(original, LegacyNativeItemCounterShellIni.ToUInt32);
        Assert.Equal(44u, ini.ReadUInt32("N", "V"));
    }

    [WindowsNativeIniFact]
    public void FloatingParsersUseCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ",";
            CultureInfo.CurrentCulture = culture;
            using var owned = new LegacyNativeIniCompleteOwnedProfiles();
            var ini = owned.Adapter(owned.Seed("[N]\r\nF=1,5\r\nD=-2,5\r\n"));
            Assert.Equal(1.5f, ini.ReadFloat("N", "F", 0));
            Assert.Equal(-2.5d, ini.ReadDouble("N", "D", 0));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [WindowsNativeIniFact]
    public void BigStringDefaultAndExplicitSizesRemain1020AndSupplied()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var value = new string('x', 1025);
        var ini = owned.Adapter(owned.Seed("[N]\r\nV=" + value + "\r\n"));
        Assert.Equal(value[..1019], ini.ReadBigString("N", "V", ""));
        Assert.Equal(value[..16], ini.ReadBigString("N", "V", "", 17));
    }

    [WindowsNativeIniFact]
    public void GenericStringReadOverloadsPreserveDefaultFormattingAndBuffer()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var ini = owned.Adapter(owned.MissingPath());
        string? formatted = null;
        Assert.Equal(123456, ini.ReadValue("N", "V", 123456, s => { formatted = s; return 123456; }));
        Assert.Equal("123456", formatted);
        Assert.Equal("123", ini.ReadValue("N", "V", "123456", s => s, 4));
        Assert.Equal("fallback", ini.ReadValue<string>("N", "V", "fallback", _ => throw new FormatException()));
        Assert.Null(ini.ReadValue<string?>("N", "V", null, s => s));
    }

    [WindowsNativeIniFact]
    public unsafe void RawAnsiBufferReadUsesOnlyOwnedBoundedMemory()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var ini = owned.Adapter(owned.Seed("[N]\r\nV=abcdef\r\n"));
        byte* buffer = stackalloc byte[8];
        byte* fallback = stackalloc byte[2] { (byte)'z', 0 };
        for (var i = 0; i < 8; i++) buffer[i] = 0x5a;
        ini.ReadString("N", "V", fallback, buffer, 5);
        Assert.Equal(new byte[] { 97, 98, 99, 100, 0, 0x5a, 0x5a, 0x5a },
            new ReadOnlySpan<byte>(buffer, 8).ToArray());
    }

    [WindowsNativeIniFact]
    public unsafe void RawAnsiMissingKeyCopiesKnownTerminatedDefault()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var ini = owned.Adapter(owned.MissingPath());
        byte* buffer = stackalloc byte[8];
        byte* fallback = stackalloc byte[5] { 115, 97, 102, 101, 0 };
        for (var i = 0; i < 8; i++) buffer[i] = 0x5a;
        ini.ReadString("N", "V", fallback, buffer, 8);
        Assert.Equal(new byte[] { 115, 97, 102, 101, 0 },
            new ReadOnlySpan<byte>(buffer, 5).ToArray());
    }

    [WindowsNativeIniFact]
    public void WriteStringNullUsesLiteralZeroWhileGenericNullThrows()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var path = owned.Seed("[N]\r\nSentinel=kept\r\n");
        var ini = owned.Adapter(path);
        ini.WriteString("N", "Null", null);
        Assert.Equal("0", ini.ReadString("N", "Null", ""));
        Assert.Equal("kept", ini.ReadString("N", "Sentinel", ""));
        Assert.Throws<NullReferenceException>(() => ini.Write<string>("N", "Bad", null!));
        Assert.False(ini.GetSection("N").Any(s => s.StartsWith("Bad=", StringComparison.Ordinal)));
        owned.CaptureExpectedMutation(path);
    }

    [WindowsNativeIniFact]
    public void GenericWriteUsesCurrentCultureFormatting()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ",";
            CultureInfo.CurrentCulture = culture;
            using var owned = new LegacyNativeIniCompleteOwnedProfiles();
            var path = owned.Seed("[N]\r\nSentinel=kept\r\n");
            var ini = owned.Adapter(path);
            ini.Write("N", "V", 12.5m);
            Assert.Equal("12,5", ini.ReadString("N", "V", ""));
            Assert.Equal("kept", ini.ReadString("N", "Sentinel", ""));
            owned.CaptureExpectedMutation(path);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [WindowsNativeIniFact]
    public void InterfaceUInt32WriteReadsBackThroughRealNativePath()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var path = owned.Seed("[N]\r\nSentinel=kept\r\n");
        ILegacyNativeItemCounterShellIni ini = owned.Adapter(path);
        ini.WriteUInt32("N", "V", uint.MaxValue);
        Assert.Equal(uint.MaxValue, ini.ReadUInt32("N", "V"));
        Assert.Equal("kept", owned.Adapter(path).ReadString("N", "Sentinel", ""));
        owned.CaptureExpectedMutation(path);
    }

    [WindowsNativeIniFact]
    public unsafe void WriteStructPersistsEightOwnedBytesViaIndependentProfileStructRead()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var path = owned.Seed("[N]\r\nSentinel=kept\r\n");
        var ini = owned.Adapter(path);
        byte* source = stackalloc byte[8] { 0, 1, 2, 3, 127, 128, 254, 255 };
        byte* target = stackalloc byte[8];
        ini.WriteStruct("N", "Blob", source, 8);
        Assert.True(GetPrivateProfileStructW("N", "Blob", target, 8, path));
        Assert.Equal(new ReadOnlySpan<byte>(source, 8).ToArray(),
            new ReadOnlySpan<byte>(target, 8).ToArray());
        Assert.Equal("kept", ini.ReadString("N", "Sentinel", ""));
        owned.CaptureExpectedMutation(path);
    }

    [WindowsNativeIniFact]
    public void SectionNamesDefaultAndExplicitSizesReturnNativeOrderedNames()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var ini = owned.Adapter(owned.Seed("[One]\r\na=1\r\n[Two]\r\nb=2\r\n"));
        Assert.Equal(new[] { "One", "Two" }, ini.GetSectionNames());
        Assert.Equal(new[] { "One", "Two" }, ini.GetSectionNames(128));
    }

    [WindowsNativeIniFact]
    public void SectionDefaultAndExplicitSizesReturnNativeKeyValueEntries()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var ini = owned.Adapter(owned.Seed("[One]\r\na=1\r\nb=2\r\n"));
        Assert.Equal(new[] { "a=1", "b=2" }, ini.GetSection("One"));
        Assert.Equal(new[] { "a=1", "b=2" }, ini.GetSection("One", 128));
    }

    [WindowsNativeIniFact]
    public void MissingAndEmptySectionEnumerationsReturnEmptyArrays()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var ini = owned.Adapter(owned.MissingPath());
        Assert.Empty(ini.GetSectionNames());
        Assert.Empty(ini.GetSection("Missing"));
        var empty = owned.Adapter(owned.Seed("[Empty]\r\n"));
        Assert.Empty(empty.GetSection("Empty"));
    }

    [WindowsNativeIniFact]
    public void SectionExistsReflectsNonemptyContentNotHeaderPresence()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var ini = owned.Adapter(owned.Seed("[One]\r\na=1\r\n[Empty]\r\n"));
        Assert.True(ini.SectionExists("One"));
        Assert.False(ini.SectionExists("Empty"));
        Assert.False(ini.SectionExists("Missing"));
    }

    [WindowsNativeIniFact]
    public void KeyExistsPreservesNativeMissingAndEmptyTrueQuirk()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var ini = owned.Adapter(owned.Seed("[N]\r\nEmpty=\r\nNormal=abc\r\n"));
        Assert.True(ini.KeyExists("N", "Empty"));
        Assert.True(ini.KeyExists("N", "Normal"));
        Assert.True(ini.KeyExists("N", "Missing"));
        Assert.True(owned.Adapter(owned.MissingPath()).KeyExists("N", "Missing"));
    }

    [WindowsNativeIniFact]
    public void KeyExistsRejectsOnlyFirstDelCharacterInOnePayloadBuffer()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var ini = owned.Adapter(owned.Seed("[N]\r\nDel=\u007fnext\r\nLater=a\u007f\r\n", unicode: true));
        Assert.False(ini.KeyExists("N", "Del"));
        Assert.True(ini.KeyExists("N", "Later"));
    }

    [WindowsNativeIniFact]
    public void MutableFileNameAppliesAcrossNumericWritesAndSections()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var first = owned.Seed("[First]\r\nV=1\r\n");
        var second = owned.Seed("[Second]\r\nV=2\r\n");
        var ini = owned.Adapter(first);
        Assert.Equal(1u, ini.ReadUInt32("First", "V"));
        ini.FileName = second;
        Assert.Equal(new[] { "Second" }, ini.GetSectionNames());
        ini.WriteUInt32("Second", "V", 3);
        Assert.Equal(3u, ini.ReadUInt32("Second", "V"));
        Assert.Equal(1u, owned.Adapter(first).ReadUInt32("First", "V"));
        owned.CaptureExpectedMutation(second);
    }

    [WindowsNativeIniFact]
    public void RepeatedStringWritesPreserveOtherSectionEntries()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var path = owned.Seed("[N]\r\nSentinel=kept\r\n[Other]\r\nUntouched=yes\r\n");
        var ini = owned.Adapter(path);
        ini.WriteString("N", "V", "one");
        ini.WriteString("N", "V", "two");
        Assert.Equal("two", ini.ReadString("N", "V", ""));
        Assert.Equal("kept", ini.ReadString("N", "Sentinel", ""));
        Assert.Equal("yes", ini.ReadString("Other", "Untouched", ""));
        owned.CaptureExpectedMutation(path);
    }

    [WindowsNativeIniFact]
    public void GenericWriteToStringFaultLeavesOwnedFileUnchanged()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        var path = owned.Seed("[N]\r\nV=kept\r\n");
        var before = owned.Bytes(path);
        Assert.Throws<InvalidOperationException>(() => owned.Adapter(path).Write("N", "V", new ThrowingValue()));
        Assert.Equal(before, owned.Bytes(path));
    }

    [WindowsNativeIniFact]
    public void UInt32MutableParserCaptureKeepsFourteenCharacterPayload()
    {
        using var owned = new LegacyNativeIniCompleteOwnedProfiles();
        using var lease = new LegacyNativeIniCompleteParserLease();
        var ini = owned.Adapter(owned.Seed("[N]\r\nV=12345678901234567890\r\n"));
        string? observed = null;
        lease.Set("ToUInt32", new Func<string, uint>(s => { observed = s; return 99; }));
        Assert.Equal(99u, ((ILegacyNativeItemCounterShellIni)ini).ReadUInt32("N", "V"));
        Assert.Equal("12345678901234", observed);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private unsafe static extern bool GetPrivateProfileStructW(
        string section, string key, void* buffer, int size, string fileName);
}
