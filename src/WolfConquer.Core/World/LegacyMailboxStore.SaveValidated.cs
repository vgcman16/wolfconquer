using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyMailboxStore
{
    private const string SaveTransactionMarker = ".mailbox-file-";

    private void SavePreparedMailbox(uint playerUid)
    {
        // Public Save retains SyncRoot. Entry objects are immutable after initialization;
        // List supplies one detached live list, preserving the established expiry rule.
        var image = PrepareMailboxSaveImage(List(playerUid));
        var path = GetMailboxPath(playerUid);
        SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
        {
            SnapshotFilePersistence.WriteAllBytesAtomically(path, image, SaveTransactionMarker);
            return true;
        });
    }

    private byte[] PrepareMailboxSaveImage(IReadOnlyList<LegacyMailboxEntry> entries)
    {
        var imageSize = 4L + (long)entries.Count * ClaimEntrySize;
        if (imageSize > CharacterSnapshotFileBaseline.MaximumBytes)
            throw new InvalidDataException("Mailbox image exceeds the bounded save size.");

        using var memory = new MemoryStream(checked((int)imageSize));
        using (var writer = new BinaryWriter(memory, encoding, leaveOpen: true))
        {
            writer.Write(entries.Count);
            foreach (var entry in entries) WriteEntry(writer, entry);
        }
        var image = memory.ToArray();
        if (image.LongLength != imageSize || !TryParseCompleteImage(image, out _))
            throw new InvalidDataException("Mailbox save image is incomplete or ambiguous.");
        // A last-row removal is a complete count-zero image, never an omitted write.
        return image;
    }
}
