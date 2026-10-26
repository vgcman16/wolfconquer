using System.Security.Cryptography;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Publishing;

internal static partial class NativeAssetSource
{
    internal static void ValidateRootSpelling(string root)
    {
        string volume = Path.GetPathRoot(root)!;
        string current = volume;
        Ordinary(current, directory: true);
        foreach (string segment in Path.GetRelativePath(volume, root).Split(Path.DirectorySeparatorChar))
            current = ExactChild(current, segment, directory: true);
        if (!current.Equals(root, StringComparison.Ordinal))
            throw new InvalidDataException("Reviewed source root spelling is not exact.");
    }

    internal static string ExactFile(string root, string relative) => ExactNode(root, relative, directory: false);

    private static string ExactNode(string root, string relative, bool directory)
    {
        string current = root;
        string[] segments = relative.Split('/');
        for (int index = 0; index < segments.Length; index++)
            current = ExactChild(current, segments[index], index < segments.Length - 1 || directory);
        return current;
    }

    private static string ExactChild(string parent, string segment, bool directory)
    {
        Ordinary(parent, directory: true);
        string? selected = null;
        int shown = 0;
        foreach (string child in Directory.EnumerateFileSystemEntries(parent))
        {
            if (++shown > SignedUpdateManifest.MaximumFiles + NativeAssetReviewSet.MaximumReviews)
                throw new InvalidDataException("Reviewed source sibling validation exceeds its bound.");
            string name = Path.GetFileName(child);
            if (!name.Equals(segment, StringComparison.OrdinalIgnoreCase)) continue;
            if (selected is not null || !name.Equals(segment, StringComparison.Ordinal))
                throw new InvalidDataException("Reviewed source names are ambiguous or differ in case.");
            selected = child;
        }
        if (selected is null) throw new InvalidDataException("Reviewed source node is missing or uses an alias.");
        Ordinary(parent, directory: true);
        Ordinary(selected, directory);
        return selected;
    }

    private static void Ordinary(string path, bool directory)
    {
        PublishFileSystem.Check(path);
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0 ||
            ((attributes & FileAttributes.Directory) != 0) != directory)
            throw new InvalidDataException("Reviewed source node has an unsupported physical type.");
    }

    internal static void VerifyDigest(FileStream stream, NativeAssetReview review)
    {
        if (stream.Length != review.Length)
            throw new InvalidDataException("Reviewed source length differs from its review.");
        string hash = Convert.ToHexString(SHA256.HashData(stream));
        if (stream.Length != review.Length || stream.Position != review.Length)
            throw new InvalidDataException("Reviewed source changed while its digest was verified.");
        VerifyCopiedDigest(review, stream.Length, hash);
    }

    internal static void VerifyCopiedDigest(NativeAssetReview review, long length, string hash)
    {
        if (length != review.Length || !hash.Equals(review.ExpectedSourceSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Reviewed source content differs from its review.");
    }
}
