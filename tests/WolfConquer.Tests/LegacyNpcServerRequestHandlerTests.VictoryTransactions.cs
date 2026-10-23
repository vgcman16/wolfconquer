using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcServerRequestHandlerTests
{
    [Fact]
    public async Task HandlerUnlocksVictoryChestWhenDeitylandQuestObjectiveIsReady()
    {
        await RunDeityChestAsync(async fixture =>
        {
            var log = fixture.Log;
            var sent = fixture.Sent;
            var connection = fixture.Connection;
            var baseSession = CreateDragonIslandSession();
            var session = baseSession with
            {
                Character = baseSession.Character with
                {
                    PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(3_944, PlayerQuestStatus.Accepted, 0, [100U]),
                    ]),
                },
            };
            fixture.SeedVictory(session);
            var sessions = fixture.Sessions;
            var playerQuestsRoot = fixture.VictoryQuestsRoot;

            await fixture.HandleAsync(ReadFrame(LegacyNpcServerRequestPacket.Script(21_972, optionId: 0, interactType: 1).ToFrameBytes()));

            var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
            Assert.Contains("Victory Chest", dialog.Text);
            Assert.Contains(sent, frame =>
                LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.InteractType == LegacyNpcReplyInteractType.Dialog &&
                packet.Text.Contains("Kill 100 devils in the Deityland"));
            Assert.Contains(sent, frame =>
                LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.InteractType == LegacyNpcReplyInteractType.Option &&
                packet.OptionId == 1 &&
                packet.Text == "Unlock~this~chest.");

            Assert.True(sessions.TryGet(connection, out var activeSession));
            Assert.NotNull(activeSession);
            fixture.AssertDialogOnly(21_972);

            await fixture.HandleAsync(ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

            Assert.Contains(sent, frame =>
                LegacyQuestListPacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.Mode == LegacyQuestListMode.FinishQuest &&
                packet.QuestId == 3_944 &&
                packet.Status == LegacyQuestStatus.Finished);
            Assert.Contains(sent, frame =>
                LegacyGameItemPacket.TryParse(ReadFrame(frame), out var packet) &&
                new[] { 3_008_727U, 3_009_002U, 4_200_004U }.Contains(packet.ItemId));
            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            var quest = Assert.Single(updatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 3_944U);
            Assert.Equal(PlayerQuestStatus.Finished, quest.Status);
            Assert.Contains(updatedSession.PlayerItems.InventoryItems, item =>
                item.ItemId is 3_008_727U or 3_009_002U or 4_200_004U);
            var persistedQuest = Assert.Single(
                new PlayerQuestSnapshotLoader(playerQuestsRoot)
                    .LoadByCharacterUid(updatedSession.Character.Uid)
                    .Quests,
                entry => entry.Uid == 3_944U);
            Assert.Equal(PlayerQuestStatus.Finished, persistedQuest.Status);
            Assert.Equal([100U], persistedQuest.Intentions.ToArray());
            Assert.Contains("source=NpcHandler.VictoryChest", log.ToString());
            Assert.Contains("WorldServer persisted LegacyPlayerQuests", log.ToString());
            Assert.Contains("source=NpcHandler.VictoryChest", log.ToString());
            Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
            fixture.AssertVictoryCommitted(21_972);
        });
    }
}
