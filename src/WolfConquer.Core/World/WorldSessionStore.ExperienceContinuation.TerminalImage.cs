using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed partial class ExperienceContinuationExecution
    {
        private WorldSession? terminalSession;
        private ActionPublication? terminalReceipt;
        private byte[]? terminalImage;

        // Called only by this execution's actual clear writer, inside its file
        // coordinator and after exact durable-image/native/holder confirmation.
        private void RecordTerminalClear(WorldSession final, ActionPublication receipt, byte[] verifiedAfter)
        {
            if (terminalSession is not null || terminalReceipt is not null || terminalImage is not null ||
                !Monitor.IsEntered(frame.RequireActionLifecycleGate(store)) ||
                !store.IsLevelUpRebuildOwnerHeld(binding, final, context) ||
                !ReferenceEquals(receipt.Published, final) || pending.Phase != LegacyExperienceContinuationPhase.Ready)
                throw new InvalidOperationException("experienceContinuationTerminalClearNotOwned");
            terminalSession = final; terminalReceipt = receipt; terminalImage = verifiedAfter;
        }

        internal bool IsTerminalClearCurrent(WorldSession final, ActionPublication receipt)
        {
            if (!ReferenceEquals(final, terminalSession) || !ReferenceEquals(receipt, terminalReceipt) || terminalImage is null ||
                !store._connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
            if (IsCurrentThreadInsideAnyConnectionGate)
                return Monitor.IsEntered(gate) && IsTerminalClearHeld(final, receipt);
            if (Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot)) return false;
            lock (frame.RequireActionLifecycleGate(store))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages, final);
                lock (gate)
                {
                    using var native = EnterNativeConnectionGate();
                    using var actors = new SpellActorScope(binding);
                    return IsTerminalClearHeld(final, receipt);
                }
            }
        }

        private bool IsTerminalClearHeld(WorldSession final, ActionPublication receipt)
        {
            if (!ReferenceEquals(final, terminalSession) || !ReferenceEquals(receipt, terminalReceipt) || terminalImage is null ||
                !Monitor.IsEntered(frame.RequireActionLifecycleGate(store)) ||
                !store.IsLevelUpRebuildOwnerHeld(binding, final, context) ||
                !receipt.TryCurrent(out var current) || !ReferenceEquals(current, final) ||
                !binding.Recovery.TryRequireLiveJournalClear(out _)) return false;
            string path = Path.Combine(binding.UsersRoot, binding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            return CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
                CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceContinuationTerminalCurrent", out var image, out _) &&
                image.AsSpan().SequenceEqual(terminalImage) &&
                store.IsLevelUpRebuildOwnerHeld(binding, final, context) &&
                LegacyExperienceContinuationCodec.ReadCharacter(final.Character, final.AccountId) is null);
        }
    }
}
