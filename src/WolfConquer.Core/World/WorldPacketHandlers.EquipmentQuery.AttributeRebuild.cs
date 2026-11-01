using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Issued only under the actual allocation's Frame/Store/native gates. This
    // carries preflight, not a simulated successful query or replay permission.
    internal sealed class PreparedAttributeRebuild
    {
        private readonly WorldPacketHandlerContext context;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSession expected, proposed;
        private readonly LegacyNativeAttributeMapProof attributes, paid;
        private readonly LegacyItemSaveGraph items;
        private readonly LegacyOwnedPhysicalMetadataWitness metadata;
        private readonly LegacyAttackSpeedMetadata speed;
        private readonly LegacyPreparedSpellFileWrite? spellFile;
        private WorldSession? committed;
        private int started;

        // The actual prepared file owner supplies this path for one sorted
        // Character/Spells lock set at the allocation writer boundary.
        internal string? LockedSpellPath => spellFile?.SpellsPath;

        private PreparedAttributeRebuild(WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed)
        {
            this.context = context; this.binding = binding; this.expected = expected; this.proposed = proposed;
            attributes = LegacyNativeAttributeMapProof.CaptureRaw(expected.Character.Attributes);
            paid = LegacyNativeAttributeMapProof.CaptureRaw(expected.Character.PaidNobility);
            if (!LegacyItemSaveGraph.TryCapture(expected.PlayerItems, out var captured) || captured is null)
                throw new InvalidDataException("attributeRebuildItemsInvalid");
            items = captured;
            binding.Items.RequireCurrentBeforePreparation();
            var querySource = proposed with { PlayerItems = proposed.PlayerItems with
                { EquipmentItems = LegacyEquipmentQuerySelector.Select(proposed) } };
            metadata = LegacyOwnedPhysicalMetadataWitness.CaptureAttributeQuery(binding, context.WorldData, querySource);
            var selected = LegacyEquipmentQuerySelector.Select(proposed).Where(row => row.Durability != 0).ToArray();
            speed = new(context.WorldData, selected.SingleOrDefault(row => row.Position % 50 == 4)?.ItemId ?? 0,
                selected.SingleOrDefault(row => row.Position % 50 == 5)?.ItemId ?? 0,
                selected.SingleOrDefault(row => row.Position % 50 == 6)?.ItemId ?? 0);
            PreflightAttributeQuery(context, proposed, metadata);
            spellFile = PrepareAttributeDerivedFiles(context, binding, expected, proposed, metadata);
        }

        internal static bool TryPrepare(WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed, out PreparedAttributeRebuild? plan)
        {
            plan = null;
            if (!binding.Store.IsAttributeRebuildOwnerHeld(binding, expected, context) ||
                !ReferenceEquals(context.Sessions, binding.Store) || !ReferenceEquals(context.Connection, binding.Connection) ||
                !ReferenceEquals(context.WorldData, binding.ItemCatalog) || !ReferenceEquals(proposed.NativeGameClient, binding.Client) ||
                proposed.AccountId != binding.AccountId || proposed.Character.Uid != binding.Uid ||
                !ReferenceEquals(proposed.PlayerItems, expected.PlayerItems) || proposed.AlternateEquipmentActive != expected.AlternateEquipmentActive ||
                !context.Connection.HasSendRoute || binding.Client.Status is null || binding.Player.BitVector is null ||
                !binding.Recovery.IsReadyForUsers(binding.UsersRoot)) return false;
            try
            {
                var prepared = new PreparedAttributeRebuild(context, binding, expected, proposed);
                if (!prepared.IsCurrentPlain()) return false;
                plan = prepared; return true;
            }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
                OverflowException or FormatException or IOException or UnauthorizedAccessException or NotSupportedException) { return false; }
        }

        internal bool IsCurrentPlain() => Volatile.Read(ref started) == 0 &&
            binding.Store.IsAttributeRebuildOwnerHeld(binding, expected, context) && attributes.IsCurrentPlain() &&
            paid.IsCurrentPlain() && items.IsCurrent(expected.PlayerItems) && metadata.IsCurrentPlain() &&
            speed.IsCurrentPlain(context.WorldData) && (spellFile is null || spellFile.DiskImageIsCurrent());

        // The Store binds its actual prepared final holder before committing;
        // RebuildAsync still requires the real committed ActionPublication.
        internal void BindCommitted(WorldSession final)
        {
            if (committed is not null || !IsCurrentPlain() || final.AccountId != binding.AccountId ||
                !ReferenceEquals(final.NativeGameClient, binding.Client) ||
                final.Character.Class != proposed.Character.Class || final.Character.Strength != proposed.Character.Strength ||
                final.Character.Agility != proposed.Character.Agility || final.Character.Vitality != proposed.Character.Vitality ||
                final.Character.Spirit != proposed.Character.Spirit || final.Character.AttributePoints != proposed.Character.AttributePoints ||
                !items.IsCurrent(final.PlayerItems)) throw new InvalidOperationException("attributeRebuildFinalChanged");
            committed = final;
        }

        internal async ValueTask<bool> RebuildAsync(WorldSessionStore.ActionPublication receipt, CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref started, 1, 0) != 0 || committed is null ||
                !ReferenceEquals(receipt.Published, committed) || !receipt.TryCurrent(out var current) ||
                !ReferenceEquals(current, committed) || !metadata.IsCurrentPlain() || !speed.IsCurrentPlain(context.WorldData)) return false;
            var scope = new LegacyEquipmentQueryPublicationScope(binding.Uid, current.PlayerItems)
            { OwnerNativeClientIdentity = binding.NativeIdentity, PhysicalReceiveOwner = binding, AttributeRebuild = this };
            var result = await ExecuteLegacyEquipmentQueryAsync(context with { Session = current }, current,
                new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.AttributeAllocation, scope, CallItems: false),
                cancellationToken).ConfigureAwait(false);
            return result.Admitted && result.Completed && result.Session is { } final &&
                IsQueryCurrent(context, final, final);
        }

        internal bool IsQueryCurrent(WorldPacketHandlerContext actual, WorldSession supplied, WorldSession current) =>
            Volatile.Read(ref started) == 1 && committed is not null && ReferenceEquals(supplied, current) &&
            ReferenceEquals(actual.Connection, context.Connection) && ReferenceEquals(actual.Sessions, binding.Store) &&
            ReferenceEquals(actual.FrameOperationLease, context.FrameOperationLease) &&
            ReferenceEquals(actual.TradeOperations, context.TradeOperations) &&
            actual.TradeOperations?.OwnsActiveExclusiveLease(actual.FrameOperationLease) == true &&
            binding.Store.TryGet(binding.Connection, out var held) && ReferenceEquals(held, current) &&
            ReferenceEquals(current.NativeGameClient, binding.Client) && current.AccountId == binding.AccountId &&
            current.Character.Uid == binding.Uid && current.Character.Class == proposed.Character.Class &&
            current.Character.Strength == proposed.Character.Strength && current.Character.Agility == proposed.Character.Agility &&
            current.Character.Vitality == proposed.Character.Vitality && current.Character.Spirit == proposed.Character.Spirit &&
            current.Character.AttributePoints == proposed.Character.AttributePoints && metadata.IsCurrentPlain() &&
            speed.IsCurrentPlain(context.WorldData) && binding.Store.IsAttributeRebuildQueryOwnerCurrent(binding, current, actual);

        internal static bool SameAttributeSpellRoot(string? actual, string expected) => !string.IsNullOrWhiteSpace(actual) &&
            Path.IsPathFullyQualified(actual) && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(actual)), expected,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
