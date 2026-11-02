using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RealStaminaPublicationWaitsOutsideSharedAndRejectsSupersededScalar(bool supersede) =>
        RunOwnedRootEquipmentAsync("independent", async fixture =>
    {
        await fixture.Query();
        await fixture.QueryIndependent(OwnedRootEquipmentFixture.IndependentCasterUid);
        await fixture.QueryIndependent(OwnedRootEquipmentFixture.IndependentTargetUid);
        await fixture.Cast(1000); // Real admitted cost leaves native stamina below its maximum.
        var owner = fixture.Owner;
        var binding = owner.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
        ushort before = binding.Player.Stamina;
        Assert.True(before < 100);
        string ownerPath = Path.Combine(fixture.Data.Users, PlayerUid.ToString(CultureInfo.InvariantCulture) + ".ini");
        var savedBefore = File.ReadAllBytes(ownerPath);
        var independentBefore = fixture.Current(OwnedRootEquipmentFixture.IndependentTargetUid).NativeGameClient!.OrdinarySpellBinding!.Player.HitPoints;
        Task blocked = fixture.StartBackpressuredWrite(PlayerUid); Task? action = null;
        bool drained = false, started = false, completed = false;
        await PreserveOwnedPauseFailuresAsync(async () =>
        {
            await fixture.WaitForBackpressure(PlayerUid);
            Assert.True(fixture.ActualBackpressure(PlayerUid));
            int outputStart = fixture.Order.Count;
            await fixture.BeginPausedRuntimeAdvance(TimeSpan.FromSeconds(20)); started = true;
            await WaitUntilAsync(() => binding.Player.Stamina > before && fixture.StaminaWaitingAtRealGate(PlayerUid), TimeSpan.FromSeconds(5));
            ushort earned = binding.Player.Stamina;
            Assert.True(earned > before);
            Assert.Equal(earned.ToString(CultureInfo.InvariantCulture), fixture.Owner.Character.Attributes["Stamina"]);
            Assert.Equal(savedBefore, File.ReadAllBytes(ownerPath)); // Stamina tick is transient, not a saved-file rewrite.
            Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
            // This actual owned combat is on a separate map and must enter the
            // real Exclusive Frame while the first socket write is stalled.
            await fixture.CastIndependent().WaitAsync(TimeSpan.FromSeconds(5));
            var independent = fixture.Current(OwnedRootEquipmentFixture.IndependentTargetUid);
            Assert.True(independent.NativeGameClient!.OrdinarySpellBinding!.Player.HitPoints < independentBefore);
            Assert.Equal(independent.Character.MinHitPoints,
                Assert.IsType<CharacterSnapshot>(fixture.Data.Loader.LoadByAccountId(
                    OwnedRootEquipmentFixture.IndependentTargetUid)).MinHitPoints);
            Assert.True(fixture.ActualBackpressure(PlayerUid));
            if (supersede)
            {
                action = fixture.Cast(1000); // Real actor cost publishes a newer native stamina value.
                await WaitUntilAsync(() => binding.Player.Stamina < earned, TimeSpan.FromSeconds(5));
                Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
            }
            await fixture.DrainBackpressuredWrite(PlayerUid); drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
            if (action is not null) await action.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.CompletePausedRuntimeAdvance(() => true); completed = true;
            var staminaFrames = fixture.Costs(outputStart).Where(row => row.Type == LegacyMsgUpdateDataType.Stamina).Select(row => row.Value).ToArray();
            if (supersede) Assert.DoesNotContain((ulong)earned, staminaFrames);
            else Assert.Contains((ulong)earned, staminaFrames);
            Assert.Equal(binding.Player.Stamina.ToString(CultureInfo.InvariantCulture), fixture.Owner.Character.Attributes["Stamina"]);
            if (!supersede) Assert.Equal(savedBefore, File.ReadAllBytes(ownerPath));
            else Assert.Equal(fixture.Owner.Character.MinMana,
                Assert.IsType<CharacterSnapshot>(fixture.Data.Loader.LoadByAccountId(PlayerUid)).MinMana);
        }, () => FinishOwnedPauseAsync(fixture, PlayerUid, blocked, drained, started, completed, action));
    });
    [Fact]
    public Task RealXpTickerPermitRequiresActualGateAndDoesNotBlockIndependentOwnedCombat() =>
        RunOwnedRootEquipmentAsync("independent", async fixture =>
    {
        await fixture.Query();
        await fixture.QueryIndependent(OwnedRootEquipmentFixture.IndependentCasterUid);
        await fixture.QueryIndependent(OwnedRootEquipmentFixture.IndependentTargetUid);
        var peer = fixture.Peer;
        var binding = peer.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
        Assert.Equal((ushort)100, binding.Player.Stamina); // No due stamina output precedes this XP tick.
        string path = Path.Combine(fixture.Data.Users, TargetPlayerUid.ToString(CultureInfo.InvariantCulture) + ".ini");
        var savedBefore = File.ReadAllBytes(path);
        var targetBefore = fixture.Current(OwnedRootEquipmentFixture.IndependentTargetUid).NativeGameClient!.OrdinarySpellBinding!.Player.HitPoints;
        Task blocked = fixture.StartBackpressuredWrite(TargetPlayerUid);
        bool drained = false, started = false, completed = false;
        await PreserveOwnedPauseFailuresAsync(async () =>
        {
            await fixture.WaitForBackpressure(TargetPlayerUid);
            Assert.True(fixture.ActualBackpressure(TargetPlayerUid));
            int outputStart = fixture.Order.Count;
            await fixture.BeginPausedRuntimeAdvance(TimeSpan.FromSeconds(20)); started = true;
            await WaitUntilAsync(() => fixture.TickerWaitingAtRealGate(TargetPlayerUid), TimeSpan.FromSeconds(5));
            Assert.Equal("1", fixture.Peer.Character.Attributes["XPCount"]);
            Assert.Equal(savedBefore, File.ReadAllBytes(path));
            var wireStarts = fixture.WireStarts(TargetPlayerUid);
            Assert.False(fixture.AttemptUngatedActualTickerPermit(TargetPlayerUid));
            Assert.Equal(wireStarts, fixture.WireStarts(TargetPlayerUid));
            await fixture.CastIndependent().WaitAsync(TimeSpan.FromSeconds(5));
            var target = fixture.Current(OwnedRootEquipmentFixture.IndependentTargetUid);
            Assert.True(target.NativeGameClient!.OrdinarySpellBinding!.Player.HitPoints < targetBefore);
            Assert.Equal(target.Character.MinHitPoints,
                Assert.IsType<CharacterSnapshot>(fixture.Data.Loader.LoadByAccountId(
                    OwnedRootEquipmentFixture.IndependentTargetUid)).MinHitPoints);
            Assert.True(fixture.ActualBackpressure(TargetPlayerUid));
            await fixture.DrainBackpressuredWrite(TargetPlayerUid); drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.CompletePausedRuntimeAdvance(() => true); completed = true;
            Assert.Contains(ParseMsgUpdates(fixture.FramesSince(TargetPlayerUid, outputStart)), packet =>
                packet.Uid == TargetPlayerUid && packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.XPCircle && row.Value == 1));
            Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(savedBefore, File.ReadAllBytes(path));
        }, () => FinishOwnedPauseAsync(fixture, TargetPlayerUid, blocked, drained, started, completed));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RealTickerGateUsesUniqueCaseInsensitiveOwnedXpField(bool duplicate) =>
        RunOwnedRootEquipmentAsync("independent", async fixture =>
    {
        await fixture.Query();
        var binding = fixture.Peer.NativeGameClient!.OrdinarySpellBinding!;
        Task blocked = fixture.StartBackpressuredWrite(TargetPlayerUid);
        bool drained = false, started = false, completed = false;
        await PreserveOwnedPauseFailuresAsync(async () =>
        {
            await fixture.WaitForBackpressure(TargetPlayerUid);
            Assert.True(fixture.ActualBackpressure(TargetPlayerUid));
            int outputStart = fixture.Order.Count;
            await fixture.BeginPausedRuntimeAdvance(TimeSpan.FromSeconds(20)); started = true;
            await WaitUntilAsync(() => fixture.TickerWaitingAtRealGate(TargetPlayerUid), TimeSpan.FromSeconds(5));
            Assert.Equal("1", fixture.Peer.Character.Attributes["XPCount"]);
            var beforeSuccessor = fixture.Peer;
            var error = Record.Exception(() => fixture.PublishXpKeySuccessor(TargetPlayerUid, duplicate));
            if (!duplicate) Assert.Null(error);
            // A real Store may itself reject an ambiguous image. If so, it has
            // left the original image and the original tick remains valid.
            var acceptedDuplicate = duplicate && error is null;
            if (error is not null)
            {
                Assert.True(duplicate);
                Assert.True(error is InvalidDataException or InvalidOperationException, error.ToString());
                Assert.Same(beforeSuccessor, fixture.Peer);
            }
            Assert.True(fixture.Sessions.IsBoundSpellOwnerCurrent(binding));
            await fixture.DrainBackpressuredWrite(TargetPlayerUid); drained = true;
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.CompletePausedRuntimeAdvance(() => true); completed = true;
            var circles = ParseMsgUpdates(fixture.FramesSince(TargetPlayerUid, outputStart))
                .Where(packet => packet.Uid == TargetPlayerUid)
                .SelectMany(packet => packet.Updates)
                .Where(row => row.Type == LegacyMsgUpdateDataType.XPCircle && row.Value == 1).ToArray();
            if (acceptedDuplicate) Assert.Empty(circles); else Assert.Single(circles);
        }, () => FinishOwnedPauseAsync(fixture, TargetPlayerUid, blocked, drained, started, completed));
    });

}
