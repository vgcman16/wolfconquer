namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerFullPromotionTransactionTests
{
    [Fact]
    public Task FailedActualPostcommitLogNotificationDoesNotRefundOrReplayThePurchase() =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            var captured = fixture.Context;
            var attempts = 0;
            Exception? observationFailure = null;
            fixture.Log.BeforeLine = line =>
            {
                if (line?.StartsWith("WorldServer persisted LegacyNpc AccountManager full promotion", StringComparison.Ordinal) != true) return;
                attempts++;
                observationFailure = Record.Exception(() => AssertCommitted(fixture, before, files, 5, 1051, 3));
                throw new IOException("Owned postcommit logger failed to deliver the notification.");
            };

            await fixture.Purchase(captured);

            Assert.Null(observationFailure);
            Assert.Equal(1, attempts);
            AssertCommitted(fixture, before, files, 5, 1051, 3);
            Assert.Empty(fixture.Sent); // The established successful path emits no packet.
            var committed = fixture.Current;
            var committedFiles = Files(fixture);
            fixture.Log.BeforeLine = null;
            await fixture.Purchase(captured);
            Assert.Same(committed, fixture.Current);
            AssertFiles(fixture, committedFiles);
            Assert.Empty(fixture.Sent);
            Assert.False(fixture.Recovery.IsBlocked);
        });
}
