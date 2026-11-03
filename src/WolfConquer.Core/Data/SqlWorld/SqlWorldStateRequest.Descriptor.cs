using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRequest
{
    private static bool TryBuildDescriptor(Guid operationId, SqlWorldStateOperationKind kind,
        SqlWorldStateChange[] changes, out byte[] descriptor, out byte[] hash)
    {
        descriptor = []; hash = [];
        long size = 8 + sizeof(uint) + 2 + 16;
        foreach (var change in changes)
        {
            var image = change.Image;
            size += sizeof(long) + sizeof(uint) + sizeof(long) + 3L * sizeof(int) +
                image.CharacterBytes.Length + image.ItemBytes.Length + (long)image.ItemUids.Length * sizeof(uint);
        }
        if (size > MaximumDescriptorBytes) return false;
        using var stream = new MemoryStream(checked((int)size));
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("WCSQLW01"u8);
            writer.Write(1U);
            writer.Write((byte)kind);
            writer.Write(checked((byte)changes.Length));
            writer.Write(operationId.ToByteArray());
            foreach (var change in changes)
            {
                var image = change.Image;
                writer.Write(image.AccountId); writer.Write(image.CharacterUid); writer.Write(change.ExpectedRevision);
                writer.Write(image.CharacterBytes.Length); writer.Write(image.CharacterBytes);
                writer.Write(image.ItemBytes.Length); writer.Write(image.ItemBytes);
                writer.Write(image.ItemUids.Length);
                foreach (var uid in image.ItemUids) writer.Write(uid);
            }
        }
        descriptor = stream.ToArray();
        hash = SHA256.HashData(descriptor);
        return true;
    }
}
