using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class PaidNobilityInputSafetyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualDuplicatePaidFieldOrSectionRefusesBeforeExpiryReplacement(bool duplicateSection)
    {
        var paid = Active(Binary(Past), "30000000") +
            (duplicateSection ? "[PaidNobility]\r\nIsActive=True\r\n" : "IsActive=True\r\n");
        using var fixture = new PaidFile(paid);
        // The actual general parser merges this old input; the strict projector must refuse its ambiguity.
        Assert.Equal("True", CharacterSnapshotParser.ParseText(Encoding.UTF8.GetString(fixture.Original)).PaidNobility["IsActive"]);
        var result = fixture.Loader.TryLoadByAccountId(9001);
        Assert.False(result.Succeeded); Assert.Null(result.Snapshot);
        Assert.Equal(CharacterSnapshotLoadStatus.SnapshotParseFailed, result.Status);
        Assert.Equal("paidNobilityStorageInvalid", result.Reason);
        fixture.AssertIntact();
    }

    [WindowsPaidExpiryFileFact]
    public void ActualHeldCharacterReadLeaseRefusesExpiryWithoutSnapshotThenReleaseRetryCommitsOnce()
    {
        var paid = Active(Binary(Past), "30000000") + "Identifier=19\r\nPaidRank=7\r\nRank=King\r\nPosition=1\r\n";
        using var fixture = new PaidFile(paid);
        var originalSnapshot = CharacterSnapshotParser.ParseText(Encoding.UTF8.GetString(fixture.Original));
        var originalFiles = Directory.GetFiles(fixture.Users).Order(StringComparer.Ordinal).ToArray();
        using (var lease = new FileStream(fixture.Pathname, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var refused = fixture.Loader.TryLoadByAccountId(9001);
            Assert.False(refused.Succeeded); Assert.Null(refused.Snapshot);
            Assert.Equal(CharacterSnapshotLoadStatus.SnapshotWriteFailed, refused.Status);
            Assert.Equal("paidNobilityPersistenceRefused", refused.Reason);
            Assert.Equal(9001L, refused.AccountId); Assert.Equal(fixture.Pathname, refused.Path);
            fixture.AssertIntact();
            var stillSaved = CharacterSnapshotParser.ParseText(File.ReadAllText(fixture.Pathname));
            Assert.Equal("777777777", stillSaved.Attributes["DonationNobility"]);
            Assert.Equal("777777777", stillSaved.Attributes["LastNobilityDonation"]);
            Assert.Equal("7", stillSaved.Attributes["NobilityRank"]); Assert.Equal("2", stillSaved.Attributes["NobilityPosition"]);
            Assert.Equal("True", stillSaved.PaidNobility["IsActive"]);
            Assert.Equal(originalSnapshot.Money, stillSaved.Money); Assert.Equal(originalSnapshot.ConquerPoints, stillSaved.ConquerPoints);
            Assert.Equal(originalSnapshot.Online, stillSaved.Online);
        }

        // The same real loader can retry only after the actual deny-replacement lease is released.
        var retry = fixture.Loader.TryLoadByAccountId(9001);
        Assert.True(retry.Succeeded, retry.Reason);
        Assert.Equal(CharacterSnapshotLoadStatus.Success, retry.Status);
        var saved = Assert.IsType<CharacterSnapshot>(retry.Snapshot);
        Assert.Equal("30000000", saved.Attributes["DonationNobility"]);
        Assert.Equal("30000000", saved.Attributes["LastNobilityDonation"]);
        Assert.Equal("1", saved.Attributes["NobilityRank"]); Assert.Equal("-1", saved.Attributes["NobilityPosition"]);
        Assert.Equal("False", saved.PaidNobility["IsActive"]); Assert.Equal("paid-input", saved.PaidNobility["OpaqueKeep"]);
        Assert.Equal(originalSnapshot.Money, saved.Money); Assert.Equal(originalSnapshot.ConquerPoints, saved.ConquerPoints);
        Assert.Equal(originalSnapshot.Online, saved.Online);

        const string normalizedPaid = "[PaidNobility]\r\nIsActive=False\r\nPeriodTime=0\r\nLastNobilityDonation=0\r\n" +
            "Identifier=0\r\nPaidRank=0\r\nRank=Serf\r\nPosition=-1\r\nOpaqueKeep=paid-input\r\n";
        var expected = Encoding.UTF8.GetString(fixture.Original)
            .Replace("[PaidNobility]\r\n" + paid + "OpaqueKeep=paid-input\r\n", normalizedPaid, StringComparison.Ordinal)
            .Replace("DonationNobility=777777777", "DonationNobility=30000000", StringComparison.Ordinal)
            .Replace("LastNobilityDonation=777777777", "LastNobilityDonation=30000000", StringComparison.Ordinal)
            .Replace("NobilityRank=7\r\n", "NobilityRank=1\r\n", StringComparison.Ordinal)
            .Replace("NobilityPosition=2\r\n", "NobilityPosition=-1\r\n", StringComparison.Ordinal);
        var settled = File.ReadAllBytes(fixture.Pathname);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), settled);
        Assert.Equal(SqlWorldStateTestData.Character(9002), File.ReadAllBytes(fixture.OtherPath));
        Assert.Equal(originalFiles, Directory.GetFiles(fixture.Users).Order(StringComparer.Ordinal).ToArray());

        var repeated = fixture.Loader.TryLoadByAccountId(9001);
        Assert.True(repeated.Succeeded, repeated.Reason);
        var repeatedSnapshot = Assert.IsType<CharacterSnapshot>(repeated.Snapshot);
        Assert.Equal("30000000", repeatedSnapshot.Attributes["DonationNobility"]);
        Assert.Equal("1", repeatedSnapshot.Attributes["NobilityRank"]);
        Assert.Equal("False", repeatedSnapshot.PaidNobility["IsActive"]);
        Assert.Equal(settled, File.ReadAllBytes(fixture.Pathname));
        Assert.Equal(SqlWorldStateTestData.Character(9002), File.ReadAllBytes(fixture.OtherPath));
        Assert.Equal(originalFiles, Directory.GetFiles(fixture.Users).Order(StringComparer.Ordinal).ToArray());
    }

    private sealed class WindowsPaidExpiryFileFactAttribute : FactAttribute
    {
        public WindowsPaidExpiryFileFactAttribute()
        {
            if (!OperatingSystem.IsWindows()) Skip = "Requires the target Windows deny-replacement file sharing contract.";
        }
    }
}
