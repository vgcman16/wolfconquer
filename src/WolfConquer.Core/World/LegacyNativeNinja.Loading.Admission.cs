namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    internal static bool HasActiveOwnedPreparation(LegacyNativeGameClientRuntime client)
    {
        if (client is null || client.MyNinja is not { } ninja) return false;
        return ninja.IsCombatOwnedBy(client) && Monitor.IsEntered(ninja.persistenceLoadGate);
    }

    private void RefuseNinjaPublicEntry(LegacyNativeGameClientRuntime client)
    {
        if (Monitor.IsEntered(persistenceLoadGate) ||
            client.MySpells is { } spells && Monitor.IsEntered(spells.MutationGate))
            throw UnavailableRanking();
    }
}
