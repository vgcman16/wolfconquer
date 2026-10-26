namespace WolfConquer.Updates.Publishing;

internal static partial class NativeAssetSource
{
    internal static void ValidateMembership(string root, IReadOnlyCollection<NativeAssetReview> reviews)
    {
        if (reviews.Count == 0) return;
        ValidateRootSpelling(root);
        foreach (var group in reviews.Where(row => row.Category == NativeAssetReviewSet.ObjectDirectory)
                     .GroupBy(row => row.CollisionPath, StringComparer.Ordinal))
            ValidateObjectTree(root, group.Key, group.ToArray());
    }

    private static void ValidateObjectTree(string root, string collision, NativeAssetReview[] rows)
    {
        string physical = ExactNode(root, collision, directory: true);
        string[] expected = rows.Select(row => row.Path).Order(StringComparer.Ordinal).ToArray();
        var seenFiles = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<(string Physical, string Relative)>();
        pending.Push((physical, collision));
        int visited = 0;
        // Every accepted directory must prefix a reviewed path. This derived
        // node bound is finite without allocating a trie for all path prefixes.
        int nodeBound = rows.Sum(row => row.Path.Length - collision.Length);
        while (pending.TryPop(out var node))
        {
            Ordinary(node.Physical, directory: true);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string child in Directory.EnumerateFileSystemEntries(node.Physical))
            {
                if (++visited > nodeBound) throw new InvalidDataException("Reviewed object membership exceeds its bound.");
                string name = Path.GetFileName(child);
                if (!names.Add(name)) throw new InvalidDataException("Reviewed object names collide.");
                string relative = node.Relative + "/" + name;
                FileAttributes attributes = File.GetAttributes(child);
                if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                    throw new InvalidDataException("Reviewed object membership contains an unsupported node.");
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (!PrefixesReviewedFile(expected, relative))
                        throw new InvalidDataException("Reviewed object membership contains an unreviewed directory.");
                    pending.Push((child, relative));
                }
                else if (Array.BinarySearch(expected, relative, StringComparer.Ordinal) < 0 || !seenFiles.Add(relative))
                    throw new InvalidDataException("Reviewed object membership contains an unreviewed file.");
            }
            Ordinary(node.Physical, directory: true);
        }
        if (seenFiles.Count != rows.Length)
            throw new InvalidDataException("Reviewed object membership is missing a reviewed file.");
    }

    private static bool PrefixesReviewedFile(string[] expected, string directory)
    {
        string prefix = directory + "/";
        int index = Array.BinarySearch(expected, prefix, StringComparer.Ordinal);
        if (index < 0) index = ~index;
        return index < expected.Length && expected[index].StartsWith(prefix, StringComparison.Ordinal);
    }
}
