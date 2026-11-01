using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class OwnedPhysicalDeathPreparation
    {
        internal DragonhowlDeathNotifications PrepareDragonhowlNotifications(WorldSessionStore.OwnedDragonhowlDeath purpose,
            WorldSessionStore.OwnedDragonhowlDeathReceipt receipt, WorldSession canonical) =>
            DragonhowlDeathNotifications.Prepare(this, purpose, receipt, canonical, historicalFrames);
    }

    // Reuses the real death builder's historical phase bytes and the actual
    // Team participant. The proc receipt is never converted to a basic hit.
    internal sealed class DragonhowlDeathNotifications : IDisposable
    {
        private sealed record Recipient(ConquerTcpConnection Connection, long Uid, WorldSession Original,
            LegacyNativeGameClientRuntime? Client);
        private sealed record Delivery(ReadOnlyMemory<byte> Frame, Recipient[] Recipients, bool Visible);
        private readonly OwnedPhysicalDeathPreparation preparation;
        private readonly WorldSessionStore.OwnedDragonhowlDeath purpose;
        private readonly WorldSessionStore.OwnedDragonhowlDeathReceipt receipt;
        private readonly WorldSession target;
        private readonly TeamSessionStateStore.OwnedPhysicalDeathMember? team;
        private readonly Delivery[] deliveries;
        private bool released, published;
        private int started;

        private DragonhowlDeathNotifications(OwnedPhysicalDeathPreparation preparation,
            WorldSessionStore.OwnedDragonhowlDeath purpose, WorldSessionStore.OwnedDragonhowlDeathReceipt receipt,
            WorldSession target, IReadOnlyList<OwnedPhysicalDeathFrame> phases)
        {
            this.preparation = preparation; this.purpose = purpose; this.receipt = receipt; this.target = target;
            if (!ReferenceEquals(preparation.Dragonhowl, purpose) || !purpose.IsCurrentPlain() ||
                !preparation.MatchesDragonhowlCanonical(target)) throw Invalid();
            try
            {
                if (preparation.Context.TeamStates is { } teams)
                    team = TeamSessionStateStore.OwnedPhysicalDeathMember.Prepare(teams, preparation.TargetBinding, preparation, target);
                var candidates = preparation.TargetBinding.Store.Snapshot();
                var visible = candidates.Where(row => !ReferenceEquals(row.Connection, preparation.TargetBinding.Connection) &&
                    row.Session.Character.Uid != target.Character.Uid && CanSeePlayer(target.Character, row.Session.Character))
                    .Select(row => Capture(row.Connection, row.Session)).ToArray();
                var self = Capture(preparation.TargetBinding.Connection, target);
                Recipient[] members = team is null ? [] : team.Recipients.SelectMany(member => candidates
                    .Where(row => ReferenceEquals(row.Connection, member.Connection) && row.Session.Character.Uid == member.Uid)
                    .Select(row => Capture(row.Connection, row.Session))).ToArray();
                var output = new List<Delivery>();
                foreach (var phase in phases)
                {
                    if (phase.Audience == OwnedPhysicalDeathAudience.AssignedTeam)
                    {
                        var frame = team?.CreateHistoricalFrame(GetOwnedPhysicalDeathMaximum(preparation, phase.Phase), phase.AssignedHitpoints);
                        if (frame is not null) output.Add(new(frame, members, false));
                        continue;
                    }
                    var bytes = phase.Bytes.ToArray();
                    if (phase.Audience is OwnedPhysicalDeathAudience.Self or OwnedPhysicalDeathAudience.SelfAndVisible)
                        output.Add(new(bytes, [self], false));
                    if (phase.Audience is OwnedPhysicalDeathAudience.Visible or OwnedPhysicalDeathAudience.SelfAndVisible)
                        output.Add(new(bytes, visible, true));
                }
                deliveries = output.ToArray();
                if (!IsCurrentPlain()) throw Invalid();
            }
            catch { Dispose(); throw; }
        }

        internal static DragonhowlDeathNotifications Prepare(OwnedPhysicalDeathPreparation preparation,
            WorldSessionStore.OwnedDragonhowlDeath purpose, WorldSessionStore.OwnedDragonhowlDeathReceipt receipt,
            WorldSession target, IReadOnlyList<OwnedPhysicalDeathFrame> phases) => new(preparation, purpose, receipt, target, phases);

        internal bool IsCurrentPlain() => !released && !published && purpose.IsCurrentPlain() &&
            preparation.IsCurrentPlain(preparation.TargetBinding) && (team is null || team.IsCurrentPlain());
        internal void PublishPrepared() { team?.PublishPrepared(); published = true; }
        internal bool IsPublishedCurrentPlain() => published && (team is null || team.IsPublishedCurrentPlain());
        public void Dispose() { team?.Dispose(); released = true; }

        internal async ValueTask<bool> PublishAsync(CancellationToken token)
        {
            if (!released || !published || Interlocked.CompareExchange(ref started, 1, 0) != 0 || !receipt.TryCurrent()) return false;
            foreach (var delivery in deliveries)
                foreach (var recipient in delivery.Recipients)
                {
                    if (!receipt.TryCurrent()) return false;
                    if (!RecipientCurrent(recipient, delivery.Visible)) continue;
                    await recipient.Connection.SendAsync(delivery.Frame.ToArray(), token).ConfigureAwait(false);
                    if (!receipt.TryCurrent()) return false;
                }
            return receipt.TryCurrent();
        }

        private bool RecipientCurrent(Recipient recipient, bool visible)
        {
            var store = preparation.TargetBinding.Store;
            if (!store.TryGet(recipient.Connection, out var current) || current is null || current.Character.Uid != recipient.Uid) return false;
            if (recipient.Client is { } client)
            {
                if (!ReferenceEquals(current.NativeGameClient, client) || client.OrdinarySpellBinding is not { } binding ||
                    !ReferenceEquals(binding.Store, store) || !ReferenceEquals(binding.Connection, recipient.Connection) ||
                    binding.Uid != recipient.Uid || binding.IsRetired || !store.IsBoundSpellOwnerCurrent(binding)) return false;
            }
            else if (!ReferenceEquals(current, recipient.Original)) return false;
            return !visible || CanSeePlayer(target.Character, current.Character);
        }
        private static Recipient Capture(ConquerTcpConnection connection, WorldSession session) =>
            new(connection, session.Character.Uid, session, session.NativeGameClient);
        private static InvalidOperationException Invalid() => new("ownedDragonhowlDeathNotificationsChanged");
    }
}
