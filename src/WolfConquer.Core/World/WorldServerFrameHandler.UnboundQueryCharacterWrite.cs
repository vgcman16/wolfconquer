using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Only preparation against this Frame's private loader origin constructs
    // this writer. The completed instance, not a callback, advances that origin.
    internal sealed class UnboundQueryCharacterWrite
    {
        private readonly WorldServerFrameHandler frame;
        private readonly WorldSessionStore store;
        private readonly ConquerTcpConnection connection;
        private readonly UnboundCharacterOrigin origin;
        private readonly WorldSession expected;
        private readonly LegacyManagedCharacterImage beforeInputs, proposedInputs, finalInputs;
        private readonly byte[] before, after;
        private readonly uint runtimeVigor;
        private readonly bool writesFile;
        internal WorldSession Proposed { get; }
        internal bool IsCommitted { get; private set; }

        private UnboundQueryCharacterWrite(WorldServerFrameHandler frame, WorldSessionStore store,
            ConquerTcpConnection connection, WorldSession expected, WorldSession proposed, bool writesFile, uint vigor)
        {
            this.frame = frame; this.store = store; this.connection = connection; this.expected = expected;
            origin = frame.RequireUnboundCharacterOrigin(store, connection, expected);
            beforeInputs = new(expected.Character); proposedInputs = new(proposed.Character);
            if ((expected with { Character = proposed.Character, EquipmentHitpointsStatus = proposed.EquipmentHitpointsStatus }) != proposed ||
                !WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(
                    expected.Character with { MinHitPoints = proposed.Character.MinHitPoints }, proposed.Character) ||
                !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(expected.Character, proposed.Character))
                throw new InvalidOperationException("managedQueryChangedUnownedFields");
            this.writesFile = writesFile; runtimeVigor = vigor;
            before = origin.Image.ToArray();
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(before, out _));
            if (!LegacyManagedCharacterImage.MatchesSaved(saved, expected.Character, origin.RuntimeVigor))
                throw new InvalidOperationException("managedQueryRuntimeBaselineChanged");
            after = writesFile ? LegacyManagedCharacterProjection.ProjectAfterImage(before, proposed.Character) : before.ToArray();
            Proposed = writesFile ? proposed with { Character = proposedInputs.WithSavedMirror(after) } : proposed;
            finalInputs = new(Proposed.Character);
            if (!BeforeIsCurrent()) throw new InvalidOperationException("managedQueryInputsChanged");
        }

        internal static UnboundQueryCharacterWrite Prepare(WorldServerFrameHandler frame, WorldSessionStore store,
            ConquerTcpConnection connection, WorldSession expected, WorldSession proposed, bool writesFile, uint vigor) =>
            new(frame, store, connection, expected, proposed, writesFile, vigor);

        internal bool BeforeIsCurrent() => !IsCommitted && beforeInputs.IsCurrent() && proposedInputs.IsCurrent() &&
            finalInputs.IsCurrent() && ReferenceEquals(frame.RequireUnboundCharacterOrigin(store, connection, expected), origin) &&
            origin.Image.AsSpan().SequenceEqual(before);

        internal bool TryCommit(out bool uncertain, out string reason)
        {
            bool outcomeUncertain = false;
            string failure = "managedQueryInputsChanged";
            bool saved = CharacterSnapshotFileCoordinator.ExecuteLocked(origin.Path, () =>
            {
                if (!BeforeIsCurrent() || !CharacterSnapshotFileBaseline.TryReadBytes(origin.Path,
                        "managedQuery", out var actual, out failure) || !actual.AsSpan().SequenceEqual(before)) return false;
                if (writesFile && !CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(origin.Path,
                        before, after, out outcomeUncertain, out failure)) return false;
                IsCommitted = true;
                return true;
            });
            uncertain = outcomeUncertain; reason = failure; return saved;
        }

        internal void CompletePublication(WorldSession actual)
        {
            if (!IsCommitted || !ReferenceEquals(actual, Proposed) || !finalInputs.IsCurrent() ||
                !ReferenceEquals(frame.RequireUnboundCharacterOrigin(store, connection, actual), origin))
                throw new InvalidOperationException("managedQueryCommittedPublicationChanged");
            CharacterSnapshotFileCoordinator.ExecuteLocked(origin.Path, () =>
            {
                if (!origin.Image.AsSpan().SequenceEqual(before) ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(origin.Path, "managedQueryCommitted", out var bytes, out _) ||
                    !bytes.AsSpan().SequenceEqual(after)) throw new InvalidOperationException("managedQueryCommittedImageChanged");
                origin.Image = after.ToArray(); origin.RuntimeVigor = runtimeVigor;
                return true;
            });
        }
    }

    internal void BlockUnboundQueryFailure() => _tradeRecovery.Block("managedQueryCharacterOutcomeRequiresRecovery");
}
