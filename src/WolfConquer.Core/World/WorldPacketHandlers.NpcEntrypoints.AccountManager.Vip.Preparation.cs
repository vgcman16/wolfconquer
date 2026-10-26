using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record AccountVipPlan(AccountVipOwner Owner, WorldSession Updated,
        KeyValuePair<string, string>[] Fields, LegacyAccountManagerVipPersistence.Prepared File,
        byte[][] Frames, byte Option, ulong Cost, ulong Level);

    private static bool TryPrepareAccountVip(WorldPacketHandlerContext context, AccountVipOwner owner,
        byte option, ulong level, ulong cost, out AccountVipPlan plan, out bool insufficient, out string reason)
    {
        plan = null!; insufficient = false; reason = "accountManagerVipOfferInvalid";
        if (!((option == 35 && level == LegacyAccountManagerVip6Level && cost == LegacyAccountManagerVip6Cost) ||
              (option == 197 && level == LegacyAccountManagerVip7Level && cost == LegacyAccountManagerVip7Cost)) || cost == 0 || level > byte.MaxValue) return false;
        if (!LegacyAccountManagerVipPersistence.TryCapture(owner.Expected, owner.Values, out var baseline, out var path, out reason)) return false;
        if (owner.Values.Donation < cost)
        {
            insufficient = true;
            reason = "accountManagerVipInsufficientPoints";
            return false;
        }
        // Source DateTime.Now is Local. Preserve its binary kind while letting
        // the real clock provider supply the local wall-clock value.
        var now = DateTime.SpecifyKind((context.TimeProvider ?? TimeProvider.System).GetLocalNow().DateTime, DateTimeKind.Local);
        var start = owner.Values.Expiry is { } binary ? DateTime.FromBinary(binary) : now;
        var expiry = (start > now ? start : now).AddDays(LegacyAccountManagerVipDays).ToBinary();
        var afterValues = new LegacyAccountManagerVipPersistence.Values(owner.Values.Donation - cost, (byte)level, expiry);
        var attributes = new Dictionary<string, string>(owner.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = afterValues.Donation.ToString(CultureInfo.InvariantCulture),
            ["VipLevel"] = level.ToString(CultureInfo.InvariantCulture),
            ["VipTime"] = expiry.ToString(CultureInfo.InvariantCulture),
        };
        var updated = owner.Expected with { Character = owner.Expected.Character with { Attributes = attributes } };
        byte[][] frames =
        [
            LegacyMsgUpdatePacket.CreateLegacyVipLevelStartupUpdate(updated.Character).ToFrameBytes(),
            LegacyVipStatusPacket.FromSnapshot(updated.Character).ToFrameBytes(),
            LegacyNpcReplyPacket.ForMessageBox($"You have received VIP{level.ToString(CultureInfo.InvariantCulture)} (30 days).").ToFrameBytes(),
        ];
        if (!LegacyAccountManagerVipPersistence.TryProject(baseline, path, afterValues, out var file, out reason)) return false;
        if (!IsCurrentAccountVipOwner(context, owner, owner.Expected, owner.Fields))
        { reason = "accountManagerVipOwnerChanged"; return false; }
        plan = new(owner, updated, attributes.ToArray(), file, frames, option, cost, level);
        reason = string.Empty;
        return true;
    }
}
