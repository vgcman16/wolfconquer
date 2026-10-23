using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Core.Data;

internal sealed partial class WindowsJournalRootIdentity
{
    private static RootCapture CaptureRoot(string configured)
    {
        var path = NormalizeConfiguredPath(configured);
        var chain = new List<DirectoryPin>();
        var original = OpenOrdinaryDirectory(path);
        try
        {
            var identity = ReadIdentity(original);
            // The returned string is only a route for additional real opens.
            // Its spelling is never compared to decide identity or authority.
            foreach (var ancestor in GetVolumeChain(ReadFinalVolumePath(original)))
            {
                var handle = OpenOrdinaryDirectory(ancestor);
                try { chain.Add(new(handle, ReadIdentity(handle))); }
                catch { handle.Dispose(); throw; }
            }
            if (chain[^1].Identity != identity)
                throw new IOException("The resolved chain no longer names the captured configured root.");
            var result = new RootCapture(original, identity, chain.ToArray());
            result.RequireCapturedIdentities();
            return result;
        }
        catch
        {
            for (var index = chain.Count - 1; index >= 0; index--) chain[index].Handle.Dispose();
            original.Dispose();
            throw;
        }
    }

    private static string NormalizeConfiguredPath(string configured)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configured);
        // Exactly one additional local acquisition form, not arbitrary device or
        // UNC support. Extended paths bypass Win32 normalization, so every actual
        // absolute component is checked before any CreateFile call.
        if (configured.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryVolumeComponents(configured, out _, out _))
                throw new ArgumentException("A complete local volume-GUID directory path is required.", nameof(configured));
            return GetVolumeChain(configured)[^1];
        }
        // Existing ordinary DOS behavior (including real short/subst spellings).
        if (configured.Length < 3 || !char.IsAsciiLetter(configured[0]) ||
            configured[1] != ':' || configured[2] != '\\' || configured.Contains('/'))
            throw new ArgumentException("An ordinary DOS or exact local volume-GUID directory path is required.", nameof(configured));
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
    }

    private static string[] GetVolumeChain(string finalPath)
    {
        if (!TryVolumeComponents(finalPath, out var volume, out var parts))
            throw new IOException("The directory did not resolve to an ordinary local volume GUID path.");
        var chain = new List<string>(parts.Length + 1) { volume };
        var current = volume;
        foreach (var part in parts)
        {
            current += (current.EndsWith('\\') ? string.Empty : "\\") + part;
            chain.Add(current);
        }
        return chain.ToArray();
    }

    private static bool TryVolumeComponents(string path, out string volume, out string[] parts)
    {
        volume = string.Empty;
        parts = [];
        const string prefix = @"\\?\Volume{";
        const int guidLength = 36;
        var boundary = prefix.Length + guidLength;
        if (path.Length >= 32_768 || !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            path.Length < boundary + 2 || path[boundary] != '}' || path[boundary + 1] != '\\' ||
            !Guid.TryParseExact(path.AsSpan(prefix.Length, guidLength), "D", out _)) return false;
        volume = path[..(boundary + 2)];
        var suffix = path[(boundary + 2)..];
        if (suffix.Length == 0) return true; // Actual volume root requires its slash.
        if (suffix.EndsWith('\\')) suffix = suffix[..^1];
        if (suffix.Length == 0) return false; // Empty/repeated absolute component.
        parts = suffix.Split('\\');
        if (parts.Length > 256) return false;
        foreach (var part in parts)
            if (part.Length == 0 || part is "." or ".." || part.Any(value => value < 32 ||
                value is ':' or '/' or '\0' or '<' or '>' or '"' or '|' or '?' or '*')) return false;
        return true;
    }

    private readonly record struct FileIdentity(ulong VolumeSerial, ulong Low, ulong High);
    private sealed record DirectoryPin(SafeFileHandle Handle, FileIdentity Identity);

    private sealed class RootCapture(SafeFileHandle original, FileIdentity identity, DirectoryPin[] chain) : IDisposable
    {
        internal FileIdentity Identity => identity;
        internal bool AncestorsContain(FileIdentity candidate)
        {
            for (var index = 0; index < chain.Length - 1; index++)
                if (chain[index].Identity == candidate) return true;
            return false;
        }

        internal void RequireCapturedIdentities()
        {
            RequireOrdinaryDirectory(original);
            if (ReadIdentity(original) != identity) throw new IOException("A retained root identity changed.");
            foreach (var pin in chain)
            {
                RequireOrdinaryDirectory(pin.Handle);
                if (ReadIdentity(pin.Handle) != pin.Identity) throw new IOException("A retained ancestor identity changed.");
            }
        }

        public void Dispose()
        {
            for (var index = chain.Length - 1; index >= 0; index--) chain[index].Handle.Dispose();
            original.Dispose();
        }
    }
}
