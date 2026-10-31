using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyOwnedAdmissionRuntime
{
    private AuthenticatedCreationStartupAuthority? activeCreationStartup;
    private bool creationStartupComplete;

    internal sealed class AuthenticatedCreationStartupAuthority
    {
        private readonly LegacyOwnedAdmissionRuntime runtime;
        private readonly int thread;
        private AuthenticatedCreationStartupAuthority(LegacyOwnedAdmissionRuntime runtime)
        { this.runtime = runtime; thread = Environment.CurrentManagedThreadId; }

        internal static AuthenticatedCreationStartupAuthority Capture(LegacyOwnedAdmissionRuntime runtime)
        {
            if (!Monitor.IsEntered(runtime.gate) || runtime.frame is not null || runtime.closed || runtime.stopping ||
                runtime.creationStartupComplete || runtime.activeCreationStartup is not null ||
                !runtime.Ordinary.IsCurrent(runtime.configured.Roots))
                throw new InvalidDataException("Creation recovery requires the actual unpublished startup Runtime.");
            return new(runtime);
        }

        internal LegacyOwnedAdmissionOptions Configuration => runtime.configured;
        internal LegacyItemUidRegistry Registry => runtime.registry;
        internal LegacyOrdinaryAdmissionUidOwner Ordinary => runtime.Ordinary;
        internal bool IsCurrentPlain(LegacyItemUidRegistry actual) => Monitor.IsEntered(runtime.gate) &&
            thread == Environment.CurrentManagedThreadId && ReferenceEquals(runtime.registry, actual) &&
            ReferenceEquals(runtime.activeCreationStartup, this) && runtime.frame is null && !runtime.closed &&
            !runtime.stopping && !runtime.creationStartupComplete && !runtime.runeDecomposeStartupComplete;
    }

    private bool TryCompleteAuthenticatedCreationStartup(out string reason)
    {
        lock (gate)
        {
            reason = "authenticatedCreationStartupUnavailable";
            if (closed || stopping || frame is not null || creationStartupComplete || activeCreationStartup is not null)
                return false;
            activeCreationStartup = AuthenticatedCreationStartupAuthority.Capture(this);
            try
            {
                if (!LegacyAuthenticatedOrdinaryCreationStartup.TryComplete(activeCreationStartup, out reason)) return false;
                creationStartupComplete = true; reason = string.Empty; return true;
            }
            finally { activeCreationStartup = null; }
        }
    }
}
