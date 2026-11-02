using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

// Retains the actual Store/Frame/native/saved-before invocation. The private
// projection is DATA; only the fixed writer and actual publisher commit it.
internal sealed partial class LegacyRelicFuseReturnOwnedEvidence
{
    private readonly WorldPacketHandlerContext context;
    private readonly WorldSession expected, stable;
    private readonly LegacyRelicFusePacket packet;
    private readonly WorldPacketHandlers.ItemSpellBeforeImages before;
    private readonly WorldPacketHandlers.LegacySpellMutationInputs inputProof;
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly WorldServerFrameHandler frame;
    private readonly LegacyOwnedAdmissionRuntime runtime;
    private readonly LegacyRelicFuseReturnPreparation preparation;
    private readonly uint accountId, characterUid;
    private readonly uint[] physicalUids;
    private readonly byte[] afterImage;
    private readonly byte[][] preparedFrames;
    private bool published;

    private LegacyRelicFuseReturnOwnedEvidence(WorldPacketHandlerContext context, WorldSession expected,
        WorldSession stable, LegacyRelicFusePacket packet, WorldPacketHandlers.ItemSpellBeforeImages before,
        WorldPacketHandlers.LegacySpellMutationInputs inputProof)
    {
        this.context = context; this.expected = expected; this.stable = stable;
        this.packet = packet; this.before = before; this.inputProof = inputProof;
        if (!ReferenceEquals(inputProof.Stable, stable) || packet.Type != LegacyRelicFusePacket.ReturnType ||
            context.Sessions is not { } store || context.WorldData is not { } catalog ||
            !ReferenceEquals(context.Session, expected)) throw Invalid();
        accountId = checked((uint)stable.AccountId); characterUid = checked((uint)stable.Character.Uid);
        if (accountId == 0 || characterUid == 0) throw Invalid();
        before.RequireRelicFuseOwner(context, stable);
        binding = expected.NativeGameClient?.OrdinarySpellBinding ?? throw Invalid();
        if (!ReferenceEquals(binding.Store, store) || !ReferenceEquals(binding.Connection, context.Connection) ||
            !ReferenceEquals(binding.ItemCatalog, catalog) || !ReferenceEquals(stable.NativeGameClient, binding.Client) ||
            !store.IsBoundSpellOwnerCurrent(binding) || !store.TryGet(context.Connection, out var current) ||
            !ReferenceEquals(current, expected) || !binding.Items.Image.IsCurrentPlain() ||
            !HeldItemsAreEligible(stable.PlayerItems) ||
            !LegacyRelicFuseReturnPreparation.TryPrepare(stable.PlayerItems, packet.RelicID1, out var prepared, out _) || prepared is null)
            throw Invalid();
        preparation = prepared;
        frame = store.RequireRelicFuseFrame(context, expected);
        frame.RequireRelicFuseReturnLifecycle(context);
        runtime = frame.RequireRelicFuseRuntime(store, context);
        physicalUids = WorldPacketHandlers.IndexRuneBagPhysicalItems(stable.PlayerItems).Keys.ToArray();
        _ = HighWater;
        if (!prepared.TryProject(stable.PlayerItems, out var projection, out _) || projection is null) throw Invalid();
        afterImage = PlayerItemSnapshotLoader.ToLegacyBytes(projection.Items);
        // Validate the exact existing item's packet widths and the original
        // echo before any writer runs. No random draw or new UID is involved.
        preparedFrames = [LegacyGameItemPacket.FromSnapshotItem(projection.Returned,
            mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(), packet.ToFrameBytes()];
        if (!DependenciesCurrent()) throw Invalid();
    }

    internal static LegacyRelicFuseReturnOwnedEvidence Capture(WorldPacketHandlerContext context,
        WorldSession expected, WorldSession stable, LegacyRelicFusePacket packet,
        WorldPacketHandlers.ItemSpellBeforeImages before, WorldPacketHandlers.LegacySpellMutationInputs inputProof) =>
        new(context, expected, stable, packet, before, inputProof);

    internal uint HighWater
    {
        get
        {
            if (!DependenciesCurrent()) throw Invalid();
            var actual = runtime.RequireRelicFuseReturnHighWater(frame);
            if (physicalUids.Any(uid => uid > actual)) throw Invalid();
            return actual;
        }
    }

    private bool DependenciesCurrent() => !published && HasActualFrameLifecycle() && ReferenceEquals(context.Sessions, binding.Store) &&
        ReferenceEquals(context.WorldData, binding.ItemCatalog) && binding.Store.IsBoundSpellOwnerCurrent(binding) &&
        binding.Store.TryGet(context.Connection, out var current) && ReferenceEquals(current, expected) &&
        binding.Items.Image.IsCurrentPlain() && HeldItemsAreEligible(stable.PlayerItems) && preparation.IsCurrent(stable.PlayerItems);

    private bool HasActualFrameLifecycle()
    {
        try { frame.RequireRelicFuseReturnLifecycle(context); return true; }
        catch (InvalidOperationException) { return false; }
    }

    internal bool ConsumeInputProof() => DependenciesCurrent() && inputProof.ConsumeCurrentPlain();
    private static InvalidOperationException Invalid() => new("relicFuseReturnRequiresCurrentOwnedInputs");
}
