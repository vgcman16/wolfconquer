using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateBundleRequest
{
    private static bool TryBuildDescriptor(Guid operationId, SqlWorldStateOperationKind kind,
        SqlWorldStateBundleChange[] changes, out byte[] descriptor, out byte[] hash)
    {
        descriptor = [];
        hash = [];
        long size = 8 + sizeof(uint) + 2 + 16;
        try
        {
            checked
            {
                foreach (var change in changes)
                {
                    var bundle = change.Image;
                    var core = bundle.Core;
                    size += sizeof(long) + sizeof(uint) + sizeof(long) + 3L * sizeof(int) +
                        core.CharacterBytes.Length + core.ItemBytes.Length + (long)core.ItemUids.Length * sizeof(uint);
                    foreach (var sidecar in bundle.Sidecars) size += 2L + sizeof(int) + sidecar.Bytes.Length;
                }
            }
        }
        catch (OverflowException) { return false; }
        if (size > MaximumDescriptorBytes) return false;

        using var stream = new MemoryStream(checked((int)size));
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("WCSQLW02"u8);
            writer.Write(2U);
            writer.Write((byte)kind);
            writer.Write(checked((byte)changes.Length));
            writer.Write(operationId.ToByteArray());
            foreach (var change in changes)
            {
                var bundle = change.Image;
                var core = bundle.Core;
                writer.Write(bundle.AccountId);
                writer.Write(bundle.CharacterUid);
                writer.Write(change.ExpectedRevision);
                writer.Write(core.CharacterBytes.Length);
                writer.Write(core.CharacterBytes);
                writer.Write(core.ItemBytes.Length);
                writer.Write(core.ItemBytes);
                writer.Write(core.ItemUids.Length);
                foreach (var uid in core.ItemUids) writer.Write(uid);
                foreach (var sidecar in bundle.Sidecars)
                {
                    writer.Write((byte)sidecar.Kind);
                    writer.Write((byte)sidecar.Presence);
                    writer.Write(sidecar.Bytes.Length);
                    writer.Write(sidecar.Bytes);
                }
            }
        }
        descriptor = stream.ToArray();
        hash = SHA256.HashData(descriptor);
        return true;
    }
}
