using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static Task RunColdNinjaRecoveryScenarioAsync(bool claimAll, bool allocated, uint job = 5051) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = await ReloadNinjaRecoverySavedStateAsync(fixture, join, job: job);
            var intent = PrepareNinjaRecoveryIntent(fixture, join, initial, claimAll, allocated, job);
            var files = fixture.PackageFiles();
            var firstLogin = fixture.Sent.Count;
            var client = await join.RestartNativeDebtRecoveryAsync();
            var ninja = Assert.IsType<LegacyNativeNinja>(client.MyNinja);
            Assert.True(ninja.IsCombatOwnedBy(client));
            Assert.Equal(25U, ninja.Exp);
            Assert.Equal(1UL, ninja.Flag);
            Assert.Equal(new uint[] { 100, 404 }, ninja.Items.Keys.Order());
            Assert.Equal(41U, ninja.Items[100U].UK);
            Assert.Equal(42U, ninja.Items[404U].UK);
            Assert.Equal(job, client.Player.Class);
            Assert.Equal(31U, client.Player.ClassExperience);
            Assert.False(client.FullLoading);
            Assert.Equal(0U, client.Player.Online);
            Assert.Null(client.Beasts);
            Assert.Null(client.HairfaceStorage);
            Assert.Null(client.CombatGear);
            Assert.Null(client.Socket);
            AssertPackageJoinFiles(fixture, files);
            AssertNinjaRecoveryIntentRetained(join, intent);
            Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), fixture.Sent[firstLogin]);
            Assert.False(client.Player.IsCheckedPass);
            var readonlyOwner = join.Current;
            var readonlyJson = JsonSerializer.Serialize(readonlyOwner);
            var first = fixture.Sent.Count;
            await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join), PackageJoinRequest(claimAll), direct: true);
            Assert.Equal(first, fixture.Sent.Count);
            Assert.Same(readonlyOwner, join.Current);
            Assert.Equal(readonlyJson, JsonSerializer.Serialize(join.Current));
            AssertPackageJoinFiles(fixture, files);
            Assert.Equal(25U, ninja.Exp);
            await AssertNativeDebtPinAsync(fixture, join, supplied: 999, action: 6, value: 0);
            Assert.False(client.Player.IsCheckedPass);
            AssertPackageJoinFiles(fixture, files);
            await AssertNativeDebtPinAsync(fixture, join, supplied: 111, action: 5, value: 257);
            Assert.True(client.Player.IsCheckedPass);
            AssertPackageJoinFiles(fixture, files);
            first = fixture.Sent.Count;
            var started = DateTime.Now;
            await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join), PackageJoinRequest(claimAll), direct: false);
            AssertPackageJoinCommitted(fixture, join, initial, claimAll, job);
            AssertColdNinjaRecoveryFrames(fixture, join, initial, claimAll, first, started, DateTime.Now);
            Assert.Same(ninja, client.MyNinja);
            Assert.Equal(75U, ninja.Exp);
            Assert.Equal(1031U, client.Player.ClassExperience);
            Assert.False(client.FullLoading);
            Assert.True(client.Player.IsCheckedPass);
            Assert.Equal(PackageJoinNinjaLine.Replace("1/25/", "1/75/", StringComparison.Ordinal), join.Current.Character.Attributes["MsgNinja"]);
            var completedIntent = AssertPackageJoinAllocation(fixture, join);
            Assert.Equal(intent.Request.OperationId, completedIntent.Request.OperationId);
            Assert.Equal(intent.CopyPayload(), completedIntent.CopyPayload());
            var committed = fixture.PackageFiles();
            first = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            AssertPackageJoinFiles(fixture, committed);
            AssertPackageJoinReplay(fixture, claimAll, first);
            Assert.Equal(75U, ninja.Exp);
            Assert.Equal(1031U, client.Player.ClassExperience);
            AssertPackageJoinAllocation(fixture, join);
        }, job: job);
}
