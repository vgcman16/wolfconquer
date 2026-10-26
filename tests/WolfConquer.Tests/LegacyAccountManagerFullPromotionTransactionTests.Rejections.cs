using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerFullPromotionTransactionTests
{
    [WindowsNpcSaleTheory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualReadOrReplacementDenialCannotPublishPromotionOrDebitAndUnlockedRetrySucceeds(bool allowRead) =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            using (File.Open(fixture.UserPath, FileMode.Open, FileAccess.Read, allowRead ? FileShare.Read : FileShare.None))
            {
                await fixture.Purchase();
                Assert.Same(before, fixture.Current);
                Assert.Empty(fixture.Sent);
            }
            AssertUnchanged(fixture, before, files);
            Assert.False(fixture.Recovery.IsBlocked);
            await fixture.Purchase();
            AssertCommitted(fixture, before, files, 5, 1051, 3);
            Assert.Empty(fixture.Sent);
        });

    [Theory]
    [InlineData("DonatePoints", "-1")]
    [InlineData("DonatePoints", "not-a-number")]
    [InlineData("DonatePoints", "18446744073709551616")]
    [InlineData("ChargePromotion", "-1")]
    [InlineData("ChargePromotion", "not-a-number")]
    [InlineData("ChargePromotion", "256")]
    [InlineData("ChargePromotion", "255")]
    public Task MalformedOrExhaustedOwnedFieldsCannotBecomeDefaultValuesOrOverflow(string key, string raw) =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            await fixture.Purchase();
            AssertUnchanged(fixture, before, files);
            Assert.Empty(fixture.Sent);
        }, donation: key == "DonatePoints" ? raw : "35", charge: key == "ChargePromotion" ? raw : "2");

    [Theory]
    [InlineData("0")]
    [InlineData("29")]
    public Task InsufficientFundsPreserveEveryFileAndUseTheExistingFourFrameDialogOrder(string donation) =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            await fixture.Purchase();
            AssertUnchanged(fixture, before, files);
            var replies = fixture.Sent.Select(ReadFrame).Select(frame =>
            { Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var reply)); return reply; }).ToArray();
            Assert.Equal(new[] { LegacyNpcReplyInteractType.Dialog, LegacyNpcReplyInteractType.Option,
                LegacyNpcReplyInteractType.Avatar, LegacyNpcReplyInteractType.Finish }, replies.Select(reply => reply.InteractType));
            Assert.Contains("You need 30 donation points.", replies[0].Text, StringComparison.Ordinal);
        }, donation: donation);

    [Theory]
    [InlineData("DonatePoints", "34")]
    [InlineData("Class", "2005")]
    [InlineData("ChargePromotion", "3")]
    public Task ChangedEconomicDiskBaselineCannotBeOverwrittenFromTheCurrentSnapshot(string key, string raw) =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var text = File.ReadAllText(fixture.UserPath);
            var changed = text.Replace(key + "=" + before.Character.Attributes[key] + "\r\n", key + "=" + raw + "\r\n", StringComparison.Ordinal);
            Assert.NotEqual(text, changed);
            File.WriteAllText(fixture.UserPath, changed, new UTF8Encoding(true));
            var files = Files(fixture);
            await fixture.Purchase();
            AssertUnchanged(fixture, before, files);
            Assert.Empty(fixture.Sent);
        });

    [Fact]
    public Task UnsupportedSavedClassDoesNotConsumeDonationPointsForANoopPromotion() =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            await fixture.Purchase();
            AssertUnchanged(fixture, before, files);
            Assert.Empty(fixture.Sent);
        }, job: 10);
}
