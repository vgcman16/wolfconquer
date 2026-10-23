using System.Buffers.Binary;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeEquipmentStatusPacketTests
{
    [Fact]
    public Task WholeStatusCodecWritesAllFortyWordsAndIgnoresUnserializedState() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        string[] fields = ["UID","MaxHitpoints","MaxMana","MaxAttack","MinAttack","Defence","MagicAttack",
            "MagicDefence","Dodge","AgilityAtack","Accuracy","PhysicalPercent","MagicPercent","MDefence",
            "Damage","ItemBless","CriticalStrike","SkillCStrike","Immunity","Penetration","Block",
            "Breakthrough","Counteraction","Detoxication","PhysicalDamageIncrease","MagicDamageIncrease",
            "PhysicalDamageDecrease","MagicDamageDecrease","MetalResistance","WoodResistance","WaterResistance",
            "FireResistance","EarthResistance","PrestigeLevel","RuneScore","DashRate"];
        for (int index = 0; index < fields.Length; index++)
            typeof(LegacyNativeMsgStatus).GetField(fields[index])!.SetValue(fixture.Owner.Status, (uint)(index + 1));
        fixture.Owner.Status.DodgeRate = uint.MaxValue;
        fixture.Owner.Status.Stamp = -1;
        fixture.Owner.Status.MaxVigor = ushort.MaxValue;
        fixture.Owner.Status.TotalAnimaLevel = ushort.MaxValue;
        fixture.Owner.Status.LukyStrike = 999;
        fixture.Owner.Status.Parry = 999;
        fixture.Owner.PerfectionStatus.LuckyStrike = 0.29;
        fixture.Owner.PerfectionStatus.StrikeLock = 25.9;
        Poison(fixture.Packet);

        Assert.Same(fixture.Packet, fixture.Packet.StatusCreate(fixture.Owner));

        uint[] words = [1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,28,2590,0,uint.MaxValue];
        var expected = new byte[164];
        BinaryPrimitives.WriteUInt16LittleEndian(expected, 164);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 2301);
        for (int index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4 + index * 4), words[index]);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(fixture.Packet));
        Assert.Equal(4, fixture.Packet.Position);
        Assert.Equal(164, fixture.Packet.Size);
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 16), Memory(fixture.Packet, 180).Skip(164));
        Assert.Empty(fixture.Queue);
    });

    [Theory]
    [InlineData(0.59, 59u)] [InlineData(-1.5, 4294967146u)]
    [InlineData(double.NaN, 0u)] [InlineData(double.PositiveInfinity, 0u)]
    [InlineData(2147483648.0, 0u)] [InlineData(4294967295.0, 4294967196u)]
    public Task BothPerfectionFieldsRetainNativeSourceShapedScaleThenUnsignedConversion(double value, uint expected) =>
        NativePacketTestThread.Run(() =>
        {
            using var fixture = new NativeSpellTestFixture();
            fixture.Owner.PerfectionStatus.LuckyStrike = value;
            fixture.Owner.PerfectionStatus.StrikeLock = value;
            fixture.Packet.StatusCreate(fixture.Owner);
            var bytes = NativeSpellTestFixture.Bytes(fixture.Packet);
            Assert.Equal(expected, BitConverter.ToUInt32(bytes, 148));
            Assert.Equal(expected, BitConverter.ToUInt32(bytes, 152));
            Assert.Equal(0u, BitConverter.ToUInt32(bytes, 156));
        });

    [Theory]
    [InlineData("user", 237)] [InlineData("status", 4)] [InlineData("perfection", 148)]
    public Task NullOwnersLeaveExactlyTheNativeReadAndWritePrefixWithoutFinalization(string missing, int cursor) =>
        NativePacketTestThread.Run(() =>
        {
            using var fixture = new NativeSpellTestFixture();
            if (missing == "status") fixture.Owner.Status = null!;
            if (missing == "perfection") fixture.Owner.PerfectionStatus = null!;
            Poison(fixture.Packet);
            Assert.Throws<NullReferenceException>(() => fixture.Packet.StatusCreate(missing == "user" ? null! : fixture.Owner));
            Assert.Equal(cursor, fixture.Packet.Position);
            Assert.Equal(321, fixture.Packet.Size);
            byte[] expected = Enumerable.Repeat((byte)0xA5, cursor + 16).ToArray();
            if (missing == "perfection") Array.Clear(expected, 4, 144);
            Assert.Equal(expected, Memory(fixture.Packet, expected.Length));
            Assert.Empty(fixture.Queue);
        });

    [Theory]
    [InlineData(0u, "1400B00808021000180020002800300038004000")]
    [InlineData(1u, "1400B00808021001180020002800300038004000")]
    [InlineData(uint.MaxValue, "1D00B008080210FFFFFFFFFFFFFFFFFF01180020002800300038004000")]
    public Task ServerInfoUsesAllEightRequiredFieldsAndWrapsVigorIntoSignedYear(uint vigor, string hex) =>
        NativePacketTestThread.Run(() =>
        {
            using var fixture = new NativeSpellTestFixture();
            Poison(fixture.Packet);
            Assert.Same(fixture.Packet, fixture.Packet.ServerInfoCreate(vigor));
            Assert.Equal(Convert.FromHexString(hex), NativeSpellTestFixture.Bytes(fixture.Packet));
            Assert.Equal(4, fixture.Packet.Position);
            Assert.Empty(fixture.Queue);
        });

    private static unsafe void Poison(LegacyNativePacket packet)
    {
        new Span<byte>(packet.Memory, LegacyNativePacket.MAX_SIZE).Fill(0xA5);
        packet.Seek(237);
        packet.Size = 321;
    }

    private static unsafe byte[] Memory(LegacyNativePacket packet, int length) => new ReadOnlySpan<byte>(packet.Memory, length).ToArray();
}
