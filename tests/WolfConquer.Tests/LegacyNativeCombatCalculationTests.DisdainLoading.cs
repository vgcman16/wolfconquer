using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Disdain = WolfConquer.Core.Data.LegacyNativeDisdain;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    private static string Row(int delta, int first = 1)
    {
        var values = Enumerable.Range(first, 20).Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        values[1] = delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return "ignored," + string.Join(",", values);
    }

    [Fact]
    public Task LoaderPublishesAllTwentyPhysicalFieldsAndDoesNotClearExistingRows() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Directory(); var prior = new Disdain.DisdainItem(); Disdain.DisdainRecords[7] = prior;
        File.WriteAllText(Path.Combine(s.Temp!, "cq_disdain.txt"), Row(-12));
        Disdain.Load(); Assert.Equal(2, Disdain.DisdainRecords.Count); Assert.Same(prior, Disdain.DisdainRecords[7]);
        var fields = new[] { "type", "delta_lev", "usr_atk_mst", "mst_atk", "max_atk", "max_xp_atk", "exp_factor",
            "xp_exp_factor", "usr_atk_usr_min", "usr_atk_usr_max", "usr_atk_usr_overadj", "usr_atk_usrx_min",
            "usr_atk_usrx_max", "usr_atk_usrx_overadj", "usrx_atk_usr_min", "usrx_atk_usr_max", "usrx_atk_usr_overadj",
            "usrx_atk_usrx_min", "usrx_atk_usrx_max", "usrx_atk_usrx_overadj" };
        for (int i = 0; i < fields.Length; i++)
            Assert.Equal(i == 1 ? -12 : i + 1, (int)typeof(Disdain.DisdainItem).GetField(fields[i])!.GetValue(Disdain.DisdainRecords[-12])!);
    });

    [Theory]
    [InlineData("short")] [InlineData("format")] [InlineData("duplicate")]
    public Task LoaderFailureRetainsPublishedPrefixAndDoesNotPublishFaultingRow(string kind) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Directory();
        string bad = kind switch { "short" => "zero,1,2", "format" => Row(5).Replace(",20", ",bad"), _ => Row(4, 40) };
        File.WriteAllLines(Path.Combine(s.Temp!, "cq_disdain.txt"), new[] { Row(4), bad, Row(6) });
        if (kind == "short") Assert.Throws<IndexOutOfRangeException>(() => Disdain.Load());
        else if (kind == "format") Assert.Throws<FormatException>(() => Disdain.Load());
        else Assert.Throws<ArgumentException>(() => Disdain.Load());
        Assert.Single(Disdain.DisdainRecords); Assert.Equal(1, Disdain.DisdainRecords[4].type);
        Assert.False(Disdain.DisdainRecords.ContainsKey(5)); Assert.False(Disdain.DisdainRecords.ContainsKey(6));
    });

    [Fact]
    public Task MissingFileIsNotTreatedAsEmptySuccessfulLoad() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Directory(); Assert.Throws<FileNotFoundException>(() => Disdain.Load());
        Assert.Empty(Disdain.DisdainRecords);
    });

    [Theory]
    [InlineData(400, 0, 400)] [InlineData(401, 0, 400)] [InlineData(-400, 0, -400)]
    [InlineData(-401, 0, -400)] [InlineData(20, 10, 10)] [InlineData(int.MaxValue, -1, -400)]
    [InlineData(int.MinValue, 1, 400)]
    public Task LookupClampsOnlyAfterNativeSignedSubtraction(int attacker, int target, int key) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var row = new Disdain.DisdainItem(); Disdain.DisdainRecords[key] = row;
        Assert.Same(row, Disdain.LookupDisdain(attacker, target)); Disdain.DisdainRecords.Clear();
        Assert.Null(Disdain.LookupDisdain(attacker, target));
    });
}
