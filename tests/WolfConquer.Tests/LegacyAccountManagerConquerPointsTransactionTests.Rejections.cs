using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerConquerPointsTransactionTests
{
    [WindowsNpcSaleTheory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualReadOrReplacementDenialLeavesTheWalletAndDebitUnchangedAndRetrySucceeds(bool allowRead) =>
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
            AssertNoApplied(fixture);
            Assert.False(fixture.Recovery.IsBlocked);

            await fixture.Purchase();

            AssertCommitted(fixture, before, files, 25, 1_000_000_400);
            AssertWalletFrame(Assert.Single(fixture.Sent), 1_000_000_400);
        });

    [Theory]
    [InlineData("DonatePoints", "-1")]
    [InlineData("DonatePoints", "not-a-number")]
    [InlineData("DonatePoints", "18446744073709551616")]
    [InlineData("ConquerPoints", "-1")]
    [InlineData("ConquerPoints", "not-a-number")]
    [InlineData("ConquerPoints", "4294967296")]
    [InlineData("ConquerPoints", "9223372036854775807")]
    [InlineData("BankCps", "-1")]
    [InlineData("BankCps", "not-a-number")]
    [InlineData("BankCps", "9223372036854775808")]
    public Task MalformedOrOutOfRangeOwnedFieldsCannotBecomeDefaultValuesOrUnsignedWallets(string key, string raw) =>
        RunAsync(async fixture =>
        {
            RewriteOwnedField(fixture, key, raw);
            var before = fixture.Current;
            var files = Files(fixture);

            await fixture.Purchase();

            AssertUnchanged(fixture, before, files);
            AssertNoApplied(fixture);
            Assert.Empty(fixture.Sent);
        });

    [Theory]
    [InlineData("3294967296")]
    [InlineData("4294967295")]
    public Task AValidExistingWalletCannotOverflowItsConfirmedNativeWidthBeforeTheDebit(string points) =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);

            await fixture.Purchase();

            AssertUnchanged(fixture, before, files);
            AssertNoApplied(fixture);
            Assert.Empty(fixture.Sent);
        }, conquerPoints: points);

    [Theory]
    [InlineData("0")]
    [InlineData("99")]
    public Task InsufficientFundsRetainTheExistingFourFrameDialogOrderWithoutChangingAnyFile(string donation) =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);

            await fixture.Purchase();

            AssertUnchanged(fixture, before, files);
            AssertNoApplied(fixture);
            var replies = fixture.Sent.Select(ReadFrame).Select(frame =>
            { Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var reply)); return reply; }).ToArray();
            Assert.Equal(new[] { LegacyNpcReplyInteractType.Dialog, LegacyNpcReplyInteractType.Option,
                LegacyNpcReplyInteractType.Avatar, LegacyNpcReplyInteractType.Finish }, replies.Select(reply => reply.InteractType));
            Assert.Contains("You need 100 donation points.", replies[0].Text, StringComparison.Ordinal);
            Assert.Equal("I~see.", replies[1].Text);
            Assert.Equal((byte)255, replies[1].OptionId);
        }, donation: donation);

    [Theory]
    [InlineData("DonatePoints", "124")]
    [InlineData("ConquerPoints", "401")]
    [InlineData("BankCps", "445")]
    public Task ChangedDiskEconomicValuesCannotBeOverwrittenByAnEarlierCurrentSnapshot(string key, string raw) =>
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
            AssertNoApplied(fixture);
            Assert.Empty(fixture.Sent);
        });

    [Theory]
    [InlineData("DonatePoints")]
    [InlineData("ConquerPoints")]
    [InlineData("BankCps")]
    public Task AmbiguousDuplicateEconomicRowsAreNotRewrittenAsAValidPurchase(string key) =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var text = File.ReadAllText(fixture.UserPath).Replace("[Unrelated]",
                key + "=" + before.Character.Attributes[key] + "\r\n[Unrelated]", StringComparison.Ordinal);
            File.WriteAllText(fixture.UserPath, text, new UTF8Encoding(true));
            var files = Files(fixture);

            await fixture.Purchase();

            AssertUnchanged(fixture, before, files);
            AssertNoApplied(fixture);
            Assert.Empty(fixture.Sent);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DisagreementBetweenTheRealWalletOrBankModelAndRawAttributeCannotSpendTheDonation(bool bank) =>
        RunAsync(async fixture =>
        {
            var previous = fixture.Current;
            var before = previous with { Character = previous.Character with
            {
                ConquerPoints = bank ? previous.Character.ConquerPoints : 401,
                BankConquerPoints = bank ? 445 : previous.Character.BankConquerPoints,
            } };
            Assert.True(fixture.Sessions.TryCompareAndSet(fixture.Connection, previous, before));
            var files = Files(fixture);

            await fixture.Purchase();

            AssertUnchanged(fixture, before, files);
            AssertNoApplied(fixture);
            Assert.Empty(fixture.Sent);
        });

    [Fact]
    public Task ForeignCharacterFileBindingCannotAuthorizeTheConversion() =>
        RunAsync(async fixture =>
        {
            var previous = fixture.Current;
            var before = previous with { Character = previous.Character with { SourceFile = "2399654.ini" } };
            Assert.True(fixture.Sessions.TryCompareAndSet(fixture.Connection, previous, before));
            var files = Files(fixture);

            await fixture.Purchase();

            AssertUnchanged(fixture, before, files);
            AssertNoApplied(fixture);
            Assert.Empty(fixture.Sent);
        });
}
