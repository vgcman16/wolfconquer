using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("default")]
    [InlineData("truncated")]
    [InlineData("duplicate")]
    [InlineData("noncanonical")]
    [InlineData("overflow")]
    [InlineData("count")]
    [InlineData("score")]
    [InlineData("too-many-rows")]
    [InlineData("oversized")]
    public Task ActualD13ColdNinjaRecoveryRejectsMalformedSavedOwnerBeforePublishingAnyNativeState(string fault) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var line = MalformedNinjaRecoveryLine(fault);
            var initial = await ReloadNinjaRecoverySavedStateAsync(fixture, join, line);
            var intent = PrepareNinjaRecoveryIntent(fixture, join, initial, claimAll: true, allocated: true);
            var files = fixture.PackageFiles();
            var first = fixture.Sent.Count;
            var client = await join.RestartNativeDebtRecoveryAsync();
            Assert.False(join.SessionStore.TryGet(join.Connection!, out _));
            Assert.Null(client.MyNinja);
            Assert.Null(client.CombatGear);
            Assert.Null(client.Beasts);
            Assert.Null(client.HairfaceStorage);
            Assert.Equal(0U, client.Player.UID);
            Assert.Equal(0U, client.Player.Class);
            Assert.Equal(0U, client.Player.ClassExperience);
            Assert.Equal(0U, client.Player.Online);
            Assert.False(client.FullLoading);
            Assert.False(client.Player.IsCheckedPass);
            Assert.Equal(first, fixture.Sent.Count);
            AssertPackageJoinFiles(fixture, files);
            AssertNinjaRecoveryIntentRetained(join, intent);
        }, job: 5051);

    [Fact]
    public Task ActualD13NinjaRecoveryPackageRefusesAConstructorOwnedReplacementThatDoesNotMatchSavedState() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = await ReloadNinjaRecoverySavedStateAsync(fixture, join);
            var intent = PrepareNinjaRecoveryIntent(fixture, join, initial, true, true);
            var client = await join.RestartNativeDebtRecoveryAsync();
            var original = Assert.IsType<LegacyNativeNinja>(client.MyNinja);
            await AssertNativeDebtPinAsync(fixture, join, 111, 5, 257);
            var replacement = new LegacyNativeNinja(client);
            client.MyNinja = replacement; // Actual competing constructor owner, no fake accepting runtime.
            var before = CapturePackageJoin(fixture, join);
            await join.FrameAsync(PackageJoinRequest(true));
            AssertPackageJoinUnchanged(fixture, join, before);
            Assert.Same(replacement, client.MyNinja);
            Assert.Equal(0U, replacement.Exp);
            Assert.Equal(25U, original.Exp);
            Assert.Equal(31U, client.Player.ClassExperience);
            AssertNinjaRecoveryIntentRetained(join, intent);
        }, job: 5051);

    private static string MalformedNinjaRecoveryLine(string fault)
    {
        var fields = PackageJoinNinjaLine.Split('/');
        switch (fault)
        {
            case "default": return "0";
            case "truncated": return PackageJoinNinjaLine[..^1];
            case "duplicate": fields[25] = "100"; break;
            case "noncanonical": fields[1] = "025"; break;
            case "overflow": fields[2] = "4294967296"; break;
            case "count": fields[19] = "3"; break;
            case "score": fields[12] = "not-a-score"; break;
            case "oversized": return new string('0', 256 * 1024 + 1);
            case "too-many-rows":
                return string.Join('/', fields.Take(19)) + "/4097/" +
                    string.Concat(Enumerable.Range(1, 4097).Select(id => id + "/1/0/0/0/"));
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        return string.Join('/', fields);
    }
}
