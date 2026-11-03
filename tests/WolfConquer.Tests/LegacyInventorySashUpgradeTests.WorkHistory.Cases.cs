using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public async Task RegisteredPackageCompletedHistoryExceeds256WithoutAllocatingDummyUidsAndRetainsOldReplayAcrossRestart()
    {
        using var timing = FixturePhaseTiming.Create(nameof(RegisteredPackageCompletedHistoryExceeds256WithoutAllocatingDummyUidsAndRetainsOldReplayAcrossRestart));
        WorldDataCatalog catalog;
        using (timing?.Measure("catalog-create")) catalog = HistoryLargeStackCatalog();
        await RunStackPackageAsync(fixture => SeedHistoryMails(fixture, 300), async (fixture, join) =>
        {
            PackageJoinBefore before;
            LegacyMailboxEntry[] mails;
            byte[] uidBytes;
            using (timing?.Measure("initial-snapshot-and-mail-read"))
            {
                before = CapturePackageJoin(fixture, join);
                mails = new LegacyMailboxStore(fixture.Root).Load(Fixture.Uid)
                    .Where(mail => mail.Flag == 1).OrderBy(mail => mail.Id).ToArray();
                Assert.Equal(300, mails.Length);
                uidBytes = File.ReadAllBytes(fixture.PackageUidState);
            }
            foreach (var mail in mails)
            {
                using (timing?.Measure("claim-frame"))
                    await join.FrameAsync(new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimRecordType, mail.Id, []).ToFrameBytes());
                using (timing?.Measure("claim-debt-readiness"))
                    Assert.True(join.ReadDebtProducer().TryRequireAccountWritable(fixture.Account, out var reason), reason);
            }
            Dictionary<string, byte[]> expected;
            using (timing?.Measure("verify-business-items-and-uid"))
            {
                var expectedItems = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems, claims: 300));
                expected = ExpectedStackBusinessFiles(fixture, before, expectedItems, mails, false, 300_031);
                AssertHistoryBusinessFiles(fixture, expected);
                Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(join.Current.PlayerItems));
                Assert.Equal("300031", join.Current.Character.Attributes["ClassExperience"]);
                Assert.Equal(uidBytes, File.ReadAllBytes(fixture.PackageUidState));
                AssertNoPackageJoinIntents(join);
            }
            using (timing?.Measure("verify-history-metadata"))
                IncludeValidatedHistoryMetadata(fixture, join, expected, 300, 0);
            Dictionary<string, byte[]> files;
            using (timing?.Measure("verify-files-chain-and-snapshot"))
            {
                AssertPackageJoinFiles(fixture, expected);
                AssertHistoryChain(fixture, join, 300, 0);
                files = fixture.PackageFiles();
            }
            using (timing?.Measure("first-replay-frame"))
                await join.FrameAsync(PackageJoinRequest(false));
            using (timing?.Measure("first-replay-file-check"))
                AssertPackageJoinFiles(fixture, files);
            using (timing?.Measure("restart-owner-close-open"))
                await join.RestartAsync();
            using (timing?.Measure("post-restart-files-uid-and-chain"))
            {
                AssertPackageJoinFiles(fixture, files);
                Assert.Equal(uidBytes, File.ReadAllBytes(fixture.PackageUidState));
                AssertHistoryChain(fixture, join, 300, 0);
            }
            var replayStart = fixture.Sent.Count;
            using (timing?.Measure("post-restart-replay-frame"))
                await join.FrameAsync(PackageJoinRequest(false));
            using (timing?.Measure("post-restart-replay-verification"))
            {
                AssertPackageJoinFiles(fixture, files);
                AssertPackageJoinReplay(fixture, false, replayStart);
            }
            using (timing?.Measure("final-debt-readiness"))
                Assert.True(join.ReadDebtProducer().TryRequireAccountWritable(fixture.Account, out var ready), ready);
        }, catalog, timing);
    }

    [Fact]
    public Task RegisteredAllocatingPackageReclaimsOnlyGenuinelyCompletedUidPairsUnderActivePressure() =>
        RunStackPackageAsync(fixture => fixture.AddStackSecondPackageMail(), async (fixture, join) =>
        {
            await join.FrameAsync(PackageJoinRequest(false));
            Assert.True(join.Registry.TryReadActiveProducerIntents(join.Domain, out var firstRows, out var reason), reason);
            var first = Assert.Single(firstRows);
            Assert.NotNull(first.Allocation);
            for (var index = 0; index < 63; index++)
            {
                var payload = BitConverter.GetBytes(index);
                var request = new LegacyItemUidReservationRequest(Guid.NewGuid(), "owned-history-pressure", checked((uint)fixture.Account),
                    Fixture.Uid, HistoryDigest(payload), 1);
                Assert.True(join.Registry.TryPrepareProducerIntent(join.Domain, request, payload, out var prepared, out reason), reason);
                Assert.Null(prepared!.Allocation); // Genuine unrelated pending intent; never a game reward or completion owner.
            }
            Assert.True(join.Registry.TryReadActiveProducerIntents(join.Domain, out var pressured, out reason), reason);
            Assert.Equal(64, pressured.Length);
            await join.FrameAsync(new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimRecordType, StackSecondMailId, []).ToFrameBytes());
            Assert.Equal("2031", join.Current.Character.Attributes["ClassExperience"]);
            Assert.True(join.Registry.TryReadActiveProducerIntents(join.Domain, out var active, out reason), reason);
            Assert.Equal(64, active.Length);
            Assert.DoesNotContain(active, row => row.Request.OperationId == first.Request.OperationId);
            Assert.Equal(63, active.Count(row => row.Request.Writer == "owned-history-pressure"));
            var second = Assert.Single(active, row => row.Request.Writer == LegacyMailboxPackageProducer.Writer);
            Assert.Equal(1, second.Allocation!.Count);
            Assert.Equal(first.Allocation!.First + (uint)first.Allocation.Count, second.Allocation.First);
            Assert.True(join.Registry.TryReadProducerIntent(join.Domain, first.Request.OperationId, out var archived, out reason), reason);
            Assert.Equal(first.Request, archived!.Request);
            Assert.Equal(first.Allocation, archived.Allocation);
            Assert.Equal(first.CopyPayload(), archived.CopyPayload());
            var history = AssertHistoryChain(fixture, join, 2, 0);
            Assert.Equal(2, history.Count);
            var files = fixture.PackageFiles();
            Assert.True(join.Registry.TryReserveProducerIntent(join.Domain, first.Request, first.CopyPayload(), out var replay, out reason), reason);
            Assert.Equal(first.Allocation, replay!.Allocation);
            AssertPackageJoinFiles(fixture, files);
            await join.RestartAsync();
            AssertPackageJoinFiles(fixture, files);
            Assert.True(join.ReadDebtProducer().TryRequireAccountWritable(fixture.Account, out reason), reason);
        }, StackPackageCatalog(3_330_770, 10));
}
