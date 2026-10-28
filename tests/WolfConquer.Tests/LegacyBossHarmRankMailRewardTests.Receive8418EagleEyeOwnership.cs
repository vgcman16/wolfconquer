using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Receive8418EagleEyeDoesNotGrantXpOrAnimateToReplacedOrReissuedOwner(bool sameRecordReissued)
    {
        using var scene = CreateReceive8418ContinuationScene();
        PrimeMonsterForServerAuthoritativeFinalHit(scene.States, scene.Monster);
        var spellPath = Path.Combine(scene.Context.PlayerSpellsRoot!, KillerUid + ".bin");
        var savedBefore = File.ReadAllBytes(spellPath);
        WorldSession retained = null!;
        int calls = 0, packetsAfterReplacement = -1;
        await InvokeReceive8418EagleEye(scene, _ =>
        {
            if (++calls == 40)
            {
                Assert.True(scene.Sessions.TryGet(scene.Connection, out var actual));
                Assert.NotNull(actual);
                if (sameRecordReissued)
                {
                    Assert.True(scene.Sessions.TryRemove(scene.Connection, out var removed));
                    Assert.Same(actual, removed);
                    // Actual legacy unbound Store reissue, not a forged binding
                    // or private holder injection. The object itself is identical.
                    scene.Sessions.Set(scene.Connection, actual);
                    retained = actual;
                }
                else
                {
                    var replacement = actual with { Username = "real-replacement-during-rate" };
                    Assert.True(scene.Sessions.TryCompareAndSet(scene.Connection, actual, replacement, out var final));
                    retained = Assert.IsType<WorldSession>(final);
                }
                packetsAfterReplacement = scene.Sent.Count;
            }
            return false;
        });
        Assert.Equal(40, calls);
        Assert.Equal(8U, scene.Session.NativePlayer!.Kills);
        Assert.True(scene.Sessions.TryGet(scene.Connection, out var current));
        Assert.Same(retained, current);
        Assert.Equal(0, current!.PlayerSpells.Spells.Single(row => row.Id == 11_030).Experience);
        Assert.Equal(savedBefore, File.ReadAllBytes(spellPath));
        Assert.Equal(packetsAfterReplacement, scene.Sent.Count);
        Assert.DoesNotContain(scene.Sent, bytes => ReadFrame(bytes).Header.Opcode == LegacySpellPacket.Opcode);
        Assert.Empty(ParseReceive8418Animations(scene.Sent));
        Assert.DoesNotContain(scene.Sent, IsReceive8418DeathFrame);
        Assert.Contains("original publication owner changed after native receive", scene.Log.ToString(), StringComparison.Ordinal);
    }
}
