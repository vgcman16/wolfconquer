using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyTradeRecoveryState
{
    private static readonly ConditionalWeakTable<ExperienceEscrowRecoveryPermit, LegacyTradeRecoveryState> IssuedExperienceEscrowPermits = new();
    // Issued only by this configured world's actual startup recovery while
    // its recovery gate is held and the earlier joint journal is clear.
    // This is not a general permission to edit a pending Character image.
    internal sealed class ExperienceEscrowRecoveryPermit
    {
        private readonly LegacyTradeRecoveryState owner;
        private readonly string root;
        internal ExperienceEscrowRecoveryPermit(LegacyTradeRecoveryState owner, string root)
        { this.owner = owner; this.root = root; }

        internal bool IsCurrentFor(string path)
        {
            try
            {
                return IssuedExperienceEscrowPermits.TryGetValue(this, out var actual) && ReferenceEquals(actual, owner) &&
                    Monitor.IsEntered(owner._gate) && !owner._initialized && owner._failureReason is null &&
                    owner._admissionRoots is { } configured &&
                    string.Equals(configured.Users, root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                    string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), root,
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                    Path.GetExtension(path).Equals(".ini", StringComparison.OrdinalIgnoreCase) &&
                    !LegacyTradeJournal.HasPending(root);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
            { return false; }
        }
    }

    private ExperienceEscrowRecoveryPermit? CaptureExperienceEscrowRecoveryPermit(string root)
    {
        if (!Monitor.IsEntered(_gate) || _initialized || _failureReason is not null || _admissionRoots is not { } configured ||
            !string.Equals(configured.Users, Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            LegacyTradeJournal.HasPending(configured.Users)) return null;
        var permit = new ExperienceEscrowRecoveryPermit(this, configured.Users);
        IssuedExperienceEscrowPermits.Add(permit, this);
        return permit;
    }
}
