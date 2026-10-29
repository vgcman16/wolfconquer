using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record ManagedManaConsumableOwner(WorldSession Expected, object Identity,
        LegacyTradeRecoveryState Recovery, LegacyNativeGameClientRuntime Client,
        LegacyOrdinarySpellBinding Binding, string Users, string Items,
        LegacySecondaryPasswordState Pin, bool PinChecked, LegacyTradeParty? Trade, uint ItemUid);

    private static bool TryCaptureManagedManaConsumableOwner(WorldPacketHandlerContext context, WorldSession supplied,
        uint uid, out ManagedManaConsumableOwner owner)
    {
        owner = null!;
        if (context.Sessions is not { } sessions || context.Trades is not { } trades ||
            !Monitor.IsEntered(trades.SyncRoot) || context.TradeOperations is null ||
            context.TradeRecovery is not { } recovery || !context.Connection.HasSendRoute ||
            !sessions.TryGet(context.Connection, out var current) || current is null || !ReferenceEquals(current, supplied) ||
            current.AccountId is <= 0 or > uint.MaxValue || current.Character.Uid is <= 0 or > uint.MaxValue ||
            string.IsNullOrWhiteSpace(current.UsersRoot) || !Path.IsPathFullyQualified(current.UsersRoot) ||
            string.IsNullOrWhiteSpace(context.PlayerItemsRoot) || !Path.IsPathFullyQualified(context.PlayerItemsRoot) ||
            context.Connection.NativeGameClient is not { OrdinarySpellBinding: { } binding } client ||
            current.UsersRoot != binding.SessionUsersRoot ||
            !LegacySecondaryPasswordRuntime.TryGet(context.Connection, current, out var pin, out var pinChecked) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection, current.Character.Uid, out var identity)) return false;
        trades.TryGet(context.Connection, out var trade);
        var captured = new ManagedManaConsumableOwner(current, identity, recovery, client, binding,
            current.UsersRoot, context.PlayerItemsRoot, pin, pinChecked, trade, uid);
        if (!ManagedManaConsumableOwnerIsCurrent(context, captured, current)) return false;
        owner = captured; return true;
    }

    // Mana use never inherited equipment's verified-PIN or blanket trade gate.
    // The PIN issuer must still be real/current; its checked flag is preserved.
    private static bool ManagedManaConsumableOwnerIsCurrent(WorldPacketHandlerContext context,
        ManagedManaConsumableOwner owner, WorldSession expected)
    {
        if (context.Sessions is not { } sessions || context.Trades is not { } trades ||
            !Monitor.IsEntered(trades.SyncRoot) || !ReferenceEquals(context.TradeRecovery, owner.Recovery) ||
            trades.HasConnectionEnded(context.Connection) || context.PlayerItemsRoot != owner.Items ||
            expected.UsersRoot != owner.Users || expected.AccountId != owner.Expected.AccountId ||
            !owner.Recovery.IsReadyFor(owner.Users, owner.Items) || !owner.Recovery.TryRequireLiveJournalClear(out _) ||
            !ReferenceEquals(context.Connection.NativeGameClient, owner.Client) || !ReferenceEquals(expected.NativeGameClient, owner.Client) ||
            !ReferenceEquals(expected.NativePlayer, owner.Binding.Player) ||
            !ReferenceEquals(owner.Client.OrdinarySpellBinding, owner.Binding) ||
            !ReferenceEquals(owner.Binding.Store, sessions) || !ReferenceEquals(owner.Binding.Recovery, owner.Recovery) ||
            owner.Binding.AccountId != expected.AccountId || owner.Binding.Uid != expected.Character.Uid ||
            !owner.Binding.PhysicalOwnerIsCurrentPlain() || !sessions.IsBoundSpellOwnerCurrent(owner.Binding) ||
            !LegacySecondaryPasswordRuntime.TryGet(context.Connection, expected, out var pin, out var pinChecked) ||
            pin != owner.Pin || pinChecked != owner.PinChecked ||
            !LegacyTradeEscrowClaims.TryRead(expected.Character.Attributes, out var claim, out _)) return false;
        trades.TryGet(context.Connection, out var trade);
        if (!ReferenceEquals(trade, owner.Trade) || trade?.Items.ContainsKey(owner.ItemUid) == true ||
            ((claim is not null || expected.LegacyTradeActive) && trade is null) || owner.Client.MyTrade is not null) return false;
        return LegacyNativeClientPublication.TryCurrent(sessions, context.Connection, expected.Character.Uid,
            owner.Identity, out var current) && ReferenceEquals(current, expected);
    }
}
