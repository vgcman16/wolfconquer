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
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, data);
        var before = GetSession(handler, connection);
        var itemPath = Path.Combine(_itemsRoot, "1293600.bin");
        var initialItems = File.ReadAllBytes(itemPath);
        var characterPath = Path.Combine(_usersRoot, "1293600.ini");
        var initialCharacter = File.ReadAllBytes(characterPath);
        Assert.Equal(stack, Assert.Single(before.PlayerItems.RuneItems).StackSize);
        var first = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateRuneEquipmentUse(uid, 101, MsgItemUsageAction.EquipRune)));
        var current = GetSession(handler, connection);
        Assert.Equal(initialCharacter, File.ReadAllBytes(characterPath));
        var row = Assert.Single(current.PlayerItems.RuneItems);
        Assert.Equal(stack, row.StackSize);
        if (accepted)
        {
            Assert.Equal(5, sent.Count - first); Assert.Equal((ushort)101, row.Position);
            Assert.Equal(stack, Assert.Single(new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600).RuneItems).StackSize);
            Assert.Contains("committed MsgItemUsage equip rune", log.ToString());
        }
        else
        {
            Assert.Same(before, current); Assert.Equal(first, sent.Count);
            Assert.Equal((ushort)211, row.Position); Assert.Equal(initialItems, File.ReadAllBytes(itemPath));
            Assert.Contains("held MsgItemUsage equip rune", log.ToString());
        }
    }
}
