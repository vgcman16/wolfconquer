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

            case LegacyMailboxModeType.ClaimMoney:
                {
                    if (!mailboxes.TryClaimMoney(uid, packet.Id, out var money, out var eMoney, out _))
                    {
                        await context.Log.WriteLineAsync(
                                "WorldServer handled LegacyMailboxMode " +
                                $"{FormatSession(context)} type={packet.Type} id={packet.Id} found=False.")
                            .ConfigureAwait(false);
                        return;
                    }

                    var updatedSession = await ApplyLegacyMailboxClaimMoneyAsync(
                            context,
                            session,
                            money,
                            eMoney,
                            cancellationToken)
                        .ConfigureAwait(false);
                    mailboxes.Save(uid);
                    await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer handled LegacyMailboxMode " +
                            $"{FormatSession(context with { Session = updatedSession })} type={packet.Type} id={packet.Id} " +
                            $"money={money} eMoney={eMoney} ack=True.")
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

            case LegacyMailboxModeType.ClaimAll:
                {
                    var updatedSession = session;
                    ulong totalMoney = 0;
                    uint totalEMoney = 0;
                    var claimedPackages = 0;
                    var claimedItems = 0;
                    var bagFullItems = 0;
                    var missingWarehouseItems = 0;
                    var refreshedCount = 0;
                    var changed = false;
                    var now = DateTime.Now;
                    foreach (var rawMailId in packet.Items)
                    {
                        if (rawMailId > uint.MaxValue)
                        {
                            continue;
                        }

                        var mailId = (uint)rawMailId;
                        if (!mailboxes.TryGet(uid, mailId, out _))
                        {
                            continue;
                        }

                        if (mailboxes.TryClaimMoney(uid, mailId, out var money, out var eMoney, out _))
                        {
                            changed |= money != 0 || eMoney != 0;
                            totalMoney = checked(totalMoney + money);
                            totalEMoney = checked(totalEMoney + eMoney);
                            updatedSession = await ApplyLegacyMailboxClaimMoneyAsync(
                                    context with { Session = updatedSession },
                                    updatedSession,
                                    money,
                                    eMoney,
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }

                        if (mailboxes.TryGet(uid, mailId, out var packageEntry) &&
                            packageEntry.Flag != 0 &&
                            LegacyBossHarmRankPackageClaimPlan.TryCreate(
                                packageEntry.Flag,
                                updatedSession.Character.Class,
                                LegacyInventorySlots - updatedSession.PlayerItems.InventoryItems.Count,
                                out var claimPlan))
                        {
                            updatedSession = await ApplyLegacyBossHarmRankPackageClaimAsync(
                                    context with { Session = updatedSession },
                                    updatedSession,
                                    claimPlan,
                                    cancellationToken)
                                .ConfigureAwait(false);
                            if (mailboxes.TryClearFlag(uid, mailId, packageClaimSucceeded: true, out _))
                            {
                                changed = true;
                                claimedPackages++;
                            }
                        }

                        var itemClaim = await TryApplyLegacyMailboxClaimItemAsync(
                                context with { Session = updatedSession },
                                updatedSession,
                                mailboxes,
                                uid,
                                mailId,
                                cancellationToken)
                            .ConfigureAwait(false);
                        updatedSession = itemClaim.UpdatedSession;
                        if (itemClaim.Claimed)
                        {
                            changed = true;
                            claimedItems++;
                        }

                        if (itemClaim.BagFull)
                        {
                            bagFullItems++;
                        }

                        if (itemClaim.MissingWarehouseItem)
                        {
                            missingWarehouseItems++;
                        }

                        if (mailboxes.TryGet(uid, mailId, out var refreshed))
                        {
                            var response = new LegacyMailListPacket(
                                LegacyMailListType.ClaimAllUpdate,
                                Count: 1,
                                Mails: [LegacyMailListPacket.FromMailboxEntry(refreshed, now)]);
                            await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken)
                                .ConfigureAwait(false);
                            refreshedCount++;
                        }
                    }

                    if (changed)
                    {
                        mailboxes.Save(uid);
                    }

                    await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer handled LegacyMailboxMode " +
                            $"{FormatSession(context with { Session = updatedSession })} type={packet.Type} id={packet.Id} " +
                            $"requested={packet.Items.Count} refreshed={refreshedCount} money={totalMoney} eMoney={totalEMoney} " +
                            $"claimedPackages={claimedPackages} claimedItems={claimedItems} bagFullItems={bagFullItems} " +
                            $"missingWarehouseItems={missingWarehouseItems} ack=True.")
                        .ConfigureAwait(false);
                    return;
                }

            case LegacyMailboxModeType.ClaimItem:
                {
                    var itemClaim = await TryApplyLegacyMailboxClaimItemAsync(
                            context,
                            session,
                            mailboxes,
                            uid,
                            packet.Id,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (!itemClaim.Found)
                    {
                        await context.Log.WriteLineAsync(
                                "WorldServer handled LegacyMailboxMode " +
                                $"{FormatSession(context)} type={packet.Type} id={packet.Id} found=False.")
                            .ConfigureAwait(false);
                        return;
                    }

                    if (itemClaim.Claimed)
                    {
                        mailboxes.Save(uid);
                    }

                    await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer handled LegacyMailboxMode " +
                            $"{FormatSession(context with { Session = itemClaim.UpdatedSession })} type={packet.Type} id={packet.Id} " +
                            $"itemUid={itemClaim.ItemUid} itemId={itemClaim.ItemId} claimed={itemClaim.Claimed} " +
                            $"bagFull={itemClaim.BagFull} missingWarehouseItem={itemClaim.MissingWarehouseItem} ack=True.")
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

    private static async ValueTask<WorldSession> ApplyLegacyMailboxClaimMoneyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ulong money,
        uint eMoney,
        CancellationToken cancellationToken)
    {
        var updatedCharacter = session.Character;
        if (money != 0)
        {
            var updatedMoney = checked(updatedCharacter.Money + (long)money);
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter with { Money = updatedMoney },
                "Money",
                CheckedUInt64(updatedMoney, nameof(updatedMoney)));
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk($"STR_PICK_MONEY@@{money}@@").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendScalarUpdateAsync(
                    context,
                    updatedCharacter,
                    LegacyMsgUpdateDataType.Money,
                    checked((ulong)updatedCharacter.Money),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (eMoney != 0)
        {
            var updatedConquerPoints = checked(updatedCharacter.ConquerPoints + eMoney);
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter with { ConquerPoints = updatedConquerPoints },
                "ConquerPoints",
                CheckedUInt64(updatedConquerPoints, nameof(updatedConquerPoints)));
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk($"You received {eMoney} ConquerPoints.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendScalarUpdateAsync(
                    context,
                    updatedCharacter,
                    LegacyMsgUpdateDataType.ConquerPoints,
                    checked((ulong)updatedCharacter.ConquerPoints),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        return updatedSession;
    }

    private static async ValueTask<(
        WorldSession UpdatedSession,
        bool Found,
        bool Claimed,
        bool BagFull,
        bool MissingWarehouseItem,
        uint ItemUid,
        uint ItemId)> TryApplyLegacyMailboxClaimItemAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMailboxStore mailboxes,
        uint playerUid,
        uint mailId,
        CancellationToken cancellationToken)
    {
        if (!mailboxes.TryGet(playerUid, mailId, out var entry))
        {
            return (session, Found: false, Claimed: false, BagFull: false, MissingWarehouseItem: false, ItemUid: 0, ItemId: 0);
        }

        if (entry.ItemId == 0)
        {
            return (session, Found: true, Claimed: false, BagFull: false, MissingWarehouseItem: false, ItemUid: 0, ItemId: 0);
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("STR_BAG_FULL@@").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            return (
                session,
                Found: true,
                Claimed: false,
                BagFull: true,
                MissingWarehouseItem: false,
                ItemUid: entry.ItemId,
                ItemId: 0);
        }

        var warehouseItem = session.PlayerItems.WarehouseItems
            .FirstOrDefault(item => item.WarehouseId == LegacyMailboxWarehouseId && item.Uid == entry.ItemId);
        if (warehouseItem is null)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyMailboxMode ClaimItem missing mailbox warehouse item " +
                    $"{FormatSession(context)} mailId={mailId} itemUid={entry.ItemId} " +
                    $"warehouseId={LegacyMailboxWarehouseId}; preserving mailbox item attachment.")
                .ConfigureAwait(false);
            return (
                session,
                Found: true,
                Claimed: false,
                BagFull: false,
                MissingWarehouseItem: true,
                ItemUid: entry.ItemId,
                ItemId: 0);
        }

        var inventoryItem = warehouseItem.WithWarehouseId(0, position: 0);
        var updatedSession = session with
        {
            PlayerItems = MoveWarehouseItemToInventory(session.PlayerItems, warehouseItem, inventoryItem),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        mailboxes.TryClearItemId(playerUid, mailId, itemClaimSucceeded: true, out _);

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(inventoryItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        return (
            updatedSession,
            Found: true,
            Claimed: true,
            BagFull: false,
            MissingWarehouseItem: false,
            ItemUid: inventoryItem.Uid,
            ItemId: inventoryItem.ItemId);
    }

}
