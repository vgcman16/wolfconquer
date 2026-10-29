using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class CapricePairCommitOwnedTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task CapricePairOnlyPendingStaminaFieldsGateLaterOutput(bool changePendingTarget) =>
        F.CapricePairRunAsync(async (f, world) =>
        {
            await F.CapricePairPrepareQueriesAsync(f, world);
            bool changed = false;
            f.BeforeSend = (uid, bytes) =>
            {
                if (changed || uid != Actor || !StaminaFrame(bytes, 60)) return;
                changed = true; RequireDurablePair(f, "60", "60");
                SetRuntimeStamina(f, changePendingTarget ? Peer : Actor, "70");
            };
            await F.CapricePairAttackAsync(f); f.BeforeSend = null;
            Assert.True(changed); RequireDurablePair(f, "60", "60");
            Assert.Equal("70", f.Current(changePendingTarget ? Peer : Actor).Character.Attributes["Stamina"]);
            if (changePendingTarget) Assert.DoesNotContain(f.Frames(Peer), bytes => StaminaFrame(bytes, 60));
            else Assert.Contains(f.Frames(Peer), bytes => StaminaFrame(bytes, 60));
            Assert.False(f.Recovery.IsBlocked); Binding(f, Actor); Binding(f, Peer);
        });

    [Fact]
    public Task CapricePairRealTargetSendFailureRetainsBothSavedDeltasAndFencesActualIssuances() =>
        F.CapricePairRunAsync(async (f, world) =>
        {
            await F.CapricePairPrepareQueriesAsync(f, world);
            var actor = Binding(f, Actor); var peer = Binding(f, Peer);
            var fault = new IOException("owned-caprice-target-send-failure"); int attempts = 0;
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid != Peer || !StaminaFrame(bytes, 60)) return;
                attempts++; RequireDurablePair(f, "60", "60"); throw fault;
            };
            // Frame logs the typed committed outcome; the fixture separately
            // rethrows this exact observation failure after real dispatch.
            Assert.Same(fault, await Assert.ThrowsAsync<IOException>(() => F.CapricePairAttackAsync(f)));
            f.BeforeSend = null;
            Assert.Equal(1, attempts); Assert.Equal("60", Saved(f, Actor).Attributes["Stamina"]);
            Assert.Equal("60", Saved(f, Peer).Attributes["Stamina"]);
            Assert.True(actor.IsRetired); Assert.True(peer.IsRetired); Assert.True(f.Recovery.IsBlocked);
            Assert.False(f.Store.TryGet(f.Connection(Actor), out _)); Assert.False(f.Store.TryGet(f.Connection(Peer), out _));
            Assert.Equal(493U, actor.Player.Mana); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
}
