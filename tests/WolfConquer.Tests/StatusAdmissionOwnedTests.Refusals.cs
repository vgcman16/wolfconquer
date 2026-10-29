using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class StatusAdmissionOwnedTests
{
    [Theory]
    [InlineData((ushort)1015, 1000L)] [InlineData((ushort)1020, 1000L)] [InlineData((ushort)8002, 1000L)]
    [InlineData((ushort)1015, 1039L)] [InlineData((ushort)1020, 1039L)] [InlineData((ushort)8002, 1039L)]
    public Task MissingActualXpListPreservesPaidAndBypassedResourcesBeforeAnySuccess(ushort id, long map) =>
        F.StatusAdmissionRunAsync(id, false, async f =>
        {
            var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files(); Owner(f);
            Assert.False(Flag(actor.Character, LegacyRuntimeUpdateTicker.XpListFlag));
            int a = f.ActorFrames.Count, p = f.Frames(Peer).Count;
            await Cast(f, id); Unchanged(f, actor, peer, files, a, p);
            Assert.Contains("xpAdmissionRejected=", f.Log.ToString(), StringComparison.Ordinal);
            Assert.Contains("XPList status flag is not active", f.Log.ToString(), StringComparison.Ordinal);
        }, map: map);

    [Theory]
    [InlineData("transform")] [InlineData("ride")] [InlineData("shadow")]
    public Task OrdinaryFlyUsesExistingActualBlockBeforeResourcesAndRetainsFailureMessage(string block) =>
        F.StatusAdmissionRunAsync(8003, false, async f =>
        {
            var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files(); Owner(f);
            if (block == "transform") Assert.Equal("2", actor.Character.Attributes["TransformationID"]);
            else Assert.True(Flag(actor.Character, block == "ride" ? 0x32 : 0x91));
            int a = f.ActorFrames.Count, p = f.Frames(Peer).Count;
            await Cast(f, 8003); Unchanged(f, actor, peer, files, a, p, failureMessages: 1);
            Assert.Contains("beforeResourceAdmission=True", f.Log.ToString(), StringComparison.Ordinal);
            Assert.Empty(Animations(f));
        }, block: block);

    [WindowsTradeJournalFailureFact]
    public async Task ValidatedEntitlementAndFlyStateSurviveRealWriterRefusalAndRetry()
    {
        foreach (var id in new ushort[] { 1015, 1020, 8002, 8003 })
            await F.StatusAdmissionRunAsync(id, id != 8003, async f =>
            {
                var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files();
                int a = f.ActorFrames.Count, p = f.Frames(Peer).Count;
                using (var held = new FileStream(f.CharacterPath(), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await Cast(f, id); Unchanged(f, actor, peer, files, a, p);
                    Assert.Equal(id != 8003, Flag(actor.Character, LegacyRuntimeUpdateTicker.XpListFlag));
                }
                await Cast(f, id); Owner(f);
                Assert.Equal(493L, f.Current().Character.MinMana);
                Assert.Equal(493L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
                Assert.Equal("96", f.Current().Character.Attributes["Stamina"]);
                Assert.Single(Animations(f), animation => animation.SpellId == id);
            });
    }
}
