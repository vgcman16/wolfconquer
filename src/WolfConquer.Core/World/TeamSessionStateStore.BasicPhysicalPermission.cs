using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class TeamSessionStateStore
{
    // Membership witness only, not an attack decision. Parent owns actual
    // lifecycle/ordered actor/vector gates; this gate is synchronous and last.
    internal sealed class OwnedBasicPhysicalPermission : IDisposable
    {
        private readonly TeamSessionStateStore owner;
        private readonly WorldPacketHandlerContext context;
        private readonly MsgAttackPacket packet;
        private readonly LegacyOrdinarySpellBinding actor, target;
        private readonly WorldSession expectedActor, expectedTarget;
        private readonly BasicPhysicalRoster actorRoster, targetRoster;
        private bool held;

        private OwnedBasicPhysicalPermission(TeamSessionStateStore owner, WorldPacketHandlerContext context,
            MsgAttackPacket packet, WorldActiveSession selected)
        {
            this.owner = owner; this.context = context; this.packet = packet;
            if (!ReferenceEquals(context.TeamStates, owner) || context.Sessions is not { } store ||
                !store.TryReadOwnedBasicPhysicalPreparationOwners(context, packet, selected,
                    out actor, out target, out expectedActor, out expectedTarget) ||
                owner._nativeWorld is not null && !ReferenceEquals(owner._nativeWorld, store)) throw InvalidPermission();
            Monitor.Enter(owner._gate, ref held);
            try
            {
                actorRoster = new(owner, actor); targetRoster = new(owner, target);
                if (!IsCurrentPlain()) throw InvalidPermission();
            }
            catch { Dispose(); throw; }
        }

        internal static OwnedBasicPhysicalPermission Prepare(TeamSessionStateStore owner, WorldPacketHandlerContext context,
            MsgAttackPacket packet, WorldActiveSession selected) => new(owner, context, packet, selected);

        internal bool IsPreparedFor(WorldPacketHandlerContext actualContext, MsgAttackPacket actualPacket, WorldActiveSession selected) =>
            ReferenceEquals(context, actualContext) && packet == actualPacket &&
            ReferenceEquals(selected.Connection, target.Connection) && ReferenceEquals(selected.Session, expectedTarget) && IsCurrentPlain();

        internal bool IsCurrentPlain() => held && Monitor.IsEntered(owner._gate) &&
            ReferenceEquals(context.TeamStates, owner) &&
            actor.Store.IsOwnedBasicPhysicalPreparationCurrent(context, packet, actor, target, expectedActor, expectedTarget) &&
            actorRoster.IsCurrentPlain() && targetRoster.IsCurrentPlain();

        public void Dispose() { if (held) { held = false; Monitor.Exit(owner._gate); } }
    }

    private sealed class BasicPhysicalRoster
    {
        private readonly TeamSessionStateStore owner;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly TeamSessionState? state;
        private readonly BasicPhysicalRosterMember[] members;

        internal BasicPhysicalRoster(TeamSessionStateStore owner, LegacyOrdinarySpellBinding binding)
        {
            this.owner = owner; this.binding = binding;
            if (!Monitor.IsEntered(owner._gate)) throw InvalidPermission();
            if (owner._states.TryGetValue(binding.Connection, out state))
            {
                if (owner._nativeClientAuthority is not null &&
                    !ReferenceEquals(state.NativeClient?.Identity, binding.NativeIdentity)) throw InvalidPermission();
                members = state.Team.Members.Select(row => new BasicPhysicalRosterMember(row, row.Connection, row.Uid,
                    row.NativeClient, row.NativeClient?.Identity)).ToArray();
                var own = members.SingleOrDefault(row => ReferenceEquals(row.Connection, binding.Connection) && row.Uid == binding.Uid);
                if (own is null || owner._nativeClientAuthority is not null &&
                    !ReferenceEquals(own.NativeIdentity, binding.NativeIdentity)) throw InvalidPermission();
            }
            else members = [];
        }

        internal bool IsCurrentPlain()
        {
            if (!Monitor.IsEntered(owner._gate)) return false;
            bool present = owner._states.TryGetValue(binding.Connection, out var actual);
            if (state is null) return !present;
            if (!present || !ReferenceEquals(state, actual) || state.Team.Members.Count != members.Length ||
                owner._nativeClientAuthority is not null && !ReferenceEquals(state.NativeClient?.Identity, binding.NativeIdentity)) return false;
            for (int index = 0; index < members.Length; index++)
            {
                var row = state.Team.Members[index]; var saved = members[index];
                if (!ReferenceEquals(row, saved.Member) || !ReferenceEquals(row.Connection, saved.Connection) || row.Uid != saved.Uid ||
                    !ReferenceEquals(row.NativeClient, saved.NativeClient) ||
                    !ReferenceEquals(row.NativeClient?.Identity, saved.NativeIdentity)) return false;
            }
            return true;
        }
    }

    private sealed record BasicPhysicalRosterMember(TeamSessionMember Member, ConquerTcpConnection Connection,
        long Uid, object? NativeClient, object? NativeIdentity);
    private static InvalidOperationException InvalidPermission() => new("ordinaryBasicPhysicalTeamPermissionChanged");
}
