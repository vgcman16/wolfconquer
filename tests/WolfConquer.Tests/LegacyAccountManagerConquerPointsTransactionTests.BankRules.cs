namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerConquerPointsTransactionTests
{
    [Fact]
    public Task CheckedBankOverflowRejectsBeforeTheDonationDebitOrAnySuccessOutput() =>
        RunAsync(async fixture =>
        {
            RewriteOwnedField(fixture, "BankCps", "9223372036854775807");
            var before = fixture.Current;
            var files = Files(fixture);

            await fixture.Purchase();

            AssertUnchanged(fixture, before, files);
            AssertNoApplied(fixture);
            Assert.Empty(fixture.Sent);
        }, conquerPoints: "1000000001");

    [Fact]
    public Task ValidMaximumBankIsPreservedWhenThereIsNoSpillInsteadOfInventingOverflow() =>
        RunAsync(async fixture =>
        {
            RewriteOwnedField(fixture, "BankCps", "9223372036854775807");
            var before = fixture.Current;
            var files = Files(fixture);

            await fixture.Purchase();

            AssertCommitted(fixture, before, files, 25, 1_000_000_400);
            AssertPurchaseFrames(fixture, 1_000_000_400, 0);
        });

    [Fact]
    public Task ANoSpillPurchasePreservesTheUnchangedBanksOriginalRawRepresentation() =>
        RunAsync(async fixture =>
        {
            RewriteOwnedField(fixture, "BankCps", "000444");
            var before = fixture.Current;
            var files = Files(fixture);

            await fixture.Purchase();

            AssertCommitted(fixture, before, files, 25, 1_000_000_400);
            Assert.Equal("000444", fixture.Current.Character.Attributes["BankCps"]);
            AssertPurchaseFrames(fixture, 1_000_000_400, 0);
        });

    [Theory]
    [InlineData("ConquerPoints")]
    [InlineData("BankCps")]
    public Task GenuineMissingNativeWalletDefaultsRemainZeroWithoutUnnecessaryBankMaterialization(string missing) =>
        RunAsync(async fixture =>
        {
            RemoveOwnedWalletField(fixture, missing);
            var before = fixture.Current;
            var files = Files(fixture);
            var points = missing == "ConquerPoints" ? 1_000_000_000u : 1_000_000_400u;

            await fixture.Purchase();

            AssertCommitted(fixture, before, files, 25, points);
            AssertPurchaseFrames(fixture, points, 0);
            if (missing == "BankCps") Assert.False(fixture.Current.Character.Attributes.ContainsKey("BankCps"));
        });

    [Fact]
    public Task ARealSpillMaterializesTheMissingDefaultBankInsideTheSameCharacterImage() =>
        RunAsync(async fixture =>
        {
            RemoveOwnedWalletField(fixture, "BankCps");
            var before = fixture.Current;
            var files = Files(fixture);

            await fixture.Purchase();

            AssertCommitted(fixture, before, files, 25, 2_000_000_000, 1);
            AssertPurchaseFrames(fixture, 2_000_000_000, 1);
        }, conquerPoints: "1000000001");
}
