using System.Globalization;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Xunit.Abstractions;
using DbRead = WolfConquer.Core.Data.LegacyNativeDbRead;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeDbReadTests
{
    private readonly ITestOutputHelper output;
    public LegacyNativeDbReadTests(ITestOutputHelper output) => this.output = output;

    private static FieldInfo Slot(string name) =>
        typeof(DbRead).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static int Cursor(DbRead read) => (int)Slot("curent_count").GetValue(read)!;
    private static DbRead Model(string?[]? rows, int? count = null, int cursor = -1, uint aCount = 0)
    {
        var read = new DbRead("inert-never-read", true) { Count = count ?? rows?.Length ?? 0, aCount = aCount };
        Slot("items").SetValue(read, rows);
        Slot("curent_count").SetValue(read, cursor);
        return read;
    }

    [Fact]
    public void NativeStorageOrderWidthsDefaultsAndDisposableShapeRemainLiteral()
    {
        var fields = typeof(DbRead).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "location", "SR", "Count", "items", "curent_count", "aCount" },
            fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(string), typeof(StreamReader), typeof(int), typeof(string[]), typeof(int), typeof(uint) },
            fields.Select(field => field.FieldType));
        Assert.Equal(new[] { "Count", "aCount" }, fields.Where(field => field.IsPublic).Select(field => field.Name));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(DbRead)));
        Assert.False(typeof(DbRead).IsSealed);
        using var read = new DbRead("inert-never-read", true);
        Assert.Equal("inert-never-read", Slot("location").GetValue(read));
        Assert.Null(Slot("SR").GetValue(read));
        Assert.Null(read.OutBase());
        Assert.Equal(0, read.Count);
        Assert.Equal(-1, Cursor(read));
        Assert.Equal(0u, read.aCount);
    }

    [Fact]
    public void ConstructorConcatenatesRawConfigOrStoresOverrideWithoutReadingAFile()
    {
        var before = LegacyNativeServerConfig.DbLocation;
        try
        {
            LegacyNativeServerConfig.DbLocation = "raw-prefix";
            using var prefixed = new DbRead("rows.txt");
            using var absolute = new DbRead("override-only", true);
            Assert.Equal("raw-prefixrows.txt", Slot("location").GetValue(prefixed));
            Assert.Equal("override-only", Slot("location").GetValue(absolute));
            LegacyNativeServerConfig.DbLocation = null!;
            using var empty = new DbRead(null);
            using var directNull = new DbRead(null, true);
            Assert.Equal("", Slot("location").GetValue(empty));
            Assert.Null(Slot("location").GetValue(directNull));
            Assert.Null(empty.OutBase());
        }
        finally { LegacyNativeServerConfig.DbLocation = before; }
    }

    [Fact]
    public void OutBaseReturnsTheSameInjectedArrayWithoutCopyOrNormalization()
    {
        string?[] rows = { "first", null, "" };
        using var read = Model(rows);
        Assert.Same(rows, read.OutBase());
        read.OutBase()![0] = "changed";
        Assert.Equal("changed", read.ReadString("default"));
        rows[1] = "second";
        Assert.Equal("second", read.ReadString("default"));
        Assert.Equal("", read.ReadString("default"));
    }

    [Fact]
    public void UseReadOnUnbornStorageIncrementsBeforeItsNullFault()
    {
        using var read = new DbRead("inert-never-read", true);
        Assert.Throws<NullReferenceException>(() => read.UseRead());
        Assert.Equal(1u, read.aCount);
        Assert.Equal(-1, Cursor(read));
    }

    [Fact]
    public void UseReadUsesArrayLengthRatherThanCountAndKeepsIncrementingPastFalse()
    {
        using var read = Model(new[] { "a", "b" }, count: -7);
        Assert.True(read.UseRead());
        Assert.True(read.UseRead());
        Assert.False(read.UseRead());
        Assert.False(read.UseRead());
        Assert.Equal(4u, read.aCount);
        Assert.Equal(-1, Cursor(read));
        Assert.Equal(-7, read.Count);
    }

    [Fact]
    public void UseReadWrapsUnsignedMaxToZeroBeforeTheLengthComparison()
    {
        using var read = Model(Array.Empty<string>(), aCount: uint.MaxValue);
        Assert.True(read.UseRead());
        Assert.Equal(0u, read.aCount);
        Assert.False(read.UseRead());
        Assert.Equal(1u, read.aCount);
    }

    [Fact]
    public void FiveCursorReadersShareExactlyOnePreincrementingCursor()
    {
        using var read = Model(new[] { "11", "22", "33", "44", "text" });
        Assert.Equal(11u, read.ReadUInt32(1));
        Assert.Equal((ushort)22, read.ReadUInt16(2));
        Assert.Equal(33UL, read.ReadUInt64(3));
        Assert.Equal((byte)44, read.ReadByte(4));
        Assert.Equal("text", read.ReadString("fallback"));
        Assert.Equal(4, Cursor(read));
        Assert.Equal("after", read.ReadString("after"));
        Assert.Equal(5, Cursor(read));
        Assert.Equal(0u, read.aCount);
    }

    [Fact]
    public void NumericReadersRetainUnsignedMaximumWidths()
    {
        using var read = Model(new[] { "4294967295", "65535", "18446744073709551615", "255" });
        Assert.Equal(uint.MaxValue, read.ReadUInt32(0));
        Assert.Equal(ushort.MaxValue, read.ReadUInt16(0));
        Assert.Equal(ulong.MaxValue, read.ReadUInt64(0));
        Assert.Equal(byte.MaxValue, read.ReadByte(0));
    }

    [Fact]
    public void NullRowsReturnEachCallerDefaultWithoutParsingOrRejectingNull()
    {
        using var read = Model(new string?[5]);
        Assert.Equal(10u, read.ReadUInt32(10));
        Assert.Equal((ushort)11, read.ReadUInt16(11));
        Assert.Equal(12UL, read.ReadUInt64(12));
        Assert.Equal((byte)13, read.ReadByte(13));
        Assert.Null(read.ReadString(null!));
        Assert.Equal(4, Cursor(read));
    }

    [Fact]
    public void ParseFaultsEscapeAndStillConsumeEachCursorPosition()
    {
        using var read = Model(new[] { "bad", "65536", "18446744073709551616", "256", "tail" });
        Assert.Throws<FormatException>(() => read.ReadUInt32(10));
        Assert.Throws<OverflowException>(() => read.ReadUInt16(11));
        Assert.Throws<OverflowException>(() => read.ReadUInt64(12));
        Assert.Throws<OverflowException>(() => read.ReadByte(13));
        Assert.Equal("tail", read.ReadString("fallback"));
        Assert.Equal(4, Cursor(read));
    }

    [Fact]
    public void StringRowsKeepEmptyWhitespaceAndExactObjectIdentity()
    {
        var spaces = new string(' ', 3);
        using var read = Model(new[] { "", spaces, " 123 " });
        Assert.Equal("", read.ReadString("fallback"));
        Assert.Same(spaces, read.ReadString("fallback"));
        Assert.Equal(123u, read.ReadUInt32(0));
    }

    [Fact]
    public void AllFourNumericParsersUseTheCurrentCulture()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.PositiveSign = "p";
            CultureInfo.CurrentCulture = culture;
            using var read = Model(new[] { "p11", "p22", "p33", "p44" });
            Assert.Equal(11u, read.ReadUInt32(0));
            Assert.Equal((ushort)22, read.ReadUInt16(0));
            Assert.Equal(33UL, read.ReadUInt64(0));
            Assert.Equal((byte)44, read.ReadByte(0));
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [Fact]
    public void CountGreaterThanStorageFaultsRatherThanReturningADefault()
    {
        using var read = Model(Array.Empty<string>(), count: 1);
        Assert.Throws<IndexOutOfRangeException>(() => read.ReadString("fallback"));
        Assert.Equal(0, Cursor(read));
    }

    [Fact]
    public void PositiveCountAndNullStorageFaultAfterTheSharedCursorIncrement()
    {
        using var read = Model(null, count: 1);
        Assert.Throws<NullReferenceException>(() => read.ReadUInt32(9));
        Assert.Equal(0, Cursor(read));
    }

    [Fact]
    public void SignedCursorWrapUsesTheWrappedNegativeIndexWithoutRepair()
    {
        using var read = Model(new[] { "value" }, count: 1, cursor: int.MaxValue);
        Assert.Throws<IndexOutOfRangeException>(() => read.ReadString("fallback"));
        Assert.Equal(int.MinValue, Cursor(read));
    }

    [Fact]
    public void DisposeOnlyAssignsFieldsAndDoesNotCloseAnInjectedStream()
    {
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes("still open\n"));
        using var stream = new StreamReader(bytes);
        var rows = new[] { "escaped" };
        var read = Model(rows, cursor: 7, aCount: 9);
        Slot("SR").SetValue(read, stream);
        read.Dispose();
        Assert.Equal("", Slot("location").GetValue(read));
        Assert.Null(Slot("SR").GetValue(read));
        Assert.Null(read.OutBase());
        Assert.Equal(0, read.Count);
        Assert.Equal(7, Cursor(read));
        Assert.Equal(9u, read.aCount);
        Assert.Equal("still open", stream.ReadLine());
        Assert.True(bytes.CanRead);
        Assert.Equal("escaped", rows[0]);
    }

    [Fact]
    public void DisposeDoesNotResetEitherCursorAndUseReadStillIncrementsBeforeNull()
    {
        var read = Model(new[] { "row" }, cursor: 0, aCount: 4);
        read.Dispose();
        Assert.Equal(99u, read.ReadUInt32(99));
        Assert.Equal(1, Cursor(read));
        Assert.Throws<NullReferenceException>(() => read.UseRead());
        Assert.Equal(5u, read.aCount);
        read.Dispose();
        Assert.Equal(1, Cursor(read));
        Assert.Equal(5u, read.aCount);
    }

    [Fact]
    public void PhysicalReaderUsesSecondEqualsTokenAndIgnoresExtraRows()
    {
        using var owned = new LegacyNativeDbReadOwnedFiles();
        using var read = new DbRead(owned.Seed("not-a-name=2=ignored\nfirst\nsecond\nextra\n"), true);
        Assert.True(read.Reader());
        Assert.Equal(2, read.Count);
        Assert.Equal(new[] { "first", "second" }, read.OutBase());
        Assert.Equal("first", read.ReadString("fallback"));
        Assert.Equal("second", read.ReadString("fallback"));
        Assert.Equal("after", read.ReadString("after"));
        var sr = (StreamReader)Slot("SR").GetValue(read)!;
        Assert.Throws<ObjectDisposedException>(() => sr.Peek());
    }

    [Fact]
    public void PhysicalPrematureEofProducesNullRowsAndTheCallerDefaults()
    {
        using var owned = new LegacyNativeDbReadOwnedFiles();
        using var read = new DbRead(owned.Seed("Count=3\n17\n"), true);
        Assert.True(read.Reader());
        Assert.Equal(3, read.Count);
        Assert.Equal(new string?[] { "17", null, null }, read.OutBase());
        Assert.Equal(17u, read.ReadUInt32(9));
        Assert.Equal((ushort)20, read.ReadUInt16(20));
        Assert.Equal("missing", read.ReadString("missing"));
    }

    [Fact]
    public void ZeroCountEarlyReturnKeepsTheEscapedOldItemsAndBothCursors()
    {
        using var owned = new LegacyNativeDbReadOwnedFiles();
        using var read = Model(new[] { "old", "two" }, cursor: 0, aCount: 1);
        Slot("location").SetValue(read, owned.Seed("Count=0\n"));
        var old = read.OutBase();
        Assert.False(read.Reader());
        Assert.Equal(0, read.Count);
        Assert.Same(old, read.OutBase());
        Assert.Equal(0, Cursor(read));
        Assert.Equal(1u, read.aCount);
        Assert.Throws<ObjectDisposedException>(() => ((StreamReader)Slot("SR").GetValue(read)!).Peek());
    }

    [Fact]
    public void SuccessfulReaderCopiesFreshStorageButNeverResetsEitherCursor()
    {
        using var owned = new LegacyNativeDbReadOwnedFiles();
        using var read = new DbRead(owned.Seed("Count=2\nfirst\nsecond\n"), true);
        Assert.True(read.Reader());
        var old = read.OutBase();
        Assert.Equal("first", read.ReadString("fallback"));
        Assert.True(read.UseRead());
        Assert.True(read.Reader());
        Assert.NotSame(old, read.OutBase());
        Assert.Equal(old, read.OutBase());
        Assert.Equal(0, Cursor(read));
        Assert.Equal(1u, read.aCount);
        Assert.Equal("second", read.ReadString("fallback"));
        Assert.True(read.UseRead());
        Assert.False(read.UseRead());
    }

    [Fact]
    public void MissingFileWithUseinvalidFalseReturnsTrueAndNewEmptyStorageAtZeroCount()
    {
        using var owned = new LegacyNativeDbReadOwnedFiles();
        using var read = new DbRead(owned.Missing(), true);
        Assert.True(read.Reader(false));
        Assert.Equal(0, read.Count);
        Assert.NotNull(read.OutBase());
        Assert.Empty(read.OutBase()!);
        Assert.Null(Slot("SR").GetValue(read));
    }

    [Fact]
    public void MissingFalseWithStalePositiveCountReplacesItemsBeforeItsCopyFault()
    {
        using var owned = new LegacyNativeDbReadOwnedFiles();
        using var read = Model(new[] { "old", "two" }, cursor: 0, aCount: 1);
        Slot("location").SetValue(read, owned.Missing());
        var old = read.OutBase();
        Assert.Throws<NullReferenceException>(() => read.Reader(false));
        Assert.Equal(2, read.Count);
        Assert.NotSame(old, read.OutBase());
        Assert.Equal(new string?[2], read.OutBase());
        Assert.Equal(0, Cursor(read));
        Assert.Equal(1u, read.aCount);
    }

    [Fact]
    public void ConstructorCachesTheRawOwnedPrefixAndOverrideUsesItsExactAbsoluteFile()
    {
        using var owned = new LegacyNativeDbReadOwnedFiles();
        var prefixed = owned.Seed("Count=1\nfirst\n", "prefixrows.txt");
        var other = owned.Seed("Count=1\nother\n", "other.txt");
        owned.SetLocation(Path.Combine(owned.Root, "prefix"));
        using var read = new DbRead("rows.txt");
        Assert.Equal(prefixed, Slot("location").GetValue(read));
        owned.SetLocation("never-read-this-config");
        using var direct = new DbRead(other, true);
        Assert.True(read.Reader());
        Assert.True(direct.Reader());
        Assert.Equal("first", read.ReadString("fallback"));
        Assert.Equal("other", direct.ReadString("fallback"));
    }

    [Fact]
    public void PhysicalHeaderCountUsesCurrentCultureWithoutAnInvariantProvider()
    {
        using var owned = new LegacyNativeDbReadOwnedFiles();
        var before = CultureInfo.CurrentCulture;
        try
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.PositiveSign = "p";
            CultureInfo.CurrentCulture = culture;
            using var read = new DbRead(owned.Seed("Header=p2\none\ntwo\n"), true);
            Assert.True(read.Reader());
            Assert.Equal(2, read.Count);
            Assert.Equal(new[] { "one", "two" }, read.OutBase());
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [Fact]
    public void MissingDefaultZeroCountRunsTheRealConsoleBranchOnlyInAFreshWindowsChild()
    {
        var r = LegacyNativeDbReadChildFixture.Run("MissingDefaultZero", output);
        Assert.True(r.GetProperty("Returned").GetBoolean());
        Assert.False(r.GetProperty("ReturnValue").GetBoolean());
        Assert.Equal(0, r.GetProperty("Count").GetInt32());
        Assert.Equal(0, r.GetProperty("ItemsLength").GetInt32());
        Assert.True(r.GetProperty("ItemsNullBefore").GetBoolean());
        Assert.False(r.GetProperty("ItemsNull").GetBoolean());
    }

    private void AssertInnerEarly(string scenario, int count)
    {
        var r = LegacyNativeDbReadChildFixture.Run(scenario, output);
        Assert.True(r.GetProperty("Primed").GetBoolean());
        Assert.True(r.GetProperty("Returned").GetBoolean());
        Assert.False(r.GetProperty("ReturnValue").GetBoolean());
        Assert.Equal(count, r.GetProperty("Count").GetInt32());
        Assert.True(r.GetProperty("SameItems").GetBoolean());
        Assert.Equal("old", r.GetProperty("First").GetString());
        Assert.Equal(0, r.GetProperty("Cursor").GetInt32());
        Assert.Equal(1u, r.GetProperty("ACount").GetUInt32());
        Assert.True(r.GetProperty("SrClosed").GetBoolean());
        Assert.False(r.GetProperty("SameSr").GetBoolean());
    }

    [Fact]
    public void MissingHeaderEqualsInnerCatchReturnsEarlyWithStaleItems() =>
        AssertInnerEarly("HeaderNoEquals", 2);

    [Fact]
    public void EmptyFileHeaderNullInnerCatchReturnsEarlyWithStaleItems() =>
        AssertInnerEarly("HeaderNull", 2);

    [Fact]
    public void InvalidHeaderIntegerInnerCatchReturnsEarlyWithStaleItems() =>
        AssertInnerEarly("HeaderInvalid", 2);

    [Fact]
    public void NegativeHeaderCountCommitsBeforeAllocationFaultAndLeavesStaleItems() =>
        AssertInnerEarly("HeaderNegative", -1);

    [Fact]
    public void OverflowHeaderIntegerNeverOverwritesTheOldCount() =>
        AssertInnerEarly("HeaderOverflow", 2);

    [Fact]
    public void ExclusiveOwnedFileOpenFailureUsesOuterCatchAndThenPublishesEmptyStorage()
    {
        var r = LegacyNativeDbReadChildFixture.Run("ShareZero", output);
        Assert.True(r.GetProperty("Returned").GetBoolean());
        Assert.False(r.GetProperty("ReturnValue").GetBoolean());
        Assert.Equal(0, r.GetProperty("Count").GetInt32());
        Assert.Equal(0, r.GetProperty("ItemsLength").GetInt32());
        Assert.False(r.GetProperty("ItemsNull").GetBoolean());
        Assert.True(r.GetProperty("SameSr").GetBoolean());
    }

    private void AssertPostCopyFault(string scenario)
    {
        var r = LegacyNativeDbReadChildFixture.Run(scenario, output);
        Assert.True(r.GetProperty("Primed").GetBoolean());
        Assert.False(r.GetProperty("Returned").GetBoolean());
        Assert.Equal("System.NullReferenceException", r.GetProperty("ExceptionType").GetString());
        Assert.Equal(2, r.GetProperty("Count").GetInt32());
        Assert.False(r.GetProperty("SameItems").GetBoolean());
        Assert.Equal(2, r.GetProperty("ItemsLength").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, r.GetProperty("First").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, r.GetProperty("Second").ValueKind);
        Assert.Equal(0, r.GetProperty("Cursor").GetInt32());
        Assert.Equal(1u, r.GetProperty("ACount").GetUInt32());
        Assert.True(r.GetProperty("SameSr").GetBoolean());
    }

    [Fact]
    public void OuterOpenFailureWithStaleCountLogsThenFaultsOutsideItsCatch() =>
        AssertPostCopyFault("SharePositive");

    [Fact]
    public void MissingDefaultWithStaleCountLogsThenFaultsOutsideItsCatch() =>
        AssertPostCopyFault("MissingDefaultPositive");
}
