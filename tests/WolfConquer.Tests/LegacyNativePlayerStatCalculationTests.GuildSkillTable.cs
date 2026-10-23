using Table = WolfConquer.Core.Data.LegacyNativeGuildSkillTypeTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerStatCalculationTests
{
    [Fact]
    public void GuildSkillRowEnumAndColdStateRetainNativeWidthsAndFourteenNames()
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Table.Type)));
        Assert.Equal(Enumerable.Range(1, 14).Select(value => (uint)value), Enum.GetValues<Table.Type>().Select(value => (uint)value));
        Assert.Equal("MaxHP", Enum.GetName((Table.Type)1)); Assert.Equal("AntiBreak", Enum.GetName((Table.Type)14));
        var row = new Table.SkillType();
        Assert.Equal(0u, row.ID); Assert.Equal(0u, row.Type); Assert.Equal(0u, row.Level); Assert.Equal(0u, row.data1);
        Assert.Equal(0u, row.data2); Assert.Equal(0u, row.Silver); Assert.Equal(0u, row.DonationPoints);
    }

    [Fact]
    public void TableLookupUsesActualTypeKeyAndExactLevelAndReturnsTheSameMutableRow()
    {
        using var scope = new Scope(); var row = new Table.SkillType { ID = 900, Type = 13, Level = 7, data1 = 21 };
        Table._syn_skill_type[13] = new() { [7] = row };
        Assert.True(Table.TryGetValue(13, 7, out var found)); Assert.Same(row, found);
        found!.data1 = 33; Assert.Equal(33u, row.data1);
        Assert.False(Table.TryGetValue(900, 7, out found)); Assert.Null(found);
        Assert.False(Table.TryGetValue(13, 6, out found)); Assert.Null(found);
        Table._syn_skill_type[13][0] = row;
        Assert.False(Table.TryGetValue(13, 99, out found)); Assert.Null(found); // No fallback to0/max.
    }

    [Fact]
    public void NullCacheAndNullNestedDictionaryKeepNativeFaults()
    {
        using var scope = new Scope(); Table._syn_skill_type = null!;
        Assert.Throws<NullReferenceException>(() => Table.TryGetValue(1, 0, out _));
        Table._syn_skill_type = new() { [1] = null! };
        Assert.Throws<NullReferenceException>(() => Table.TryGetValue(1, 0, out _));
    }

    [Fact]
    public void LoaderReadsActualFileSeparatorsAllColumnsAndFirstDuplicateWins()
    {
        using var scope = new Scope();
        File.WriteAllText(scope.FilePath,
            "900@@13@@7@@21@@22@@23@@24\n901 13 8 31 32 33 34\n902@@13@@7@@99@@99@@99@@99\n903@@14@@0@@41@@42@@43@@44\n");
        Table.Load(); Assert.Equal(2, Table._syn_skill_type.Count);
        Assert.True(Table.TryGetValue(13, 7, out var row));
        Assert.Equal(900u, row!.ID); Assert.Equal(13u, row.Type); Assert.Equal(7u, row.Level);
        Assert.Equal(21u, row.data1); Assert.Equal(22u, row.data2); Assert.Equal(23u, row.Silver); Assert.Equal(24u, row.DonationPoints);
        Assert.True(Table.TryGetValue(13, 8, out row)); Assert.Equal(901u, row!.ID);
        Assert.True(Table.TryGetValue(14, 0, out row)); Assert.Equal(903u, row!.ID);
        Assert.False(Table.TryGetValue(900, 7, out _)); Assert.Equal("", scope.Output.ToString());
    }

    [Fact]
    public void MissingFileRetainsCacheButExistingEmptyFileReplacesIt()
    {
        using var scope = new Scope(); var cache = Table._syn_skill_type;
        cache[1] = new(); Table.Load(); Assert.Same(cache, Table._syn_skill_type);
        Assert.Contains("!File.Exists: " + scope.FilePath, scope.Output.ToString());
        File.WriteAllText(scope.FilePath, ""); Table.Load();
        Assert.NotSame(cache, Table._syn_skill_type); Assert.Empty(Table._syn_skill_type);
    }

    [Theory]
    [InlineData("invalid@@13@@8@@1@@2@@3@@4", "FormatException")]
    [InlineData("901@@13@@8", "IndexOutOfRangeException")]
    [InlineData("-1@@13@@8@@1@@2@@3@@4", "OverflowException")]
    [InlineData("901\t13\t8\t1\t2\t3\t4", "FormatException")]
    [InlineData("", "IndexOutOfRangeException")]
    public void MalformedLaterRowIsCaughtWithEarlierPublicationRetainedAndRemainingRowsUnprocessed(string bad, string error)
    {
        using var scope = new Scope(); var previous = Table._syn_skill_type; previous[99] = new();
        File.WriteAllText(scope.FilePath, "900@@13@@7@@21@@22@@23@@24\n" + bad + "\n902@@14@@8@@1@@2@@3@@4\n");
        Table.Load(); Assert.NotSame(previous, Table._syn_skill_type);
        Assert.True(Table.TryGetValue(13, 7, out _)); Assert.False(Table.TryGetValue(14, 8, out _));
        Assert.False(Table._syn_skill_type.ContainsKey(99)); Assert.Contains(error, scope.Output.ToString());
    }
}
