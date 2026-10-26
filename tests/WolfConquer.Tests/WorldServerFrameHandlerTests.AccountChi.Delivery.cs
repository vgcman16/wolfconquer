namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public Task AccountManagerChargeChiAtomicFailedPostcommitLogCannotRefundOrChargeAFreshRepeat() =>
        RunChiPurchaseAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = ChiFiles(fixture);
            var captured = fixture.Context;
            var attempts = 0;
            Exception? observationFailure = null;
            fixture.Log.BeforeLine = line =>
            {
                if (line?.StartsWith("WorldServer persisted LegacyNpc AccountManager attribute flag purchase", StringComparison.Ordinal) != true) return;
                attempts++;
                observationFailure = Record.Exception(() => AssertChiCommitted(fixture, before, files, 150));
                throw new IOException("Owned actual postcommit ChargeChi logger failed.");
            };

            await fixture.Purchase(captured);

            Assert.Null(observationFailure);
            Assert.Equal(1, attempts);
            AssertChiCommitted(fixture, before, files, 150);
            Assert.False(fixture.Recovery.IsBlocked);
            Assert.DoesNotContain("WorldServer skipped LegacyNpc AccountManager attribute flag purchase persistence", fixture.Log.ToString());
            Assert.DoesNotContain("WorldServer held for recovery LegacyNpc AccountManager attribute flag purchase", fixture.Log.ToString());
            var committed = fixture.Current;
            var committedFiles = ChiFiles(fixture);
            fixture.Log.BeforeLine = null;

            await fixture.Purchase(captured);
            AssertChiUnchanged(fixture, committed, committedFiles);
            await fixture.Purchase();
            AssertChiUnchanged(fixture, committed, committedFiles);
        }, donation: "250");
}
