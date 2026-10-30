using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedItemRepairTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualStoreAndInterruptedRepairWalFenceTheRealHolderWithoutPublishingPreparedNativeItems(bool meteor)
    {
        await WithRepair(meteor, false, async f =>
        {
            await f.Login(); var before = f.Current; var binding = AssertGraph(f);
            var nativeTarget = f.Native.Inventory!.ClientItems[RepairedTarget(before).Uid];
            var proposed = meteor ? before with { PlayerItems = ExpectedItems(before, true) } : PaidProposal(before);
            Assert.True(LegacyItemRepairPersistence.TryPrepare(before, proposed, f.Items, RepairedTarget(before).Uid,
                meteor ? LegacyItemRepairPayment.Meteors : LegacyItemRepairPayment.Money, out var write, out var reason), reason);
            var prepared = Assert.IsType<LegacyItemRepairPersistence>(write);
            var outcome = LegacyItemRepairOutcome.Rejected;
            byte[] character = ExpectedCharacter(File.ReadAllBytes(f.CharacterPath), meteor);
            byte[] items = File.ReadAllBytes(f.ItemsPath); int first = f.Sent.Count;
            bool PersistActualInterruptedImages()
            {
                outcome = prepared.TryCommit(out reason, LegacyItemRepairJournalInterruption.AfterCharacter);
                if (outcome == LegacyItemRepairOutcome.RecoveryRequired) throw new LegacyCommittedSpellPublicationException();
                return outcome == LegacyItemRepairOutcome.Committed;
            }

            Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
                f.Store.TryCommitSession(f.Connection, before, proposed, PersistActualInterruptedImages));

            Assert.Equal(LegacyItemRepairOutcome.RecoveryRequired, outcome);
            Assert.True(LegacyTradeJournal.HasPending(f.Users)); Assert.True(f.Recovery.IsBlocked);
            Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.True(binding.IsRetired);
            Assert.False(binding.Items.PhysicalOwnerIsCurrentPlain());
            Assert.Equal(meteor ? (ushort)0 : (ushort)5, nativeTarget.Durability);
            Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(items, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(first, f.Sent.Count);
            // No live-owner replay: the raw persistence suite owns fresh recovery.
            var held = f.Files(); await f.Send(Request()); f.CheckFiles(held); Assert.Equal(first, f.Sent.Count);
        });
    }

    [Fact]
    public async Task ActualRepairFilesStayCommittedWhenPhysicalOwnerIsLostBeforeStorePublication()
    {
        await WithRepair(false, false, async f =>
        {
            await f.Login(); var before = f.Current; var binding = AssertGraph(f);
            var nativeTarget = f.Native.Inventory!.ClientItems[RepairedTarget(before).Uid];
            var proposed = PaidProposal(before);
            Assert.True(LegacyItemRepairPersistence.TryPrepare(before, proposed, f.Items, nativeTarget.Uid,
                LegacyItemRepairPayment.Money, out var write, out var reason), reason);
            var prepared = Assert.IsType<LegacyItemRepairPersistence>(write);
            byte[] character = ExpectedCharacter(File.ReadAllBytes(f.CharacterPath), meteor: false);
            byte[] items = PlayerItemSnapshotLoader.ToLegacyBytes(proposed.PlayerItems);
            var outcome = LegacyItemRepairOutcome.Rejected; int first = f.Sent.Count;
            bool PersistThenLoseActualOwner()
            {
                outcome = prepared.TryCommit(out reason);
                if (outcome == LegacyItemRepairOutcome.Committed) f.Native.Player = new LegacyNativePlayerRuntime(f.Native);
                if (outcome == LegacyItemRepairOutcome.RecoveryRequired) throw new LegacyCommittedSpellPublicationException();
                return outcome == LegacyItemRepairOutcome.Committed;
            }

            Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
                f.Store.TryCommitSession(f.Connection, before, proposed, PersistThenLoseActualOwner));

            Assert.Equal(LegacyItemRepairOutcome.Committed, outcome); Assert.Empty(reason);
            Assert.True(f.Recovery.IsBlocked); Assert.True(binding.IsRetired);
            Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.False(binding.Items.PhysicalOwnerIsCurrentPlain());
            Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(items, File.ReadAllBytes(f.ItemsPath));
            Assert.False(LegacyTradeJournal.HasPending(f.Users)); Assert.Equal(first, f.Sent.Count);
            Assert.Equal((ushort)5, nativeTarget.Durability); // Detached old owner was not falsely published.
        });
    }
}
