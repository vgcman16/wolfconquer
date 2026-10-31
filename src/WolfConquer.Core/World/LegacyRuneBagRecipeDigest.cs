using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Core.World;

internal static class LegacyRuneBagRecipeDigest
{
    internal static string Compute(Guid domain, uint account, uint uid, uint selected, bool intoBag,
        LegacyRuneBagMetadata metadata, byte[] character, byte[] items, byte[]? spells)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("rune-bag-return-v1"); writer.Write(domain.ToByteArray()); writer.Write(account); writer.Write(uid);
            writer.Write(selected); writer.Write(intoBag); writer.Write(metadata.ItemId); writer.Write(metadata.RawMaximumStackSize);
            Write(character); Write(items); Write(spells);
            void Write(byte[]? image) { writer.Write(image?.Length ?? -1); if (image is not null) writer.Write(image); }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }
}
