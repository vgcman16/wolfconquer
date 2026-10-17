namespace WolfConquer.Core.Data;

internal static class PlayerItemSnapshotFilePersistence
{
    private const string TransactionMarker = ".player-item-file-";

    internal static byte[]? ReadAllBytesIfExists(string path)
    {
        return SnapshotFilePersistence.ReadAllBytesIfExists(path);
    }

    internal static void WriteAllBytesAtomically(string path, ReadOnlySpan<byte> bytes)
    {
        SnapshotFilePersistence.WriteAllBytesAtomically(path, bytes, TransactionMarker);
    }
}
