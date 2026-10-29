using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed class LegacyRootAdmissionOwner(
        WorldSession expected, WorldSessionStore? store, ConquerTcpConnection connection, object? identity)
    {
        internal WorldSession Expected { get; } = expected;
        internal WorldSessionStore? Store { get; } = store;
        internal ConquerTcpConnection Connection { get; } = connection;
        internal object? Identity { get; } = identity;
        internal WorldSessionStore.MagicCostOutputReceipt? CostOutput { get; set; }
        // Diagnostics only, never authorization or a reusable admission token.
        internal bool CommitAttempted { get; set; }
        internal bool Committed { get; set; }
    }

    private static bool TryCaptureLegacyRootAdmissionOwner(
        WorldPacketHandlerContext context, WorldSession session, object? expectedIdentity,
        out LegacyRootAdmissionOwner owner)
    {
        owner = null!;
        if (context.Sessions is not { } world)
        {
            if (expectedIdentity is not null) return false;
            // Existing local-record compatibility, explicitly not a Store transaction.
            owner = new(session, null, context.Connection, null);
            return true;
        }
        var identity = expectedIdentity;
        if (identity is null && !LegacyNativeClientPublication.TryCaptureIdentity(
                world, context.Connection, session.Character.Uid, out identity)) return false;
        if (!LegacyNativeClientPublication.TryCurrent(
                world, context.Connection, session.Character.Uid, identity, out var current)) return false;
        owner = new(current, world, context.Connection, identity);
        return true;
    }

    private static bool TryCommitLegacyRootAdmission(
        LegacyRootAdmissionOwner owner, WorldSession candidate, out WorldSession session)
    {
        session = owner.Expected;
        if (owner.Store is not { } world)
        {
            owner.Committed = true;
            session = candidate; // No fabricated Store, saver or native generation.
            return true;
        }
        owner.CommitAttempted = true;
        // The issued identity and exact record are checked together under the
        // real Store gate. This is not a nested writer inside an identity query.
        var committed = world.TryCommitMagicCosts(owner.Connection, owner.Expected,
            owner.Identity!, candidate, out var output);
        owner.Committed = committed;
        if (committed) { owner.CostOutput = output; session = output!.Published; }
        return committed;
    }

    private static bool IsLegacyRootCommittedRecordCurrent(
        LegacyRootAdmissionOwner owner, WorldSession committed) =>
        owner.Store is not { } world ||
        (owner.Identity is { } identity &&
         LegacyNativeClientPublication.TryCurrent(world, owner.Connection,
             owner.Expected.Character.Uid, identity, out var current) &&
         ReferenceEquals(current, committed));

    private static bool RefreshLegacyRootAcceptedOwner(
        WorldPacketHandlerContext context, LegacyRootAdmissionOwner owner, ref WorldSession session) =>
        TryRefreshLegacyPlayerMagicResourceSession(
            context, owner.Expected.Character.Uid, ref session, owner.Identity);

    private static bool IsLegacyRootPreparationFailure(Exception error) =>
        error is ArgumentException or FormatException or OverflowException or InvalidDataException;

    private static async ValueTask ReportLegacyRootAdmissionFaultAsync(
        WorldPacketHandlerContext context, LegacyRootAdmissionOwner owner, Exception error)
    {
        var outcome = owner.Committed ? "committed" : "commit-publication-unproven";
        if (owner.Store is null) outcome = "unbound-local-record-" + outcome;
        try
        {
            await context.Log.WriteLineAsync(
                $"WorldServer root magic admission {outcome}; no rollback or refund attempted. " +
                $"uid={owner.Expected.Character.Uid} error={error.GetType().Name}.").ConfigureAwait(false);
        }
        catch (Exception diagnostic)
        {
            throw new AggregateException(
                "Root admission failed and its outcome diagnostic also failed.", error, diagnostic);
        }
    }
}
