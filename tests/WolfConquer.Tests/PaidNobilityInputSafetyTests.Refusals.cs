using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class PaidNobilityInputSafetyTests
{
    [Theory]
    [MemberData(nameof(InvalidPaidInputs))]
    public void ActualPublicAndStagedCharacterLoadsRefuseMalformedPaidStateWithoutSnapshotOrAnyFileChange(string caseName, string paid)
    {
        Assert.False(string.IsNullOrWhiteSpace(caseName));
        using var fixture = new PaidFile(paid);
        var original = CharacterSnapshotParser.ParseText(System.Text.Encoding.UTF8.GetString(fixture.Original));
        Assert.Equal("777777777", original.Attributes["DonationNobility"]);
        Assert.Equal("7", original.Attributes["NobilityRank"]);
        Assert.Equal(1000L, original.Money); Assert.Equal(100L, original.ConquerPoints);

        AssertRefusedFileResult(fixture.Loader.TryLoadByAccountId(9001), fixture);
        fixture.AssertIntact();
        AssertRefusedFileResult(fixture.Loader.TryLoadByAccountIdForAdmission(9001), fixture);
        fixture.AssertIntact();
        var error = Assert.Throws<FormatException>(() => fixture.Loader.LoadByAccountId(9001));
        Assert.Contains("paidNobilityStorageInvalid", error.Message, StringComparison.Ordinal);
        fixture.AssertIntact();
    }

    private static void AssertRefusedFileResult(CharacterSnapshotLoadResult result, PaidFile fixture)
    {
        Assert.False(result.Succeeded); Assert.Null(result.Snapshot);
        Assert.Equal(CharacterSnapshotLoadStatus.SnapshotParseFailed, result.Status);
        Assert.Equal("paidNobilityStorageInvalid", result.Reason);
        Assert.Equal(9001L, result.AccountId); Assert.Equal(fixture.Pathname, result.Path);
    }
}
