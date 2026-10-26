using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static void AssertDeliveryCommittedState(Fixture fixture, PackageJoin join, PackageJoinBefore before, bool claimAll)
    {
        var current = join.Current;
        var intent = AssertPackageJoinAllocation(fixture, join);
        var expected = ExpectedPackageJoinUnallocatedFiles(fixture, before, claimAll, job: 5051);
        foreach (var path in new[] { fixture.CharacterPath, fixture.AtomicMailPath, fixture.PackageGearPath })
            Assert.Equal(expected[Path.GetRelativePath(fixture.Root, path)], File.ReadAllBytes(path));
        Assert.Equal(1031U, uint.Parse(current.Character.Attributes["ClassExperience"], CultureInfo.InvariantCulture));
        Assert.Equal(1031U, join.NativeClient.Player.ClassExperience);
        Assert.Equal(75U, join.NativeClient.MyNinja!.Exp);
        var mail = File.ReadAllBytes(fixture.AtomicMailPath);
        Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(mail.AsSpan(4 + 728)));
        var saved = new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(Fixture.Uid);
        var expectedItems = ExpectedPackageJoinItems(before, intent, claimAll);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems), File.ReadAllBytes(fixture.ItemPath));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems), PlayerItemSnapshotLoader.ToLegacyBytes(saved));
        Assert.Equal(before.Owner.PlayerItems.InventoryItems.Count + 6 + (claimAll ? 1 : 0), saved.InventoryItems.Count);
        foreach (var (row, index) in RankOnePackageRows.Select((row, index) => (row, index)))
        {
            var generated = Assert.Single(saved.InventoryItems, item => item.Uid == intent.Allocation!.First + (uint)index);
            Assert.Equal(row.Id, generated.ItemId);
            Assert.Equal(row.Quantity, generated.StackSize);
        }
        Assert.True(LegacyItemSaveGraph.TryCapture(saved, out _));
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        var producer = Assert.IsType<LegacyMailboxPackageProducer>(join.SessionStore.MailboxPackages);
        Assert.True(producer.HasPendingAccountWork(fixture.Account, out var pending, out var reason), reason);
        Assert.False(pending);
        var work = Assert.IsType<LegacyMailboxPackageWorkStore.Record>(
            new LegacyMailboxPackageWorkStore(producer).Read(intent.Request.OperationId));
        Assert.True(work.Completed);
        Assert.Equal(intent.Request.PayloadSha256, work.PayloadSha256);
        var targets = new[] { fixture.CharacterPath, fixture.ItemPath, fixture.AtomicMailPath, fixture.PackageGearPath };
        Assert.Equal(targets.Select(path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()), work.After);
    }
}
