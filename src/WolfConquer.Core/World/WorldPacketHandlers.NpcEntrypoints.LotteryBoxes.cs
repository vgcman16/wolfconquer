using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyLotteryBoxesSourceMethod = "NpcHandler.LotteryBoxes";
    private const uint LegacyLotteryBoxesHeroRewardGoalId = 306;
    private const byte LegacyLotteryBoxesMinimumColor = 4;
    private const byte LegacyLotteryBoxesMaximumColorExclusive = 8;
    private const byte LegacyLotteryBoxesEmptySocket = byte.MaxValue;
    private const string RuntimeLegacyLotteryBoxesRankAttribute = "LegacyLotteryRank";
    private const string RuntimeLegacyLotteryBoxesPrizeIndexAttribute = "LegacyLotteryPrizeIndex";
    private const string RuntimeLegacyLotteryBoxesColorAttribute = "LegacyLotteryColor";

    private static readonly LegacyLotteryRankCadence LegacyLotteryBoxesRankCadence = new();

    private static async ValueTask<bool> TryHandleLegacyLotteryBoxesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyLotteryBoxNpc(npcId))
        {
            return false;
        }

        if (session.Character.MapId != LegacyLotteryBoxMapId ||
            ReadAttributeUInt64(session.Character.Attributes, "DynamicID", "DynamicId") != 0)
        {
            return false;
        }

        if (optionId is not (0 or 1))
        {
            return true;
        }

        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc LotteryBoxes draw fenced " +
                    $"{FormatSession(context)} source={LegacyLotteryBoxesSourceMethod} npcId={npcId} option={optionId} " +
                    "reason=sessionStoreMissing; the entire draw was preserved without persistence, session mutation, or packets.")
                .ConfigureAwait(false);
            return true;
        }

        var lotteryEntries = ReadAttributeUInt64(session.Character.Attributes, "LotteryEntries");
        if (lotteryEntries >= LegacyLotteryEntryLimit)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyLotteryBoxesSourceMethod,
                    ["Sorry, your lottery entries are finished. Please come back tomorrow."],
                    [new LegacyNpcDialogOption("Oops.", LegacyNpcClosedOption)],
                    avatarId: 0,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventoryStackCount(
                session.PlayerItems,
                LegacyLotterySmallTicketItemId,
                LegacyLotterySmallTicketCost))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyLotteryBoxesSourceMethod,
                    ["You need to pay 3 Small Lottery Tickets to draw from the lottery in the Lottery Center."],
                    [new LegacyNpcDialogOption("Oops.", LegacyNpcClosedOption)],
                    avatarId: 0,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer fenced LegacyNpc LotteryBoxes draw " +
                    $"{FormatSession(context)} source={LegacyLotteryBoxesSourceMethod} npcId={npcId} option={optionId}; " +
                    "inventory is full, so tickets and lottery state were preserved.")
                .ConfigureAwait(false);
            return true;
        }

        if (!TryGenerateLegacyLotteryPrize(
                context.WorldData,
                session.Character,
                out var generatedPrize,
                out var generationReason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc LotteryBoxes reward generation fenced " +
                    $"{FormatSession(context)} source={LegacyLotteryBoxesSourceMethod} npcId={npcId} option={optionId} " +
                    $"reason={generationReason}; tickets and lottery state were preserved.")
                .ConfigureAwait(false);
            return true;
        }

        var consumeFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            LegacyLotterySmallTicketItemId,
            LegacyLotterySmallTicketCost,
            consumeFrames);
        // The source stores GenerateLotteryItem in Player.LotteryItem; this bounded slice materializes that row immediately.
        var prizeItem = CreateLegacyLotteryPrizeItem(session.PlayerItems, generatedPrize);
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([prizeItem]).ToArray(),
        };
        var updatedEntries = checked(lotteryEntries + 1);
        var updatedSession = AddLegacyHeroRewardGoal(session, LegacyLotteryBoxesHeroRewardGoalId);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(updatedSession.Character, "LotteryEntries", updatedEntries),
            "AddJade",
            0UL);
        updatedSession = updatedSession with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyLotteryBoxesState(context, updatedSession, out var persistenceReason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc LotteryBoxes transaction fenced " +
                    $"{FormatSession(context)} source={LegacyLotteryBoxesSourceMethod} npcId={npcId} option={optionId} " +
                    $"reason={persistenceReason}; staged ticket, prize, entry, jade, and HeroReward mutations were discarded.")
                .ConfigureAwait(false);
            return true;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        foreach (var consumeFrame in consumeFrames)
        {
            await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(prizeItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyStringPacket.PlayerEffect(npcId, "lottery").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc LotteryBoxes draw " +
                $"{FormatSession(updatedContext)} source={LegacyLotteryBoxesSourceMethod} npcId={npcId} " +
                $"option={optionId} ticketItemId={LegacyLotterySmallTicketItemId} ticketCost={LegacyLotterySmallTicketCost} " +
                $"lotteryEntries={updatedEntries} heroRewardGoal={LegacyLotteryBoxesHeroRewardGoalId} " +
                $"rank={generatedPrize.Rank} sourcePoolIndex={generatedPrize.SourcePoolIndex} " +
                $"eligiblePoolCount={generatedPrize.EligiblePoolCount} itemId={prizeItem.ItemId} itemUid={prizeItem.Uid} " +
                $"color={prizeItem.Color} sockets={generatedPrize.SocketCount} plus={prizeItem.Plus} " +
                $"sourceFile={generatedPrize.Prize.SourceFile} sourceLine={generatedPrize.Prize.LineNumber}; " +
                "materialization=bounded-local-immediate-grant; decompiled NpcHandler.LotteryBoxes only assigns Player.LotteryItem " +
                "and decompiled LotteryTable.CreateGameItem is otherwise unused, so this materializes GenerateLotteryItem data " +
                "rather than reproducing an exact observed source call.")
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc LotteryBoxes remaining side effects " +
                $"{FormatSession(updatedContext)} source={LegacyLotteryBoxesSourceMethod} " +
                "gap=Achievement.CreateFlaID(10118) and Activeness.IncreaseTask(4,16,28) are not modeled; " +
                "source BaseFunc.RandGet real-random seeding is represented by Random.Shared with deterministic character-attribute overrides.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyLotteryBoxNpc(uint npcId)
    {
        return npcId is >= LegacyLotteryBoxFirstNpcId and <= LegacyLotteryBoxLastNpcId;
    }

    private static bool TryGenerateLegacyLotteryPrize(
        WorldDataCatalog? worldData,
        CharacterSnapshot character,
        out LegacyGeneratedLotteryPrize generated,
        out string reason)
    {
        generated = default;
        reason = string.Empty;
        if (worldData is null)
        {
            reason = "worldDataMissing";
            return false;
        }

        var rank = SelectLegacyLotteryRank(character);
        if (!worldData.LotteryPrizesByType.TryGetValue(rank, out var sourcePool) || sourcePool.Count == 0)
        {
            reason = $"lotteryRank{rank}Missing";
            return false;
        }

        var eligible = new List<LegacyLotteryPrizeCandidate>(sourcePool.Count);
        for (var sourceIndex = 0; sourceIndex < sourcePool.Count; sourceIndex++)
        {
            if (TryCreateLegacyLotteryPrizeCandidate(
                    worldData,
                    sourcePool[sourceIndex],
                    sourceIndex,
                    out var candidate))
            {
                eligible.Add(candidate);
            }
        }

        if (eligible.Count == 0)
        {
            reason = $"lotteryRank{rank}HasNoEligibleItemType";
            return false;
        }

        var eligibleIndex = SelectLegacyLotteryPrizeIndex(character, eligible.Count);
        var selected = eligible[eligibleIndex];
        generated = new LegacyGeneratedLotteryPrize(
            selected.Prize,
            selected.ItemType,
            rank,
            selected.SourcePoolIndex,
            eligible.Count,
            selected.SocketCount,
            selected.Plus,
            SelectLegacyLotteryColor(character));
        return true;
    }

    private static bool TryCreateLegacyLotteryPrizeCandidate(
        WorldDataCatalog worldData,
        LotteryPrize prize,
        int sourcePoolIndex,
        out LegacyLotteryPrizeCandidate candidate)
    {
        candidate = default;
        if (prize.ItemTypeId is not { } itemTypeId ||
            itemTypeId <= 0 ||
            !worldData.ItemsById.TryGetValue(itemTypeId, out var itemTypes) ||
            itemTypes.Count == 0)
        {
            return false;
        }

        var socketCount = prize.SocketTwo ?? 0;
        var plus = prize.PlusLevel ?? 0;
        var itemType = itemTypes[0];
        if (socketCount is < 0 or > 2 ||
            plus is < byte.MinValue or > byte.MaxValue ||
            itemType.LegacyDurability > ushort.MaxValue)
        {
            return false;
        }

        candidate = new LegacyLotteryPrizeCandidate(
            prize,
            itemType,
            sourcePoolIndex,
            checked((byte)socketCount),
            checked((byte)plus));
        return true;
    }

    private static int SelectLegacyLotteryRank(CharacterSnapshot character)
    {
        if (TryReadInt64Attribute(
                character.Attributes,
                RuntimeLegacyLotteryBoxesRankAttribute,
                out var forcedRank))
        {
            return checked((int)PositiveModulo(forcedRank - 1, 6) + 1);
        }

        return LegacyLotteryBoxesRankCadence.NextRank();
    }

    private static int SelectLegacyLotteryPrizeIndex(CharacterSnapshot character, int poolCount)
    {
        if (TryReadInt64Attribute(
                character.Attributes,
                RuntimeLegacyLotteryBoxesPrizeIndexAttribute,
                out var forcedIndex))
        {
            return checked((int)PositiveModulo(forcedIndex, poolCount));
        }

        return poolCount == 1 ? 0 : Random.Shared.Next(0, poolCount);
    }

    private static byte SelectLegacyLotteryColor(CharacterSnapshot character)
    {
        var colorCount = LegacyLotteryBoxesMaximumColorExclusive - LegacyLotteryBoxesMinimumColor;
        if (TryReadInt64Attribute(
                character.Attributes,
                RuntimeLegacyLotteryBoxesColorAttribute,
                out var forcedColor))
        {
            return checked((byte)(
                LegacyLotteryBoxesMinimumColor +
                PositiveModulo(forcedColor - LegacyLotteryBoxesMinimumColor, colorCount)));
        }

        return checked((byte)Random.Shared.Next(
            LegacyLotteryBoxesMinimumColor,
            LegacyLotteryBoxesMaximumColorExclusive));
    }

    private static PlayerItemSnapshotItem CreateLegacyLotteryPrizeItem(
        PlayerItemSnapshot items,
        LegacyGeneratedLotteryPrize generated)
    {
        var durability = checked((ushort)generated.ItemType.LegacyDurability);
        return new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(items),
            ItemId = checked((uint)generated.ItemType.Id),
            Durability = durability,
            MaximumDurability = durability,
            Position = 0,
            SocketOne = generated.SocketCount > 0 ? LegacyLotteryBoxesEmptySocket : (byte)0,
            SocketTwo = generated.SocketCount > 1 ? LegacyLotteryBoxesEmptySocket : (byte)0,
            Plus = generated.Plus,
            StackSize = 1,
            Color = generated.Color,
        };
    }

    private static bool TryPersistLegacyLotteryBoxesState(
        WorldPacketHandlerContext context,
        WorldSession session,
        out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            reason = "usersRootMissing";
            return false;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            reason = "playerItemsRootMissing";
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LotteryEntries"] = ReadAttributeUInt64(session.Character.Attributes, "LotteryEntries")
                .ToString(CultureInfo.InvariantCulture),
            ["AddJade"] = "0",
            [RuntimeHeroRewardsAttribute] = session.Character.Attributes[RuntimeHeroRewardsAttribute],
        };

        var characterPath = Path.Combine(
            session.UsersRoot,
            session.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        if (!File.Exists(characterPath))
        {
            reason = "snapshotMissing";
            return false;
        }

        string[] characterLines;
        try
        {
            characterLines = File.ReadAllLines(characterPath, LegacyCharacterSnapshotEncoding);
        }
        catch (IOException)
        {
            reason = "snapshotReadFailed";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            reason = "snapshotReadDenied";
            return false;
        }

        if (!TryValidateLegacyCharacterUid(characterLines, session.Character.Uid, out reason) ||
            !TryApplyLegacyCharacterAttributeLines(characterLines, values, out var updatedCharacterLines))
        {
            if (reason.Length == 0)
            {
                reason = "characterSectionMissing";
            }

            return false;
        }

        var itemPath = Path.Combine(
            context.PlayerItemsRoot,
            session.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var transactionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var characterTempPath = characterPath + ".lottery-" + transactionId + ".tmp";
        var characterBackupPath = characterPath + ".lottery-" + transactionId + ".bak";
        var itemTempPath = itemPath + ".lottery-" + transactionId + ".tmp";
        var itemBackupPath = itemPath + ".lottery-" + transactionId + ".bak";
        var itemExisted = File.Exists(itemPath);
        var characterCommitted = false;

        try
        {
            Directory.CreateDirectory(context.PlayerItemsRoot);
            File.WriteAllLines(characterTempPath, updatedCharacterLines, LegacyCharacterSnapshotEncoding);
            File.WriteAllBytes(itemTempPath, PlayerItemSnapshotLoader.ToLegacyBytes(session.PlayerItems));

            File.Replace(
                characterTempPath,
                characterPath,
                characterBackupPath,
                ignoreMetadataErrors: true);
            characterCommitted = true;

            if (itemExisted)
            {
                File.Replace(itemTempPath, itemPath, itemBackupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(itemTempPath, itemPath);
            }
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            var restored = !characterCommitted ||
                TryRestoreLegacyLotteryFile(characterBackupPath, characterPath, existed: true);
            reason = restored ? "transactionWriteFailed" : "transactionRollbackFailed";
            return false;
        }
        finally
        {
            TryDeleteLegacyLotteryFile(characterTempPath);
            TryDeleteLegacyLotteryFile(itemTempPath);
        }

        TryDeleteLegacyLotteryFile(characterBackupPath);
        TryDeleteLegacyLotteryFile(itemBackupPath);
        return true;
    }

    private static bool TryRestoreLegacyLotteryFile(string backupPath, string targetPath, bool existed)
    {
        try
        {
            if (!existed)
            {
                TryDeleteLegacyLotteryFile(targetPath);
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
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            return false;
        }
    }

    private static void TryDeleteLegacyLotteryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
        }
    }

    private readonly record struct LegacyLotteryPrizeCandidate(
        LotteryPrize Prize,
        ItemType ItemType,
        int SourcePoolIndex,
        byte SocketCount,
        byte Plus);

    private readonly record struct LegacyGeneratedLotteryPrize(
        LotteryPrize Prize,
        ItemType ItemType,
        int Rank,
        int SourcePoolIndex,
        int EligiblePoolCount,
        byte SocketCount,
        byte Plus,
        byte Color);

    private sealed class LegacyLotteryRankCadence
    {
        private static readonly int[] RankTicks = [600, 500, 300, 100, 40];

        private readonly int[] _counts = new int[RankTicks.Length];
        private readonly object _sync = new();

        public int NextRank()
        {
            lock (_sync)
            {
                for (var index = 0; index < RankTicks.Length; index++)
                {
                    _counts[index]++;
                    if (_counts[index] == RankTicks[index])
                    {
                        _counts[index] = 0;
                        return index + 1;
                    }
                }

                return 6;
            }
        }
    }
}
