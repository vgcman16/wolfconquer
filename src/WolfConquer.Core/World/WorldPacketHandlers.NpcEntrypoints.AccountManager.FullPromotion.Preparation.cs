using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record AccountPromotionPlan(AccountPromotionOwner Owner, WorldSession Updated,
        LegacyAccountManagerFullPromotionPersistence.Prepared File, byte Option,
        LegacyAccountManagerFullPromotionPersistence.Values After);

    private static bool TryPrepareAccountPromotion(WorldPacketHandlerContext context, AccountPromotionOwner owner,
        byte option, out AccountPromotionPlan? plan, out bool insufficient, out string reason)
    {
        plan = null; insufficient = false; reason = "accountManagerFullPromotionOfferInvalid";
        if (option != 36) return false;
        var promoted = ResolveLegacyAccountManagerFullPromotionClass(owner.Values.Class);
        // Unsupported classes cannot buy a no-op. Preserve all eleven valid
        // source ranges, including their already-promoted terminal classes.
        if (promoted == owner.Values.Class && promoted is not
            (1_051 or 2_051 or 4_051 or 5_051 or 6_051 or 7_051 or 8_051 or 9_051 or 13_051 or 14_051 or 16_051))
        { reason = "accountManagerFullPromotionClassUnsupported"; return false; }
        if (!LegacyAccountManagerFullPromotionPersistence.TryCapture(owner.Expected, owner.Values,
                out var baseline, out var path, out reason) || baseline is null) return false;
        if (owner.Values.Donation < LegacyAccountManagerFullPromotionCost)
        { insufficient = true; reason = "accountManagerFullPromotionInsufficientPoints"; return false; }
        // Native Player/loader/save represent ChargePromotion as a byte. Never
        // debit a purchase whose durable/native counter would wrap or truncate.
        if (owner.Values.ChargePromotion == byte.MaxValue)
        { reason = "accountManagerFullPromotionCounterExhausted"; return false; }
        var after = new LegacyAccountManagerFullPromotionPersistence.Values(
            owner.Values.Donation - LegacyAccountManagerFullPromotionCost, promoted, (byte)(owner.Values.ChargePromotion + 1));
        var attributes = new Dictionary<string, string>(owner.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = after.Donation.ToString(CultureInfo.InvariantCulture),
            ["Class"] = after.Class.ToString(CultureInfo.InvariantCulture),
            ["ChargePromotion"] = after.ChargePromotion.ToString(CultureInfo.InvariantCulture),
        };
        var updated = owner.Expected with { Character = owner.Expected.Character with { Class = promoted, Attributes = attributes } };
        if (!LegacyAccountManagerFullPromotionPersistence.TryProject(baseline, path, after, out var file, out reason) || file is null) return false;
        if (!IsCurrentAccountPromotionOwner(context, owner, owner.Expected, owner.Fields))
        { reason = "accountManagerFullPromotionOwnerChanged"; return false; }
        plan = new(owner, updated, file, option, after);
        reason = string.Empty;
        return true;
    }
}
