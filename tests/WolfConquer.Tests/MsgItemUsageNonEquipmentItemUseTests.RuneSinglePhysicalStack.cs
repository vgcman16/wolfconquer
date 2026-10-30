using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using WolfConquer.Core.Packets;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Theory]
    [InlineData((ushort)0, true)] [InlineData((ushort)1, true)] [InlineData((ushort)2, false)]
    public async Task RunePhysicalSlotAcceptsNativeSingleUnitEncodingsWithoutRewritingRawStack(ushort stack, bool accepted)
    {
        const uint uid = 5_337_530, itemId = 4_010_501;
        var data = CreateWorldDataCatalog(CreateItemType(itemId, "SinglePhysicalRune", durability: 1));
        WriteSnapshot(1_293_600, 1_293_600, 1_015, 826, 565, minHitPoints: 10_000);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(uid, itemId, 211, stackSize: stack));
        await WithOwnedRuneDropAsync(data, async f =>
        {
            const uint actor = 1_293_600;
            var before = f.Current(actor); var initial = f.Data.Files();
            var initialItems = File.ReadAllBytes(f.ItemPath(actor));
            var initialCharacter = File.ReadAllBytes(f.CharacterPath(actor));
            Assert.Equal(stack, Assert.Single(before.PlayerItems.RuneItems).StackSize);
            int first = f.Frames(actor).Count;
            await f.SendAsync(actor, CreateRuneEquipmentUse(uid, 101, MsgItemUsageAction.EquipRune));
            var current = f.Current(actor);
            Assert.Equal(initialCharacter, File.ReadAllBytes(f.CharacterPath(actor)));
            var row = Assert.Single(current.PlayerItems.RuneItems); Assert.Equal(stack, row.StackSize);
            Assert.Equal(before.PlayerSpells.Spells, current.PlayerSpells.Spells);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(current.NativeGameClient!.OrdinarySpellBinding!));
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            if (accepted)
            {
                Assert.Equal(5, f.Frames(actor).Count - first); Assert.Equal((ushort)101, row.Position);
                var expected = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems with
                    { RuneItems = new[] { Assert.Single(before.PlayerItems.RuneItems).WithPosition(101) } });
                initial[Path.GetRelativePath(f.Data.Root, f.ItemPath(actor))] = expected;
                f.Data.AssertFiles(initial);
                Assert.Equal(stack, Assert.Single(new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(actor).RuneItems).StackSize);
                Assert.Contains("committed MsgItemUsage equip rune", f.Log.ToString());
            }
            else
            {
                Assert.Same(before, current); Assert.Equal(first, f.Frames(actor).Count);
                Assert.Equal((ushort)211, row.Position); Assert.Equal(initialItems, File.ReadAllBytes(f.ItemPath(actor)));
                f.Data.AssertFiles(initial); Assert.Contains("held MsgItemUsage equip rune", f.Log.ToString());
            }
        }, 1_293_600);
    }
}
