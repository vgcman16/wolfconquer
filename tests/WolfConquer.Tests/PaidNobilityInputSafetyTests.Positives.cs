using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class PaidNobilityInputSafetyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("False")]
    [InlineData(" false ")]
    [InlineData("0")]
    [InlineData(" -0 ")]
    public void MissingOrValidInactiveFlagPreservesOptionalMissingOrIrrelevantExpiryFields(string? active)
    {
        var paid = active is null ? "PeriodTime=bad\r\nLastNobilityDonation=-1\r\n" : "IsActive=" + active + "\r\n";
        using var fixture = new PaidFile(paid);
        var loaded = fixture.Loader.TryLoadByAccountId(9001);
        Assert.True(loaded.Succeeded, loaded.Reason);
        var snapshot = Assert.IsType<CharacterSnapshot>(loaded.Snapshot);
        Assert.Equal("777777777", snapshot.Attributes["DonationNobility"]);
        Assert.Equal("7", snapshot.Attributes["NobilityRank"]);
        Assert.Equal("paid-input", snapshot.PaidNobility["OpaqueKeep"]);
        fixture.AssertIntact();
    }

    [Theory]
    [InlineData("True")]
    [InlineData(" true ")]
    [InlineData("1")]
    [InlineData(" +2 ")]
    [InlineData("255")]
    public void ValidBooleanNumericAndWhitespaceActiveFormsValidateWithoutExpiringFutureData(string active)
    {
        using var fixture = new PaidFile(Active(" +" + Binary(Future) + " ", " +18446744073709551615 ", active));
        var loaded = fixture.Loader.TryLoadByAccountId(9001);
        Assert.True(loaded.Succeeded, loaded.Reason);
        var snapshot = Assert.IsType<CharacterSnapshot>(loaded.Snapshot);
        Assert.Equal("777777777", snapshot.Attributes["DonationNobility"]);
        Assert.Equal("7", snapshot.Attributes["NobilityRank"]);
        Assert.Same(snapshot, CharacterSnapshotLoader.PrepareExpiredPaidNobility(snapshot, Future.AddTicks(-1)));
        fixture.AssertIntact();
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void ActualTypedExpiryKeepsItsInclusiveDeadlineAndRestoresTheValidatedDonation(int ticks, bool expired)
    {
        var source = CharacterSnapshotParser.ParseText(CharacterWithPaid(Active(Binary(Future), "30000000", "2")));
        var result = CharacterSnapshotLoader.PrepareExpiredPaidNobility(source, Future.AddTicks(ticks));
        Assert.Equal(expired ? "30000000" : "777777777", result.Attributes["DonationNobility"]);
        Assert.Equal(expired ? "1" : "7", result.Attributes["NobilityRank"]);
        Assert.Equal(expired ? "-1" : "2", result.Attributes["NobilityPosition"]);
        Assert.Equal(expired ? "False" : "2", result.PaidNobility["IsActive"]);
        Assert.Equal("paid-input", result.PaidNobility["OpaqueKeep"]);
        Assert.Equal("777777777", source.Attributes["DonationNobility"]);
        Assert.Equal("2", source.PaidNobility["IsActive"]);
        Assert.Equal(source.Money, result.Money); Assert.Equal(source.ConquerPoints, result.ConquerPoints);
        if (!expired) Assert.Same(source, result);
    }

    [Fact]
    public void ExplicitZeroDonationAndDateMinAreValidValuesRatherThanMissingData()
    {
        var source = CharacterSnapshotParser.ParseText(CharacterWithPaid(Active("0", "0")));
        var result = CharacterSnapshotLoader.PrepareExpiredPaidNobility(source, DateTime.MinValue);
        Assert.Equal("0", result.Attributes["DonationNobility"]); Assert.Equal("0", result.Attributes["NobilityRank"]);
        Assert.Equal("False", result.PaidNobility["IsActive"]);
        Assert.Equal("777777777", source.Attributes["DonationNobility"]);
    }

    [Fact]
    public void ActualPublicExpiredFileLoadKeepsValidNormalizationAndUntouchedPeer()
    {
        using var fixture = new PaidFile(Active(Binary(Past), "30000000", "1"));
        var loaded = fixture.Loader.TryLoadByAccountId(9001);
        Assert.True(loaded.Succeeded, loaded.Reason);
        var snapshot = Assert.IsType<CharacterSnapshot>(loaded.Snapshot);
        Assert.Equal("30000000", snapshot.Attributes["DonationNobility"]); Assert.Equal("1", snapshot.Attributes["NobilityRank"]);
        Assert.Equal("False", snapshot.PaidNobility["IsActive"]);
        var disk = CharacterSnapshotParser.ParseText(File.ReadAllText(fixture.Pathname));
        Assert.Equal(snapshot.Attributes["DonationNobility"], disk.Attributes["DonationNobility"]);
        Assert.Equal("False", disk.PaidNobility["IsActive"]);
        Assert.Equal(1000L, disk.Money); Assert.Equal(100L, disk.ConquerPoints);
        Assert.Equal(SqlWorldStateTestData.Character(9002), File.ReadAllBytes(fixture.OtherPath));
        // Successful owned local write only; failed writer/CAS outcomes are a separate audit.
    }
}
