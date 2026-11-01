using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed class ManagedQueryHitpointsPurpose { }
    private sealed class ManagedQueryHitpointsData(WorldPacketHandlerContext context, WorldSession expected, int value, bool vigor)
    {
        internal readonly WorldPacketHandlerContext Context = context;
        internal readonly WorldSession Expected = expected;
        internal readonly int Value = value;
        internal readonly bool Vigor = vigor;
        internal bool Consumed;
    }
    private static readonly ConditionalWeakTable<ManagedQueryHitpointsPurpose, ManagedQueryHitpointsData> ManagedQueryHitpoints = new();

    // Only the actual execution's AssignAsync creates and registers a purpose.
    // This table does not itself authorize a file write or holder publication.
    private static ManagedQueryHitpointsPurpose IssueManagedQueryHitpoints(
        WorldPacketHandlerContext context, WorldSession expected, int value, bool vigor = false)
    {
        var purpose = new ManagedQueryHitpointsPurpose();
        ManagedQueryHitpoints.Add(purpose, new(context, expected, value, vigor));
        return purpose;
    }

    internal static bool TryConsumeManagedQueryHitpoints(ManagedQueryHitpointsPurpose purpose,
        WorldPacketHandlerContext context, WorldSession expected, int value, bool vigor)
    {
        if (!ManagedQueryHitpoints.TryGetValue(purpose, out var data) || data.Consumed ||
            !ReferenceEquals(data.Context.Connection, context.Connection) ||
            !ReferenceEquals(data.Context.Sessions, context.Sessions) ||
            !ReferenceEquals(data.Expected, expected) || data.Value != value || data.Vigor != vigor) return false;
        data.Consumed = true;
        return true;
    }

    internal static WorldSession ProjectManagedQueryVigor(WorldSession expected, ushort value) => expected with
        { Character = WithEquipmentQueryVigor(expected.Character, value) };

    private static LegacyEquipmentQueryPublicationResult CommitAssignedManagedOrLegacyHitpoints(
        WorldPacketHandlerContext context, WorldSession session, LegacyEquipmentQueryPublicationScope scope,
        int value, WorldSession? required)
    {
        if (scope.ManagedHitpoints is { } purpose && context.Sessions is { } store)
        {
            bool committed = store.TryCommitUnboundQueryCharacter(context, session, purpose, value,
                vigor: false, out var applicable, out var written);
            if (applicable)
                return committed && written is not null
                    ? RefreshLegacyEquipmentQueryPublication(context with { Session = written }, written, scope)
                    : new(null, required, LegacyEquipmentQueryPublicationLoss.OwnerLost);
        }
        return CommitLegacyEquipmentQueryMutation(context, session, scope,
            owner => ProjectAssignedLegacyHitpoints(context, owner, scope.OwnerUid, value));
    }

    private sealed partial class LegacyEquipmentQueryExecution
    {
        private bool CommitManagedOrLegacyVigor(ushort vigor)
        {
            if (!Refresh()) return false;
            if (Context.Connection.NativeGameClient is null && Session.NativeGameClient is null &&
                Context.Sessions is { } store)
            {
                var purpose = IssueManagedQueryHitpoints(Context, Session, vigor, vigor: true);
                bool committed = store.TryCommitUnboundQueryCharacter(Context, Session, purpose, vigor,
                    vigor: true, out var applicable, out var written);
                if (applicable)
                {
                    Adopt(committed && written is not null
                        ? RefreshLegacyEquipmentQueryPublication(Context with { Session = written }, written, Request.Scope)
                        : new(null, Current.RequiredParticipantSession, LegacyEquipmentQueryPublicationLoss.OwnerLost));
                    return Current.Completed;
                }
            }
            return Commit(session => ProjectManagedQueryVigor(session, vigor));
        }
    }
}
