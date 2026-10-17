using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1015TokenRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            !TryGetLegacyMap1015TokenRewardSpec(
                monsterFamily.Id,
                out var questId,
                out var itemId,
                out var ratePercent,
                out var rateAttribute,
                out var tokenName,
                out var sourceMessage))
        {
            return session;
        }

        var sourceBranch =
            $"MonsterRole.cs killer.Player.Map == 1015 && Family.ID == {monsterFamily.Id.ToString(CultureInfo.InvariantCulture)} {tokenName} token reward";
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!CheckLegacyMap1015TokenRewardRate(session.Character, rateAttribute, ratePercent))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 token reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} token={tokenName} reason=\"Core.Rate({ratePercent})=false\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("Sorry, your inventory is full. Make some room first.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 token reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} token={tokenName} reason=\"inventory full\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, itemId) >= 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 token reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} token={tokenName} reason=\"token already in inventory\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        $"map1015-{tokenName.ToLowerInvariant()}-token",
                        reason: $"source AddItemWitchStack({itemId.ToString(CultureInfo.InvariantCulture)}) grants the {tokenName} Token, but the item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
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
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(sourceMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1015 token reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} token={tokenName} itemMode={itemMode} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool TryGetLegacyMap1015TokenRewardSpec(
        int familyId,
        out uint questId,
        out uint itemId,
        out int ratePercent,
        out string rateAttribute,
        out string tokenName,
        out string sourceMessage)
    {
        switch (familyId)
        {
            case 85:
                questId = 1_824;
                itemId = 710_566;
                ratePercent = 30;
                rateAttribute = RuntimeLegacyMap1015Family85ScarletTokenRateAttribute;
                tokenName = "Scarlet";
                sourceMessage = "You picked up the Scarlet Token. Now hurry up and give it to Felix!";
                return true;
            case 2_226:
                questId = 1_823;
                itemId = 710_565;
                ratePercent = 30;
                rateAttribute = RuntimeLegacyMap1015Family2226PitchTokenRateAttribute;
                tokenName = "Pitch";
                sourceMessage = "You picked up the Pitch Token. Now hurry up and give it to Felix!";
                return true;
            case 2_225:
                questId = 1_822;
                itemId = 710_564;
                ratePercent = 50;
                rateAttribute = RuntimeLegacyMap1015Family2225BlackTokenRateAttribute;
                tokenName = "Black";
                sourceMessage = "You picked up the Black Token. Now hurry up and give it to Felix!";
                return true;
            case 79:
                questId = 1_821;
                itemId = 710_563;
                ratePercent = 30;
                rateAttribute = RuntimeLegacyMap1015Family79IndigoTokenRateAttribute;
                tokenName = "Indigo";
                sourceMessage = "You~picked~up~the~Indigo~Token.~Now~hurry~up~and~give~it~to~Felix!";
                return true;
            default:
                questId = 0;
                itemId = 0;
                ratePercent = 0;
                rateAttribute = "";
                tokenName = "";
                sourceMessage = "";
                return false;
        }
    }

    private static bool CheckLegacyMap1015TokenRewardRate(
        CharacterSnapshot character,
        string attributeName,
        int ratePercent)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, attributeName)
            ? forced
            : Random.Shared.Next(0, 100) < ratePercent;
    }
}
