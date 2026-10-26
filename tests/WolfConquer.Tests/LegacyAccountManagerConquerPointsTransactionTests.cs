namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerConquerPointsTransactionTests
{
    [Theory]
    [InlineData("125", "400", 25UL, 1_000_000_400u, 444L, 0u)]
    [InlineData("100", "1000000000", 0UL, 2_000_000_000u, 444L, 0u)]
    [InlineData("100", "3294967295", 0UL, 2_000_000_000u, 2_294_967_739L, 2_294_967_295u)]
    [InlineData("18446744073709551615", "0", ulong.MaxValue - 100, 1_000_000_000u, 444L, 0u)]
    [InlineData("100", "2500000000", 0UL, 2_000_000_000u, 1_500_000_444L, 1_500_000_000u)]
    public Task ActualOptionThreeCommitsItsExactWalletAndConditionalBankImageBeforeTheOriginalWire(
        string donation, string points, ulong expectedDonation, uint expectedPoints, long expectedBank, uint spill) =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);

            await fixture.Purchase();

            AssertCommitted(fixture, before, files, expectedDonation, expectedPoints, expectedBank);
            AssertPurchaseFrames(fixture, expectedPoints, spill);
            Assert.Contains("WorldServer applied LegacyNpc AccountManager conquer point purchase", fixture.Log.ToString());
            Assert.False(fixture.Recovery.IsBlocked);
        }, donation, points);

    [Fact]
    public Task TheFirstActualWalletSendObservesTheCompleteFileAndStoreCommit() =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            var observed = 0;
            Exception? observationFailure = null;
            fixture.BeforeSend = frame =>
            {
                observed++;
                observationFailure = Record.Exception(() =>
                {
                    AssertCommitted(fixture, before, files, 25, 1_000_000_400);
                    // Independent protobuf/little-endian literal: opcode2176,
                    // UID2399653, one type29 entry containing total1000000400.
                    Assert.Equal(Convert.FromHexString("1300800808A5BB92011208081D109097EBDC03"), frame);
                });
            };

            await fixture.Purchase();

            Assert.Null(observationFailure);
            Assert.Equal(1, observed);
            AssertWalletFrame(Assert.Single(fixture.Sent), 1_000_000_400);
        });

    [Fact]
    public Task AStaleCapturedPurchaseCannotReplayButANewPaidRequestIsADistinctPurchase() =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            var captured = fixture.Context;
            await fixture.Purchase(captured);
            AssertCommitted(fixture, before, files, 125, 1_000_000_400);
            var first = fixture.Current;
            var firstFiles = Files(fixture);

            await fixture.Purchase(captured);

            AssertUnchanged(fixture, first, firstFiles);
            AssertWalletFrame(Assert.Single(fixture.Sent), 1_000_000_400);
            await fixture.Purchase();
            AssertCommitted(fixture, first, firstFiles, 25, 2_000_000_000, 844);
            Assert.Equal(3, fixture.Sent.Count);
            AssertWalletFrame(fixture.Sent.Last(), 2_000_000_000);
            Assert.True(WolfConquer.Core.World.LegacyNpcReplyPacket.TryParse(ReadFrame(fixture.Sent.ElementAt(1)), out var box));
            Assert.Equal("400 Your ConquerPoints[CPS] has Transfare To Your Bank Thanks.", box.Text);
        }, donation: "225");
}
