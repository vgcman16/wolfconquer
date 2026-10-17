using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyEpicTrojanBladeSpiritItemId = 3_003_340;
    private const uint LegacyTrojanEpicWeaponBaseItemId = 614_000;

    private static bool TryConvertLegacyEpicTrojanEligibleWeapon(
        PlayerItemSnapshot items,
        out PlayerItemSnapshot updatedItems,
        out PlayerItemSnapshotItem convertedItem,
        out bool fromInventory)
    {
        foreach (var item in items.InventoryItems)
        {
            if (!IsLegacyEpicTrojanConvertibleWeapon(item.ItemId))
            {
                continue;
            }

            convertedItem = item.WithItemId(ConvertLegacyEpicTrojanWeaponItemId(item.ItemId));
            updatedItems = ReplaceInventoryItem(items, item, convertedItem);
            fromInventory = true;
            return true;
        }

        foreach (var item in items.EquipmentItems)
        {
            if (!IsLegacyEpicTrojanConvertibleWeapon(item.ItemId))
            {
                continue;
            }

            convertedItem = item.WithItemId(ConvertLegacyEpicTrojanWeaponItemId(item.ItemId));
            updatedItems = ReplaceEquipmentItem(items, item, convertedItem);
            fromInventory = false;
            return true;
        }

        updatedItems = items;
        convertedItem = default!;
        fromInventory = false;
        return false;
    }

    private static bool IsLegacyEpicTrojanConvertibleWeapon(uint itemId)
    {
        var position = ItemType.GetLegacyEquipmentPosition(itemId);
        return position is 4 or 5 &&
            !ItemType.IsLegacyArrow(itemId) &&
            !ItemType.IsLegacyTwoHandWeapon(itemId) &&
            !IsLegacyTrojanEpicWeapon(itemId);
    }

    private static uint ConvertLegacyEpicTrojanWeaponItemId(uint itemId)
    {
        return itemId % 1_000 + LegacyTrojanEpicWeaponBaseItemId;
    }

    private static bool IsLegacyTrojanEpicWeapon(uint itemId)
    {
        return itemId is >= LegacyTrojanEpicWeaponBaseItemId and <= 614_439;
    }

    private static CharacterSnapshot ResetLegacyEpicTrojanState(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["EpicTrojanMrMirrorPrograss"] = "0",
            ["StageEpicTrojanQuest"] = "0",
            ["EpicTrojanEvilArrayPoints"] = "0",
            ["EpicTrojanAbysalStage"] = "0",
            ["EpicTrijanKillGhostReaver"] = "0",
            ["ChangeEpicTrojan"] = "0",
            ["ChangeArrayEpicTrojan"] = "0",
            ["ChangeMr_MirrorEpicTrojan"] = "0",
            ["ChangeGeneralPakEpicTrojan"] = "0",
            ["CanChangeEpicMaterial"] = "1",
            ["CanChangeArrayEpicMaterial"] = "1",
            ["CanChangeMr_MirrorEpicMaterial"] = "1",
            ["CanChangGeneralPakMaterial"] = "1",
        };

        return character with { Attributes = attributes };
    }

    private static async ValueTask<int> BroadcastLegacyMap3833EpicTrojanSuccessMessageAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemTalk(
                session.Character.Name +
                "~successfully~prevented~Twin~City~from~an~olden~massacre,~and~obtained~an~Epic~Weapon!")
            .ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is { Count: > 0 })
        {
            foreach (var active in activeSessions)
            {
                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }

            return activeSessions.Count;
        }

        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        return 1;
    }
}
