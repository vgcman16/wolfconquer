using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyHouseDemonMonsterDeathRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyHouseDemonRewardFamily(monsterFamily.Id, out var family) ||
            !IsLegacyHouseDemonOwnerInstance(session.Character))
        {
            return session;
        }

        const string sourceGate = "MonsterRole.cs killer.MyHouse != null && killer.Player.DynamicID == killer.Player.UID";
        var reward = SelectLegacyHouseDemonReward(session.Character, family);
        var sourceBranch = $"{sourceGate}; Family.ID == {family.FamilyId}; {reward.SourceDetail}";
        var sentCount = await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                context,
                session,
                monster,
                monsterFamily,
                reward.ItemId,
                count: 1,
                scatterRange: 3,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(reward.MessageBox).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (reward.GlobalMessageTemplate is not null)
        {
            await BroadcastLegacySystemRedCenterMessageAsync(
                    context,
                    FormatLegacyHouseDemonGlobalMessage(reward.GlobalMessageTemplate, session.Character.Name),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy house guest monster death reward " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"dynamicId={ReadCharacterDynamicId(session.Character)} itemId={reward.ItemId} sent={sentCount} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return session;
    }

    private static bool IsLegacyHouseDemonOwnerInstance(CharacterSnapshot character)
    {
        var dynamicId = ReadCharacterDynamicId(character);
        return dynamicId != 0 &&
            character.Uid >= 0 &&
            dynamicId == checked((ulong)character.Uid);
    }

    private static LegacyHouseDemonReward SelectLegacyHouseDemonReward(
        CharacterSnapshot character,
        LegacyHouseDemonRewardFamily family)
    {
        if (TryReadLegacyHouseDemonReward(character, family, out var forcedReward))
        {
            return forcedReward;
        }

        for (var index = 0; index < family.Rewards.Count - 1; index++)
        {
            var candidate = family.Rewards[index];
            if (CheckLegacyHouseDemonRate(candidate.RateNumerator, candidate.RateDenominator))
            {
                return candidate;
            }
        }

        return family.Rewards[family.Rewards.Count - 1];
    }

    private static bool TryReadLegacyHouseDemonReward(
        CharacterSnapshot character,
        LegacyHouseDemonRewardFamily family,
        out LegacyHouseDemonReward reward)
    {
        reward = default;
        if (!character.Attributes.TryGetValue(RuntimeLegacyHouseDemonRewardBranchAttribute, out var raw))
        {
            return false;
        }

        var key = raw.Trim().ToLowerInvariant();
        var forcedIndex = key switch
        {
            "first" or "top" or "rare" => 0,
            "second" => 1,
            "third" => 2,
            "fourth" => 3,
            "fifth" => 4,
            "fallback" or "last" or "pill" => family.Rewards.Count - 1,
            _ => -1,
        };

        if (forcedIndex >= 0 &&
            forcedIndex < family.Rewards.Count)
        {
            reward = family.Rewards[forcedIndex];
            return true;
        }

        if (key.StartsWith("item", StringComparison.Ordinal))
        {
            key = key["item".Length..];
        }

        if (!uint.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId))
        {
            return false;
        }

        foreach (var candidate in family.Rewards)
        {
            if (candidate.ItemId == itemId)
            {
                reward = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool CheckLegacyHouseDemonRate(int numerator, int denominator)
    {
        return numerator > 0 &&
            denominator > 0 &&
            Random.Shared.Next(0, denominator) < numerator;
    }

    private static string FormatLegacyHouseDemonGlobalMessage(string template, string playerName)
    {
        return template.Replace("{name}", playerName, StringComparison.Ordinal);
    }

    private static bool TryGetLegacyHouseDemonRewardFamily(
        int familyId,
        out LegacyHouseDemonRewardFamily family)
    {
        foreach (var candidate in LegacyHouseDemonRewardFamilies)
        {
            if (candidate.FamilyId == familyId)
            {
                family = candidate;
                return true;
            }
        }

        family = default;
        return false;
    }

    private static readonly LegacyHouseDemonRewardFamily[] LegacyHouseDemonRewardFamilies =
    [
        new(
            2_435,
            "Heaven Demon",
            [
                new(720_679, 1, 10_000, "You killed a Heaven Demon and found a Frost CP Pack (69000CPs)!", "{name} found a Frost CP Pack (69000CPs)!", "Core.Rate(1, 10000) DropItemID(720679)"),
                new(720_678, 10, 9_999, "You killed a Heaven Demon and found a Life CP Pack (13500CPs)!", "{name} found a Life CP Pack (13500CPs)!", "Core.Rate(10, 9999) DropItemID(720678)"),
                new(720_677, 3_700, 9_989, "You killed a Heaven Demon and found a Blood CP Pack (1000CPs)!", null, "Core.Rate(3700, 9989) DropItemID(720677)"),
                new(720_676, 1_289, 6_289, "You killed a Heaven Demon and found a Soul CP Pack (500CPs)!", null, "Core.Rate(1289, 6289) DropItemID(720676)"),
                new(720_675, 1_000, 5_000, "You killed a Heaven Demon and found a Ghost CP Pack (250CPs)!", null, "Core.Rate(1000, 5000) DropItemID(720675)"),
                new(720_680, 0, 0, "You killed a Heaven Demon and found a Heaven Pill equal to the EXP of 2 and a half EXP Balls!", null, "fallback DropItemID(720680)"),
            ]),
        new(
            2_436,
            "Chaos Demon",
            [
                new(720_685, 1, 10_000, "You killed a Chaos Demon and found a Nimbus CP Pack (138000CPs)!", "{name} found a Nimbus CP Pack (138000CPs)!", "Core.Rate(1, 10000) DropItemID(720685)"),
                new(720_684, 10, 9_999, "You killed a Chaos Demon and found a Butterfly CP Pack (27000CPs)!", "{name} found a Butterfly CP Pack (27000CPs)!", "Core.Rate(10, 9999) DropItemID(720684)"),
                new(720_683, 3_700, 9_989, "You killed a Chaos Demon and found a Heart CP Pack (2000CPs)!", null, "Core.Rate(3700, 9989) DropItemID(720683)"),
                new(720_682, 1_289, 6_289, "You killed a Chaos Demon and found a Flower CP Pack (1000CPs)!", null, "Core.Rate(1289, 6289) DropItemID(720682)"),
                new(720_681, 1_000, 5_000, "You killed a Chaos Demon and found a Deity CP Pack (500CPs)!", null, "Core.Rate(1000, 5000) DropItemID(720681)"),
                new(720_686, 0, 0, "You killed a Chaos Demon and found a Mystery Pill equal to the EXP of 2 and 1/3 EXP Balls!", null, "fallback DropItemID(720686)"),
            ]),
        new(
            2_437,
            "Sacred Demon",
            [
                new(720_691, 1, 10_000, "You killed a Sacred Demon and found a Kylin CP Pack (276000CPs)", "{name} found a Kylin CP Pack (276000CPs)!", "Core.Rate(1, 10000) DropItemID(720691)"),
                new(720_690, 10, 9_999, "You killed a Sacred Demon and found a Rainbow CP Pack (54000CPs)!", "{name} found a Rainbow CP Pack (54000CPs)!", "Core.Rate(10, 9999) DropItemID(720690)"),
                new(720_689, 3_700, 9_989, "You killed a Sacred Demon and found a Shadow CP Pack (4000CPs)!", null, "Core.Rate(3700, 9989) DropItemID(720689)"),
                new(720_688, 1_289, 6_289, "You killed a Sacred Demon and found a Jewel CP Pack (2000CPs)!", null, "Core.Rate(1289, 6289) DropItemID(720688)"),
                new(720_687, 1_000, 5_000, "You killed a Sacred Demon and found a Cloud CP Pack (1000CPs)!", null, "Core.Rate(1000, 5000) DropItemID(720687)"),
                new(720_692, 0, 0, "You killed a Sacred Demon and found a Wind Pill equal to the EXP of 5 EXP Balls!", null, "fallback DropItemID(720692)"),
            ]),
        new(
            2_438,
            "Aurora Demon",
            [
                new(720_697, 1, 10_000, "You killed an Aurora Demon and found a Pilgrim CP Pack (690000CPs)!", "{name} got a Pilgrim CP Pack (690000CPs)!", "Core.Rate(1, 10000) DropItemID(720697)"),
                new(720_696, 10, 9_999, "You killed an Aurora Demon and found a Zephyr CP Pack (135000CPs)!", "{name} found a Zephyr CP Pack (135000CPs)!", "Core.Rate(10, 9999) DropItemID(720696)"),
                new(720_695, 3_700, 9_989, "You killed an Aurora Demon and found an Earth CP Pack (10000CPs)!", "{name} found an Earth CP Pack (10000CPs)!", "Core.Rate(3700, 9989) DropItemID(720695)"),
                new(720_694, 1_289, 6_289, "You killed an Aurora Demon and found a Moon CP Pack (5000CPs)!", null, "Core.Rate(1289, 6289) DropItemID(720694)"),
                new(720_693, 1_000, 5_000, "You killed an Aurora Demon and found a Fog CP Pack (2500CPs)!", null, "Core.Rate(1000, 5000) DropItemID(720693)"),
                new(720_698, 0, 0, "You killed an Aurora Demon and got a Wind Pill equal to the EXP of 8 and 1/3 EXP Balls!", null, "fallback DropItemID(720698)"),
            ]),
        new(
            2_420,
            "Demon",
            [
                new(720_654, 1, 10_000, "You killed a Demon and found a Joy CP Pack (1380CPs)!", "{name} found a Joy CP Pack (1380CPs)!", "Core.Rate(1, 10000) DropItemID(720654)"),
                new(720_653, 10, 9_999, "You killed a Demon and found a Dream CP Pack (270CPs)!", null, "Core.Rate(10, 9999) DropItemID(720653)"),
                new(720_655, 3_700, 9_989, "You killed a Demon and found a Mammon CP Pack (20CPs)!", null, "Core.Rate(3700, 9989) DropItemID(720655)"),
                new(720_656, 1_289, 6_289, "You killed a Demon and found a Mascot CP Pack (10CPs)!", null, "Core.Rate(1289, 6289) DropItemID(720656)"),
                new(720_657, 1_000, 5_000, "You killed a Demon and found a Hope CP Pack (5CPs)!", null, "Core.Rate(1000, 5000) DropItemID(720657)"),
                new(720_668, 0, 0, "You killed a Demon and found a Magic Ball equal to the EXP of 1/6 of an EXP Ball!", null, "fallback DropItemID(720668)"),
            ]),
        new(
            2_421,
            "Ancient Demon",
            [
                new(720_662, 1, 10_000, "You killed a Ancient Demon and found a Mystic CP Pack (6900CPs)!", "{name} found a Mystic CP Pack (6900CPs)!", "Core.Rate(1, 10000) DropItemID(720662)"),
                new(720_661, 10, 9_999, "You killed a Ancient Demon and found a Pure CP Pack (1350CPs)!", null, "Core.Rate(10, 9999) DropItemID(720661)"),
                new(720_660, 3_700, 9_989, "You killed a Ancient Demon and found a Legend CP Pack (100CPs)!", null, "Core.Rate(3700, 9989) DropItemID(720660)"),
                new(720_659, 1_289, 6_289, "You killed a Ancient Demon and found a Sweet CP Pack (50CPs)!", null, "Core.Rate(1289, 6289) DropItemID(720659)"),
                new(720_658, 1_000, 5_000, "You killed a Ancient Demon and found a Festival CP Pack (25CPs)!", null, "Core.Rate(1000, 5000) DropItemID(720658)"),
                new(720_669, 0, 0, "You killed the Ancient Demon and found a Super Ball equal to the EXP of 5/6 of an EXP Ball!", null, "fallback DropItemID(720669)"),
            ]),
        new(
            2_422,
            "Flood Demon",
            [
                new(720_667, 1, 10_000, "You killed a Flood Demon and found a Fantasy CP Pack (13800CPs)!", "{name} found a Fantasy CP Pack (13800CPs)!", "Core.Rate(1, 10000) DropItemID(720667)"),
                new(720_666, 10, 9_999, "You killed a Flood Demon and found a Star CP Pack (2700CPs)!", "{name} found a Star CP Pack (2700CPs)!", "Core.Rate(10, 9999) DropItemID(720666)"),
                new(720_664, 1_289, 6_289, "You killed a Flood Demon and found a Flare CP Pack (100CPs)!", null, "Core.Rate(1289, 6289) DropItemID(720664)"),
                new(720_663, 1_000, 5_000, "You killed a Flood Demon and found a Violet CP Pack (50CPs)!", null, "Core.Rate(1000, 5000) DropItemID(720663)"),
                new(720_670, 0, 0, "You killed the Flood Demon and found an Ultra Ball equal to EXP worth 1 and 2/3 EXP Balls!", null, "fallback DropItemID(720670)"),
            ]),
    ];

    private readonly record struct LegacyHouseDemonRewardFamily(
        int FamilyId,
        string SourceName,
        IReadOnlyList<LegacyHouseDemonReward> Rewards);

    private readonly record struct LegacyHouseDemonReward(
        uint ItemId,
        int RateNumerator,
        int RateDenominator,
        string MessageBox,
        string? GlobalMessageTemplate,
        string SourceDetail);
}
