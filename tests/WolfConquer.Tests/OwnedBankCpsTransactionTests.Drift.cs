using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedBankCpsTransactionTests
{
    private static readonly FieldInfo BankInventoryCps = typeof(LegacyNativePlayerRuntime)
        .GetField("_cps", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo BankBoundCps = typeof(LegacyNativePlayerRuntime)
        .GetField("_bountCps", BindingFlags.NonPublic | BindingFlags.Instance)!;

    [Theory]
    [InlineData(false, "cp")]
    [InlineData(false, "bound")]
    [InlineData(false, "bank")]
    [InlineData(false, "warehouse")]
    [InlineData(true, "cp")]
    [InlineData(true, "bound")]
    [InlineData(true, "bank")]
    [InlineData(true, "warehouse")]
    public Task NativeWalletDriftBeforeCpBankConfirmationDoesNotConsumePendingOrReachTheWriter(bool withdraw, string slot) =>
        WithBank(async f =>
        {
            uint amount = withdraw ? 100U : 300U;
            await f.Login(); await StageBank(f, deposit: !withdraw, amount);
            var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
            var baseline = CpBankNative(f);
            SetCpBankRawFault(f, slot, restore: false);
            var fault = CpBankNative(f);
            try
            {
                await ConfirmBank(f, deposit: !withdraw);

                Assert.Same(before, f.Current); Assert.Equal(amount, f.Current.PendingBankConquerPoints);
                Assert.Equal(!withdraw, f.Current.PendingBankConquerPointsIsDeposit);
                Assert.Equal(fault, CpBankNative(f)); AssertNoBankSuccess(f.Sent.Skip(first));
                f.CheckFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Users));
            }
            finally { SetCpBankRawFault(f, slot, restore: true); }
            Assert.Equal(baseline, CpBankNative(f)); int retry = f.Sent.Count;

            await ConfirmBank(f, deposit: !withdraw);

            var cps = withdraw ? 1_100L : 700L; var bank = withdraw ? 150L : 550L;
            AssertBankTuple(f, cps, bank); AssertBankSuccessFrames(f.Sent.Skip(retry).ToArray(), cps, bank);
        });

    [Theory]
    [InlineData("cp")]
    [InlineData("bound")]
    [InlineData("bank")]
    [InlineData("warehouse")]
    public Task NativeWalletDriftAfterTheGenuineCpBankWriterRetiresWithoutRefundOrSuccess(string slot) =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, deposit: true, 300);
            var before = f.Current; var proposal = BankCpsProposal(before, deposit: true, 300);
            var itemBytes = File.ReadAllBytes(f.ItemsPath); int first = f.Sent.Count;
            bool saved = false; var reason = string.Empty;
            bool ActualWriterThenRawFault()
            {
                saved = LegacyNpcSalePersistence.TryPersistBankCps(before, proposal, f.Items, deposit: true, 300, out reason);
                if (saved) SetCpBankRawFault(f, slot, restore: false);
                return saved;
            }

            Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
                f.Store.TryCommitSession(f.Connection, before, proposal, ActualWriterThenRawFault));

            Assert.True(saved, reason); Assert.False(f.Store.TryGet(f.Connection, out _));
            Assert.True(f.Recovery.IsBlocked); Assert.True(f.Native.OrdinarySpellBinding!.IsRetired);
            Assert.Equal(300U, before.PendingBankConquerPoints);
            var persisted = ReadCpBankSaved(f);
            Assert.Equal((700L, 550L, 1_000L, 300L),
                (persisted.ConquerPoints, persisted.BankConquerPoints, persisted.Money, persisted.BoundConquerPoints));
            Assert.Equal("555", persisted.Attributes["WHMoney"]); Assert.Equal("555", persisted.Attributes["WarehouseMoney"]);
            Assert.Equal(itemBytes, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(first, f.Sent.Count);
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
            Assert.NotEqual((1_000U, 300, 250UL, 555L, 1_000L), CpBankNative(f));
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SavedCpBankInputDriftAfterGenuineWriterRetiresBeforePublishingThePreparedNativeTuple(bool proposedInput) =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, deposit: true, 300);
            var before = f.Current; var proposal = BankCpsProposal(before, deposit: true, 300);
            var expectedFields = Assert.IsType<Dictionary<string, string>>(before.Character.Attributes);
            var proposedFields = Assert.IsType<Dictionary<string, string>>(proposal.Character.Attributes);
            int first = f.Sent.Count; bool saved = false; var reason = string.Empty;
            bool ActualWriterThenValueFault()
            {
                saved = LegacyNpcSalePersistence.TryPersistBankCps(before, proposal, f.Items, deposit: true, 300, out reason);
                if (saved) (proposedInput ? proposedFields : expectedFields)["BankCps"] = "999";
                return saved;
            }

            Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
                f.Store.TryCommitSession(f.Connection, before, proposal, ActualWriterThenValueFault));

            Assert.True(saved, reason); Assert.False(f.Store.TryGet(f.Connection, out _));
            Assert.True(f.Recovery.IsBlocked); Assert.True(f.Native.OrdinarySpellBinding!.IsRetired);
            Assert.Equal((1_000U, 300, 250UL, 555L, 1_000L), CpBankNative(f));
            Assert.Equal((700L, 550L), (ReadCpBankSaved(f).ConquerPoints, ReadCpBankSaved(f).BankConquerPoints));
            Assert.Equal(first, f.Sent.Count); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });

    private static (uint Cps, int Bound, ulong Bank, long Warehouse, long Money) CpBankNative(Fixture f) =>
        (f.Native.Player.ConquerPoints, f.Native.Player.BoundConquerPoints,
            f.Native.ConquerPointsSave, f.Native.Player.WHMoney, f.Native.Player.Money);

    private static void SetCpBankRawFault(Fixture f, string slot, bool restore)
    {
        switch (slot)
        {
            case "cp": BankInventoryCps.SetValue(f.Native.Player, restore ? 1_000U : 1_001U); break;
            case "bound": BankBoundCps.SetValue(f.Native.Player, restore ? 300 : 301); break;
            case "bank": f.Native.ConquerPointsSave = restore ? 250UL : 251UL; break;
            case "warehouse": f.Native.Player.WHMoney = restore ? 555L : 556L; break;
            default: throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
}
