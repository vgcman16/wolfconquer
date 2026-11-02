using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RealOwnedReviverUsesCurrentQueryMonsterAndReleasesWorldBeforeWire(bool outsideRange) =>
        RunOwnedRootEquipmentAsync(outsideRange ? "reviver-far" : "reviver", async fixture =>
    {
        await fixture.Query(); // The actual native equipment query publishes the real maximums epoch.
        await fixture.QueryIndependent(OwnedRootEquipmentFixture.IndependentCasterUid);
        await fixture.QueryIndependent(OwnedRootEquipmentFixture.IndependentTargetUid);
        string DescribeQuery()
        {
            try { return fixture.DescribeRealMaximumsQuery(PlayerUid); }
            catch (Exception probe) { return "query-probe-failed: " + probe; }
        }
        var queryBeforeDeath = DescribeQuery();
        await fixture.DispelOwner(); // Actual public peer attack, not a seeded dead holder.
        var dead = fixture.Owner;
        var binding = dead.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
        Assert.Equal(0L, dead.Character.MinHitPoints); Assert.Equal(0, binding.Player.HitPoints);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(dead.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.Equal(0L, Assert.IsType<CharacterSnapshot>(fixture.Data.Loader.LoadByAccountId(PlayerUid)).MinHitPoints);
        var queryAfterDeath = DescribeQuery();
        int start = fixture.Order.Count;
        if (outsideRange)
        {
            await fixture.AdvanceRuntime(TimeSpan.FromMilliseconds(20), () => fixture.Owner.Character.MinHitPoints == 0);
            Assert.Equal(0L, fixture.Owner.Character.MinHitPoints); Assert.Equal(0, binding.Player.HitPoints);
            Assert.Equal(0L, Assert.IsType<CharacterSnapshot>(fixture.Data.Loader.LoadByAccountId(PlayerUid)).MinHitPoints);
            Assert.DoesNotContain(fixture.Animations(PlayerUid, start), packet => packet.SpellId == 1100);
            return;
        }
        var otherBefore = fixture.Current(OwnedRootEquipmentFixture.IndependentTargetUid).NativeGameClient!.OrdinarySpellBinding!.Player.HitPoints;
        Task blocked = fixture.StartBackpressuredWrite(PlayerUid);
        bool drained = false, started = false, completed = false;
        await PreserveOwnedPauseFailuresAsync(async () =>
        {
            await fixture.WaitForBackpressure(PlayerUid); Assert.True(fixture.ActualBackpressure(PlayerUid));
            await fixture.BeginPausedRuntimeAdvance(TimeSpan.FromMilliseconds(20)); started = true;
            try { await WaitUntilAsync(() => fixture.ReviverWaitingAtRealGate(PlayerUid), TimeSpan.FromSeconds(5)); }
            catch (TimeoutException error)
            {
                string query;
                try { query = fixture.DescribeRealMaximumsQuery(PlayerUid); }
                catch (Exception probe) { query = "query-probe-failed: " + probe; }
                throw new TimeoutException("Real Reviver did not reach the wire gate. Query before death=" + queryBeforeDeath +
                    "; after death=" + queryAfterDeath + "; at timeout=" + query +
                    "\nFixture diagnostics:\n" + fixture.Output, error);
            }
            var revived = fixture.Owner;
            Assert.NotSame(dead, revived); Assert.True(revived.Character.MinHitPoints > 0);
            Assert.Equal(revived.Character.MinHitPoints, (long)binding.Player.HitPoints);
            Assert.Equal(revived.Character.MinHitPoints,
                Assert.IsType<CharacterSnapshot>(fixture.Data.Loader.LoadByAccountId(PlayerUid)).MinHitPoints);
            Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
            await fixture.CastIndependent().WaitAsync(TimeSpan.FromSeconds(5));
            var other = fixture.Current(OwnedRootEquipmentFixture.IndependentTargetUid);
            Assert.True(other.NativeGameClient!.OrdinarySpellBinding!.Player.HitPoints < otherBefore);
            Assert.Equal(other.Character.MinHitPoints,
                Assert.IsType<CharacterSnapshot>(fixture.Data.Loader.LoadByAccountId(
                    OwnedRootEquipmentFixture.IndependentTargetUid)).MinHitPoints);
            Assert.True(fixture.ActualBackpressure(PlayerUid));
            await fixture.DrainBackpressuredWrite(PlayerUid); drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.CompletePausedRuntimeAdvance(() => fixture.Owner.Character.MinHitPoints > 0); completed = true;
            Assert.False(LegacyPlayerDeathState.HasStatusFlag(fixture.Owner.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
            var updates = ParseMsgUpdates(fixture.FramesSince(PlayerUid, start)).Where(row => row.Uid == PlayerUid).SelectMany(row => row.Updates).ToArray();
            Assert.Contains(updates, row => row.Type == LegacyMsgUpdateDataType.Hitpoints && row.Value == (ulong)fixture.Owner.Character.MinHitPoints);
            Assert.Contains(updates, row => row.Type == LegacyMsgUpdateDataType.Stamina && row.Value == LegacyPlayerDeathState.DefaultReviveStamina);
            Assert.Contains(updates, row => row.Type == LegacyMsgUpdateDataType.StatusFlag);
            Assert.Contains(updates, row => row.Type == LegacyMsgUpdateDataType.Mesh);
            Assert.Contains(fixture.Animations(PlayerUid, start), packet => packet.SpellId == 1100);
            Assert.Contains(fixture.FramesSince(TargetPlayerUid, start), bytes => ReadFrame(bytes).Header.Opcode == LegacyPlayerSpawnPacket.Opcode);
            Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
        }, () => FinishOwnedPauseAsync(fixture, PlayerUid, blocked, drained, started, completed));
    });
}
