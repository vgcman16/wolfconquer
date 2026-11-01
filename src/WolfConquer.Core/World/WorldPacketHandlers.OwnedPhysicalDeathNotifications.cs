using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class OwnedPhysicalDeathPreparation
    {
        internal OwnedPhysicalDeathNotifications PrepareNotifications(WorldSessionStore.BasicPhysicalPublication actual,
            WorldSession canonicalActor, WorldSession canonicalTarget) =>
            OwnedPhysicalDeathNotifications.Prepare(this, actual, canonicalActor, canonicalTarget, historicalFrames);
    }

    internal static uint GetOwnedPhysicalDeathMaximum(OwnedPhysicalDeathPreparation preparation, WorldSession phase) =>
        GetLegacyEquipmentRuntimeMaxHitpoints(phase, preparation.Context.WorldData);

    internal sealed partial class OwnedPhysicalDeathNotifications : IDisposable
    {
        private sealed record Recipient(ConquerTcpConnection Connection, long Uid, WorldSession Original,
            LegacyNativeGameClientRuntime? Client);
        private sealed record Delivery(ReadOnlyMemory<byte> Frame, Recipient[] Recipients, bool RequireVisibility);
        private readonly OwnedPhysicalDeathPreparation preparation;
        private readonly WorldSessionStore.BasicPhysicalPublication actual;
        private readonly WorldSession actor, target;
        private readonly TeamSessionStateStore.OwnedPhysicalDeathMember? team;
        private readonly Delivery[] deliveries;
        private bool preparationReleased, published;
        private int started;

        private OwnedPhysicalDeathNotifications(OwnedPhysicalDeathPreparation preparation,
            WorldSessionStore.BasicPhysicalPublication actual, WorldSession actor, WorldSession target,
            IReadOnlyList<OwnedPhysicalDeathFrame> phases)
        {
            this.preparation = preparation; this.actual = actual; this.actor = actor; this.target = target;
            if (!actual.IsPreparedDeathPublication(preparation, actor, target)) throw InvalidNotifications();
            try
            {
                if (preparation.Context.TeamStates is { } teams)
                    team = TeamSessionStateStore.OwnedPhysicalDeathMember.Prepare(teams, preparation.TargetBinding, preparation, target);
                var candidates = preparation.TargetBinding.Store.Snapshot();
                var visible = candidates.Where(row => !ReferenceEquals(row.Connection, preparation.TargetBinding.Connection) &&
                        row.Session.Character.Uid != target.Character.Uid && CanSeePlayer(target.Character, row.Session.Character))
                    .Select(row => CaptureRecipient(row.Connection, row.Session)).ToArray();
                var self = CaptureRecipient(preparation.TargetBinding.Connection, target);
                Recipient[] teamRecipients = team is null ? [] : team.Recipients.SelectMany(member => candidates
                        .Where(row => ReferenceEquals(row.Connection, member.Connection) && row.Session.Character.Uid == member.Uid)
                        .Select(row => CaptureRecipient(row.Connection, row.Session))).ToArray();
                var output = new List<Delivery>();
                foreach (var phase in phases)
                {
                    if (phase.Audience == OwnedPhysicalDeathAudience.AssignedTeam)
                    {
                        var bytes = team?.CreateHistoricalFrame(GetOwnedPhysicalDeathMaximum(preparation, phase.Phase), phase.AssignedHitpoints);
                        if (bytes is not null) output.Add(new(bytes, teamRecipients, false));
                        continue;
                    }
                    var bytesCopy = phase.Bytes.ToArray();
                    if (phase.Audience is OwnedPhysicalDeathAudience.Self or OwnedPhysicalDeathAudience.SelfAndVisible)
                        output.Add(new(bytesCopy, [self], false));
                    if (phase.Audience is OwnedPhysicalDeathAudience.Visible or OwnedPhysicalDeathAudience.SelfAndVisible)
                        output.Add(new(bytesCopy, visible, true));
                }
                deliveries = output.ToArray();
                if (!IsCurrentPlain()) throw InvalidNotifications();
            }
            catch { Dispose(); throw; }
        }

        internal static OwnedPhysicalDeathNotifications Prepare(OwnedPhysicalDeathPreparation preparation,
            WorldSessionStore.BasicPhysicalPublication actual, WorldSession actor, WorldSession target,
            IReadOnlyList<OwnedPhysicalDeathFrame> phases) => new(preparation, actual, actor, target, phases);

        internal bool IsCurrentPlain() => !preparationReleased && !published &&
            actual.IsPreparedDeathPublication(preparation, actor, target) && (team is null || team.IsCurrentPlain());

        internal void PublishPrepared()
        {
            team?.PublishPrepared();
            published = true;
        }

        internal bool IsPublishedCurrentPlain() => published && (team is null || team.IsPublishedCurrentPlain());

        public void Dispose()
        {
            team?.Dispose();
            preparationReleased = true;
        }

        private static Recipient CaptureRecipient(ConquerTcpConnection connection, WorldSession session) =>
            new(connection, session.Character.Uid, session, session.NativeGameClient);

        private static InvalidOperationException InvalidNotifications() => new("ordinaryPhysicalDeathNotificationsChanged");
    }
}
