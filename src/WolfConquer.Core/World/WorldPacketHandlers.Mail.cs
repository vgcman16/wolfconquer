using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacyMailList)]
    private static async ValueTask HandleLegacyMailListAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyMailList " +
                    $"{FormatSession(context)} {FormatFrameDiagnostic(frame)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Mailboxes is not { } mailboxes)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyMailList missing LegacyMailboxStore; " +
                    "mailbox store is not configured for the current world handler.")
                .ConfigureAwait(false);
            return;
        }

        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var now = DateTime.Now;
        var mails = mailboxes.List(uid)
            .OrderBy(static mail => mail.Id)
            .Select(mail => LegacyMailListPacket.FromMailboxEntry(mail, now))
            .ToArray();

        var responseCount = 0;
        foreach (var page in mails.Chunk(10))
        {
            if (page.Length == 0)
            {
                continue;
            }

            var response = new LegacyMailListPacket(
                LegacyMailListType.FullPage,
                Count: 1,
                Mails: page);
            await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            responseCount++;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyMailList " +
                $"{FormatSession(context)} mailCount={mails.Length} responseFrames={responseCount}.")
            .ConfigureAwait(false);
    }

    [PacketHandler(WorldPacketOpcodes.LegacyMailboxMode)]
    private static async ValueTask HandleLegacyMailboxModeAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyMailboxModePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyMailboxMode packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type is LegacyMailboxModeType.ClaimItem or LegacyMailboxModeType.ClaimMoney or LegacyMailboxModeType.ClaimAll)
        {
            await HandleLegacyMailboxClaimsAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyMailboxMode " +
                    $"{FormatSession(context)} type={packet.Type} id={packet.Id}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Mailboxes is not { } mailboxes)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyMailboxMode missing LegacyMailboxStore; " +
                    "mailbox store is not configured for the current world handler.")
                .ConfigureAwait(false);
            return;
        }

        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        switch (packet.Type)
        {
            case LegacyMailboxModeType.Content:
                {
                    if (!mailboxes.TryGet(uid, packet.Id, out var entry))
                    {
                        await context.Log.WriteLineAsync(
                                "WorldServer handled LegacyMailboxMode " +
                                $"{FormatSession(context)} type={packet.Type} id={packet.Id} found=False.")
                            .ConfigureAwait(false);
                        return;
                    }

                    await context.Connection.SendAsync(
                            new LegacyMailContentPacket(entry.Id, entry.Content).ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer handled LegacyMailboxMode " +
                            $"{FormatSession(context)} type={packet.Type} id={packet.Id} found=True response=content ack=True.")
                        .ConfigureAwait(false);
                    return;
                }

            case LegacyMailboxModeType.Delete:
                {
                    if (!mailboxes.TryRemoveIfEmpty(uid, packet.Id, out var entry))
                    {
                        await context.Log.WriteLineAsync(
                                "WorldServer handled LegacyMailboxMode " +
                                $"{FormatSession(context)} type={packet.Type} id={packet.Id} removed=False.")
                            .ConfigureAwait(false);
                        return;
                    }

                    mailboxes.Save(uid);
                    await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer handled LegacyMailboxMode " +
                            $"{FormatSession(context)} type={packet.Type} id={entry.Id} removed=True ack=True.")
                        .ConfigureAwait(false);
                    return;
                }

            case LegacyMailboxModeType.DeleteClaim:
                {
                    var removedCount = 0;
                    foreach (var rawMailId in packet.Items)
                    {
                        if (rawMailId > uint.MaxValue ||
                            !mailboxes.TryRemoveIfEmpty(uid, (uint)rawMailId, out _))
                        {
                            continue;
                        }

                        removedCount++;
                        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    }

                    if (removedCount > 0)
                    {
                        mailboxes.Save(uid);
                    }

                    await context.Log.WriteLineAsync(
                            "WorldServer handled LegacyMailboxMode " +
                            $"{FormatSession(context)} type={packet.Type} id={packet.Id} requested={packet.Items.Count} " +
                            $"removed={removedCount} ackFrames={removedCount}.")
                        .ConfigureAwait(false);
                    return;
                }

            case LegacyMailboxModeType.ClaimRecordType:
                {
                    if (!mailboxes.TryGet(uid, packet.Id, out var entry))
                    {
                        await context.Log.WriteLineAsync(
                                "WorldServer handled LegacyMailboxMode " +
                                $"{FormatSession(context)} type={packet.Type} id={packet.Id} found=False.")
                            .ConfigureAwait(false);
                        return;
                    }

                    if (entry.Flag == 0)
                    {
                        await context.Log.WriteLineAsync(
                                "WorldServer handled LegacyMailboxMode " +
                                $"{FormatSession(context)} type={packet.Type} id={packet.Id} flag=0 ack=False.")
                            .ConfigureAwait(false);
                        return;
                    }

                    if (!LegacyBossHarmRankPackageClaimPlan.TryCreate(
                        entry.Flag,
                        session.Character.Class,
                        LegacyInventorySlots - session.PlayerItems.InventoryItems.Count,
                        out var claimPlan))
                    {
                        if (claimPlan.Status == LegacyBossHarmRankPackageClaimPlanStatus.InventoryFull)
                        {
                            await context.Connection.SendAsync(
                                    LegacyMsgMessagePacket.SystemTalk("STR_BAG_FULL@@").ToFrameBytes(),
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }

                        await context.Log.WriteLineAsync(
                                "WorldServer handled LegacyMailboxMode " +
                                $"{FormatSession(context)} type={packet.Type} id={packet.Id} flag={entry.Flag} " +
                                $"packageStatus={claimPlan.Status} ack=False.")
                            .ConfigureAwait(false);
                        return;
                    }

                    var updatedSession = await ApplyLegacyBossHarmRankPackageClaimAsync(
                            context,
                            session,
                            claimPlan,
                            cancellationToken)
                        .ConfigureAwait(false);
                    mailboxes.TryClearFlag(uid, packet.Id, packageClaimSucceeded: true, out _);
                    mailboxes.Save(uid);
                    await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer handled LegacyMailboxMode " +
                            $"{FormatSession(context with { Session = updatedSession })} type={packet.Type} id={packet.Id} " +
                            $"flag={entry.Flag} rank={claimPlan.Rank} itemStacks={claimPlan.ItemGrants.Count} ack=True.")
                        .ConfigureAwait(false);
                    return;
                }
        }

        await context.Log.WriteLineAsync(
                "WorldServer deferred LegacyMailboxMode " +
                $"{FormatSession(context)} type={packet.Type} id={packet.Id}; mailbox mutations are not wired yet.")
            .ConfigureAwait(false);
    }

}
