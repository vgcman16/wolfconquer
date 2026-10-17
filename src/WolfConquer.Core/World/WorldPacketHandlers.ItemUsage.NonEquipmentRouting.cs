using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyNonEquipmentItemUseAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            PlayerItemSnapshotItem item,
            CancellationToken cancellationToken)
    {
        var skillBook = context.WorldData is { } worldData
            ? LegacySkillBookUse.Classify(item.ItemId, worldData)
            : LegacySkillBookUse.Classify(item.ItemId);
        if (item.ItemId is not (
            LegacyBirdIslandGateItemId or
            LegacyMsgItemUsageTeleportTownId or
            LegacyMsgItemUsageTeleportWildMapId or
            LegacyMsgItemUsageTeleportTempleId or
            LegacyMsgItemUsageTeleportWaterId or
            LegacyMsgItemUsageTeleportDualMapId or
            LegacyMsgItemUsageTeleportNorthGateId or
            LegacyMsgItemUsageTeleportSouthGateId or
            LegacyMsgItemUsageTeleportTempleWestId or
            LegacyMsgItemUsageTeleportTempleEastId or
            LegacyArroyoScrollItemId or
            LegacyDailySignInPackItemId or
            LegacyHouseMaterialPackItemId or
            LegacyExpBallItemId or
            LegacyToughDrillItemId or
            LegacyStarDrillItemId or
            LegacyRelicSpiritItemNpcId or
            LegacyRelicCrystalItemNpcId) &&
            !IsLegacyDirectRuneFragmentItem(item.ItemId) &&
            !IsLegacyItemPackNpc(item.ItemId) &&
            !IsLegacyDemonBoxItem(item.ItemId) &&
            !skillBook.IsSkillBook)
        {
            return false;
        }

        if (session.Character.MinHitPoints <= 0 || HasLegacyStatusFlag(session.Character, LegacyDeadFlag))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage non-equipment item use " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; " +
                    "legacy UseItem requires the character to be alive.")
                .ConfigureAwait(false);
            return true;
        }

        switch (item.ItemId)
        {
            case var _ when skillBook.IsSkillBook:
                await HandleLegacySkillBookUseAsync(context, session, item, skillBook, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case LegacyMsgItemUsageTeleportTownId:
            case LegacyMsgItemUsageTeleportWildMapId:
            case LegacyMsgItemUsageTeleportTempleId:
            case LegacyMsgItemUsageTeleportWaterId:
            case LegacyMsgItemUsageTeleportDualMapId:
            case LegacyMsgItemUsageTeleportNorthGateId:
            case LegacyMsgItemUsageTeleportSouthGateId:
            case LegacyMsgItemUsageTeleportTempleWestId:
            case LegacyMsgItemUsageTeleportTempleEastId:
            case LegacyBirdIslandGateItemId:
                if (!LegacyMsgItemUsageTeleportDestinations.TryGetValue(item.ItemId, out var destinations))
                {
                    await context.Log.WriteLineAsync(
                            "PARITY-GAP MsgItemUsage teleport item " +
                            $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; " +
                            "source usage destination map is missing from local static mapping.")
                        .ConfigureAwait(false);
                    return true;
                }

                await HandleLegacyItemUsageTeleportItemUseAsync(context, session, item, destinations, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case LegacyArroyoScrollItemId:
                await HandleLegacyConsumeOnlyItemUseAsync(
                        context,
                        session,
                        item,
                        "ArroyoScroll",
                        "legacy UseItem consumes item 1060039 without a follow-up teleport or reward branch",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case LegacyDailySignInPackItemId:
                await HandleLegacyDailySignInPackUseAsync(context, session, item, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case LegacyHouseMaterialPackItemId:
                await HandleLegacyHouseMaterialPackUseAsync(context, session, item, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case LegacyExpBallItemId:
                await HandleLegacyExpBallUseAsync(context, session, item, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case LegacyToughDrillItemId:
            case LegacyStarDrillItemId:
                await HandleLegacyDrillDirectUseAsync(context, item)
                    .ConfigureAwait(false);
                return true;

            case LegacyRelicSpiritItemNpcId:
            case LegacyRelicCrystalItemNpcId:
                await HandleLegacyDeitylandSupportItemUseAsync(context, session, item, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case var _ when IsLegacyDirectRuneFragmentItem(item.ItemId):
                await HandleLegacyDirectRuneFragmentUseAsync(context, session, item, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case var _ when IsLegacyItemPackNpc(item.ItemId):
                await HandleLegacyItemPackUseAsync(context, session, item, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case var _ when IsLegacyDemonBoxItem(item.ItemId):
                await HandleLegacyDemonBoxItemUseAsync(context, session, item, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask HandleLegacyDrillDirectUseAsync(
        WorldPacketHandlerContext context,
        PlayerItemSnapshotItem item)
    {
        var family = item.ItemId == LegacyToughDrillItemId ? "tough drill" : "star drill";
        await context.Log.WriteLineAsync(
                $"WorldServer skipped MsgItemUsage {family} direct use " +
                $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; " +
                $"legacy UseItem has no direct case for item {item.ItemId}, and MsgItemUsuagePacket action " +
                $"CreateSocketItem handles ToughDrill {LegacyToughDrillItemId} and StarDrill {LegacyStarDrillItemId} second-socket forging.")
            .ConfigureAwait(false);
    }
}
