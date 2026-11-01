namespace WolfConquer.Core.World;

public sealed partial class TeamSessionStateStore
{
    // The caller already holds the genuine proc lifecycle and ordered actor
    // gates. The team gate is always acquired last and never crosses an await.
    internal DragonhowlExperienceRoster? CaptureDragonhowlExperienceRoster(WorldSessionStore store,
        WorldSessionStore.OwnedDragonhowlOwner owner, LegacyOrdinarySpellBinding killerBinding) =>
        DragonhowlExperienceRoster.TryCapture(this, store, owner, killerBinding);

    internal sealed class DragonhowlExperienceRoster
    {
        private readonly TeamSessionStateStore teams;
        private readonly WorldSessionStore store;
        private readonly WorldSessionStore.OwnedDragonhowlOwner owner;
        private readonly LegacyOrdinarySpellBinding killer;
        private readonly BasicPhysicalRoster membership;

        internal CombatExperienceRoster? Roster { get; }

        private DragonhowlExperienceRoster(TeamSessionStateStore teams, WorldSessionStore store,
            WorldSessionStore.OwnedDragonhowlOwner owner, LegacyOrdinarySpellBinding killer,
            BasicPhysicalRoster membership, CombatExperienceRoster? roster)
        {
            this.teams = teams; this.store = store; this.owner = owner; this.killer = killer;
            this.membership = membership; Roster = roster;
        }

        internal static DragonhowlExperienceRoster? TryCapture(TeamSessionStateStore teams, WorldSessionStore store,
            WorldSessionStore.OwnedDragonhowlOwner owner, LegacyOrdinarySpellBinding killer)
        {
            if (!store.IsDragonhowlExperienceKillerHeld(owner, teams, killer) ||
                teams._nativeWorld is not null && !ReferenceEquals(teams._nativeWorld, store)) return null;
            lock (teams._gate)
            {
                if (!store.IsDragonhowlExperienceKillerHeld(owner, teams, killer)) return null;
                bool present = teams._states.TryGetValue(killer.Connection, out _);
                var membership = new BasicPhysicalRoster(teams, killer);
                var roster = CombatExperienceRoster.Capture(teams, killer);
                // A failed populated-team capture is corruption/refusal, not
                // an absence branch. Genuine absence has its own retained proof.
                if (present != (roster is not null)) return null;
                var captured = new DragonhowlExperienceRoster(teams, store, owner, killer, membership, roster);
                return captured.IsCurrentPlain() ? captured : null;
            }
        }

        internal bool IsCurrentPlain()
        {
            if (!store.IsDragonhowlExperienceKillerHeld(owner, teams, killer) ||
                teams._nativeWorld is not null && !ReferenceEquals(teams._nativeWorld, store)) return false;
            lock (teams._gate)
            {
                if (!store.IsDragonhowlExperienceKillerHeld(owner, teams, killer) || !membership.IsCurrentPlain()) return false;
                if (Roster is null) return !teams._states.TryGetValue(killer.Connection, out _);
                // Existing scope additionally pins every mapped member and the
                // real membership generation. It grants no XP or proc phase.
                using var scope = Roster.TryEnter(killer);
                return scope?.IsCurrentPlain() == true;
            }
        }
    }
}
