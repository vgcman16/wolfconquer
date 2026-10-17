using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryPurchaseLegacyAccountManagerItemAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        uint itemId,
        ulong cost,
        CancellationToken cancellationToken)
    {
        var donatePoints = ReadAttributeUInt64(session.Character.Attributes, "DonatePoints");
        if (donatePoints < cost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.AccountManager",
                    [
                        string.Concat(
                            LegacyAccountManagerInsufficientDonatePointsText,
                            " You need ",
                            cost.ToString(CultureInfo.InvariantCulture),
                            " donation points."),
                    ],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    LegacyAccountManagerShopAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountManager item purchase " +
                    $"{FormatSession(context)} source=NpcHandler.AccountManager option={optionId} " +
                    $"itemId={itemId} cost={cost} donatePoints={donatePoints}.")
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("Please make 1 more space in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountManager item purchase " +
                    $"{FormatSession(context)} source=NpcHandler.AccountManager option={optionId} " +
                    $"itemId={itemId} cost={cost}; inventory is full.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedDonatePoints = donatePoints - cost;
        var grantedItem = CreateLegacyAccountManagerItemGrant(context, session.PlayerItems, itemId);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat([grantedItem]).ToArray(),
        };
        var updatedCharacter = WithCharacterAttribute(session.Character, "DonatePoints", updatedDonatePoints);
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = updatedDonatePoints.ToString(CultureInfo.InvariantCulture),
        };
        if (TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc AccountManager item purchase character " +
                    $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                    $"path={Path.GetFileName(path)} keys=DonatePoints.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountManager item purchase character persistence " +
                    $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                    $"reason={reason}.")
                .ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            try
            {
                new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                    updatedCharacter.Uid,
                    updatedItems);
                await context.Log.WriteLineAsync(
                        "WorldServer persisted LegacyNpc AccountManager item purchase items " +
                        $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                        $"path={updatedCharacter.Uid.ToString(CultureInfo.InvariantCulture)}.bin itemId={itemId}.")
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc AccountManager item purchase persistence " +
                        $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                        $"itemId={itemId} reason={ex.Message}.")
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc AccountManager item purchase missing PlayerItemsRoot " +
                    $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                    $"itemId={itemId}; granted item is session-only.")
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountManager item purchase " +
                $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                $"option={optionId} itemId={itemId} cost={cost} donatePoints={updatedDonatePoints}.")
            .ConfigureAwait(false);
        return true;
    }

    private static PlayerItemSnapshotItem CreateLegacyAccountManagerItemGrant(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot items,
        uint itemId)
    {
        var durability = TryReadItemType(context.WorldData, itemId, out var itemType)
            ? checked((ushort)itemType.LegacyDurability)
            : (ushort)0;
        return new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(items),
            ItemId = itemId,
            Durability = durability,
            MaximumDurability = durability,
            Position = 0,
            StackSize = 1,
        };
    }
}
