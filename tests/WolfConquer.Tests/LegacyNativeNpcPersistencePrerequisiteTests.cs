using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeNpcPersistencePrerequisiteTests
{
    [Fact]
    public void RealNpcFactoryIsDistinctFromTheNativeImplicitZeroConstruction()
    {
        var zero = new LegacyNativeNpcRuntime();
        var created = LegacyNativeNpcRuntime.Create();
        Assert.Equal(default, zero.Respawn); Assert.Equal((ushort)0, zero.Leng);
        Assert.False(zero.Alive); Assert.Equal(LegacyNativeMapObjectType.Player, zero.ObjType);
        Assert.Equal((ushort)26, created.Leng); Assert.Equal((ushort)2210, created.PacketID);
        Assert.True(created.Alive); Assert.False(created.AllowDynamic);
        Assert.Equal(LegacyNativeMapObjectType.Npc, created.ObjType);
        Assert.False(created.IsTrap());
        Assert.False(typeof(ILegacyNativeMapObj).IsAssignableFrom(typeof(LegacyNativeNpcRuntime)));
    }

    [Theory]
    [InlineData((ushort)0, "1400A20808011002180020032804300538064007")]
    [InlineData((ushort)8, "1400A20808011002180020032804300838064007")]
    public Task WholeAttributedSpawnWriterAndReaderUseTheActualNpcOwnerWithoutFactoryDefaults(ushort mesh, string expected) => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        var npc = new LegacyNativeNpcRuntime { UID = 1, UnKnow = 2, X = 3, Y = 4, Mesh = 5, NpcType = (LegacyNativeNpcType)6, Sort = 7 };
        fixture.Packet.NpcCreate(npc, mesh);
        Assert.Equal(Convert.FromHexString(expected), NativeSpellTestFixture.Bytes(fixture.Packet));
        fixture.Packet.Seek(237); // Genuine generic reader restarts at header itself.
        fixture.Packet.GetNpc(out var read);
        Assert.Equal(20, fixture.Packet.Position);
        Assert.Equal(1u, read.UID); Assert.Equal(2u, read.UnKnow);
        Assert.Equal((ushort)3, read.X); Assert.Equal((ushort)4, read.Y);
        Assert.Equal(mesh == 0 ? (ushort)5 : mesh, read.Mesh);
        Assert.Equal((LegacyNativeNpcType)6, read.NpcType); Assert.Equal((ushort)7, read.Sort);
        Assert.False(read.Alive); Assert.Equal((ushort)0, read.PacketID);
    });

    [Fact]
    public Task NativeGetArrayMutatesUnknownBeforeSerializingAndNullWriterStillRetainsThatMutation() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        var npc = new LegacyNativeNpcRuntime { UID = 1, UnKnow = 2, X = 3, Y = 4, Mesh = 5, NpcType = (LegacyNativeNpcType)6, Sort = 7 };
        Assert.Same(fixture.Packet, npc.GetArray(fixture.Packet, true, true));
        Assert.Equal(1u, npc.UnKnow);
        Assert.Equal(Convert.FromHexString("1400A20808011001180020032804300538064007"), NativeSpellTestFixture.Bytes(fixture.Packet));
        npc.UID = 9; npc.UnKnow = 8;
        Assert.Throws<NullReferenceException>(() => npc.GetArray(null!, false, false));
        Assert.Equal(9u, npc.UnKnow);
        fixture.Packet.Seek(237); fixture.Packet.Size = 321;
        Assert.Throws<NullReferenceException>(() => fixture.Packet.NpcCreate(null!));
        Assert.Equal(4, fixture.Packet.Position); Assert.Equal(321, fixture.Packet.Size);
    });

    [Theory]
    [InlineData((ushort)119, (ushort)119, true)] [InlineData((ushort)120, (ushort)100, false)]
    [InlineData((ushort)81, (ushort)81, true)] [InlineData((ushort)80, (ushort)100, false)]
    public void WholeCanSeeUsesActualNativeMapObjectAndInclusiveNineteenTileDistance(ushort x, ushort y, bool expected)
    {
        var npc = new LegacyNativeNpcRuntime { UID = 1, Map = 1002, DynamicID = 7, X = 100, Y = 100 };
        var player = new LegacyNativeGameClientRuntime().Player;
        player.UID = 2; player.Map = 1002; player.DynamicID = 7; player.X = x; player.Y = y;
        Assert.Equal(expected, npc.CanSee(player));
        player.Map = 1003; Assert.False(npc.CanSee(player));
        player.Map = 1002; player.DynamicID = 8; Assert.False(npc.CanSee(player));
        npc.AllowDynamic = true; Assert.Equal(expected, npc.CanSee(player));
        player.UID = 1; Assert.False(npc.CanSee(player));
        Assert.False(npc.CanSee(null!));
    }
}
