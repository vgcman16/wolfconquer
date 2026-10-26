namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageRecipe
{
    internal sealed record WorkDescription(LegacyMailboxModePacket Request, string[] Before, string[] After, int Count,
        string? ItemsAfterSha256 = null);

    internal static bool TryReadWorkDescription(byte[] payload, Guid domain, uint account, uint uid,
        out WorkDescription? description)
    {
        description = null;
        if (!TryReadHeader(payload, domain, account, uid, out var keys)) return false;
        try
        {
            using var input = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(input, Utf8);
            var magic = reader.ReadUInt32();
            if (magic != Magic && magic != StackMagic) return false;
            input.Position = 4 + 16 + 4 + 4 + 4 + keys.Length * 32;
            var type = (LegacyMailboxModeType)reader.ReadUInt16();
            var id = reader.ReadUInt32();
            var count = reader.ReadInt32();
            if (type is not (LegacyMailboxModeType.ClaimRecordType or LegacyMailboxModeType.ClaimAll) ||
                count < 0 || count > (input.Length - input.Position) / 8) return false;
            var ids = new ulong[count];
            for (var index = 0; index < count; index++) ids[index] = reader.ReadUInt64();
            var selected = reader.ReadInt32();
            if (selected < 1 || selected > (input.Length - input.Position - 224 - 4) / 68) return false;
            var packageKeys = new SortedDictionary<uint, string>();
            for (var index = 0; index < selected; index++)
            {
                var mailId = reader.ReadUInt32();
                var flag = reader.ReadUInt32();
                _ = reader.ReadInt64(); _ = reader.ReadUInt64();
                _ = reader.ReadUInt32(); _ = reader.ReadUInt32(); _ = reader.ReadUInt32();
                var lineage = Convert.ToHexString(reader.ReadBytes(32));
                if (mailId == 0 || flag > 9 || lineage.Length != 64) return false;
                if (flag != 0)
                {
                    if (packageKeys.TryGetValue(mailId, out var previous) && previous != lineage) return false;
                    packageKeys[mailId] = lineage;
                }
            }
            if (!packageKeys.Values.SequenceEqual(keys, StringComparer.Ordinal)) return false;
            string Hash() => Convert.ToHexString(reader.ReadBytes(32)).ToLowerInvariant();
            var before = Enumerable.Range(0, 4).Select(_ => Hash()).ToArray();
            var after = Enumerable.Range(0, 3).Select(_ => Hash()).ToArray();
            var itemsAfter = magic == StackMagic ? Hash() : null;
            var grants = reader.ReadInt32();
            if (grants < 1 || grants > (magic == Magic ? 40 : 256) || before.Concat(after).Any(hash => hash.Length != 64)) return false;
            if (magic == Magic)
            {
                if (input.Length - input.Position != grants * 51L) return false;
                description = new(new(type, id, ids), before, after, grants);
                return true;
            }
            if (itemsAfter!.Length != 64 || input.Length - input.Position < grants * 51L + 44) return false;
            input.Position += grants * 51L;
            if (!TryReadStackWorkTail(reader, grants, out var newCount)) return false;
            var zeroHash = new string('0', 64);
            if (newCount == 0 ? itemsAfter == zeroHash : itemsAfter != zeroHash) return false;
            description = new(new(type, id, ids), before, after, newCount, newCount == 0 ? itemsAfter : null);
            return true;
        }
        catch (Exception error) when (error is IOException or ArgumentException or OverflowException) { return false; }
    }
}
