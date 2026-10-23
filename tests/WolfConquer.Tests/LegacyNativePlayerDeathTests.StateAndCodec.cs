using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerDeathTests
{
    [Fact]
    public Task DeathSlotsRetainRealZeroBirthAndSoulSpellBelongsToOwningClient() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        Assert.Equal(0, s.Player.SlayerPercent); Assert.Equal((ushort)0, s.Player.Dead_X); Assert.Equal((ushort)0, s.Player.Dead_Y);
        Assert.False(s.Player.GetPkPkPoints); Assert.False(s.Player.InElitePk); Assert.Equal(0u, s.Player.KillerPkPoints); Assert.Equal(0u, s.Player.TeamDeathMacthKills);
        Assert.Equal(default, s.Player.GhostStamp); Assert.Equal(default, s.Player.DeadStamp);
        Assert.Equal(default, s.Player.AbsolutionStamp); Assert.Equal(default, s.Player.NeptunesCurse); Assert.Equal(default, s.Player.SlayerStamp);
        Assert.Same(s.Client, s.Player.Owner);
        Assert.Equal((ushort)0, s.Client.OnSoulSpell);
        s.Client.OnSoulSpell = 31;
        Assert.Equal((ushort)31, s.Player.Owner.OnSoulSpell);
    });
    [Fact]
    public Task NoDropListKeepsSourceValuesAndSeparateNativeListIdentity() => NativePacketTestThread.Run(() =>
    {
        Assert.Equal(new uint[] { 1764, 700, 3954, 3820 }, LegacyNativeConstants.NoDropItems);
        Assert.NotSame(LegacyNativeConstants.NoDropItems, LegacyNativeConstants.FreePkMap);
    });
    [Theory]
    [InlineData(0u, 0)] [InlineData(1u, 1)] [InlineData(65535u, 65535)]
    [InlineData(65536u, 0)] [InlineData(2147483647u, 65535)] [InlineData(uint.MaxValue, 65535)]
    public Task JiangKillMessageUsesUInt16NarrowingAndResetsExistingWriter(uint spell, ushort expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Packet.Seek(100); Assert.Same(s.Packet, s.Packet.CreateJiangMessage(spell));
        Assert.Equal(new byte[] { 6, 0, 5, 9, (byte)expected, (byte)(expected >> 8) }, NativeSpellTestFixture.Bytes(s.Packet)); Assert.Empty(s.Queue);
    });
    [Fact]
    public Task JiangMessageNullReceiverRetainsNativeFault() => NativePacketTestThread.Run(() =>
        Assert.Throws<NullReferenceException>(() => LegacyNativeMsgJiangMessage.CreateJiangMessage(null!, 1)));
}
