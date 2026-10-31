using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// The real current Runtime/Frame/Store issues this once after complete recipe
// preflight. Detached operation/receipt data cannot construct it or grant items.
internal sealed partial class LegacyRuneDecomposeEvidence
{
    private readonly WorldPacketHandlerContext context;
    private readonly WorldSession stable, expected;
    private readonly WorldPacketHandlers.ItemSpellBeforeImages before;
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly WorldServerFrameHandler frame;
    private readonly LegacyOwnedAdmissionRuntime runtime;
    private readonly Guid domain;
    private readonly uint selectedUid;
    private readonly RewardCatalog catalog;
    private readonly LegacyRuneComposeProgressWitness progress;
    private readonly LegacyRuneOperationEvidence.CatalogWitness? providers;
    private readonly LegacyRuneMetadata[] runeMetadata;
    private readonly LegacyRuneDecomposeRewardMetadata[] rewardMetadata;
    private readonly LegacyRuneDecomposeProgressRow progressRow;
    private readonly LegacyItemUidReservationRequest request;
    private LegacyItemUidReservationReceipt? issuedAllocation;
    private bool reserving;
    internal LegacyRuneDecomposeOperation Operation { get; }
    internal string ItemPath => before.ItemPath;

    private LegacyRuneDecomposeEvidence(WorldPacketHandlerContext context, WorldSession stable, uint uid,
        WorldPacketHandlers.ItemSpellBeforeImages before)
    {
        this.context = context; this.stable = stable; this.before = before; selectedUid = uid;
        before.RequireRuneDecomposeOwner(context, stable, uid);
        binding = context.Connection.NativeGameClient?.OrdinarySpellBinding ?? throw Invalid();
        if (!ReferenceEquals(context.Sessions, binding.Store) || !ReferenceEquals(binding.ItemCatalog, context.WorldData) ||
            !binding.Store.TryGet(context.Connection, out var current) || current is null ||
            current.Character.Uid != stable.Character.Uid || !ReferenceEquals(current.NativeGameClient, binding.Client)) throw Invalid();
        expected = current;
        frame = binding.Store.RequireRuneDecomposeFrame(context, expected);
        runtime = frame.RequireRuneDecomposeRuntime(binding.Store, context);
        domain = runtime.RequireRuneDecomposeDomain(frame);
        var adopted = context.WorldData ?? throw Invalid();
        WorldPacketHandlers.RequireRuneDecomposePlacement(stable.PlayerItems);
        var target = stable.PlayerItems.RuneItems.Single(row => row.Uid == uid);
        uint family = target.ItemId / 10_000, level = target.ItemId % 100;
        progress = new(adopted);
        if (level is 0 or > 27 || !progress.Lookup.TryGetValue((family, checked((byte)level)), out var curve)) throw Invalid();
        var used = progress.CaptureUsed(new HashSet<(uint Family, byte Level)> { (family, checked((byte)level)) });
        if (used.Length != 1 || used[0] != curve) throw Invalid();
        progressRow = new(curve.Family, curve.Level, curve.Progress);
        uint essence = family switch { 401 => 4_060_001, 402 => 4_040_001, 403 => 4_050_001, _ => throw Invalid() };
        uint fragment = family switch
        {
            401 => target.Bound == 0 ? 3_306_366U : 3_306_369U,
            402 => target.Bound == 0 ? 3_306_368U : 3_306_371U,
            _ => target.Bound == 0 ? 3_306_367U : 3_306_370U,
        };
        catalog = new(adopted, target.ItemId, essence, fragment);
        rewardMetadata = catalog.Metadata.ToArray();
        var plan = WorldPacketHandlers.PlanRecordedRuneDecompose(stable.PlayerItems, uid, progressRow, catalog.Caps);
        if (plan.EssenceStacks.Length == 0 || plan.NewUidCount is < 1 or > 101) throw Invalid();
        if (target.Position is not (211 or 212))
            providers = new(adopted, LegacyRuneOperation.RequiredMetadata(stable.PlayerItems, uid));
        runeMetadata = providers?.Rows.ToArray() ?? [];
        // This game projection preflight retires only exact owned default rows.
        // Stored targets retain Character/Spells identity with no unused mapping.
        PreflightDerivableImages(plan);
        before.RequireRuneDecomposeOwner(context, stable, uid);
        if (!DependenciesAreCurrent() || runtime.RequireRuneDecomposeDomain(frame) != domain) throw Invalid();
        runtime.RequireRuneDecomposePreflight(frame, plan.NewUidCount);
        byte? color = plan.FragmentStackUid is null ? checked((byte)Random.Shared.Next(3, 9)) : null;
        var digest = LegacyRuneDecomposeRecipeDigest.Compute(domain, checked((uint)stable.AccountId), checked((uint)stable.Character.Uid),
            uid, progressRow, runeMetadata, rewardMetadata, color, before.CopyCharacter(), before.CopyItems(), before.CopySpells());
        request = new(Guid.NewGuid(), LegacyItemUidRegistry.RuneDecomposeWriter, checked((uint)stable.AccountId),
            checked((uint)stable.Character.Uid), digest, plan.NewUidCount);
        try
        {
            LegacyItemUidRegistry.Reservation? reservation;
            LegacyItemUidReservationReceipt? allocation;
            reserving = true;
            try
            {
                var result = runtime.ReserveRuneDecomposeNamespace(frame, this, request, out reservation, out allocation, out var reason);
                if (result == LegacyItemUidReservationOutcome.Reserved && reservation is not null && allocation is not null)
                    issuedAllocation = allocation; // Actual fresh issuer result, never a caller claim.
                if (result != LegacyItemUidReservationOutcome.Reserved || reservation is null || allocation is null)
                    throw new InvalidOperationException("runeDecomposeNamespaceReservation:" + reason);
            }
            finally { reserving = false; }
            if (!reservation.TryTake(out var ids) || ids.Length != request.Count ||
                ids.Where((id, index) => id != (ulong)allocation.First + (uint)index).Any()) throw Invalid();
            Operation = new(uid, progressRow, runeMetadata.ToArray(), rewardMetadata.ToArray(), request, allocation, color);
            Operation.Validate();
            if (!IsCurrent()) throw Invalid();
            context.ItemSpellJournalInterruption?.ArmRuneDecomposeBeforeWriter(this);
        }
        catch { FenceIssuedNamespace(); throw; }
    }

