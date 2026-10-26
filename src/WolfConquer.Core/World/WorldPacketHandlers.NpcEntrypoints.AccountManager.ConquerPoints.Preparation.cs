using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record AccountCpPlan(AccountCpOwner Owner, WorldSession Updated,
        KeyValuePair<string, string>[] Fields, LegacyAccountManagerConquerPointsPersistence.Prepared File,
        byte Option, ulong Cost, long Grant, uint BankTransfer,
        LegacyAccountManagerConquerPointsPersistence.Values After, byte[][] Frames);

    private static bool TryPrepareAccountCp(WorldPacketHandlerContext context, AccountCpOwner owner,
        byte option, ulong cost, long grant, out AccountCpPlan? plan, out bool insufficient, out string reason)
    {
        plan = null; insufficient = false; reason = "accountManagerConquerPointsOfferInvalid";
        if (option != 3 || cost != LegacyAccountManagerOneBillionConquerPointsCost ||
            grant != LegacyAccountManagerOneBillionConquerPointsGrant) return false;
        if (!LegacyAccountManagerConquerPointsPersistence.TryCapture(owner.Expected, owner.Values,
                out var baseline, out var path, out reason) || baseline is null) return false;
        if (owner.Values.Donation < cost)
        { insufficient = true; reason = "accountManagerConquerPointsInsufficientPoints"; return false; }

        // AccountManager invokes the native uint CPS setter. Its >2B excess
        // belongs to saved BankCps, not a discarded or invented wallet. Reject
        // source uint-addition wrap and the managed bank's Int64 overflow first.
        const uint walletCap = 2_000_000_000;
        var total = (ulong)owner.Values.ConquerPoints + (ulong)grant;
        if (total > uint.MaxValue)
        { reason = "accountManagerConquerPointsWalletOverflow"; return false; }
        var transfer = total > walletCap ? checked((uint)(total - walletCap)) : 0u;
        var wallet = checked((uint)(total - transfer));
        var bank = checked(owner.Values.BankConquerPoints + transfer);
        var after = new LegacyAccountManagerConquerPointsPersistence.Values(owner.Values.Donation - cost, wallet, bank);
        var attributes = new Dictionary<string, string>(owner.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = after.Donation.ToString(CultureInfo.InvariantCulture),
            ["ConquerPoints"] = after.ConquerPoints.ToString(CultureInfo.InvariantCulture),
        };
        if (transfer != 0) attributes["BankCps"] = bank.ToString(CultureInfo.InvariantCulture);
        var updated = owner.Expected with
        {
            Character = owner.Expected.Character with
            { ConquerPoints = wallet, BankConquerPoints = bank, Attributes = attributes },
        };
        var frames = new List<byte[]>();
        if (transfer != 0)
            frames.Add(LegacyNpcReplyPacket.ForMessageBox(transfer.ToString(CultureInfo.InvariantCulture) +
                " Your ConquerPoints[CPS] has Transfare To Your Bank Thanks.").ToFrameBytes());
        frames.Add(new LegacyMsgUpdatePacket(checked((uint)owner.Expected.Character.Uid),
            [new(LegacyMsgUpdateDataType.ConquerPoints, wallet)]).ToFrameBytes());
        if (!LegacyAccountManagerConquerPointsPersistence.TryProject(baseline, path, after, transfer != 0,
                out var file, out reason) || file is null) return false;
        if (!IsCurrentAccountCpOwner(context, owner, owner.Expected, owner.Fields))
        { reason = "accountManagerConquerPointsOwnerChanged"; return false; }
        plan = new(owner, updated, attributes.ToArray(), file, option, cost, grant, transfer, after, frames.ToArray());
        reason = string.Empty;
        return true;
    }
}
