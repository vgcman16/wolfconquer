using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private bool TryPublishRelicDecomposeRankingHeld(RelicDecomposePreparationState state, WorldSession final)
    {
        if (state.Context.RuneRankings is not { } ranking) return true;
        if (state.Context.RuneRankingPublicationOwner is not { } owner ||
            !IsCurrentThreadInsideAnyConnectionGate || !Monitor.IsEntered(state.Binding.Client.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(state.Binding.Player.SpellAdmissionSyncRoot) ||
            !BindingIsCurrentPlain(state.Binding, state.Holder, final) ||
            !owner.TryCurrent(state.Context with { Session = final }, final, out var current) || !ReferenceEquals(current, final) ||
            !LegacyItemSaveGraph.TryCapture(final.PlayerItems, out var graph) || graph is null) return false;
        var stable = graph.WithOwnedItemsForPersistence(final);
        // This leaf is reached only AFTER the Runtime/Registry reservation and
        // retirement methods have returned. Never acquire ranking under Registry.
        using var lease = ranking.PrepareUpdate(stable.Character, stable.PlayerItems);
        if (!graph.IsCurrent(final.PlayerItems) || !lease.IsCurrentPlain() ||
            !BindingIsCurrentPlain(state.Binding, state.Holder, final) ||
            !owner.TryCurrent(state.Context with { Session = final }, final, out current) || !ReferenceEquals(current, final)) return false;
        lease.Publish(); return true;
    }
}
