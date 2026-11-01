using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class TeamSessionStateStore
{
    // Synchronous participant held after the actual ordered Store/native
    // gates. No await may occur until Dispose releases this preparation gate.
    internal sealed class OwnedPhysicalDeathMember : IDisposable
    {
        private readonly TeamSessionStateStore owner;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation;
        private readonly TeamSessionState? state;
        private readonly TeamSessionMember? member;
        private readonly TeamSessionMember[] roster;
        private readonly LegacyTeamMemberInfoEntry? before, after;
        private bool held, published;

        private OwnedPhysicalDeathMember(TeamSessionStateStore owner, LegacyOrdinarySpellBinding binding,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonical)
        {
            this.owner = owner; this.binding = binding; this.preparation = preparation;
            Monitor.Enter(owner._gate, ref held);
            try
            {
                if (!preparation.IsCurrentPlain(binding) || owner._nativeWorld is not null &&
                    !ReferenceEquals(owner._nativeWorld, binding.Store)) throw InvalidMember();
                if (owner._states.TryGetValue(binding.Connection, out state))
                {
                    if (owner._nativeClientAuthority is not null &&
                        !ReferenceEquals(state.NativeClient?.Identity, binding.NativeIdentity)) throw InvalidMember();
                    roster = state.Team.Members.ToArray();
                    member = roster.SingleOrDefault(row => ReferenceEquals(row.Connection, binding.Connection) && row.Uid == binding.Uid);
                    if (member is not null && owner._nativeClientAuthority is not null &&
                        !ReferenceEquals(member.NativeClient?.Identity, binding.NativeIdentity)) throw InvalidMember();
                    before = member?.LegacyInfo;
                    if (before is not null)
                    {
                        if (before.Uid != binding.Uid) throw InvalidMember();
                        after = before with
                        {
                            MaxHitpoints = unchecked((ushort)WorldPacketHandlers.GetOwnedPhysicalDeathMaximum(preparation, canonical)),
                            MinHitpoints = unchecked((ushort)canonical.Character.MinHitPoints),
                        };
                    }
                }
                else roster = [];
                if (!IsCurrentPlain()) throw InvalidMember();
            }
            catch { Dispose(); throw; }
        }

        internal static OwnedPhysicalDeathMember Prepare(TeamSessionStateStore owner, LegacyOrdinarySpellBinding binding,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonical) =>
            new(owner, binding, preparation, canonical);

        internal IReadOnlyList<(ConquerTcpConnection Connection, long Uid)> Recipients =>
            roster.Select(row => (row.Connection, row.Uid)).ToArray();

        internal byte[]? CreateHistoricalFrame(uint maximum, int assigned)
        {
            if (!IsCurrentPlain()) throw InvalidMember();
            if (before is null) return null;
            var entry = before with { MaxHitpoints = unchecked((ushort)maximum), MinHitpoints = unchecked((ushort)assigned) };
            return new LegacyTeamMemberInfoPacket(LegacyTeamMemberInfoAction.AddMember, 1, 1, [entry]).ToFrameBytes();
        }

        internal bool IsCurrentPlain() => held && !published && Monitor.IsEntered(owner._gate) &&
            preparation.IsCurrentPlain(binding) && MappingMatches() &&
            (member is null || ReferenceEquals(member.LegacyInfo, before));

        private bool MappingMatches()
        {
            bool present = owner._states.TryGetValue(binding.Connection, out var actual);
            if (state is null) return !present;
            if (!present || !ReferenceEquals(actual, state) || state.Team.Members.Count != roster.Length ||
                owner._nativeClientAuthority is not null && !ReferenceEquals(state.NativeClient?.Identity, binding.NativeIdentity)) return false;
            for (int index = 0; index < roster.Length; index++)
                if (!ReferenceEquals(state.Team.Members[index], roster[index])) return false;
            return member is null || owner._nativeClientAuthority is null ||
                ReferenceEquals(member.NativeClient?.Identity, binding.NativeIdentity);
        }

        // Called only after all actual settlement participants passed final
        // before proofs. No team mutation or historical replay occurs later.
        internal void PublishPrepared()
        {
            if (member is not null && after is not null) member.LegacyInfo = after;
            published = true;
        }

        internal bool IsPublishedCurrentPlain()
        {
            lock (owner._gate)
                return published && MappingMatches() && (member is null || ReferenceEquals(member.LegacyInfo, after));
        }

        public void Dispose() { if (held) { held = false; Monitor.Exit(owner._gate); } }
        private static InvalidOperationException InvalidMember() => new("ordinaryPhysicalDeathTeamMemberChanged");
    }
}
