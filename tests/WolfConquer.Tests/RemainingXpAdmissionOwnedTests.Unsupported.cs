using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class RemainingXpAdmissionOwnedTests
{
    [Theory]
    [InlineData("50", (byte)5)]
    [InlineData("not-a-number", (byte)0)]
    public Task UnsupportedAttachStatusRefusesBeforeAnyOwnedCostOrCooldown(string raw, byte staminaCost) =>
        F.RemainingXpRun(9999, raw, false, async f =>
        {
            var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files(); Owner(f);
            int ownFrames = f.ActorFrames.Count, peerFrames = f.Frames(Peer).Count;
            await Cast(f, 9999);
            Unchanged(f, actor, peer, files, ownFrames, peerFrames);
            Assert.Equal(raw, f.Current().Character.Attributes["Stamina"]);
            Assert.Contains("unsupported attach status spell", f.Log.ToString(), StringComparison.Ordinal);
        }, staminaCost: staminaCost);
}
