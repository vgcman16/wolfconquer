using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class RemainingXpAdmissionOwnedTests
{
    [WindowsTradeJournalFailureFact]
    public Task ActualCharacterWriterRefusalCannotConsumeValidatedXpEligibilityOrAnyPreparedCost() =>
        F.RemainingXpRun(11970, "93", true, async f =>
        {
            var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files(); Owner(f);
            int a = f.ActorFrames.Count, p = f.Frames(Peer).Count;
            using (var held = new FileStream(f.CharacterPath(), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await Cast(f, 11970);
                Unchanged(f, actor, peer, files, a, p);
                Assert.Equal("93", f.Current().Character.Attributes["Stamina"]);
                Assert.True(Flag(f.Current().Character, LegacyRuntimeUpdateTicker.XpListFlag));
            }
            // Genuine retry after a definite refusal, not a fake acceptance route.
            await Cast(f, 11970); Owner(f);
            Assert.Equal("100", f.Current().Character.Attributes["Stamina"]);
            Assert.Equal(493L, f.Current().Character.MinMana);
            Assert.False(Flag(f.Current().Character, LegacyRuntimeUpdateTicker.XpListFlag));
        });
}
