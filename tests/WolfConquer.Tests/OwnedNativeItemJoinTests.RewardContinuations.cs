using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedNativeItemJoinTests
{
    [Theory]
    [InlineData("skin")]
    [InlineData("deity")]
    [InlineData("victory")]
    public async Task PersistedRewardPacketsPublishExactJoinedCanonicalReceiptBeforeTheirOriginalSuccessSequence(string flow)
    {
        const uint skin = 4200018, key = 3303973, keeperId = 50000, questId = 3944;
        uint[] rewards = flow == "deity" ? new uint[] { 3306362, 3303480, 3003124 } : new uint[] { 3008727, 3009002, 4200004 };
        uint costId = flow == "skin" ? skin : key;
        var keeper = new PlayerItemSnapshotItem { Uid = Fixture.ItemUid + 20, ItemId = keeperId,
            StackSize = 1, Durability = 1, MaximumDurability = 1 };
        var cost = new PlayerItemSnapshotItem { Uid = Fixture.ItemUid, ItemId = costId,
            StackSize = 1, Durability = 1, MaximumDurability = 1 };
        var initial = new PlayerItemSnapshot(flow == "victory" ? new[] { keeper } : new[] { cost, keeper },
            Array.Empty<PlayerItemSnapshotItem>(), Array.Empty<PlayerItemSnapshotItem>(),
            Array.Empty<PlayerItemSnapshotItem>(), Array.Empty<PlayerItemSnapshotItem>());
        var quests = new PlayerQuestSnapshot(new[]
        {
            new PlayerQuestSnapshotEntry(questId, PlayerQuestStatus.Accepted, 12345, new uint[] { 100 }),
            new PlayerQuestSnapshotEntry(9001, PlayerQuestStatus.Finished, 42, new uint[] { 7 })
        });
        var catalogItems = rewards.Concat(new uint[] { skin, key, keeperId }).Distinct().Select(RewardCatalogRow).ToArray();
        var npcs = new[] { new Npc(22598, 0, 0, 1015, 830, 584, null), new Npc(21972, 0, 0, 1015, 830, 584, null) };
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login(); var before = f.Current; var binding = RequireGraph(f);
            var source = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
            var oldQuests = PlayerQuestSnapshotLoader.Serialize(before.Character.PlayerQuests);
            var oldUids = before.PlayerItems.InventoryItems.Select(row => row.Uid).ToHashSet();
            var actualKeeper = f.Native.Inventory!.ClientItems[keeper.Uid];
            var originalCharacter = File.ReadAllBytes(f.CharacterPath);
            var routes = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(f.Users));
            var questPath = routes.PathFor(LegacyTradeJournalFileKind.Quests, Fixture.Uid);
            WorldSession? receipt = null; Exception? callbackFailure = null; int observed = 0;
            f.OnSend = _ =>
            {
                observed++;
                callbackFailure ??= Record.Exception(() =>
                {
                    var current = f.Current;
                    if (receipt is null) receipt = current; else Assert.Same(receipt, current);
                    Assert.NotSame(before, current); Assert.Same(binding, RequireGraph(f));
                    Assert.Same(actualKeeper, f.Native.Inventory!.ClientItems[keeper.Uid]);
                    Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems), File.ReadAllBytes(f.ItemsPath));
                    Assert.False(LegacyTradeJournal.HasPending(f.Users));
                    Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                    Assert.False(Monitor.IsEntered(f.Trades.SyncRoot));
                    if (flow == "skin")
                    {
                        Assert.Equal("0/1/1/", LegacyDragonSkinState.FromSnapshot(current.Character).ToSnapshotValue());
                        Assert.Equal("0/1/1/", LegacyDragonSkinState.FromSnapshot(
                            CharacterSnapshotParser.ParseText(File.ReadAllText(f.CharacterPath))).ToSnapshotValue());
                    }
                    else
                    {
                        Assert.Equal(originalCharacter, File.ReadAllBytes(f.CharacterPath));
                        // Exercise the ACTUAL current creator. Its global UID
                        // reservation frontier remains open; no allocator is injected.
                        Assert.Single(current.PlayerItems.InventoryItems, row => !oldUids.Contains(row.Uid));
                    }
                    if (flow == "victory")
                    {
                        Assert.True(current.Character.PlayerQuests.TryGetQuest(questId, out var finished));
                        Assert.Equal(PlayerQuestStatus.Finished, finished.Status);
                        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(current.Character.PlayerQuests), File.ReadAllBytes(questPath));
                    }
                });
            };
            byte[] request = flow == "skin"
                ? new LegacyDragonSkinPacket(LegacyDragonSkinType.Unlock, Fixture.Uid, 1, false).ToFrameBytes()
                : new LegacyNpcDialogRequestPacket(flow == "deity" ? 22598u : 21972u, 0, 1, 1, 0, "").ToFrameBytes();
            int first = f.Sent.Count;

            await f.Send(request); f.OnSend = null;

            Assert.Null(callbackFailure); Assert.Equal(2, observed); Assert.NotNull(receipt);
            Assert.Same(receipt, f.Current); Assert.Same(binding, RequireGraph(f));
            var frames = f.Sent.Skip(first).ToArray(); Assert.Equal(2, frames.Length);
            if (flow is "skin" or "deity")
                Assert.Equal(new MsgItemUsagePacket(cost.Uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), frames[0]);
            if (flow == "skin")
            {
                Assert.Equal(new LegacyDragonSkinPacket(LegacyDragonSkinType.Unlock, Fixture.Uid, 1, true).ToFrameBytes(), frames[1]);
                Assert.DoesNotContain(cost.Uid, f.Native.Inventory!.ClientItems.Keys);
            }
            else
            {
                if (flow == "victory")
                    Assert.Equal(LegacyQuestListPacket.CreateSingleQuestFrame(LegacyQuestListMode.FinishQuest,
                        new LegacyQuestListItem(questId, LegacyQuestStatus.Finished, 12345)), frames[0]);
                Assert.True(LegacyGameItemPacket.TryParse(ReadRewardFrame(frames[1]), out var item));
                Assert.Contains(item.ItemId, rewards);
                var actual = Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == item.Uid);
                Assert.DoesNotContain(actual.Uid, oldUids);
                Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(actual).ToFrameBytes(), frames[1]);
                Assert.Equal(actual.ItemId, f.Native.Inventory!.ClientItems[actual.Uid].ITEM_ID);
            }
            Assert.Equal(source, PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems));
            Assert.Equal(oldQuests, PlayerQuestSnapshotLoader.Serialize(before.Character.PlayerQuests));
            var itemsAfter = File.ReadAllBytes(f.ItemsPath); var characterAfter = File.ReadAllBytes(f.CharacterPath);
            var questsAfter = File.ReadAllBytes(questPath); int replay = f.Sent.Count;

            await f.Send(request);

            Assert.Same(receipt, f.Current); RequireGraph(f);
            Assert.Equal(itemsAfter, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(characterAfter, File.ReadAllBytes(f.CharacterPath));
            Assert.Equal(questsAfter, File.ReadAllBytes(questPath));
            Assert.DoesNotContain(f.Sent.Skip(replay), bytes => LegacyGameItemPacket.TryParse(ReadRewardFrame(bytes), out _));
        }, characterLines: new[] { "AnimaSkin=0/0/" }, playerItems: initial, additionalItems: catalogItems,
            npcs: npcs, playerQuests: quests);
    }

    private static ItemType RewardCatalogRow(uint id)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray(); fields[0] = id.ToString(CultureInfo.InvariantCulture);
        fields[1] = "OwnedReward" + fields[0]; fields[ItemType.DurabilityFieldIndex] = "1"; fields[ItemType.StackSizeFieldIndex] = "1";
        return new(checked((int)id), fields[1], fields);
    }
    private static PacketFrame ReadRewardFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame;
    }
}
