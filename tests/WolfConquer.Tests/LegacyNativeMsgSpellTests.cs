using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeMsgSpellTests
{
    [Fact]
    public Task SpellPacketRoundtripsTheIndependentFull21BytePayload() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        var source = new LegacyNativeMsgSpell { Experience = -2, ID = 0x1234, UnKnow = 0x5678,
            Level = 0x9ABC, Type = 0xDEF0, UseSoul = (LegacyNativeMsgSpell.UseSpellSoulTyp)0x11223344,
            Soul = 0x55, HashLevelSoul = 0x66778899, PreviousLevel = 7 };
        Assert.Same(f.Packet, f.Packet.SpellCreate(source));
        Assert.Equal(25, f.Packet.Size);
        Assert.Equal(4, f.Packet.Position);
        var bytes = NativeSpellTestFixture.Bytes(f.Packet);
        Assert.Equal(Convert.FromHexString("19003508FEFFFFFF34127856BC9AF0DE443322115599887766"), bytes);
        var parsed = NativeSpellTestFixture.ReadSpell(bytes);
        Assert.NotSame(source, parsed);
        Assert.Equal(-2, parsed.Experience);
        Assert.Equal((ushort)0x1234, parsed.ID);
        Assert.Equal((ushort)0x5678, parsed.UnKnow);
        Assert.Equal((ushort)0x9ABC, parsed.Level);
        Assert.Equal((ushort)0xDEF0, parsed.Type);
        Assert.Equal(0x11223344U, (uint)parsed.UseSoul);
        Assert.Equal((byte)0x55, parsed.Soul);
        Assert.Equal(0x66778899, parsed.HashLevelSoul);
        Assert.Equal((byte)0, parsed.SoulLevel);
        Assert.Equal((byte)0, parsed.PreviousLevel);
    });

    [Fact]
    public Task SoulLevelFlagsRemainStickyWhenLevelsDecreaseOrReachZero() => NativePacketTestThread.Run(() =>
    {
        var spell = new LegacyNativeMsgSpell();
        Assert.Equal(default(DateTime), spell.ColdTime);
        Assert.Equal(default(DateTime), spell.StampSpell);
        Assert.False(spell.IsSpellWithColdTime);
        spell.SoulLevel = 1;
        Assert.Equal(2, spell.HashLevelSoul);
        spell.SoulLevel = 2;
        Assert.Equal(18, spell.HashLevelSoul);
        spell.SoulLevel = 3;
        Assert.Equal(274, spell.HashLevelSoul);
        spell.SoulLevel = 4;
        Assert.Equal(65810, spell.HashLevelSoul);
        spell.SoulLevel = 0;
        Assert.Equal((byte)0, spell.SoulLevel);
        Assert.Equal(65810, spell.HashLevelSoul);
        spell.HashLevelSoul |= 0x40000000;
        spell.SoulLevel = 255;
        Assert.Equal(0x40010112, spell.HashLevelSoul);
        Assert.Equal((byte)255, spell.SoulLevel);
    });

    [Theory]
    [InlineData(0U, (byte)0, 0U)]
    [InlineData(9U, (byte)1, 9U)]
    [InlineData(12U, (byte)2, 12U)]
    [InlineData(16U, (byte)3, 16U)]
    [InlineData(24U, (byte)4, 24U)]
    [InlineData(17U, (byte)0, 0U)]
    [InlineData(uint.MaxValue, (byte)0, 0U)]
    public Task UseSoulGetterAndSetterKeepNativeRawValueMappings(uint raw, byte decoded, uint reencoded) => NativePacketTestThread.Run(() =>
    {
        var spell = new LegacyNativeMsgSpell { UseSoul = (LegacyNativeMsgSpell.UseSpellSoulTyp)raw };
        Assert.Equal(decoded, spell.UseSpellSoul);
        spell.UseSpellSoul = decoded;
        Assert.Equal(reencoded, (uint)spell.UseSoul);
        spell.UseSpellSoul = 255;
        Assert.Equal(LegacyNativeMsgSpell.UseSpellSoulTyp.None, spell.UseSoul);
    });

    [Fact]
    public Task ColdTimeReadsTheGenuineSignedTimeHelperWithoutChangingState() => NativePacketTestThread.Run(() =>
    {
        var cold = DateTime.Now.AddSeconds(30);
        var spell = new LegacyNativeMsgSpell { ColdTime = cold };
        Assert.InRange(spell.GetColdTime, 29000, 31000);
        Assert.Equal(cold, spell.ColdTime);
        Assert.False(spell.IsSpellWithColdTime);
    });

    [Theory]
    [InlineData((byte)0, 9U, false)]
    [InlineData((byte)2, 12U, true)]
    [InlineData((byte)0, uint.MaxValue, true)]
    [InlineData((byte)0, 0U, true)]
    public Task HandlerUsesSameCachedObjectAndEchoesIncomingFieldsOnlyWhenGuardAllows(
        byte soulLevel, uint incomingSoul, bool accepted) => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        var stored = new LegacyNativeMsgSpell { ID = 1000, Level = 8, Experience = 123,
            SoulLevel = soulLevel, UseSoul = LegacyNativeMsgSpell.UseSpellSoulTyp.Four };
        f.Storage.ClientSpells[1000] = stored;
        var incoming = new LegacyNativeMsgSpell { ID = 1000, Level = 99, Experience = -7,
            Type = 77, Soul = 65, HashLevelSoul = 321, UseSoul = (LegacyNativeMsgSpell.UseSpellSoulTyp)incomingSoul };
        f.Packet.SpellCreate(incoming); // Finalize leaves native cursor at4 for the handler.

        LegacyNativeMsgSpell.HandlerSpell(f.Owner, f.Packet);

        Assert.Same(stored, f.Storage.ClientSpells[1000]);
        Assert.Equal((ushort)8, stored.Level);
        Assert.Equal(123, stored.Experience);
        Assert.Equal(soulLevel, stored.SoulLevel);
        Assert.Equal(accepted ? incomingSoul : 24U, (uint)stored.UseSoul);
        Assert.Equal(accepted ? 1 : 0, f.Queue.Count);
        if (!accepted) { Assert.Equal(25, f.Packet.Position); return; }
        var echoed = NativeSpellTestFixture.ReadSpell(f.Queue.Dequeue());
        Assert.Equal((ushort)3, echoed.Type);
        Assert.Equal((ushort)99, echoed.Level);
        Assert.Equal(-7, echoed.Experience);
        Assert.Equal((byte)65, echoed.Soul);
        Assert.Equal(321, echoed.HashLevelSoul);
        Assert.Equal(incomingSoul, (uint)echoed.UseSoul);
        Assert.Equal((ushort)77, incoming.Type);
    });

    [Fact]
    public Task HandlerMissingIdDoesNotPublishOrSend() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        f.Packet.SpellCreate(new LegacyNativeMsgSpell { ID = 1000, UseSpellSoul = 1 });
        LegacyNativeMsgSpell.HandlerSpell(f.Owner, f.Packet);
        Assert.Empty(f.Storage.ClientSpells);
        Assert.Empty(f.Queue);
        Assert.Equal(25, f.Packet.Position);
    });

    [Fact]
    public Task HandlerDoesNotRepairAnUnbornSpellOwnerAfterReadingThePayload() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        f.Owner.MySpells = null;
        f.Packet.SpellCreate(new LegacyNativeMsgSpell { ID = 1000 });
        Assert.Throws<NullReferenceException>(() => LegacyNativeMsgSpell.HandlerSpell(f.Owner, f.Packet));
        Assert.Null(f.Owner.MySpells);
        Assert.Empty(f.Queue);
        Assert.Equal(25, f.Packet.Position);
    });
}
