using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask PublishAccountChiAsync(WorldPacketHandlerContext context, AccountChiPlan plan)
    {
        try
        {
            // File+Store already commit the complete debit and persistent flag.
            // The valid branch has no purchase-success packet. A failed log
            // cannot refund, pay again or become an uncommitted rejection.
            await context.Log.WriteLineAsync("WorldServer persisted LegacyNpc AccountManager attribute flag purchase " +
                $"{FormatSession(context with { Session = plan.Updated })} source=NpcHandler.AccountManager " +
                $"path={Path.GetFileName(plan.File.Path)} keys=DonatePoints,ChargeChi.").ConfigureAwait(false);
            await context.Log.WriteLineAsync("WorldServer applied LegacyNpc AccountManager attribute flag purchase " +
                $"{FormatSession(context with { Session = plan.Updated })} source=NpcHandler.AccountManager " +
                $"option={plan.Option} attribute=ChargeChi cost={plan.Cost} donatePoints={plan.After.Donation}.")
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // No recursive logger and no false rejection after commitment.
        }
    }

    private static async ValueTask PublishAccountChiInsufficientAsync(WorldPacketHandlerContext context,
        AccountChiOwner owner, CancellationToken cancellationToken)
    {
        byte[][] frames =
        [
            LegacyNpcReplyPacket.ForDialog(LegacyAccountManagerInsufficientDonatePointsText + " You need " +
                LegacyAccountManagerChargeChiCost.ToString(CultureInfo.InvariantCulture) + " donation points.").ToFrameBytes(),
            LegacyNpcReplyPacket.ForOption("I~see.", LegacyNpcClosedOption).ToFrameBytes(),
            LegacyNpcReplyPacket.ForAvatar(LegacyAccountManagerShopAvatarId).ToFrameBytes(),
            LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
        ];
        foreach (var frame in frames)
        {
            if (!IsCurrentAccountChiOwner(context, owner, owner.Expected, owner.Fields)) return;
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }
}
