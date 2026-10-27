using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Existing APIs and MAX_SIZE owned buffers only: safe for a tests-only baseline.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativePreparedNinjaCodecTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("TQServer", "5451536572766572")]
    [InlineData("龙ABCDEFG", "E9BE994142434445")]
    public Task SpellCreatePreservesTheFullFixedPayloadAndFirstEightUtf8SealBytes(string? seal, string suffix) =>
        NativePacketTestThread.Run(() =>
        {
            using var fixture = new NativeSpellTestFixture();
            if (seal is not null) WolfConquer.Core.Networking.LegacyNativePacket.SealString = seal;
            var spell = new LegacyNativeMsgSpell { Experience = -2, ID = 0x1234, UnKnow = 0x5678,
                Level = 0x9ABC, Type = 0xDEF0, UseSoul = (LegacyNativeMsgSpell.UseSpellSoulTyp)0x11223344,
                Soul = 0x55, HashLevelSoul = 0x66778899 };
            fixture.Packet.Size = 321;
            fixture.Packet.Seek(237);
            Assert.Same(fixture.Packet, fixture.Packet.SpellCreate(spell));
            Assert.Equal(Convert.FromHexString("19003508FEFFFFFF34127856BC9AF0DE443322115599887766" + suffix),
                NativeSpellTestFixture.Bytes(fixture.Packet));
            Assert.Equal(4, fixture.Packet.Position);
            Assert.Equal(seal is null ? 25 : 33, fixture.Packet.Size);
            Assert.Empty(fixture.Queue);
        });

    [Fact]
    public Task NullMessagesRefuseWithoutChangingExistingPacketBytesSizeOrCursor() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        byte[] before = Enumerable.Repeat((byte)0xA5, fixture.Packet.Capacity).ToArray();
        unsafe { Marshal.Copy(before, 0, (nint)fixture.Packet.Memory, before.Length); }
        fixture.Packet.Size = 321;
        fixture.Packet.Seek(237);
        Assert.Throws<ArgumentNullException>(() => fixture.Packet.SpellCreate(null!));
        Assert.Throws<ArgumentNullException>(() => fixture.Packet.CreateNinjaUser(null!));
        Assert.Equal(321, fixture.Packet.Size);
        Assert.Equal(237, fixture.Packet.Position);
        unsafe { Assert.Equal(before, new ReadOnlySpan<byte>(fixture.Packet.Memory, before.Length).ToArray()); }
        Assert.Empty(fixture.Queue);
    });

    [Fact]
    public void NullPacketReceiversRefuseBeforePreparation()
    {
        Assert.Throws<InvalidOperationException>(() => LegacyNativeSpellPacket.SpellCreate(null!, new()));
        Assert.Throws<InvalidOperationException>(() => LegacyNativeMsgNinja.CreateNinjaUser(null!, new()));
    }
}
