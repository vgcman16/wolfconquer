using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private sealed record StackPreparedWork(LegacyMailboxPackageRecipe Recipe,
        LegacyMailboxPackageProducer.PreparedJointWork Work, Dictionary<string, byte[]> After);

    private static StackPreparedWork PrepareStackNoAllocationWork(Fixture fixture, PackageJoin join,
        PackageJoinBefore before, PlayerItemSnapshot expectedItems, bool claimAll = false)
    {
        var after = ExpectedStackBusinessFiles(fixture, before, expectedItems, [fixture.AtomicMailSeed], claimAll, 1031);
        var catalog = StackPackageCatalog();
        var metadata = new Dictionary<uint, LegacyMailboxPackageCatalog>();
        var grants = RankOnePackageRows.Select((grant, index) =>
        {
            Assert.True(LegacyMailboxPackageCatalog.TryCapture(catalog, grant.Id, out var row, out var reason), reason);
            metadata.Add(grant.Id, row!);
            return new LegacyMailboxPackageItemRecipe(AtomicMailId, (byte)(index < 4 ? 0 : 2), grant.Id,
                grant.Quantity, 321, 0, 0, 0, row!.Digest);
        }).ToArray();
        byte[] Image(Dictionary<string, byte[]> files, string path) => files[Path.GetRelativePath(fixture.Root, path)];
        byte[][] images = [Image(before.Files, fixture.CharacterPath), Image(before.Files, fixture.ItemPath),
            Image(before.Files, fixture.AtomicMailPath), Image(before.Files, fixture.PackageGearPath)];
        var seed = LegacyMailboxPackageStackSeed.Create(join.Domain, checked((uint)fixture.Account), Fixture.Uid,
            [fixture.AtomicMailSeed], images, grants);
        Assert.True(LegacyMailboxPackageStackPlan.TryCreate(before.Owner.PlayerItems, grants, metadata,
            [fixture.AtomicMailSeed], claimAll, seed, out var stacks, out var failure), failure);
        Assert.Equal(0, stacks!.NewCount);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(stacks.Materialize(null).Items));
        Assert.True(LegacyMailboxModePacket.TryParse(ReadFrame(PackageJoinRequest(claimAll)), out var packet));
        var recipe = LegacyMailboxPackageRecipe.CreateForStacks(join.Domain, checked((uint)fixture.Account), Fixture.Uid,
            packet, [fixture.AtomicMailSeed], grants, stacks, images,
            Image(after, fixture.CharacterPath), Image(after, fixture.PackageGearPath), Image(after, fixture.AtomicMailPath));
        Assert.Equal(0, recipe.Count);
        Assert.True(join.ReadDebtProducer().TryPrepareNoAllocationWork(recipe,
            PackageJoinJournalChanges(fixture, before.Files, after), out var work, out var admitted, out failure), failure);
        Assert.True(admitted);
        Assert.NotNull(work);
        return new(recipe, work, after);
    }
}
