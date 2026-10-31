using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Only the actual admitted producer can issue this capability. Captured
// operation JSON and receipt DTOs cannot construct or install it.
internal sealed partial class LegacyRuneBagEvidence
{
    private readonly WorldPacketHandlerContext context;
    private readonly WorldSession stable, expected;
    private readonly WorldPacketHandlers.ItemSpellBeforeImages before;
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly WorldServerFrameHandler frame;
    private readonly LegacyOwnedAdmissionRuntime runtime;
    private readonly Guid domain;
    private readonly uint selectedUid;
    private readonly bool intoBag;
    private LegacyItemUidReservationRequest? request;
    private LegacyItemUidReservationReceipt? issuedAllocation;
    private bool reserving;
    internal LegacyRuneBagOperation Operation { get; }
    internal LegacyRuneBagWireEvidence Wire { get; }
    internal string ItemPath => before.ItemPath;

    private LegacyRuneBagEvidence(WorldPacketHandlerContext context, WorldSession stable, uint uid, bool intoBag,
        WorldPacketHandlers.ItemSpellBeforeImages before)
    {
        this.context = context; this.stable = stable; this.before = before; selectedUid = uid; this.intoBag = intoBag;
        before.RequireRuneBagOwner(context, stable);
        binding = context.Connection.NativeGameClient?.OrdinarySpellBinding ?? throw Invalid();
        if (!ReferenceEquals(context.Sessions, binding.Store) || !ReferenceEquals(binding.ItemCatalog, context.WorldData) ||
            !binding.Store.TryGet(context.Connection, out var current) || current is null ||
            current.Character.Uid != stable.Character.Uid || !ReferenceEquals(current.NativeGameClient, binding.Client)) throw Invalid();
        expected = current;
        frame = binding.Store.RequireRuneBagFrame(context, expected);
        runtime = frame.RequireRuneBagRuntime(binding.Store, context);
        domain = runtime.RequireRuneBagDomain(frame);
        if (stable.Character.Reborn < 2 || stable.Character.Level < 15) throw Invalid();
        var target = (intoBag ? stable.PlayerItems.InventoryItems : stable.PlayerItems.RuneItems).Single(row => row.Uid == uid);
        Wire = LegacyRuneBagWireEvidence.Capture(context, stable, target.ItemId);
        var recipe = LegacyRuneBagRecipe.Prepare(stable.PlayerItems, uid, intoBag, Wire.Metadata);
        _ = LegacyGameItemPacket.FromSnapshotItem(target, mode: LegacyGameItemPacket.AddItemMode, runeSpellOverride: Wire.RuneOverride).ToFrameBytes();
        if (recipe.Plan.NewUidCount == 0)
            _ = LegacyGameItemPacket.FromSnapshotItem(recipe.Project(null).ResultItem,
                mode: intoBag ? LegacyGameItemPacket.AddItemMode : LegacyGameItemPacket.UpdateItemMode,
                runeSpellOverride: Wire.RuneOverride).ToFrameBytes();
        before.RequireRuneBagOwner(context, stable);
        if (!DependenciesAreCurrent() || runtime.RequireRuneBagDomain(frame) != domain) throw Invalid();
        RequireRawNamespace();
        runtime.RequireRuneBagPreflight(frame, recipe.Plan.NewUidCount);
        if (recipe.Plan.NewUidCount == 0)
        {
            Operation = new(intoBag, uid, Wire.Metadata, null, null); Operation.Validate();
            if (!IsCurrent()) throw Invalid();
            return;
        }
        request = new(Guid.NewGuid(), LegacyItemUidRegistry.RuneBagWriter, checked((uint)stable.AccountId),
            checked((uint)stable.Character.Uid), LegacyRuneBagRecipeDigest.Compute(domain, checked((uint)stable.AccountId),
                checked((uint)stable.Character.Uid), uid, intoBag, Wire.Metadata,
                before.CopyCharacter(), before.CopyItems(), before.CopySpells()), 1);
        try
        {
            LegacyItemUidRegistry.Reservation? reservation;
            LegacyItemUidReservationReceipt? allocation;
            reserving = true;
            try
            {
                var result = runtime.ReserveRuneBagNamespace(frame, this, request, out reservation, out allocation, out var reason);
                if (result == LegacyItemUidReservationOutcome.Reserved && reservation is not null && allocation is not null)
                    issuedAllocation = allocation;
                if (result != LegacyItemUidReservationOutcome.Reserved || reservation is null || allocation is null)
                    throw new InvalidOperationException("runeBagNamespaceReservation:" + reason);
            }
            finally { reserving = false; }
            if (!reservation.TryTake(out var ids) || ids.Length != 1 || ids[0] != allocation.First) throw Invalid();
            Operation = new(intoBag, uid, Wire.Metadata, request, allocation); Operation.Validate();
            RequireRawNamespace();
            if (!IsCurrent()) throw Invalid();
            context.ItemSpellJournalInterruption?.ArmRuneBagBeforeWriter(this);
        }
        catch { FenceIssuedNamespace(); throw; }
    }

