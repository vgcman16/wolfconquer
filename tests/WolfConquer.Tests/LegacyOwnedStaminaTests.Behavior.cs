using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStaminaTests
{
    [Theory]
    [InlineData(null, 0)] [InlineData("0", 0)] [InlineData("99", 99)] [InlineData("65535", 65535)]
    public Task ActualAdmissionUsesSourceZeroAbsenceOrExplicitValidatedManagedCompatibility(string? raw, int expected) => Run(f =>
    {
        AssertCoherent(f, checked((ushort)expected));
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
        if (raw is null) Assert.False(saved.Attributes.ContainsKey("Stamina"));
        else Assert.Equal(raw, saved.Attributes["Stamina"]);
        Assert.Equal(f.OriginalSpells, File.ReadAllBytes(f.SpellsPath));
        return Task.CompletedTask;
    }, new(Stamina: raw));

    [Theory]
    [InlineData(false, 5, 3)] [InlineData(true, 15, 1)]
    public Task ActualMinValueFirstDueAndNormalBoundaryUseThePrivateSourceClock(bool sitting, int gain, int seconds) => Run(async f =>
    {
        AssertCoherent(f, 0); await TickAndAssert(f, checked((ushort)gain)); await TickHeld(f);
        f.Clock.Advance(TimeSpan.FromSeconds(seconds) - TimeSpan.FromTicks(1)); await TickHeld(f);
        f.Clock.Advance(TimeSpan.FromTicks(1)); await TickAndAssert(f, checked((ushort)(gain * 2))); await TickHeld(f);
    }, new(Stamina: null, Sitting: sitting));

    [Theory]
    [InlineData(50000000L, false, 40)] [InlineData(50000001L, true, 50)]
    public Task GenuineFanFirstDueAndStrictFiveSecondBoundaryPreserveEffectThenSelfScalar(long elapsedTicks, bool effect, int expected) => Run(async f =>
    {
        await MintFan(f); await TickAndAssert(f, 25, effect: true); await TickHeld(f);
        f.Clock.Advance(TimeSpan.FromTicks(elapsedTicks)); await TickAndAssert(f, checked((ushort)expected), effect);
        await TickHeld(f);
    }, new(Sitting: true, Peer: true));

    [Theory]
    [InlineData(84, false, 99, true)] [InlineData(85, false, 100, false)] [InlineData(86, false, 100, false)]
    [InlineData(134, true, 149, true)] [InlineData(135, true, 150, false)]
    public Task SourceFanEffectRequiresStrictPreClampRoomAndCapsWithoutOverflow(int initial, bool blessed, int expected, bool effect) => Run(async f =>
    {
        await MintFan(f); await TickAndAssert(f, checked((ushort)expected), effect); await TickHeld(f);
    }, new(Stamina: initial.ToString(System.Globalization.CultureInfo.InvariantCulture), Blessed: blessed, Peer: true));

    [Theory]
    [InlineData(false, 109, 110)] [InlineData(true, 159, 160)]
    public Task ActualCompletedMonkEquipmentCacheAndLearned12560AdoptTheRealTenPointCapacity(bool blessed, int before, int expected) => Run(async f =>
    {
        Assert.Contains(f.Current.PlayerSpells.Spells, row => row.Id == 12560 && row.Level == 0);
        Assert.Equal(622003U, Assert.Single(f.Current.PlayerItems.EquipmentItems).ItemId);
        await TickAndAssert(f, checked((ushort)expected)); f.Clock.Advance(TimeSpan.FromSeconds(3)); await TickHeld(f);
    }, new(Stamina: before.ToString(System.Globalization.CultureInfo.InvariantCulture), Blessed: blessed, Mode: "monk"));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualQueryRemovalOrActualExpiryStopsTheFanBonusWithoutReplayingItsCooldown(bool expire) => Run(async f =>
    {
        await MintFan(f); await TickAndAssert(f, 15, effect: true);
        if (expire)
        {
            Assert.True(f.Native.Player!.UpdateFlag((LegacyNativeMsgUpdate.Flags)194, 0, true, 0));
            await f.Handler.SendDueLegacyWindWalkerFanExpirationsAsync(CancellationToken.None);
        }
        else await f.UnequipFanAsync();
        AssertFan(f, false); ClearOutput(f); f.Clock.Advance(TimeSpan.FromSeconds(6)); await TickAndAssert(f, 20);
        await TickHeld(f);
    });

    [Fact]
    public Task RepeatedGenuineEquipmentQueryDoesNotResetEitherCommittedStaminaClock() => Run(async f =>
    {
        await MintFan(f); await TickAndAssert(f, 15, true);
        var row = f.Native.Player!.BitVector.ArrayFlags[194]; var timer = row.Timer;
        await f.QueryAsync(); Assert.Same(row, f.Native.Player.BitVector.ArrayFlags[194]); Assert.Equal(timer, row.Timer);
        ClearOutput(f); await TickHeld(f); f.Clock.Advance(TimeSpan.FromSeconds(3)); await TickAndAssert(f, 20);
        f.Clock.Advance(TimeSpan.FromSeconds(3)); await TickAndAssert(f, 35, true);
    });
}
