using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask PublishAccountPromotionAsync(WorldPacketHandlerContext context, AccountPromotionPlan plan)
    {
        try
        {
            // The complete three-field image and Store are committed. This source
            // branch emits no finalized success dialog or other purchase frame.
            // Diagnostic failure cannot refund, retry or report a rejection.
            await context.Log.WriteLineAsync("WorldServer persisted LegacyNpc AccountManager full promotion " +
                $"{FormatSession(context with { Session = plan.Updated })} source=NpcHandler.AccountManager " +
                $"path={Path.GetFileName(plan.File.Path)} keys=DonatePoints,Class,ChargePromotion.").ConfigureAwait(false);
            await context.Log.WriteLineAsync("WorldServer applied LegacyNpc AccountManager full promotion " +
                $"{FormatSession(context with { Session = plan.Updated })} source=NpcHandler.AccountManager " +
                $"option={plan.Option} cost={LegacyAccountManagerFullPromotionCost} class={plan.Owner.Values.Class} " +
                $"promotedClass={plan.After.Class} donatePoints={plan.After.Donation} " +
                $"chargePromotion={plan.After.ChargePromotion}.").ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Do not recursively log or translate a committed action to rejection.
        }
    }

    private static async ValueTask PublishAccountPromotionInsufficientAsync(WorldPacketHandlerContext context,
        AccountPromotionOwner owner, CancellationToken cancellationToken)
    {
        byte[][] frames =
        [
            LegacyNpcReplyPacket.ForDialog(LegacyAccountManagerInsufficientDonatePointsText + " You need " +
                LegacyAccountManagerFullPromotionCost.ToString(CultureInfo.InvariantCulture) + " donation points.").ToFrameBytes(),
            LegacyNpcReplyPacket.ForOption("I~see.", LegacyNpcClosedOption).ToFrameBytes(),
            LegacyNpcReplyPacket.ForAvatar(LegacyAccountManagerShopAvatarId).ToFrameBytes(),
            LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
        ];
        foreach (var frame in frames)
        {
            if (!IsCurrentAccountPromotionOwner(context, owner, owner.Expected, owner.Fields)) return;
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }
}
