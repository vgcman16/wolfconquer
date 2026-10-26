using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed class LegacyMailboxPackageCatalog
{
    private readonly WorldDataCatalog catalog;
    private readonly ItemType source;
    private readonly string name;
    private readonly string[] fields;
    internal ushort Durability { get; }
    internal ushort StackLimit { get; }
    internal string Digest { get; }

    private LegacyMailboxPackageCatalog(WorldDataCatalog catalog, ItemType source, string[] fields,
        ushort durability, ushort stackLimit, string digest)
    { this.catalog = catalog; this.source = source; this.fields = fields; name = source.Name; Durability = durability; StackLimit = stackLimit; Digest = digest; }

    internal static bool TryCapture(WorldDataCatalog catalog, uint id,
        out LegacyMailboxPackageCatalog? captured, out string reason)
    {
        captured = null;
        reason = "mailboxPackageCatalogInvalid";
        if (catalog is null || id > int.MaxValue || !catalog.ItemsById.TryGetValue((int)id, out var rows) || rows.Count != 1 ||
            rows[0] is not { } source || source.Id != id || source.Name is null || source.Name.Length > 1024 ||
            source.Fields is not { Count: > ItemType.StackSizeFieldIndex and <= 256 }) return false;
        var fields = source.Fields.ToArray();
        if (fields.Any(value => value is null || value.Length > 1024 || value.Contains((char)0)) ||
            !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawId) || rawId != source.Id ||
            !string.Equals(fields[1], source.Name, StringComparison.Ordinal) ||
            !ushort.TryParse(fields[ItemType.DurabilityFieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var durability) ||
            !ushort.TryParse(fields[ItemType.StackSizeFieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var stack) ||
            !uint.TryParse(fields[ItemType.TimeFieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var time)) return false;
        if (time != 0) { reason = "mailboxPackageTimedItemOwnerUnavailable"; return false; }
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, new UTF8Encoding(false, true), leaveOpen: true))
        {
            writer.Write(source.Id); writer.Write(source.Name); writer.Write(fields.Length);
            foreach (var field in fields) writer.Write(field);
        }
        var limit = source.IsLegacyAccessory ? (ushort)1 : Math.Max((ushort)1, stack);
        var candidate = new LegacyMailboxPackageCatalog(catalog, source, fields, durability, limit,
            Convert.ToHexString(SHA256.HashData(output.ToArray())).ToLowerInvariant());
        if (!candidate.IsCurrent()) return false;
        captured = candidate;
        reason = string.Empty;
        return true;
    }

    internal bool IsCurrent() => catalog.ItemsById.TryGetValue(source.Id, out var rows) && rows.Count == 1 &&
        ReferenceEquals(rows[0], source) && string.Equals(source.Name, name, StringComparison.Ordinal) &&
        source.Fields is not null && fields.SequenceEqual(source.Fields, StringComparer.Ordinal);
}
