using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Raw images stay internal. Only logical file labels and non-content metadata
// can enter the public report. This read helper does not create directories.
internal sealed record OwnedPlayerFileObservation(string Kind, string Path, byte[]? Bytes,
    DateTime? CreationUtc, DateTime? LastWriteUtc)
{
    internal static OwnedPlayerFileObservation Read(string kind, string path, int maximum, bool required = false)
    {
        for (var parent = Directory.GetParent(path); parent is not null; parent = parent.Parent)
        {
            if (!parent.Exists) continue;
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException();
        }
        FileAttributes attributes;
        try { attributes = File.GetAttributes(path); }
        catch (FileNotFoundException) when (!required) { return new(kind, path, null, null, null); }
        catch (DirectoryNotFoundException) when (!required) { return new(kind, path, null, null, null); }
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
            !CharacterSnapshotFileBaseline.TryValidateSafePath(path, "ownedPlayer", out _))
            throw new InvalidDataException();
        var first = new FileInfo(path);
        first.Refresh();
        if (first.Length > maximum) throw new InvalidDataException();
        var created = first.CreationTimeUtc;
        var changed = first.LastWriteTimeUtc;
        if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "ownedPlayer", out var bytes, out _) || bytes.Length > maximum)
            throw new InvalidDataException();
        var last = new FileInfo(path);
        last.Refresh();
        if (!last.Exists || last.Length != bytes.Length || last.Length != first.Length ||
            last.CreationTimeUtc != created || last.LastWriteTimeUtc != changed)
            throw new OwnedPlayerInspectionUnstableException();
        return new(kind, path, bytes, created, changed);
    }

    internal bool SameImage(OwnedPlayerFileObservation other) => Kind == other.Kind && Path == other.Path &&
        CreationUtc == other.CreationUtc && LastWriteUtc == other.LastWriteUtc &&
        (Bytes is null ? other.Bytes is null : other.Bytes is not null && Bytes.AsSpan().SequenceEqual(other.Bytes));

    internal LegacyOwnedPlayerFileEvidence Evidence() => new(Kind, Bytes is not null,
        Bytes?.LongLength ?? 0, CreationUtc, LastWriteUtc, true);
}

internal sealed class OwnedPlayerInspectionUnstableException : IOException { }
