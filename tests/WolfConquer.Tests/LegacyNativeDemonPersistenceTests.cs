using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeDemonPersistenceTests
{
    [Fact]
    public void DefaultScalarSerializationHasFourFieldsAndTrailingSeparator()
    {
        var demon = new LegacyNativeDemonExterminatorRuntime();
        Assert.Equal("0/0/0/0/", demon.ToString());
        Assert.False(demon.OnQuest);
    }

    [Fact]
    public void ScalarWidthMaximaRoundTripWithoutOnQuestSerialization()
    {
        var original = new LegacyNativeDemonExterminatorRuntime
            { ItemUID = uint.MaxValue, FinishToday = byte.MaxValue, HuntKills = ushort.MaxValue,
              QuestTyp = uint.MaxValue, OnQuest = true };
        Assert.Equal("4294967295/255/65535/4294967295/", original.ToString());
        var restored = new LegacyNativeDemonExterminatorRuntime();
        restored.ReadLine(original.ToString());
        AssertScalars(restored, uint.MaxValue, byte.MaxValue, ushort.MaxValue, uint.MaxValue);
        Assert.False(restored.OnQuest);
        Assert.True(original.OnQuest);
    }

    [Fact]
    public void NullEmptyOrSeparatorFreeInputOverwritesAllScalarsWithDefaults()
    {
        foreach (var input in new string?[] { null, "", "123", "not-a-line" })
        {
            var demon = Seeded();
            demon.ReadLine(input);
            AssertScalars(demon, 0, 0, 0, 0);
            Assert.True(demon.OnQuest);
        }
    }

    [Fact]
    public void EmptyAndMissingColumnsRetainPositionsRatherThanCompacting()
    {
        var demon = Seeded();
        demon.ReadLine("7//9");
        AssertScalars(demon, 7, 0, 9, 0);
        demon.ReadLine("/2//4/");
        AssertScalars(demon, 0, 2, 0, 4);
        demon.ReadLine("/");
        AssertScalars(demon, 0, 0, 0, 0);
    }

    [Fact]
    public void MalformedAndOverflowColumnsDefaultIndependentlyAndAdvance()
    {
        var demon = Seeded();
        demon.ReadLine("bad/2/3/4/");
        AssertScalars(demon, 0, 2, 3, 4);
        demon.ReadLine("7/256/65536/4294967296/");
        AssertScalars(demon, 7, 0, 0, 0);
        demon.ReadLine("4294967296/255/65535/8/");
        AssertScalars(demon, 0, 255, 65535, 8);
    }

    [Fact]
    public void ScalarReadUsesNativeWhitespaceAndSignedIntegerTryParseRules()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        var demon = Seeded();
        demon.ReadLine(" +7 / +2 / +3 / +4 /");
        AssertScalars(demon, 7, 2, 3, 4);
        demon.ReadLine("-1/-1/-1/-1/");
        AssertScalars(demon, 0, 0, 0, 0);
        demon.ReadLine("1,000/2.0/3e1/0x4/");
        AssertScalars(demon, 0, 0, 0, 0);
    }

    [Fact]
    public void ExtraColumnsAreIgnoredRatherThanRejectedOrPreserved()
    {
        var demon = Seeded();
        demon.ReadLine("7/2/3/4/extra/255/true");
        AssertScalars(demon, 7, 2, 3, 4);
        Assert.Equal("7/2/3/4/", demon.ToString());
    }

    [Fact]
    public void RepeatedReadReplacesEachScalarAndDoesNotAccumulateOldState()
    {
        var demon = Seeded();
        demon.ReadLine("8/9/10/11/");
        AssertScalars(demon, 8, 9, 10, 11);
        demon.ReadLine("12/");
        AssertScalars(demon, 12, 0, 0, 0);
        Assert.True(demon.OnQuest);
    }

    [Fact]
    public void SerializationRereadsFieldsWithoutMutatingScalarsOrOnQuest()
    {
        var demon = Seeded();
        Assert.Equal("7/2/3/4/", demon.ToString());
        demon.ItemUID = 8;
        demon.FinishToday = 9;
        demon.HuntKills = 10;
        demon.QuestTyp = 11;
        demon.OnQuest = false;
        Assert.Equal("8/9/10/11/", demon.ToString());
        AssertScalars(demon, 8, 9, 10, 11);
        Assert.False(demon.OnQuest);
    }

    [Fact]
    public void ReadLeavesSameCachedItemAndOnQuestUntouched()
    {
        var (demon, client, item) = Cached();
        client.Inventory = null;
        demon.ReadLine("7/2/30/4/");
        Assert.True(demon.OnQuest);
        item.MaximDurability = 12;
        demon.UppdateJar(null!, 71);
        AssertScalars(demon, 7, 2, 31, 4);
        Assert.Equal("7/2/31/4/", demon.ToString());
        demon.UppdateJar(null!, 71);
        Assert.Equal((ushort)32, demon.HuntKills);
    }

    [Fact]
    public void LoadingZeroDoesNotClearDormantCacheBeforeSameUidIsLoadedAgain()
    {
        var (demon, client, _) = Cached();
        client.Inventory = null;
        demon.ReadLine(null);
        demon.UppdateJar(null!, 72);
        AssertScalars(demon, 0, 0, 0, 0);
        demon.ReadLine("7/2/3/4/");
        demon.UppdateJar(null!, 72);
        AssertScalars(demon, 7, 2, 4, 4);
        Assert.True(demon.OnQuest);
    }

    [Fact]
    public void ChangedUidReadIsNonThrowingButNextRequiredLookupKeepsNativeFault()
    {
        var (demon, _, _) = Cached();
        demon.ReadLine("8/2/3/4/");
        AssertScalars(demon, 8, 2, 3, 4);
        Assert.Throws<NullReferenceException>(() => demon.UppdateJar(null!, 72));
        AssertScalars(demon, 8, 2, 3, 4);
    }

    [Fact]
    public void NewRoundTripOwnerDoesNotAcquireTheOriginalPrivateItemCache()
    {
        var (original, client, _) = Cached();
        var restored = new LegacyNativeDemonExterminatorRuntime();
        restored.ReadLine(original.ToString());
        Assert.False(restored.OnQuest);
        Assert.Throws<NullReferenceException>(() => restored.UppdateJar(null!, 72));
        client.Inventory = null;
        original.UppdateJar(null!, 72);
        Assert.Equal((ushort)2, original.HuntKills);
        Assert.Equal((ushort)1, restored.HuntKills);
    }

    [Fact]
    public void ReaderValidityRequiresActualSeparatorAndCountIncludesEmptyTokens()
    {
        foreach (var input in new string?[] { null, "", "7" })
        {
            using var invalid = new LegacyNativeDbReadLine(input, '/');
            Assert.False(invalid.Valid);
            Assert.Equal(0, invalid.GetCount());
            Assert.Equal(9U, invalid.Read(9U));
        }
        using var valid = new LegacyNativeDbReadLine("/7/", '/');
        Assert.True(valid.Valid);
        Assert.Equal(3, valid.GetCount());
    }

    [Fact]
    public void ReaderAllNativeOverloadsShareOneCursorAndExhaustToSuppliedDefaults()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        using var reader = new LegacyNativeDbReadLine(
            "-2147483648/4294967295/255/-128/65535/18446744073709551615/-9223372036854775808/text", '/');
        Assert.Equal(int.MinValue, reader.Read(0));
        Assert.Equal(uint.MaxValue, reader.Read(0U));
        Assert.Equal(byte.MaxValue, reader.Read((byte)0));
        Assert.Equal(sbyte.MinValue, reader.Read((sbyte)0));
        Assert.Equal(ushort.MaxValue, reader.Read((ushort)0));
        Assert.Equal(ulong.MaxValue, reader.Read(0UL));
        Assert.Equal(long.MinValue, reader.Read(0L));
        Assert.Equal("text", reader.Read("fallback"));
        Assert.Equal(10, reader.Read(10));
        Assert.Equal(11U, reader.Read(11U));
        Assert.Equal((byte)12, reader.Read((byte)12));
        Assert.Equal((sbyte)13, reader.Read((sbyte)13));
        Assert.Equal((ushort)14, reader.Read((ushort)14));
        Assert.Equal(15UL, reader.Read(15UL));
        Assert.Equal(16L, reader.Read(16L));
        Assert.Equal("end", reader.Read("end"));
        Assert.Equal(8, reader.GetCount());
    }

    [Fact]
    public void NumericMalformedTokensConsumeOneColumnForEveryReadOverload()
    {
        using var reader = new LegacyNativeDbReadLine("x/x/x/x/x/x/x/last", '/');
        Assert.Equal(1, reader.Read(1));
        Assert.Equal(2U, reader.Read(2U));
        Assert.Equal((byte)3, reader.Read((byte)3));
        Assert.Equal((sbyte)4, reader.Read((sbyte)4));
        Assert.Equal((ushort)5, reader.Read((ushort)5));
        Assert.Equal(6UL, reader.Read(6UL));
        Assert.Equal(7L, reader.Read(7L));
        Assert.Equal("last", reader.Read("fallback"));
    }

    [Fact]
    public void EveryNumericOverflowDefaultsWithoutThrowingOrStickingTheCursor()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        using var reader = new LegacyNativeDbReadLine(
            "2147483648/4294967296/256/128/65536/18446744073709551616/9223372036854775808/end", '/');
        Assert.Equal(1, reader.Read(1));
        Assert.Equal(2U, reader.Read(2U));
        Assert.Equal((byte)3, reader.Read((byte)3));
        Assert.Equal((sbyte)4, reader.Read((sbyte)4));
        Assert.Equal((ushort)5, reader.Read((ushort)5));
        Assert.Equal(6UL, reader.Read(6UL));
        Assert.Equal(7L, reader.Read(7L));
        Assert.Equal("end", reader.Read("fallback"));
    }

    [Fact]
    public void StringReadDoesNotTrimOrReplaceEmptyTokens()
    {
        using var reader = new LegacyNativeDbReadLine("  text  //", '/');
        Assert.Equal("  text  ", reader.Read("fallback"));
        Assert.Equal("", reader.Read("fallback"));
        Assert.Equal("", reader.Read("fallback"));
        Assert.Equal("fallback", reader.Read("fallback"));
    }

    [Fact]
    public void InvalidReadStillAdvancesSharedCursorBeforeValidityIsRestored()
    {
        using var reader = new LegacyNativeDbReadLine("1/2/3", '/');
        reader.Valid = false;
        Assert.Equal(99, reader.Read(99));
        reader.Valid = true;
        Assert.Equal(2, reader.Read(0));
        Assert.Equal(3U, reader.Read(0U));
    }

    [Fact]
    public void DisposeOnlyNullsDataAndLeavesValidReaderFaultsObservable()
    {
        var reader = new LegacyNativeDbReadLine("1/2", '/');
        Assert.Equal(1, reader.Read(0));
        reader.Dispose();
        Assert.True(reader.Valid);
        Assert.Equal(0, reader.GetCount());
        Assert.Throws<NullReferenceException>(() => reader.Read(7));
        Assert.Throws<NullReferenceException>(() => reader.Read("fallback"));
        reader.Dispose();
        Assert.True(reader.Valid);
    }

    [Fact]
    public void DisposingInvalidReaderPreservesDefaultBehavior()
    {
        var reader = new LegacyNativeDbReadLine(null, '/');
        reader.Dispose();
        reader.Dispose();
        Assert.False(reader.Valid);
        Assert.Equal(0, reader.GetCount());
        Assert.Equal(7U, reader.Read(7U));
        Assert.Equal("fallback", reader.Read("fallback"));
    }

    [Fact]
    public void ManuallySettingValidDoesNotFabricateMissingData()
    {
        using var reader = new LegacyNativeDbReadLine("7", '/');
        reader.Valid = true;
        Assert.Throws<NullReferenceException>(() => reader.Read(0U));
        Assert.Equal(0, reader.GetCount());
    }

    [Fact]
    public void WriterAllNativeOverloadsAppendInOrderWithNoLeadingSeparator()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        var writer = new LegacyNativeDbWriteLine('/');
        Assert.Same(writer, writer.Add(new[] { -1, 2 }));
        Assert.Same(writer, writer.Add((byte)3).Add((sbyte)-4).Add((short)-5)
            .Add((ushort)6).Add(7U).Add(-8).Add(-9L).Add(10UL).Add("tail"));
        Assert.Equal("-1/2/3/-4/-5/6/7/-8/-9/10/tail/", writer.Close());
    }

    [Fact]
    public void CloseReturnsCurrentTextWithoutFinalizingOrClearingBuilder()
    {
        var writer = new LegacyNativeDbWriteLine('|');
        Assert.Equal("", writer.Close());
        writer.Add(Array.Empty<int>());
        Assert.Equal("", writer.Close());
        writer.Add(7U);
        Assert.Equal("7|", writer.Close());
        Assert.Equal("7|", writer.Close());
        writer.Add((byte)2);
        Assert.Equal("7|2|", writer.Close());
    }

    [Fact]
    public void StringWriterDoesNotEscapeSeparatorOrLineBreaks()
    {
        var writer = new LegacyNativeDbWriteLine('/');
        writer.Add("a/b\r\nc").Add("");
        Assert.Equal("a/b\r\nc//", writer.Close());
    }

    [Fact]
    public void NullStringFaultLeavesPreviouslyWrittenPrefixAndAllowsFurtherAppends()
    {
        var writer = new LegacyNativeDbWriteLine('/');
        writer.Add(7U).Add((byte)2);
        Assert.Throws<NullReferenceException>(() => writer.Add((string)null!));
        Assert.Equal("7/2/", writer.Close());
        writer.Add((ushort)3);
        Assert.Equal("7/2/3/", writer.Close());
    }

    [Fact]
    public void NullArrayFaultDoesNotRollbackPreviouslyWrittenPrefix()
    {
        var writer = new LegacyNativeDbWriteLine('/');
        writer.Add("prefix");
        Assert.Throws<NullReferenceException>(() => writer.Add((int[])null!));
        Assert.Equal("prefix/", writer.Close());
    }

    [Fact]
    public void NativeHelpersAndDemonReadUseCurrentCultureSignsNotInvariantParsing()
    {
        var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        custom.NumberFormat.NegativeSign = "m";
        custom.NumberFormat.PositiveSign = "p";
        using var culture = new CultureScope(custom);
        var writer = new LegacyNativeDbWriteLine('/');
        writer.Add(-7).Add((short)-8).Add((sbyte)-9).Add(long.MinValue);
        Assert.Equal("m7/m8/m9/m9223372036854775808/", writer.Close());
        using var reader = new LegacyNativeDbReadLine(writer.Close(), '/');
        Assert.Equal(-7, reader.Read(0));
        Assert.Equal(-8, reader.Read(0));
        Assert.Equal((sbyte)-9, reader.Read((sbyte)0));
        Assert.Equal(long.MinValue, reader.Read(0L));
        var demon = Seeded();
        demon.ReadLine("p7/p2/p3/p4/");
        AssertScalars(demon, 7, 2, 3, 4);
        Assert.Equal("7/2/3/4/", demon.ToString());
    }

    [Fact]
    public void StringReadUsesNullDefaultOnlyWhenInvalidOrPastEnd()
    {
        using var invalid = new LegacyNativeDbReadLine(null, '/');
        Assert.Null(invalid.Read((string?)null));
        using var valid = new LegacyNativeDbReadLine("/", '/');
        Assert.Equal("", valid.Read((string?)null));
        Assert.Equal("", valid.Read((string?)null));
        Assert.Null(valid.Read((string?)null));
    }

    [Fact]
    public void InvalidReaderDefaultsAcrossAllOverloadsWithoutParsingSingleToken()
    {
        using var reader = new LegacyNativeDbReadLine("123", '/');
        Assert.Equal(1, reader.Read(1));
        Assert.Equal(2U, reader.Read(2U));
        Assert.Equal((byte)3, reader.Read((byte)3));
        Assert.Equal((sbyte)4, reader.Read((sbyte)4));
        Assert.Equal((ushort)5, reader.Read((ushort)5));
        Assert.Equal(6UL, reader.Read(6UL));
        Assert.Equal(7L, reader.Read(7L));
        Assert.Equal("fallback", reader.Read("fallback"));
    }

    private static LegacyNativeDemonExterminatorRuntime Seeded() => new()
        { ItemUID = 7, FinishToday = 2, HuntKills = 3, QuestTyp = 4, OnQuest = true };

    private static (LegacyNativeDemonExterminatorRuntime Demon, LegacyNativeGameClientRuntime Client,
        LegacyNativeGameItemRuntime Item) Cached()
    {
        var client = new LegacyNativeGameClientRuntime();
        client.Inventory = new LegacyNativeInventoryRuntime(client);
        var item = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 13 };
        client.Inventory.ClientItems[7] = item;
        var demon = new LegacyNativeDemonExterminatorRuntime { ItemUID = 7, OnQuest = true };
        demon.UppdateJar(client, 72);
        return (demon, client, item);
    }

    private static void AssertScalars(LegacyNativeDemonExterminatorRuntime demon,
        uint itemUid, byte finishToday, ushort huntKills, uint questTyp)
    {
        Assert.Equal(itemUid, demon.ItemUID);
        Assert.Equal(finishToday, demon.FinishToday);
        Assert.Equal(huntKills, demon.HuntKills);
        Assert.Equal(questTyp, demon.QuestTyp);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo original = CultureInfo.CurrentCulture;

        internal CultureScope(CultureInfo culture) => CultureInfo.CurrentCulture = culture;

        public void Dispose() => CultureInfo.CurrentCulture = original;
    }
}
