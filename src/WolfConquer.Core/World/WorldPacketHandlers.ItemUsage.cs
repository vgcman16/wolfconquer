using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{

    private const uint LegacyRemoteShopId = 2_888;

    private const uint LegacyExchangeShopRandomShopId = 168;

    private const uint LegacyHookScytheExceptionSpellId = 10_311;

    private const int LegacyInventorySlots = 40;

    private const uint LegacyToughDrillItemId = 1_200_005;

    private const uint LegacySocketMeteorItemId = 1_088_000;

    private const uint LegacyStarDrillItemId = 1_200_006;

    private const double LegacyExpBallAmount = 600.0;

    private const byte LegacyNoSocket = 0;

    private const byte LegacyEmptySocket = byte.MaxValue;

    private const byte LegacyNormalRainbowGem = 61;

    private const byte LegacyRefinedRainbowGem = 62;

    private const byte LegacySuperRainbowGem = 63;

    private const string RuntimeLegacyToughDrillSocketRollAttribute = "LegacyToughDrillSocketRoll";

    [PacketHandler(WorldPacketOpcodes.MsgItemUsage)]
    private static async ValueTask HandleMsgItemUsageAsync(
            WorldPacketHandlerContext context,
            PacketFrame frame,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!MsgItemUsagePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed MsgItemUsage packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action is MsgItemUsageAction.DecomposeRelics or MsgItemUsageAction.DecomposeRune or
            MsgItemUsageAction.DropRune or MsgItemUsageAction.ComposeRune or MsgItemUsageAction.ChanceRune or
            MsgItemUsageAction.Rune)
            context = CaptureLegacyRuneRankingPublicationOwner(context);

        // This stage has no native public item-mutation writers. Never expose its old raw image
        // as a current lookup after these public mutation intents (including rejected requests).
        if (packet.Action is not (MsgItemUsageAction.Ping or MsgItemUsageAction.ShowItem or
                MsgItemUsageAction.ShowWarehouseMoney))
            context.Connection.NativeGameClient?.RawEquipmentObservation?.InvalidateRawLoadObservation("publicItemUsageMutationIntent");
        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage " +
                $"{FormatSession(context)} action={packet.Action} id={packet.Id} dwParam={packet.DwParam} " +
                $"timestamp={packet.Timestamp} from {context.Connection.RemoteEndPoint}.")
            .ConfigureAwait(false);

        if (packet.Action == MsgItemUsageAction.Ping)
        {
            await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    $"WorldServer echoed MsgItemUsage ping {FormatSession(context)} id={packet.Id}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgItemUsage state handling {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.ShowItem)
        {
            await HandleVisibleDx8D13ShowChatItemAsync(context, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action is
            MsgItemUsageAction.ShowVendingList or
            MsgItemUsageAction.AddVendingItemGold or
            MsgItemUsageAction.RemoveVendingItem or
            MsgItemUsageAction.BuyVendingItem or
            MsgItemUsageAction.AddVendingItemConquerPoints)
        {
            await HandleVisibleDx8D13VendorAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action is MsgItemUsageAction.Equip or MsgItemUsageAction.Unequip)
        {
            await HandleOwnedEquipmentIntentAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Action is (MsgItemUsageAction.RemoveInventory
                or MsgItemUsageAction.SetEquipPosition
                or MsgItemUsageAction.UpdateDurability
                or MsgItemUsageAction.ActiveItems) &&
            await TryHandleLegacyConsumableItemUseAsync(context, session, packet, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (packet.Action == MsgItemUsageAction.ShowWarehouseMoney)
        {
            var response = packet with
            {
                DwParam = ReadWarehouseMoney(session.Character),
            };

            await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer sent MsgItemUsage warehouse money " +
                    $"{FormatSession(context)} money={response.DwParam}.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action is MsgItemUsageAction.DepositWarehouse or MsgItemUsageAction.WarehouseWithdraw)
        {
            await HandleWarehouseMoneyMutationAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.BuyItem)
        {
            await HandleBuyItemAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.BuyItemFromForging)
        {
            await HandleBuyItemFromForgingAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.RepairItem)
        {
            await HandleRepairItemAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.SellItem)
        {
            await HandleSellItemAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.DropItem)
        {
            await HandleDropItemAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.CreateSocketItem)
        {
            await HandleLegacyCreateSocketItemAsync(context, session, packet, frame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.DecomposeRelics)
        {
            await HandleLegacyDecomposeRelicsAsync(context, session, packet, frame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.RedRuneProgressControl)
        {
            await HandleLegacyRedRuneProgressControlAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.DecomposeRune)
        {
            await HandleLegacyDecomposeRuneAsync(context, session, packet, frame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.DropRune)
        {
            await HandleLegacyDropRuneAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.ComposeRune)
        {
            await HandleLegacyComposeRuneAsync(context, session, packet, frame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.ChanceRune)
        {
            await HandleLegacyChanceRuneAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.Rune)
        {
            await HandleLegacyDirectRuneBagItemUseAsync(context, session, packet, frame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.EquipRune)
        {
            await HandleLegacyEquipRuneAsync(context, session, packet, frame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.UnequipRune)
        {
            await HandleLegacyUnequipRuneAsync(context, session, packet, frame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.MythForggingMythsoulBagAdd)
        {
            await HandleLegacyMythsoulBagAddAsync(context, session, packet, frame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == MsgItemUsageAction.MythForggingMythsoulBagRemove)
        {
            await HandleLegacyMythsoulBagRemoveAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action is MsgItemUsageAction.UnAlternante or MsgItemUsageAction.Alternante)
        {
            if (IsAlternateEquipmentToggleBlocked(session, out var reason))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped alternate equipment toggle " +
                        $"{FormatSession(context)} reason={reason}.")
                    .ConfigureAwait(false);
                return;
            }

            var updated = session with { AlternateEquipmentActive = !session.AlternateEquipmentActive };
            context.Sessions?.Set(context.Connection, updated);
            await SendPlayerSpawnToVisiblePeersAsync(context, updated, cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer toggled alternate equipment " +
                    $"{FormatSession(context)} active={updated.AlternateEquipmentActive}.")
                .ConfigureAwait(false);
            return;
        }

        await LogUnsupportedItemUsageWireEvidenceAsync(context, session, packet, frame)
            .ConfigureAwait(false);
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.UnsupportedItemUse(
                    packet.Id,
                    action: (uint)packet.Action,
                    actionName: packet.Action.ToString(),
                    source: "MsgItemUsage",
                    reason: "MsgItemUsage action is not modeled"))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP unsupported MsgItemUsage " +
                $"{FormatSession(context)} action={packet.Action} actionId={(ushort)packet.Action} " +
                $"id={packet.Id} dwParam={packet.DwParam} dwParam2={packet.DwParam2} " +
                $"dwParam3={packet.DwParam3} dwParam4={packet.DwParam4} timestamp={packet.Timestamp}; " +
                $"{FormatFrameDiagnostic(frame)}")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleVisibleDx8D13ShowChatItemAsync(
        WorldPacketHandlerContext context,
        MsgItemUsagePacket packet,
        CancellationToken cancellationToken)
    {
        if (context.ProtocolProfile != ConquerProtocolProfile.VisibleDx8D13)
        {
            await context.Log.WriteLineAsync(
                    "PROTOCOL-PROFILE skipped MsgItemUsage ShowItem " +
                    $"profile={context.ProtocolProfile} action={(ushort)packet.Action} id={packet.Id} " +
                    "reason=action 52 is only verified in the VisibleDx8D13 executable.")
                .ConfigureAwait(false);
            return;
        }

        if (context.ChatItems is null || !context.ChatItems.TryGet(packet.Id, out var entry) || entry is null)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer missed D13 chat item {FormatSession(context)} uid={packet.Id}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(entry.Item.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        if (entry.Extra is not null)
        {
            await context.Connection.SendAsync(entry.Extra.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        if (entry.Lock is { } itemLock)
        {
            await context.Connection.SendAsync(itemLock.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                $"WorldServer served D13 chat item {FormatSession(context)} uid={packet.Id} mode={entry.Item.Mode}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyConsumableItemUseAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            CancellationToken cancellationToken)
    {
        var item = session.PlayerItems.InventoryItems.FirstOrDefault(item => item.Uid == packet.Id);
        if (item is null) return false;
        switch (LegacyConsumableItemUse.Classify(item.ItemId))
        {
            case LegacyConsumableItemKind.Hitpoints:
                await HandleOwnedHealthConsumableAsync(context, session, packet.Id, cancellationToken).ConfigureAwait(false);
                break;
            case LegacyConsumableItemKind.Mana:
                await HandleOwnedManagedManaConsumableAsync(context, session, packet.Id, cancellationToken).ConfigureAwait(false);
                break;
            case LegacyConsumableItemKind.FullRestore:
                await HandleOwnedFullRestoreConsumableAsync(context, session, packet.Id, cancellationToken).ConfigureAwait(false);
                break;
            default:
                return false;
        }
        return true;
    }

    private static CharacterSnapshot AddLegacyDailySignInChiPoints(
            CharacterSnapshot character,
            int points)
    {
        var current = LegacyChiState.FromSnapshot(character).ChiPoints;
        var updated = checked(current + points);
        return WithCharacterAttribute(
            character,
            "ChiPoints",
            updated.ToString(CultureInfo.InvariantCulture));
    }

    private static bool TryCalculateLegacyExpBallUse(
            WorldDataCatalog? worldData,
            CharacterSnapshot character,
            double amount,
            out long updatedLevel,
            out long updatedExperience)
    {
        updatedLevel = character.Level;
        updatedExperience = character.Experience;

        if (worldData is null ||
            character.Level < 0 ||
            character.Level >= LegacyMaxPlayerLevel ||
            character.Level > int.MaxValue ||
            character.Experience < 0 ||
            !worldData.LevelExperienceByTypeAndLevel.TryGetValue((0, checked((int)character.Level)), out var currentLevel) ||
            currentLevel.Experience <= 0 ||
            currentLevel.UpLevTime < 1)
        {
            return false;
        }

        var levelTime = (double)character.Experience * currentLevel.UpLevTime / currentLevel.Experience;
        levelTime += amount;

        var targetLevel = character.Level;
        var targetLevelTime = currentLevel.UpLevTime;
        while (targetLevel < LegacyMaxPlayerLevel && levelTime >= targetLevelTime)
        {
            levelTime -= targetLevelTime;
            targetLevel++;
            if (targetLevel >= LegacyMaxPlayerLevel)
            {
                break;
            }

            if (!worldData.LevelExperienceByTypeAndLevel.TryGetValue((0, checked((int)targetLevel)), out var nextLevel) ||
                nextLevel.UpLevTime < 1)
            {
                return false;
            }

            targetLevelTime = nextLevel.UpLevTime;
        }

        if (targetLevel >= LegacyMaxPlayerLevel)
        {
            updatedLevel = LegacyMaxPlayerLevel;
            updatedExperience = 0;
            return true;
        }

        if (!worldData.LevelExperienceByTypeAndLevel.TryGetValue((0, checked((int)targetLevel)), out var targetLevelData) ||
            targetLevelData.Experience <= 0 ||
            targetLevelData.UpLevTime < 1)
        {
            return false;
        }

        updatedLevel = targetLevel;
        updatedExperience = checked((long)(ulong)(levelTime / targetLevelTime * targetLevelData.Experience));
        return true;
    }













    private static async ValueTask TryPersistLegacyItemUsageMutationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string operation,
        IReadOnlyDictionary<string, string>? characterValues,
        bool persistItems)
    {
        if (characterValues is { Count: > 0 })
        {
            await TryPersistLegacyItemUsageCharacterAsync(context, session, operation, characterValues)
                .ConfigureAwait(false);
        }

        if (persistItems)
        {
            await TryPersistLegacyItemUsageItemsAsync(context, session, operation).ConfigureAwait(false);
        }
    }

    private static async ValueTask TryPersistLegacyItemUsageCharacterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string operation,
        IReadOnlyDictionary<string, string> values)
    {
        if (!TryPersistLegacyCharacterAttributes(session.UsersRoot, session.AccountId, values, out var characterPath, out var reason))
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP MsgItemUsage {operation} character persistence " +
                    $"{FormatSession(context)} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                $"WorldServer persisted MsgItemUsage {operation} character " +
                $"{FormatSession(context)} path={Path.GetFileName(characterPath)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask TryPersistLegacyItemUsageItemsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string operation)
    {
        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            try
            {
                new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                    session.Character.Uid,
                    session.PlayerItems);
                await context.Log.WriteLineAsync(
                        $"WorldServer persisted MsgItemUsage {operation} items " +
                        $"{FormatSession(context)} path={session.Character.Uid.ToString(CultureInfo.InvariantCulture)}.bin.")
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        $"PARITY-GAP MsgItemUsage {operation} item persistence " +
                        $"{FormatSession(context)} reason=itemSnapshotSaveFailed exception={ex.GetType().Name}.")
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP MsgItemUsage {operation} item persistence " +
                    $"{FormatSession(context)} reason=playerItemsRootMissing.")
                .ConfigureAwait(false);
        }
    }





















    private static CharacterSnapshot AddLegacyStudyPoints(
        CharacterSnapshot character,
        ushort points)
    {
        var state = character.Attributes.TryGetValue("SubProfInfo", out var rawState) &&
            LegacySubClassHitpointsCalculator.TryParseLegacyState(rawState, out var parsed)
                ? parsed
                : new LegacySubClassState(StudyPoints: 0, ActiveSubClass: 0, Entries: []);
        var updatedPoints = checked((ushort)Math.Min(ushort.MaxValue, state.StudyPoints + points));
        var words = new List<string>
        {
            updatedPoints.ToString(CultureInfo.InvariantCulture),
            state.ActiveSubClass.ToString(CultureInfo.InvariantCulture),
            state.Entries.Count.ToString(CultureInfo.InvariantCulture),
        };
        foreach (var entry in state.Entries)
        {
            words.Add(entry.Id.ToString(CultureInfo.InvariantCulture));
            words.Add(entry.Level.ToString(CultureInfo.InvariantCulture));
            words.Add(entry.Phrase.ToString(CultureInfo.InvariantCulture));
        }

        return WithCharacterAttribute(character, "SubProfInfo", string.Join('#', words) + "#");
    }













































































































}
