using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedBankCpsTransactionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task AmbiguousUnrelatedPersistenceFieldsRefuseRealBankConfirmationWithoutConsumingItsPendingAmount(bool deposit) =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, deposit, 100);
            var current = f.Current;
            var fields = new Dictionary<string, string>(current.Character.Attributes, StringComparer.Ordinal)
            { ["TestMarker"] = "first", ["testmarker"] = "second" };
            f.Store.Set(f.Connection, current with { Character = current.Character with { Attributes = fields } });
            var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
            AssertBankTuple(f, 1000, 250, 100, deposit);

            await ConfirmBank(f, deposit);

            Assert.Same(before, f.Current); Assert.Same(fields, f.Current.Character.Attributes);
            AssertBankTuple(f, 1000, 250, 100, deposit); f.CheckFiles(files);
            AssertNoBankSuccess(f.Sent.Skip(first));
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
            Assert.Contains("bankCpsCharacterFieldsAmbiguous", f.Log.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Frame exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        });
}
