using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class TeamSessionStateStore
{
    // Membership evidence only. The caller retains its real XP and file owners.
    internal CombatExperienceRoster? CaptureCombatExperienceRoster(LegacyOrdinarySpellBinding killer)
    {
        lock (_gate)
            return CombatExperienceRoster.Capture(this, killer);
    }

    internal sealed class CombatExperienceRoster
    {
        private readonly TeamSessionStateStore owner;
        private readonly TeamSessionState state;
        private readonly object generation;
        private readonly Row[] rows;
        private readonly IReadOnlyList<(ConquerTcpConnection Connection, long Uid)> members;

        private CombatExperienceRoster(TeamSessionStateStore owner, TeamSessionState state, Row[] rows)
        {
            this.owner = owner;
            this.state = state;
            generation = state.Team.MembershipGeneration;
            this.rows = rows;
            members = Array.AsReadOnly(rows.Select(row => (row.Member.Connection, row.Member.Uid)).ToArray());
        }

        internal static CombatExperienceRoster? Capture(TeamSessionStateStore owner, LegacyOrdinarySpellBinding killer)
        {
            if (!Monitor.IsEntered(owner._gate) || owner._nativeWorld is not null && !ReferenceEquals(owner._nativeWorld, killer.Store) ||
                !owner._states.TryGetValue(killer.Connection, out var state) || state.MemberUid != killer.Uid ||
                owner._nativeClientAuthority is not null && !ReferenceEquals(state.NativeClient?.Identity, killer.NativeIdentity)) return null;
            var rows = new List<Row>();
            foreach (var member in state.Team.Members)
            {
                if (!owner._states.TryGetValue(member.Connection, out var mapped) || !ReferenceEquals(mapped.Team, state.Team) ||
                    mapped.MemberUid != member.Uid || !ReferenceEquals(mapped.NativeClient, member.NativeClient) ||
                    rows.Any(row => ReferenceEquals(row.Member.Connection, member.Connection) || row.Member.Uid == member.Uid)) return null;
                rows.Add(new Row(member, mapped, member.NativeClient, member.NativeClient?.Identity));
            }
            if (!rows.Any(row => ReferenceEquals(row.Member.Connection, killer.Connection) && row.Member.Uid == killer.Uid &&
                (owner._nativeClientAuthority is null || ReferenceEquals(row.NativeIdentity, killer.NativeIdentity)))) return null;
            return new CombatExperienceRoster(owner, state, rows.ToArray());
        }

        internal IReadOnlyList<(ConquerTcpConnection Connection, long Uid)> Members => members;

        // Caller already owns the actual recipient connection and client/player gates.
        // This scope takes the team gate last and must be disposed before any await.
        internal CombatExperienceRosterScope? TryEnter(LegacyOrdinarySpellBinding recipient) =>
            CombatExperienceRosterScope.TryEnter(this, recipient);

        private bool MatchesPlain(LegacyOrdinarySpellBinding recipient)
        {
            if (!Monitor.IsEntered(owner._gate) || !ReferenceEquals(state.Team.MembershipGeneration, generation) ||
                state.Team.Members.Count != rows.Length || owner._nativeWorld is not null && !ReferenceEquals(owner._nativeWorld, recipient.Store)) return false;
            bool found = false;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (!ReferenceEquals(state.Team.Members[i], row.Member) ||
                    !owner._states.TryGetValue(row.Member.Connection, out var mapped) || !ReferenceEquals(mapped, row.State) ||
                    !ReferenceEquals(mapped.Team, state.Team) || mapped.MemberUid != row.Member.Uid ||
                    !ReferenceEquals(row.Member.NativeClient, row.NativeClient) || !ReferenceEquals(mapped.NativeClient, row.NativeClient) ||
                    !ReferenceEquals(row.Member.NativeClient?.Identity, row.NativeIdentity)) return false;
                if (ReferenceEquals(row.Member.Connection, recipient.Connection) && row.Member.Uid == recipient.Uid &&
                    (owner._nativeClientAuthority is null || ReferenceEquals(row.NativeIdentity, recipient.NativeIdentity))) found = true;
            }
            return found;
        }

        private sealed record Row(TeamSessionMember Member, TeamSessionState State, LegacyTournamentWatcherClient? NativeClient, object? NativeIdentity);

        internal sealed class CombatExperienceRosterScope : IDisposable
        {
            private readonly CombatExperienceRoster roster;
            private readonly LegacyOrdinarySpellBinding recipient;
            private bool held;
            private CombatExperienceRosterScope(CombatExperienceRoster roster, LegacyOrdinarySpellBinding recipient)
            { this.roster = roster; this.recipient = recipient; }

            internal static CombatExperienceRosterScope? TryEnter(CombatExperienceRoster roster, LegacyOrdinarySpellBinding recipient)
            {
                if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                    !Monitor.IsEntered(recipient.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(recipient.Player.SpellAdmissionSyncRoot) ||
                    !recipient.Store.IsBoundSpellOwnerCurrent(recipient)) return null;
                var scope = new CombatExperienceRosterScope(roster, recipient);
                Monitor.Enter(roster.owner._gate, ref scope.held);
                try
                {
                    if (scope.IsCurrentPlain()) return scope;
                    scope.Dispose();
                    return null;
                }
                catch { scope.Dispose(); throw; }
            }

            internal bool IsCurrentPlain() => held && WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
                Monitor.IsEntered(recipient.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(recipient.Player.SpellAdmissionSyncRoot) &&
                recipient.Store.IsBoundSpellOwnerCurrent(recipient) && roster.MatchesPlain(recipient);

            public void Dispose() { if (held) { held = false; Monitor.Exit(roster.owner._gate); } }
        }
    }
}
