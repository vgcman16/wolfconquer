using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task CompletedProducerRetirementPreservesOriginalPayloadReceiptReplayAndNeverReissuesTheRange() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var completed = await CompleteUidHistoryPackageAsync(fixture, join, PackageJoinRequest(true));
            var original = completed.Intent;
            var before = fixture.PackageFiles();
            Assert.True(join.Registry.TryArchiveCompletedProducerIntent(join.Domain, original.Request.OperationId,
                completed.Proof, out var reason), reason);
            AssertUidHistoryOnlyChanges(fixture, before, original.Request.OperationId);
            Assert.True(join.Registry.TryReadActiveProducerIntents(join.Domain, out var active, out reason), reason);
            Assert.Empty(active);
            Assert.True(join.Registry.TryReadProducerIntents(join.Domain, out var history, out reason), reason);
            AssertUidHistorySnapshot(original, Assert.Single(history));
            Assert.True(join.Registry.TryFindProducerIntent(join.Domain, original.Request.OperationId, out var found, out var exists, out reason), reason);
            Assert.True(exists);
            AssertUidHistorySnapshot(original, Assert.IsType<LegacyItemUidProducerIntentSnapshot>(found));
            Assert.True(join.Registry.TryFindProducerIntent(join.Domain, Guid.NewGuid(), out found, out exists, out reason), reason);
            Assert.False(exists);
            Assert.Null(found);
            var archivedFiles = fixture.PackageFiles();
            Assert.True(join.Registry.TryPrepareProducerIntent(join.Domain, original.Request, original.CopyPayload(), out var replay, out reason), reason);
            AssertUidHistorySnapshot(original, replay!);
            Assert.True(join.Registry.TryReserveProducerIntent(join.Domain, original.Request, original.CopyPayload(), out replay, out reason), reason);
            AssertUidHistorySnapshot(original, replay!);
            Assert.False(join.Registry.TryPrepareProducerIntent(join.Domain,
                original.Request with { Count = original.Request.Count + 1 }, original.CopyPayload(), out replay, out _));
            Assert.Null(replay);
            Assert.Equal(LegacyItemUidReservationOutcome.Rejected, join.Registry.ReserveForOperation(join.Domain,
                original.Request, out var token, out var receipt, out reason));
            Assert.Equal("itemUidProducerIntentOwnsOperation", reason);
            Assert.Null(token);
            Assert.Null(receipt);
            Assert.True(join.Registry.TryArchiveCompletedProducerIntent(join.Domain, original.Request.OperationId,
                completed.Proof, out reason), reason);
            AssertPackageJoinFiles(fixture, archivedFiles);
            Assert.True(join.Registry.TryReserve(2, out token, out reason), reason);
            Assert.True(token!.TryTake(out var ids));
            Assert.Equal(new[] { original.Allocation!.First + (uint)original.Allocation.Count,
                original.Allocation.First + (uint)original.Allocation.Count + 1 }, ids);
        });

    [Fact]
    public Task CompletedRetirementCanRemoveANonprefixPairAndLaterAppendInV4WithoutLosingOlderOwnership() =>
        RunStackPackageAsync(fixture =>
        {
            fixture.AddStackSecondPackageMail();
            var mailbox = new LegacyMailboxStore(fixture.Root);
            mailbox.Add(Fixture.Uid, new LegacyMailboxEntry { Id = 25, Flag = 1, Title = "Third owned package",
                SenderName = "System", Content = "V4 append control", Timer = fixture.AtomicMailSeed.Timer });
            mailbox.Save(Fixture.Uid);
        }, async (fixture, join) =>
        {
            var first = await CompleteUidHistoryPackageAsync(fixture, join, PackageJoinRequest(false));
            var second = await CompleteUidHistoryPackageAsync(fixture, join,
                new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimRecordType, StackSecondMailId, []).ToFrameBytes());
            Assert.True(second.Intent.Allocation!.First > first.Intent.Allocation!.First);
            Assert.True(join.Registry.TryArchiveCompletedProducerIntent(join.Domain, second.Intent.Request.OperationId, second.Proof, out var reason), reason);
            Assert.True(join.Registry.TryReadActiveProducerIntents(join.Domain, out var active, out reason), reason);
            AssertUidHistorySnapshot(first.Intent, Assert.Single(active));
            var third = await CompleteUidHistoryPackageAsync(fixture, join,
                new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimRecordType, 25, []).ToFrameBytes());
            Assert.Equal(4, JsonNode.Parse(File.ReadAllBytes(fixture.PackageUidState))!["Version"]!.GetValue<int>());
            Assert.True(third.Intent.Allocation!.First > second.Intent.Allocation.First);
            Assert.True(join.Registry.TryArchiveCompletedProducerIntent(join.Domain, first.Intent.Request.OperationId, first.Proof, out reason), reason);
            Assert.True(join.Registry.TryArchiveCompletedProducerIntent(join.Domain, third.Intent.Request.OperationId, third.Proof, out reason), reason);
            Assert.True(join.Registry.TryReadActiveProducerIntents(join.Domain, out active, out reason), reason);
            Assert.Empty(active);
            Assert.True(join.Registry.TryReadProducerIntents(join.Domain, out var history, out reason), reason);
            Assert.Equal(3, history.Length);
            foreach (var original in new[] { first, second, third })
                AssertUidHistorySnapshot(original.Intent, Assert.Single(history, row => row.Request.OperationId == original.Intent.Request.OperationId));
            var files = fixture.PackageFiles();
            await join.RestartAsync();
            AssertPackageJoinFiles(fixture, files);
            Assert.True(join.Registry.TryReadProducerIntents(join.Domain, out history, out reason), reason);
            Assert.Equal(3, history.Length);
        }, StackPackageCatalog(3_330_770, 10));
}
