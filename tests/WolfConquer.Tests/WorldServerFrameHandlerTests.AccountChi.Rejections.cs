using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [WindowsNpcSaleTheory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AccountManagerChargeChiAtomicActualReadOrReplacementDenialDoesNotDebitAndRetrySucceeds(bool allowRead) =>
        RunChiPurchaseAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = ChiFiles(fixture);
            using (File.Open(fixture.UserPath, FileMode.Open, FileAccess.Read, allowRead ? FileShare.Read : FileShare.None))
            {
                await fixture.Purchase();
                Assert.Same(before, fixture.Current);
                Assert.Empty(fixture.Sent);
            }
            AssertChiUnchanged(fixture, before, files);
            Assert.Equal(0, ChiAppliedCount(fixture));
            Assert.False(fixture.Recovery.IsBlocked);

            await fixture.Purchase();

            AssertChiCommitted(fixture, before, files, 15);
        });

    [Theory]
    [InlineData("DonatePoints", "-1")]
    [InlineData("DonatePoints", "not-a-number")]
    [InlineData("DonatePoints", "18446744073709551616")]
    [InlineData("ChargeChi", "-1")]
    [InlineData("ChargeChi", "not-a-number")]
    [InlineData("ChargeChi", "256")]
    [InlineData("ChargeChi", "18446744073709551615")]
    public Task AccountManagerChargeChiAtomicMalformedOrOverflowingOwnedValuesCannotDefaultOrWrap(string key, string raw) =>
        RunChiPurchaseAsync(async fixture =>
        {
            ChiSetOwnedField(fixture, key, raw); // Poison only the already-admitted real holder.
            var before = fixture.Current;
            var files = ChiFiles(fixture);

            await fixture.Purchase();

            AssertChiUnchanged(fixture, before, files);
            Assert.Equal(0, ChiAppliedCount(fixture));
        });

    [Theory]
    [InlineData("0")]
    [InlineData("99")]
    public Task AccountManagerChargeChiAtomicInsufficientDonationRetainsTheExistingFourFrameDialog(string donation) =>
        RunChiPurchaseAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = ChiFiles(fixture);

            await fixture.Purchase();

            Assert.Same(before, fixture.Current);
            AssertChiFiles(fixture, files);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
            Assert.Equal(0, ChiAppliedCount(fixture));
            var replies = fixture.Sent.Select(ReadFrame).Select(frame =>
            { Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var reply)); return reply; }).ToArray();
            Assert.Equal(new[] { LegacyNpcReplyInteractType.Dialog, LegacyNpcReplyInteractType.Option,
                LegacyNpcReplyInteractType.Avatar, LegacyNpcReplyInteractType.Finish }, replies.Select(reply => reply.InteractType));
            Assert.Contains("You need 100 donation points.", replies[0].Text, StringComparison.Ordinal);
            Assert.Equal("I~see.", replies[1].Text);
            Assert.Equal(byte.MaxValue, replies[1].OptionId);
            Assert.Equal((ushort)263, replies[2].InputMaxLength);
        }, donation);

    [Theory]
    [InlineData("DonatePoints", "114")]
    [InlineData("ChargeChi", "2")]
    public Task AccountManagerChargeChiAtomicChangedSavedValuesCannotBeOverwrittenFromTheCurrentSnapshot(string key, string raw) =>
        RunChiPurchaseAsync(async fixture =>
        {
            var before = fixture.Current;
            var text = File.ReadAllText(fixture.UserPath);
            var changed = text.Replace(key + "=" + before.Character.Attributes[key] + "\r\n", key + "=" + raw + "\r\n", StringComparison.Ordinal);
            Assert.NotEqual(text, changed);
            File.WriteAllText(fixture.UserPath, changed, new UTF8Encoding(true));
            var files = ChiFiles(fixture);

            await fixture.Purchase();

            AssertChiUnchanged(fixture, before, files);
            Assert.Equal(0, ChiAppliedCount(fixture));
        });

    [Theory]
    [InlineData("DonatePoints")]
    [InlineData("ChargeChi")]
    public Task AccountManagerChargeChiAtomicDuplicateEconomicRowsCannotObtainAReplacementImage(string key) =>
        RunChiPurchaseAsync(async fixture =>
        {
            var before = fixture.Current;
            var text = File.ReadAllText(fixture.UserPath).Replace("[PaidNobility]",
                key + "=" + before.Character.Attributes[key] + "\r\n[PaidNobility]", StringComparison.Ordinal);
            File.WriteAllText(fixture.UserPath, text, new UTF8Encoding(true));
            var files = ChiFiles(fixture);

            await fixture.Purchase();

            AssertChiUnchanged(fixture, before, files);
            Assert.Equal(0, ChiAppliedCount(fixture));
        });

    [Fact]
    public Task AccountManagerChargeChiAtomicForeignFileBindingCannotSpendTheCurrentDonation() =>
        RunChiPurchaseAsync(async fixture =>
        {
            var previous = fixture.Current;
            var before = previous with { Character = previous.Character with { SourceFile = "1293601.ini" } };
            Assert.True(fixture.Sessions.TryCompareAndSet(fixture.Connection, previous, before));
            var files = ChiFiles(fixture);

            await fixture.Purchase();

            AssertChiUnchanged(fixture, before, files);
            Assert.Equal(0, ChiAppliedCount(fixture));
        });
}
