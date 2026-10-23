using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeAnimaSkinLoadedAttribute = "RuntimeAnimaSkinLoaded";
    private const string LegacyAnimaSkinAttribute = "AnimaSkin";
    private const uint LegacyDragonSkinOneItemId = 4_200_018;
    private const uint LegacyDragonSkinTwoItemId = 4_200_016;
    private const uint LegacyDragonSkinThreeItemId = 4_200_017;

    [PacketHandler(WorldPacketOpcodes.LegacyAnimaSkin)]
    private static async ValueTask HandleLegacyDragonSkinAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyDragonSkinPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyDragonSkin packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyDragonSkin " +
                $"{FormatSession(context)} type={packet.Type} userId={packet.UserID} " +
                $"skinType={packet.SkinType} ok={packet.OK}.")
            .ConfigureAwait(false);

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyDragonSkin {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        switch (packet.Type)
        {
            case LegacyDragonSkinType.MyShow:
                await HandleLegacyDragonSkinMyShowAsync(context, session, cancellationToken).ConfigureAwait(false);
                break;

            case LegacyDragonSkinType.PlayerView:
                await HandleLegacyDragonSkinPlayerViewAsync(context, packet, session, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyDragonSkinType.Select:
                await HandleLegacyDragonSkinSelectAsync(context, packet, session, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyDragonSkinType.Restore:
                await HandleLegacyDragonSkinRestoreAsync(context, session, cancellationToken).ConfigureAwait(false);
                break;

            case LegacyDragonSkinType.Unlock:
                await HandleLegacyDragonSkinUnlockAsync(context, packet, session, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyDragonSkinType.Retrieve:
                await HandleLegacyDragonSkinRetrieveAsync(context, packet, session, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyDragonSkinType.Effect:
                await HandleLegacyDragonSkinEffectAsync(context, packet).ConfigureAwait(false);
                break;
        }
    }

    private static async ValueTask HandleLegacyDragonSkinPlayerViewAsync(
        WorldPacketHandlerContext context,
        LegacyDragonSkinPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var selfUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var targetUserId = packet.UserID == 0 ? selfUid : packet.UserID;
        var targetSession = targetUserId == selfUid
            ? session
            : TryGetSessionByUid(context, targetUserId, out var active)
                ? active.Session
                : null;

        if (targetSession is null)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyDragonSkin PlayerView {FormatSession(context)} userId={packet.UserID}; target session was not found.")
                .ConfigureAwait(false);
            return;
        }

        var targetState = LegacyDragonSkinState.FromSnapshot(targetSession.Character);
        await context.Connection.SendAsync(
                new LegacyDragonSkinPacket(
                    LegacyDragonSkinType.PlayerView,
                    targetUserId,
                    targetState.EquippedSkin,
                    OK: false).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyDragonSkin PlayerView " +
                $"{FormatSession(context)} userId={targetUserId} skinType={targetState.EquippedSkin}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyDragonSkinEffectAsync(
        WorldPacketHandlerContext context,
        LegacyDragonSkinPacket packet)
    {
        await context.Log.WriteLineAsync(
                "WorldServer ignored LegacyDragonSkin Effect " +
                $"{FormatSession(context)} userId={packet.UserID} skinType={packet.SkinType}; legacy source has no inbound Effect branch.")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyDragonSkinItemId(uint skinType, out uint itemId)
    {
        itemId = skinType switch
        {
            1 => LegacyDragonSkinOneItemId,
            2 => LegacyDragonSkinTwoItemId,
            3 => LegacyDragonSkinThreeItemId,
            _ => 0,
        };
        return itemId != 0;
    }

}
