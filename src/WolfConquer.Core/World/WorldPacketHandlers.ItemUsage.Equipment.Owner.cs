using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record EquipmentMoveOwner(WorldSession Expected, object Identity,
        LegacyTradeRecoveryState Recovery, LegacyNativeGameClientRuntime? Client,
        LegacyNativePlayerRuntime? Player, LegacyOrdinarySpellBinding? Binding, string UsersRoot, string ItemsRoot);

    private static bool TryCaptureEquipmentMoveOwner(WorldPacketHandlerContext context,
        out EquipmentMoveOwner owner, out string reason)
    {
        owner = null!; reason = "equipmentMoveOwnerUnavailable";
        if (context.Sessions is not { } sessions || context.Trades is not { } trades ||
            context.TradeRecovery is not { } recovery || !Monitor.IsEntered(trades.SyncRoot) ||
            !context.Connection.HasSendRoute || trades.HasConnectionEnded(context.Connection) ||
            !sessions.TryGet(context.Connection, out var current) || current is null ||
            current.AccountId is <= 0 or > uint.MaxValue || current.Character.Uid is <= 0 or > uint.MaxValue ||
            current.Character.Money < 0 || current.Character.ConquerPoints < 0 ||
            string.IsNullOrWhiteSpace(current.UsersRoot) || !Path.IsPathFullyQualified(current.UsersRoot) ||
            string.IsNullOrWhiteSpace(context.PlayerItemsRoot) || !Path.IsPathFullyQualified(context.PlayerItemsRoot) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection, current.Character.Uid, out var identity))
            return false;
        var captured = new EquipmentMoveOwner(current, identity, recovery, context.Connection.NativeGameClient,
            context.Connection.NativeGameClient?.Player, context.Connection.NativeGameClient?.OrdinarySpellBinding, current.UsersRoot, context.PlayerItemsRoot);
        if (!EquipmentMoveOwnerIsCurrent(context, captured, current) || !EquipmentMoveAdmissionIsCurrent(context, captured)) return false;
        owner = captured; reason = string.Empty; return true;
    }

    private static bool EquipmentMoveOwnerIsCurrent(WorldPacketHandlerContext context, EquipmentMoveOwner owner,
        WorldSession expected) => context.Sessions is { } sessions && context.Trades is { } trades &&
        ReferenceEquals(context.TradeRecovery, owner.Recovery) && !owner.Recovery.IsBlocked &&
        !trades.HasConnectionEnded(context.Connection) && context.PlayerItemsRoot == owner.ItemsRoot &&
        expected.UsersRoot == owner.UsersRoot && expected.AccountId == owner.Expected.AccountId &&
        ReferenceEquals(context.Connection.NativeGameClient, owner.Client) && ReferenceEquals(expected.NativeGameClient, owner.Client) &&
        ReferenceEquals(owner.Client?.OrdinarySpellBinding, owner.Binding) &&
        (owner.Client is null || owner.Player is not null && ReferenceEquals(owner.Client.Player, owner.Player) &&
            ReferenceEquals(owner.Player.Owner, owner.Client) && owner.Player.UID == owner.Expected.Character.Uid &&
            (owner.Client.OrdinarySpellBinding is not { } binding ||
                ReferenceEquals(binding.Store, sessions) && ReferenceEquals(binding.Recovery, owner.Recovery) &&
                binding.Store.IsBoundSpellOwnerCurrent(binding))) &&
        LegacyNativeClientPublication.TryCurrent(sessions, context.Connection, owner.Expected.Character.Uid,
            owner.Identity, out var current) && ReferenceEquals(current, expected);

    // Ordinary equip is not a trade. Bound/locked equipment is not rejected merely
    // because sale/drop would disallow it; active trade/escrow itself is fenced.
    private static bool EquipmentMoveAdmissionIsCurrent(WorldPacketHandlerContext context, EquipmentMoveOwner owner) =>
        context.Trades is { } trades && Monitor.IsEntered(trades.SyncRoot) &&
        owner.Recovery.IsReadyFor(owner.UsersRoot, owner.ItemsRoot) &&
        owner.Recovery.TryRequireLiveJournalClear(out _) && !trades.Contains(context.Connection) &&
        !owner.Expected.LegacyTradeActive && owner.Client?.MyTrade is null &&
        LegacyTradeEscrowClaims.TryRead(owner.Expected.Character.Attributes, out var claim, out _) && claim is null &&
        LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, owner.Expected);

    private sealed class EquipmentMoveInputs
    {
        private readonly FullAccountStringMap attributes, paid;
        private Dictionary<string, string>.Enumerator beforeAttributes, afterAttributes, beforePaid, afterPaid;
        private readonly PlayerItemSnapshot original;
        private bool armed, beforeConsumed, afterConsumed;
        internal LegacyItemSaveGraph Before { get; }
        internal WorldSession Stable { get; }

        internal EquipmentMoveInputs(WorldSession source)
        {
            original = source.PlayerItems;
            var remaining = CharacterSnapshotFileBaseline.MaximumBytes / PlayerItemSnapshotLoader.LegacyDbItemSize;
            IReadOnlyList<PlayerItemSnapshotItem>[] groups = [original.InventoryItems, original.RelicFuseItems,
                original.MythsoulBagItems, original.EquipmentItems, original.RuneItems, original.WardrobeItems, original.WarehouseItems];
            foreach (var group in groups)
            {
                var rows = FullAccountKnownRows(group, remaining); remaining -= rows.Length;
                foreach (var row in rows)
                {
                    if (row is null) throw InvalidEquipmentMove();
                    var children = FullAccountKnownRows(row.DepositedItems, remaining); remaining -= children.Length;
                    foreach (var child in children)
                        if (child is null || FullAccountKnownRows(child.DepositedItems, 0).Length != 0) throw InvalidEquipmentMove();
                }
            }
            if (!LegacyItemSaveGraph.TryCapture(original, out var before)) throw InvalidEquipmentMove();
            Before = before;
            attributes = new(source.Character.Attributes); paid = new(source.Character.PaidNobility);
            Stable = before.WithOwnedItemsForPersistence(source with
            { Character = source.Character with { Attributes = attributes.Copy, PaidNobility = paid.Copy } });
        }

        internal void Arm()
        {
            if (armed) throw InvalidEquipmentMove();
            beforeAttributes = attributes.Open(); afterAttributes = attributes.Open();
            beforePaid = paid.Open(); afterPaid = paid.Open(); armed = true;
        }
        internal bool ConsumeBefore() => armed && !beforeConsumed && Consume(ref beforeConsumed, beforeAttributes, beforePaid);
        internal bool ConsumeAfter() => armed && !afterConsumed && Consume(ref afterConsumed, afterAttributes, afterPaid);
        private bool Consume(ref bool consumed, Dictionary<string, string>.Enumerator first,
            Dictionary<string, string>.Enumerator second)
        {
            consumed = true;
            try { return attributes.Matches(first) && paid.Matches(second) && Before.IsCurrent(original); }
            catch (InvalidOperationException) { return false; } // Actual raw Dictionary enumerator invalidation is a failed proof.
        }
    }

    private static InvalidOperationException InvalidEquipmentMove() => new("equipmentMoveRequiresCompleteCurrentState");
}
