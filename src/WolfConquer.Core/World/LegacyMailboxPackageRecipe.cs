using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyMailboxPackageItemRecipe(uint MailId, byte Phase, uint ItemId,
    ushort Quantity, ushort Durability, byte Plus, byte Bound, uint SocketProgress, string CatalogSha256);

// Immutable deterministic work description. Eligibility belongs to the real
// registered mailbox owner; a digest alone never grants gameplay permission.
internal sealed partial class LegacyMailboxPackageRecipe
{
    private const uint Magic = 0x31504d57; // WMP1, little endian.
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private readonly byte[] payload;
    private readonly string[] lineages;
    internal Guid OperationId { get; }
    internal uint AccountId { get; }
    internal uint CharacterUid { get; }
    internal int Count { get; }
    internal byte[] CopyPayload() => payload.ToArray();
    internal bool Overlaps(string[] other) => other.Any(value => lineages.Contains(value, StringComparer.Ordinal));

    private LegacyMailboxPackageRecipe(Guid operation, uint account, uint uid, int count, byte[] payload, string[] lineages)
    { OperationId = operation; AccountId = account; CharacterUid = uid; Count = count; this.payload = payload; this.lineages = lineages; }

    internal static LegacyMailboxPackageRecipe Create(Guid domain, uint account, uint uid,
        LegacyMailboxModePacket packet, LegacyMailboxEntry[] selected, LegacyMailboxPackageItemRecipe[] grants,
        byte[][] beforeImages, byte[] characterAfter, byte[] gearAfter, byte[] mailAfter)
    {
        if (domain == Guid.Empty || account == 0 || uid == 0 || grants.Length is < 1 or > 40 || beforeImages.Length != 4)
            throw new ArgumentException("Invalid mailbox package recipe shape.");
        var keys = selected.Where(mail => mail.Flag != 0).DistinctBy(mail => mail.Id)
            .OrderBy(mail => mail.Id).Select(Lineage).ToArray();
        if (keys.Length is < 1 or > 40) throw new ArgumentException("Invalid mailbox package lineage count.");
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Utf8, leaveOpen: true);
        WriteHeader(writer, domain, account, uid, keys);
        var operationBytes = SHA256.HashData(output.ToArray())[..16];
        if (operationBytes.All(value => value == 0)) operationBytes[0] = 1;
        var operation = new Guid(operationBytes);
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
        writer.Write(grants.Length);
        foreach (var grant in grants)
        {
            writer.Write(grant.MailId); writer.Write(grant.Phase); writer.Write(grant.ItemId); writer.Write(grant.Quantity);
            writer.Write(grant.Durability); writer.Write(grant.Plus); writer.Write(grant.Bound); writer.Write(grant.SocketProgress);
            writer.Write(Convert.FromHexString(grant.CatalogSha256)); RequireBound(output);
        }
        writer.Flush(); RequireBound(output);
        return new(operation, account, uid, grants.Length, output.ToArray(), keys);
    }

    private static string Lineage(LegacyMailboxEntry mail)
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Utf8, leaveOpen: true);
        writer.Write(mail.Id); writer.Write(mail.Flag); writer.Write(mail.Timer);
        writer.Write(mail.Title); writer.Write(mail.SenderName); writer.Write(mail.Content); writer.Flush();
        return Convert.ToHexString(SHA256.HashData(output.ToArray()));
    }

    private static void RequireBound(MemoryStream output)
    {
        if (output.Length > LegacyItemUidRegistry.MaximumProducerPayloadBytes)
            throw new ArgumentException("Mailbox package recipe exceeds the durable payload bound.");
    }
}
