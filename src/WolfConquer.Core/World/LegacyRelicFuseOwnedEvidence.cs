using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

// Built from the actual prepared item-spell invocation. A graph witness or
// a packet supplied without this Store/Frame/before-image chain issues nothing.
internal sealed partial class LegacyRelicFuseOwnedEvidence
{
    private readonly WorldPacketHandlerContext context;
    private readonly WorldSession expected, stable;
    private readonly LegacyRelicFusePacket packet;
    private readonly WorldPacketHandlers.ItemSpellBeforeImages before;
    private readonly WorldPacketHandlers.LegacySpellMutationInputs inputProof;
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly WorldServerFrameHandler frame;
    private readonly LegacyOwnedAdmissionRuntime runtime;
    private readonly LegacyRelicFuseInputs inputs;
    private readonly LegacyRelicFusePreparation preparation;
    private readonly LegacyRelicFuseRuleGeneration ruleGeneration;
    private readonly Guid domain;
    private readonly uint accountId, characterUid;
    private LegacyItemUidReservationRequest? pending;
    private bool reserving;
    private WorldDataCatalog.LegacyRelicFuseRuleLease? ruleLease;

    private LegacyRelicFuseOwnedEvidence(WorldPacketHandlerContext context, WorldSession expected,
        WorldSession stable, LegacyRelicFusePacket packet, WorldPacketHandlers.ItemSpellBeforeImages before,
        WorldPacketHandlers.LegacySpellMutationInputs inputProof)
    {
        this.context = context; this.expected = expected; this.stable = stable; this.packet = packet; this.before = before; this.inputProof = inputProof;
        if (!ReferenceEquals(inputProof.Stable, stable)) throw Invalid();
        if (packet.Type != LegacyRelicFusePacket.ComposeType || context.Sessions is not { } store ||
            context.WorldData is not { } catalog || !ReferenceEquals(context.Session, expected)) throw Invalid();
        accountId = checked((uint)stable.AccountId);
        characterUid = checked((uint)stable.Character.Uid);
        if (accountId == 0 || characterUid == 0) throw Invalid();
        before.RequireRelicFuseOwner(context, stable);
        binding = expected.NativeGameClient?.OrdinarySpellBinding ?? throw Invalid();
        if (!ReferenceEquals(binding.Store, store) || !ReferenceEquals(binding.Connection, context.Connection) ||
            !ReferenceEquals(binding.ItemCatalog, catalog) || !ReferenceEquals(stable.NativeGameClient, binding.Client) ||
            !store.IsBoundSpellOwnerCurrent(binding) || !store.TryGet(context.Connection, out var current) ||
            !ReferenceEquals(current, expected) || !binding.Items.Image.IsCurrentPlain()) throw Invalid();
        if (!LegacyRelicFuseInputs.TryCapture(stable.PlayerItems, packet.RelicID1, packet.RelicID2, out var captured, out _) || captured is null ||
            !LegacyRelicFusePreparation.TryPrepare(catalog, captured, out var prepared, out _) || prepared is null) throw Invalid();
        inputs = captured; preparation = prepared; ruleGeneration = prepared.RuleGeneration;
        frame = store.RequireRelicFuseFrame(context, expected);
        runtime = frame.RequireRelicFuseRuntime(store, context);
        domain = runtime.RequireRelicFuseCapacity(frame);
        if (!DependenciesCurrent()) throw Invalid();
    }

    internal static LegacyRelicFuseOwnedEvidence Capture(WorldPacketHandlerContext context, WorldSession expected,
        WorldSession stable, LegacyRelicFusePacket packet, WorldPacketHandlers.ItemSpellBeforeImages before,
        WorldPacketHandlers.LegacySpellMutationInputs inputProof) =>
        new(context, expected, stable, packet, before, inputProof);

    internal bool TryAcquireRuleLease(out WorldDataCatalog.LegacyRelicFuseRuleLease? lease)
    {
        lease = null;
        if (ruleLease is not null || context.WorldData is not { } catalog ||
            !ReferenceEquals(catalog, binding.ItemCatalog) ||
            !catalog.TryAcquireLegacyRelicFuseRuleLease(ruleGeneration, out var acquired) || acquired is null)
            return false;
        ruleLease = acquired;
        if (!DependenciesCurrent()) { acquired.Dispose(); ruleLease = null; return false; }
        lease = acquired;
        return true;
    }

    private bool HasCurrentRuleLease() => ruleLease is { IsCurrent: true };

    private bool DependenciesCurrent() => ReferenceEquals(context.Sessions, binding.Store) &&
        ReferenceEquals(context.WorldData, binding.ItemCatalog) && binding.Store.IsBoundSpellOwnerCurrent(binding) &&
        binding.Store.TryGet(context.Connection, out var current) && ReferenceEquals(current, expected) &&
        binding.Items.Image.IsCurrentPlain() && ReferenceEquals(preparation.RuleGeneration, ruleGeneration) &&
        context.WorldData is { } data && ruleGeneration.IsCurrent(data) &&
        inputs.IsCurrent(stable.PlayerItems) && preparation.IsCurrent(stable.PlayerItems);

    internal void RequireReservationReady(LegacyOwnedAdmissionRuntime actual, WorldServerFrameHandler actualFrame,
        LegacyItemUidReservationRequest request)
    {
        if (!reserving || !HasCurrentRuleLease() || !ReferenceEquals(actual, runtime) || !ReferenceEquals(actualFrame, frame) ||
            !ReferenceEquals(request, pending) || !DependenciesCurrent()) throw Invalid();
        before.RequireRelicFuseOwner(context, stable);
    }

    // A reference check only; callable under the actual Registry gate without
    // reacquiring Store/native gates. It grants no reservation.
    internal void RequireRegistry(LegacyItemUidRegistry actual)
    {
        if (!ReferenceEquals(runtime.Ordinary.Registry, actual)) throw Invalid();
    }
    internal bool ConsumeInputProof() => HasCurrentRuleLease() && DependenciesCurrent() && inputProof.ConsumeCurrentPlain();
    private static InvalidOperationException Invalid() => new("relicFuseRequiresCurrentOwnedInputs");
}
