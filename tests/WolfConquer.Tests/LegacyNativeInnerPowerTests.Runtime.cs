using System.Buffers.Binary;
using WolfConquer.Core.World;
using Inner = WolfConquer.Core.World.LegacyNativeInnerPowerRuntime;
using Attr = WolfConquer.Core.Data.LegacyNativeInnerPowerTable.AtributeType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInnerPowerTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(50u)]
    [InlineData(100u)]
    [InlineData(150u)]
    [InlineData(uint.MaxValue)]
    public Task EquipmentAddsEveryNativeFieldWithMultiplicationBeforeDivision(uint percent) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var native = new NativeSpellTestFixture();
        var inner = new Inner("Owner", 1);
        for (byte type = 1; type <= 14; type++) inner.AddAtributes((Attr)type, (uint)(type * 101));
        var equipment = new LegacyNativeEquipItemStorage(native.Owner);
        var status = native.Owner.Status;
        status.CriticalStrike = 9; status.MaxAttack = 7; status.MinAttack = 6; status.MDefence = 44;
        equipment.AddInnerPowerAtributes(inner, percent);
        uint Apply(uint value) => unchecked(value * percent) / 100;
        Assert.Equal(unchecked(9 + Apply(1010)), status.CriticalStrike);
        Assert.Equal(Apply(1111), status.SkillCStrike); Assert.Equal(Apply(1212), status.Immunity);
        Assert.Equal(Apply(1414), status.Counteraction); Assert.Equal(Apply(1313), status.Breakthrough);
        Assert.Equal(Apply(101), status.MaxHitpoints); Assert.Equal(unchecked(7 + Apply(202)), status.MaxAttack);
        Assert.Equal(unchecked(6 + Apply(202)), status.MinAttack); Assert.Equal(Apply(303), status.MagicAttack);
        Assert.Equal(Apply(505), status.MagicDefence); Assert.Equal(Apply(606), status.PhysicalDamageIncrease);
        Assert.Equal(Apply(808), status.PhysicalDamageDecrease); Assert.Equal(Apply(707), status.MagicDamageIncrease);
        Assert.Equal(Apply(909), status.MagicDamageDecrease); Assert.Equal(Apply(404), status.Defence);
        Assert.Equal(44u, status.MDefence); Assert.Empty(native.Queue); Assert.Null(native.Owner.Player.InnerPower);
    });

    [Fact]
    public Task EquipmentDoesNotAutoRefreshInnerStatusAndNullsRetainNativeFaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(Definition(1, 1)); using var native = new NativeSpellTestFixture();
        var inner = new Inner("Owner", 1); inner.Stages[0].UnLocked = true;
        var equipment = new LegacyNativeEquipItemStorage(native.Owner);
        equipment.AddInnerPowerAtributes(inner); Assert.Equal(0u, native.Owner.Status.MaxHitpoints);
        Assert.Throws<NullReferenceException>(() => equipment.AddInnerPowerAtributes(null!, 0));
        Assert.Throws<NullReferenceException>(() => new LegacyNativeEquipItemStorage(null).AddInnerPowerAtributes(inner, 0));
    });

    [Theory]
    [InlineData(3, 4, 7, "0C00800808111204084D1007")]
    [InlineData(int.MaxValue, 1, int.MinValue, "150080080811120D084D1080808080F8FFFFFFFF01")]
    public Task PotencyMutatesBeforeSendingActualTypedUpdate(int start, int amount, int expected, string hex) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var native = new NativeSpellTestFixture();
        var inner = new Inner("DifferentUid", 99) { Potency = start };
        native.Packet.InitWriter();
        inner.AddPotency(native.Packet, native.Owner, amount);
        Assert.Equal(expected, inner.Potency);
        byte[] frame = Assert.Single(native.Queue);
        Assert.Equal(hex, Convert.ToHexString(frame));
        Assert.Equal((ushort)2176, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        Assert.Null(native.Owner.Player.InnerPower); // Recipient is argument, not the InnerPower UID.
    });

    [Fact]
    public Task PotencyKeepsMutationWhenRecipientOrPacketFaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var native = new NativeSpellTestFixture();
        var inner = new Inner("Owner", 1);
        Assert.Throws<NullReferenceException>(() => inner.AddPotency(native.Packet, null!, 5)); Assert.Equal(5, inner.Potency);
        Assert.Throws<NullReferenceException>(() => inner.AddPotency(null!, native.Owner, 6)); Assert.Equal(11, inner.Potency);
        Assert.Empty(native.Queue);
    });
}
