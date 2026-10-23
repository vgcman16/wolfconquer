using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLifecycleTests
{
    [Theory]
    [InlineData(0u, 0u, true)] [InlineData(7u, 99u, true)] [InlineData(uint.MaxValue, 1u, true)]
    [InlineData(7u, 99u, false)]
    public Task ProtectionCommitsOnlyWhenRequestedAndWritesAllFiveUnpackedValues(uint time, uint prior, bool update)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.ExpProtection = prior;
        s.Player.CreateExpProtection(s.Packet, time, update);
        uint expected = update ? time : prior; Assert.Equal(expected, s.Player.ExpProtection);
        Assert.Equal(Update(17, 73, 0, 0, expected, 0, 0, 0), Assert.Single(s.Queue));
        Assert.Equal(4, s.Packet.Position);
        if (time == 7 && update) Assert.Equal("140080080811120C084918001807180018001800", Convert.ToHexString(s.Queue.Peek()));
    });

    [Theory]
    [InlineData(0u, 1u)] [InlineData(3u, 2u)] [InlineData(uint.MaxValue, uint.MaxValue)]
    public Task ExtraExperienceWritesRawTimerAndWrappingRateWithoutMutatingThem(uint timer, uint rate)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.DExpTime = timer; s.Player.RateExp = rate;
        s.Player.CreateExtraExpPacket(s.Packet);
        Assert.Equal(Update(17, 18, 0, 0, timer, 0, unchecked(rate * 100), 0), Assert.Single(s.Queue));
        Assert.Equal(timer, s.Player.DExpTime); Assert.Equal(rate, s.Player.RateExp);
        if (timer == 3) Assert.Equal("150080080811120D081218001803180018C8011800", Convert.ToHexString(s.Queue.Peek()));
    });

    [Fact]
    public Task DirectPacketWritersRetainNativeCursorRatherThanSilentlyResettingIt() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Packet.Seek(237); s.Player.CreateExpProtection(s.Packet, 7);
        byte[] bytes = Assert.Single(s.Queue); byte[] normal = Update(17, 73, 0, 0, 7, 0, 0, 0);
        Assert.Equal(237 + normal.Length - 4, bytes.Length);
        Assert.All(bytes.Skip(4).Take(233), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(normal.AsSpan(4).ToArray(), bytes.AsSpan(237).ToArray()); Assert.Equal(4, s.Packet.Position);
    });

    [Fact]
    public Task ProtectionNullStreamFaultsAfterMutationAndFalseUpdatePreservesPrestate() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.ExpProtection = 3;
        Assert.Throws<NullReferenceException>(() => s.Player.CreateExpProtection(null!, 7)); Assert.Equal(7u, s.Player.ExpProtection);
        Assert.Throws<NullReferenceException>(() => s.Player.CreateExpProtection(null!, 99, false)); Assert.Equal(7u, s.Player.ExpProtection);
        Assert.Throws<NullReferenceException>(() => s.Player.CreateExtraExpPacket(null!)); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(0, 0)] [InlineData(900, 900)] [InlineData(901, 901)] [InlineData(902, 900)] [InlineData(65535, 900)]
    public Task ExtraAttributeWholePropertyKeepsItsExactNineHundredOneBoundary(ushort input, ushort expected)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.ExtraAtributes = input; Assert.Equal(expected, s.Player.ExtraAtributes); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(800, 100, 1, 801, 101)] [InlineData(900, 100, 1, 901, 101)]
    [InlineData(901, 65535, 1, 900, 0)] [InlineData(900, 100, 65535, 899, 99)]
    public Task AttributeGrantUsesRealClampingPropertyAndSeparateUInt16WrapBeforePacket(ushort extra, ushort attributes,
        ushort grant, ushort expectedExtra, ushort expectedAttributes) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.ExtraAtributes = extra; s.Player.Atributes = attributes;
        s.Player.AddExtraAtributes(s.Packet, grant);
        Assert.Equal(expectedExtra, s.Player.ExtraAtributes); Assert.Equal(expectedAttributes, s.Player.Atributes);
        Assert.Equal(Update(17, 10, expectedAttributes), Assert.Single(s.Queue));
    });

    [Fact]
    public Task AttributeGrantNullStreamRetainsBothPhysicalMutationsBeforeFault() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.ExtraAtributes = 1; s.Player.Atributes = 2;
        Assert.Throws<NullReferenceException>(() => s.Player.AddExtraAtributes(null!, 3));
        Assert.Equal(4, s.Player.ExtraAtributes); Assert.Equal(5, s.Player.Atributes); Assert.Empty(s.Queue);
    });
}