    internal static LegacyRuneDecomposeEvidence Capture(WorldPacketHandlerContext context, WorldSession stable, uint uid,
        WorldPacketHandlers.ItemSpellBeforeImages before) => new(context, stable, uid, before);

    internal void RequireReservationReady(LegacyOwnedAdmissionRuntime actual, WorldServerFrameHandler actualFrame,
        LegacyItemUidReservationRequest actualRequest)
    {
        if (!reserving || !ReferenceEquals(actual, runtime) || !ReferenceEquals(actualFrame, frame) ||
            !ReferenceEquals(actualRequest, request) || !DependenciesAreCurrent()) throw Invalid();
        before.RequireRuneDecomposeOwner(context, stable, selectedUid);
    }

    private bool DependenciesAreCurrent() => ReferenceEquals(context.WorldData, binding.ItemCatalog) &&
        binding.Store.IsBoundSpellOwnerCurrent(binding) && binding.Store.TryGet(context.Connection, out var current) &&
        ReferenceEquals(current, expected) && binding.Items.Image.IsCurrentPlain() && catalog.IsCurrent() && progress.IsCurrent() &&
        (providers is null || providers.IsCurrent());

    internal bool IsCurrent()
    {
        if (!DependenciesAreCurrent() || Operation.SelectedUid != selectedUid || !ReferenceEquals(Operation.Request, request) || Operation.Progress != progressRow ||
            !Operation.RuneMetadata.SequenceEqual(runeMetadata) || !Operation.RewardMetadata.SequenceEqual(rewardMetadata)) return false;
        runtime.RequireRuneDecomposeAllocation(frame, request, Operation.Allocation);
        return true;
    }

    internal void RequireRegistry(LegacyItemUidRegistry actual)
    {
        if (!ReferenceEquals(actual, runtime.Ordinary.Registry)) throw Invalid();
    }

    internal void RequireProposedCharacter(CharacterSnapshot proposed)
    {
        var projected = WorldPacketHandlers.ProjectRecordedRuneDecompose(stable.Character, stable.PlayerItems, stable.PlayerSpells, Operation);
        if (!WorldPacketHandlers.RuneDecomposeCharactersEqual(projected.Character, proposed)) throw Invalid();
    }

    private static InvalidOperationException Invalid() => new("runeDecomposeRequiresCurrentAdmittedDependencies");
}
