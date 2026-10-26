using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record AccountChiPlan(AccountChiOwner Owner, WorldSession Updated,
        LegacyAccountManagerChargeChiPersistence.Prepared File, byte Option, ulong Cost,
        LegacyAccountManagerChargeChiPersistence.Values After);

    private static bool TryPrepareAccountChi(WorldPacketHandlerContext context, AccountChiOwner owner,
        byte option, ulong cost, string attributeName, out AccountChiPlan? plan, out bool insufficient, out string reason)
    {
        plan = null; insufficient = false; reason = "accountManagerChargeChiOfferInvalid";
        if (option != 2 || cost != LegacyAccountManagerChargeChiCost ||
            !string.Equals(attributeName, "ChargeChi", StringComparison.Ordinal)) return false;
        if (!LegacyAccountManagerChargeChiPersistence.TryCapture(owner.Expected, owner.Values,
                out var baseline, out var path, out reason) || baseline is null) return false;
        // MsgChiInfo Study and the managed consumer keep reading flag==1;
        // neither consumes it. A repeat debit cannot grant any further effect.
        if (owner.Values.Flag == 1)
        { reason = "accountManagerChargeChiAlreadyGranted"; return false; }
        if (owner.Values.Donation < cost)
        { insufficient = true; reason = "accountManagerChargeChiInsufficientPoints"; return false; }
        var after = new LegacyAccountManagerChargeChiPersistence.Values(owner.Values.Donation - cost, 1);
        var attributes = new Dictionary<string, string>(owner.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = after.Donation.ToString(CultureInfo.InvariantCulture),
            ["ChargeChi"] = "1",
        };
        var updated = owner.Expected with { Character = owner.Expected.Character with { Attributes = attributes } };
        if (!LegacyAccountManagerChargeChiPersistence.TryProject(baseline, path, after,
                out var file, out reason) || file is null) return false;
        if (!IsCurrentAccountChiOwner(context, owner, owner.Expected, owner.Fields))
        { reason = "accountManagerChargeChiOwnerChanged"; return false; }
        plan = new(owner, updated, file, option, cost, after);
        reason = string.Empty;
        return true;
    }
}
