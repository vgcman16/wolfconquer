namespace WolfConquer.Core.World;

// This is an exclusion scope for the still-unbound whole components, not a cast
// authority. Admission uses these real actors too, so it cannot attach halfway
// through a raw executor's late effects. No Store acquisition/callback occurs.
internal sealed class LegacyNativeUnboundSpellScope : IDisposable
{
    private readonly LegacyNativeGameClientRuntime client;
    private readonly LegacyNativePlayerRuntime player;
    private bool clientHeld, playerHeld;
    private LegacyNativeUnboundSpellScope(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player)
    { this.client = client; this.player = player; }

    internal static bool TryEnter(LegacyNativeGameClientRuntime client, out LegacyNativeUnboundSpellScope? scope)
    {
        scope = null;
        if (client is null || client.Player is not { } player || !ReferenceEquals(player.Owner, client) ||
            client.OrdinarySpellBinding is not null || player.HasOwnedVitalsAttachment ||
            WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
            Monitor.IsEntered(player.SpellAdmissionSyncRoot) && !Monitor.IsEntered(client.SpellAdmissionSyncRoot)) return false;
        var candidate = new LegacyNativeUnboundSpellScope(client, player);
        try
        {
            Monitor.Enter(client.SpellAdmissionSyncRoot, ref candidate.clientHeld);
            Monitor.Enter(player.SpellAdmissionSyncRoot, ref candidate.playerHeld);
            if (!ReferenceEquals(client.Player, player) || client.OrdinarySpellBinding is not null || player.HasOwnedVitalsAttachment)
            { candidate.Dispose(); return false; }
            scope = candidate; return true;
        }
        catch { candidate.Dispose(); throw; }
    }
    public void Dispose()
    {
        if (playerHeld) { playerHeld = false; Monitor.Exit(player.SpellAdmissionSyncRoot); }
        if (clientHeld) { clientHeld = false; Monitor.Exit(client.SpellAdmissionSyncRoot); }
    }
}
