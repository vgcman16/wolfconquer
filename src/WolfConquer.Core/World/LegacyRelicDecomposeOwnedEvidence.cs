using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Only the actual prepared invocation, captured beforeimages and live native
// binding may create this producer. Recipe DATA alone grants no authority.
internal sealed partial class LegacyRelicDecomposeOwnedEvidence
{
    private readonly WorldPacketHandlerContext context;
    private readonly WorldSession expected, stable;
    private readonly LegacyRelicDecomposeRequest request;
    private readonly LegacyRelicDecomposePreparation preparation;
    private readonly LegacyRelicDecomposeRecipe recipe;
    private readonly WorldPacketHandlers.ItemSpellBeforeImages before;
    private readonly WorldPacketHandlers.LegacySpellMutationInputs inputs;
    private readonly LegacyPreparedSpellFileWrite writer;
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly WorldServerFrameHandler frame;
    private readonly LegacyOwnedAdmissionRuntime runtime;
    private readonly Guid domain;
    private readonly uint accountId, characterUid;

    private LegacyRelicDecomposeOwnedEvidence(WorldPacketHandlerContext context, WorldSession expected,
        WorldSession stable, LegacyRelicDecomposeRequest request,
        WorldPacketHandlers.ItemSpellBeforeImages before,
        WorldPacketHandlers.LegacySpellMutationInputs inputs, LegacyPreparedSpellFileWrite writer)
    {
        this.context = context; this.expected = expected; this.stable = stable; this.request = request;
        this.before = before; this.inputs = inputs; this.writer = writer;
        if (!ReferenceEquals(context.Session, expected) || !ReferenceEquals(inputs.Stable, stable) ||
            context.Sessions is not { } store || context.WorldData is not { } catalog) throw Invalid();
        before.RequireRelicDecomposeOwner(context, stable);
        if (!before.IsRelicDecomposeWriter(writer)) throw Invalid();
        binding = expected.NativeGameClient?.OrdinarySpellBinding ?? throw Invalid();
        if (!ReferenceEquals(binding.Store, store) || !ReferenceEquals(binding.Connection, context.Connection) ||
            !ReferenceEquals(binding.ItemCatalog, catalog) || !ReferenceEquals(stable.NativeGameClient, binding.Client) ||
            !store.IsBoundSpellOwnerCurrent(binding) || !store.TryGet(context.Connection, out var current) ||
            !ReferenceEquals(current, expected) || !binding.Items.Image.IsCurrentPlain()) throw Invalid();
        accountId = checked((uint)stable.AccountId); characterUid = checked((uint)stable.Character.Uid);
        if (accountId == 0 || characterUid == 0 ||
            !LegacyRelicDecomposePreparation.TryPrepare(catalog, stable.PlayerItems, request, out var prepared, out _) ||
            prepared is null) throw Invalid();
        preparation = prepared; recipe = prepared.Recipe;
        frame = store.RequireRelicDecomposeFrame(context, expected);
        runtime = frame.RequireRelicDecomposeRuntime(store, context);
        runtime.RequireRelicDecomposePublicationPlan(frame);
        domain = runtime.RequireRelicDecomposeCapacity(frame, recipe.NewUidCount);
        if (!DependenciesCurrent()) throw Invalid();
    }

    internal static LegacyRelicDecomposeOwnedEvidence Capture(WorldPacketHandlerContext context, WorldSession expected,
        WorldSession stable, LegacyRelicDecomposeRequest request, WorldPacketHandlers.ItemSpellBeforeImages before,
        WorldPacketHandlers.LegacySpellMutationInputs inputs, LegacyPreparedSpellFileWrite writer) =>
        new(context, expected, stable, request, before, inputs, writer);

    private bool DependenciesCurrent() => ReferenceEquals(context.Sessions, binding.Store) &&
        ReferenceEquals(context.WorldData, binding.ItemCatalog) && binding.Store.IsBoundSpellOwnerCurrent(binding) &&
        binding.Store.TryGet(context.Connection, out var current) && ReferenceEquals(current, expected) &&
        binding.Items.Image.IsCurrentPlain() && preparation.IsCurrent(stable.PlayerItems);

    internal void RequireReservationReady(LegacyOwnedAdmissionRuntime actual, WorldServerFrameHandler actualFrame,
        LegacyItemUidReservationRequest actualRequest)
    {
        if (!reserving || !ReferenceEquals(actual, runtime) || !ReferenceEquals(actualFrame, frame) ||
            !ReferenceEquals(actualRequest, pending) || !DependenciesCurrent()) throw Invalid();
        before.RequireRelicDecomposeOwnerHeld(context, stable);
    }
    internal void RequireRegistry(LegacyItemUidRegistry actual)
    { if (!ReferenceEquals(runtime.Ordinary.Registry, actual)) throw Invalid(); }
    internal bool ConsumeInputProof() => DependenciesCurrent() && inputs.ConsumeCurrentPlain();
    internal void ThrowBeforeRankingIfPlanned() => runtime.ThrowBeforeRelicDecomposeRankingIfPlanned(frame);
    internal string[] FilePaths => [before.CharacterPath, before.ItemPath, before.SpellsPath];
    internal string ItemPath => before.ItemPath;
    private static InvalidOperationException Invalid() => new("relicDecomposeRequiresCurrentOwnedInputs");
}
