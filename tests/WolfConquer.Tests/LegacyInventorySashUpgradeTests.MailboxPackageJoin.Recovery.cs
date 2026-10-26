using System.Security.Cryptography;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ActualD13FreshPackageOwnerResumesTheSameGenuinePreparedOrAllocatedIntentAfterUidCrash(
        bool allocated, bool stateReplaced) => RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var recipe = CreatePackageJoinRecipe(fixture, join, before);
            var payload = recipe.CopyPayload();
            var request = new LegacyItemUidReservationRequest(recipe.OperationId, LegacyMailboxPackageProducer.Writer,
                recipe.AccountId, recipe.CharacterUid, Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), recipe.Count);
            var interruption = stateReplaced ? LegacyItemUidInterruption.AfterStateReplace : LegacyItemUidInterruption.AfterPendingFlush;
            if (allocated)
                Assert.True(join.Registry.TryPrepareProducerIntent(join.Domain, request, payload, out _, out var preparedReason), preparedReason);
            var completed = allocated
                ? join.Registry.TryReserveProducerIntent(join.Domain, request, payload, interruption, out _, out var reason)
                : join.Registry.TryPrepareProducerIntent(join.Domain, request, payload, interruption, out _, out reason);
            Assert.False(completed);
            Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
            AssertPackageJoinBusinessPreimages(fixture, before);
            Assert.Equal(before.FirstFrame, fixture.Sent.Count);
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            AssertPackageJoinBusinessPreimages(fixture, before);
            Assert.Equal(before.FirstFrame, fixture.Sent.Count);
            await join.RestartAsync();
            Assert.True(join.Registry.TryReadProducerIntent(join.Domain, recipe.OperationId, out var retained, out reason), reason);
            Assert.Equal(payload, retained!.CopyPayload());
            Assert.Equal(allocated, retained.Allocation is not null);
            Assert.Equal(recipe.OperationId, retained.Request.OperationId);
            AssertPackageJoinBusinessPreimages(fixture, before);
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            AssertPackageJoinCommitted(fixture, join, before, claimAll: true, job: 10);
            Assert.Equal(recipe.OperationId, AssertPackageJoinAllocation(fixture, join).Request.OperationId);
        });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public Task ActualD13FreshPackageOwnerReconcilesEveryRealJointJournalPrefixWithoutSecondAllocation(int replacements) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var recipe = CreatePackageJoinRecipe(fixture, join, before);
            var payload = recipe.CopyPayload();
            var request = new LegacyItemUidReservationRequest(recipe.OperationId, LegacyMailboxPackageProducer.Writer,
                recipe.AccountId, recipe.CharacterUid, Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), recipe.Count);
            Assert.True(join.Registry.TryPrepareProducerIntent(join.Domain, request, payload, out _, out var reason), reason);
            Assert.True(join.Registry.TryReserveProducerIntent(join.Domain, request, payload, out _, out reason), reason);
            var intent = AssertPackageJoinAllocation(fixture, join);
            var after = ExpectedPackageJoinUnallocatedFiles(fixture, before, claimAll: true, job: 10);
            after[Path.GetRelativePath(fixture.Root, fixture.ItemPath)] =
                PlayerItemSnapshotLoader.ToLegacyBytes(ExpectedPackageJoinItems(before, intent, claimAll: true));
            var changes = PackageJoinJournalChanges(fixture, before.Files, after);
            var producer = join.ReadDebtProducer();
            Assert.True(producer.TryPrepareJointWork(recipe, intent.Allocation!, changes, out var work, out reason), reason);
            var boundary = replacements switch
            {
                0 => LegacyMailboxPackageInterruption.AfterPendingJournal,
                1 => LegacyMailboxPackageInterruption.AfterCharacter,
                2 => LegacyMailboxPackageInterruption.AfterItems,
                3 => LegacyMailboxPackageInterruption.AfterMailbox,
                4 => LegacyMailboxPackageInterruption.AfterCombatGear,
                _ => throw new ArgumentOutOfRangeException(nameof(replacements)),
            };
            Assert.False(work!.TryCommit(boundary, out var proof, out var durable, out reason));
            Assert.Null(proof);
            Assert.False(durable);
            Assert.Equal("mailboxClaimRecoveryRequired:packageJournal:tradeJournalRecoveryRequired:interrupted", reason);
            Assert.True(LegacyTradeJournal.HasPending(fixture.Users));
            var interrupted = fixture.PackageFiles();
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            AssertPackageJoinFiles(fixture, interrupted);
            Assert.Equal(before.FirstFrame, fixture.Sent.Count);
            await join.RestartAsync();
            AssertPackageJoinCommitted(fixture, join, before, claimAll: true, job: 10);
            var committed = fixture.PackageFiles();
            var firstReplay = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            AssertPackageJoinFiles(fixture, committed);
            AssertPackageJoinReplay(fixture, claimAll: true, firstReplay);
            Assert.Equal(recipe.OperationId, AssertPackageJoinAllocation(fixture, join).Request.OperationId);
        });

    private static LegacyMailboxPackageRecipe CreatePackageJoinRecipe(Fixture fixture, PackageJoin join, PackageJoinBefore before)
        => CreatePackageJoinStackRecipe(fixture, join, before, claimAll: true, job: 10);

    // Negative upgrade fixture only. Normal debt/recovery must use real WMP2.
    private static LegacyMailboxPackageRecipe CreateLegacyPackageJoinRecipe(Fixture fixture, PackageJoin join, PackageJoinBefore before)
    {
        var after = ExpectedPackageJoinUnallocatedFiles(fixture, before, claimAll: true, job: 10);
        var grants = RankOnePackageRows.Select((row, index) =>
        {
            Assert.True(LegacyMailboxPackageCatalog.TryCapture(PackageJoinCatalog(), row.Id, out var metadata, out var reason), reason);
            return new LegacyMailboxPackageItemRecipe(AtomicMailId, (byte)(index < 4 ? 0 : 2), row.Id,
                row.Quantity, 321, 0, 0, 0, metadata!.Digest);
        }).ToArray();
        byte[] Image(Dictionary<string, byte[]> files, string path) => files[Path.GetRelativePath(fixture.Root, path)];
        return LegacyMailboxPackageRecipe.Create(join.Domain, checked((uint)fixture.Account), Fixture.Uid,
            new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimAll, 0, [AtomicMailId]), [fixture.AtomicMailSeed], grants,
            [Image(before.Files, fixture.CharacterPath), Image(before.Files, fixture.ItemPath),
                Image(before.Files, fixture.AtomicMailPath), Image(before.Files, fixture.PackageGearPath)],
            Image(after, fixture.CharacterPath), Image(after, fixture.PackageGearPath), Image(after, fixture.AtomicMailPath));
    }

    private static LegacyTradeJournalChange[] PackageJoinJournalChanges(Fixture fixture,
        Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
    {
        LegacyTradeJournalChange Change(string path, LegacyTradeJournalFileKind kind, long id)
        {
            var relative = Path.GetRelativePath(fixture.Root, path);
            return new(kind, id, before[relative], after[relative]);
        }
        return
        [
            Change(fixture.CharacterPath, LegacyTradeJournalFileKind.Character, fixture.Account),
            Change(fixture.ItemPath, LegacyTradeJournalFileKind.Items, Fixture.Uid),
            Change(fixture.AtomicMailPath, LegacyTradeJournalFileKind.Mailbox, Fixture.Uid),
            Change(fixture.PackageGearPath, LegacyTradeJournalFileKind.CombatGear, Fixture.Uid),
        ];
    }

    private static void AssertPackageJoinBusinessPreimages(Fixture fixture, PackageJoinBefore before)
    {
        foreach (var path in new[] { fixture.CharacterPath, fixture.ItemPath, fixture.AtomicMailPath, fixture.PackageGearPath })
            Assert.Equal(before.Files[Path.GetRelativePath(fixture.Root, path)], File.ReadAllBytes(path));
    }
}
