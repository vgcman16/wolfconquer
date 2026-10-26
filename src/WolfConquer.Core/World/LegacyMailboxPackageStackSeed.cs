using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Core.World;

internal static class LegacyMailboxPackageStackSeed
{
    internal static byte[] Create(Guid domain, uint account, uint uid, LegacyMailboxEntry[] selected,
        byte[][] beforeImages, LegacyMailboxPackageItemRecipe[] grants)
    {
        if (domain == Guid.Empty || account == 0 || uid == 0 || beforeImages.Length != 4)
            throw new ArgumentException("Invalid package stack seed owner.");
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, new UTF8Encoding(false, true), leaveOpen: true);
        writer.Write(0x32435057u); // WPC2
        writer.Write(domain.ToByteArray()); writer.Write(account); writer.Write(uid);
        var keys = LegacyMailboxPackageRecipe.CaptureLineageKeys(selected);
        writer.Write(keys.Length);
        foreach (var key in keys) writer.Write(Convert.FromHexString(key));
        foreach (var bytes in beforeImages) writer.Write(SHA256.HashData(bytes));
        writer.Write(grants.Length);
        foreach (var grant in grants)
        {
            writer.Write(grant.MailId); writer.Write(grant.Phase); writer.Write(grant.ItemId); writer.Write(grant.Quantity);
            writer.Write(grant.Durability); writer.Write(grant.Plus); writer.Write(grant.Bound); writer.Write(grant.SocketProgress);
            writer.Write(Convert.FromHexString(grant.CatalogSha256));
        }
        writer.Flush();
        return SHA256.HashData(output.ToArray());
    }
}
