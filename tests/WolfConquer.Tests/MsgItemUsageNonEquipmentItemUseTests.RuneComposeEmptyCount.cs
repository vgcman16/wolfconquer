using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public async Task HandlerRuneComposeZeroCountIgnoresTrailingMaterialsAndReloadsUnchanged()
    {
        await AssertRuneComposePrefixSuccessAsync(
            declaredCount: 0,
            offeredUids: [RuneComposePrefixMaterialAUid, RuneComposePrefixMaterialBUid],
            selectedUids: [],
            expectedItemId: RuneComposePrefixTargetId,
            expectedExperience: 0,
            expectSerializedChange: false);
    }

    [Fact]
    public async Task HandlerRuneComposeFiftyCountIgnoresTrailingMaterialsAndReloadsUnchanged()
    {
        await AssertRuneComposePrefixSuccessAsync(
            declaredCount: 50,
            offeredUids: [RuneComposePrefixMaterialAUid, RuneComposePrefixMaterialBUid],
            selectedUids: [],
            expectedItemId: RuneComposePrefixTargetId,
            expectedExperience: 0,
            expectSerializedChange: false);
    }

    [Fact]
    public async Task HandlerRuneComposeMaxCountIgnoresTrailingMaterialsAndReloadsUnchanged()
    {
        await AssertRuneComposePrefixSuccessAsync(
            declaredCount: uint.MaxValue,
            offeredUids: [RuneComposePrefixMaterialAUid, RuneComposePrefixMaterialBUid],
            selectedUids: [],
            expectedItemId: RuneComposePrefixTargetId,
            expectedExperience: 0,
            expectSerializedChange: false);
    }

    // This explicit target/count builder also corrects only the old compose request fixture.
    // The global CreateRuneEquipmentUse count-zero helper remains unchanged for its other callers.
    private static byte[] CreateRuneComposeDeclaredCountUse(
        uint targetUid, uint declaredCount, params uint[] offeredUids)
    {
        var writer = new PacketWriter(
            WorldPacketOpcodes.MsgItemUsage,
            MsgItemUsagePacket.PayloadLength + (offeredUids.Length * sizeof(uint)));
        writer.WriteUInt32(0, targetUid);
        writer.WriteUInt64(4, 0);
        writer.WriteUInt16(12, (ushort)MsgItemUsageAction.ComposeRune);
        writer.WriteUInt32(14, 511_043_495);
        writer.WriteUInt32(18, declaredCount);
        writer.WriteUInt32(22, 0);
        writer.WriteUInt32(26, 0);
        for (var index = 0; index < offeredUids.Length; index++)
        {
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + (index * sizeof(uint)), offeredUids[index]);
        }
        return writer.ToArray();
    }
}
