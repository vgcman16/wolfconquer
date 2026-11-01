using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed class OwnedProficiencyPublicationData
    {
        internal readonly WorldSessionStore Store;
        internal readonly WorldServerFrameHandler Frame;
        internal readonly WorldPacketHandlerContext Context;
        internal readonly LegacyOrdinarySpellBinding Binding;
        internal readonly object Identity;
        internal readonly WorldSession Before, After;
        internal readonly WorldPacketHandlers.BasicPhysicalProficiencyCalculation Calculation;
        internal readonly LegacyNativeProficiencyStorage.PreparedSessionProficiencyMap Native;
        internal readonly LegacySavedProficiencyImage BeforeImage;
        internal LegacySavedProficiencyImage AfterImage = null!;
        private byte[]? beforeBytes;
        private byte[] afterBytes = null!;
        internal string Path = null!;
        internal bool Committed, FileWritten;

        internal OwnedProficiencyPublicationData(WorldSessionStore store, WorldServerFrameHandler frame,
            WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding, object identity,
            WorldSession before, WorldSession after, WorldPacketHandlers.BasicPhysicalProficiencyCalculation calculation,
            LegacyNativeProficiencyStorage.PreparedSessionProficiencyMap native)
        {
            Store = store; Frame = frame; Context = context; Binding = binding; Identity = identity;
            Before = before; After = after; Calculation = calculation; Native = native; BeforeImage = binding.SavedProficiencies;
        }

        internal void PrepareFile()
        {
            string root = Frame.RequireBasicPhysicalProficiencyRoot(Store, Binding);
            if (File.Exists(root) || !string.Equals(root, Binding.ProficienciesRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw InvalidOwnedProficiency();
            Path = System.IO.Path.Combine(root, Binding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            SnapshotFileLockCoordinator.ExecuteLocked(Path, () =>
            {
                if (!BeforeIsCurrentPlain() || !BeforeImage.MatchesSnapshot(Before.PlayerProficiencies)) throw InvalidOwnedProficiency();
                beforeBytes = LegacySavedProficiencyImage.Read(Path);
                if (!BeforeImage.Matches(beforeBytes)) throw InvalidOwnedProficiency();
                afterBytes = LegacyBasicPhysicalProficiencyImage.Project(beforeBytes, Calculation);
                AfterImage = new(afterBytes);
                if (beforeBytes is not null)
                {
                    using var writable = new FileStream(Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                    if (writable.Length != beforeBytes.Length) throw InvalidOwnedProficiency();
                }
                if (!BeforeIsCurrentPlain()) throw InvalidOwnedProficiency();
                return true;
            });
        }

        internal bool BeforeIsCurrentPlain() => !Committed && !FileWritten &&
            ReferenceEquals(Binding.SavedProficiencies, BeforeImage) &&
            Store.IsFramePersistenceCurrent(Frame, Context.Connection, Before) &&
            Frame.IsActionLifecycleCurrent(Store, Context);

        internal bool TryWrite(out string reason)
        {
            string refusal = string.Empty;
            bool result = SnapshotFileLockCoordinator.ExecuteLocked(Path, () =>
            {
                if (!BeforeIsCurrentPlain() || !BeforeImage.Matches(LegacySavedProficiencyImage.Read(Path)) ||
                    !Binding.Recovery.TryRequireLiveJournalClear(out _)) return false;
                try
                {
                    SnapshotFilePersistence.WriteAllBytesAtomically(Path, afterBytes, ".player-proficiency-file-");
                    FileWritten = true;
                    if (!AfterImage.Matches(LegacySavedProficiencyImage.Read(Path))) throw InvalidOwnedProficiency();
                    return true;
                }
                catch (Exception error)
                {
                    // A failed writer is never accepted from an equal AFTER
                    // file. Only proven unchanged BEFORE permits safe refusal.
                    bool unchanged = false;
                    try { unchanged = !FileWritten && BeforeImage.Matches(LegacySavedProficiencyImage.Read(Path)); }
                    catch (Exception inspect) { Store.FenceOwnedProficiencyFailure(this, new AggregateException(error, inspect)); }
                    if (unchanged) { refusal = "ownedProficiencyWrite:" + error.GetType().Name; return false; }
                    Store.FenceOwnedProficiencyFailure(this, error); throw;
                }
            });
            reason = result ? string.Empty : refusal.Length == 0 ? "ownedProficiencyBeforeImageChanged" : refusal;
            return result;
        }

        internal bool AfterWriteIsCurrentPlain() => FileWritten && !Committed &&
            ReferenceEquals(Binding.SavedProficiencies, BeforeImage) &&
            Store.IsFramePersistenceCurrent(Frame, Context.Connection, Before) &&
            Frame.IsActionLifecycleCurrent(Store, Context) &&
            AfterImage.Matches(LegacySavedProficiencyImage.Read(Path));
    }
}
