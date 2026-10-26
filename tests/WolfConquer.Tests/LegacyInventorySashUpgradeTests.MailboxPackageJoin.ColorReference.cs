using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private sealed record PackageReferenceCatalog(int Id, string Name, string[] Fields);

    private static PackageReferenceCatalog[] CapturePackageReferenceCatalog(PackageJoin join) => join.CopyReferenceCatalog();

    private static LegacyMailboxPackageItemRecipe[] ReferencePackageGrants(PackageJoinBefore before,
        LegacyMailboxEntry[] selected) => selected.Where(mail => mail.Flag != 0).SelectMany(mail =>
    {
        Assert.Equal(1U, mail.Flag); // These fixtures deliberately use known rank1 rewards.
        return RankOnePackageRows.Select((grant, index) =>
        {
            var metadata = Assert.Single(before.Catalog, row => row.Id == grant.Id);
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, new UTF8Encoding(false, true), leaveOpen: true))
            {
                writer.Write(metadata.Id); writer.Write(metadata.Name); writer.Write(metadata.Fields.Length);
                foreach (var field in metadata.Fields) writer.Write(field);
            }
            Assert.True(ushort.TryParse(metadata.Fields[ItemType.DurabilityFieldIndex], NumberStyles.None,
                CultureInfo.InvariantCulture, out var durability));
            Assert.Equal(321, durability);
            return new LegacyMailboxPackageItemRecipe(mail.Id, (byte)(index < 4 ? 0 : 2), grant.Id, grant.Quantity,
                durability, 0, 0, 0, Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant());
        });
    }).ToArray();

    // Independent WPC2 contract calculation from immutable fixture inputs,
    // never a produced item's Color or the implementation's retained seed.
    private static byte[] ReferencePackageSeed(PackageJoinBefore before, Guid domain, LegacyMailboxEntry[] selected)
    {
        var grants = ReferencePackageGrants(before, selected);
        var lineages = selected.Where(mail => mail.Flag != 0).DistinctBy(mail => mail.Id).OrderBy(mail => mail.Id)
            .Select(mail =>
            {
                using var bytes = new MemoryStream();
                using (var writer = new BinaryWriter(bytes, new UTF8Encoding(false, true), leaveOpen: true))
                {
                    writer.Write(mail.Id); writer.Write(mail.Flag); writer.Write(mail.Timer);
                    writer.Write(mail.Title); writer.Write(mail.SenderName); writer.Write(mail.Content);
                }
                return SHA256.HashData(bytes.ToArray());
            }).ToArray();
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, new UTF8Encoding(false, true), leaveOpen: true))
        {
            writer.Write(0x32435057U); writer.Write(domain.ToByteArray());
            writer.Write(checked((uint)before.Owner.AccountId)); writer.Write(checked((uint)before.Owner.Character.Uid));
            writer.Write(lineages.Length);
            foreach (var lineage in lineages) writer.Write(lineage);
            foreach (var image in ReferencePackageBeforeImages(before)) writer.Write(SHA256.HashData(image));
            writer.Write(grants.Length);
            foreach (var grant in grants)
            {
                writer.Write(grant.MailId); writer.Write(grant.Phase); writer.Write(grant.ItemId); writer.Write(grant.Quantity);
                writer.Write(grant.Durability); writer.Write(grant.Plus); writer.Write(grant.Bound); writer.Write(grant.SocketProgress);
                writer.Write(Convert.FromHexString(grant.CatalogSha256));
            }
        }
        return SHA256.HashData(output.ToArray());
    }

    private static byte[][] ReferencePackageBeforeImages(PackageJoinBefore before)
    {
        var root = Path.GetDirectoryName(before.Owner.UsersRoot)!;
        return before.BusinessPaths.Select(path => before.Files[Path.GetRelativePath(root, path)]).ToArray();
    }

    private static uint[] ReferencePackageColors(PackageJoinBefore before, Guid domain,
        LegacyMailboxEntry[] selected, int count)
    {
        var seed = ReferencePackageSeed(before, domain, selected);
        return Enumerable.Range(0, count).Select(index =>
        {
            var input = new byte[36];
            seed.CopyTo(input, 0);
            BinaryPrimitives.WriteInt32LittleEndian(input.AsSpan(32), index);
            return 3U + SHA256.HashData(input)[0] % 6U;
        }).ToArray();
    }

    private sealed partial class PackageJoin
    {
        internal PackageReferenceCatalog[] CopyReferenceCatalog() => worldData.Items
            .Select(item => new PackageReferenceCatalog(item.Id, item.Name, item.Fields.ToArray())).ToArray();
    }
}
