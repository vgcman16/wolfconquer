using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap3071Family2700MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_071 ||
            monsterFamily.Id != 2_700)
        {
            return session;
        }

        const string sourceBranch = "MonsterRole.cs killer.Player.Map == 3071 && Family.ID == 2700";
        await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                context,
                session,
                monster,
                monsterFamily,
                itemId: 1_088_001,
                count: 3,
                scatterRange: 6,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                context,
                session,
                monster,
                monsterFamily,
                itemId: 723_341,
                count: 3,
                scatterRange: 6,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);

        var reward = SelectLegacyMap3071Family2700Reward(session.Character);
        if (reward.Kind == LegacyMap3071Family2700RewardKind.FloorDrop)
        {
            await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    reward.ItemId,
                    count: 1,
                    scatterRange: 6,
                    sourceBranch: $"{sourceBranch}; {reward.SourceDetail}",
                    cancellationToken)
                .ConfigureAwait(false);
            return session;
        }

        return await TryGrantLegacyMap3071CoinRewardAsync(
                context,
                session,
                monster,
                monsterFamily,
                LegacyMap3071CoinReward.Gold(reward.SourceDetail),
                $"{sourceBranch}; {reward.SourceDetail}",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap3071Family7022MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_071 ||
            monsterFamily.Id != 7_022)
        {
            return session;
        }

        const string sourceBranch = "MonsterRole.cs killer.Player.Map == 3071 && Family.ID == 7022";
        var reward = SelectLegacyMap3071Family7022Reward(session.Character);
        if (reward.Kind == LegacyMap3071CoinRewardKind.None)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 3071 family 7022 coin reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} map={monster.MapId}; " +
                    $"branch=\"{sourceBranch}; {reward.SourceDetail}\".")
                .ConfigureAwait(false);
            return session;
        }

        return await TryGrantLegacyMap3071CoinRewardAsync(
                context,
                session,
                monster,
                monsterFamily,
                reward,
                $"{sourceBranch}; {reward.SourceDetail}",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryGrantLegacyMap3071CoinRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        LegacyMap3071CoinReward reward,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (reward.Kind == LegacyMap3071CoinRewardKind.None)
        {
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("Your~inventory~is~full!").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 3071 coin reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} map={monster.MapId}; " +
                    $"itemId={reward.ItemId} inventory is full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, reward.ItemId, out var coinItemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map3071-coin-reward",
                        reason: "source grants a map 3071 monster-death coin reward, but the item type was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", reward.ItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", reward.ItemId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var coin = CreatePurchasedItems(session.PlayerItems, coinItemType, count: 1)[0];
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat([coin]).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(coin).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyStringPacket
                    .PlayerEffect(CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)), reward.EffectName)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        if (reward.MessageBox)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(reward.Message).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(reward.Message).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3071 coin reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"familyId={monsterFamily.Id} map={monster.MapId} itemId={reward.ItemId} " +
                $"effect={reward.EffectName} messageBox={reward.MessageBox} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static LegacyMap3071CoinReward SelectLegacyMap3071Family7022Reward(CharacterSnapshot character)
    {
        if (TryReadLegacyMap3071Family7022Reward(character, out var forcedReward))
        {
            return forcedReward;
        }

        if (!CheckLegacyMap3071Rate(1, 30))
        {
            return LegacyMap3071CoinReward.None("Core.Rate(1, 30)=false");
        }

        if (CheckLegacyMap3071Rate(1, 200))
        {
            return LegacyMap3071CoinReward.Gold("Core.Rate(1, 200) Inventory.Add(711609)");
        }

        if (CheckLegacyMap3071Rate(2, 199))
        {
            return LegacyMap3071CoinReward.Silver("Core.Rate(2, 199) Inventory.Add(711610)");
        }

        return LegacyMap3071CoinReward.Copper("fallback Inventory.Add(711611)");
    }

    private static bool TryReadLegacyMap3071Family7022Reward(
        CharacterSnapshot character,
        out LegacyMap3071CoinReward reward)
    {
        if (!character.Attributes.TryGetValue(RuntimeLegacyMap3071Family7022RewardBranchAttribute, out var raw))
        {
            reward = default;
            return false;
        }

        if (raw.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("skip", StringComparison.OrdinalIgnoreCase))
        {
            reward = LegacyMap3071CoinReward.None("forced Core.Rate(1, 30)=false");
            return true;
        }

        if (raw.Equals("gold", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("goldCoin", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("711609", StringComparison.OrdinalIgnoreCase))
        {
            reward = LegacyMap3071CoinReward.Gold("forced Core.Rate(1, 200) Inventory.Add(711609)");
            return true;
        }

        if (raw.Equals("silver", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("silverCoin", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("711610", StringComparison.OrdinalIgnoreCase))
        {
            reward = LegacyMap3071CoinReward.Silver("forced Core.Rate(2, 199) Inventory.Add(711610)");
            return true;
        }

        if (raw.Equals("copper", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("copperCoin", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("711611", StringComparison.OrdinalIgnoreCase))
        {
            reward = LegacyMap3071CoinReward.Copper("forced fallback Inventory.Add(711611)");
            return true;
        }

        reward = default;
        return false;
    }

    private static LegacyMap3071Family2700Reward SelectLegacyMap3071Family2700Reward(
        CharacterSnapshot character)
    {
        if (TryReadLegacyMap3071Family2700Reward(character, out var forcedReward))
        {
            return forcedReward;
        }

        foreach (var candidate in LegacyMap3071Family2700RewardLadder)
        {
            if (CheckLegacyMap3071Rate(candidate.RateNumerator, candidate.RateDenominator))
            {
                return candidate with
                {
                    SourceDetail =
                        FormattableString.Invariant(
                            $"Core.Rate({candidate.RateNumerator}, {candidate.RateDenominator}) DropItemID({candidate.ItemId})"),
                };
            }
        }

        return LegacyMap3071Family2700Reward.GoldCoin("fallback Inventory.Add(711609)");
    }

    private static bool TryReadLegacyMap3071Family2700Reward(
        CharacterSnapshot character,
        out LegacyMap3071Family2700Reward reward)
    {
        if (!character.Attributes.TryGetValue(RuntimeLegacyMap3071Family2700RewardBranchAttribute, out var raw))
        {
            reward = default;
            return false;
        }

        if (raw.Equals("gold", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("goldCoin", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("711609", StringComparison.OrdinalIgnoreCase))
        {
            reward = LegacyMap3071Family2700Reward.GoldCoin("forced Inventory.Add(711609)");
            return true;
        }

        if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId))
        {
            foreach (var candidate in LegacyMap3071Family2700RewardLadder)
            {
                if (candidate.ItemId == itemId)
                {
                    reward = candidate with
                    {
                        SourceDetail = FormattableString.Invariant($"forced DropItemID({itemId})"),
                    };
                    return true;
                }
            }
        }

        reward = default;
        return false;
    }

    private static bool CheckLegacyMap3071Rate(int numerator, int denominator)
    {
        return numerator > 0 &&
            denominator > 0 &&
            Random.Shared.Next(0, denominator) < numerator;
    }

    private static readonly LegacyMap3071Family2700Reward[] LegacyMap3071Family2700RewardLadder =
    [
        LegacyMap3071Family2700Reward.Floor(710_212, 5, 100, ""),
        LegacyMap3071Family2700Reward.Floor(720_128, 30, 100, ""),
        LegacyMap3071Family2700Reward.Floor(720_128, 30, 100, ""),
        LegacyMap3071Family2700Reward.Floor(728_917, 20, 100, ""),
        LegacyMap3071Family2700Reward.Floor(727_306, 20, 100, ""),
        LegacyMap3071Family2700Reward.Floor(728_918, 20, 100, ""),
        LegacyMap3071Family2700Reward.Floor(710_214, 10, 100, ""),
        LegacyMap3071Family2700Reward.Floor(4_200_005, 2, 100, ""),
        LegacyMap3071Family2700Reward.Floor(723_834, 20, 100, ""),
    ];

    private enum LegacyMap3071Family2700RewardKind
    {
        FloorDrop,
        GoldCoin,
    }

    private enum LegacyMap3071CoinRewardKind
    {
        None,
        Gold,
        Silver,
        Copper,
    }

    private readonly record struct LegacyMap3071CoinReward(
        LegacyMap3071CoinRewardKind Kind,
        uint ItemId,
        string EffectName,
        string Message,
        bool MessageBox,
        string SourceDetail)
    {
        public static LegacyMap3071CoinReward None(string sourceDetail)
        {
            return new LegacyMap3071CoinReward(
                LegacyMap3071CoinRewardKind.None,
                0,
                "",
                "",
                false,
                sourceDetail);
        }

        public static LegacyMap3071CoinReward Gold(string sourceDetail)
        {
            return new LegacyMap3071CoinReward(
                LegacyMap3071CoinRewardKind.Gold,
                711_609,
                "dispel4",
                "You~received~a~Gold~Coin!",
                true,
                sourceDetail);
        }

        public static LegacyMap3071CoinReward Silver(string sourceDetail)
        {
            return new LegacyMap3071CoinReward(
                LegacyMap3071CoinRewardKind.Silver,
                711_610,
                "dispel7",
                "You~received~a~Silver~Coin!",
                false,
                sourceDetail);
        }

        public static LegacyMap3071CoinReward Copper(string sourceDetail)
        {
            return new LegacyMap3071CoinReward(
                LegacyMap3071CoinRewardKind.Copper,
                711_611,
                "dispel5",
                "You~received~a~Copper~Coin!",
                false,
                sourceDetail);
        }
    }

    private readonly record struct LegacyMap3071Family2700Reward(
        LegacyMap3071Family2700RewardKind Kind,
        uint ItemId,
        int RateNumerator,
        int RateDenominator,
        string SourceDetail)
    {
        public static LegacyMap3071Family2700Reward Floor(
            uint itemId,
            int rateNumerator,
            int rateDenominator,
            string sourceDetail)
        {
            return new LegacyMap3071Family2700Reward(
                LegacyMap3071Family2700RewardKind.FloorDrop,
                itemId,
                rateNumerator,
                rateDenominator,
                sourceDetail);
        }

        public static LegacyMap3071Family2700Reward GoldCoin(string sourceDetail)
        {
            return new LegacyMap3071Family2700Reward(
                LegacyMap3071Family2700RewardKind.GoldCoin,
                711_609,
                0,
                0,
                sourceDetail);
        }
    }
}
