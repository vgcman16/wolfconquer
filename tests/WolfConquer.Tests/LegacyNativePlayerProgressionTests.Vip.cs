using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Vip = WolfConquer.Core.World.LegacyNativeMsgVipStatus.VipFlags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerProgressionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(255)]
    public Task VipPropertyIsUnclampedPhysicalByteWithoutExpiryOrPublication(int value) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.Equal((byte)0, scope.Player.VipLevel);
            Assert.Equal(default, scope.Player.ExpireVip);
            var expired = new DateTime(2001, 1, 1);
            scope.Player.ExpireVip = expired;
            scope.Client.FullLoading = true;

            scope.Player.VipLevel = (byte)value;

            Assert.Equal((byte)value, scope.Player.VipLevel);
            Assert.Equal((byte)value, typeof(LegacyNativePlayerRuntime)
                .GetField("_viplevel", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(scope.Player));
            Assert.Equal(expired, scope.Player.ExpireVip);
            Assert.True(scope.Client.FullLoading);
            Assert.Null(scope.Client.Inventory);
            Assert.Null(scope.Client.Equipment);
            Assert.Empty(scope.Queue);
            Assert.Equal(237, scope.Packet.Position);
            Assert.Equal(321, scope.Packet.Size);
        });

    [Theory]
    [InlineData(0, 65536u)]
    [InlineData(1, 34863u)]
    [InlineData(5, 34863u)]
    [InlineData(6, 65535u)]
    [InlineData(7, 65535u)]
    [InlineData(128, 65535u)]
    [InlineData(255, 65535u)]
    public Task UpdateVipPublishesLevelBeforeLiteralStatusAndDoesNotEnforceExpiry(int level, uint mode) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Player.VipLevel = (byte)level;
            scope.Player.ExpireVip = DateTime.MinValue;

            scope.Player.UpdateVip(scope.Packet);

            byte[][] frames = scope.Queue.ToArray();
            Assert.Equal(2, frames.Length);
            Assert.Equal(VipUpdateFrame((byte)level, position: 237), frames[0]);
            Assert.Equal(VipStatusFrame(mode), frames[1]);
            Assert.Equal(VipStatusFrame(mode), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
            Assert.Equal(8, scope.Packet.Size);
            Assert.Equal((byte)level, scope.Player.VipLevel);
            Assert.Equal(DateTime.MinValue, scope.Player.ExpireVip);
        });

    [Fact]
    public Task RepeatedVipValueDoesNotSendAndExplicitUpdatesRetainActualIncomingCursor() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Player.VipLevel = 6;
            scope.Player.VipLevel = 6;
            Assert.Empty(scope.Queue);
            scope.Player.UpdateVip(scope.Packet);
            scope.Player.UpdateVip(scope.Packet);
            byte[][] frames = scope.Queue.ToArray();
            Assert.Equal(4, frames.Length);
            Assert.Equal(VipUpdateFrame(6, position: 237), frames[0]);
            Assert.Equal(VipUpdateFrame(6), frames[2]);
            Assert.Equal(frames[1], frames[3]);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FakeOrDeadTransportSuppressesQueueButWholeUpdateStillBuildsFinalStatus(bool fake) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Client.Fake = fake;
            scope.Client.Socket!.Alive = fake;
            scope.Player.VipLevel = 5;

            scope.Player.UpdateVip(scope.Packet);

            Assert.Empty(scope.Queue);
            Assert.Equal(VipStatusFrame(34863), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal((byte)5, scope.Player.VipLevel);
        });

    [Fact]
    public Task NullVipUpdateStreamFaultsBeforeEitherPublicationAndRetainsPhysicalVip() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Player.VipLevel = 7;
            Assert.Throws<NullReferenceException>(() => scope.Player.UpdateVip(null!));
            Assert.Equal((byte)7, scope.Player.VipLevel);
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public void VipMetadataRetainsAllNativeNamesIntWidthAndOneByteStruct()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Vip)));
        Assert.False(typeof(Vip).IsDefined(typeof(FlagsAttribute), inherit: false));
        Assert.Equal(LayoutKind.Sequential, typeof(LegacyNativeMsgVipStatus).StructLayoutAttribute!.Value);
        Assert.Equal(1, Marshal.SizeOf<LegacyNativeMsgVipStatus>());
        Assert.Equal(new[]
        {
            "PortalTeleport", "Avatar", "MoreForVip", "FrozenGrot", "TeleportTeam",
            "CityTeleport", "CityTeleportTeam", "BlessTime", "OflineTG",
            "RefinaryAndArtefacts", "Friends", "VipHaire", "Labirint", "DailyQuests",
            "VipFurniture", "BonusLotery", "VipLevelOne", "FullVip", "None"
        }, Enum.GetNames<Vip>());
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(4u)]
    [InlineData(8u)]
    [InlineData(16u)]
    [InlineData(32u)]
    [InlineData(64u)]
    [InlineData(128u)]
    [InlineData(256u)]
    [InlineData(512u)]
    [InlineData(1024u)]
    [InlineData(2048u)]
    [InlineData(4096u)]
    [InlineData(8192u)]
    [InlineData(16384u)]
    [InlineData(32768u)]
    [InlineData(34863u)]
    [InlineData(65535u)]
    [InlineData(65536u)]
    [InlineData(uint.MaxValue)]
    public Task VipStatusProducerRetainsRawEnumBitsWithoutMaskOrCursorDependency(uint mode) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();

            Assert.Same(scope.Packet, scope.Packet.VipStatusCreate((Vip)mode));

            Assert.Equal(VipStatusFrame(mode), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
            Assert.Empty(scope.Queue);
        });
}
