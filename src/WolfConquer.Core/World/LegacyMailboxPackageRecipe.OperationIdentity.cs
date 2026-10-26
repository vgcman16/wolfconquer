using System.Buffers.Binary;
using System.Security.Cryptography;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageRecipe
{
    internal static bool TryReadOperationIdentity(byte[] payload, Guid domain, uint account, uint uid, out Guid operation)
    {
        operation = Guid.Empty;
        if (!TryReadHeader(payload, domain, account, uid, out var keys)) return false;
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Utf8, leaveOpen: true);
        WriteHeader(writer, domain, account, uid, keys, BinaryPrimitives.ReadUInt32LittleEndian(payload));
        var bytes = SHA256.HashData(output.ToArray())[..16];
        if (bytes.All(value => value == 0)) bytes[0] = 1;
        operation = new Guid(bytes);
        return true;
    }
}
