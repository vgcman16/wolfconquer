using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class RemainingXpAdmissionOwnedTests
{
    [Fact]
    public Task ActualCurrentEntitlementCannotBeReplacedByAnEligibleRetainedCaller() =>
        F.RemainingXpRun(6011, "50", true, async f =>
        {
            var caller = f.Current();
            f.Store.Set(f.Connection(Actor), F.RemainingXpWithEligibility(caller, false));
            var expected = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files();
            Assert.True(Flag(caller.Character, LegacyRuntimeUpdateTicker.XpListFlag));
            Assert.False(Flag(expected.Character, LegacyRuntimeUpdateTicker.XpListFlag));
            int a = f.ActorFrames.Count, p = f.Frames(Peer).Count;
            await InvokeRetained(f, caller, 6011);
            Unchanged(f, expected, peer, files, a, p);
            Assert.Equal("50", f.Current().Character.Attributes["Stamina"]);
        });

    [Fact]
    public Task IneligibleRetainedCallerDoesNotVetoAnActualEligibleCurrentOwner() =>
        F.RemainingXpRun(6011, "50", false, async f =>
        {
            var caller = f.Current();
            f.Store.Set(f.Connection(Actor), F.RemainingXpWithEligibility(caller, true));
            Assert.False(Flag(caller.Character, LegacyRuntimeUpdateTicker.XpListFlag));
            await InvokeRetained(f, caller, 6011); Owner(f);
            Assert.Equal("45", f.Current().Character.Attributes["Stamina"]);
            Assert.Equal(493L, f.Current().Character.MinMana);
            Assert.Equal(493L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
            Assert.True(Flag(f.Current().Character, 0x2F));
            Assert.False(Flag(f.Current().Character, LegacyRuntimeUpdateTicker.XpListFlag));
            Assert.Single(Animations(f), animation => animation.SpellId == 6011);
        });
}
