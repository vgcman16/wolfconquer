using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Updates.ClientLayout;

// Narrow local-NTFS mount-point primitive. No privilege adjustment, subprocess,
// symlink fallback, recursive deletion, asset copying, or target mutation.
internal static partial class ClientDirectoryJunction
{
    internal static void CreateNew(string path, string target)
    {
        using var pins = ValidatePair(path, target, out var link, out var destination);
        var data = EncodeMountPoint(destination);
        if (!CreateDirectoryW(NativePath(link), IntPtr.Zero))
            throw NativeError("Cannot create a NEW junction directory.");
        SafeFileHandle? handle = null;
        try
        {
            handle = OpenDirectory(link, ReadAttributes | GenericWrite | DeleteAccess, 0);
            RequireHandle(handle, link, reparse: false);
            RequireNtfs(handle);
            if (!DeviceIoControl(handle, SetReparsePoint, data, (uint)data.Length, null, 0, out _, IntPtr.Zero))
                throw NativeError("Cannot set the mount-point reparse data; no privilege fallback is attempted.");
            RequireExact(handle, link, destination);
        }
        catch (Exception original)
        {
            Exception? cleanup = null;
            try
            {
                if (handle is null) throw new IOException("No pinned identity is available for safe creation cleanup.");
                if ((ReadInformation(handle).Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    RequireExact(handle, link, destination);
                    RemoveMountPointData(handle);
                }
                RequireHandle(handle, link, reparse: false);
                MarkEmptyDirectoryForDeletion(handle);
            }
            catch (Exception error) { cleanup = error; }
            throw new IOException(cleanup is null
                ? "Junction creation failed; the new directory was marked for nonrecursive removal."
                : "Junction creation failed; a new directory may remain and needs inspection. No unverified path was removed.",
                cleanup is null ? original : new AggregateException(original, cleanup));
        }
        finally { handle?.Dispose(); }
    }

    internal static bool IsExact(string path, string target)
    {
        using var pins = ValidatePair(path, target, out var link, out var destination);
        using var handle = TryOpenDirectory(link, ReadAttributes, ShareRead);
        if (handle is null) return false;
        RequireExact(handle, link, destination);
        return true;
    }

    internal static void DeleteExact(string path, string target)
    {
        using var pins = ValidatePair(path, target, out var link, out var destination);
        using var handle = OpenDirectory(link, ReadAttributes | GenericWrite | DeleteAccess, 0);
        RequireExact(handle, link, destination);
        // Remove only the tag on this exact no-follow handle, then delete its
        // now-ordinary empty directory on that SAME handle. Never reopen by path.
        RemoveMountPointData(handle);
        try
        {
            RequireHandle(handle, link, reparse: false);
            MarkEmptyDirectoryForDeletion(handle);
        }
        catch (Exception error)
        {
            throw new IOException("Junction data was removed, but its ordinary directory could not be removed. Target assets were not traversed.", error);
        }
    }

    private static void RequireExact(SafeFileHandle handle, string path, string target)
    {
        RequireHandle(handle, path, reparse: true);
        RequireNtfs(handle);
        var buffer = new byte[MaximumReparseBytes];
        if (!DeviceIoControl(handle, GetReparsePoint, null, 0, buffer, (uint)buffer.Length, out var count, IntPtr.Zero))
            throw NativeError("Cannot query exact junction data.");
        ValidateMountPoint(buffer, count, target);
    }
}
