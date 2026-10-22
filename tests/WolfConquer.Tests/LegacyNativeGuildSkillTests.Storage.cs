using System.Collections.Concurrent;
using System.Globalization;
using GuildSkill = WolfConquer.Core.World.LegacyNativeGuildSkillRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeGuildSkillTests
{
    [Fact]
    public void ConstructorOwnsFreshActualDictionaryAndSkillDefaultsArePhysicalZeros()
    {
        using var scope = new Scope();
        var other = new GuildSkill();
        Assert.IsType<ConcurrentDictionary<uint, GuildSkill.Skill>>(scope.Storage.Skills);
        Assert.NotSame(scope.Storage.Skills, other.Skills);
        Assert.Empty(scope.Storage.Skills);
        RowIs(new GuildSkill.Skill(), 0, 0);
        Assert.Equal("0/", scope.Storage.ToString());
    }

    [Fact]
    public void NullEmptyUnseparatedMalformedAndNonPositiveCountsPreserveExistingOwners()
    {
        using var scope = new Scope();
        var row = scope.Row(17, 17, 9);
        var dictionary = scope.Storage.Skills;
        foreach (string? line in new string?[] { null, "", "1", "/", "bad/", "-1/", "2147483648/", "0/ignored/" })
        {
            scope.Storage.Load(line);
            Assert.Same(dictionary, scope.Storage.Skills);
            Assert.Same(row, Assert.Single(dictionary).Value);
            RowIs(row, 17, 9);
            Assert.Equal("1/17/9/", scope.Storage.ToString());
        }
    }

    [Fact]
    public void LoadUsesCurrentCultureAndFullUnsignedRangesWithoutClampingLevel()
    {
        using var scope = new Scope();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        scope.Storage.Load(" p1 / p4294967295 / p4294967295 /ignored/trailing/");
        RowIs(Assert.Single(scope.Storage.Skills).Value, uint.MaxValue, uint.MaxValue);
        Assert.Equal("1/4294967295/4294967295/", scope.Storage.ToString());
    }

    [Fact]
    public void MalformedAndTruncatedFieldsDefaultIndividuallyAndStillPublishRows()
    {
        using var scope = new Scope();
        scope.Storage.Load("2/bad/-1/17/");
        Assert.Equal(2, scope.Storage.Skills.Count);
        RowIs(scope.Storage.Skills[0], 0, 0);
        RowIs(scope.Storage.Skills[17], 17, 0);
        scope.Storage.Load("1/19/4294967296/");
        RowIs(scope.Storage.Skills[19], 19, 0);
        Assert.Equal(3, scope.Storage.Skills.Count);
    }

    [Fact]
    public void DuplicateRowsOverwriteThroughRecoveredIdictionaryExtensionRatherThanThrowOrFirstWin()
    {
        using var scope = new Scope();
        var escaped = scope.Row(17, 777, 99);
        scope.Storage.Load("2/17/1/17/9/");
        var final = Assert.Single(scope.Storage.Skills).Value;
        Assert.NotSame(escaped, final);
        RowIs(final, 17, 9);
        RowIs(escaped, 777, 99);
        Assert.Equal("1/17/9/", scope.Storage.ToString());
    }

    [Fact]
    public void RepeatedLoadRetainsDictionaryAndUnmentionedRowsButReplacesMatchingPhysicalObjects()
    {
        using var scope = new Scope();
        var old = scope.Row(17, 17, 1);
        var unmentioned = scope.Row(19, 19, 2);
        var dictionary = scope.Storage.Skills;
        scope.Storage.Load("2/17/9/23/3/");
        Assert.Same(dictionary, scope.Storage.Skills);
        Assert.NotSame(old, dictionary[17]);
        RowIs(old, 17, 1);
        RowIs(dictionary[17], 17, 9);
        Assert.Same(unmentioned, dictionary[19]);
        RowIs(dictionary[23], 23, 3);
        Assert.Equal(3, dictionary.Count);
    }

    [Fact]
    public void SaveEmitsPhysicalIdsAndFreshLoadRekeysCollisionsWithLastEnumeratedRowWinning()
    {
        using var scope = new Scope();
        scope.Row(100, 17, 3);
        scope.Row(200, 17, 9);
        var rows = scope.Storage.Skills.Values.ToArray();
        string expected = "2/" + string.Concat(rows.Select(row => $"17/{row.Level}/"));
        Assert.Equal(expected, scope.Storage.ToString());
        var fresh = new GuildSkill();
        fresh.Load(expected);
        RowIs(Assert.Single(fresh.Skills).Value, 17, rows[1].Level);
        Assert.False(fresh.Skills.ContainsKey(100));
        Assert.False(fresh.Skills.ContainsKey(200));
        Assert.NotSame(rows[1], fresh.Skills[17]);
        Assert.Equal(2, scope.Storage.Skills.Count);
    }

    [Fact]
    public void ActualRecordRoundtripProducesIndependentRowsAndPreservesEveryStoredValue()
    {
        using var scope = new Scope();
        scope.Row(17, 17, 0);
        scope.Row(19, 19, uint.MaxValue);
        string record = scope.Storage.ToString();
        var fresh = new GuildSkill();
        fresh.Load(record);
        Assert.Equal(2, fresh.Skills.Count);
        RowIs(fresh.Skills[17], 17, 0);
        RowIs(fresh.Skills[19], 19, uint.MaxValue);
        Assert.NotSame(scope.Storage.Skills[17], fresh.Skills[17]);
        Assert.NotSame(scope.Storage.Skills[19], fresh.Skills[19]);
        fresh.Skills[19].Level = 3;
        Assert.Equal(uint.MaxValue, scope.Storage.Skills[19].Level);
    }

    [Fact]
    public void MethodsUseCurrentMutableDictionaryOwnerWithoutAHiddenSnapshot()
    {
        using var scope = new Scope();
        var prior = scope.Storage.Skills;
        var escaped = scope.Row(17, 17, 9);
        var replacement = new ConcurrentDictionary<uint, GuildSkill.Skill>();
        scope.Storage.Skills = replacement;
        Assert.Equal("0/", scope.Storage.ToString());
        scope.Storage.Load("1/19/3/");
        Assert.Same(replacement, scope.Storage.Skills);
        RowIs(Assert.Single(replacement).Value, 19, 3);
        Assert.Same(escaped, Assert.Single(prior).Value);
        Assert.False(replacement.ContainsKey(17));
    }

    [Fact]
    public void NullDictionaryFaultsOnlyWhenAReadWriteLoopActuallyTouchesItAndIsNotRepaired()
    {
        using var scope = new Scope();
        scope.Storage.Skills = null!;
        scope.Storage.Load(null);
        scope.Storage.Load("0/");
        Assert.Throws<NullReferenceException>(() => scope.Storage.Load("1/17/9/"));
        Assert.Throws<NullReferenceException>(() => scope.Storage.ToString());
        Assert.Null(scope.Storage.Skills);
    }

    [Fact]
    public void NullStoredRowMakesSaveFaultButRealLoadCanOverwriteThatKeyWithNewSkill()
    {
        using var scope = new Scope();
        scope.Storage.Skills[17] = null!;
        Assert.Throws<NullReferenceException>(() => scope.Storage.ToString());
        Assert.Null(scope.Storage.Skills[17]);
        scope.Storage.Load("1/17/9/");
        RowIs(Assert.Single(scope.Storage.Skills).Value, 17, 9);
        Assert.Equal("1/17/9/", scope.Storage.ToString());
    }
}
