namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerFullPromotionTransactionTests
{
    [Theory]
    [InlineData(1005L, 1051L)]
    [InlineData(2005L, 2051L)]
    [InlineData(4005L, 4051L)]
    [InlineData(5005L, 5051L)]
    [InlineData(6005L, 6051L)]
    [InlineData(7005L, 7051L)]
    [InlineData(8005L, 8051L)]
    [InlineData(9005L, 9051L)]
    [InlineData(13005L, 13051L)]
    [InlineData(14005L, 14051L)]
    [InlineData(16005L, 16051L)]
    public Task EveryEstablishedClassMappingCommitsExactlyThreeFieldsAndNoSuccessPacket(long job, long promoted) =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);

            await fixture.Purchase(); // Actual Frame -> actual registered reply.

            AssertCommitted(fixture, before, files, 5, promoted, 3);
            Assert.Empty(fixture.Sent);
            Assert.Contains("WorldServer applied LegacyNpc AccountManager full promotion", fixture.Log.ToString());
            Assert.Contains("option=36 cost=30", fixture.Log.ToString());
        }, job: job);

    [Theory]
    [InlineData(null, (byte)1)]
    [InlineData("254", (byte)255)]
    public Task NativeCounterDefaultAndLastRepresentableIncrementArePreserved(string? charge, byte expected) =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            await fixture.Purchase();
            AssertCommitted(fixture, before, files, 5, 1051, expected);
            Assert.Empty(fixture.Sent);
        }, charge: charge);

    [Fact]
    public Task DonationBalanceUsesItsGenuineUnsignedWidthWithoutTruncation() =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            await fixture.Purchase();
            AssertCommitted(fixture, before, files, ulong.MaxValue - 30, 1051, 3);
            Assert.Empty(fixture.Sent);
        }, donation: "18446744073709551615");

    [Fact]
    public Task AValidTerminalClassCanPayAgainThroughAFreshOwnerButNotAStaleCapturedRequest() =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var originalFiles = Files(fixture);
            var captured = fixture.Context;
            await fixture.Purchase(captured);
            AssertCommitted(fixture, before, originalFiles, 35, 1051, 3);
            var first = fixture.Current;
            var firstFiles = Files(fixture);

            await fixture.Purchase(captured);
            Assert.Same(first, fixture.Current);
            AssertFiles(fixture, firstFiles);
            await fixture.Purchase();

            AssertCommitted(fixture, first, firstFiles, 5, 1051, 4);
            Assert.Empty(fixture.Sent);
        }, donation: "65");
}
