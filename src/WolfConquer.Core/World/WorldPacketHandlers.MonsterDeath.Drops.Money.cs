using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryAddLegacyMonsterDeathMoneyDrop(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        WorldDataCatalog worldData,
        int mapId,
        uint dynamicId,
        ushort x,
        ushort y,
        out FloorItemRuntimeEntry entry,
        out LegacyMonsterDeathDropSelection selection,
        out bool placementRejected,
        out string noDropReason)
    {
        var generated = false;
        var selected = default(LegacyMonsterDeathDropSelection);
        var reason = "";

        (uint ItemId, uint Amount)? GenerateMoneyPayload()
        {
            generated = true;
            var amount = ReadLegacyMonsterDeathDropMoneyAmount(character);
            var candidate = GetLegacyMoneyItemId(amount);
            selected = new LegacyMonsterDeathDropSelection(candidate, FloorItemRuntimePickupKind.Money, amount);
            if (!TryUseKnownLegacyDropItemId(worldData, candidate, out var itemId))
            {
                reason = $"source money drop item id {candidate} is not present in the local itemtype catalog";
                return null;
            }

            return (itemId, amount);
        }

        var packet = LegacyFloorItemPacket.FromMonsterDrop(0, x, y, character, floorUid: 0);
        // This draft is never indexed or sent. Only the placed, generated payload
        // can enter the visible store; no amount RNG runs for rejected placement.
        var draft = new FloorItemRuntimeEntry(mapId, packet,
            FloorItemRuntimePickupKind.Money, Amount: 0, Item: null, OwnerOnly: true)
        { DynamicId = dynamicId };
        bool added;
        if (context.FloorItems is { } floorItems)
        {
            added = floorItems.TryAddMoneyVisibleOnGround(draft, DateTimeOffset.UtcNow, 0,
                (groundMapId, groundX, groundY) =>
                    // Native GameMap.IsFlagPresent never admits either zero edge.
                    groundX > 0 && groundY > 0 &&
                    TryValidateStaticTerrainLocation(context, groundMapId, groundX, groundY, out _),
                GenerateMoneyPayload, NextLegacyFloorItemUid, out entry);
        }
        else
        {
            // Retain the existing untracked-send compatibility path; its caller
            // reports missing runtime state rather than claiming pickup tracking.
            var payload = GenerateMoneyPayload();
            added = payload.HasValue;
            entry = payload is { } money
                ? draft with
                {
                    Packet = packet with { ItemId = money.ItemId, Uid = NextLegacyFloorItemUid() },
                    Amount = money.Amount,
                }
                : default!;
        }

        if (!added && generated && reason.Length == 0)
        {
            reason = "generated money drop floor UID collides with a visible or reserved entry";
        }
        placementRejected = !generated;
        selection = selected;
        noDropReason = placementRejected
            ? "source money drop could not be placed on ground"
            : reason;
        return added;
    }
}
