namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("255")]
    [InlineData(null)]
    public Task AccountManagerChargeChiAtomicCommitsOnlyDonationAndTheNativeByteFlagWithoutSuccessWire(string? flag) =>
        RunChiPurchaseAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = ChiFiles(fixture);

            await fixture.Purchase();

            AssertChiCommitted(fixture, before, files, 15);
            Assert.Equal(1, ChiAppliedCount(fixture));
            Assert.False(fixture.Recovery.IsBlocked);
        }, flag: flag);

    [Fact]
    public Task AccountManagerChargeChiAtomicPreservesTheValidManagedUnsignedDonationWidth() =>
        RunChiPurchaseAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = ChiFiles(fixture);

            await fixture.Purchase();

            AssertChiCommitted(fixture, before, files, ulong.MaxValue - 100);
        }, donation: "18446744073709551615");

    [Theory]
    [InlineData("0")]
    [InlineData("115")]
    public Task AccountManagerChargeChiAtomicAlreadyEnabledPermanentBenefitDoesNotChargeForANoop(string donation) =>
        RunChiPurchaseAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = ChiFiles(fixture);

            await fixture.Purchase();

            AssertChiUnchanged(fixture, before, files);
            Assert.Equal(0, ChiAppliedCount(fixture));
        }, donation, flag: "1");

    [Fact]
    public Task AccountManagerChargeChiAtomicNeitherCapturedReplayNorFreshRepeatChargesThePersistentFlagAgain() =>
        RunChiPurchaseAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = ChiFiles(fixture);
            var captured = fixture.Context;
            await fixture.Purchase(captured);
            AssertChiCommitted(fixture, before, files, 150);
            var committed = fixture.Current;
            var committedFiles = ChiFiles(fixture);

            await fixture.Purchase(captured);
            AssertChiUnchanged(fixture, committed, committedFiles);
            await fixture.Purchase(); // A genuine new owner capture still sees permanent ChargeChi==1.
            AssertChiUnchanged(fixture, committed, committedFiles);
            Assert.Equal(1, ChiAppliedCount(fixture));
        }, donation: "250");
}
