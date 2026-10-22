using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Install;

internal sealed class InstallFileSystem
{
    private readonly HashSet<string> _caseChecked = new(StringComparer.OrdinalIgnoreCase);
    internal string Root { get; }
    internal string Control => Path.Combine(Root, ".wolfconquer-updates");
    internal string StatePath => Path.Combine(Control, "state.json");
    internal InstallFileSystem(string root)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Durable client installation requires Windows.");
        if (!Path.IsPathFullyQualified(root) || root.StartsWith("\\\\", StringComparison.Ordinal))
            throw new InvalidDataException("Install root must be an explicit local absolute directory.");
        Root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        if (string.Equals(Root, Path.GetPathRoot(Root)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A volume root is not an install directory.");
        Check(Root);
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException(Root);
    }

    internal string Target(string relative)
    {
        UpdatePathPolicy.ValidateRelativePath(relative);
        var full = Path.GetFullPath(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));
        Check(full);
        return full;
    }

    internal void Check(string path)
    {
        var full = Path.GetFullPath(path);
        if (!string.Equals(full, Root, StringComparison.OrdinalIgnoreCase) &&
            !full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Path escaped the explicit install root.");
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Reparse points are not permitted in an install path.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            var parent = Path.GetDirectoryName(current);
            if (parent is not null && Directory.Exists(parent) && _caseChecked.Add(parent))
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (Directory.EnumerateFileSystemEntries(parent).Any(entry => !names.Add(Path.GetFileName(entry))))
                    throw new InvalidDataException("Existing path names collide under Windows comparison.");
            }
        }
    }

    internal void DirectoryFor(string path)
    {
        Check(path);
        Directory.CreateDirectory(path);
        Check(path);
    }

    internal string? Hash(string path)
    {
        Check(path);
        if (Directory.Exists(path)) throw new IOException("A file path names an existing directory.");
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    internal byte[]? Read(string path, long maximum = 16L * 1024 * 1024)
    {
        Check(path);
        if (Directory.Exists(path)) throw new IOException("A metadata file path names a directory.");
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximum) throw new InvalidDataException("Updater metadata exceeds its bound.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    internal void WriteNew(string path, ReadOnlySpan<byte> bytes)
    {
        Check(path);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    internal void Copy(string source, string destination)
    {
        Check(source); Check(destination);
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.WriteThrough);
        input.CopyTo(output);
        output.Flush(flushToDisk: true);
    }

    internal void AtomicMove(string source, string destination)
    {
        Check(source); Check(destination);
        // Same-volume control/staging paths; WRITE_THROUGH makes the rename durable.
        if (!MoveFileExW(source, destination, 0x1 | 0x8))
            throw new IOException("Atomic update move failed.", new Win32Exception(Marshal.GetLastWin32Error()));
        Check(destination);
    }

    internal void AtomicWrite(string destination, ReadOnlySpan<byte> bytes)
    {
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        WriteNew(temporary, bytes);
        AtomicMove(temporary, destination);
    }

    internal static bool Same(string? first, string? second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    internal static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileExW(string existing, string replacement, uint flags);
}
