using System.Security.Cryptography;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageRecipe
{
    private const uint StackMagic = 0x32504d57; // WMP2: exact stack mutations and colors.

    internal static LegacyMailboxPackageRecipe CreateForStacks(Guid domain, uint account, uint uid,
        LegacyMailboxModePacket packet, LegacyMailboxEntry[] selected, LegacyMailboxPackageItemRecipe[] grants,
        LegacyMailboxPackageStackPlan stacks, byte[][] beforeImages,
        byte[] characterAfter, byte[] gearAfter, byte[] mailAfter)
    {
        var keys = CaptureLineageKeys(selected);
        if (domain == Guid.Empty || account == 0 || uid == 0 || grants.Length is < 1 or > 256 ||
            beforeImages.Length != 4 || keys.Length is < 1 or > 40 || stacks.NewCount is < 0 or > 40)
            throw new ArgumentException("Invalid mailbox stack recipe shape.");
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Utf8, leaveOpen: true);
        WriteHeader(writer, domain, account, uid, keys, StackMagic);
        var operationBytes = SHA256.HashData(output.ToArray())[..16];
        if (operationBytes.All(value => value == 0)) operationBytes[0] = 1;
        writer.Write((ushort)packet.Type); writer.Write(packet.Id); writer.Write(packet.Items.Count);
        foreach (var id in packet.Items) { writer.Write(id); RequireBound(output); }
        writer.Write(selected.Length);
        foreach (var mail in selected)
        {
            writer.Write(mail.Id); writer.Write(mail.Flag); writer.Write(mail.Timer); writer.Write(mail.Money);
            writer.Write(mail.EMoney); writer.Write(mail.ItemId); writer.Write(mail.EmoneyRecordType);
            writer.Write(Convert.FromHexString(Lineage(mail))); RequireBound(output);
        }
        foreach (var image in beforeImages) writer.Write(SHA256.HashData(image));
        writer.Write(SHA256.HashData(characterAfter)); writer.Write(SHA256.HashData(gearAfter)); writer.Write(SHA256.HashData(mailAfter));
        var itemsAfter = stacks.NewCount == 0
            ? WolfConquer.Core.Data.PlayerItemSnapshotLoader.ToLegacyBytes(stacks.Materialize(null).Items) : null;
        writer.Write(itemsAfter is null ? new byte[32] : SHA256.HashData(itemsAfter));
        writer.Write(grants.Length);
        foreach (var grant in grants)
        {
            writer.Write(grant.MailId); writer.Write(grant.Phase); writer.Write(grant.ItemId); writer.Write(grant.Quantity);
            writer.Write(grant.Durability); writer.Write(grant.Plus); writer.Write(grant.Bound); writer.Write(grant.SocketProgress);
            writer.Write(Convert.FromHexString(grant.CatalogSha256)); RequireBound(output);
        }
        writer.Write(stacks.CopySeed()); writer.Write(stacks.NewCount);
        var changes = stacks.CopyChanges();
        writer.Write(changes.Length);
        foreach (var change in changes)
        {
            writer.Write(change.GrantIndex); writer.Write(change.ExistingUid); writer.Write(change.AllocationIndex);
            writer.Write(change.QuantityAfter); writer.Write(change.Color); writer.Write(change.IsNew); RequireBound(output);
        }
        var attachments = stacks.CopyAttachmentUids();
        writer.Write(attachments.Length);
        foreach (var attachment in attachments) writer.Write(attachment);
        writer.Flush(); RequireBound(output);
        return new(new Guid(operationBytes), account, uid, stacks.NewCount, output.ToArray(), keys);
    }
}
