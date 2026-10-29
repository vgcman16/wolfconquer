using MySql.Data.MySqlClient;
using System.Globalization;
using Command = WolfConquer.Core.Data.LegacyNativeMySqlCommand;
using Kind = WolfConquer.Core.Data.LegacyNativeMySqlCommandType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMySqlCommandBuilderTests
{
    [Fact]
    public void SelectBindsHostileTextWithoutChangingSqlStructure()
    {
        const string value = "O'Brien\\folder\0\b\n\r\t\u001a中文😀' OR 1=1; --";
        using var builder = new Command(Kind.SELECT);
        Assert.Same(builder, builder.Select("accounts").Where("Username", value));
        using MySqlCommand command = builder.CreateProviderCommand();
        Assert.Equal("SELECT * FROM `accounts` WHERE `Username` = @p0", command.CommandText);
        Assert.Null(command.Connection);
        AssertParameter(command, 0, MySqlDbType.VarChar, value);
        Assert.DoesNotContain(value, command.CommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateBindsEverySetAndPredicateIncludingTheSecondString()
    {
        using var builder = new Command(Kind.UPDATE);
        builder.Update("accounts").Set("State", 1L).Set("Username", "first'")
            .Set("Description", "second'\\\n").Where("ID", ulong.MaxValue).And("Enabled", true);
        using MySqlCommand command = builder.CreateProviderCommand();
        Assert.Equal("UPDATE `accounts` SET `State` = @p0, `Username` = @p1, `Description` = @p2 WHERE `ID` = @p3 AND `Enabled` = @p4", command.CommandText);
        Assert.Equal(5, command.Parameters.Count);
        AssertParameter(command, 0, MySqlDbType.Int64, 1L);
        AssertParameter(command, 1, MySqlDbType.VarChar, "first'");
        AssertParameter(command, 2, MySqlDbType.VarChar, "second'\\\n");
        AssertParameter(command, 3, MySqlDbType.UInt64, ulong.MaxValue);
        AssertParameter(command, 4, MySqlDbType.Byte, (byte)1);
    }

    [Fact]
    public void InsertRendersEveryFieldAndTypedValueWithoutMutatingOnSnapshots()
    {
        using var builder = new Command(Kind.INSERT);
        builder.Insert("owned_values").Insert("Signed", long.MinValue).Insert("Unsigned", ulong.MaxValue)
            .Insert("Enabled", false).Insert("Text", "中文'\\\0");
        string before = builder.Command;
        using MySqlCommand first = builder.CreateProviderCommand();
        using MySqlCommand second = builder.CreateProviderCommand();
        Assert.Equal("INSERT INTO `owned_values` (`Signed`, `Unsigned`, `Enabled`, `Text`) VALUES (@p0, @p1, @p2, @p3)", first.CommandText);
        Assert.Equal(before, builder.Command);
        Assert.Equal(first.CommandText, second.CommandText);
        Assert.Equal(4, first.Parameters.Count);
        AssertParameter(first, 0, MySqlDbType.Int64, long.MinValue);
        AssertParameter(first, 1, MySqlDbType.UInt64, ulong.MaxValue);
        AssertParameter(first, 2, MySqlDbType.Byte, (byte)0);
        AssertParameter(first, 3, MySqlDbType.VarChar, "中文'\\\0");
        Assert.NotSame(first.Parameters[0], second.Parameters[0]);
        first.Parameters[0].Value = 7L;
        Assert.Equal(long.MinValue, second.Parameters[0].Value);
        using MySqlCommand third = builder.CreateProviderCommand();
        Assert.Equal(long.MinValue, third.Parameters[0].Value);
    }

    [Fact]
    public void InsertHasNoByteCounterWrapAndCanExtendAfterPreparation()
    {
        using var builder = new Command(Kind.INSERT);
        builder.Insert("owned_values");
        for (int index = 0; index < 257; index++) builder.Insert("Field" + index, (long)index);
        using MySqlCommand first = builder.CreateProviderCommand();
        Assert.Equal(257, first.Parameters.Count);
        Assert.Contains("`Field256`", first.CommandText);
        AssertParameter(first, 256, MySqlDbType.Int64, 256L);
        builder.Insert("Tail", "last");
        using MySqlCommand extended = builder.CreateProviderCommand();
        Assert.Equal(258, extended.Parameters.Count);
        AssertParameter(extended, 257, MySqlDbType.VarChar, "last");
        Assert.Equal(257, first.Parameters.Count);
    }

    [Fact]
    public void DeleteRetainsEachTypedOverloadAsBoundData()
    {
        var cases = new (Action<Command> Build, MySqlDbType Type, object Value)[]
        {
            (c => c.Delete("owned_values", "Value", "x' OR 1=1"), MySqlDbType.VarChar, "x' OR 1=1"),
            (c => c.Delete("owned_values", "Value", long.MinValue), MySqlDbType.Int64, long.MinValue),
            (c => c.Delete("owned_values", "Value", ulong.MaxValue), MySqlDbType.UInt64, ulong.MaxValue),
            (c => c.Delete("owned_values", "Value", false), MySqlDbType.Byte, (byte)0)
        };
        foreach (var item in cases)
        {
            using var builder = new Command(Kind.DELETE);
            item.Build(builder);
            using MySqlCommand command = builder.CreateProviderCommand();
            Assert.Equal("DELETE FROM `owned_values` WHERE `Value` = @p0", command.CommandText);
            Assert.Single(command.Parameters.Cast<MySqlParameter>());
            AssertParameter(command, 0, item.Type, item.Value);
        }
    }

    [Fact]
    public void PredicatesAndOrderHaveValidSpacingAndFixedComparisonOperators()
    {
        using var builder = new Command(Kind.SELECT);
        builder.Select("owned_values").Where("A", -1L, true).And("B", 2L, false)
            .And("C", 3UL).And("D", "x'").And("E", false)
            .Or("F", -4L).Or("G", 5UL).Or("H", "y'").Or("I", true).Order("A desc");
        using MySqlCommand command = builder.CreateProviderCommand();
        Assert.Equal("SELECT * FROM `owned_values` WHERE `A` > @p0 AND `B` < @p1 AND `C` = @p2 AND `D` = @p3 AND `E` = @p4 OR `F` = @p5 OR `G` = @p6 OR `H` = @p7 OR `I` = @p8 ORDER BY `A` DESC", command.CommandText);
        Assert.Equal(new object[] { -1L, 2L, 3UL, "x'", (byte)0, -4L, 5UL, "y'", (byte)1 },
            command.Parameters.Cast<MySqlParameter>().Select(parameter => parameter.Value));
        using var count = new Command(Kind.COUNT);
        count.Count("owned_values").Where("Enabled", true);
        using MySqlCommand countCommand = count.CreateProviderCommand();
        Assert.Equal("SELECT count(*) FROM `owned_values` WHERE `Enabled` = @p0", countCommand.CommandText);
        AssertParameter(countCommand, 0, MySqlDbType.Byte, (byte)1);
    }

    [Fact]
    public void CurrentCultureDoesNotChangeNumericDataOrCommandSyntax()
    {
        var selected = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        selected.NumberFormat.NegativeSign = "m";
        selected.NumberFormat.NumberDecimalSeparator = ",";
        using var culture = new CultureScope(selected);
        using var builder = new Command(Kind.UPDATE);
        builder.Update("owned_values").Set("Signed", long.MinValue).Set("Unsigned", ulong.MaxValue)
            .Set("Ratio", 1.5).Set("Boxed", (object)(-9)).Set("Money", (object)decimal.MaxValue);
        using MySqlCommand command = builder.CreateProviderCommand();
        Assert.Equal("UPDATE `owned_values` SET `Signed` = @p0, `Unsigned` = @p1, `Ratio` = @p2, `Boxed` = @p3, `Money` = @p4", command.CommandText);
        AssertParameter(command, 0, MySqlDbType.Int64, long.MinValue);
        AssertParameter(command, 1, MySqlDbType.UInt64, ulong.MaxValue);
        AssertParameter(command, 2, MySqlDbType.Double, 1.5);
        AssertParameter(command, 3, MySqlDbType.Int32, -9);
        AssertParameter(command, 4, MySqlDbType.Decimal, decimal.MaxValue);
    }

    [Fact]
    public void ObjectSetCallsToStringOnceAndBindsItsExactResult()
    {
        var probe = new ToStringProbe("value'\\\n");
        using var builder = new Command(Kind.UPDATE);
        builder.Update("owned_values").Set("Value", (object)probe).Set("Flag", (object)true);
        using MySqlCommand command = builder.CreateProviderCommand();
        Assert.Equal(1, probe.Calls);
        AssertParameter(command, 0, MySqlDbType.VarChar, "value'\\\n");
        AssertParameter(command, 1, MySqlDbType.Byte, (byte)1);
    }

    [Fact]
    public void FloatingPointBoundariesAndEmptyTextRemainExactBoundValues()
    {
        using var builder = new Command(Kind.UPDATE);
        builder.Update("owned_values").Set("Maximum", double.MaxValue)
            .Set("Minimum", -double.MaxValue).Set("Smallest", double.Epsilon).Set("Empty", "");
        using MySqlCommand command = builder.CreateProviderCommand();
        AssertParameter(command, 0, MySqlDbType.Double, double.MaxValue);
        AssertParameter(command, 1, MySqlDbType.Double, -double.MaxValue);
        AssertParameter(command, 2, MySqlDbType.Double, double.Epsilon);
        AssertParameter(command, 3, MySqlDbType.VarChar, "");
        Assert.Equal("UPDATE `owned_values` SET `Maximum` = @p0, `Minimum` = @p1, `Smallest` = @p2, `Empty` = @p3", command.CommandText);
    }

    private static void AssertParameter(MySqlCommand command, int index, MySqlDbType type, object value)
    {
        MySqlParameter parameter = command.Parameters[index];
        Assert.Equal("@p" + index.ToString(CultureInfo.InvariantCulture), parameter.ParameterName);
        Assert.Equal(type, parameter.MySqlDbType);
        Assert.Equal(value, parameter.Value);
    }

    private sealed class ToStringProbe(string text)
    {
        public int Calls;
        public override string ToString() { Calls++; return text; }
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;
        public CultureScope(CultureInfo selected) => CultureInfo.CurrentCulture = selected;
        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }
}