    internal static LegacyRuneBagEvidence Capture(WorldPacketHandlerContext context, WorldSession stable, uint uid,
        bool intoBag, WorldPacketHandlers.ItemSpellBeforeImages before) => new(context, stable, uid, intoBag, before);

    internal void RequireReservationReady(LegacyOwnedAdmissionRuntime actual, WorldServerFrameHandler actualFrame,
        LegacyItemUidReservationRequest actualRequest)
    {
        if (!reserving || !ReferenceEquals(actual, runtime) || !ReferenceEquals(actualFrame, frame) ||
            !ReferenceEquals(actualRequest, request) || !DependenciesAreCurrent()) throw Invalid();
        before.RequireRuneBagOwner(context, stable);
        RequireRawNamespace();
    }

    private bool DependenciesAreCurrent() => ReferenceEquals(context.WorldData, binding.ItemCatalog) &&
        binding.Store.IsBoundSpellOwnerCurrent(binding) && binding.Store.TryGet(context.Connection, out var current) &&
        ReferenceEquals(current, expected) && binding.Items.Image.IsCurrentPlain() && Wire.IsCurrent();

    internal bool IsCurrent()
    {
        if (!DependenciesAreCurrent() || Operation.SelectedUid != selectedUid || Operation.IntoBag != intoBag ||
            Operation.Metadata != Wire.Metadata || !ReferenceEquals(Operation.Request, request)) return false;
        if (Operation.Allocation is { } allocation) runtime.RequireRuneBagAllocation(frame, request!, allocation);
        else if (runtime.RequireRuneBagDomain(frame) != domain) return false;
        return true;
    }

    internal void RequireRegistry(LegacyItemUidRegistry actual)
    { if (!ReferenceEquals(actual, runtime.Ordinary.Registry)) throw Invalid(); }
    internal string SavedBeforePath(int ordinal) => ordinal switch
    {
        0 => before.CharacterPath, 1 => before.ItemPath, 2 => before.SpellsPath,
        _ => throw new ArgumentOutOfRangeException(nameof(ordinal)),
    };
    internal void FenceUnlessCommitted()
    { if (issuedAllocation is not null && !before.HasCommittedRuneBag(this)) FenceIssuedNamespace(); }
    private void FenceIssuedNamespace()
    { if (issuedAllocation is not null) runtime.FenceRuneBagNamespace(frame); }
    internal void RetireCommittedNamespace(LegacyPreparedSpellFileWrite.RuneCharacterPublication proof)
    {
        if (Operation.Allocation is not { } allocation) return;
        try
        {
            if (!runtime.RetireRuneBagNamespace(frame, proof, allocation, out _)) throw new LegacyCommittedSpellPublicationException();
        }
        catch (Exception)
        {
            runtime.Recovery.Block("ordinarySpellRecoveryRequired:committedRuneBagNamespaceUnproven");
            throw new LegacyCommittedSpellPublicationException();
        }
    }

    private static InvalidOperationException Invalid() => new("runeBagRequiresCurrentAdmittedDependencies");
}
