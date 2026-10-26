using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyMailboxStore
{
    private IReadOnlyList<LegacyMailboxEntry> LoadCore(uint playerUid)
    {
        var path = GetMailboxPath(playerUid);
        var loaded = SnapshotFileLockCoordinator.ExecuteLocked(path, () => ReadCompleteMailbox(path));
        var now = nowProvider();
        var live = loaded.Where(entry => entry.IsLive(now)).ToList();
        // Every byte/row and live projection is validated before replacing a prior valid cache.
        var result = live.ToArray();
        mailboxByPlayerUid[playerUid] = live;
        return result;
    }

    private LegacyMailboxEntry[] ReadCompleteMailbox(string path)
    {
        byte[] bytes;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!CharacterSnapshotFileBaseline.TryValidateSafePath(path, "mailboxLoad", out var reason))
                throw new InvalidDataException(reason);
            if (stream.Length is < 4 or > CharacterSnapshotFileBaseline.MaximumBytes)
                throw new InvalidDataException("Mailbox image size is invalid.");
            bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1 || stream.Length != bytes.Length)
                throw new InvalidDataException("Mailbox image changed while reading.");
        }
        catch (FileNotFoundException) { return []; }
        catch (DirectoryNotFoundException) { return []; }
        try
        {
            if (TryParseCompleteImage(bytes, out var rows)) return rows;
        }
        catch (ArgumentException error)
        { throw new InvalidDataException("Mailbox row contains an invalid timer or encoding.", error); }
        throw new InvalidDataException("Mailbox image is incomplete or ambiguous.");
    }
}
