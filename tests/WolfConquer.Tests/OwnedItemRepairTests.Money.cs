using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedItemRepairTests
{
    private static readonly FieldInfo NativeMoneySlot = typeof(LegacyNativePlayerRuntime)
        .GetField("_Money", BindingFlags.NonPublic | BindingFlags.Instance)!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SuccessfulRepairPublishesTheActualNativeMoneyAtFirstReplyWithoutExtraFrames(bool meteor) =>
        WithRepair(meteor, false, async f =>
        {
            await f.Login();
            Assert.Equal(1000L, f.Native.Player.Money);
            Exception? observed = null; int calls = 0, first = f.Sent.Count;
            f.OnSend = sentFrame =>
            {
                calls++;
                try
                {
                    Assert.Equal(meteor ? 1000L : 943L, f.Native.Player.Money);
                    Assert.Equal(f.Current.Character.Money, f.Native.Player.Money);
                    var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
                        File.ReadAllBytes(f.CharacterPath), out _));
                    Assert.Equal(f.Native.Player.Money, saved.Money);
                }
                catch (Exception error) { observed ??= error; }
            };

            await f.Send(Request()); f.OnSend = null;

            if (observed is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(observed).Throw();
            Assert.Equal(meteor ? 6 : 3, calls);
            Assert.Equal(meteor ? 6 : 3, f.Sent.Count - first);
            Assert.Equal(meteor ? 1000L : 943L, f.Native.Player.Money);
            if (!meteor) AssertPaidFrames(f.Sent.Skip(first).ToArray());
        });

    [Fact]
    public Task InsufficientRepairFundsLeaveActualNativeMoneyAndAllOwnedFilesUntouched() =>
        WithRepair(false, false, async f =>
        {
            await f.Login(); var before = f.Current; var files = f.Files(); int first = f.Sent.Count;

            await f.Send(Request());

            Assert.Same(before, f.Current); Assert.Equal(1000L, f.Native.Player.Money);
            Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        }, goldWorth: 1_000_000);

    [Fact]
    public Task OwnedNativeMoneyDriftRefusesRepairBeforeItsRealJournalWriter() =>
        WithRepair(false, false, async f =>
        {
            await f.Login(); var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
            NativeMoneySlot.SetValue(f.Native.Player, 999L); // Corrupt only this owned genuine native slot.
            try
            {
                await f.Send(Request());

                Assert.Same(before, f.Current); Assert.Equal(999L, f.Native.Player.Money);
                Assert.Equal(1000L, before.Character.Money); Assert.Equal((ushort)5, RepairedTarget(f.Current).Durability);
                Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
            }
            finally { NativeMoneySlot.SetValue(f.Native.Player, 1000L); }
        });

    [Fact]
    public Task MoneyDriftAfterTheActualRepairJournalRetiresPublicationWithoutRefundOrSuccess() =>
        WithRepair(false, false, async f =>
        {
            await f.Login(); var before = f.Current; int first = f.Sent.Count;
            var proposal = PaidProposal(before);
            Assert.True(LegacyItemRepairPersistence.TryPrepare(before, proposal, f.Items, Fixture.ItemUid,
                LegacyItemRepairPayment.Money, out var write, out var reason), reason);
            Assert.NotNull(write);
            LegacyItemRepairOutcome? actual = null;
            bool ActualWriterThenOwnedRawFault()
            {
                actual = write!.TryCommit(out reason);
                if (actual == LegacyItemRepairOutcome.Committed) NativeMoneySlot.SetValue(f.Native.Player, 999L);
                return actual == LegacyItemRepairOutcome.Committed;
            }

            Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
                f.Store.TryCommitSession(f.Connection, before, proposal, ActualWriterThenOwnedRawFault));

            Assert.Equal(LegacyItemRepairOutcome.Committed, actual);
            Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.True(f.Recovery.IsBlocked);
            Assert.True(f.Native.OrdinarySpellBinding!.IsRetired); Assert.Equal(999L, f.Native.Player.Money);
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
            Assert.Equal(943L, saved.Money);
            Assert.Equal((ushort)10, Assert.Single(new PlayerItemSnapshotLoader(f.Items).LoadByCharacterUid(Fixture.Uid).InventoryItems).Durability);
            Assert.False(LegacyTradeJournal.HasPending(f.Users)); Assert.Equal(first, f.Sent.Count);
        });
}
