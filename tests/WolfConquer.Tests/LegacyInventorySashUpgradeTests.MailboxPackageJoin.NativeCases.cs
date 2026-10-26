using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyInventorySashUpgradeTests
{
    internal static Task RunNativePiratePackageMissingWireComponentAsync() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var client = join.NativeClient;
            var native = NativePackageJoinValues(client);
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            AssertPackageJoinUnchanged(fixture, join, before);
            Assert.Equal(native, NativePackageJoinValues(client));
            AssertNoPackageJoinIntents(join);
            Assert.Equal(31U, client.Player.ClassExperience);
            Assert.Equal(17U, client.CombatGear!.PirateExp);
            Assert.Equal(19U, client.CombatGear.AirPowers.Points);
        }, job: 7051, nativeComponent: true);

    internal static Task RunNativePiratePackageComponentAsync(bool claimAll) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var client = join.NativeClient;
            var player = client.Player;
            var gear = Assert.IsType<LegacyNativeMyCombatGear>(client.CombatGear);
            var powers = gear.AirPowers;
            var before = CapturePackageJoin(fixture, join);
            var sends = 0;
            join.SendObserver = _ =>
            {
                AssertPackageJoinCommitted(fixture, join, before, claimAll, 7051);
                Assert.Same(player, client.Player);
                Assert.Same(gear, client.CombatGear);
                Assert.Same(powers, gear.AirPowers);
                Assert.Equal(1031U, player.ClassExperience);
                Assert.Equal(67U, gear.PirateExp);
                Assert.Equal(69U, powers.Points);
                sends++;
            };
            var started = DateTime.Now;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            join.SendObserver = null;
            Assert.True(sends > 0);
            AssertPackageJoinFrames(fixture, join, before, claimAll, started, DateTime.Now, before.FirstFrame, job: 7051);
            AssertPackageJoinCommitted(fixture, join, before, claimAll, 7051);
            var committed = fixture.PackageFiles();
            var firstReplay = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            AssertPackageJoinFiles(fixture, committed);
            Assert.Equal(1031U, player.ClassExperience);
            Assert.Equal(67U, gear.PirateExp);
            Assert.Equal(69U, powers.Points);
            AssertPackageJoinReplay(fixture, claimAll, firstReplay);
            Assert.Null(client.Socket);
            await join.RestartAsync();
            Assert.NotSame(client, join.NativeClient);
            Assert.Equal(1031U, join.NativeClient.Player.ClassExperience);
            Assert.Equal(67U, join.NativeClient.CombatGear!.PirateExp);
            Assert.Equal(69U, join.NativeClient.CombatGear.AirPowers.Points);
            firstReplay = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            AssertPackageJoinFiles(fixture, committed);
            AssertPackageJoinReplay(fixture, claimAll, firstReplay);
        }, job: 7051, nativeComponent: true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RegisteredD13GenuineNinjaPackageComponentPublishesCanonicalLineAndPhysicalExpOnlyAfterJointCommit(bool claimAll) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var client = join.NativeClient;
            var player = client.Player;
            var ninja = Assert.IsType<LegacyNativeNinja>(client.MyNinja);
            Assert.Equal(25U, ninja.Exp);
            Assert.Equal(31U, player.ClassExperience);
            var before = CapturePackageJoin(fixture, join);
            var sends = 0;
            join.SendObserver = _ =>
            {
                AssertPackageJoinCommitted(fixture, join, before, claimAll, 5051);
                Assert.Same(player, client.Player);
                Assert.Same(ninja, client.MyNinja);
                Assert.Equal(75U, ninja.Exp);
                Assert.Equal(1031U, player.ClassExperience);
                sends++;
            };
            var started = DateTime.Now;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            join.SendObserver = null;
            Assert.True(sends > 0);
            AssertPackageJoinFrames(fixture, join, before, claimAll, started, DateTime.Now, before.FirstFrame, job: 5051);
            AssertPackageJoinCommitted(fixture, join, before, claimAll, 5051);
            var committed = fixture.PackageFiles();
            var firstReplay = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            AssertPackageJoinFiles(fixture, committed);
            Assert.Equal(75U, ninja.Exp);
            Assert.Equal(1031U, player.ClassExperience);
            AssertPackageJoinReplay(fixture, claimAll, firstReplay);
            Assert.Null(client.Socket);
            await join.RestartAsync();
            Assert.NotSame(client, join.NativeClient);
            Assert.Equal(1031U, join.NativeClient.Player.ClassExperience);
            Assert.Equal(75U, join.NativeClient.MyNinja!.Exp);
            firstReplay = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            AssertPackageJoinFiles(fixture, committed);
            AssertPackageJoinReplay(fixture, claimAll, firstReplay);
        }, job: 5051, nativeComponent: true);

    [Theory]
    [InlineData(5051L, "uid")]
    [InlineData(5051L, "class")]
    [InlineData(5051L, "class-exp")]
    [InlineData(5051L, "ninja-missing")]
    [InlineData(5051L, "ninja-user")]
    [InlineData(5051L, "ninja-exp")]
    [InlineData(5051L, "ninja-row")]
    [InlineData(7051L, "gear-missing")]
    [InlineData(7051L, "gear-user")]
    [InlineData(7051L, "gear-exp")]
    [InlineData(7051L, "gear-air")]
    public Task RegisteredD13NativePackageComponentRefusesIncoherentRequiredOwnerBeforeAnyIntent(long job, string fault) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var client = join.NativeClient;
            switch (fault)
            {
                case "uid": client.Player.UID++; break;
                case "class": typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client.Player, 5050U); break;
                case "class-exp": typeof(LegacyNativePlayerRuntime).GetField("_classexp", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client.Player, 32U); break;
                case "ninja-missing": client.MyNinja = null; break;
                case "ninja-user": client.MyNinja!.user = new(); break;
                case "ninja-exp": client.MyNinja!.Exp++; break;
                case "ninja-row": client.MyNinja!.Items[100].UK++; break;
                case "gear-missing": client.CombatGear = null; break;
                case "gear-user": client.CombatGear!.user = new(); break;
                case "gear-exp": client.CombatGear!.PirateExp++; break;
                case "gear-air": client.CombatGear!.AirPowers.Points++; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            var damaged = CapturePackageJoin(fixture, join);
            var native = NativePackageJoinValues(client);
            await join.FrameAsync(PackageJoinRequest(claimAll: true));
            AssertPackageJoinUnchanged(fixture, join, damaged);
            Assert.Equal(native, NativePackageJoinValues(client));
            AssertNoPackageJoinIntents(join);
            Assert.Null(client.Socket);
        }, job: job, nativeComponent: true);

    private static string NativePackageJoinValues(LegacyNativeGameClientRuntime client) =>
        $"{client.Player.UID}/{client.Player.Class}/{client.Player.ClassExperience}/" +
        $"{client.MyNinja?.Exp}/{client.MyNinja?.Items.GetValueOrDefault(100U)?.UK}/" +
        $"{client.CombatGear?.PirateExp}/{client.CombatGear?.AirPowers.Points}";
}
