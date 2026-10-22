using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using Command = WolfConquer.Core.Data.LegacyNativeMySqlCommand;
using Kind = WolfConquer.Core.Data.LegacyNativeMySqlCommandType;

namespace WolfConquer.Tests;

public sealed class LegacyNativeMySqlCommandBuilderTests
{
    [Fact]
    public void AllFiveConstructorsRetainTheNativeTemplatesAndType()
    {
        var cases = new (Kind Kind, string Text)[]
        {
            (Kind.SELECT, "SELECT * FROM <R>"),
            (Kind.UPDATE, "UPDATE <R> SET "),
            (Kind.INSERT, "INSERT INTO <R> (<F>) VALUES (<V>)"),
            (Kind.DELETE, "DELETE FROM <R> WHERE <C> = <V>"),
            (Kind.COUNT, "SELECT count(<V>) FROM <R>")
        };
        foreach (var item in cases)
        {
            using IDisposable owner = new Command(item.Kind);
            var command = (Command)owner;
            Assert.Equal(item.Kind, command.Type);
            Assert.Equal(item.Text, command.Command);
        }
    }

    [Fact]
    public void UnknownEnumConstructorDoesNotInventAnInitializedFallback()
    {
        var kind = (Kind)int.MaxValue;
        using var command = (IDisposable)new Command(kind);
        var value = (Command)command;
        Assert.Equal(kind, value.Type);
        Assert.Throws<NullReferenceException>(() => _ = value.Command);
    }

    [Fact]
    public void PublicTypeMutationDoesNotReconstructTheBuilderOrInsertCaches()
    {
        using IDisposable owner = new Command(Kind.SELECT);
        var command = (Command)owner;
        command.Type = Kind.UPDATE;
        command.Set("c", 1L);
        Assert.Equal("SELECT * FROM <R>`c` = 1 ", command.Command);
        command.Type = Kind.INSERT;
        Assert.Throws<NullReferenceException>(() => command.Insert("c", 1L));
        Assert.Equal("SELECT * FROM <R>`c` = 1 ", command.Command);
    }

    [Fact]
    public void CommandSetterReplacesTextAndAcceptsNullAsTheBclEmptyBuilder()
    {
        using IDisposable owner = new Command(Kind.SELECT);
        var command = (Command)owner;
        command.Command = "custom";
        Assert.Equal("custom", command.Command);
        command.Command = null!;
        Assert.Equal(string.Empty, command.Command);
        Assert.Equal(Kind.SELECT, command.Type);
    }

    [Fact]
    public void TableMethodsReplaceEveryMarkerAndReturnTheSameInstance()
    {
        using IDisposable selectOwner = new Command(Kind.SELECT);
        var select = (Command)selectOwner;
        select.Command = "<R>/<R>";
        Assert.Same(select, select.Select("items"));
        Assert.Equal("`items`/`items`", select.Command);
        using IDisposable countOwner = new Command(Kind.COUNT);
        var count = (Command)countOwner;
        Assert.Same(count, count.Count("items"));
        Assert.Equal("SELECT count(<V>) FROM `items`", count.Command);
        using IDisposable insertOwner = new Command(Kind.INSERT);
        var insert = (Command)insertOwner;
        Assert.Same(insert, insert.Insert("items"));
        Assert.Equal("INSERT INTO `items` (<F>) VALUES (<V>)", insert.Command);
    }

