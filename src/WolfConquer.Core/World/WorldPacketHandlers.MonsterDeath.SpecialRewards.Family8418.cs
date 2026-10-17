using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyFamily8418Map10342MonsterDeathRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const string sourceBranch = "MonsterRole.cs Family.ID == 8418 && killer.Player.Map == 10342";
        var reward = SelectLegacyFamily8418Reward(session.Character);
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };
        var applied = false;
        ulong? value = null;

        switch (reward.Kind)
        {
            case LegacyFamily8418RewardKind.FloorDrop:
                {
                    var sentCount = await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                            updatedContext,
                            updatedSession,
                            monster,
                            monsterFamily,
                            reward.ItemId,
                            count: 1,
                            scatterRange: 3,
                            sourceBranch: $"{sourceBranch}; {reward.SourceDetail}",
                            cancellationToken)
                        .ConfigureAwait(false);
                    applied = sentCount > 0;
                    value = reward.ItemId;
                    break;
                }

            case LegacyFamily8418RewardKind.InventoryStack:
                updatedSession = await TryGrantLegacyFamily8418InventoryRewardAsync(
                        updatedContext,
                        updatedSession,
                        reward.ItemId,
                        $"{sourceBranch}; {reward.SourceDetail}",
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedContext = updatedContext with { Session = updatedSession };
                applied = true;
                value = reward.ItemId;
                break;

            case LegacyFamily8418RewardKind.ClassExperience:
                {
                    var currentClassExperience = ReadAttributeUInt64(updatedSession.Character.Attributes, "ClassExperience");
                    var updatedClassExperience = checked(currentClassExperience + reward.Amount);
                    var updatedCharacter = WithCharacterAttribute(
                        updatedSession.Character,
                        "ClassExperience",
                        updatedClassExperience);
                    updatedSession = updatedSession with { Character = updatedCharacter };
                    context.Sessions?.Set(context.Connection, updatedSession);
                    updatedContext = updatedContext with { Session = updatedSession };

                    await SendScalarUpdateAsync(
                            updatedContext,
                            updatedCharacter,
                            LegacyMsgUpdateDataType.ClassExperience,
                            updatedClassExperience,
                            cancellationToken)
                        .ConfigureAwait(false);
                    applied = true;
                    value = updatedClassExperience;
                    break;
                }
        }

        if (reward.SourceMessage is not null)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(
                            reward.SourceMessage.Replace("{name}", updatedSession.Character.Name, StringComparison.Ordinal))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy family 8418 map 10342 monster death reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} map={monster.MapId} " +
                $"reward={reward.Kind} itemId={reward.ItemId} applied={applied} value={value?.ToString(CultureInfo.InvariantCulture) ?? "none"} " +
                $"branch=\"{sourceBranch}; {reward.SourceDetail}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryGrantLegacyFamily8418InventoryRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint itemId,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "family-8418-map-10342-inventory-reward-item",
                        reason: "source grants a family 8418 map 10342 inventory reward on monster death, but the item type was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", session.Character.MapId),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                            LegacyParityGapDiagnostics.Fact("branch", sourceBranch),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                            LegacyParityGapDiagnostics.Fact("branch", sourceBranch),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var updatedItems = AddOrStackLegacyInventoryItem(
            session.PlayerItems,
            itemType,
            itemId,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        rewardItem,
                        LegacyGameItemPacket.CreateRuneSpellOverride(rewardItem, context.WorldData?.ItemsById),
                        itemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy family 8418 map 10342 inventory reward " +
                $"{FormatSession(context with { Session = updatedSession })} itemId={itemId} stack={rewardItem.StackSize} " +
                $"mode={itemMode} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static LegacyFamily8418Reward SelectLegacyFamily8418Reward(CharacterSnapshot character)
    {
        if (TryReadLegacyFamily8418Reward(character, out var forcedReward))
        {
            return forcedReward;
        }

        if (CheckLegacyFamily8418Rate(0.0001))
        {
            return LegacyFamily8418Floor(
                3_001_044,
                "Core.Rate(0.0001) DropItemID(3001044)",
                "{name} He Get 1 Mastrey Froute !");
        }

        if (CheckLegacyFamily8418Rate(0.008))
        {
            return LegacyFamily8418Floor(3_001_045, "Core.Rate(0.008) DropItemID(3001045)");
        }

        if (CheckLegacyFamily8418Rate(0.003))
        {
            return LegacyFamily8418Floor(3_316_793, "Core.Rate(0.003) DropItemID(3316793)");
        }

        if (CheckLegacyFamily8418Rate(0.0004))
        {
            return LegacyFamily8418Floor(3_330_770, "Core.Rate(0.0004) DropItemID(3330770)");
        }

        if (CheckLegacyFamily8418Rate(0.0005))
        {
            return LegacyFamily8418None("outer Core.Rate(0.0005)=true");
        }

        if (CheckLegacyFamily8418Rate(0.0006))
        {
            return new LegacyFamily8418Reward(
                LegacyFamily8418RewardKind.ClassExperience,
                ItemId: 0,
                Amount: 50,
                SourceMessage: "{name} He Get 50 ClassExperience   in Map Huant",
                SourceDetail: "Core.Rate(0.0006) ClassExperience += 50");
        }

        if (CheckLegacyFamily8418Rate(0.0002))
        {
            return LegacyFamily8418Floor(
                729_463,
                "Core.Rate(0.0002) DropItemID(729463)",
                "{name} He Get Ninja points  Archive in Map Huant !");
        }

        if (CheckLegacyFamily8418Rate(0.0006))
        {
            return LegacyFamily8418Floor(
                729_464,
                "Core.Rate(0.0006) DropItemID(729464)",
                "{name} He Get Skill Ninja Archive in Map Huant !");
        }

        if (CheckLegacyFamily8418Rate(0.0002))
        {
            return SelectLegacyFamily8418SwitchReward(Random.Shared.Next(1, 6));
        }

        foreach (var reward in LegacyFamily8418TailFloorRewards)
        {
            if (CheckLegacyFamily8418Rate(reward.RatePercent))
            {
                return LegacyFamily8418Floor(
                    reward.ItemId,
                    FormattableString.Invariant($"Core.Rate({reward.RatePercent.ToString(CultureInfo.InvariantCulture)}) DropItemID({reward.ItemId})"));
            }
        }

        if (CheckLegacyFamily8418Rate(0.0008) ||
            CheckLegacyFamily8418Rate(0.0007) ||
            CheckLegacyFamily8418Rate(0.0009) ||
            CheckLegacyFamily8418Rate(0.00003) ||
            CheckLegacyFamily8418Rate(0.0004))
        {
            return LegacyFamily8418None("terminal guard Core.Rate(...) hit");
        }

        if (CheckLegacyFamily8418Rate(0.0007))
        {
            return LegacyFamily8418Floor(3_306_368, "nested Core.Rate(0.0007) DropItemID(3306368)");
        }

        if (CheckLegacyFamily8418Rate(0.0008))
        {
            return LegacyFamily8418Inventory(3_335_127, "nested Core.Rate(0.0008) AddItemWitchStack(3335127)");
        }

        if (CheckLegacyFamily8418Rate(0.0022))
        {
            return LegacyFamily8418Inventory(3_335_126, "nested Core.Rate(0.0022) AddItemWitchStack(3335126)");
        }

        if (!CheckLegacyFamily8418Rate(0.008))
        {
            return LegacyFamily8418None("nested Core.Rate(0.008)=false");
        }

        return LegacyFamily8418Floor(3_004_280, "nested Core.Rate(0.008) DropItemID(3004280)");
    }

    private static bool TryReadLegacyFamily8418Reward(
        CharacterSnapshot character,
        out LegacyFamily8418Reward reward)
    {
        reward = default;
        if (!character.Attributes.TryGetValue(RuntimeLegacyFamily8418RewardBranchAttribute, out var raw))
        {
            return false;
        }

        var key = raw.Trim().ToLowerInvariant();
        switch (key)
        {
            case "none":
            case "no-drop":
            case "guard":
            case "guard-blocked":
                reward = LegacyFamily8418None("forced no-drop");
                return true;

            case "mastrey":
            case "mastrey-fruit":
            case "mastery-fruit":
                reward = LegacyFamily8418Floor(
                    3_001_044,
                    "forced Core.Rate(0.0001) DropItemID(3001044)",
                    "{name} He Get 1 Mastrey Froute !");
                return true;

            case "class":
            case "class-exp":
            case "class-experience":
                reward = new LegacyFamily8418Reward(
                    LegacyFamily8418RewardKind.ClassExperience,
                    ItemId: 0,
                    Amount: 50,
                    SourceMessage: "{name} He Get 50 ClassExperience   in Map Huant",
                    SourceDetail: "forced ClassExperience += 50");
                return true;

            case "ninja-points-archive":
            case "ninja-points-archive-item":
                reward = LegacyFamily8418Floor(
                    729_463,
                    "forced DropItemID(729463)",
                    "{name} He Get Ninja points  Archive in Map Huant !");
                return true;

            case "skill-ninja-archive":
            case "skill-ninja-archive-item":
                reward = LegacyFamily8418Floor(
                    729_464,
                    "forced DropItemID(729464)",
                    "{name} He Get Skill Ninja Archive in Map Huant !");
                return true;

            case "switch1":
            case "switch3333400":
                reward = LegacyFamily8418Floor(3_333_400, "forced switch roll DropItemID(3333400)");
                return true;

            case "switch2":
            case "switch3333401":
                reward = LegacyFamily8418Floor(3_333_401, "forced switch roll DropItemID(3333401)");
                return true;

            case "switch3":
            case "switch3333402":
                reward = LegacyFamily8418Floor(3_333_402, "forced switch roll DropItemID(3333402)");
                return true;

            case "switch5":
            case "switch3333403":
                reward = LegacyFamily8418Floor(3_333_403, "forced switch roll DropItemID(3333403)");
                return true;

            case "switch6":
            case "switch3333404":
                reward = LegacyFamily8418Floor(3_333_404, "forced unreachable switch roll DropItemID(3333404)");
                return true;

            case "inventory3335127":
            case "inventory-3335127":
            case "3335127-inventory":
                reward = LegacyFamily8418Inventory(3_335_127, "forced AddItemWitchStack(3335127)");
                return true;

            case "inventory3335126":
            case "inventory-3335126":
            case "3335126-inventory":
                reward = LegacyFamily8418Inventory(3_335_126, "forced AddItemWitchStack(3335126)");
                return true;

            case "fallback":
            case "fallback3004280":
            case "item3004280":
                reward = LegacyFamily8418Floor(3_004_280, "forced nested DropItemID(3004280)");
                return true;
        }

        if (key.StartsWith("item", StringComparison.Ordinal) &&
            uint.TryParse(key["item".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId))
        {
            reward = LegacyFamily8418Floor(itemId, FormattableString.Invariant($"forced DropItemID({itemId})"));
            return true;
        }

        if (uint.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericItemId))
        {
            reward = LegacyFamily8418Floor(
                numericItemId,
                FormattableString.Invariant($"forced DropItemID({numericItemId})"));
            return true;
        }

        return false;
    }

    private static LegacyFamily8418Reward SelectLegacyFamily8418SwitchReward(int sourceRoll)
    {
        return sourceRoll switch
        {
            1 => LegacyFamily8418Floor(3_333_400, "switch roll=1 DropItemID(3333400)"),
            2 => LegacyFamily8418Floor(3_333_401, "switch roll=2 DropItemID(3333401)"),
            3 => LegacyFamily8418Floor(3_333_402, "switch roll=3 DropItemID(3333402)"),
            4 => LegacyFamily8418Floor(3_333_400, "switch roll=4 DropItemID(3333400)"),
            5 when CheckLegacyFamily8418Rate(0.002) =>
                LegacyFamily8418Floor(3_333_403, "switch roll=5 Core.Rate(0.002) DropItemID(3333403)"),
            6 when CheckLegacyFamily8418Rate(0.0001) =>
                LegacyFamily8418Floor(3_333_404, "switch roll=6 Core.Rate(0.0001) DropItemID(3333404)"),
            _ => LegacyFamily8418None(FormattableString.Invariant($"switch roll={sourceRoll} no drop")),
        };
    }

    private static bool CheckLegacyFamily8418Rate(double ratePercent)
    {
        return Random.Shared.NextDouble() * 100.0 < ratePercent;
    }

    private static LegacyFamily8418Reward LegacyFamily8418Floor(
        uint itemId,
        string sourceDetail,
        string? sourceMessage = null)
    {
        return new LegacyFamily8418Reward(
            LegacyFamily8418RewardKind.FloorDrop,
            itemId,
            Amount: 0,
            sourceMessage,
            sourceDetail);
    }

    private static LegacyFamily8418Reward LegacyFamily8418Inventory(uint itemId, string sourceDetail)
    {
        return new LegacyFamily8418Reward(
            LegacyFamily8418RewardKind.InventoryStack,
            itemId,
            Amount: 0,
            SourceMessage: null,
            sourceDetail);
    }

    private static LegacyFamily8418Reward LegacyFamily8418None(string sourceDetail)
    {
        return new LegacyFamily8418Reward(
            LegacyFamily8418RewardKind.None,
            ItemId: 0,
            Amount: 0,
            SourceMessage: null,
            sourceDetail);
    }

    private static readonly LegacyFamily8418FloorReward[] LegacyFamily8418TailFloorRewards =
    [
        new(3_330_609, 0.0003),
        new(3_330_608, 0.0004),
        new(3_327_203, 0.0005),
        new(3_321_805, 0.0007),
        new(3_321_107, 0.0008),
        new(3_321_108, 0.0006),
        new(3_311_632, 0.009),
        new(700_132, 0.0003),
        new(3_009_101, 0.0004),
        new(3_009_102, 0.0005),
        new(3_009_103, 0.0007),
        new(3_009_104, 0.008),
        new(3_321_098, 0.0003),
        new(3_009_000, 0.0004),
        new(3_009_001, 0.0005),
        new(3_009_002, 0.0007),
        new(3_009_003, 0.0002),
        new(3_009_104, 0.0008),
        new(3_007_110, 0.0005),
        new(3_005_360, 0.0007),
        new(3_001_044, 0.0008),
        new(3_001_038, 0.0008),
    ];

    private enum LegacyFamily8418RewardKind
    {
        None,
        FloorDrop,
        InventoryStack,
        ClassExperience,
    }

    private readonly record struct LegacyFamily8418Reward(
        LegacyFamily8418RewardKind Kind,
        uint ItemId,
        ulong Amount,
        string? SourceMessage,
        string SourceDetail);

    private readonly record struct LegacyFamily8418FloorReward(uint ItemId, double RatePercent);
}
