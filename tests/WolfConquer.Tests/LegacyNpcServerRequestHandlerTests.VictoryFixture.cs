using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcServerRequestHandlerTests
{
    private sealed partial class DeityChestFixture
    {
        private string? victoryQuestRoot;
        private byte[] victoryQuestsBefore = [];
        private Task? currentChestOperationProbe;
        private CancellationTokenSource? currentChestOperationProbeBudget;
        internal string VictoryQuestsRoot => victoryQuestRoot ?? throw new InvalidOperationException("Victory was not seeded.");
        private string VictoryQuestPath => Path.Combine(VictoryQuestsRoot, "1293600.bin");
        private IEnumerable<string> VictoryExpectedFiles => victoryQuestRoot is null ? [] : new[] { VictoryQuestPath };

        internal void SeedVictory(WorldSession seed)
        {
            // Reuse the exact Name-aware User/item seed with an empty sidecar;
            // its scalar comparison remains real and unchanged for Rare/Epic.
            var sourceQuests = seed.Character.PlayerQuests;
            Seed(seed with { Character = seed.Character with { PlayerQuests = PlayerQuestSnapshot.Empty } });
            victoryQuestRoot = Path.Combine(Root, "Quests");
            Directory.CreateDirectory(victoryQuestRoot);
            var loader = new PlayerQuestSnapshotLoader(victoryQuestRoot);
            loader.SaveByCharacterUid(CharacterUid, sourceQuests);
            var loaded = loader.LoadByCharacterUid(CharacterUid);
            victoryQuestsBefore = File.ReadAllBytes(VictoryQuestPath);
            Assert.Equal(PlayerQuestSnapshotLoader.Serialize(sourceQuests), victoryQuestsBefore);
            Assert.Equal(victoryQuestsBefore, PlayerQuestSnapshotLoader.Serialize(loaded));
            original = Current with { Character = Current.Character with { PlayerQuests = loaded } };
            Sessions.Set(Connection, original);
            // Genuine triple recovery only AFTER the complete three owned files
            // exist. No provider, inferred sibling authority or readiness fake.
            recovery = new LegacyTradeRecoveryState(Users, Items, victoryQuestRoot);
            Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
            AssertReady();
            AssertSavedCurrent();
            AssertOnlyExpectedFiles();
        }

        private void AssertVictoryDialogOnly()
        {
            if (victoryQuestRoot is null) return;
            Assert.Equal(victoryQuestsBefore, File.ReadAllBytes(VictoryQuestPath));
            Assert.Equal(victoryQuestsBefore, PlayerQuestSnapshotLoader.Serialize(Current.Character.PlayerQuests));
            Assert.Same(original.Character.PlayerQuests, Current.Character.PlayerQuests);
        }

        private void AssertSavedVictoryQuest()
        {
            if (victoryQuestRoot is null) return;
            var bytes = PlayerQuestSnapshotLoader.Serialize(Current.Character.PlayerQuests);
            Assert.Equal(bytes, File.ReadAllBytes(VictoryQuestPath));
            Assert.Equal(bytes, PlayerQuestSnapshotLoader.Serialize(
                new PlayerQuestSnapshotLoader(victoryQuestRoot).LoadByCharacterUid(CharacterUid)));
        }

        internal void AssertVictoryCommitted(uint npcId)
        {
            Assert.Equal(10, Sent.Count);
            Assert.True(original.Character.PlayerQuests.TryGetQuest(3_944, out var oldQuest));
            var expectedQuests = original.Character.PlayerQuests.WithQuest(oldQuest with { Status = PlayerQuestStatus.Finished });
            Assert.Equal(LegacyQuestListPacket.CreateSingleQuestFrame(LegacyQuestListMode.FinishQuest,
                new LegacyQuestListItem(3_944, LegacyQuestStatus.Finished, oldQuest.Time)), Sent[8]);
            var packet = AssertGameItemIn(Sent[9], [3_008_727U, 3_009_002U, 4_200_004U]);
            Assert.NotEqual(0U, packet.Uid);
            Assert.DoesNotContain(original.PlayerItems.InventoryItems, item => item.Uid == packet.Uid);
            var reward = new PlayerItemSnapshotItem { Uid = packet.Uid, ItemId = packet.ItemId, StackSize = 1 };
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(reward).ToFrameBytes(), Sent[9]);
            var expectedItems = original.PlayerItems with
            { InventoryItems = original.PlayerItems.InventoryItems.Concat([reward]).ToArray() };
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems), PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Equal(PlayerQuestSnapshotLoader.Serialize(expectedQuests), PlayerQuestSnapshotLoader.Serialize(Current.Character.PlayerQuests));
            Assert.Equal(original with
            {
                ActiveNpcId = npcId,
                Character = original.Character with { PlayerQuests = Current.Character.PlayerQuests },
                PlayerItems = Current.PlayerItems,
            }, Current);
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(original.PlayerItems));
            Assert.Equal(victoryQuestsBefore, PlayerQuestSnapshotLoader.Serialize(original.Character.PlayerQuests));
            Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
            AssertSavedCurrent();
            AssertOnlyExpectedFiles();
            AssertReady();
            Assert.DoesNotContain("PARITY-GAP", Log.ToString());
        }

        private async Task HandleRegisteredWithOperationAsync(WorldPacketHandlerContext context,
            WolfConquer.Core.Packets.PacketFrame frame, CancellationToken cancellationToken)
        {
            // Complete actual delegate (including dispatch) owns one real Shared
            // lease. Transaction code must not recursively upgrade this scope.
            using var operation = await operations.EnterSharedAsync(cancellationToken);
            await Registry.HandleAsync(context, frame, cancellationToken);
        }

        private async Task ProbeCurrentChestOperationReleaseAsync()
        {
            currentChestOperationProbeBudget ??= new CancellationTokenSource(Bound);
            using var operation = await operations.EnterExclusiveAsync(currentChestOperationProbeBudget.Token);
        }
    }
}
