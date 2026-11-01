namespace WolfConquer.Core.World;

public sealed partial class TeamSessionStateStore
{
    // Held membership witness only. The registered proc request and real Store
    // writers own action admission/publication; a roster never grants damage.
    internal sealed class OwnedDragonhowlPermission : IDisposable
    {
        private readonly TeamSessionStateStore teams;
        private readonly WorldSessionStore store;
        private readonly WorldSessionStore.OwnedDragonhowlOwner purpose;
        private readonly LegacyOrdinarySpellBinding[] bindings;
        private readonly BasicPhysicalRoster[] rosters;
        private bool held;
        private OwnedDragonhowlPermission(TeamSessionStateStore teams, WorldSessionStore store,
            WorldSessionStore.OwnedDragonhowlOwner purpose)
        {
            this.teams = teams; this.store = store; this.purpose = purpose;
            if (!store.TryReadDragonhowlPermission(purpose, teams, out bindings) ||
                teams._nativeWorld is not null && !ReferenceEquals(teams._nativeWorld, store)) throw InvalidPermission();
            Monitor.Enter(teams._gate, ref held);
            try
            {
                rosters = bindings.Select(binding => new BasicPhysicalRoster(teams, binding)).ToArray();
                if (!IsCurrentPlain()) throw InvalidPermission();
            }
            catch { Dispose(); throw; }
        }
        internal static OwnedDragonhowlPermission Prepare(TeamSessionStateStore teams, WorldSessionStore store,
            WorldSessionStore.OwnedDragonhowlOwner purpose) => new(teams, store, purpose);
        internal bool IsCurrentPlain() => held && Monitor.IsEntered(teams._gate) &&
            store.TryReadDragonhowlPermission(purpose, teams, out var current) && current.SequenceEqual(bindings) &&
            rosters.All(row => row.IsCurrentPlain());
        public void Dispose() { if (held) { held = false; Monitor.Exit(teams._gate); } }
    }
}
