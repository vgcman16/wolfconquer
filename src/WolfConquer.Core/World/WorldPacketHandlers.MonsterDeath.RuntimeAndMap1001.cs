using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool ShouldApplyLegacyPursuingDevilsQuestClear(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        if (TryReadUInt64(session.Character.Attributes, out var forcedVisibleCount, "LegacyVisibleMonsterCount"))
        {
            return forcedVisibleCount <= 1;
        }

        return EnumerateVisibleMonsterTargets(context, session.Character)
            .Take(2)
            .Count() <= 1;
    }

    private static async ValueTask<WorldSession> TrySetLegacyQuestObjectiveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint questId,
        uint value,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "player quest snapshot",
                        "monster-death-quest-objective-set",
                        reason: "source SetQuestObjectives requires an active player quest row, but the modern session has no matching quest snapshot",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", session.Character.MapId),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("value", value),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        uint[] intentions = quest.Intentions.Count == 0
            ? [value]
            : quest.Intentions.ToArray();
        intentions[0] = value;

        var updatedQuest = quest with { Intentions = intentions };
        var updatedSession = WithPlayerQuests(
            session,
            session.Character.PlayerQuests.WithQuest(updatedQuest));
        context.Sessions?.Set(context.Connection, updatedSession);
        await PersistLegacyPlayerQuestsAsync(
                context,
                updatedSession,
                sourceBranch,
                "monster-death-quest-objective-set",
                questId,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                MsgQuestDataPacket.CreateObjectiveFrame(updatedQuest),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy monster death quest objective set " +
                $"{FormatSession(context with { Session = updatedSession })} questId={questId} value={value} " +
                $"objective0={updatedQuest.Intentions[0]} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyQuestObjectiveIncrementAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint questId,
        uint amount,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "player quest snapshot",
                        "monster-death-quest-objective",
                        reason: "source IncreaseQuestObjectives requires an active player quest row, but the modern session has no matching quest snapshot",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", session.Character.MapId),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        uint[] intentions = quest.Intentions.Count == 0
            ? [amount]
            : quest.Intentions.ToArray();
        if (quest.Intentions.Count != 0)
        {
            intentions[0] = checked(intentions[0] + amount);
        }

        var updatedQuest = quest with { Intentions = intentions };
        var updatedSession = WithPlayerQuests(
            session,
            session.Character.PlayerQuests.WithQuest(updatedQuest));
        context.Sessions?.Set(context.Connection, updatedSession);
        await PersistLegacyPlayerQuestsAsync(
                context,
                updatedSession,
                sourceBranch,
                "monster-death-quest-objective-increment",
                questId,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                MsgQuestDataPacket.CreateObjectiveFrame(updatedQuest),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy monster death quest objective " +
                $"{FormatSession(context with { Session = updatedSession })} questId={questId} amount={amount} " +
                $"objective0={updatedQuest.Intentions[0]} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyQuestObjectiveIncrementsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint questId,
        IReadOnlyList<uint> amounts,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "player quest snapshot",
                        "monster-death-quest-objectives",
                        reason: "source IncreaseQuestObjectives requires an active player quest row, but the modern session has no matching quest snapshot",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", session.Character.MapId),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        if (amounts.Count == 0)
        {
            return session;
        }

        var intentions = quest.Intentions.Count == 0
            ? new uint[amounts.Count]
            : quest.Intentions.ToArray();
        if (intentions.Length < amounts.Count)
        {
            Array.Resize(ref intentions, amounts.Count);
        }

        for (var index = 0; index < amounts.Count; index++)
        {
            intentions[index] = checked(intentions[index] + amounts[index]);
        }

        var updatedQuest = quest with { Intentions = intentions };
        var updatedSession = WithPlayerQuests(
            session,
            session.Character.PlayerQuests.WithQuest(updatedQuest));
        context.Sessions?.Set(context.Connection, updatedSession);
        await PersistLegacyPlayerQuestsAsync(
                context,
                updatedSession,
                sourceBranch,
                "monster-death-quest-objective-increments",
                questId,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                MsgQuestDataPacket.CreateObjectiveFrame(updatedQuest),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy monster death quest objectives " +
                $"{FormatSession(context with { Session = updatedSession })} questId={questId} " +
                $"amounts={string.Join(',', amounts)} objectives={string.Join(',', updatedQuest.Intentions)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static bool TryGetLegacyBattleFieldMonsterDeathPoints(long mapId, out ulong battlePoints)
    {
        battlePoints = mapId switch
        {
            1_081 => 10,
            1_080 => 20,
            2_060 => 30,
            _ => 0,
        };

        return battlePoints != 0;
    }

    private static async ValueTask SendLegacyLocationEffectToVisiblePlayersAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort x,
        ushort y,
        string effectName,
        CancellationToken cancellationToken)
    {
        var frame = LegacyStringPacket.LocationEffect(x, y, effectName).ToFrameBytes();
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);

        if (context.Sessions is not { } sessions)
        {
            return;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == session.Character.Uid ||
                !CanSeeFloorItem(active.Session.Character, session.Character.MapId, x, y))
            {
                continue;
            }

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsLegacyMap1011Or1020Family3130Or3134DropBranch(
        LegacyMonsterInstance monster,
        Monster monsterFamily)
    {
        return monster.MapId is 1_011 or 1_020 &&
            monsterFamily.Id is 3_130 or 3_134;
    }

    private static bool CheckLegacyMapSpecialDropRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(
                character.Attributes,
                out var forced,
                RuntimeLegacyMapSpecialDropRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 50;
    }

    private static uint SelectLegacyMap1011Or1020Family3130DropItemId(CharacterSnapshot character)
    {
        if (TryReadInt64Attribute(
                character.Attributes,
                RuntimeLegacyMapSpecialDropItemIndexAttribute,
                out var forcedIndex) &&
            forcedIndex >= 0)
        {
            return LegacyMap1011Or1020Family3130ItemDrops[
                checked((int)(forcedIndex % LegacyMap1011Or1020Family3130ItemDrops.Length))];
        }

        return LegacyMap1011Or1020Family3130ItemDrops[
            Random.Shared.Next(0, LegacyMap1011Or1020Family3130ItemDrops.Length)];
    }

    private static bool CheckLegacyDeityLandRandomDropRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(
                character.Attributes,
                out var forced,
                RuntimeLegacyDeityLandRandomDropRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 3;
    }

    private static uint SelectLegacyDeityLandRandomDropItemId(CharacterSnapshot character)
    {
        var sourceRoll = Random.Shared.Next(1, 18);
        if (TryReadInt64Attribute(
                character.Attributes,
                RuntimeLegacyDeityLandRandomDropRollAttribute,
                out var forcedRoll))
        {
            sourceRoll = checked((int)PositiveModulo(forcedRoll - 1, LegacyDeityLandRandomDropItems.Length) + 1);
        }

        return LegacyDeityLandRandomDropItems[sourceRoll - 1];
    }

    private static int SelectLegacyDeityLandWarDevilLuckyRewardRoll(CharacterSnapshot character)
    {
        var sourceRoll = Random.Shared.Next(1, 24);
        if (TryReadInt64Attribute(
                character.Attributes,
                RuntimeLegacyDeityLandWarDevilLuckyRewardRollAttribute,
                out var forcedRoll))
        {
            sourceRoll = checked((int)PositiveModulo(forcedRoll - 1, LegacyDeityLandWarDevilLuckyRewardItems.Length) + 1);
        }

        return sourceRoll;
    }

    private static int SelectLegacyDeityLandWarDevilBaseRewardRoll(CharacterSnapshot character)
    {
        var sourceRoll = Random.Shared.Next(1, 6);
        if (TryReadInt64Attribute(
                character.Attributes,
                RuntimeLegacyDeityLandWarDevilBaseRewardRollAttribute,
                out var forcedRoll))
        {
            sourceRoll = checked((int)PositiveModulo(forcedRoll - 1, LegacyDeityLandWarDevilBaseRewardItems.Length) + 1);
        }

        return sourceRoll;
    }

    private static uint SelectLegacyDeityLandStoneSpriteRewardItemId(CharacterSnapshot character)
    {
        if (TryReadInt64Attribute(
                character.Attributes,
                RuntimeLegacyDeityLandStoneSpriteRewardItemIdAttribute,
                out var forcedItemId) &&
            forcedItemId >= LegacyDeityLandStoneSpriteRewardItems[0] &&
            forcedItemId <= LegacyDeityLandStoneSpriteRewardItems[^1])
        {
            return checked((uint)forcedItemId);
        }

        return checked((uint)Random.Shared.Next(730_001, 730_006));
    }

    private static string ResolveLegacyItemName(WorldDataCatalog? worldData, uint itemId)
    {
        if (worldData is not null &&
            itemId <= int.MaxValue &&
            worldData.ItemsById.TryGetValue(checked((int)itemId), out var itemTypes) &&
            itemTypes.Count > 0 &&
            !string.IsNullOrWhiteSpace(itemTypes[0].Name))
        {
            return itemTypes[0].Name;
        }

        return itemId.ToString(CultureInfo.InvariantCulture);
    }

    private static long PositiveModulo(long value, int divisor)
    {
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static bool IsLegacyMap11127SpecialDropSuppressedByEarlierSourceBranch(
        LegacyMonsterInstance monster,
        Monster monsterFamily)
    {
        if (monsterFamily.Id < 0)
        {
            return false;
        }

        var familyId = checked((uint)monsterFamily.Id);
        return LegacyBossSpecialDeathDropPolicy.IsStudyPointSpecialBranch(familyId, monster.Boss) ||
            monster.Boss && familyId is
                LegacyBossSpecialDeathDropPolicy.ThrillingSpookFamilyId or
                LegacyBossSpecialDeathDropPolicy.StudyPointTeamRewardFamilyId or
                LegacyBossSpecialDeathDropPolicy.SoulBoxFamilyId;
    }
}
