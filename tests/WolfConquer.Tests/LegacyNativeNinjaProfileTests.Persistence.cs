using System.Globalization;
using WolfConquer.Core.World;
using Ninja = WolfConquer.Core.World.LegacyNativeNinja;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaProfileTests
{
    private static readonly ulong[] ProfileValues =
        [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 15, 16, 17, 18, 19];

    private static ulong[] ProfileScalars(Ninja value) =>
    [
        value.Flag, value.Exp, value.Fire, value.Water, value.Earth, value.Wind, value.Lightning,
        value.tmp_Fire, value.tmp_Water, value.tmp_Earth, value.tmp_Wind, value.tmp_Lightning,
        value.FireMastery, value.WaterMastery, value.EarthMastery, value.WindMastery,
        value.LightningMastery, value.UnlockMastery,
    ];

    private static void SeedProfileScalars(Ninja value)
    {
        value.Flag = 1; value.Exp = 2; value.Fire = 3; value.Water = 4; value.Earth = 5;
        value.Wind = 6; value.Lightning = 7; value.tmp_Fire = 8; value.tmp_Water = 9;
        value.tmp_Earth = 10; value.tmp_Wind = 11; value.tmp_Lightning = 12;
        value.FireMastery = 14; value.WaterMastery = 15; value.EarthMastery = 16;
        value.WindMastery = 17; value.LightningMastery = 18; value.UnlockMastery = 19;
    }

    private static string ProfileLine(string count, string rows = "", string[]? prefix = null) =>
        string.Join("/", prefix ?? Enumerable.Repeat("0", 19).ToArray()) + "/" + count + "/" + rows;

    private static void AssertProfileItem(Ninja.Item value, uint id, uint level, uint position,
        uint experience, uint uk)
    {
        Assert.Equal(id, value.ItemID); Assert.Equal(level, value.Level);
        Assert.Equal(position, value.Position); Assert.Equal(experience, value.Exp); Assert.Equal(uk, value.UK);
    }

    [Fact]
    public void EmptyProfileWritesExactlyTwentyZeroTokensWithoutMetadataOrPlayerUse()
    {
        using var scope = new Scope();
        var ninja = new Ninja();
        LegacyNativeNinjaFile.gouyu_type = null!;
        Assert.Null(ninja.user);
        Assert.Equal(string.Concat(Enumerable.Repeat("0/", 20)), ninja.ToString());
        Assert.Empty(ninja.Items);
    }

    [Fact]
    public void SaveUsesExactTwentyTokenPrefixAndFiveItemFieldsWithLiveComputedScore()
    {
        using var scope = new Scope();
        SeedProfileScalars(scope.Ninja);
        var item = scope.Item(700, 100, 9, 21, 22, 23);
        var metadata = scope.Meta(11, 100, 9, 20);
        Assert.Equal("1/2/3/4/5/6/7/8/9/10/11/12/112/14/15/16/17/18/19/1/100/9/21/22/23/",
            scope.Ninja.ToString());
        metadata.Score = 33;
        Assert.Equal("1/2/3/4/5/6/7/8/9/10/11/12/125/14/15/16/17/18/19/1/100/9/21/22/23/",
            scope.Ninja.ToString());
        item.ItemID = 777;
        Assert.Equal("1/2/3/4/5/6/7/8/9/10/11/12/80/14/15/16/17/18/19/1/777/9/21/22/23/",
            scope.Ninja.ToString());
        Assert.Same(item, scope.Ninja.Items[700]);
        Assert.Equal(ProfileValues, ProfileScalars(scope.Ninja));
    }

    [Fact]
    public void LoadConsumesButIgnoresStoredScoreAndDoesNotCallAddItemLevelClampOrMetadata()
    {
        using var scope = new Scope();
        var metadata = scope.Metadata;
        LegacyNativeNinjaFile.gouyu_type = null!;
        scope.Ninja.user = null!;
        scope.Ninja.Load("1/2/3/4/5/6/7/8/9/10/11/12/4294967295/14/15/16/17/18/19/1/100/0/4294967295/4294967295/4294967295/");
        Assert.Equal(ProfileValues, ProfileScalars(scope.Ninja));
        AssertProfileItem(Assert.Single(scope.Ninja.Items).Value, 100, 0, uint.MaxValue, uint.MaxValue, uint.MaxValue);
        Assert.Null(scope.Ninja.user);
        LegacyNativeNinjaFile.gouyu_type = metadata;
        Assert.Equal(92u, scope.Ninja.Score); // Missing DBItem score0 + Fire*4 + five masteries.
        Assert.Equal("92", scope.Ninja.ToString().Split('/')[12]);
    }

    [Fact]
    public void ProfileLoadUsesCurrentCultureAndFullUlongFlagAndUintRanges()
    {
        using var scope = new Scope();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        string[] prefix = [ulong.MaxValue.ToString(CultureInfo.InvariantCulture),
            .. Enumerable.Repeat(uint.MaxValue.ToString(CultureInfo.InvariantCulture), 18)];
        prefix = prefix.Select(token => " p" + token + " ").ToArray();
        string rows = string.Concat(Enumerable.Repeat(" p4294967295 /", 5));
        scope.Ninja.Load(ProfileLine(" p1 ", rows + "ignored/", prefix));
        ulong[] expectedScalars = [ulong.MaxValue, .. Enumerable.Repeat((ulong)uint.MaxValue, 17)];
        Assert.Equal(expectedScalars, ProfileScalars(scope.Ninja));
        AssertProfileItem(Assert.Single(scope.Ninja.Items).Value,
            uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);
        string[] written = scope.Ninja.ToString().Split('/');
        Assert.Equal("18446744073709551615", written[0]);
        Assert.Equal("4294967291", written[12]); // Five uint.Max masteries, native uint sum wrap.
        Assert.Equal("1", written[19]);
        Assert.Equal("", written[^1]);
    }

    [Fact]
    public void NullEmptyAndUnseparatedLoadResetEveryScalarButNeverClearRetainedItems()
    {
        using var scope = new Scope();
        var item = scope.Item(700, 777, 99, 21, 22, 23);
        var dictionary = scope.Ninja.Items;
        foreach (string? line in new string?[] { null, "", "1" })
        {
            SeedProfileScalars(scope.Ninja);
            scope.Ninja.Load(line);
            Assert.Equal(new ulong[18], ProfileScalars(scope.Ninja));
            Assert.Same(dictionary, scope.Ninja.Items);
            Assert.Same(item, Assert.Single(dictionary).Value);
            AssertProfileItem(item, 777, 99, 21, 22, 23);
        }
    }

    [Fact]
    public void DuplicateItemRowsKeepFirstRawLevelAndDoNotUseOverwritingAddPath()
    {
        using var scope = new Scope();
        scope.Ninja.Load(ProfileLine("2", "100/0/1/2/3/100/4294967295/4/5/6/"));
        AssertProfileItem(Assert.Single(scope.Ninja.Items).Value, 100, 0, 1, 2, 3);
        Assert.Equal("100/0/1/2/3/", string.Join("/", scope.Ninja.ToString().Split('/').Skip(20)));
    }

    [Fact]
    public void RepeatedLoadReplacesScalarsButRetainsMatchingKeyObjectsAndUnmentionedRows()
    {
        using var scope = new Scope();
        var old = scope.Item(100, 777, 9, 21, 22, 23);
        var unmentioned = scope.Item(200, 200, 1, 2, 3, 4);
        var dictionary = scope.Ninja.Items;
        scope.Ninja.Load("1/2/3/4/5/6/7/8/9/10/11/12/999/14/15/16/17/18/19/2/100/0/1/2/3/300/4294967295/4/5/6/");
        Assert.Equal(ProfileValues, ProfileScalars(scope.Ninja));
        Assert.Same(dictionary, scope.Ninja.Items);
        Assert.Same(old, dictionary[100]);
        Assert.Same(unmentioned, dictionary[200]);
        AssertProfileItem(old, 777, 9, 21, 22, 23);
        AssertProfileItem(dictionary[300], 300, uint.MaxValue, 4, 5, 6);
        Assert.Equal(3, dictionary.Count);
    }

    [Fact]
    public void TruncatedItemFieldsDefaultAndZeroItemIdIsPublishedWithoutNormalization()
    {
        using var scope = new Scope();
        scope.Ninja.Load(ProfileLine("3", "100/0/1/2/3/200/4294967295/"));
        Assert.Equal(3, scope.Ninja.Items.Count);
        AssertProfileItem(scope.Ninja.Items[100], 100, 0, 1, 2, 3);
        AssertProfileItem(scope.Ninja.Items[200], 200, uint.MaxValue, 0, 0, 0);
        AssertProfileItem(scope.Ninja.Items[0], 0, 0, 0, 0, 0);
    }

    [Fact]
    public void MalformedScalarDefaultsAndIgnoredScoreStillAdvanceEveryPrefixToken()
    {
        using var scope = new Scope();
        string[] prefix = ["18446744073709551616", "-1", "4294967296", "bad", "",
            "6", "7", "8", "9", "10", "11", "12", "not-a-score", "14", "15", "16", "17", "18", "19"];
        scope.Ninja.Load(ProfileLine("0", prefix: prefix));
        Assert.Equal(new ulong[] { 0, 0, 0, 0, 0, 6, 7, 8, 9, 10, 11, 12, 14, 15, 16, 17, 18, 19 },
            ProfileScalars(scope.Ninja));
        Assert.Empty(scope.Ninja.Items);
    }

    [Fact]
    public void NullItemsFaultOccursAfterEveryHeaderScalarHasAlreadyBeenPublished()
    {
        using var scope = new Scope();
        scope.Ninja.Items = null!;
        Assert.Throws<NullReferenceException>(() => scope.Ninja.Load(
            "1/2/3/4/5/6/7/8/9/10/11/12/999/14/15/16/17/18/19/1/100/0/1/2/3/"));
        Assert.Equal(ProfileValues, ProfileScalars(scope.Ninja));
        Assert.Null(scope.Ninja.Items);
        scope.Ninja.Load(ProfileLine("-1")); // No row loop means the null dictionary is not touched.
        Assert.Equal(new ulong[18], ProfileScalars(scope.Ninja));
        Assert.Null(scope.Ninja.Items);
    }

    [Fact]
    public void SaveScoreMetadataAndNullRowFaultsDoNotNormalizeOrMutatePhysicalState()
    {
        using var scope = new Scope();
        SeedProfileScalars(scope.Ninja);
        var item = scope.Item(700, 100, 9, 21, 22, 23);
        var metadata = scope.Metadata;
        var dictionary = scope.Ninja.Items;
        LegacyNativeNinjaFile.gouyu_type = null!;
        Assert.Throws<NullReferenceException>(() => scope.Ninja.ToString());
        Assert.Same(item, dictionary[700]);
        Assert.Equal(ProfileValues, ProfileScalars(scope.Ninja));
        AssertProfileItem(item, 100, 9, 21, 22, 23);
        LegacyNativeNinjaFile.gouyu_type = metadata;
        dictionary[700] = null!;
        Assert.Throws<NullReferenceException>(() => scope.Ninja.ToString());
        Assert.Same(dictionary, scope.Ninja.Items);
        Assert.Null(dictionary[700]);
        Assert.Equal(ProfileValues, ProfileScalars(scope.Ninja));
    }

    [Fact]
    public void ActualSavedRecordReloadsFreshOwnerWithIndependentRowsAndPhysicalIdsAsKeys()
    {
        using var scope = new Scope();
        SeedProfileScalars(scope.Ninja);
        scope.Item(700, 100, 1, 9, 10, 11);
        scope.Item(800, 600, 2, 17, 18, 19);
        scope.Meta(1, 100, 1, 9); scope.Meta(2, 600, 2, 11);
        string record = scope.Ninja.ToString();
        var fresh = new Ninja();
        fresh.Load(record);
        Assert.Null(fresh.user);
        Assert.Equal(ProfileValues, ProfileScalars(fresh));
        Assert.Equal(112u, fresh.Score);
        AssertProfileItem(fresh.Items[100], 100, 1, 9, 10, 11);
        AssertProfileItem(fresh.Items[600], 600, 2, 17, 18, 19);
        Assert.False(fresh.Items.ContainsKey(700)); Assert.False(fresh.Items.ContainsKey(800));
        Assert.NotSame(scope.Ninja.Items[700], fresh.Items[100]);
        Assert.NotSame(scope.Ninja.Items[800], fresh.Items[600]);
        fresh.Items[100].Level = 99;
        Assert.Equal(1u, scope.Ninja.Items[700].Level);
        Assert.Equal(112u, scope.Ninja.Score);
    }

    [Fact]
    public void PhysicalItemIdAliasesSerializeTwiceThenFreshLoadKeepsFirstEnumeratedRow()
    {
        using var scope = new Scope();
        scope.Item(700, 777, 0, 1, 2, 3);
        scope.Item(800, 777, uint.MaxValue, 4, 5, 6);
        Ninja.Item[] values = scope.Ninja.Items.Values.ToArray();
        string record = scope.Ninja.ToString();
        string expected = ProfileLine("2", string.Concat(values.Select(row =>
            $"777/{row.Level}/{row.Position}/{row.Exp}/{row.UK}/")));
        Assert.Equal(expected, record);
        var fresh = new Ninja();
        fresh.Load(record);
        Ninja.Item first = Assert.Single(fresh.Items).Value;
        AssertProfileItem(first, 777, values[0].Level, values[0].Position, values[0].Exp, values[0].UK);
        Assert.NotSame(values[0], first);
        Assert.False(fresh.Items.ContainsKey(700)); Assert.False(fresh.Items.ContainsKey(800));
        Assert.Equal(2, scope.Ninja.Items.Count);
    }
}
