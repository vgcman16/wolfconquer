using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Item = WolfConquer.Core.Data.PlayerItemSnapshotItem;

namespace WolfConquer.Tests;

public sealed partial class LegacyItemRepairPersistenceTests
{
    [Fact]
    public void RetainedPhysicalRootChildAndPerfectionOrderPaddingAndTextTailAreNeverNormalized()
    {
        var root = Row(Target, children: new[] { Row(105), Row(99) }); root.OwnerName = "owner"; root.Signature = "sig";
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(Items(root, Row(97)));
        // Four physical rows, one perfection. Deliberate child encounter order105,99.
        for (int index = 0; index < 4; index++) { bytes[4 + index * 184 + 14] = (byte)(0xD0 + index); bytes[4 + index * 184 + 80] = 0xF3; }
        bytes[^1] = 0xE1; // Beyond the signature NUL: opaque80 tail, not rewritten UTF8.
        var before = PlayerItemSnapshotLoader.Parse(bytes); var after = Repaired(before);
        var original = bytes.ToArray(); var expected = bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(4 + 8), 1000);
        Assert.Equal(expected, LegacyItemRepairPersistence.PrepareItemImage(bytes, before, after, Target, LegacyItemRepairPayment.Money));
        Assert.Equal(original, bytes); Assert.Equal(105u, BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(4 + 184)));
        Assert.Equal(99u, BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(4 + 2 * 184)));
    }

    [Fact]
    public void OpaqueZeroValuedPerfectionRowSurvivesEvenWhenDtoSerializerWouldOmitIt()
    {
        var original = PlayerItemSnapshotLoader.ToLegacyBytes(Items(Row(Target)));
        var bytes = new byte[original.Length + 80]; original.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + 184), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(original.Length), Target);
        var before = PlayerItemSnapshotLoader.Parse(bytes);
        Assert.Equal(original.Length, PlayerItemSnapshotLoader.ToLegacyBytes(before).Length);
        var expected = bytes.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(12), 1000);
        Assert.Equal(expected, LegacyItemRepairPersistence.PrepareItemImage(bytes, before, Repaired(before), Target, LegacyItemRepairPayment.Money));
    }

    [Fact]
    public void RemovedMaterialOwnPerfectionIsSplicedWithoutTouchingRetainedPerfections()
    {
        var target = Row(Target, durability: 0); target.OwnerName = "target";
        var material = Row(92, 1088001, stack: 5); material.OwnerName = "material";
        var raw = PlayerItemSnapshotLoader.ToLegacyBytes(Items(target, material, Row(93)));
        int oldHeader = 4 + 3 * 184, oldPerf = oldHeader + 4;
        raw[oldPerf + 79] = 0xB1; raw[oldPerf + 80 + 79] = 0xB2;
        var before = PlayerItemSnapshotLoader.Parse(raw); var after = Repaired(before, new() { [92] = null });
        var result = LegacyItemRepairPersistence.PrepareItemImage(raw, before, after, Target, LegacyItemRepairPayment.Meteors);
        var expected = new byte[raw.Length - 184 - 80];
        raw.AsSpan(0, 4 + 184).CopyTo(expected);
        raw.AsSpan(4 + 2 * 184, 184 + 4 + 80).CopyTo(expected.AsSpan(4 + 184));
        BinaryPrimitives.WriteInt32LittleEndian(expected, 2); BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(12), 1000);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4 + 2 * 184), 1);
        Assert.Equal(expected, result); Assert.Equal((byte)0xB1, result[^1]);
    }

    [Theory]
    [InlineData("missing-tail")]
    [InlineData("trailing")]
    [InlineData("negative-count")]
    [InlineData("duplicate")]
    [InlineData("nested")]
    [InlineData("foreign-perfection")]
    public void CompleteMalformedRawInputRefusesWithoutChangingCallerBytes(string fault)
    {
        var before = Items(Row(Target), Row(92)); var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        if (fault == "missing-tail") Array.Resize(ref bytes, bytes.Length - 1);
        if (fault == "trailing") Array.Resize(ref bytes, bytes.Length + 1);
        if (fault == "negative-count") BinaryPrimitives.WriteInt32LittleEndian(bytes, -1);
        if (fault == "duplicate") BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + 184), Target);
        if (fault == "nested")
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + 136), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + 184 + 136), 1);
        }
        if (fault == "foreign-perfection")
        {
            int header = 4 + 2 * 184; Array.Resize(ref bytes, bytes.Length + 80);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(header), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(header + 4), 999);
        }
        var original = bytes.ToArray();
        Assert.Throws<FormatException>(() => LegacyItemRepairPersistence.PrepareItemImage(bytes, before,
            Repaired(before), Target, LegacyItemRepairPayment.Money));
        Assert.Equal(original, bytes);
    }

    [Theory]
    [InlineData("not-needed")]
    [InlineData("maximum-changed")]
    [InlineData("unrelated-durability")]
    [InlineData("position")]
    [InlineData("perfection")]
    [InlineData("new-item")]
    public void PaidProjectionRejectsEveryNonRepairMutation(string fault)
    {
        var before = Items(Row(Target, durability: fault == "not-needed" ? (ushort)1000 : (ushort)250), Row(92));
        var after = Repaired(before);
        if (fault == "maximum-changed") after = Items(new Item { Uid = Target, ItemId = 410009, Durability = 1001, MaximumDurability = 1001, StackSize = 1 }, Row(92));
        if (fault == "unrelated-durability") after = Items(Row(Target, durability: 1000), Row(92, durability: 300));
        if (fault == "position") after = Items(Row(Target, durability: 1000, position: 4), Row(92));
        if (fault == "perfection") after.InventoryItems[0].OwnerName = "changed";
        if (fault == "new-item") after = Items(Row(Target, durability: 1000), Row(92), Row(93));
        var raw = PlayerItemSnapshotLoader.ToLegacyBytes(before); var original = raw.ToArray();
        Assert.Throws<InvalidDataException>(() => LegacyItemRepairPersistence.PrepareItemImage(raw, before, after, Target, LegacyItemRepairPayment.Money));
        Assert.Equal(original, raw);
    }

    [Theory]
    [InlineData("four-units")]
    [InlineData("six-units")]
    [InlineData("wrong-type")]
    [InlineData("deposits")]
    [InlineData("target-as-material")]
    [InlineData("zero-stack-retained")]
    public void MeteorProjectionRefusesUnsafeOrIncompletePhysicalCost(string fault)
    {
        Item target = Row(Target, fault == "target-as-material" ? 1088001u : 410009u, durability: 0, stack: 5);
        Item material = Row(92, fault == "wrong-type" ? 410009u : 1088002u,
            stack: fault == "four-units" ? (ushort)4 : fault == "six-units" ? (ushort)6 : (ushort)5,
            children: fault == "deposits" ? new[] { Row(99) } : null);
        var before = Items(target, material);
        var after = fault == "target-as-material" ? Items(Row(Target, 1088001, durability: 1000, stack: 0), material) :
            fault == "zero-stack-retained" ? Repaired(before, new() { [92] = 0 }) : Repaired(before, new() { [92] = null });
        var raw = PlayerItemSnapshotLoader.ToLegacyBytes(before); var original = raw.ToArray();
        Assert.Throws<InvalidDataException>(() => LegacyItemRepairPersistence.PrepareItemImage(raw, before, after, Target, LegacyItemRepairPayment.Meteors));
        Assert.Equal(original, raw);
    }
}
