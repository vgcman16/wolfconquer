using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyItemPackServicesIdealDesgnerNpcId = 19_436;
    private const uint LegacyItemPackServicesAstralPheonixNpcId = 19_452;

    private const uint LegacyItemPackServicesSpiritOfLegendItemId = 3_300_056;
    private const uint LegacyItemPackServicesSilverPhoenixFragmentItemId = 3_300_053;
    private const uint LegacyItemPackServicesGoldenPhoenixFragmentItemId = 3_300_054;
    private const uint LegacyItemPackServicesHolyPhoenixFragmentItemId = 3_300_055;
    private const ushort LegacyItemPackServicesPhoenixSpiritCost = 5;
    private const long LegacyItemPackServicesPhoenixConquerPointCost = 15_000;
    private const int LegacyItemPackServicesPhoenixStackSizeFieldIndex = 46;
    private const string RuntimeLegacyPhoenixCupRewardRollAttribute = "LegacyPhoenixCupRewardRoll";
    private const string RuntimeLegacyPhoenixCupRewardAmountAttribute = "LegacyPhoenixCupRewardAmount";

    private const ushort LegacyItemPackServicesAstralAvatarId = 4;
    private static async ValueTask<bool> LegacyItemPackServicesTryHandleIdealDesgnerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.IdealDesgner";
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "I can turn a Spirit of Legend into a random temporary Phoenix Cup or <tip color=0xfff05050 desc=A#certain#number#of#cup#fragments#can#be#combined#into#the#corresponding#Phoenix#Cup.>cup fragment</tip>.",
                            "~Equipping the Phoenix Cup will bring you attribute bonus. If you have any cup that you don`t need anymore, you can exchange it for <tip color=0xfff05050 desc=30#fragments#can#be#combined#into#a#90-day#Holy#Phoenix#Cup.>Holy Phoenix Cup Fragment</tip> or",
                            "~Chi Points.",
                        ],
                        [
                            new("Use~the~Spirit~of~Legend.", 1),
                            new("Swap~for~cup~fragment~or~Chi.", LegacyNpcClosedOption),
                            new("Learn~more~about~Phoenix~Cups.", 30),
                            new("I`ll~talk~to~you~later.", LegacyNpcClosedOption),
                        ],
                        LegacyItemPackServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                if (LegacyItemPackServicesHasStack(
                        session,
                        LegacyItemPackServicesSpiritOfLegendItemId,
                        LegacyItemPackServicesPhoenixSpiritCost))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            source,
                            ["f you really want the Phoenix Cup, you can also pay 5 Spirit of Legend to make a try."],
                            [
                                new("Deal!~Here~are~5xSpiritofLegend.", 5),
                                new("I`ll~think~about~it.", LegacyNpcClosedOption),
                            ],
                            LegacyItemPackServicesDefaultAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["It seems you don`t have 5x Spirit of Legend. If you really want the Phoenix Cup, you can also pay 15,000 CPs to make a try."],
                        [
                            new("Deal!~Here~are~15,000~CPs.", 2),
                            new("I`ll~think~about~it.", LegacyNpcClosedOption),
                        ],
                        LegacyItemPackServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 5:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["Are you sure you want to try with 5x Spirit of Legend?"],
                        [
                            new("Yes.~I`ll~pay~the~5xSpiritofLegend.", 6),
                            new("I`ll~think~about~it.", LegacyNpcClosedOption),
                        ],
                        LegacyItemPackServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 6:
                if (!HasLegacyInventorySpace(session.PlayerItems, 1))
                {
                    await LegacyItemPackServicesSendMessageBoxAsync(
                            context,
                            source,
                            "Please make 1 more space in your inventory.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (!LegacyItemPackServicesHasStack(
                        session,
                        LegacyItemPackServicesSpiritOfLegendItemId,
                        LegacyItemPackServicesPhoenixSpiritCost))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            source,
                            ["You should give me a Spirit of Legend or 15,000 CPs before trying to make a phoenix cup."],
                            [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                            LegacyItemPackServicesDefaultAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await LegacyItemPackServicesApplySpiritPhoenixCupAsync(
                        context,
                        session,
                        optionId,
                        source,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["Are you sure you want to try with 15,000 CPs instead of a Spirit of Legend?"],
                        [
                            new("Yes.~I`ll~pay~the~money.", 3),
                            new("I`ll~think~about~it.", LegacyNpcClosedOption),
                        ],
                        LegacyItemPackServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 3:
                if (!HasLegacyInventorySpace(session.PlayerItems, 1))
                {
                    await LegacyItemPackServicesSendMessageBoxAsync(
                            context,
                            source,
                            "Please make 1 more space in your inventory.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (session.Character.ConquerPoints < LegacyItemPackServicesPhoenixConquerPointCost)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            source,
                            ["You should give me a Spirit of Legend or 15,000 CPs before trying to make a phoenix cup."],
                            [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                            LegacyItemPackServicesDefaultAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (session.LegacyTradeActive)
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer ignored LegacyNpc ItemPackServices Ideal Designer paid Phoenix Cup attempt " +
                            $"{FormatSession(context)} source={source} sourceLines=22975-23009 optionId={optionId} " +
                            "reason=in-trade; source branch is silent.")
                        .ConfigureAwait(false);
                    return true;
                }

                await LegacyItemPackServicesApplyPaidPhoenixCupAsync(
                        context,
                        session,
                        optionId,
                        source,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 30:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "The Cross-server Elite PK Tournament will award 4 kinds of phoenix cups which grant different attributes.",
                            "~Which cup would you like to know about?",
                        ],
                        [
                            new("Holy~Phoenix~Cup.", 31),
                            new("Golden~Phoenix~Cup.", 32),
                            new("Silver~Phoenix~Cup.", 33),
                            new("Bronze~Phoenix~Cup.", 34),
                        ],
                        LegacyItemPackServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 31:
                await LegacyItemPackServicesSendPhoenixCupInfoAsync(
                        context,
                        source,
                        [
                            "Holy Phoenix Cup's basic attributes: 1% Blessed, HP +1200, MP +1200, Defense +1000, \n",
                            "                  P-Strike +1%, M-Strike +1%, Break +1%,      Anti-break +1% \n",
                            "Bonus attributes if tempered: Lv.3 Toxin Eraser, Lv.5 Draining Touch, Lv.2 Blood Spawn, \n",
                            "                   Lv.5 Free Soul, Lv.5 Killing Flash, Lv.3 Straight Life, HP +500",
                        ],
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 32:
                await LegacyItemPackServicesSendPhoenixCupInfoAsync(
                        context,
                        source,
                        [
                            "Golden Phoenix Cup's basic attributes: 1% Blessed, HP +1000, MP +1000, Defense +500 \n",
                            "Bonus attributes if tempered: Lv.3 Toxin Eraser, Lv.4 Draining Touch, Lv.2 Blood Spawn, \n",
                            "                  Lv.4 Free Soul, Lv.4 Killing Flash, Lv.3 Straight Life, HP +300 \n",
                        ],
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 33:
                await LegacyItemPackServicesSendPhoenixCupInfoAsync(
                        context,
                        source,
                        [
                            "Silver Phoenix Cup's basic attributes: 1% Blessed, HP +900, MP +900, Defense +300 \n",
                            "Bonus attributes if tempered: Lv.3 Toxin Eraser, Lv.3 Draining Touch, Lv.1 Blood Spawn, \n",
                            "                  Lv.3 Free Soul, Lv.3 Killing Flash, Lv.2 Straight Life \n",
                        ],
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 34:
                await LegacyItemPackServicesSendPhoenixCupInfoAsync(
                        context,
                        source,
                        [
                            "Bronze Phoenix Cup's basic attributes: 1% Blessed, HP +800, MP +800, Defense +150 \n",
                            "Bonus attributes if tempered: Lv.3 Toxin Eraser, Lv.2 Draining Touch, Lv.2 Killing Flash,  Lv.2 Free Soul \n",
                        ],
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyItemPackServicesTryHandleAstralPheonixAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId != 0)
        {
            return false;
        }

        await SendLegacyNpcDialogAsync(
                context,
                "NpcHandler.AstralPheonix",
                [
                    "1.Join the Cross-Server Elite PK Tournament and you can get Elite Points. \n",
                    "2.Elite Points can be used to exchange for reward. \n",
                    "3.Top 8 players can get valuable reward such as exclusive halo and garments.",
                ],
                LegacyItemPackServicesCloseOptions,
                LegacyItemPackServicesAstralAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask LegacyItemPackServicesSendPhoenixCupInfoAsync(
        WorldPacketHandlerContext context,
        string source,
        IReadOnlyList<string> text,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                source,
                text,
                LegacyItemPackServicesCloseOptions,
                LegacyItemPackServicesDefaultAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask LegacyItemPackServicesApplyPaidPhoenixCupAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string source,
        CancellationToken cancellationToken)
    {
        const string sourceLines = "22975-23009";
        if (!LegacyItemPackServicesTryGetPhoenixInfrastructure(
                context,
                session,
                requireUsersRoot: true,
                out var sessions,
                out _,
                out var infrastructureReason))
        {
            await LegacyItemPackServicesLogPhoenixFenceAsync(
                    context,
                    optionId,
                    source,
                    sourceLines,
                    infrastructureReason + "; no CPs or items were mutated")
                .ConfigureAwait(false);
            return;
        }

        var rewardRoll = LegacyItemPackServicesReadPhoenixRewardRoll(session.Character);
        var rewardAmount = LegacyItemPackServicesReadPhoenixRewardAmount(session.Character);
        var rewardItemId = LegacyItemPackServicesResolvePhoenixRewardItemId(rewardRoll);
        if (!LegacyItemPackServicesTryAddPhoenixFragmentReward(
                context.WorldData,
                session.PlayerItems,
                rewardItemId,
                rewardAmount,
                out var updatedItems,
                out var rewardItem,
                out var rewardMode,
                out var rewardReason))
        {
            await LegacyItemPackServicesLogPhoenixFenceAsync(
                    context,
                    optionId,
                    source,
                    sourceLines,
                    rewardReason + "; no CPs or items were mutated")
                .ConfigureAwait(false);
            return;
        }

        var updatedConquerPoints = checked(
            session.Character.ConquerPoints - LegacyItemPackServicesPhoenixConquerPointCost);
        var updatedCharacter = WithCharacterAttribute(
            session.Character with { ConquerPoints = updatedConquerPoints },
            "ConquerPoints",
            checked((ulong)updatedConquerPoints));
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
        };

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                characterValues,
                persistItems: true,
                out var persistenceReason))
        {
            await LegacyItemPackServicesLogPhoenixFenceAsync(
                    context,
                    optionId,
                    source,
                    sourceLines,
                    persistenceReason + "; no CP, item, session, or packet mutation was committed")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)updatedConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(
                    LegacyItemPackServicesPhoenixRewardMessage(rewardItemId, rewardAmount)).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: rewardMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc ItemPackServices Ideal Designer paid Phoenix Cup attempt " +
                $"{FormatSession(updatedContext)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                $"cost={LegacyItemPackServicesPhoenixConquerPointCost} " +
                $"conquerPoints={session.Character.ConquerPoints}->{updatedConquerPoints} " +
                $"rewardRoll={rewardRoll} rewardItemId={rewardItemId} rewardAmount={rewardAmount} " +
                $"rewardUid={rewardItem.Uid} rewardMode={rewardMode} " +
                "sourceOrder=conquer-points+talk-message+add-item.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LegacyItemPackServicesApplySpiritPhoenixCupAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string source,
        CancellationToken cancellationToken)
    {
        const string sourceLines = "22931-22963";
        if (!LegacyItemPackServicesTryGetPhoenixInfrastructure(
                context,
                session,
                requireUsersRoot: false,
                out var sessions,
                out var playerItemsRoot,
                out var infrastructureReason))
        {
            await LegacyItemPackServicesLogPhoenixFenceAsync(
                    context,
                    optionId,
                    source,
                    sourceLines,
                    infrastructureReason + "; no Spirit of Legend items were consumed")
                .ConfigureAwait(false);
            return;
        }

        if (!LegacyItemPackServicesTryConsumePhoenixSpirits(
                session.PlayerItems,
                out var consumedItems,
                out var consumeFrames))
        {
            await LegacyItemPackServicesLogPhoenixFenceAsync(
                    context,
                    optionId,
                    source,
                    sourceLines,
                    "Spirit of Legend count drifted after the unbound gate; no mutation was committed")
                .ConfigureAwait(false);
            return;
        }

        var rewardRoll = LegacyItemPackServicesReadPhoenixRewardRoll(session.Character);
        var rewardAmount = LegacyItemPackServicesReadPhoenixRewardAmount(session.Character);
        var rewardItemId = LegacyItemPackServicesResolvePhoenixRewardItemId(rewardRoll);
        if (!LegacyItemPackServicesTryAddPhoenixFragmentReward(
                context.WorldData,
                consumedItems,
                rewardItemId,
                rewardAmount,
                out var updatedItems,
                out var rewardItem,
                out var rewardMode,
                out var rewardReason))
        {
            await LegacyItemPackServicesLogPhoenixFenceAsync(
                    context,
                    optionId,
                    source,
                    sourceLines,
                    rewardReason + "; no Spirit of Legend items were consumed")
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { PlayerItems = updatedItems };
        cancellationToken.ThrowIfCancellationRequested();
        if (!LegacyItemPackServicesTryPersistPhoenixItems(
                playerItemsRoot,
                updatedSession.Character.Uid,
                updatedItems,
                out var persistenceReason))
        {
            await LegacyItemPackServicesLogPhoenixFenceAsync(
                    context,
                    optionId,
                    source,
                    sourceLines,
                    persistenceReason + "; no session mutation or packets were committed")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        foreach (var consumeFrame in consumeFrames)
        {
            await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(
                    LegacyItemPackServicesPhoenixRewardMessage(rewardItemId, rewardAmount)).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: rewardMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc ItemPackServices Ideal Designer Spirit Phoenix Cup attempt " +
                $"{FormatSession(updatedContext)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                $"consumePackets={consumeFrames.Count} rewardRoll={rewardRoll} rewardItemId={rewardItemId} " +
                $"rewardAmount={rewardAmount} rewardUid={rewardItem.Uid} rewardMode={rewardMode} " +
                "sourceOrder=remove-items+talk-message+add-item.")
            .ConfigureAwait(false);
    }

    private static bool LegacyItemPackServicesTryGetPhoenixInfrastructure(
        WorldPacketHandlerContext context,
        WorldSession session,
        bool requireUsersRoot,
        out WorldSessionStore sessions,
        out string playerItemsRoot,
        out string reason)
    {
        sessions = null!;
        playerItemsRoot = string.Empty;
        reason = string.Empty;
        if (context.Sessions is not { } availableSessions)
        {
            reason = "session-store-missing";
            return false;
        }

        if (!availableSessions.TryGet(context.Connection, out var registered) ||
            registered is null ||
            !ReferenceEquals(registered, session))
        {
            reason = "session-registration-missing-or-stale";
            return false;
        }

        if (session.Character.Uid <= 0)
        {
            reason = "character-uid-unavailable";
            return false;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            reason = "player-items-root-missing";
            return false;
        }

        if (requireUsersRoot && string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            reason = "users-root-missing";
            return false;
        }

        sessions = availableSessions;
        playerItemsRoot = context.PlayerItemsRoot;
        return true;
    }

    private static bool LegacyItemPackServicesTryConsumePhoenixSpirits(
        PlayerItemSnapshot items,
        out PlayerItemSnapshot consumedItems,
        out IReadOnlyList<byte[]> consumeFrames)
    {
        consumedItems = items;
        var frames = new List<byte[]>(LegacyItemPackServicesPhoenixSpiritCost);
        for (var pass = 0; pass < LegacyItemPackServicesPhoenixSpiritCost; pass++)
        {
            var passTargets = consumedItems.InventoryItems
                .Where(item => item.ItemId == LegacyItemPackServicesSpiritOfLegendItemId &&
                    item.Bound == 0 && item.StackSize > 0)
                .ToArray();
            foreach (var passTarget in passTargets)
            {
                var currentTarget = consumedItems.InventoryItems.FirstOrDefault(
                    item => item.Uid == passTarget.Uid &&
                        item.ItemId == LegacyItemPackServicesSpiritOfLegendItemId &&
                        item.Bound == 0 && item.StackSize > 0);
                if (currentTarget is null)
                {
                    continue;
                }

                consumedItems = ConsumeSingleLegacyInventoryItem(
                    consumedItems,
                    currentTarget,
                    out var consumeFrame);
                frames.Add(consumeFrame);
                if (frames.Count == LegacyItemPackServicesPhoenixSpiritCost)
                {
                    consumeFrames = frames;
                    return true;
                }
            }
        }

        consumeFrames = [];
        consumedItems = items;
        return false;
    }

    private static bool LegacyItemPackServicesTryPersistPhoenixItems(
        string playerItemsRoot,
        long characterUid,
        PlayerItemSnapshot items,
        out string reason)
    {
        reason = string.Empty;
        var transactionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var itemPath = Path.Combine(
            playerItemsRoot,
            characterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var itemTempPath = itemPath + ".phoenix-" + transactionId + ".tmp";
        var itemBackupPath = itemPath + ".phoenix-" + transactionId + ".bak";
        var itemExisted = File.Exists(itemPath);
        var committed = false;

        try
        {
            Directory.CreateDirectory(playerItemsRoot);
            var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(items);
            using (var stream = new FileStream(
                       itemTempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4_096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (itemExisted)
            {
                File.Replace(itemTempPath, itemPath, itemBackupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(itemTempPath, itemPath);
            }

            committed = true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            System.Security.SecurityException)
        {
            var restoreRequired = File.Exists(itemBackupPath) ||
                (!itemExisted && !committed && File.Exists(itemPath));
            var restored = !restoreRequired ||
                LegacyItemPackServicesTryRestorePhoenixItems(itemBackupPath, itemPath, itemExisted);
            reason = restored
                ? "item-persistence-" + ex.GetType().Name
                : "item-persistence-rollback-failed:" + ex.GetType().Name;
            return false;
        }
        finally
        {
            LegacyItemPackServicesTryDeletePhoenixPersistenceFile(itemTempPath);
        }

        LegacyItemPackServicesTryDeletePhoenixPersistenceFile(itemBackupPath);
        return true;
    }

    private static bool LegacyItemPackServicesTryRestorePhoenixItems(
        string backupPath,
        string targetPath,
        bool existed)
    {
        try
        {
            if (!existed)
            {
                LegacyItemPackServicesTryDeletePhoenixPersistenceFile(targetPath);
                return true;
            }

            if (!File.Exists(backupPath))
            {
                return false;
            }

            if (File.Exists(targetPath))
            {
                File.Replace(backupPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(backupPath, targetPath);
            }

            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            System.Security.SecurityException)
        {
            return false;
        }
    }

    private static void LegacyItemPackServicesTryDeletePhoenixPersistenceFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            System.Security.SecurityException)
        {
        }
    }

    private static bool LegacyItemPackServicesTryAddPhoenixFragmentReward(
        WorldDataCatalog? worldData,
        PlayerItemSnapshot items,
        uint rewardItemId,
        ushort amount,
        out PlayerItemSnapshot updatedItems,
        out PlayerItemSnapshotItem rewardItem,
        out ushort rewardMode,
        out string reason)
    {
        updatedItems = items;
        rewardItem = null!;
        rewardMode = 0;
        reason = string.Empty;
        if (!TryReadLegacyItemType(worldData, rewardItemId, out var rewardItemType))
        {
            reason = $"reward-itemtype-missing:itemId={rewardItemId}";
            return false;
        }

        if (rewardItemType.Id != checked((int)rewardItemId) ||
            rewardItemType.LegacyDurability > ushort.MaxValue ||
            rewardItemType.Fields.Count <= LegacyItemPackServicesPhoenixStackSizeFieldIndex ||
            !ushort.TryParse(
                rewardItemType.Fields[LegacyItemPackServicesPhoenixStackSizeFieldIndex],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var stackLimit) ||
            stackLimit == 0 ||
            amount == 0 ||
            amount > stackLimit)
        {
            reason = $"reward-itemtype-invalid:itemId={rewardItemId}";
            return false;
        }

        var compatibleStack = items.InventoryItems.FirstOrDefault(item =>
            item.ItemId == rewardItemId &&
            item.Bound == 0 &&
            (uint)item.StackSize + amount <= stackLimit);
        if (compatibleStack is not null)
        {
            rewardItem = compatibleStack
                .WithStackSize(checked((ushort)(compatibleStack.StackSize + amount)))
                .WithLocked(0);
            rewardMode = LegacyGameItemPacket.UpdateItemMode;
            updatedItems = ReplaceInventoryItem(items, compatibleStack, rewardItem);
            return true;
        }

        if (!HasLegacyInventorySpace(items, 1))
        {
            reason = "inventory-full-before-reward";
            return false;
        }

        try
        {
            rewardItem = CreatePurchasedItems(
                    items,
                    rewardItemType,
                    count: 1,
                    isBound: false)[0]
                .WithStackSize(amount)
                .WithColor(checked((uint)Random.Shared.Next(3, 9)));
        }
        catch (OverflowException)
        {
            reason = $"item-uid-exhausted:itemId={rewardItemId}";
            return false;
        }

        rewardMode = LegacyGameItemPacket.AddItemMode;
        updatedItems = items with
        {
            InventoryItems = items.InventoryItems
                .Concat([rewardItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        return true;
    }

    private static byte LegacyItemPackServicesReadPhoenixRewardRoll(CharacterSnapshot character)
    {
        if (TryReadUInt32Attribute(
                character.Attributes,
                out var forcedRoll,
                RuntimeLegacyPhoenixCupRewardRollAttribute))
        {
            return forcedRoll is 1 or 2
                ? checked((byte)forcedRoll)
                : checked((byte)(forcedRoll % 2 + 1));
        }

        return checked((byte)Random.Shared.Next(1, 3));
    }

    private static ushort LegacyItemPackServicesReadPhoenixRewardAmount(CharacterSnapshot character)
    {
        if (TryReadUInt32Attribute(
                character.Attributes,
                out var forcedAmount,
                RuntimeLegacyPhoenixCupRewardAmountAttribute))
        {
            return forcedAmount is 1 or 2
                ? checked((ushort)forcedAmount)
                : checked((ushort)(forcedAmount % 2 + 1));
        }

        return checked((ushort)Random.Shared.Next(1, 3));
    }

    private static uint LegacyItemPackServicesResolvePhoenixRewardItemId(byte rewardRoll)
    {
        return rewardRoll switch
        {
            1 => LegacyItemPackServicesSilverPhoenixFragmentItemId,
            2 => LegacyItemPackServicesGoldenPhoenixFragmentItemId,
            _ => LegacyItemPackServicesHolyPhoenixFragmentItemId,
        };
    }

    private static string LegacyItemPackServicesPhoenixRewardMessage(uint rewardItemId, ushort amount)
    {
        var itemName = rewardItemId switch
        {
            LegacyItemPackServicesSilverPhoenixFragmentItemId => "SilverPhoenixCupFragment",
            LegacyItemPackServicesGoldenPhoenixFragmentItemId => "GoldenPhoenixCupFragment",
            _ => "HolyPhoenixCupFragment",
        };
        return $"You received {itemName}*{amount}!";
    }

    private static async ValueTask LegacyItemPackServicesLogPhoenixFenceAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string source,
        string sourceLines,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc ItemPackServices Ideal Designer Phoenix Cup reward " +
                $"{FormatSession(context)} source={source} sourceLines={sourceLines} " +
                $"npcId={LegacyItemPackServicesIdealDesgnerNpcId} optionId={optionId} reason={reason}.")
            .ConfigureAwait(false);
    }
}
