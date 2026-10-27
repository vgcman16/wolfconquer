using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class PrestigeRankingSnapshotTests
{
    public static IEnumerable<object[]> CorruptSnapshotRows() =>
        PrestigeRankingQueryTestData.Corruptions.Select(kind => new object[] { kind });

    [Theory]
    [MemberData(nameof(CorruptSnapshotRows))]
    public void CorruptIneligibleTailPreventsWholeSnapshotPublication(string corruption)
    {
        PrestigeRankEntry[] rows =
        [PrestigeRankingQueryTestData.Row(), PrestigeRankingQueryTestData.Corruption(corruption)!];
        Assert.False(PrestigeRankingSnapshot.TryCreate(rows, out var snapshot, out var rejection));
        Assert.Null(snapshot);
        Assert.False(string.IsNullOrWhiteSpace(rejection));
        Assert.Throws<FormatException>(() => PrestigeRankingSnapshot.Create(rows));
    }

    public static IEnumerable<object[]> NumericSnapshotFields()
    {
        // Every numeric column after UID is checked, including all 43 components.
        foreach (var index in Enumerable.Range(2, 63).Where(index => index != 20))
        {
            yield return new object[] { index, "-1" };
            yield return new object[] { index, "4294967296" };
            yield return new object[] { index, "NaN" };
        }
    }

    [Theory]
    [MemberData(nameof(NumericSnapshotFields))]
    public void EveryNumericColumnRejectsMalformedSignedOrOverflowInput(int index, string invalid)
    {
        var tail = PrestigeRankingQueryTestData.ChangeField(PrestigeRankingQueryTestData.Row(99, 1), index, invalid);
        Assert.False(PrestigeRankingSnapshot.TryCreate([PrestigeRankingQueryTestData.Row(), tail],
            out var snapshot, out var rejection));
        Assert.Null(snapshot);
        Assert.False(string.IsNullOrWhiteSpace(rejection));
    }

    [Fact]
    public void DuplicatePhysicalUidCannotBeHiddenInAnotherClassOrBelowThreshold()
    {
        var first = PrestigeRankingQueryTestData.Row();
        var duplicate = PrestigeRankingQueryTestData.Row(41, total: 1, category: 3, name: "Other");
        Assert.False(PrestigeRankingSnapshot.TryCreate([first, duplicate], out var snapshot, out var rejection));
        Assert.Null(snapshot);
        Assert.False(string.IsNullOrWhiteSpace(rejection));
        Assert.Throws<FormatException>(() => PrestigeRankingSnapshot.Create([first, duplicate]));
    }

    [Fact]
    public void CorruptionBeyondBothRetainedLimitsCannotBeIgnored()
    {
        var rows = Enumerable.Range(0, 110)
            .Select(index => PrestigeRankingQueryTestData.Row(1000 + index, (uint)(20_000 + index)))
            .Append(PrestigeRankingQueryTestData.Corruption("tail-overflow")!).ToArray();
        Assert.False(PrestigeRankingSnapshot.TryCreate(rows, out var snapshot, out var rejection));
        Assert.Null(snapshot);
        Assert.False(string.IsNullOrWhiteSpace(rejection));
    }

    [Fact]
    public void MissingRowsAndInvalidQueryCategoryCannotMasqueradeAsValidAbsence()
    {
        Assert.False(PrestigeRankingSnapshot.TryCreate(null, out var snapshot, out var rejection));
        Assert.Null(snapshot);
        Assert.False(string.IsNullOrWhiteSpace(rejection));
        Assert.Throws<ArgumentNullException>(() => PrestigeRankingSnapshot.Create(null!));
        var valid = PrestigeRankingSnapshot.Create([PrestigeRankingQueryTestData.Row()]);
        Assert.Throws<ArgumentOutOfRangeException>(() => valid.GetRanks((PrestigeRankCategory)12));
        Assert.Throws<ArgumentOutOfRangeException>(() => valid.GetRanks((PrestigeRankCategory)uint.MaxValue));
    }
}
