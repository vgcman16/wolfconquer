using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedItemRepairTests
{
    private static readonly FieldInfo NativeInventoryCpsSlot = typeof(LegacyNativePlayerRuntime)
        .GetField("_cps", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo NativeBoundCpsSlot = typeof(LegacyNativePlayerRuntime)
        .GetField("_bountCps", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static (uint Cps, int Bound, ulong Bank) RepairCps(Fixture f) =>
        (f.Native.Player.ConquerPoints, f.Native.Player.BoundConquerPoints, f.Native.ConquerPointsSave);

    private static void SetOwnedCpsRawFault(Fixture f, string slot, bool restore)
    {
        switch (slot)
        {
            case "cp": NativeInventoryCpsSlot.SetValue(f.Native.Player, restore ? 200U : 201U); break;
            case "bound": NativeBoundCpsSlot.SetValue(f.Native.Player, restore ? 300 : 301); break;
            case "bank": f.Native.ConquerPointsSave = restore ? 123UL : 124UL; break;
            default: throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualRepairRetainsTheCompleteNativeCpTupleAtItsFirstReply(bool meteor) =>
        WithRepair(meteor, false, async f =>
        {
            await f.Login(); Assert.Equal((200U, 300, 123UL), RepairCps(f));
            Exception? observed = null; int replies = 0;
            f.OnSend = sentFrame =>
            {
                replies++;
                try
                {
                    Assert.Equal((200U, 300, 123UL), RepairCps(f));
                    Assert.Equal((200L, 300L, 123L), (f.Current.Character.ConquerPoints,
                        f.Current.Character.BoundConquerPoints, f.Current.Character.BankConquerPoints));
                }
                catch (Exception error) { observed ??= error; }
            };

            await f.Send(Request()); f.OnSend = null;

            if (observed is not null) ExceptionDispatchInfo.Capture(observed).Throw();
            Assert.Equal(meteor ? 6 : 3, replies);
            Assert.Equal((200U, 300, 123UL), RepairCps(f));
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
            Assert.Equal((200L, 300L, 123L), (saved.ConquerPoints, saved.BoundConquerPoints, saved.BankConquerPoints));
        });

    [Theory]
    [InlineData("cp")]
    [InlineData("bound")]
    [InlineData("bank")]
    public Task NativeCpTupleDriftRefusesTheRealRepairBeforeAnyJournalOrCost(string slot) =>
        WithRepair(false, false, async f =>
        {
            await f.Login(); var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
            SetOwnedCpsRawFault(f, slot, restore: false); var corrupt = RepairCps(f);
            try
            {
                await f.Send(Request());

                Assert.Same(before, f.Current); Assert.Equal(corrupt, RepairCps(f));
                Assert.Equal(1000L, before.Character.Money); Assert.Equal((ushort)5, RepairedTarget(f.Current).Durability);
                Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
            }
            finally { SetOwnedCpsRawFault(f, slot, restore: true); }
        });

    [Theory]
    [InlineData("cp")]
    [InlineData("bound")]
    [InlineData("bank")]
    public Task NativeCpTupleDriftAfterTheGenuineRepairWriterRetiresWithoutRefundOrSuccess(string slot) =>
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
                if (actual == LegacyItemRepairOutcome.Committed) SetOwnedCpsRawFault(f, slot, restore: false);
                return actual == LegacyItemRepairOutcome.Committed;
            }

            Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
                f.Store.TryCommitSession(f.Connection, before, proposal, ActualWriterThenOwnedRawFault));

            Assert.Equal(LegacyItemRepairOutcome.Committed, actual);
            Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.True(f.Recovery.IsBlocked);
            Assert.True(f.Native.OrdinarySpellBinding!.IsRetired);
            Assert.NotEqual((200U, 300, 123UL), RepairCps(f));
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
            Assert.Equal(943L, saved.Money);
            Assert.Equal((200L, 300L, 123L), (saved.ConquerPoints, saved.BoundConquerPoints, saved.BankConquerPoints));
            Assert.Equal((ushort)10, Assert.Single(new PlayerItemSnapshotLoader(f.Items).LoadByCharacterUid(Fixture.Uid).InventoryItems).Durability);
            Assert.False(LegacyTradeJournal.HasPending(f.Users)); Assert.Equal(first, f.Sent.Count);
        });
}
