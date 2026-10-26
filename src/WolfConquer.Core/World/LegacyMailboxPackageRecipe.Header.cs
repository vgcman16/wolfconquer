using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageRecipe
{
    private static void WriteHeader(BinaryWriter writer, Guid domain, uint account, uint uid, string[] lineages, uint magic = Magic)
    {
        writer.Write(magic); writer.Write(domain.ToByteArray()); writer.Write(account); writer.Write(uid);
        writer.Write(lineages.Length);
        foreach (var key in lineages) writer.Write(Convert.FromHexString(key));
        writer.Flush();
    }

    internal static bool TryReadHeader(byte[] payload, Guid domain, uint account, uint uid, out string[] keys)
    {
        keys = [];
        if (payload is null || payload.Length is < 32 or > LegacyItemUidRegistry.MaximumProducerPayloadBytes) return false;
        try
        {
            using var input = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(input, Utf8);
            var magic = reader.ReadUInt32();
            if (magic != Magic && magic != StackMagic || new Guid(reader.ReadBytes(16)) != domain ||
                reader.ReadUInt32() != account || reader.ReadUInt32() != uid) return false;
            var count = reader.ReadInt32();
            if (count is < 1 or > 40 || input.Length - input.Position < count * 32) return false;
            var result = new string[count];
            for (var index = 0; index < count; index++) result[index] = Convert.ToHexString(reader.ReadBytes(32));
            if (result.Distinct(StringComparer.Ordinal).Count() != count) return false;
            keys = result;
            return true;
        }
        catch (Exception error) when (error is IOException or ArgumentException) { return false; }
    }
}
