using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("items")] [InlineData("gear")] [InlineData("mailbox")] [InlineData("character")]
    public Task CommittedNoAllocationWorkKeepsRealWritersFencedUntilValidatedStoreAndCachePublication(string writer) =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var expected = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems));
            var prepared = PrepareStackNoAllocationWork(fixture, join, before, expected);
            var producer = join.ReadDebtProducer();
            var mailboxes = Assert.IsType<LegacyMailboxStore>(ActualDebtContext(fixture, join).Mailboxes);
            lock (mailboxes.SyncRoot)
            {
                mailboxes.Load(Fixture.Uid);
                Assert.True(mailboxes.TryCaptureClaims(Fixture.Uid,
                    before.Files[Path.GetRelativePath(fixture.Root, fixture.AtomicMailPath)], out var capture, out var reason), reason);
                var projection = capture.ProjectPackages(new Dictionary<uint, LegacyMailboxEntry>
                { [AtomicMailId] = fixture.AtomicMailSeed.WithClearedFlag() });
                Assert.Equal(prepared.After[Path.GetRelativePath(fixture.Root, fixture.AtomicMailPath)], projection.Image);
                Assert.True(prepared.Work.TryCommit(out var proof, out var durable, out reason), reason);
                Assert.True(durable);
                Assert.NotNull(proof);
                AssertNoPackageJoinIntents(join);
                AssertHistoryChain(fixture, join, 1, 0); // No active UID intent and no pending v2 row remain.
                Assert.Same(before.Owner, join.Current); // The real Store holder is still the old unpublished owner.
                var committed = fixture.PackageFiles();
                var first = fixture.Sent.Count;
                if (writer == "character")
                {
                    Assert.False(TryWritePublicationCharacter(fixture, join, out reason));
                    Assert.NotEmpty(reason);
                }
                else Assert.Throws<IOException>(() => WritePublicationUidSnapshot(fixture, writer));
                Assert.False(producer.TryRequireCharacterWritable(Fixture.Uid, out reason));
                Assert.True(producer.HasPendingAccountWork(fixture.Account, out var pending, out reason), reason);
                Assert.True(pending);
                AssertPackageJoinFiles(fixture, committed);
                Assert.Equal(first, fixture.Sent.Count);
                Assert.True(join.SessionStore.TryCommitPreparedSession(join.Connection!, before.Owner, producer, nativeItems =>
                {
                    // This recovery control intentionally committed its real
                    // work above. Prepare from those actual saved postimages
                    // before Store/cache completion, never replay the writer.
                    var candidate = LoadDebtRecoveryCandidate(fixture, before.Owner);
                    nativeItems.ValidateItemIds(candidate.PlayerItems.InventoryItems.Select(item => item.ItemId));
                    var final = nativeItems.Prepare(candidate);
                    nativeItems.ArmBeforeCommit();
                    // Publish the genuine cache projection and complete saved
                    // loader candidate under the actual Store holder/gate. The
                    // real producer then verifies all committed files/session
                    // postimages before clearing its awaiting identity.
                    mailboxes.PublishClaims(capture, projection);
                    return final;
                }));
                Assert.False(ReferenceEquals(before.Owner, join.Current));
                Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(join.Current.PlayerItems));
                Assert.True(producer.TryRequireCharacterWritable(Fixture.Uid, out reason), reason);
                Assert.True(producer.TryRequireAccountWritable(fixture.Account, out reason), reason);
                if (writer == "character") Assert.True(TryWritePublicationCharacter(fixture, join, out reason), reason);
                else WritePublicationUidSnapshot(fixture, writer);
                AssertPackageJoinFiles(fixture, committed);
                Assert.Equal(first, fixture.Sent.Count);
                Assert.Equal(0U, Assert.Single(mailboxes.List(Fixture.Uid), row => row.Id == AtomicMailId).Flag);
            }
            return Task.CompletedTask;
        });

    [Fact]
    public Task MissingRetainedAwaitingWorkRefusesUidWritersRatherThanTreatingCompletedHistoryAsAbsent() =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var expected = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems));
            var prepared = PrepareStackNoAllocationWork(fixture, join, before, expected);
            Assert.True(prepared.Work.TryCommit(out _, out var durable, out var reason), reason);
            Assert.True(durable);
            File.Delete(HistoryCompletedPath(fixture, join, prepared.Recipe.OperationId.ToString("N")));
            var files = fixture.PackageFiles();
            Assert.False(join.ReadDebtProducer().TryRequireCharacterWritable(Fixture.Uid, out reason));
            Assert.StartsWith("mailboxClaimRecoveryRequired:", reason);
            Assert.Throws<IOException>(() => WritePublicationUidSnapshot(fixture, "items"));
            AssertPackageJoinFiles(fixture, files);
            Assert.Same(before.Owner, join.Current);
            return Task.CompletedTask;
        });

    private static void WritePublicationUidSnapshot(Fixture fixture, string writer)
    {
        switch (writer)
        {
            case "items":
                var items = new PlayerItemSnapshotLoader(fixture.Items);
                items.SaveByCharacterUid(Fixture.Uid, items.LoadByCharacterUid(Fixture.Uid)); break;
            case "gear":
                var gear = new CombatGearSnapshotLoader(fixture.PackageGearRoot);
                gear.SaveByCharacterUid(Fixture.Uid, gear.LoadByCharacterUid(Fixture.Uid)); break;
            case "mailbox":
                var mailbox = new LegacyMailboxStore(fixture.Root);
                mailbox.Load(Fixture.Uid);
                mailbox.Save(Fixture.Uid); break;
            default: throw new ArgumentOutOfRangeException(nameof(writer));
        }
    }

    private static bool TryWritePublicationCharacter(Fixture fixture, PackageJoin join, out string reason) =>
        CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(fixture.CharacterPath, Fixture.Uid,
            new Dictionary<string, string> { ["Money"] = join.Current.Character.Money.ToString(CultureInfo.InvariantCulture) },
            detailedIoFailures: true, out reason);
}
