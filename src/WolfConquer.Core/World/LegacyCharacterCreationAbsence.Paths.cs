using System.Globalization;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyCharacterCreationAbsence
{
    private RootMembership CaptureMembership(string root,
        IReadOnlyDictionary<LegacyTradeJournalFileKind, LegacyTradeJournalChange>? changes = null, string? transactionId = null)
    {
        RequireSafeDirectory(root, allowMissing: true);
        var entries = new Dictionary<string, FileAttributes>(PathComparer);
        if (!PathExists(root)) return new(root, false, entries);
        var ownLeaves = targets.Values.Where(path => PathComparer.Equals(Path.GetDirectoryName(path), root))
            .Select(Path.GetFileName).ToArray();
        int count = 0;
        foreach (var path in Directory.EnumerateFileSystemEntries(root))
        {
            if (++count > ushort.MaxValue) throw new InvalidDataException("Creation participant membership exceeds 65535 entries.");
            RequireSafePath(path);
            var leaf = Path.GetFileName(path);
            RequireKnownScratchOrOrdinaryEntry(path, leaf, changes, transactionId);
            foreach (var own in ownLeaves)
            {
                if (!string.Equals(Path.GetExtension(leaf), Path.GetExtension(own), StringComparison.OrdinalIgnoreCase) ||
                    !uint.TryParse(Path.GetFileNameWithoutExtension(leaf), NumberStyles.None, CultureInfo.InvariantCulture,
                        out var id) || id.ToString(CultureInfo.InvariantCulture) != Path.GetFileNameWithoutExtension(own)) continue;
                if (leaf != own) throw new InvalidDataException("Creation participant has a noncanonical account alias.");
            }
            if (!entries.TryAdd(leaf, File.GetAttributes(path)))
                throw new InvalidDataException("Creation participant membership has an ambiguous path alias.");
        }
        RequireSafeDirectory(root, allowMissing: false);
        return new(root, true, entries);
    }

    private void RequireKnownScratchOrOrdinaryEntry(string path, string leaf,
        IReadOnlyDictionary<LegacyTradeJournalFileKind, LegacyTradeJournalChange>? changes, string? transactionId)
    {
        var journal = Path.Combine(configuration.UsersRoot, ".managed-trade-journal");
        if (PathComparer.Equals(path, journal))
        { RequireSafeDirectory(path, allowMissing: false); return; }
        bool knownStage = changes is not null && changes.Keys.Any(kind =>
            PathComparer.Equals(path, targets[kind] + ".managed-trade-" + transactionId + ".tmp"));
        if ((leaf.Contains(".managed-trade-", StringComparison.OrdinalIgnoreCase) ||
                leaf.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) || leaf.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) &&
            !knownStage)
            throw new InvalidDataException("Creation participant contains foreign or incomplete staging evidence.");
        if (!Directory.Exists(path)) return;
        // These source formats have flat account leaves. The two real metadata
        // directories have independent journal/registry custody; arbitrary
        // nested directories cannot hide otherwise unscanned foreign stages.
        if (!PathComparer.Equals(path, Path.Combine(configuration.ItemsRoot, ".managed-item-uids")))
            throw new InvalidDataException("Creation participant contains an unsupported nested directory.");
        RequireSafeDirectory(path, allowMissing: false);
    }

    private static void RequireAbsent(string path)
    {
        RequireSafePath(path);
        if (PathExists(path)) throw new InvalidDataException("Creation participant already exists.");
    }

    internal static void RequireSafeDirectory(string path, bool allowMissing)
    {
        RequireSafePath(path);
        if (!TryAttributes(path, out var attributes))
        {
            if (allowMissing) return;
            throw new InvalidDataException("Creation participant root is missing.");
        }
        if ((attributes & FileAttributes.Directory) == 0)
            throw new InvalidDataException("Creation participant root is a file.");
    }

    internal static void RequireSafePath(string path)
    {
        var current = Path.GetFullPath(path);
        bool leaf = true;
        while (!string.IsNullOrEmpty(current))
        {
            if (TryAttributes(current, out var attributes))
            {
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Creation path contains a reparse point.");
                if (!leaf && (attributes & FileAttributes.Directory) == 0)
                    throw new InvalidDataException("Creation path ancestor is a file.");
            }
            var parent = Path.GetDirectoryName(current);
            if (parent == current) break;
            current = parent; leaf = false;
        }
    }

    private static bool PathExists(string path) => TryAttributes(path, out _);

    private static bool TryAttributes(string path, out FileAttributes attributes)
    {
        try { attributes = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { attributes = default; return false; }
        catch (DirectoryNotFoundException) { attributes = default; return false; }
    }
}
