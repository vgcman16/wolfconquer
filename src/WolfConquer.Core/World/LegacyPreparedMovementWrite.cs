using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedMovementWrite
{
    internal enum Outcome { Rejected, Committed, RecoveryRequired }
    private readonly WorldServerFrameHandler frame;
    private readonly WorldSessionStore store;
    private readonly WorldPacketHandlerContext context;
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly WorldPacketHandlers.OwnedMovementProjection plan;
    private readonly string path;
    private readonly byte[] before, after;
    private WorldPacketHandlers.OwnedMovementProjection? committed;
    internal WorldSession Proposed => plan.Proposed;

    private LegacyPreparedMovementWrite(WorldServerFrameHandler frame, WorldSessionStore store,
        WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding, WorldPacketHandlers.OwnedMovementProjection plan)
    {
        this.frame = frame; this.store = store; this.context = context; this.binding = binding; this.plan = plan;
        path = Path.Combine(binding.UsersRoot, binding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        (before, after) = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
        {
            if (!IsCurrent() || !binding.Recovery.TryRequireLiveJournalClear(out var readiness))
                throw new InvalidOperationException("ordinaryMovementOwnerUnavailable");
            if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "ordinaryMovement", out var captured, out var reason))
                throw new IOException("ordinaryMovementCharacterUnavailable:" + reason);
            var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(captured, out _));
            var expected = plan.Expected.Character;
            if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(disk, expected) ||
                CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(disk, expected) ||
                !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(disk, expected) ||
                !LegacyPairMovementCharacterProjection.TryProjectCoordinates(captured, checked((uint)binding.Uid),
                    checked((uint)expected.MapId), checked((ushort)expected.X), checked((ushort)expected.Y),
                    checked((ushort)plan.Proposed.Character.X), checked((ushort)plan.Proposed.Character.Y), out var projected, out reason))
                throw new InvalidDataException("ordinaryMovementCharacterBaselineInvalid:" + reason);
            if (!IsCurrent()) throw new InvalidOperationException("ordinaryMovementOwnerChangedDuringCapture");
            return (captured, projected);
        });
    }

    internal static LegacyPreparedMovementWrite Prepare(WorldServerFrameHandler frame, WorldSessionStore store,
        WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding, WorldPacketHandlers.OwnedMovementProjection plan) =>
        new(frame, store, context, binding, plan);

    private bool IsCurrent() => plan.IsCurrentPlain() && store.IsMovementWriteOwnerCurrent(frame, context, binding, plan.Expected);

    internal Outcome TryCommit(out string reason)
    {
        var failure = "ordinaryMovementOwnerChanged";
        try
        {
            var result = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            {
                if (!IsCurrent()) return Outcome.Rejected;
                if (!binding.Recovery.TryRequireLiveJournalClear(out failure)) return Outcome.RecoveryRequired;
                if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "ordinaryMovement", out var actual, out failure) ||
                    !actual.AsSpan().SequenceEqual(before)) return Outcome.Rejected;
                if (!IsCurrent()) return Outcome.Rejected;
                if (!CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path, before, after, out var uncertain, out failure))
                    return uncertain ? Outcome.RecoveryRequired : Outcome.Rejected;
                committed = plan; return Outcome.Committed;
            });
            reason = failure; return result;
        }
        catch (Exception error) when (IsFileFailure(error))
        {
            reason = "ordinaryMovementWrite:" + error.GetType().Name;
            try
            {
                return SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
                    CharacterSnapshotFileBaseline.TryReadBytes(path, "ordinaryMovement", out var actual, out _) &&
                    actual.AsSpan().SequenceEqual(before) ? Outcome.Rejected : Outcome.RecoveryRequired);
            }
            catch (Exception observation) when (IsFileFailure(observation))
            { reason += ";observation:" + observation.GetType().Name; return Outcome.RecoveryRequired; }
        }
    }

    private static bool IsFileFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        InvalidOperationException or ArgumentException or NotSupportedException or System.Security.SecurityException;
}
