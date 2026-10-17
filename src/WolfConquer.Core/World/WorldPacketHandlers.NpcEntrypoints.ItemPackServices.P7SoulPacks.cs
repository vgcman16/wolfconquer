using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyItemPackServicesSelectP7EquipmentSoulPackNpcId = 4_294_967_293;
    private const uint LegacyItemPackServicesSelectP7WeaponSoulPackNpcId = 4_294_967_294;
    private const uint LegacyItemPackServicesP7EquipmentSoulPackItemId = 3_004_248;
    private const uint LegacyItemPackServicesP7WeaponSoulPackItemId = 3_004_247;
    private const string LegacyItemPackServicesP7SoulPackMissingText =
        "Sorry, you don`t have a P7WeaponSoulPack.";
    private const string LegacyItemPackServicesP7SoulPackPageText =
        "Select the p7 weapon you need.";
    private const string LegacyItemPackServicesP7SoulPackLaterPageText =
        "Selecte the p7 weapon you need.";

    private static async ValueTask<bool> LegacyItemPackServicesTryHandleP7EquipmentSoulPackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.SelectP7EquipmentSoulPack";
        if (!LegacyItemPackServicesHasUnboundP7SoulPack(
                session,
                LegacyItemPackServicesP7EquipmentSoulPackItemId))
        {
            await LegacyItemPackServicesSendMessageBoxAsync(
                    context,
                    source,
                    LegacyItemPackServicesP7SoulPackMissingText,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is 0 or 40)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    [LegacyItemPackServicesP7SoulPackPageText],
                    [
                        new("MoonHeadgear", 1),
                        new("SunHeadgear", 2),
                        new("StarHeadgear", 3),
                        new("IceHeadgear", 4),
                        new("HeavenNecklace", 5),
                        new("FervorBag", 6),
                        new("NetherArmor", 7),
                        new("Next~Page", 8),
                    ],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 8)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    [LegacyItemPackServicesP7SoulPackPageText],
                    [
                        new("EclipseArmor", 9),
                        new("CraneRing", 10),
                        new("DragonRing", 11),
                        new("RainbowBracelet", 12),
                        new("LionHeavyRing", 13),
                        new("TigerHeavyRing", 14),
                        new("FoxBoots", 15),
                        new("DragonBoots", 16),
                        new("CraneBoots", 17),
                        new("Back~Page", 40),
                    ],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (LegacyItemPackServicesP7EquipmentSoulRewards.TryGetValue(optionId, out var rewardItemId))
        {
            await LegacyItemPackServicesExchangeP7SoulAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacyItemPackServicesP7EquipmentSoulPackItemId,
                    rewardItemId,
                    source,
                    "32954-33017",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> LegacyItemPackServicesTryHandleP7WeaponSoulPackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.SelectP7WeaponSoulPack";
        if (!LegacyItemPackServicesHasUnboundP7SoulPack(
                session,
                LegacyItemPackServicesP7WeaponSoulPackItemId))
        {
            await LegacyItemPackServicesSendMessageBoxAsync(
                    context,
                    source,
                    LegacyItemPackServicesP7SoulPackMissingText,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is 0 or 20)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    [LegacyItemPackServicesP7SoulPackPageText],
                    [
                        new("MonsterSaber", 1),
                        new("SkyHammer", 2),
                        new("ShadowKatana", 3),
                        new("SkyHalberd", 4),
                        new("DemonScythe", 5),
                        new("SpiritShield", 6),
                        new("TimeBacksword", 7),
                        new("Next~Page", 8),
                    ],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 8)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    [LegacyItemPackServicesP7SoulPackLaterPageText],
                    [
                        new("SunBow", 9),
                        new("BuddaBeads", 10),
                        new("DeathPistol", 11),
                        new("RepentRapier", 12),
                        new("GhostKnife", 13),
                        new("WarCraze(Nunchaku)", 14),
                        new("WonderHossu", 15),
                        new("Back~Page", 20),
                        new("Next~Page", 21),
                    ],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 21)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    [LegacyItemPackServicesP7SoulPackLaterPageText],
                    [
                        new("FistofDeity(EpicWarrior)", 16),
                        new("FistofDemon(EpicWarrior)", 17),
                        new("DivineFan(WindWalker)", 18),
                        new("DivineShockwave(Thunderstriker-Stormhammer)", 25),
                        new("DivineShockwave(Thunderstriker-Flashaxe)", 26),
                        new("DivineVoidSplitter(Thunderstriker-Stormhammer)", 27),
                        new("DivineVoidSplitter(Thunderstriker-Flashaxe)", 28),
                        new("Back~Page", 8),
                    ],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (LegacyItemPackServicesP7WeaponSoulRewards.TryGetValue(optionId, out var rewardItemId))
        {
            await LegacyItemPackServicesExchangeP7SoulAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacyItemPackServicesP7WeaponSoulPackItemId,
                    rewardItemId,
                    source,
                    "33073-33155",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is 19 or 22 or 23 or 24)
        {
            await LegacyItemPackServicesLogNoOpAsync(context, npcId, optionId, source, "33157-33161")
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask LegacyItemPackServicesExchangeP7SoulAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        uint packItemId,
        uint rewardItemId,
        string source,
        string sourceLines,
        CancellationToken cancellationToken)
    {
        // Inventory.Remove selects the first matching item after the separate unbound Contain gate.
        var packItem = session.PlayerItems.InventoryItems.FirstOrDefault(item =>
            item.ItemId == packItemId);
        if (!LegacyItemPackServicesHasUnboundP7SoulPack(session, packItemId) || packItem is null)
        {
            await LegacyItemPackServicesLogP7SoulFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    rewardItemId,
                    source,
                    sourceLines,
                    "pack-count-drift")
                .ConfigureAwait(false);
            return;
        }

        var consumedItems = ConsumeSingleLegacyInventoryItem(
            session.PlayerItems,
            packItem,
            out var consumptionFrame);
        if (!HasLegacyInventorySpace(consumedItems, 1))
        {
            await LegacyItemPackServicesLogP7SoulFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    rewardItemId,
                    source,
                    sourceLines,
                    "inventory-full-after-planned-pack-removal")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LegacyItemPackServicesLogP7SoulFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    rewardItemId,
                    source,
                    sourceLines,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (!sessions.TryGet(context.Connection, out var registeredSession) || registeredSession is null)
        {
            await LegacyItemPackServicesLogP7SoulFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    rewardItemId,
                    source,
                    sourceLines,
                    "session-registration-missing")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.Uid <= 0)
        {
            await LegacyItemPackServicesLogP7SoulFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    rewardItemId,
                    source,
                    sourceLines,
                    "character-uid-unavailable")
                .ConfigureAwait(false);
            return;
        }

        var playerItemsRoot = context.PlayerItemsRoot;
        if (string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            await LegacyItemPackServicesLogP7SoulFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    rewardItemId,
                    source,
                    sourceLines,
                    "player-items-root-missing")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyItemType(context.WorldData, rewardItemId, out var rewardItemType))
        {
            await LegacyItemPackServicesLogP7SoulFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    rewardItemId,
                    source,
                    sourceLines,
                    "reward-itemtype-missing")
                .ConfigureAwait(false);
            return;
        }

        if (rewardItemType.Id != checked((int)rewardItemId) ||
            rewardItemType.LegacyDurability > ushort.MaxValue)
        {
            await LegacyItemPackServicesLogP7SoulFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    rewardItemId,
                    source,
                    sourceLines,
                    "reward-itemtype-invalid")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotItem grantedSoul;
        try
        {
            grantedSoul = CreatePurchasedItems(session.PlayerItems, rewardItemType, 1)[0]
                .WithColor(checked((uint)Random.Shared.Next(3, 9)));
        }
        catch (OverflowException)
        {
            await LegacyItemPackServicesLogP7SoulFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    rewardItemId,
                    source,
                    sourceLines,
                    "item-uid-exhausted")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = consumedItems with
        {
            InventoryItems = consumedItems.InventoryItems
                .Concat([grantedSoul])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(
                updatedSession.Character.Uid,
                updatedItems);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            System.Security.SecurityException)
        {
            await LegacyItemPackServicesLogP7SoulFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    rewardItemId,
                    source,
                    sourceLines,
                    "persistence-" + ex.GetType().Name)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(consumptionFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedSoul).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc ItemPackServices P7 soul exchange " +
                $"{FormatSession(updatedContext)} source={source} sourceLines={sourceLines} " +
                $"npcId={npcId.ToString(CultureInfo.InvariantCulture)} " +
                $"optionId={optionId.ToString(CultureInfo.InvariantCulture)} " +
                $"packItemId={packItemId.ToString(CultureInfo.InvariantCulture)} " +
                $"rewardItemId={rewardItemId.ToString(CultureInfo.InvariantCulture)} " +
                $"bound=false plus=0 socketOne=0 socketTwo=0 color={grantedSoul.Color.ToString(CultureInfo.InvariantCulture)} " +
                "sourceOrder=Remove,Add.")
            .ConfigureAwait(false);
    }

    private static bool LegacyItemPackServicesHasUnboundP7SoulPack(
        WorldSession session,
        uint packItemId)
    {
        var count = 0u;
        foreach (var item in session.PlayerItems.InventoryItems)
        {
            if (item.ItemId != packItemId || item.Bound != 0)
            {
                continue;
            }

            count += item.StackSize;
            if (count >= 1)
            {
                return true;
            }
        }

        return false;
    }

    private static async ValueTask LegacyItemPackServicesLogP7SoulFenceAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        uint packItemId,
        uint rewardItemId,
        string source,
        string sourceLines,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc ItemPackServices P7 soul exchange " +
                $"{FormatSession(context)} source={source} sourceLines={sourceLines} " +
                $"npcId={npcId.ToString(CultureInfo.InvariantCulture)} " +
                $"optionId={optionId.ToString(CultureInfo.InvariantCulture)} " +
                $"packItemId={packItemId.ToString(CultureInfo.InvariantCulture)} " +
                $"rewardItemId={rewardItemId.ToString(CultureInfo.InvariantCulture)} reason={reason}; " +
                "no item persistence, session mutation, or exchange packets were committed.")
            .ConfigureAwait(false);
    }

    private static readonly IReadOnlyDictionary<byte, uint> LegacyItemPackServicesP7EquipmentSoulRewards =
        new Dictionary<byte, uint>
        {
            [1] = 820_073,
            [2] = 820_074,
            [3] = 820_075,
            [4] = 820_076,
            [5] = 821_033,
            [6] = 821_034,
            [7] = 822_071,
            [9] = 822_072,
            [10] = 823_058,
            [11] = 823_059,
            [12] = 823_060,
            [13] = 823_061,
            [14] = 823_062,
            [15] = 824_018,
            [16] = 824_019,
            [17] = 824_020,
        };

    private static readonly IReadOnlyDictionary<byte, uint> LegacyItemPackServicesP7WeaponSoulRewards =
        new Dictionary<byte, uint>
        {
            [1] = 800_020,
            [2] = 800_111,
            [3] = 800_142,
            [4] = 800_215,
            [5] = 800_255,
            [6] = 800_422,
            [7] = 800_522,
            [9] = 800_618,
            [10] = 800_725,
            [11] = 800_810,
            [12] = 800_811,
            [13] = 800_917,
            [14] = 801_004,
            [15] = 801_104,
            [16] = 801_216,
            [17] = 801_218,
            [18] = 801_308,
            [25] = 827_010,
            [26] = 827_011,
            [27] = 827_012,
            [28] = 827_013,
        };
}
