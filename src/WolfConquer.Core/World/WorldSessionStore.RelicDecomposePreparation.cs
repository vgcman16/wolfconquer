using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed class RelicDecomposePreparationState(WorldPacketHandlerContext context, SessionHolder holder,
        LegacyOrdinarySpellBinding binding, LegacyPreparedSpellFileWrite writer,
        WorldPacketHandlers.ItemSpellBeforeImages before, WorldPacketHandlers.LegacySpellMutationInputs inputs)
    {
        internal readonly WorldPacketHandlerContext Context = context;
        internal readonly SessionHolder Holder = holder;
        internal readonly LegacyOrdinarySpellBinding Binding = binding;
        internal readonly LegacyPreparedSpellFileWrite Writer = writer;
        internal readonly WorldPacketHandlers.ItemSpellBeforeImages Before = before;
        internal readonly WorldPacketHandlers.LegacySpellMutationInputs Inputs = inputs;
        internal readonly int Thread = Environment.CurrentManagedThreadId;
        internal bool Active = true;
        internal PreparedSessionSpellPublication? Publication;
        internal LegacyTradeJournalChange[]? Changes;
        internal RelicWireOutput? Output;
        internal WorldSession? Final;
        internal string Reason = "relicDecomposeStoreOwnerUnavailable";
    }
    private readonly ConditionalWeakTable<LegacyRelicDecomposeOwnedEvidence, RelicDecomposePreparationState> relicDecomposePreparations = new();

    // Fixed registry-preview continuation. No caller validator, saver or success
    // callback is accepted. All relevant gates were entered BEFORE registry.
    internal bool TryPrepareRelicDecomposeNativeHeld(LegacyRelicDecomposeOwnedEvidence evidence,
        WorldSession candidate, out string reason)
    {
        reason = "relicDecomposeNativePreparationOwnerUnavailable";
        if (!relicDecomposePreparations.TryGetValue(evidence, out var state) || !state.Active || state.Publication is not null ||
            state.Thread != Environment.CurrentManagedThreadId || state.Context.Session is not { } expected ||
            !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
            !Monitor.IsEntered(state.Binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(state.Binding.Player.SpellAdmissionSyncRoot) ||
            !BindingIsCurrentPlain(state.Binding, state.Holder, expected) ||
            state.Context.TradeOperations?.OwnsActiveExclusiveLease(state.Context.FrameOperationLease) != true) return false;
        state.Before.RequireRelicDecomposeOwnerHeld(state.Context, state.Inputs.Stable);
        if (!LegacyItemSaveGraph.TryCapture(candidate.PlayerItems, out var graph) || graph is null) return false;
        var proposed = graph.WithOwnedItemsForPersistence(candidate);
        state.Writer.PrepareImage(proposed.PlayerSpells);
        var publication = new PreparedSessionSpellPublication(this, state.Context.Connection, state.Holder, expected, proposed);
        state.Publication = publication;
        publication.ArmFinalProof();
        state.Changes = WorldPacketHandlers.PrepareRelicDecomposeJournalImages(state.Inputs, publication.FinalSession, state.Before, state.Writer);
        state.Output = PrepareRelicDecomposeWire(state.Context, state.Holder, state.Binding, state.Writer, evidence);
        if (!publication.IsCurrentPlain()) return false;
        reason = string.Empty; return true;
    }
}
