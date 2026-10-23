namespace WolfConquer.Tests;

public sealed partial class DataToolClientNativeLaunchProbeTests
{
    private static void DeleteOwnedNativeLaunchTempRoot(string tempRoot)
    {
        const string prefix = "wolfconquer-client-native-launch-probe-";
        var root = Path.GetFullPath(tempRoot).TrimEnd(Path.DirectorySeparatorChar);
        var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        var name = Path.GetFileName(root);
        if (!string.Equals(Path.GetDirectoryName(root), parent, StringComparison.OrdinalIgnoreCase) ||
            !name.StartsWith(prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(name[prefix.Length..], "N", out _))
        {
            throw new InvalidOperationException("Refusing cleanup outside the exact owned native-launch fixture.");
        }

        if (!Directory.Exists(root))
        {
            return;
        }

        var directories = new List<string> { root };
        var files = new List<string>();
        for (var index = 0; index < directories.Count; index++)
        {
            var directory = directories[index];
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Refusing reparse-point fixture cleanup.");
            }

            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var full = Path.GetFullPath(entry);
                if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Fixture child escaped its exact root.");
                }

                var attributes = File.GetAttributes(full);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException("Refusing reparse-point fixture child cleanup.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    directories.Add(full);
                }
                else
                {
                    files.Add(full);
                }
            }
        }

        // Full target preflight precedes any deletion. Delete explicit owned
        // files/directories only; no recursive root/glob deletion.
        foreach (var file in files)
        {
            File.Delete(file);
        }
        for (var index = directories.Count - 1; index >= 0; index--)
        {
            Directory.Delete(directories[index], recursive: false);
        }
    }
}
