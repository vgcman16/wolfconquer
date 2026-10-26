using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static LegacyMailboxPackageRecipe CreatePackageJoinStackRecipe(Fixture fixture, PackageJoin join,
        PackageJoinBefore before, bool claimAll, long job, CombatGearSnapshot? gearAfter = null)
    {
        var after = ExpectedPackageJoinUnallocatedFiles(fixture, before, claimAll, job);
        if (gearAfter is not null) after[Path.GetRelativePath(fixture.Root, fixture.PackageGearPath)] =
            new System.Text.UTF8Encoding(false, true).GetBytes(CombatGearSnapshotLoader.ToLegacyText(gearAfter));
        var selected = new[] { before.MailSeed };
        var grants = ReferencePackageGrants(before, selected);
        var metadata = new Dictionary<uint, LegacyMailboxPackageCatalog>();
        foreach (var grant in grants)
        {
            Assert.True(LegacyMailboxPackageCatalog.TryCapture(join.PackageReferenceWorldData, grant.ItemId, out var captured, out var reason), reason);
            Assert.Equal(grant.CatalogSha256, captured!.Digest);
            metadata.Add(grant.ItemId, captured);
        }
        var images = ReferencePackageBeforeImages(before);
        var referenceSeed = ReferencePackageSeed(before, join.Domain, selected);
        var seed = LegacyMailboxPackageStackSeed.Create(join.Domain, checked((uint)fixture.Account), Fixture.Uid,
            selected, images, grants);
        Assert.Equal(referenceSeed, seed);
        Assert.True(LegacyMailboxPackageStackPlan.TryCreate(before.Owner.PlayerItems, grants, metadata,
            selected, claimAll, seed, out var stacks, out var failure), failure);
        Assert.Equal(6, stacks!.NewCount);
        var colors = ReferencePackageColors(before, join.Domain, selected, 6);
        var changes = stacks.CopyChanges();
        Assert.Equal(6, changes.Length);
        for (var index = 0; index < changes.Length; index++)
        {
            Assert.True(changes[index].IsNew);
            Assert.Equal(index, changes[index].GrantIndex);
            Assert.Equal(index, changes[index].AllocationIndex);
            Assert.Equal(0U, changes[index].ExistingUid);
            Assert.Equal(RankOnePackageRows[index].Quantity, changes[index].QuantityAfter);
            Assert.Equal(colors[index], changes[index].Color);
        }
        byte[] Image(string path) => after[Path.GetRelativePath(fixture.Root, path)];
        Assert.True(LegacyMailboxModePacket.TryParse(ReadFrame(PackageJoinRequest(claimAll)), out var packet));
        return LegacyMailboxPackageRecipe.CreateForStacks(join.Domain, checked((uint)fixture.Account), Fixture.Uid,
            packet, selected, grants, stacks, images, Image(fixture.CharacterPath), Image(fixture.PackageGearPath), Image(fixture.AtomicMailPath));
    }

    private sealed partial class PackageJoin
    {
        internal WorldDataCatalog PackageReferenceWorldData => worldData;
    }
}
