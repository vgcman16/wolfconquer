using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed partial class ExperienceContinuationExecution
    {
        // Unwired: the actual cold or interrupted owner supplies the same
        // admitted pending holder, never an old process's publication receipt.
        internal bool TryPreparePendingQuery(out WorldPacketHandlers.PreparedLevelUpRebuild? rebuild)
        {
            rebuild = null;
            if (!TryIssueResumeQueryPurpose(out var purpose) || purpose is null) return false;
            lock (frame.RequireActionLifecycleGate(store))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages, current);
                if (!store._connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
                lock (gate)
                {
                    using var native = EnterNativeConnectionGate();
                    using var actors = new SpellActorScope(binding);
                    if (!WorldPacketHandlers.PreparedLevelUpRebuild.TryPrepareResume(context, purpose, binding, current, out var plan) || plan is null)
                        return false;
                    string path = Path.Combine(binding.UsersRoot, current.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
                    string[] paths = plan.LockedSpellPath is { } spells ? [path, spells] : [path];
                    bool ready = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                    {
                        if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceContinuationResumeQuery", out var saved, out _) ||
                            !IsHeldCurrentPlain(saved) || !purpose.CoversResume(context, binding, current) || !plan.IsCurrentPlain()) return false;
                        plan.BindCommitted(current);
                        return plan.IsCurrentPlain();
                    });
                    if (!ready) return false;
                    rebuild = plan; return true;
                }
            }
        }
    }
}
