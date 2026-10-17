using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family2171JadeCourtesanRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 2_171)
        {
            return session;
        }

        const uint questId = 408;
        const uint purpleEyeItemId = 710_216;
        const uint jadeSceptreItemId = 710_217;
        const uint jadeiteItemId = 710_218;
        const uint hairpinItemId = 710_219;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 2171 Jade Courtesan reward";
        if (CountLegacyInventoryItem(session.PlayerItems, jadeSceptreItemId) >= 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 Jade Courtesan reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={jadeSceptreItemId} reason=\"Jade Sceptre already in inventory\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Your inventory is full! Make some room, first.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 Jade Courtesan reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={jadeSceptreItemId} reason=\"inventory full\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, jadeSceptreItemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1015-jade-sceptre",
                        reason: "source Inventory.Add(710217) grants the Jade Sceptre after killing Jade Courtesan, but the item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", jadeSceptreItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("itemId", jadeSceptreItemId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var updatedItems = AddLegacyInventoryItem(
            session.PlayerItems,
            itemType,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await TrySetLegacyQuestObjectiveAsync(
                updatedContext,
                updatedSession,
                questId,
                value: 1,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        const string rewardMessage =
            "You`ve killed the Jade Courtesan and obtained her Jade Sceptre.";
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(rewardMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var allPlatformItemsCollected =
            CountLegacyInventoryItem(updatedSession.PlayerItems, purpleEyeItemId) >= 1 &&
            CountLegacyInventoryItem(updatedSession.PlayerItems, jadeSceptreItemId) >= 1 &&
            CountLegacyInventoryItem(updatedSession.PlayerItems, jadeiteItemId) >= 1 &&
            CountLegacyInventoryItem(updatedSession.PlayerItems, hairpinItemId) >= 1;
        var platformMessage = allPlatformItemsCollected
            ? "You`ve collected the Purple Eye, Jade Scepter, Jadeite, and Hairpin. Go to open the Teleport Platform on Bird Island (655,693)."
            : "As long as you collect the Purple Eye, Jade Sceptre, Jadeite, and Hairpin, you can open the Platform (Bird Island 655,693) and go to the Rose Garden.";
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(platformMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1015 Jade Courtesan reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={jadeSceptreItemId} itemMode={itemMode} " +
                $"allPlatformItemsCollected={allPlatformItemsCollected} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family2157BloodyShawnRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 2_157)
        {
            return session;
        }

        const uint questId = 523;
        const uint spearItemId = 721_781;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 2157 Bloody Shawn spear reward";
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
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
                    "WorldServer skipped legacy map 1015 Bloody Shawn reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={spearItemId} reason=\"inventory full\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, spearItemId) >= 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 Bloody Shawn reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={spearItemId} reason=\"Spear already in inventory\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, spearItemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1015-bloody-shawn-spear",
                        reason: "source Inventory.Add(721781) grants Bloody Shawn's Spear for quest 523, but the item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", spearItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", spearItemId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var updatedItems = AddLegacyInventoryItem(
            session.PlayerItems,
            itemType,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket
                    .ForMessageBox("Your~power~and~fortitude~overwhelmed~Bloody~Shawn,~and~you~obtained~his~Spear.~Grab~it~along~and~find~Ruan~Brother.")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1015 Bloody Shawn reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={spearItemId} itemMode={itemMode} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family2156TroutElfRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 2_156)
        {
            return session;
        }

        const uint questId = 522;
        const uint freshFishItemId = 721_778;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 2156 Trout Elf fresh fish reward";
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted ||
            CountLegacyInventoryItem(session.PlayerItems, freshFishItemId) >= 3)
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
                    "WorldServer skipped legacy map 1015 Trout Elf reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={freshFishItemId} reason=\"inventory full\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, freshFishItemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1015-trout-elf-fresh-fish",
                        reason: "source AddItemWitchStack(721778) grants fresh fish for quest 522, but the item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", freshFishItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", freshFishItemId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var fishCountBefore = CountLegacyInventoryItem(session.PlayerItems, freshFishItemId);
        var updatedItems = AddOrStackLegacyInventoryItem(
            session.PlayerItems,
            itemType,
            freshFishItemId,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket
                    .ForMessageBox("Trout~Elves~died~and~turned~into~fresh~fish.~Collect~3~fish~and~trade~with~Ye~Sheng~(Bird~Island~324,400)~for~his~Iron~Poise.")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1015 Trout Elf reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={freshFishItemId} itemMode={itemMode} " +
                $"fishCountBefore={fishCountBefore} fishCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, freshFishItemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family2224RedTokenRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 2_224)
        {
            return session;
        }

        const uint questId = 1_820;
        const uint redTokenItemId = 710_562;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 2224 quest 1820 Red Token reward";
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("Sorry, your inventory is full. Make some room first.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 2224 Red Token reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={redTokenItemId} reason=\"inventory full\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, redTokenItemId) >= 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 2224 Red Token reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={redTokenItemId} reason=\"Red Token already in inventory\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, redTokenItemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1015-red-token",
                        reason: "source Inventory.Add(710562) grants the Red Token for quest 1820, but the item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", redTokenItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", redTokenItemId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var updatedItems = AddLegacyInventoryItem(
            session.PlayerItems,
            itemType,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("You picked up the Red Token. Now hurry up and give it to Felix!").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1015 family 2224 Red Token reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={redTokenItemId} itemMode={itemMode} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

}
