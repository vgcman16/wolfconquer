using System.Globalization;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedBankCpsTransactionTests
{
    [Theory]
    [InlineData(true, 1000L, 250L, 300U, 700L, 550L)]
    [InlineData(false, 1000L, 250L, 125U, 1125L, 125L)]
    [InlineData(true, 4294967295L, 9223372032559808512L, uint.MaxValue, 0L, long.MaxValue)]
    [InlineData(false, 4294967195L, 250L, 100U, 4294967295L, 150L)]
    public Task ActualBankConfirmationCommitsSavedCurrentAndNativeWalletBeforeItsOriginalSuccessSequence(
        bool deposit, long cps, long bank, uint amount, long afterCps, long afterBank) =>
        WithBank(async f =>
        {
            await f.Login(); AssertBankTuple(f, cps, bank);
            var beforeStage = f.Files(); int stageFirst = f.Sent.Count;
            await StageBank(f, deposit, amount);
            AssertBankTuple(f, cps, bank, amount, deposit); f.CheckFiles(beforeStage);
            AssertBankStageFrames(f.Sent.Skip(stageFirst).ToArray(), deposit, amount);
            var items = File.ReadAllBytes(f.ItemsPath); var state = File.ReadAllBytes(f.Data.State);
            Exception? observed = null; int replies = 0, first = f.Sent.Count;
            f.OnSend = _ =>
            {
                replies++;
                try { AssertBankTuple(f, afterCps, afterBank); }
                catch (Exception error) { observed ??= error; }
            };

            await ConfirmBank(f, deposit); f.OnSend = null;

            if (observed is not null) ExceptionDispatchInfo.Capture(observed).Throw();
            Assert.Equal(5, replies); AssertBankTuple(f, afterCps, afterBank);
            AssertBankSuccessFrames(f.Sent.Skip(first).ToArray(), afterCps, afterBank);
            Assert.Equal(items, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(state, File.ReadAllBytes(f.Data.State));
            Assert.Equal((decimal)cps + bank, (decimal)afterCps + afterBank);
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
            Assert.DoesNotContain("Frame exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
            var committed = f.Current; var files = f.Files(); int replay = f.Sent.Count;

            await ConfirmBank(f, deposit);

            Assert.Same(committed, f.Current); f.CheckFiles(files);
            AssertBankTuple(f, afterCps, afterBank); AssertNoBankSuccess(f.Sent.Skip(replay));
        }, cps, bank);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task RealBankInputStagesOnlyTheCurrentPendingAuthorityWithoutCurrencyOrFileChanges(bool deposit) =>
        WithBank(async f =>
        {
            await f.Login(); AssertBankTuple(f, 1000, 250);
            var before = f.Current; var files = f.Files(); int first = f.Sent.Count;

            await StageBank(f, deposit, 100);

            Assert.NotSame(before, f.Current); AssertBankTuple(f, 1000, 250, 100, deposit);
            AssertBankStageFrames(f.Sent.Skip(first).ToArray(), deposit, 100); f.CheckFiles(files);
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("4294967296")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("1.0")]
    [InlineData("invalid")]
    public Task MalformedBankInputPreservesTheExistingRealPendingAmount(string input) =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, true, 25);
            var before = f.Current; var files = f.Files(); int first = f.Sent.Count;

            await SendBank(f, 10, input);

            Assert.Same(before, f.Current); AssertBankTuple(f, 1000, 250, 25, deposit: true);
            f.CheckFiles(files); AssertNoBankSuccess(f.Sent.Skip(first));
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task ConfirmationInTheWrongDirectionPreservesTheCurrentPendingAmount(bool deposit) =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, deposit, 100);
            var before = f.Current; var files = f.Files(); int first = f.Sent.Count;

            await ConfirmBank(f, !deposit);

            Assert.Same(before, f.Current); AssertBankTuple(f, 1000, 250, 100, deposit);
            f.CheckFiles(files); AssertNoBankSuccess(f.Sent.Skip(first));
        });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task BankConfirmationRevalidatesFundsAfterARealInterveningStorePublication(bool deposit) =>
        WithBank(async f =>
        {
            await f.Login(); uint amount = deposit ? 300U : 200U;
            await StageBank(f, deposit, amount);
            var pending = f.Current;
            long cps = deposit ? 100 : 1000, bank = deposit ? 250 : 100;
            var attributes = new Dictionary<string, string>(pending.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["ConquerPoints"] = cps.ToString(CultureInfo.InvariantCulture),
                ["BankCps"] = bank.ToString(CultureInfo.InvariantCulture),
            };
            // This is an owned fixture setup of a completed intervening debit
            // through the real Store/Frame scalar path. It supplies no bank
            // saver, accepting callback or confirmation authority.
            f.Store.Set(f.Connection, pending with { Character = pending.Character with
                { ConquerPoints = cps, BankConquerPoints = bank, Attributes = attributes } });
            AssertBankTuple(f, cps, bank, amount, deposit);
            var before = f.Current; var files = f.Files(); int first = f.Sent.Count;

            await ConfirmBank(f, deposit);

            Assert.Same(before, f.Current); AssertBankTuple(f, cps, bank, amount, deposit);
            f.CheckFiles(files); AssertNoBankSuccess(f.Sent.Skip(first));
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });

    [Fact]
    public Task BankDepositCapacityFailurePreservesPendingAndDoesNotDebitOrPublishSuccess() =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, true, 1);
            var before = f.Current; var files = f.Files(); int first = f.Sent.Count;

            await ConfirmBank(f, true);

            Assert.Same(before, f.Current); AssertBankTuple(f, 1000, long.MaxValue, 1, deposit: true);
            f.CheckFiles(files); AssertNoBankSuccess(f.Sent.Skip(first));
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        }, bank: long.MaxValue);

    [Fact]
    public Task BankWithdrawalOutsideTheCurrentNativeCpSlotRefusesBeforeWriterOrPendingConsumption() =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, false, 1);
            var before = f.Current; var files = f.Files(); int first = f.Sent.Count;

            await ConfirmBank(f, false);

            Assert.Same(before, f.Current); AssertBankTuple(f, uint.MaxValue, 250, 1);
            f.CheckFiles(files); AssertNoBankSuccess(f.Sent.Skip(first));
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        }, cps: uint.MaxValue);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task PhysicalPinAdmissionLossPreservesPendingAndWalletUntilTheSameOwnerIsRestored(bool deposit) =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, deposit, 100);
            var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
            Assert.True(f.Native.Player.IsCheckedPass); f.Native.Player.IsCheckedPass = false;
            try
            {
                await ConfirmBank(f, deposit);

                Assert.Same(before, f.Current); AssertBankTuple(f, 1000, 250, 100, deposit);
                f.CheckFiles(files); AssertNoBankSuccess(f.Sent.Skip(first));
            }
            finally { f.Native.Player.IsCheckedPass = true; }
            int retry = f.Sent.Count;
            await ConfirmBank(f, deposit);
            AssertBankTuple(f, deposit ? 900 : 1100, deposit ? 350 : 150);
            AssertBankSuccessFrames(f.Sent.Skip(retry).ToArray(), deposit ? 900 : 1100, deposit ? 350 : 150);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EndedOrRecoveryBlockedRealOwnerCannotConsumeBankConfirmation(bool recovery) =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, true, 100);
            var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
            if (recovery) f.Recovery.Block("ownedBankCpsExplicitRecoveryFault");
            else f.Trades.MarkConnectionEnded(f.Connection);

            await ConfirmBank(f, true);

            Assert.Same(before, f.Current); AssertBankTuple(f, 1000, 250, 100, deposit: true);
            f.CheckFiles(files); AssertNoBankSuccess(f.Sent.Skip(first));
        });

    [Theory]
    [InlineData(102177U, (byte)10, true)]
    [InlineData(102177U, (byte)20, true)]
    [InlineData(102177U, (byte)2, true)]
    [InlineData(102177U, (byte)3, true)]
    [InlineData(102177U, (byte)0, false)]
    [InlineData(102177U, (byte)1, false)]
    [InlineData(102177U, (byte)5, false)]
    [InlineData(20499U, (byte)2, false)]
    public void OnlyTheActualBankNpcMutationOptionsOwnTheDispatchOperation(uint npc, byte option, bool owns)
    {
        var frame = BankFrame(LegacyNpcServerRequestPacket.Script(npc, option).ToFrameBytes());
        Assert.Equal(owns, WorldServerFrameHandler.HandlerOwnsOperation(frame, ConquerProtocolProfile.VisibleDx8D13));
    }

    [Fact]
    public Task BankConfirmationReleasesItsActualExclusiveBeforeTheFirstCommittedReply() =>
        WithBank(async f =>
        {
            await f.Login(); await StageBank(f, true, 100);
            bool available = false; int observations = 0; Exception? observed = null;
            Task cleanup = Task.CompletedTask; int first = f.Sent.Count;
            f.OnSend = _ =>
            {
                if (observations++ != 0) return;
                try { AssertBankTuple(f, 900, 350); } catch (Exception error) { observed = error; }
                using var cancellation = new CancellationTokenSource();
                var acquisition = f.Operations.EnterExclusiveAsync(cancellation.Token);
                available = acquisition.IsCompletedSuccessfully;
                if (available) acquisition.Result.Dispose();
                else { cancellation.Cancel(); cleanup = FinishBankLeaseProbe(acquisition); }
            };

            await ConfirmBank(f, true); f.OnSend = null;
            await cleanup.WaitAsync(TimeSpan.FromSeconds(5));

            if (observed is not null) ExceptionDispatchInfo.Capture(observed).Throw();
            Assert.True(available, "Neither outer Frame dispatch nor bank mutation may retain Exclusive during replies.");
            Assert.Equal(5, observations); AssertBankSuccessFrames(f.Sent.Skip(first).ToArray(), 900, 350);
        });

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public Task Actual2255BankRepliesResolveExplicitOrCurrentActiveNpcBeforeCommittingAndReleasingExclusive(
        bool deposit, bool explicitNpc) =>
        WithBank(async f =>
        {
            await f.Login();
            // A real overview click establishes the implicit NPC route. No
            // arbitrary session field supplies the active banker for this case.
            await SendBank(f, 0);
            Assert.Equal(BankNpcUid, f.Current.ActiveNpcId);
            uint npc = explicitNpc ? BankNpcUid : 0;
            var files = f.Files(); int first = f.Sent.Count;
            var input = new LegacyNpcDialogRequestPacket(npc, 0, deposit ? (byte)10 : (byte)20,
                (byte)LegacyNpcReplyInteractType.Input, 0, "100");

            await f.Send(input.ToFrameBytes());

            AssertBankTuple(f, 1000, 250, 100, deposit); f.CheckFiles(files);
            AssertBankStageFrames(f.Sent.Skip(first).ToArray(), deposit, 100);
            var afterCps = deposit ? 900L : 1100L; var afterBank = deposit ? 350L : 150L;
            int replies = 0; bool released = false; Exception? observed = null; Task cleanup = Task.CompletedTask;
            f.OnSend = _ =>
            {
                if (replies++ != 0) return;
                try { AssertBankTuple(f, afterCps, afterBank); } catch (Exception error) { observed = error; }
                using var cancellation = new CancellationTokenSource();
                var acquisition = f.Operations.EnterExclusiveAsync(cancellation.Token);
                released = acquisition.IsCompletedSuccessfully;
                if (released) acquisition.Result.Dispose();
                else { cancellation.Cancel(); cleanup = FinishBankLeaseProbe(acquisition); }
            };
            first = f.Sent.Count;
            var confirm = new LegacyNpcDialogRequestPacket(npc, 0, deposit ? (byte)2 : (byte)3,
                (byte)LegacyNpcReplyInteractType.Option, 0, "");

            await f.Send(confirm.ToFrameBytes()); f.OnSend = null;
            await cleanup.WaitAsync(TimeSpan.FromSeconds(5));

            if (observed is not null) ExceptionDispatchInfo.Capture(observed).Throw();
            Assert.True(released); Assert.Equal(5, replies);
            AssertBankTuple(f, afterCps, afterBank);
            AssertBankSuccessFrames(f.Sent.Skip(first).ToArray(), afterCps, afterBank);
            var committed = f.Current; files = f.Files(); int replay = f.Sent.Count;
            await f.Send(confirm.ToFrameBytes());
            Assert.Same(committed, f.Current); f.CheckFiles(files); AssertNoBankSuccess(f.Sent.Skip(replay));
        });

    [Theory]
    [InlineData(102177U, 0U, (byte)10, true)]
    [InlineData(102177U, 0U, (byte)20, true)]
    [InlineData(102177U, 0U, (byte)2, true)]
    [InlineData(102177U, 0U, (byte)3, true)]
    [InlineData(0U, 102177U, (byte)10, true)]
    [InlineData(0U, 102177U, (byte)20, true)]
    [InlineData(0U, 102177U, (byte)2, true)]
    [InlineData(0U, 102177U, (byte)3, true)]
    [InlineData(0U, 20499U, (byte)2, false)]
    [InlineData(0U, 0U, (byte)2, false)]
    [InlineData(20499U, 102177U, (byte)2, false)]
    [InlineData(102177U, 102177U, (byte)0, false)]
    [InlineData(102177U, 102177U, byte.MaxValue, false)]
    public void Bank2255OperationClassificationUsesOnlyItsExplicitOrActualActiveNpcHint(
        uint packetNpc, uint activeNpc, byte option, bool owns)
    {
        var bytes = new LegacyNpcDialogRequestPacket(packetNpc, 0, option,
            (byte)LegacyNpcReplyInteractType.Option, 0, "").ToFrameBytes();
        Assert.Equal(owns, WorldServerFrameHandler.HandlerOwnsOperation(BankFrame(bytes),
            ConquerProtocolProfile.VisibleDx8D13, activeNpc == 0 ? null : activeNpc));
    }

    private static async Task FinishBankLeaseProbe(ValueTask<IDisposable> acquisition)
    {
        try { (await acquisition).Dispose(); }
        catch (OperationCanceledException) { }
    }
}
