using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class RemainingXpAdmissionOwnedTests
{
    [Theory]
    [InlineData((ushort)1110)] [InlineData((ushort)1025)] [InlineData((ushort)6011)] [InlineData((ushort)11970)]
    public Task MissingXpListRefusesBeforeRealPaidResourcesCooldownFilesOrOutput(ushort id) =>
        F.RemainingXpRun(id, "50", false, async f =>
        {
            var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files(); Owner(f);
            Assert.False(Flag(actor.Character, LegacyRuntimeUpdateTicker.XpListFlag)); Assert.Equal("50", actor.Character.Attributes["Stamina"]);
            var nativeStamina = actor.NativePlayer!.Stamina;
            int a = f.ActorFrames.Count, p = f.Frames(Peer).Count;
            await Cast(f, id);
            Unchanged(f, actor, peer, files, a, p);
            Assert.Equal(nativeStamina, actor.NativePlayer.Stamina);
            Assert.Equal("50", f.Current().Character.Attributes["Stamina"]);
            Assert.Contains("xpAdmissionRejected=", f.Log.ToString(), StringComparison.Ordinal);
            Assert.Contains("XPList status flag is not active", f.Log.ToString(), StringComparison.Ordinal);
        });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task WrongThunderRampageClassRefusesBeforeAnyActualCostOrEntitlementConsumption(bool xpList) =>
        F.RemainingXpRun(15780, "50", xpList, async f =>
        {
            var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files(); Owner(f);
            Assert.Equal(142L, actor.Character.Class);
            int a = f.ActorFrames.Count, p = f.Frames(Peer).Count;
            await Cast(f, 15780);
            Unchanged(f, actor, peer, files, a, p);
            Assert.Equal(xpList, Flag(f.Current().Character, LegacyRuntimeUpdateTicker.XpListFlag));
            Assert.Contains("xpAdmissionRejected=ThunderRampage", f.Log.ToString(), StringComparison.Ordinal);
            Assert.Contains("required Thunderstriker class is absent", f.Log.ToString(), StringComparison.Ordinal);
        });

    [Theory]
    [InlineData((ushort)11970, "-1")] [InlineData((ushort)12860, "-1")] [InlineData((ushort)15780, "-1")]
    [InlineData((ushort)11970, "bad")] [InlineData((ushort)12860, "bad")] [InlineData((ushort)15780, "bad")]
    [InlineData((ushort)11970, "18446744073709551616")] [InlineData((ushort)12860, "18446744073709551616")] [InlineData((ushort)15780, "18446744073709551616")]
    [InlineData((ushort)11970, "")] [InlineData((ushort)12860, "")] [InlineData((ushort)15780, "")]
    public Task ActivationRequiresValidSavedRestoreBalanceBeforeManaEvenAtZeroPricedStamina(ushort id, string raw) =>
        F.RemainingXpRun(id, raw, true, async f =>
        {
            var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files(); Owner(f);
            Assert.Equal(raw, actor.Character.Attributes["Stamina"]);
            Assert.Equal(raw, f.Data.Loader.LoadByAccountId(Actor)!.Attributes["Stamina"]);
            int a = f.ActorFrames.Count, p = f.Frames(Peer).Count;
            await Cast(f, id);
            Unchanged(f, actor, peer, files, a, p); Assert.True(Flag(f.Current().Character, LegacyRuntimeUpdateTicker.XpListFlag));
            Assert.Contains("xpAdmissionRejected=xpRestorationStaminaInvalid", f.Log.ToString(), StringComparison.Ordinal);
        }, staminaCost: 0, classCode: id == 15780 ? 9000 : 142);

    [Fact]
    public Task Map1039CostBypassDoesNotGrantMissingXpListEntitlement() =>
        F.RemainingXpRun(6011, "50", false, async f =>
        {
            var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files();
            int a = f.ActorFrames.Count, p = f.Frames(Peer).Count;
            await Cast(f, 6011); Unchanged(f, actor, peer, files, a, p);
            Assert.Contains("XPList status flag is not active", f.Log.ToString(), StringComparison.Ordinal);
        }, map: 1039);
}
