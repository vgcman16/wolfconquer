using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private sealed record MigratedAllocatingWork(Guid Operation, byte[] OriginalBytes,
        LegacyItemUidProducerIntentSnapshot Intent, Dictionary<string, byte[]> ExpectedBusiness);

    private static async Task<MigratedAllocatingWork> SeedMigratedPendingAllocatingWorkAsync(Fixture fixture, PackageJoin join)
    {
        var before = CapturePackageJoin(fixture, join);
        var catalog = StackPackageCatalog();
        var metadata = new Dictionary<uint, LegacyMailboxPackageCatalog>();
        var grants = RankOnePackageRows.Select((grant, index) =>
        {
            Assert.True(LegacyMailboxPackageCatalog.TryCapture(catalog, grant.Id, out var item, out var reason), reason);
            metadata.Add(grant.Id, item!);
            return new LegacyMailboxPackageItemRecipe(AtomicMailId, (byte)(index < 4 ? 0 : 2), grant.Id,
                grant.Quantity, 321, 0, 0, 0, item!.Digest);
        }).ToArray();
        byte[] Image(Dictionary<string, byte[]> files, string path) => files[Path.GetRelativePath(fixture.Root, path)];
        byte[][] images = [Image(before.Files, fixture.CharacterPath), Image(before.Files, fixture.ItemPath),
            Image(before.Files, fixture.AtomicMailPath), Image(before.Files, fixture.PackageGearPath)];
        var seed = LegacyMailboxPackageStackSeed.Create(join.Domain, checked((uint)fixture.Account), Fixture.Uid,
            [fixture.AtomicMailSeed], images, grants);
        Assert.True(LegacyMailboxPackageStackPlan.TryCreate(before.Owner.PlayerItems, grants, metadata,
            [fixture.AtomicMailSeed], false, seed, out var stacks, out var failure), failure);
        Assert.Equal(6, stacks!.NewCount);
        var after = ExpectedStackBusinessFiles(fixture, before, before.Owner.PlayerItems, [fixture.AtomicMailSeed], false, 1031);
        Assert.True(LegacyMailboxModePacket.TryParse(ReadFrame(PackageJoinRequest(false)), out var packet));
        var recipe = LegacyMailboxPackageRecipe.CreateForStacks(join.Domain, checked((uint)fixture.Account), Fixture.Uid,
            packet, [fixture.AtomicMailSeed], grants, stacks, images,
            Image(after, fixture.CharacterPath), Image(after, fixture.PackageGearPath), Image(after, fixture.AtomicMailPath));
        var producer = join.ReadDebtProducer();
        Assert.True(producer.TryReserveRecipe(recipe, out var receipt, out var admitted, out failure), failure);
        Assert.True(admitted);
        Assert.NotNull(receipt);
        var items = stacks.Materialize(receipt).Items;
        Assert.Equal(RankOnePackageRows.Select(grant => grant.Id), items.InventoryItems.Select(item => item.ItemId));
        Assert.Equal(RankOnePackageRows.Select(grant => grant.Quantity), items.InventoryItems.Select(item => item.StackSize));
        Assert.All(items.InventoryItems, item => Assert.InRange(item.Color, 3U, 8U));
        after[Path.GetRelativePath(fixture.Root, fixture.ItemPath)] = PlayerItemSnapshotLoader.ToLegacyBytes(items);
        Assert.True(producer.TryPrepareJointWork(recipe, receipt!, PackageJoinJournalChanges(fixture, before.Files, after),
            out var prepared, out failure), failure);
        Assert.NotNull(prepared);
        Assert.True(join.Registry.TryReadProducerIntent(join.Domain, recipe.OperationId, out var intent, out failure), failure);
        var body = ReadHistoryWorkDocument(fixture, join, recipe.OperationId);
        Assert.Equal(1, body["Version"]!.GetValue<int>());
        Assert.False(body["Completed"]!.GetValue<bool>());
        var original = Encoding.UTF8.GetBytes(body.ToJsonString());
        var root = HistoryRoot(fixture, join);
        await join.DisposeAsync();
        var retained = Path.Combine(fixture.Root, "owned-history-before-legacy-fixture");
        Assert.StartsWith(Path.GetFullPath(fixture.Root) + Path.DirectorySeparatorChar, Path.GetFullPath(root));
        Assert.StartsWith(Path.GetFullPath(fixture.Root) + Path.DirectorySeparatorChar, Path.GetFullPath(retained));
        Directory.Move(root, retained); // Retain every original fixture file; never touch real source/saves.
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, recipe.OperationId.ToString("N") + ".json"), original);
        await join.StartAsync(bootstrap: false); // Genuine flat pending-v1 migration and read-only registered recovery owner.
        Assert.True(join.ReadDebtProducer().HasPendingAccountWork(fixture.Account, out var pending, out failure), failure);
        Assert.True(pending);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(root, "legacy", recipe.OperationId.ToString("N") + ".json")));
        return new(recipe.OperationId, original, intent!, after);
    }

    private static async Task CompleteAndRetireMigratedWorkAsync(Fixture fixture, PackageJoin join, MigratedAllocatingWork work)
    {
        await join.FrameAsync(PackageJoinRequest(false));
        AssertHistoryBusinessFiles(fixture, work.ExpectedBusiness);
        Assert.Equal("1031", join.Current.Character.Attributes["ClassExperience"]);
        Assert.True(join.ReadDebtProducer().TryRequireAccountWritable(fixture.Account, out var reason), reason);
        Assert.True(join.ReadDebtProducer().TryGetCompletedWorkProof(work.Operation, out var proof, out reason), reason);
        Assert.NotNull(proof);
        var beforeRetirement = fixture.PackageFiles();
        Assert.True(join.Registry.TryArchiveCompletedProducerIntent(join.Domain, work.Operation, proof!, out reason), reason);
        AssertUidHistoryOnlyChanges(fixture, beforeRetirement, work.Operation);
        Assert.True(join.Registry.TryReadActiveProducerIntents(join.Domain, out var active, out reason), reason);
        Assert.Empty(active);
        Assert.True(join.Registry.TryReadProducerIntent(join.Domain, work.Operation, out var archived, out reason), reason);
        AssertUidHistorySnapshot(work.Intent, archived!);
    }
}
