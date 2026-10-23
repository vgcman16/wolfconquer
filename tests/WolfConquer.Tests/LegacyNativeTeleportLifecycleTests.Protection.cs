using System.Reflection;
using WolfConquer.Core.World;
namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    [Fact]
    public Task ProtectionBirthKeepsBothNativeDefaultClocksAndPrivateJumpSlot()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var player = scope.Client.Player;
            var field = typeof(LegacyNativePlayerRuntime).GetField("ProtectedJumpAttack", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.True(field.IsPrivate); Assert.Equal(default(DateTime), (DateTime)field.GetValue(player)!);
            Assert.Equal(default, player.Protect); Assert.True(player.AllowAttack());
        });
    }

    [Theory]
    [InlineData(-1000)]
    [InlineData(0)]
    [InlineData(100000)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public Task ProtectionUsesNativeMillisecondsWithoutClamping(int duration)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            DateTime before = DateTime.Now;
            scope.Client.Player.ProtectAttack(duration);
            DateTime after = DateTime.Now;
            Assert.InRange(scope.Client.Player.Protect, before.AddMilliseconds(duration), after.AddMilliseconds(duration));
        });
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(0)]
    [InlineData(10000)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public Task JumpProtectionUsesNativeSecondsWithoutClamping(int duration)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            DateTime before = DateTime.Now; scope.Client.Player.ProtectJumpAttack(duration); DateTime after = DateTime.Now;
            var field = typeof(LegacyNativePlayerRuntime).GetField("ProtectedJumpAttack", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.InRange((DateTime)field.GetValue(scope.Client.Player)!, before.AddSeconds(duration), after.AddSeconds(duration));
        });
    }

    [Fact]
    public Task AttackAdmissionRequiresBothRealProtectionClocksToExpire()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var player = scope.Client.Player;
            player.ProtectAttack(100000); Assert.False(player.AllowAttack());
            player.ProtectAttack(-10000); Assert.True(player.AllowAttack());
            player.ProtectJumpAttack(1000); Assert.False(player.AllowAttack());
            player.ProtectAttack(-10000); Assert.False(player.AllowAttack());
            player.ProtectJumpAttack(-1000); Assert.True(player.AllowAttack());
        });
    }

    [Theory]
    [InlineData(1008u, true, false)]
    [InlineData(2007u, false, true)]
    [InlineData(1003u, false, false)]
    [InlineData(2002u, false, false)]
    [InlineData(0u, false, false)]
    [InlineData(uint.MaxValue, false, false)]
    public void CoreGenderPredicatesUseOnlyNativeExactBodies(uint mesh, bool boy, bool girl)
    {
        Assert.Equal(boy, LegacyNativeRoleCore.IsBoy(mesh));
        Assert.Equal(girl, LegacyNativeRoleCore.IsGirl(mesh));
    }
}
