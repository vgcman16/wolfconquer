using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedTeleportDestinationWrite
{
    internal enum Outcome { Rejected, Committed, RecoveryRequired }
    private readonly WorldServerFrameHandler frame;
    private readonly WorldSessionStore store;
    private readonly WorldPacketHandlerContext context;
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly WorldPacketHandlers.OwnedTeleportDestinationProjection plan;
    private readonly string path;
    private readonly byte[] before, after;
    private WorldPacketHandlers.OwnedTeleportDestinationProjection? committed;
    internal WorldSession Proposed => plan.Proposed;

    private LegacyPreparedTeleportDestinationWrite(WorldServerFrameHandler frame, WorldSessionStore store,
        WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding,
        WorldPacketHandlers.OwnedTeleportDestinationProjection plan)
    {
        this.frame = frame; this.store = store; this.context = context; this.binding = binding; this.plan = plan;
        path = frame.RequireTeleportDestinationPath(store, binding, plan.Expected);
        (before, after) = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
        {
            if (!IsCurrent() || !binding.Recovery.TryRequireLiveJournalClear(out _)) throw Unavailable();
            if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "teleportDestination", out var raw, out var reason))
                throw new IOException("teleportDestinationCharacterUnavailable:" + reason);
            var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(raw, out _));
            if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(disk, plan.Expected.Character) ||
                CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(disk, plan.Expected.Character) ||
                !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(disk, plan.Expected.Character)) throw Unavailable();
            var projected = LegacyTeleportCharacterProjection.Project(raw, disk, plan.Proposed.Character);
            using (var access = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete)) { }
            if (!IsCurrent()) throw Unavailable();
            return (raw, projected);
        });
    }

    internal static LegacyPreparedTeleportDestinationWrite Prepare(WorldServerFrameHandler frame, WorldSessionStore store,
        WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding,
        WorldPacketHandlers.OwnedTeleportDestinationProjection plan) => new(frame, store, context, binding, plan);
    private bool IsCurrent() => plan.IsCurrentPlain() && store.IsTeleportDestinationWriteOwnerCurrent(frame, context, binding, plan);

    internal Outcome TryCommit(out string reason)
    {
        var failure = "teleportDestinationOwnerChanged";
        try
        {
            var outcome = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            {
                if (!IsCurrent()) return Outcome.Rejected;
                if (!binding.Recovery.TryRequireLiveJournalClear(out failure)) return Outcome.RecoveryRequired;
                if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "teleportDestination", out var actual, out failure) ||
                    !actual.AsSpan().SequenceEqual(before) || !IsCurrent()) return Outcome.Rejected;
                if (!CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path, before, after, out var uncertain, out failure))
                    return uncertain ? Outcome.RecoveryRequired : Outcome.Rejected;
                committed = plan; return Outcome.Committed;
            });
            reason = failure; return outcome;
        }
        catch (Exception error) when (IsFileFailure(error))
        {
            reason = "teleportDestinationWrite:" + error.GetType().Name;
            try
            {
                return SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
                    CharacterSnapshotFileBaseline.TryReadBytes(path, "teleportDestination", out var actual, out _) &&
                    actual.AsSpan().SequenceEqual(before) ? Outcome.Rejected : Outcome.RecoveryRequired);
            }
            catch (Exception observation) when (IsFileFailure(observation))
            { reason += ";observation:" + observation.GetType().Name; return Outcome.RecoveryRequired; }
        }
    }
    private static bool IsFileFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        InvalidOperationException or ArgumentException or NotSupportedException or System.Security.SecurityException;
    private static InvalidOperationException Unavailable() => new("teleportDestinationCurrentCapturedOwnerRequired");
}

public sealed partial class WorldServerFrameHandler
{
    internal string RequireTeleportDestinationPath(WorldSessionStore store, LegacyOrdinarySpellBinding binding, WorldSession expected)
    {
        var root = _snapshotLoader?.UsersRoot;
        if (!ReferenceEquals(store, _sessions) || !store.IsFramePersistenceCurrent(this, binding.Connection, expected) ||
            string.IsNullOrWhiteSpace(root) || !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)),
                binding.UsersRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("teleportDestinationConfiguredCharacterRootRequired");
        return Path.Combine(binding.UsersRoot, binding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
    }
}
