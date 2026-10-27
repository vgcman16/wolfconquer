using System.Security.AccessControl;
using System.Security.Principal;

namespace WolfConquer.Core.World;

public sealed partial class UnknownPacketAndFieldLogger
{
    private static FileStream OpenPrivateOutput(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            throw new ArgumentException("A pre-existing absolute private diagnostics parent is required.");
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        RejectReparseAncestors(parent);
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException();
        var root = Path.Combine(parent, "world-wire-" + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Diagnostic owner collision.");
        CreatePrivateDirectory(root);
        RejectReparseAncestors(root);
        var file = Path.Combine(root, "packets.ndjson");
        var fileOptions = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.Read,
            BufferSize = 4096, Options = FileOptions.None,
        };
        if (!OperatingSystem.IsWindows())
            fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(file, fileOptions);
    }

    private static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User ?? throw new UnauthorizedAccessException();
            var security = new DirectorySecurity();
            security.SetOwner(sid);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
            FileSystemAclExtensions.Create(new DirectoryInfo(path), security);
            VerifyPrivateWindowsDirectory(path, sid);
        }
        else
        {
            var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            Directory.CreateDirectory(path, mode);
            if ((File.GetUnixFileMode(path) & ~mode) != 0) throw new UnauthorizedAccessException();
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void VerifyPrivateWindowsDirectory(string path, SecurityIdentifier sid)
    {
        var security = FileSystemAclExtensions.GetAccessControl(new DirectoryInfo(path));
        if (!security.AreAccessRulesProtected || !sid.Equals(security.GetOwner(typeof(SecurityIdentifier))))
            throw new UnauthorizedAccessException();
        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));
        if (rules.Count != 1 || rules[0] is not FileSystemAccessRule rule ||
            !sid.Equals(rule.IdentityReference) || rule.AccessControlType != AccessControlType.Allow ||
            rule.FileSystemRights != FileSystemRights.FullControl) throw new UnauthorizedAccessException();
    }

    private static void RejectReparseAncestors(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse diagnostic output is not permitted.");
    }
}
