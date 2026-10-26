using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task RegisteredPackageCompletedHistoryExceeds256WithoutAllocatingDummyUidsAndRetainsOldReplayAcrossRestart() =>
        RunStackPackageAsync(fixture => SeedHistoryMails(fixture, 300), async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var mails = new LegacyMailboxStore(fixture.Root).Load(Fixture.Uid)
                .Where(mail => mail.Flag == 1).OrderBy(mail => mail.Id).ToArray();
            Assert.Equal(300, mails.Length);
            var uidBytes = File.ReadAllBytes(fixture.PackageUidState);
            foreach (var mail in mails)
            {
                await join.FrameAsync(new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimRecordType, mail.Id, []).ToFrameBytes());
                Assert.True(join.ReadDebtProducer().TryRequireAccountWritable(fixture.Account, out var reason), reason);
            }
            var expectedItems = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems, claims: 300));
            var expected = ExpectedStackBusinessFiles(fixture, before, expectedItems, mails, false, 300_031);
            AssertHistoryBusinessFiles(fixture, expected);
            Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(join.Current.PlayerItems));
            Assert.Equal("300031", join.Current.Character.Attributes["ClassExperience"]);
            Assert.Equal(uidBytes, File.ReadAllBytes(fixture.PackageUidState));
            AssertNoPackageJoinIntents(join);
            IncludeValidatedHistoryMetadata(fixture, join, expected, 300, 0);
            AssertPackageJoinFiles(fixture, expected);
            AssertHistoryChain(fixture, join, 300, 0);
            var files = fixture.PackageFiles();
            await join.FrameAsync(PackageJoinRequest(false));
            AssertPackageJoinFiles(fixture, files);
            await join.RestartAsync();
            AssertPackageJoinFiles(fixture, files);
            Assert.Equal(uidBytes, File.ReadAllBytes(fixture.PackageUidState));
            AssertHistoryChain(fixture, join, 300, 0);
            var replayStart = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(false));
            AssertPackageJoinFiles(fixture, files);
            AssertPackageJoinReplay(fixture, false, replayStart);
            Assert.True(join.ReadDebtProducer().TryRequireAccountWritable(fixture.Account, out var ready), ready);
        }, HistoryLargeStackCatalog());

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