    [Fact]
    public void DeleteOverloadsKeepTheirExactNativeQuotingAndUnsignedWidth()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        AssertDelete(-7L, "DELETE FROM `t` WHERE `c` = -7");
        AssertDelete(ulong.MaxValue, "DELETE FROM `t` WHERE `c` = 18446744073709551615");
        using IDisposable owner = new Command(Kind.DELETE);
        var command = (Command)owner;
        Assert.Same(command, command.Delete("t", "c", true));
        Assert.Equal("DELETE FROM `t` WHERE `c` = 1", command.Command);
        using IDisposable textOwner = new Command(Kind.DELETE);
        var text = (Command)textOwner;
        Assert.Same(text, text.Delete("t", "c", "x' OR 1=1"));
        Assert.Equal("DELETE FROM `t` WHERE `c` = 'x' OR 1=1'", text.Command);
    }

    [Fact]
    public void NullNamesAndNullStringValuesKeepNativeConcatenationNotValidation()
    {
        using IDisposable owner = new Command(Kind.DELETE);
        var command = (Command)owner;
        command.Delete(null!, null!, (string)null!);
        Assert.Equal("DELETE FROM `` WHERE `` = ''", command.Command);
        using IDisposable selectOwner = new Command(Kind.SELECT);
        var select = (Command)selectOwner;
        select.Select(null!);
        Assert.Equal("SELECT * FROM ``", select.Command);
    }

    [Fact]
    public void UpdateAndAllTypedSetsKeepExactNativeSpacesAndCommas()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        using IDisposable owner = new Command(Kind.UPDATE);
        var command = (Command)owner;
        Assert.Same(command, command.Update("t"));
        Assert.Same(command, command.Set("signed", -7L));
        Assert.Same(command, command.Set("unsigned", ulong.MaxValue));
        Assert.Same(command, command.Set("ratio", 1.5));
        Assert.Same(command, command.Set("text", "x'"));
        Assert.Same(command, command.Set("flag", false));
        Assert.Equal("UPDATE `t` SET `signed` = -7 ,`unsigned` = 18446744073709551615 ,`ratio` = 1.5 ,`text` = 'x'' ,`flag` = 0 ", command.Command);
    }

    [Fact]
    public void GenuineLoadCharacterUnsignedOnlineCallBuildsWithoutExecutingSql()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        using IDisposable owner = new Command(Kind.UPDATE);
        var command = (Command)owner;
        uint oldOnline = uint.MaxValue;
        command.Update("configuration").Set("PlayersOnline", oldOnline);
        Assert.Equal("UPDATE `configuration` SET `PlayersOnline` = 4294967295 ", command.Command);
    }

    [Fact]
    public void NonUpdateSetIsNoOpButObjectNullFaultPrecedesItsTypedDispatch()
    {
        using IDisposable owner = new Command(Kind.SELECT);
        var command = (Command)owner;
        var before = command.Command;
        Assert.Same(command, command.Set("c", 1L));
        Assert.Same(command, command.Set("c", 1UL));
        Assert.Same(command, command.Set("c", 1.5));
        Assert.Same(command, command.Set("c", (string)null!));
        Assert.Same(command, command.Set("c", true));
        Assert.Equal(before, command.Command);
        Assert.Throws<NullReferenceException>(() => command.Set("c", (object)null!));
        Assert.Equal(before, command.Command);
    }

    [Fact]
    public void ObjectSetUsesBoolBranchAndExactlyOneRealToString()
    {
        using IDisposable owner = new Command(Kind.UPDATE);
        var command = (Command)owner;
        var probe = new ToStringProbe("value'");
        command.Set("flag", (object)true);
        command.Set("probe", (object)probe);
        Assert.Equal(1, probe.Calls);
        Assert.Equal("UPDATE <R> SET `flag` = 1 ,`probe` = 'value'' ", command.Command);
        Assert.Throws<NullReferenceException>(() => command.Set("null", (object)null!));
        Assert.Equal(1, probe.Calls);
    }

    [Fact]
    public void CommandSetterDoesNotResetTheAlreadyConsumedCommaState()
    {
        using IDisposable owner = new Command(Kind.UPDATE);
        var command = (Command)owner;
        command.Set("first", 1L);
        command.Command = "abc";
        command.Set("next", 2L);
        Assert.Equal("abc,`next` = 2 ", command.Command);
    }

    [Fact]
    public void ShortCommandFaultKeepsNativeCommaIndexOrderAndPartialState()
    {
        using IDisposable owner = new Command(Kind.UPDATE);
        var command = (Command)owner;
        command.Set("first", 1L);
        command.Command = "x";
        Assert.Throws<IndexOutOfRangeException>(() => command.Set("next", 2L));
        Assert.Equal("x", command.Command);
        Assert.False(Private<bool>(command, "firstPart"));
        command.Command = "x,";
        command.Set("next", 2L);
        Assert.Equal("x,`next` = 2 ", command.Command);
    }

    [Fact]
    public void CommaExaminesOnlyTheLastThreeCharactersWithoutNormalizingText()
    {
        using IDisposable owner = new Command(Kind.UPDATE);
        var command = (Command)owner;
        command.Set("first", 1L);
        command.Command = "x,y";
        command.Set("next", 2L);
        Assert.Equal("x,y`next` = 2 ", command.Command);
        command.Command = "x,yz";
        command.Set("next", 2L);
        Assert.Equal("x,yz`next` = 2 ", command.Command);
        command.Command = ",xyz";
        command.Set("next", 2L);
        Assert.Equal(",xyz,`next` = 2 ", command.Command);
    }

    [Fact]
    public void CurrentCultureControlsNumericConcatenationAndToString()
    {
        var selected = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        selected.NumberFormat.NegativeSign = "m";
        selected.NumberFormat.NumberDecimalSeparator = ",";
        using var culture = new CultureScope(selected);
        using IDisposable owner = new Command(Kind.UPDATE);
        var command = (Command)owner;
        command.Set("signed", -7L).Set("ratio", 1.5).Set("boxed", (object)(-9));
        Assert.Equal("UPDATE <R> SET `signed` = m7 ,`ratio` = 1,5 `boxed` = 'm9' ", command.Command);
    }

    [Fact]
    public void InsertKeepsActualByteStringCachesAndDefersPlaceholderExpansion()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        using IDisposable owner = new Command(Kind.INSERT);
        var command = (Command)owner;
        command.Insert("t");
        Assert.Same(command, command.Insert("signed", -7L));
        Assert.Same(command, command.Insert("unsigned", ulong.MaxValue));
        Assert.Same(command, command.Insert("flag", true));
        var text = new string("中文'".ToCharArray());
        Assert.Same(command, command.Insert("text", text));
        Assert.Equal("INSERT INTO `t` (<F>) VALUES (<V>)", command.Command);
        var fields = Private<LegacyNativeDbSafeDictionary<byte, string>>(command, "insertFields");
        var values = Private<LegacyNativeDbSafeDictionary<byte, string>>(command, "insertValues");
        Assert.Equal((byte)4, Private<byte>(command, "lastpair"));
        Assert.Equal("signed", fields[0]);
        Assert.Equal("-7", values[0]);
        Assert.Equal("18446744073709551615", values[1]);
        Assert.Equal("1", values[2]);
        Assert.Same(text, values[3]);
    }

    [Fact]
    public void NullInsertStringFaultsBeforeEitherCacheAdmissionOrPairIncrement()
    {
        using IDisposable owner = new Command(Kind.INSERT);
        var command = (Command)owner;
        Assert.Throws<NullReferenceException>(() => command.Insert("field", (string)null!));
        Assert.Empty(Private<LegacyNativeDbSafeDictionary<byte, string>>(command, "insertFields"));
        Assert.Empty(Private<LegacyNativeDbSafeDictionary<byte, string>>(command, "insertValues"));
        Assert.Equal((byte)0, Private<byte>(command, "lastpair"));
        Assert.Equal("INSERT INTO <R> (<F>) VALUES (<V>)", command.Command);
    }

    [Fact]
    public void BytePairWrapOverwritesKeyZeroRatherThanAddingAValidationOrCap()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        using IDisposable owner = new Command(Kind.INSERT);
        var command = (Command)owner;
        // One compiled Fact with a local loop, not256 source-model test rows.
        for (int index = 0; index < 256; index++)
            command.Insert("field" + index, (long)index);
        var fields = Private<LegacyNativeDbSafeDictionary<byte, string>>(command, "insertFields");
        var values = Private<LegacyNativeDbSafeDictionary<byte, string>>(command, "insertValues");
        Assert.Equal(256, fields.Count);
        Assert.Equal((byte)0, Private<byte>(command, "lastpair"));
        command.Insert("replacement", 999L);
        Assert.Equal(256, fields.Count);
        Assert.Equal("replacement", fields[0]);
        Assert.Equal("999", values[0]);
        Assert.Equal("field255", fields[255]);
        Assert.Equal((byte)1, Private<byte>(command, "lastpair"));
    }

    [Fact]
    public void WhereAndComparisonOverloadsKeepTheirNativeMissingLeadingSpace()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        AssertWhere(c => c.Where("c", -7L), "SELECT * FROM <R>WHERE `c` = -7");
        AssertWhere(c => c.Where("c", ulong.MaxValue), "SELECT * FROM <R>WHERE `c` = 18446744073709551615");
        AssertWhere(c => c.Where("c", "x'"), "SELECT * FROM <R>WHERE `c` = 'x''");
        AssertWhere(c => c.Where("c", true), "SELECT * FROM <R>WHERE `c` = 1");
        AssertWhere(c => c.Where("c", 7L, true), "SELECT * FROM <R>WHERE `c` > 7");
        AssertWhere(c => c.Where("c", 7L, false), "SELECT * FROM <R>WHERE `c` < 7");
    }

    [Fact]
    public void AndOrOrderKeepAllOverloadsCapitalizationAndMissingOrderSpace()
    {
        using var culture = new CultureScope(CultureInfo.InvariantCulture);
        using IDisposable owner = new Command(Kind.SELECT);
        var command = (Command)owner;
        command.And("a", -7L).And("b", 8UL).And("c", "x'").And("d", false)
            .And("e", 9L, true).And("f", 10L, false)
            .Or("g", -11L).Or("h", 12UL).Or("i", "y'").Or("j", true).Order("z");
        Assert.Equal("SELECT * FROM <R> AND `a` = -7 AND `b` = 8 AND `c` = 'x'' AND `d` = 0 AND `e` > 9 AND `f` < 10 Or `g` = -11 Or `h` = 12 Or `i` = 'y'' Or `j` = 1ORDER BY z", command.Command);
    }

    [Fact]
    public void ExplicitDisposeClearsActualCachesWithoutResettingNativePairState()
    {
        var command = new Command(Kind.INSERT);
        command.Insert("a", 7L);
        var fields = Private<LegacyNativeDbSafeDictionary<byte, string>>(command, "insertFields");
        var values = Private<LegacyNativeDbSafeDictionary<byte, string>>(command, "insertValues");
        var cached = fields.GetValues();
        ((IDisposable)command).Dispose();
        Assert.Empty(fields);
        Assert.Empty(values);
        Assert.True(fields.Update);
        Assert.Same(cached, fields.MyArray);
        Assert.Equal((byte)1, Private<byte>(command, "lastpair"));
        Assert.Equal(Kind.INSERT, command.Type);
        Assert.Throws<NullReferenceException>(() => _ = command.Command);
        ((IDisposable)command).Dispose();
        command.Command = "reused";
        command.Insert("b", 8L);
        Assert.Equal("b", fields[1]);
        Assert.Equal("8", values[1]);
        Assert.Equal((byte)2, Private<byte>(command, "lastpair"));
        ((IDisposable)command).Dispose();
    }

    [Fact]
    public void PureOwnerRetainsProtectedBuilderAndOnlyExplicitDisposeSurface()
    {
        var type = typeof(Command);
        Assert.False(type.IsPublic);
        Assert.False(type.IsSealed);
        Assert.True(type.GetField("_command", BindingFlags.Instance | BindingFlags.NonPublic)!.IsFamily);
        Assert.Null(type.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(type.GetMethod("System.IDisposable.Dispose", BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.Null(type.TypeInitializer);
    }

    private static T Private<T>(Command command, string name) =>
        (T)typeof(Command).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(command)!;

    private static void AssertWhere(Func<Command, Command> build, string expected)
    {
        using IDisposable owner = new Command(Kind.SELECT);
        var command = (Command)owner;
        Assert.Same(command, build(command));
        Assert.Equal(expected, command.Command);
    }

    private static void AssertDelete(long value, string expected)
    {
        using IDisposable owner = new Command(Kind.DELETE);
        var command = (Command)owner;
        command.Delete("t", "c", value);
        Assert.Equal(expected, command.Command);
    }

    private static void AssertDelete(ulong value, string expected)
    {
        using IDisposable owner = new Command(Kind.DELETE);
        var command = (Command)owner;
        command.Delete("t", "c", value);
        Assert.Equal(expected, command.Command);
    }

    private sealed class ToStringProbe(string text)
    {
        public int Calls;
        public override string ToString()
        {
            Calls++;
            return text;
        }
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;
        public CultureScope(CultureInfo selected) => CultureInfo.CurrentCulture = selected;
        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }
}
