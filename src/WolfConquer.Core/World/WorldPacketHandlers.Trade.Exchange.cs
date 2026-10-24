using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static string ApplyLegacyTradeExchangeAction(WorldPacketHandlerContext context,
        LegacyTradeStateStore trades, LegacyTradeParty party, LegacyTradePacket packet, List<LegacyTradeSend> sends)
    {
        if (!TryGetLegacyTradeSessions(context, party, out var own, out var other)) return "stalePairBinding";
        if (!IsLegacyTradeSecurityAdmitted(party.Other.Connection, other)) return "partnerSecondaryPasswordUnverifiedOrMalformed";
        switch (packet.Type)
        {
            case 6: return OfferLegacyTradeItem(context, party, own, other, packet, sends);
            case 7:
            case 12: return OfferLegacyTradeCurrency(context, party, own, other, packet, sends);
            case 9: return ConfirmLegacyTrade(context, trades, party, own, other, packet, sends);
            // Native 3/4/5/8/10/11 are server notifications, not client mutations.
            default: return "ignoredServerOrUnknownAction";
        }
    }

    private static string OfferLegacyTradeItem(WorldPacketHandlerContext context, LegacyTradeParty party,
        WorldSession own, WorldSession other, LegacyTradePacket packet, List<LegacyTradeSend> sends)
    {
        if (packet.ItemUID is 0 or > uint.MaxValue) return "invalidItemUid";
        if (!LegacyTradeItemGraph.TryCapture(own.PlayerItems, out var ownItems) ||
            !LegacyTradeItemGraph.TryCapture(other.PlayerItems, out var otherItems)) return "tradeItemGraphInvalid";
        if (!ownItems.TryGetInventory((uint)packet.ItemUID, out var closure)) return "missingOrAmbiguousInventoryItem";
        var item = closure.Source;
        if (!IsLegacyTradeClosureTradable(context, party, closure))
        {
            TradePacket(sends, party, TradeReply(packet, 10));
            TradeMessage(sends, party.Connection, party.Uid, "unable to trade this item.");
            return "itemNotTradable";
        }
        if (party.Items.ContainsKey(item.Uid)) return "duplicateOffer";
        if (otherItems.ContainsAny(closure)) return "receivingUidCollision";
        if (LegacyInventorySlots - other.PlayerItems.InventoryItems.Count < party.Items.Count + 1)
        {
            TradePacket(sends, party, TradeReply(packet, 10));
            TradeMessage(sends, party.Connection, party.Uid, "There is not enough room in your partner inventory.");
            return "offerCapacity";
        }
        try
        {
            // Mode Trade=2; the underlying inventory item is never temporarily mutated.
            var preview = LegacyGameItemPacket.FromSnapshotItem(item, mode: 2).ToFrameBytes();
            if (!context.Sessions!.TryCommitPair(party.Connection, own, own, party.Other.Connection, other, other,
                    () => ownItems.IsCurrent(own.PlayerItems) && otherItems.IsCurrent(other.PlayerItems) &&
                          IsLegacyTradeClosureTradable(context, party, closure)))
                return "staleItemOffer";
            party.Items.Add(item.Uid, new(item, closure));
            party.ResetConfirmations(); // Harden changes after confirmation instead of exchanging unseen edits.
            sends.Add(new(party.Other.Connection, party.Other.Uid, preview));
            return "itemOffered";
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            return "itemRepresentationInvalid";
        }
    }

    private static string OfferLegacyTradeCurrency(WorldPacketHandlerContext context, LegacyTradeParty party,
        WorldSession own, WorldSession other, LegacyTradePacket packet, List<LegacyTradeSend> sends)
    {
        // Currency escrow must remain refundable through the complete real item owner.
        // This is save validity only: unrelated bound/locked items are permitted.
        if (!LegacyTradeItemGraph.TryCapture(own.PlayerItems, out var ownItems) ||
            !LegacyTradeItemGraph.TryCapture(other.PlayerItems, out var otherItems)) return "tradeItemGraphInvalid";
        if (packet.Money < 0 || own.Character.Money < 0 || own.Character.ConquerPoints is < 0 or > uint.MaxValue)
            return "invalidCurrency";
        var money = packet.Type == 7;
        if ((money && own.Character.Money < packet.Money) || (!money && own.Character.ConquerPoints < packet.CPS))
            return "insufficientCurrency";
        if ((money && packet.Money == 0) || (!money && packet.CPS == 0))
        {
            if (!context.Sessions!.TryCommitPair(party.Connection, own, own, party.Other.Connection, other, other,
                    () => ownItems.IsCurrent(own.PlayerItems) && otherItems.IsCurrent(other.PlayerItems)))
                return "staleCurrencyOffer";
            party.ResetConfirmations();
            TradeWallet(sends, party, own);
            TradePacket(sends, party.Other, TradeReply(packet, money ? 8u : 11u));
            return "zeroCurrencyOffered";
        }
        long newMoney;
        uint newCp;
        WorldSession updated;
        try
        {
            newMoney = money ? checked(party.Money + packet.Money) : party.Money;
            newCp = money ? party.ConquerPoints : checked(party.ConquerPoints + packet.CPS);
            updated = WithLegacyTradeWallet(own, money ? own.Character.Money - packet.Money : own.Character.Money,
                money ? own.Character.ConquerPoints : own.Character.ConquerPoints - packet.CPS);
            updated = LegacyTradeEscrowClaims.WithClaim(updated,
                new LegacyTradeEscrowClaim(party.TradeId, newMoney, newCp));
        }
        catch (OverflowException) { return "escrowOverflow"; }
        var reason = string.Empty;
        if (!context.Sessions!.TryCommitPair(party.Connection, own, updated, party.Other.Connection, other, other,
                () =>
                {
                    if (!ownItems.IsCurrent(own.PlayerItems) || !otherItems.IsCurrent(other.PlayerItems))
                    {
                        reason = "tradeItemGraphChanged";
                        return false;
                    }
                    return TryPersistLegacyTradeWallet(context, own, updated, out reason);
                }))
        {
            if (IsLegacyTradeRecoveryRequired(reason))
            {
                TradeMessage(sends, party.Connection, party.Uid,
                    "Trade recovery is required. Actions are paused until server recovery.");
                TradeMessage(sends, party.Other.Connection, party.Other.Uid,
                    "Trade recovery is required. Actions are paused until server recovery.");
            }
            return "escrowCommitRejected:" + reason;
        }
        party.Money = newMoney;
        party.ConquerPoints = newCp;
        party.ResetConfirmations();
        TradeWallet(sends, party, updated);
        TradePacket(sends, party.Other, TradeReply(packet, money ? 8u : 11u));
        return money ? "moneyEscrowed" : "conquerPointsEscrowed";
    }

    private static string ConfirmLegacyTrade(WorldPacketHandlerContext context, LegacyTradeStateStore trades,
        LegacyTradeParty party, WorldSession own, WorldSession other, LegacyTradePacket packet,
        List<LegacyTradeSend> sends)
    {
        party.Confirmed = true;
        if (!party.Other.Confirmed)
        {
            TradePacket(sends, party.Other, TradeReply(packet, 9));
            return "awaitingOtherConfirmation";
        }

        // Never serialize a malformed graph to manufacture a refund. Cancel/disconnect
        // use the same structural guard at the persistence boundary and retain escrow too.
        if (!LegacyTradeItemGraph.TryCapture(own.PlayerItems, out var ownItems) ||
            !LegacyTradeItemGraph.TryCapture(other.PlayerItems, out var otherItems))
        {
            party.ResetConfirmations();
            TradeMessage(sends, party.Connection, party.Uid, "Trade item state is invalid; escrow is retained.");
            TradeMessage(sends, party.Other.Connection, party.Other.Uid, "Trade item state is invalid; escrow is retained.");
            return "offeredItemGraphInvalid:escrowRetained";
        }
        var problem = string.Empty;
        if (LegacyInventorySlots - own.PlayerItems.InventoryItems.Count < party.Other.Items.Count ||
            LegacyInventorySlots - other.PlayerItems.InventoryItems.Count < party.Items.Count)
            problem = "capacityBeforeOutgoingRemovals";
        else if (!ValidLegacyTradeOffers(context, party, ownItems) || !ValidLegacyTradeOffers(context, party.Other, otherItems))
            problem = "offeredItemChangedOrMissing";
        else if (LegacyTradeHasUidCollision(ownItems, party.Other) || LegacyTradeHasUidCollision(otherItems, party))
            problem = "receivingUidCollision";
        if (problem.Length != 0)
        {
            TradeMessage(sends, party.Connection, party.Uid, "There was an error with the trade");
            TradeMessage(sends, party.Other.Connection, party.Other.Uid, "There was an error with the trade");
            return CloseLegacyTrade(context, trades, party, packet, sends) ? problem + ":refunded" : problem + ":escrowRetained";
        }

        WorldSession updatedOwn, updatedOther;
        var committedFrames = new List<LegacyTradeSend>();
        try
        {
            updatedOwn = WithLegacyTradeWallet(own, checked(own.Character.Money + party.Other.Money),
                checked(own.Character.ConquerPoints + party.Other.ConquerPoints));
            updatedOther = WithLegacyTradeWallet(other, checked(other.Character.Money + party.Money),
                checked(other.Character.ConquerPoints + party.ConquerPoints));
            if (updatedOwn.Character.Money < 0 || updatedOther.Character.Money < 0 ||
                updatedOwn.Character.ConquerPoints is < 0 or > uint.MaxValue ||
                updatedOther.Character.ConquerPoints is < 0 or > uint.MaxValue) throw new OverflowException();
            var ownIncoming = party.Other.Items.Values.Select(x => x.Closure.CreateInventoryItem()).ToArray();
            var otherIncoming = party.Items.Values.Select(x => x.Closure.CreateInventoryItem()).ToArray();
            updatedOwn = updatedOwn with
            {
                LegacyTradeActive = false,
                PlayerItems = own.PlayerItems with { InventoryItems = own.PlayerItems.InventoryItems
                    .Where(x => !party.Items.ContainsKey(x.Uid)).Concat(ownIncoming).ToArray() },
            };
            updatedOther = updatedOther with
            {
                LegacyTradeActive = false,
                PlayerItems = other.PlayerItems with { InventoryItems = other.PlayerItems.InventoryItems
                    .Where(x => !party.Other.Items.ContainsKey(x.Uid)).Concat(otherIncoming).ToArray() },
            };
            updatedOwn = LegacyTradeEscrowClaims.WithClaim(updatedOwn, null);
            updatedOther = LegacyTradeEscrowClaims.WithClaim(updatedOther, null);
            TradeWallet(committedFrames, party, updatedOwn);
            TradeWallet(committedFrames, party.Other, updatedOther);
            foreach (var item in ownIncoming)
            {
                TradeItem(committedFrames, party, item, LegacyGameItemPacket.AddItemMode);
                RemoveLegacyTradeItem(committedFrames, party.Other, item.Uid);
            }
            foreach (var item in otherIncoming)
            {
                TradeItem(committedFrames, party.Other, item, LegacyGameItemPacket.AddItemMode);
                RemoveLegacyTradeItem(committedFrames, party, item.Uid);
            }
            TradePacket(committedFrames, party, TradeReply(packet, 4) with { UID = party.Uid });
            TradePacket(committedFrames, party.Other, TradeReply(packet, 4) with { UID = party.Other.Uid });
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            party.ResetConfirmations();
            TradeMessage(sends, party.Connection, party.Uid, "Trade cannot be represented safely; cancel to recover escrow.");
            return "exchangeRepresentationOrCurrencyOverflow";
        }
        var reason = string.Empty;
        if (!context.Sessions!.TryCommitPair(party.Connection, own, updatedOwn, party.Other.Connection, other, updatedOther,
                () => TryPersistLegacyTradePair(context, own, updatedOwn, other, updatedOther, out reason,
                    ownItems, otherItems, party)))
        {
            party.ResetConfirmations();
            TradeMessage(sends, party.Connection, party.Uid, IsLegacyTradeRecoveryRequired(reason)
                ? "Trade recovery is required. Actions are paused until server recovery."
                : "Trade could not be saved; no exchange occurred. Retry or cancel.");
            if (IsLegacyTradeRecoveryRequired(reason))
                TradeMessage(sends, party.Other.Connection, party.Other.Uid,
                    "Trade recovery is required. Actions are paused until server recovery.");
            return "exchangeCommitRejected:" + reason;
        }
        trades.Remove(party);
        sends.AddRange(committedFrames);
        return "exchanged";
    }

    private static void RemoveLegacyTradeItem(List<LegacyTradeSend> sends, LegacyTradeParty owner, uint itemUid) =>
        sends.Add(new(owner.Connection, owner.Uid,
            new MsgItemUsagePacket(itemUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes()));
}
