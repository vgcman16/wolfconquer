namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record ItemRepairOwner(
        WorldSession Expected,
        object Identity,
        LegacyTradeRecoveryState Recovery,
        WorldServerFrameHandler Frame,
        LegacyNativeGameClientRuntime? Client,
        LegacyOrdinarySpellBinding? Binding,
        string UsersRoot,
        string ItemsRoot,
        LegacySecondaryPasswordState Pin,
        bool? PinChecked);

    private static bool TryCaptureItemRepairOwner(
        WorldPacketHandlerContext context,
        WorldSessionStore.ItemRepairPreparationLease preparation,
        out ItemRepairOwner owner)
    {
        owner = null!;
        preparation.RequireCurrent();
        var current = preparation.Expected;
        if (context.Trades is not { } trades || !Monitor.IsEntered(trades.SyncRoot) ||
            !Monitor.IsEntered(LegacySecondaryPasswordRuntime.SyncRoot) ||
            context.TradeRecovery is not { } recovery || !context.Connection.HasSendRoute ||
            current.AccountId is <= 0 or > uint.MaxValue || current.Character.Uid is <= 0 or > uint.MaxValue ||
            current.Character.Money < 0 ||
            !TryReadItemRepairPin(context, current, preparation.Binding, out var pin, out var pinChecked))
            return false;
        var captured = new ItemRepairOwner(current, preparation.Identity, recovery, preparation.Frame,
            context.Connection.NativeGameClient, preparation.Binding, preparation.UsersRoot,
            preparation.ItemsRoot, pin, pinChecked);
        if (!ItemRepairOwnerIsCurrent(context, captured, current))
            return false;
        preparation.RequireCurrent();
        owner = captured;
        return true;
    }

    private static bool TryReadItemRepairPin(
        WorldPacketHandlerContext context,
        WorldSession expected,
        LegacyOrdinarySpellBinding? binding,
        out LegacySecondaryPasswordState pin,
        out bool? pinChecked)
    {
        pin = null!;
        pinChecked = null;
        if (binding is not null)
        {
            if (!LegacySecondaryPasswordRuntime.TryGet(context.Connection, expected, out pin, out var actualChecked))
                return false;
            pinChecked = actualChecked;
            return LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, expected);
        }
        // The genuine managed Frame path has no native PIN issuer. Its existing
        // admission policy permits only an actual parsed zero-password state.
        return context.Connection.NativeGameClient is null && expected.NativeGameClient is null &&
            LegacySecondaryPasswordState.TryRead(expected.Character.Attributes, out pin, out _) &&
            LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, expected);
    }

    private static bool ItemRepairOwnerIsCurrent(
        WorldPacketHandlerContext context, ItemRepairOwner owner, WorldSession expected)
    {
        if (context.Sessions is not { } sessions || context.Trades is not { } trades ||
            !Monitor.IsEntered(trades.SyncRoot) || !Monitor.IsEntered(LegacySecondaryPasswordRuntime.SyncRoot) ||
            !ReferenceEquals(context.TradeRecovery, owner.Recovery) ||
            !context.Connection.HasSendRoute || trades.HasConnectionEnded(context.Connection) ||
            context.PlayerItemsRoot != owner.ItemsRoot || expected.UsersRoot != owner.Expected.UsersRoot ||
            expected.AccountId != owner.Expected.AccountId || expected.Character.Uid != owner.Expected.Character.Uid ||
            !ReferenceEquals(context.Connection.NativeGameClient, owner.Client) ||
            !ReferenceEquals(expected.NativeGameClient, owner.Client) ||
            !ReferenceEquals(owner.Client?.OrdinarySpellBinding, owner.Binding) ||
            !owner.Recovery.IsReadyFor(owner.UsersRoot, owner.ItemsRoot) ||
            !owner.Recovery.TryRequireLiveJournalClear(out _) || trades.Contains(context.Connection) ||
            expected.LegacyTradeActive || owner.Client?.MyTrade is not null ||
            !LegacyTradeEscrowClaims.TryRead(expected.Character.Attributes, out var claim, out _) || claim is not null ||
            !TryReadItemRepairPin(context, expected, owner.Binding, out var pin, out var pinChecked) ||
            pin != owner.Pin || pinChecked != owner.PinChecked)
            return false;
        if (owner.Binding is { } binding &&
            (!ReferenceEquals(binding.Store, sessions) || !ReferenceEquals(binding.Recovery, owner.Recovery) ||
             !ReferenceEquals(binding.Client, owner.Client) || !ReferenceEquals(expected.NativePlayer, binding.Player) ||
             !binding.PhysicalOwnerIsCurrentPlain() || !sessions.IsBoundSpellOwnerCurrent(binding)))
            return false;
        return LegacyNativeClientPublication.TryCurrent(sessions, context.Connection, expected.Character.Uid,
            owner.Identity, out var current) && ReferenceEquals(current, expected);
    }
}
