using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Called only inside the actual Store and file gates. A missing image keeps
    // the existing complete first-save policy; existing opaque bytes are retained.
    private static bool TryPreparePerformerDanceImage(string path, byte[] expected, byte[] complete, out byte[]? image)
    {
        image = null;
        byte[] saved;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            // Expected is the bounded, fully serialized genuine session. Do not
            // allocate from an untrusted file header or unbounded stream length.
            if (stream.Length != expected.Length) return false;
            saved = new byte[expected.Length];
            stream.ReadExactly(saved);
            if (stream.ReadByte() != -1) return false;
        }
        catch (FileNotFoundException) { image = complete; return true; }
        catch (DirectoryNotFoundException) { image = complete; return true; }

        if (!saved.AsSpan(0, sizeof(int)).SequenceEqual(expected.AsSpan(0, sizeof(int)))) return false;
        for (var offset = sizeof(int); offset < saved.Length; offset += PlayerSpellSnapshotLoader.LegacySpellSize)
            if (!saved.AsSpan(offset, PlayerSpellSnapshotLoader.LegacySpellSize - 1)
                .SequenceEqual(expected.AsSpan(offset, PlayerSpellSnapshotLoader.LegacySpellSize - 1))) return false;

        image = new byte[checked(saved.Length + PlayerSpellSnapshotLoader.LegacySpellSize)];
        saved.CopyTo(image, 0);
        BinaryPrimitives.WriteInt32LittleEndian(image, BinaryPrimitives.ReadInt32LittleEndian(complete));
        complete.AsSpan(complete.Length - PlayerSpellSnapshotLoader.LegacySpellSize)
            .CopyTo(image.AsSpan(saved.Length));
        return true;
    }
}
