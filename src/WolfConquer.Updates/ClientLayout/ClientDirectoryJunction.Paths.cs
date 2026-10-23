using Microsoft.Win32.SafeHandles;
using System.Text;

namespace WolfConquer.Updates.ClientLayout;

internal static partial class ClientDirectoryJunction
{
    private static DirectoryPins ValidatePair(string path, string target, out string link, out string destination)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Client junctions require local Windows NTFS.");
        link = ValidateSyntax(path);
        destination = ValidateSyntax(target);
        if (Within(link, destination) || Within(destination, link))
            throw new InvalidDataException("Junction path and target must not overlap.");
        var pins = new DirectoryPins();
        try
        {
            pins.AddChain(Path.GetDirectoryName(link)!);
            pins.AddChain(destination);
            return pins;
        }
        catch { pins.Dispose(); throw; }
    }

    private static string ValidateSyntax(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length is < 4 or > 2048 ||
            !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\' || path.Contains('/'))
            throw new InvalidDataException("Use a bounded local absolute DOS directory path, never UNC/device/volume paths.");
        try { _ = StrictUnicode.GetByteCount(path); }
        catch (EncoderFallbackException error) { throw new InvalidDataException("Directory path is invalid UTF-16.", error); }
        // Reuse Windows lexical safeguards, without the updater's reserved-name
        // policy: these are explicit absolute roots, not install manifest paths.
        var tail = path[3..];
        ValidateSegments(tail);
        var full = Path.GetFullPath(path);
        if (!string.Equals(full, path, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Directory path aliases are refused.");
        return char.ToUpperInvariant(full[0]) + full[1..];
    }

    private static void ValidateSegments(string tail)
    {
        foreach (var segment in tail.Split('\\'))
        {
            if (segment.Length is 0 or > 255 || segment is "." or ".." ||
                segment.EndsWith('.') || segment.EndsWith(' ') ||
                segment.Any(character => char.IsControl(character) || ":<>\"|?*".Contains(character)))
                throw new InvalidDataException("Unsafe directory segment or path alias.");
            var stem = segment.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CLOCK$" or "CONIN$" or "CONOUT$" ||
                ((stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                    stem.Length == 4 && "123456789¹²³".Contains(stem[3])))
                throw new InvalidDataException("Windows device aliases are refused.");
        }
    }

    private static bool Within(string root, string path) =>
        path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + '\\', StringComparison.OrdinalIgnoreCase);

    private static void RequireHandle(SafeFileHandle handle, string path, bool reparse)
    {
        var attributes = ReadInformation(handle).Attributes;
        if ((attributes & FileAttributes.Directory) == 0 || ((attributes & FileAttributes.ReparsePoint) != 0) != reparse)
            throw new InvalidDataException(reparse ? "Expected a directory reparse point, not an ordinary entry." : "Expected an ordinary directory without a reparse point.");
        var result = new StringBuilder(4096);
        var count = GetFinalPathNameByHandleW(handle, result, (uint)result.Capacity, 0);
        if (count == 0) throw NativeError("Cannot identify the opened directory.");
        if (count >= result.Capacity || !result.ToString().Equals(NativePath(path), StringComparison.Ordinal))
            throw new InvalidDataException("Opened directory is a short-name, case, volume, or redirected path alias.");
    }

    private static void RequireNtfs(SafeFileHandle handle)
    {
        var name = new StringBuilder(32);
        if (!GetVolumeInformationByHandleW(handle, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, out var flags, name, (uint)name.Capacity))
            throw NativeError("Cannot identify the directory filesystem.");
        if (!name.ToString().Equals("NTFS", StringComparison.OrdinalIgnoreCase) || (flags & 0x80) == 0)
            throw new InvalidDataException("The exact directory must be on local NTFS with reparse-point support.");
    }

    private sealed class DirectoryPins : IDisposable
    {
        private readonly Dictionary<string, SafeFileHandle> _handles = new(StringComparer.Ordinal);

        internal void AddChain(string path)
        {
            var root = Path.GetPathRoot(path)!;
            Pin(root);
            var current = root;
            foreach (var segment in path[root.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                Pin(current);
            }
        }

        private void Pin(string path)
        {
            if (_handles.ContainsKey(path)) return;
            // Attribute-only opens do not establish the read/share constraint
            // needed to prevent an ordinary rename of this parent directory.
            // Actual list access participates in sharing. Permit writes because
            // CreateNew creates a child and ordinary child replacement must work;
            // deny delete sharing, not arbitrary child-namespace mutation.
            var handle = OpenDirectory(path, ReadAttributes | ListDirectory, ShareRead | ShareWrite);
            try
            {
                RequireHandle(handle, path, reparse: false);
                RequireNtfs(handle);
                _handles.Add(path, handle);
            }
            catch { handle.Dispose(); throw; }
        }

        public void Dispose()
        {
            foreach (var handle in _handles.Values.Reverse()) handle.Dispose();
            _handles.Clear();
        }
    }
}
