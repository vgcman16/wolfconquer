namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool MailboxClaimDeliveryManagedCurrent(LegacyMailboxClaimPlan plan,
        out LegacySecondaryPasswordState? pin)
    {
        pin = null;
        // Finish every callback-capable managed traversal BEFORE current Store,
        // physical Player/PIN and callback-free native publication fences.
        if (plan.PackagePublication is { } publication &&
            (!publication.CatalogsCurrent() || !MailboxPackageGearMatches(plan.Updated.CombatGear, publication.Prepared.GearAfter)))
            return false;
        return plan.ItemsAfter.IsCurrent(plan.PreparedItems) &&
            plan.Updated.Character.Attributes.SequenceEqual(plan.UpdatedFields) &&
            LegacySecondaryPasswordState.TryRead(plan.Updated.Character.Attributes, out pin, out _);
    }

    private static bool MailboxClaimDeliveryOwnerFinalCurrent(LegacyMailboxClaimPlan plan, LegacySecondaryPasswordState pin)
    {
        var owner = plan.Baseline.Owner;
        var context = owner.Context;
        if (context.Trades is not { } trades || trades.HasConnectionEnded(context.Connection) ||
            !LegacyNativeClientPublication.TryCurrent(context.Sessions!, context.Connection, owner.Expected.Character.Uid,
            owner.Identity, out var current) || !ReferenceEquals(current, plan.Updated) ||
            !ReferenceEquals(context.Connection.NativeGameClient, owner.Client) ||
            !ReferenceEquals(plan.Updated.NativeGameClient, owner.Client)) return false;
        if (owner.Client is null) return pin.Password == 0;
        var player = owner.Player;
        return player is not null && ReferenceEquals(owner.Client.Player, player) && ReferenceEquals(player.Owner, owner.Client) &&
            player.UID == plan.Updated.Character.Uid && player.IsCheckedPass && player.SecurityPassword == pin.Password &&
            player.OnReset == pin.OnReset && player.ResetSecurityPassowrd == pin.ResetAt;
    }
}
