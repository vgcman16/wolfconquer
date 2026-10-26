using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask PublishAccountCpAsync(WorldPacketHandlerContext context, AccountCpPlan plan,
        CancellationToken cancellationToken)
    {
        try
        {
            // The whole Character image and actual Store are committed. Neither
            // notification failure nor retirement may refund or replay it.
            var keys = plan.BankTransfer == 0 ? "DonatePoints,ConquerPoints" : "DonatePoints,ConquerPoints,BankCps";
            await context.Log.WriteLineAsync("WorldServer persisted LegacyNpc AccountManager conquer point purchase " +
                $"{FormatSession(context with { Session = plan.Updated })} source=NpcHandler.AccountManager " +
                $"path={Path.GetFileName(plan.File.Path)} keys={keys}.").ConfigureAwait(false);
            await context.Log.WriteLineAsync("WorldServer applied LegacyNpc AccountManager conquer point purchase " +
                $"{FormatSession(context with { Session = plan.Updated })} source=NpcHandler.AccountManager " +
                $"option={plan.Option} cost={plan.Cost} grant={plan.Grant} donatePoints={plan.After.Donation} " +
                $"conquerPoints={plan.After.ConquerPoints} bankCps={plan.After.BankConquerPoints} " +
                $"bankTransfer={plan.BankTransfer} walletUpdate=LegacyMsgUpdate.ConquerPoints.").ConfigureAwait(false);
            foreach (var frame in plan.Frames)
            {
                if (!IsCurrentAccountCpOwner(context, plan.Owner, plan.Updated, plan.Fields)) return;
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // No recursive logger and no false uncommitted rejection/refund.
        }
    }

    private static async ValueTask PublishAccountCpInsufficientAsync(WorldPacketHandlerContext context,
        AccountCpOwner owner, CancellationToken cancellationToken)
    {
        byte[][] frames =
        [
            LegacyNpcReplyPacket.ForDialog(LegacyAccountManagerInsufficientDonatePointsText + " You need " +
                LegacyAccountManagerOneBillionConquerPointsCost.ToString(CultureInfo.InvariantCulture) + " donation points.").ToFrameBytes(),
            LegacyNpcReplyPacket.ForOption("I~see.", LegacyNpcClosedOption).ToFrameBytes(),
            LegacyNpcReplyPacket.ForAvatar(LegacyAccountManagerShopAvatarId).ToFrameBytes(),
            LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
        ];
        foreach (var frame in frames)
        {
            if (!IsCurrentAccountCpOwner(context, owner, owner.Expected, owner.Fields)) return;
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }
}
