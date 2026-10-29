using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class StatusAdmissionOwnedTests
{
    [Theory]
    [InlineData((ushort)1015)] [InlineData((ushort)1020)] [InlineData((ushort)8002)]
    public Task EligibleRetainedXpCallerCannotAuthorizeAnIneligibleActualCurrentRecord(ushort id) =>
        F.StatusAdmissionRunAsync(id, true, async f =>
        {
            var caller = f.Current(); Assert.True(Flag(caller.Character, LegacyRuntimeUpdateTicker.XpListFlag));
            f.Store.Set(f.Connection(Actor), F.StatusAdmissionState(caller, false));
            var current = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files();
            int a = f.ActorFrames.Count, p = f.Frames(Peer).Count;
            await InvokeRetained(f, caller, id); Unchanged(f, current, peer, files, a, p);
            Assert.Contains("XPList status flag is not active", f.Log.ToString(), StringComparison.Ordinal);
        });

    [Theory]
    [InlineData("transform")] [InlineData("ride")] [InlineData("shadow")]
    public Task UnblockedRetainedFlyCallerCannotAuthorizeAnActuallyBlockedCurrentRecord(string block) =>
        F.StatusAdmissionRunAsync(8003, false, async f =>
        {
            var caller = f.Current(); f.Store.Set(f.Connection(Actor), F.StatusAdmissionState(caller, false, block));
            var current = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files();
            int a = f.ActorFrames.Count, p = f.Frames(Peer).Count;
            await InvokeRetained(f, caller, 8003); Unchanged(f, current, peer, files, a, p, failureMessages: 1);
            Assert.Contains("beforeResourceAdmission=True", f.Log.ToString(), StringComparison.Ordinal);
        });

    [Theory]
    [InlineData((ushort)1020)] [InlineData((ushort)8003)]
    public Task IneligibleRetainedCallerDoesNotVetoAnActuallyEligibleCurrentRecord(ushort id) =>
        F.StatusAdmissionRunAsync(id, false, async f =>
        {
            var caller = f.Current(); f.Store.Set(f.Connection(Actor), F.StatusAdmissionState(caller, id != 8003));
            if (id == 8003)
            {
                // Capture a genuinely blocked old record, then replace it
                // through the same real issuer with an unblocked current one.
                f.Store.Set(f.Connection(Actor), F.StatusAdmissionState(f.Current(), false, "ride"));
                caller = f.Current();
                f.Store.Set(f.Connection(Actor), F.StatusAdmissionState(caller, false));
            }
            await InvokeRetained(f, caller, id); Owner(f);
            Assert.Equal(493L, f.Current().Character.MinMana); Assert.Equal("96", f.Current().Character.Attributes["Stamina"]);
            Assert.Equal(493L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
            Assert.Single(Animations(f), packet => packet.SpellId == id);
            Assert.True(Flag(f.Current().Character, id == 1020 ? 8 : 27));
        });
}
