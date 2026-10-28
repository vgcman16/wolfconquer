namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed class SpellActorScope : IDisposable
    {
        private readonly object client, player, spells, proficiencies;
        private bool clientHeld, playerHeld, spellsHeld, proficienciesHeld;

        internal SpellActorScope(LegacyOrdinarySpellBinding binding)
        {
            client = binding.Client.SpellAdmissionSyncRoot;
            player = binding.Player.SpellAdmissionSyncRoot;
            spells = binding.Storage.MutationGate;
            proficiencies = binding.Proficiencies.MutationGate;
            try
            {
                Monitor.Enter(client, ref clientHeld);
                Monitor.Enter(player, ref playerHeld);
                Monitor.Enter(spells, ref spellsHeld);
                Monitor.Enter(proficiencies, ref proficienciesHeld);
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (proficienciesHeld) { proficienciesHeld = false; Monitor.Exit(proficiencies); }
            if (spellsHeld) { spellsHeld = false; Monitor.Exit(spells); }
            if (playerHeld) { playerHeld = false; Monitor.Exit(player); }
            if (clientHeld) { clientHeld = false; Monitor.Exit(client); }
        }
    }
}
