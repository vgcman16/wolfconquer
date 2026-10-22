using System.Reflection;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.World.LegacyNativeAtributesStatus;

namespace WolfConquer.Tests;

// Actual in-memory native table and physical stat storage; no Load/config/host or setter proof.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeAtributesStatusTests
{
    [Fact]
    public void InstantAndPlayerStatsKeepExactPublicUInt16FieldsDefaultsAndNativeTypo()
    {
        Assert.Equal(typeof(Dictionary<uint, Dictionary<ushort, Table.Instant>>), typeof(Table).BaseType);
        var instant = new Table.Instant();
        var fields = typeof(Table.Instant).GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Strenght", "Vitality", "Agility", "Spirit" }, fields.Select(field => field.Name));
        foreach (var field in fields)
        {
            Assert.Equal(typeof(ushort), field.FieldType);
            Assert.False(field.IsInitOnly);
            Assert.Equal((ushort)0, field.GetValue(instant));
            field.SetValue(instant, ushort.MaxValue);
            Assert.Equal(ushort.MaxValue, field.GetValue(instant));
        }
        Assert.Null(typeof(Table.Instant).GetField("Strength"));
        var player = Player();
        foreach (string name in new[] { "Agility", "Vitality", "Spirit", "Strength" })
        {
            var field = typeof(LegacyNativePlayerRuntime).GetField(name, BindingFlags.Public | BindingFlags.Instance)!;
            Assert.Equal(typeof(ushort), field.FieldType);
            Assert.False(field.IsInitOnly);
            Assert.Null(typeof(LegacyNativePlayerRuntime).GetProperty(name));
            Assert.Equal((ushort)0, field.GetValue(player));
            field.SetValue(player, ushort.MaxValue);
            Assert.Equal(ushort.MaxValue, field.GetValue(player));
        }
    }

    [Theory]
    [InlineData(1000u, 1051u, 1005u)]
    [InlineData(2000u, 2051u, 2005u)]
    [InlineData(4000u, 4051u, 4005u)]
    [InlineData(5000u, 5051u, 5005u)]
    [InlineData(6000u, 6051u, 6005u)]
    [InlineData(7000u, 7051u, 7005u)]
    [InlineData(8000u, 8051u, 8005u)]
    [InlineData(9000u, 9051u, 9005u)]
    [InlineData(10000u, 14051u, 10000u)]
    [InlineData(16000u, 16051u, 16005u)]
    public void EveryMappedFamilyKeepsInclusiveBoundsAndRoutesRealResetAndCheck(uint first, uint last, uint key)
    {
        var selected = Row(20);
        var fallback = Row(40);
        var table = new Table { [key] = new() { [7] = selected }, [0] = new() { [7] = fallback } };
        foreach (uint job in new[] { first - 1, first, first + 1, (first + last) / 2, last - 1, last, last + 1 })
        {
            bool member = job >= first && job <= last;
            Assert.Equal(member ? key : 0u, (uint)Position.Invoke(table, [job])!);
            var player = Player(job);
            table.ResetStatsNonReborn(player);
            AssertStats(member ? selected : fallback, player);
            Assert.True(table.CheckStatus(player));
            Assert.Equal(job, player.Class);
            Assert.Equal((ushort)7, player.Level);
        }
    }

    [Fact]
    public void AllUnmappedGapsUseKeyZeroWhileTheWholeTaoistSpanShares10000()
    {
        var fallback = Row(40);
        var taoist = Row(20);
        var table = new Table { [0] = new() { [7] = fallback }, [10000] = new() { [7] = taoist } };
        foreach (uint job in new uint[] { 0, 999, 1052, 1999, 2052, 3000, 3999, 4052, 4999, 5052,
            5999, 6052, 6999, 7052, 7999, 8052, 8999, 9052, 9999, 14052, 15000, 15999, 16052, uint.MaxValue })
        {
            Assert.Equal(0u, (uint)Position.Invoke(table, [job])!);
            var player = Player(job);
            table.ResetStatsNonReborn(player);
            AssertStats(fallback, player);
        }
        foreach (uint job in new uint[] { 10000, 10051, 11000, 12000, 13000, 13001, 13002, 13051, 13500, 14000, 14001, 14002, 14051 })
        {
            Assert.Equal(10000u, (uint)Position.Invoke(table, [job])!);
            var player = Player(job);
            table.ResetStatsNonReborn(player);
            AssertStats(taoist, player);
        }
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)119)]
    [InlineData((ushort)120)]
    [InlineData((ushort)121)]
    [InlineData((ushort)256)]
    [InlineData(ushort.MaxValue)]
    public void ResetAndCheckCapLevelAt120AndResetIgnoresRebornDespiteItsName(ushort level)
    {
        ushort capped = (ushort)Math.Min(120, (int)level);
        var selected = Row(10);
        var rows = new Dictionary<ushort, Table.Instant> { [capped] = selected };
        if (level > 120) rows[level] = Row(50);
        var table = new Table { [1005] = rows };
        var player = Player(level: level, reborn: byte.MaxValue);
        Sentinel(player);
        table.ResetStatsNonReborn(player);
        AssertStats(selected, player);
        Assert.True(table.CheckStatus(player));
        Assert.Equal(level, player.Level);
        Assert.Equal(byte.MaxValue, player.Reborn);
        Assert.Same(selected, rows[capped]);
    }

    [Theory]
    [InlineData("Strength")]
    [InlineData("Agility")]
    [InlineData("Spirit")]
    [InlineData("Vitality")]
    public void CheckStatusRequiresEveryStatAtLeastTheRowThresholdAndNeverMutates(string field)
    {
        var row = Row();
        var table = new Table { [1005] = new() { [7] = row } };
        var player = Player();
        table.ResetStatsNonReborn(player);
        Assert.True(table.CheckStatus(player));
        var member = typeof(LegacyNativePlayerRuntime).GetField(field)!;
        member.SetValue(player, (ushort)((ushort)member.GetValue(player)! - 1));
        var before = Stats(player);
        Assert.False(table.CheckStatus(player));
        Assert.Equal(before, Stats(player));
        Sentinel(player);
        before = Stats(player);
        Assert.True(table.CheckStatus(player));
        Assert.Equal(before, Stats(player));
        Assert.Same(row, table[1005][7]);
    }

    [Theory]
    [InlineData((ushort)0, (byte)0, false, true)]
    [InlineData((ushort)120, (byte)0, false, true)]
    [InlineData((ushort)121, (byte)0, false, false)]
    [InlineData((ushort)7, (byte)1, false, false)]
    [InlineData(ushort.MaxValue, byte.MaxValue, false, false)]
    [InlineData((ushort)121, (byte)1, true, true)]
    [InlineData((ushort)256, (byte)2, true, true)]
    [InlineData(ushort.MaxValue, byte.MaxValue, true, true)]
    public void GetStatusHonorsDefaultRebornGateButAtrebornReadsUncappedPhysicalLevel(
        ushort level, byte reborn, bool atreborn, bool applies)
    {
        var table = new Table();
        var row = Row(30);
        if (applies)
        {
            table[1005] = new() { [level] = row };
            if (level > 120) table[1005][120] = Row(10);
        }
        var player = Player(level: level, reborn: reborn);
        Sentinel(player);
        var before = Stats(player);
        if (atreborn) table.GetStatus(player, true);
        else table.GetStatus(player); // Actual omitted/default argument path.
        if (applies) AssertStats(row, player);
        else Assert.Equal(before, Stats(player)); // Empty table proves the gated-off branch does not look up.
        Assert.Equal(level, player.Level);
        Assert.Equal(reborn, player.Reborn);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)119)]
    [InlineData((byte)120)]
    [InlineData((byte)121)]
    [InlineData(byte.MaxValue)]
    public void InfoStrByteClassAlwaysSelectsKeyZeroAndKeepsCapTypoAndExactPunctuation(byte level)
    {
        ushort capped = (ushort)Math.Min(120, (int)level);
        var row = Row();
        var table = new Table { [0] = new() { [capped] = row }, [1005] = new() { [capped] = Row(99) } };
        for (int job = 0; job <= byte.MaxValue; job++)
            Assert.Equal("You need to have Strenght : 10, Agility : 12, Vitality: 11, Spirit: 13", table.InfoStr((byte)job, level));
        Assert.Same(row, table[0][capped]);
    }

    [Fact]
    public void ResetAndGetReadCurrentAliasedRowsAndInnerDictionaryReplacementsWithoutCaching()
    {
        var first = Row(10);
        var original = new Dictionary<ushort, Table.Instant> { [7] = first };
        var table = new Table { [1005] = original };
        var player = Player();
        table.ResetStatsNonReborn(player);
        AssertStats(first, player);
        first.Strenght = 88;
        table.GetStatus(player);
        AssertStats(first, player);
        var second = Row(20);
        original[7] = second;
        table.ResetStatsNonReborn(player);
        AssertStats(second, player);
        var third = Row(30);
        table[1005] = new() { [7] = third };
        table.GetStatus(player);
        AssertStats(third, player);
        Assert.Same(second, original[7]);
        Assert.Same(third, table[1005][7]);
        player.Strength = 99;
        Assert.Equal((ushort)30, third.Strenght); // Native assignments copy scalar fields, not a live DTO row binding.
    }

    [Theory]
    [InlineData("outer")]
    [InlineData("inner")]
    [InlineData("level")]
    [InlineData("row")]
    public void MissingOrNullLookupFaultsPreserveAllPhysicalStatsAndDoNotRepairTheTable(string missing)
    {
        var table = new Table();
        if (missing != "outer")
        {
            Dictionary<ushort, Table.Instant> rows = missing == "inner" ? null! : new();
            if (missing == "row") rows[7] = null!;
            table[1005] = rows;
            table[0] = rows;
        }
        var player = Player();
        Sentinel(player);
        var before = Stats(player);
        int count = table.Count;
        Type error = missing is "outer" or "level" ? typeof(KeyNotFoundException) : typeof(NullReferenceException);
        Assert.Throws(error, () => table.ResetStatsNonReborn(player));
        Assert.Equal(before, Stats(player));
        Assert.Throws(error, () => table.CheckStatus(player));
        Assert.Equal(before, Stats(player));
        Assert.Throws(error, () => table.GetStatus(player, true));
        Assert.Equal(before, Stats(player));
        Assert.Throws(error, () => table.InfoStr(0, 7));
        Assert.Equal(count, table.Count);
        if (missing == "inner") Assert.Null(table[1005]);
        if (missing == "row") Assert.Null(table[1005][7]);
    }

    [Fact]
    public void NullPlayerAndNullTableReceiverFaultWithoutChangingAnExistingPhysicalPlayer()
    {
        var table = new Table();
        Assert.Throws<NullReferenceException>(() => table.ResetStatsNonReborn(null!));
        Assert.Throws<NullReferenceException>(() => table.CheckStatus(null!));
        Assert.Throws<NullReferenceException>(() => table.GetStatus(null!));
        Assert.Throws<NullReferenceException>(() => table.GetStatus(null!, true));
        var player = Player();
        Sentinel(player);
        var before = Stats(player);
        Table missing = null!;
        Assert.Throws<NullReferenceException>(() => missing.ResetStatsNonReborn(player));
        Assert.Throws<NullReferenceException>(() => missing.CheckStatus(player));
        Assert.Throws<NullReferenceException>(() => missing.GetStatus(player, true));
        Assert.Throws<NullReferenceException>(() => missing.InfoStr(0, 7));
        Assert.Equal(before, Stats(player));
    }
}
