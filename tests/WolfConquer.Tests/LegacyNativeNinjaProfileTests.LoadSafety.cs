using Ninja = WolfConquer.Core.World.LegacyNativeNinja;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaProfileTests
{
    private static void AssertRejectedProfile(Scope scope, string line)
    {
        var ninja = scope.Ninja;
        var scalars = ProfileScalars(ninja);
        var dictionary = ninja.Items;
        var rows = dictionary.ToArray();
        var values = rows.ToDictionary(pair => pair.Key, pair => new[]
            { pair.Value.ItemID, pair.Value.Level, pair.Value.Position, pair.Value.Exp, pair.Value.UK });
        var user = ninja.user;
        Assert.Throws<InvalidDataException>(() => ninja.Load(line));
        Assert.Equal(scalars, ProfileScalars(ninja));
        Assert.Same(dictionary, ninja.Items);
        Assert.Equal(rows.Length, ninja.Items.Count);
        foreach (var row in rows)
        {
            Assert.Same(row.Value, ninja.Items[row.Key]);
            AssertProfileItem(row.Value, values[row.Key][0], values[row.Key][1], values[row.Key][2], values[row.Key][3], values[row.Key][4]);
        }
        Assert.Same(user, ninja.user);
        Assert.Same(ninja, scope.Client.MyNinja);
        Assert.Null(scope.Client.Socket);
    }

    [Theory]
    [InlineData("unseparated")]
    [InlineData("missing-slash")]
    [InlineData("extra-tail")]
    [InlineData("missing-row")]
    [InlineData("extra-row")]
    [InlineData("negative-count")]
    [InlineData("count-overflow")]
    [InlineData("too-many")]
    [InlineData("flag-overflow")]
    [InlineData("scalar-overflow")]
    [InlineData("score-overflow")]
    [InlineData("score-invalid")]
    [InlineData("zero-id")]
    [InlineData("late-overflow")]
    [InlineData("leading-zero")]
    [InlineData("signed")]
    [InlineData("whitespace")]
    [InlineData("oversized")]
    public void OrdinaryLoadRejectsIncompleteAmbiguousAndUnboundedInputWithoutChangingPriorGraph(string fault)
    {
        using var scope = new Scope();
        SeedProfileScalars(scope.Ninja);
        scope.Item(700, 100, 9, 21, 22, 23);
        scope.Item(800, 600, 2, 17, 18, 19);
        AssertRejectedProfile(scope, RejectedProfileLine(fault));
    }

    private static string RejectedProfileLine(string fault)
    {
        const string row = "100/1/2/3/4/";
        var prefix = Enumerable.Repeat("0", 19).ToArray();
        switch (fault)
        {
            case "unseparated": return "1";
            case "missing-slash": return ProfileLine("1", row)[..^1];
            case "extra-tail": return ProfileLine("1", row) + "999/";
            case "missing-row": return ProfileLine("2", row);
            case "extra-row": return ProfileLine("1", row + "200/2/3/4/5/");
            case "negative-count": return ProfileLine("-1");
            case "count-overflow": return ProfileLine("4294967296");
            case "too-many": return ProfileLine("4097", string.Concat(Enumerable.Range(1, 4097).Select(id => $"{id}/1/2/3/4/")));
            case "flag-overflow": prefix[0] = "18446744073709551616"; break;
            case "scalar-overflow": prefix[18] = "4294967296"; break;
            case "score-overflow": prefix[12] = "4294967296"; break;
            case "score-invalid": prefix[12] = "ignored-but-invalid"; break;
            case "zero-id": return ProfileLine("1", "0/1/2/3/4/");
            case "late-overflow": return ProfileLine("2", row + "200/2/3/4/4294967296/");
            case "leading-zero": prefix[0] = "00"; break;
            case "signed": prefix[1] = "+1"; break;
            case "whitespace": prefix[1] = " 1"; break;
            case "oversized": return new string('1', 256 * 1024 + 1);
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        return ProfileLine("1", row, prefix);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitMissingFieldSentinelAndCompleteEmptyRecordReplaceWholeStateWithoutMutatingOldRows(bool canonical)
    {
        using var scope = new Scope();
        SeedProfileScalars(scope.Ninja);
        var row = scope.Item(700, 777, 9, 21, 22, 23);
        var old = scope.Ninja.Items;
        scope.Ninja.Load(canonical ? ProfileLine("0") : "0");
        Assert.Equal(new ulong[18], ProfileScalars(scope.Ninja));
        Assert.Empty(scope.Ninja.Items);
        Assert.NotSame(old, scope.Ninja.Items);
        Assert.Same(row, Assert.Single(old).Value);
        AssertProfileItem(row, 777, 9, 21, 22, 23);
        Assert.Null(scope.Client.Socket);
    }

    [Fact]
    public void CompleteMaximumBoundedRecordIsAdmittedWithoutMetadataOrGameplayOwners()
    {
        using var scope = new Scope();
        var ninja = new Ninja();
        var rows = string.Concat(Enumerable.Range(1, 4096).Select(id => $"{id}/0/4294967295/4294967295/4294967295/"));
        ninja.Load(ProfileLine("4096", rows));
        Assert.Null(ninja.user);
        Assert.Equal(4096, ninja.Items.Count);
        for (uint id = 1; id <= 4096; id++) AssertProfileItem(ninja.Items[id], id, 0, uint.MaxValue, uint.MaxValue, uint.MaxValue);
        Assert.Equal(new ulong[18], ProfileScalars(ninja));
    }
}
