using System.IO.Compression;
using System.Security.Cryptography;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Publishing;

public static partial class ReleasePublisher
{
    private static string Temporary(string blobs) => Path.Combine(blobs, ".publish-" + Guid.NewGuid().ToString("N") + ".tmp");

    private static UpdateFileEntry CopySource(SourceFile source, string blobs)
    {
        string temporary = Temporary(blobs);
        try
        {
            using var input = PublishFileSystem.Read(source.FullPath);
            if (input.Length != source.Length || File.GetLastWriteTimeUtc(source.FullPath) != source.LastWriteUtc)
                throw new InvalidDataException("A source file changed after plan validation.");
            string hash;
            using (var output = PublishFileSystem.New(temporary))
            {
                hash = CopyHashed(input, output, source.Length);
                output.Flush(true);
            }
            if (input.Length != source.Length || File.GetLastWriteTimeUtc(source.FullPath) != source.LastWriteUtc)
                throw new InvalidDataException("A source file changed while publishing.");
            PublishFileSystem.Check(source.FullPath);
            PromoteBlob(temporary, blobs, source.Length, hash);
            return new UpdateFileEntry(source.Entry.Path, source.Length, hash, source.Entry.BaseSha256);
        }
        finally { PublishFileSystem.DeleteOwnedTemporary(temporary); }
    }

    private static string CopyHashed(Stream input, Stream output, long expected)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = input.Read(buffer)) != 0)
        {
            total = checked(total + read);
            if (total > expected) throw new InvalidDataException("Source exceeds its validated length.");
            hash.AppendData(buffer, 0, read);
            output.Write(buffer, 0, read);
        }
        if (total != expected) throw new InvalidDataException("Source is shorter than its validated length.");
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void PromoteBlob(string temporary, string blobs, long length, string hash)
    {
        string destination = Path.Combine(blobs, hash.ToLowerInvariant());
        PublishFileSystem.Check(temporary); PublishFileSystem.Check(destination);
        if (!File.Exists(destination))
        {
            try { File.Move(temporary, destination); }
            catch (IOException) when (File.Exists(destination)) { } // Another publisher may win the same immutable hash.
        }
        PublishFileSystem.VerifyBlob(destination, length, hash);
    }

    private static string StoreBytes(string blobs, byte[] bytes)
    {
        string temporary = Temporary(blobs);
        try
        {
            using (var output = PublishFileSystem.New(temporary)) { output.Write(bytes); output.Flush(true); }
            string hash = Convert.ToHexString(SHA256.HashData(bytes));
            PromoteBlob(temporary, blobs, bytes.LongLength, hash);
            return hash;
        }
        finally { PublishFileSystem.DeleteOwnedTemporary(temporary); }
    }

    private static UpdateBundle BuildBundle(string blobs, IReadOnlyList<UpdateFileEntry> files)
    {
        string temporary = Temporary(blobs);
        try
        {
            using (var output = PublishFileSystem.New(temporary))
            {
                using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                    foreach (var file in files)
                    {
                        string blob = Path.Combine(blobs, file.Sha256.ToLowerInvariant());
                        using var input = PublishFileSystem.Read(blob);
                        if (input.Length != file.Length) throw new InvalidDataException("Bundle source length is invalid.");
                        var entry = zip.CreateEntry(file.Path, CompressionLevel.Optimal);
                        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                        using var destination = entry.Open();
                        string hash = CopyHashed(input, destination, file.Length);
                        if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase) || input.Length != file.Length)
                            throw new InvalidDataException("Bundle source blob is invalid.");
                    }
                output.Flush(true);
            }
            UpdateBundle bundle;
            using (var transport = PublishFileSystem.Read(temporary))
                bundle = new UpdateBundle(transport.Length, Convert.ToHexString(SHA256.HashData(transport)));
            PromoteBlob(temporary, blobs, bundle.Length, bundle.Sha256);
            return bundle;
        }
        finally { PublishFileSystem.DeleteOwnedTemporary(temporary); }
    }
}
