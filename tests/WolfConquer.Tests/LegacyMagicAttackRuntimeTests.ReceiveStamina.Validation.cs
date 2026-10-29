using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(true, "-1")] [InlineData(false, "-1")]
    [InlineData(true, "not-a-number")] [InlineData(false, "not-a-number")]
    [InlineData(true, "18446744073709551616")] [InlineData(false, "18446744073709551616")]
    [InlineData(true, "")] [InlineData(false, "")]
    public Task ReceiveStaminaCapriceInvalidSavedParticipantDoesNotPrepareEitherBalanceOrStamp(bool invalidActor, string raw) =>
        ReceiveStaminaOwnedAsync(invalidActor ? raw : "40", invalidActor ? "80" : raw, true, (f, _) =>
        {
            var actor = f.Current(PlayerUid); var peer = f.Current(TargetPlayerUid);
            var invalid = invalidActor ? actor : peer;
            Assert.Equal(raw, invalid.Character.Attributes["Stamina"]);
            Assert.Equal(raw, f.Data.Loader.LoadByAccountId(invalid.AccountId)!.Attributes["Stamina"]);
            var files = ProcFiles(f); int a = f.Frames(PlayerUid).Count, p = f.Frames(TargetPlayerUid).Count;
            var prepared = ReceiveStaminaPrepare(f, true);
            Assert.False(prepared.Applied); Assert.False(prepared.Restored);
            Assert.Same(actor.Character, prepared.Actor); Assert.Same(peer.Character, prepared.Peer);
            ReceiveStaminaUnchanged(f, actor, peer, files, a, p); return Task.CompletedTask;
        });

    [Theory]
    [InlineData("18446744073709551615")] [InlineData("18446744073709551596")]
    public Task ReceiveStaminaCapriceUnrepresentableGainRejectsBeforeTargetDebit(string raw) =>
        ReceiveStaminaOwnedAsync(raw, "80", true, (f, _) =>
        {
            var actor = f.Current(PlayerUid); var peer = f.Current(TargetPlayerUid); var files = ProcFiles(f);
            int a = f.Frames(PlayerUid).Count, p = f.Frames(TargetPlayerUid).Count;
            var prepared = ReceiveStaminaPrepare(f, true);
            Assert.False(prepared.Applied); Assert.Same(actor.Character, prepared.Actor); Assert.Same(peer.Character, prepared.Peer);
            ReceiveStaminaUnchanged(f, actor, peer, files, a, p); return Task.CompletedTask;
        });

    [Theory]
    [InlineData("-1")] [InlineData("not-a-number")] [InlineData("18446744073709551616")] [InlineData("")]
    public Task ReceiveStaminaLightInvalidSavedBalanceDoesNotAcceptOrRestore(string raw) =>
        ReceiveStaminaOwnedAsync(raw, "80", false, (f, _) =>
        {
            var actor = f.Current(PlayerUid); var peer = f.Current(TargetPlayerUid); var files = ProcFiles(f);
            Assert.Equal(raw, actor.Character.Attributes["Stamina"]);
            Assert.Equal(raw, f.Data.Loader.LoadByAccountId(actor.AccountId)!.Attributes["Stamina"]);
            int a = f.Frames(PlayerUid).Count, p = f.Frames(TargetPlayerUid).Count;
            var prepared = ReceiveStaminaPrepare(f, false);
            Assert.False(prepared.Applied); Assert.False(prepared.Restored); Assert.Same(actor.Character, prepared.Actor);
            ReceiveStaminaUnchanged(f, actor, peer, files, a, p); return Task.CompletedTask;
        });

    [Theory]
    [InlineData(false, "120", "80")] [InlineData(true, "170", "130")]
    public Task ReceiveStaminaCapriceOnlyAbsentValuesUseExistingBlessingDefaults(bool blessing, string gain, string loss) =>
        ReceiveStaminaOwnedAsync(null, null, true, (f, _) =>
        {
            Assert.False(f.Current(PlayerUid).Character.Attributes.ContainsKey("Stamina"));
            Assert.False(f.Current(TargetPlayerUid).Character.Attributes.ContainsKey("Stamina"));
            var prepared = ReceiveStaminaPrepare(f, true); Assert.True(prepared.Applied);
            Assert.Equal(gain, prepared.Actor.Attributes["Stamina"]); Assert.Equal(loss, prepared.Peer.Attributes["Stamina"]);
            return Task.CompletedTask;
        }, blessing);

    [Fact]
    public Task ReceiveStaminaCapriceLargestRepresentableGainRemainsSupported() =>
        ReceiveStaminaOwnedAsync("18446744073709551595", "80", true, (f, _) =>
        {
            var prepared = ReceiveStaminaPrepare(f, true); Assert.True(prepared.Applied);
            Assert.Equal("18446744073709551615", prepared.Actor.Attributes["Stamina"]);
            Assert.Equal("60", prepared.Peer.Attributes["Stamina"]); return Task.CompletedTask;
        });

    [Fact]
    public Task ReceiveStaminaCapriceIndependentXpBranchDoesNotRequireStamina() =>
        ReceiveStaminaOwnedAsync("not-a-number", "-1", true, (f, _) =>
        {
            var prepared = ReceiveStaminaPrepare(f, true);
            Assert.False(prepared.Applied); Assert.True(prepared.Restored);
            Assert.Equal("not-a-number", prepared.Actor.Attributes["Stamina"]);
            Assert.Equal("-1", prepared.Peer.Attributes["Stamina"]);
            Assert.False(HasLegacyStatusFlag(prepared.Peer.Attributes, LegacyXpListFlag)); return Task.CompletedTask;
        }, branch: 2);
}
