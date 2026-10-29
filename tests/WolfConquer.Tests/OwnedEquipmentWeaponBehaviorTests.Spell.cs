using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedEquipmentWeaponBehaviorTests
{
    [Fact]
    public Task HandlerItemUsageEquipMovesHookScytheToRightHandWithPerseveranceSpell()
    {
        AssertItemClassifications(511000, 0, 0);
        var graph = new PlayerItemSnapshot([Row(2001, 511000, 0)], [], [], [], []);
        var spells = new PlayerSpellSnapshot([new PlayerSpellSnapshotEntry(10311, 0, 0, 0, 0, 0)]);
        var input = new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(classCode: 7051), graph, spells);
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(OwnedEquipmentBehaviorFixture.Item(511000)), async f =>
        {
            Assert.True(f.Current().PlayerSpells.ContainsSpell(10311));
            var start = f.ActorFrames.Count;
            await f.DispatchAsync(Equip(4));
            var frames = f.ActorFrames.Skip(start).ToArray();
            Assert.Equal(13, frames.Length);
            Usage(frames[0], 2001, 0, MsgItemUsageAction.RemoveInventory);
            ItemFrame(frames[1], 2001, 511000, 4, LegacyGameItemPacket.AddItemMode);
            ItemFrame(frames[2], 2001, 511000, 4, LegacyGameItemPacket.UpdateItemMode);
            LegacySelectedEquipmentQueryAssertions.NeutralQuery(frames, 3, OwnedEquipmentBehaviorFixture.Actor);
            Assert.Contains(f.Current().PlayerItems.EquipmentItems, row => row.Uid == 2001 && row.Position == 4);
            Assert.True(f.Current().PlayerSpells.ContainsSpell(10311));
            var saved = new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(OwnedEquipmentBehaviorFixture.Actor);
            Assert.Contains(saved.EquipmentItems, row => row.Uid == 2001 && row.Position == 4);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
    }
}
