using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask PublishAccountVipAsync(WorldPacketHandlerContext context, AccountVipPlan plan,
        CancellationToken cancellationToken)
    {
        try
        {
            // The complete debit+VIP image and Store session are committed.
            // Logging/transport retirement cannot refund or make this replayable.
            await context.Log.WriteLineAsync("WorldServer applied LegacyNpc AccountManager VIP purchase " +
                $"{FormatSession(context with { Session = plan.Updated })} source=NpcHandler.AccountManager " +
                $"option={plan.Option} cost={plan.Cost} vipLevel={plan.Level}.").ConfigureAwait(false);
            foreach (var frame in plan.Frames)
            {
                if (!IsCurrentAccountVipOwner(context, plan.Owner, plan.Updated, plan.Fields)) return;
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // No recursive logger call and no false uncommitted rejection.
        }
    }

    private static async ValueTask PublishAccountVipInsufficientAsync(WorldPacketHandlerContext context,
        AccountVipOwner owner, ulong cost, CancellationToken cancellationToken)
    {
        byte[][] frames =
        [
            LegacyNpcReplyPacket.ForDialog(LegacyAccountManagerInsufficientDonatePointsText + " You need " +
                cost.ToString(CultureInfo.InvariantCulture) + " donation points.").ToFrameBytes(),
            LegacyNpcReplyPacket.ForOption("I~see.", LegacyNpcClosedOption).ToFrameBytes(),
            LegacyNpcReplyPacket.ForAvatar(LegacyAccountManagerShopAvatarId).ToFrameBytes(),
            LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
        ];
        foreach (var frame in frames)
        {
            if (!IsCurrentAccountVipOwner(context, owner, owner.Expected, owner.Fields)) return;
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }
}
