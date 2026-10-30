using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedItemRepairTests
{
    private static Task WithWarehouseRepair(bool meteor, Func<Fixture, Task> body) =>
        WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, body,
            characterLines: ["WHMoney=555", "WarehouseMoney=555"],
            playerItems: Saved(meteor), additionalItems: Catalog());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RealRepairKeepsSavedAndNativeWarehouseGoldCoherentAtItsFirstReplyWithoutAdditionalFrames(bool meteor) =>
        WithWarehouseRepair(meteor, async f =>
        {
            await f.Login(); Assert.Equal(555L, f.Native.Player.WHMoney);
            int first = f.Sent.Count, replies = 0; Exception? observed = null;
            f.OnSend = sentFrame =>
            {
                replies++;
                try
                {
                    Assert.Equal(555L, f.Native.Player.WHMoney);
                    Assert.Equal("555", f.Current.Character.Attributes["WHMoney"]);
                    Assert.Equal("555", f.Current.Character.Attributes["WarehouseMoney"]);
                    var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
                        File.ReadAllBytes(f.CharacterPath), out _));
                    Assert.Equal("555", saved.Attributes["WHMoney"]);
                    Assert.Equal("555", saved.Attributes["WarehouseMoney"]);
                    Assert.Equal(meteor ? 1000L : 943L, saved.Money);
                }
                catch (Exception error) { observed ??= error; }
            };

            await f.Send(Request()); f.OnSend = null;

            if (observed is not null) ExceptionDispatchInfo.Capture(observed).Throw();
            Assert.Equal(meteor ? 6 : 3, replies); Assert.Equal(meteor ? 6 : 3, f.Sent.Count - first);
            Assert.Equal(555L, f.Native.Player.WHMoney);
            Assert.DoesNotContain(f.Sent.Skip(first), bytes => LegacyMsgUpdatePacket.TryParse(Frame(bytes), out var update) &&
                update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.WarehouseMoney));
            if (!meteor) AssertPaidFrames(f.Sent.Skip(first).ToArray());
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });

    [Theory]
    [InlineData(556L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    public Task WarehouseRawDriftRefusesTheGenuineRepairBeforeItsWriterWithoutCostJournalOrOutput(long raw) =>
        WithWarehouseRepair(false, async f =>
        {
            await f.Login(); var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
            f.Native.Player.WHMoney = raw;
            try
            {
                await f.Send(Request());

                Assert.Same(before, f.Current); Assert.Equal(raw, f.Native.Player.WHMoney);
                Assert.Equal(1000L, f.Native.Player.Money); Assert.Equal((ushort)5, RepairedTarget(f.Current).Durability);
                Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
            }
            finally { f.Native.Player.WHMoney = 555; }
        });

    [Theory]
    [InlineData(556L)]
    [InlineData(-1L)]
    public Task WarehouseDriftAfterTheActualRepairJournalRetiresWithoutRefundOrSuccess(long raw) =>
        WithWarehouseRepair(false, async f =>
        {
            await f.Login(); var before = f.Current; int first = f.Sent.Count;
            var proposal = PaidProposal(before);
            Assert.True(LegacyItemRepairPersistence.TryPrepare(before, proposal, f.Items, Fixture.ItemUid,
                LegacyItemRepairPayment.Money, out var write, out var reason), reason);
            Assert.NotNull(write); LegacyItemRepairOutcome? actual = null;
            bool ActualWriterThenOwnedRawFault()
            {
                actual = write!.TryCommit(out reason);
                if (actual == LegacyItemRepairOutcome.Committed) f.Native.Player.WHMoney = raw;
                return actual == LegacyItemRepairOutcome.Committed;
            }

            Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
                f.Store.TryCommitSession(f.Connection, before, proposal, ActualWriterThenOwnedRawFault));

            Assert.Equal(LegacyItemRepairOutcome.Committed, actual);
            Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.True(f.Recovery.IsBlocked);
            Assert.True(f.Native.OrdinarySpellBinding!.IsRetired); Assert.Equal(raw, f.Native.Player.WHMoney);
            Assert.Equal(1000L, f.Native.Player.Money); // Durable success was deliberately not published after failed proof.
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
            Assert.Equal(943L, saved.Money); Assert.Equal("555", saved.Attributes["WHMoney"]);
            Assert.Equal("555", saved.Attributes["WarehouseMoney"]);
            Assert.Equal((ushort)10, Assert.Single(new PlayerItemSnapshotLoader(f.Items)
                .LoadByCharacterUid(Fixture.Uid).InventoryItems).Durability);
            Assert.False(LegacyTradeJournal.HasPending(f.Users)); Assert.Equal(first, f.Sent.Count);
        });
}
