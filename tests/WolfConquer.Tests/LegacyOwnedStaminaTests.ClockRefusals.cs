namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStaminaTests
{
    [Theory]
    [InlineData(2)] // Fresh due gate follows the captured entry time.
    [InlineData(3)] // Fresh stamina stamp follows the due gate.
    [InlineData(4)] // Fresh fan stamp follows the stamina stamp.
    public Task ActualClockRollbackWithinOneDueStepRefusesWithoutSpendingEitherStamp(int utcRead) => Run(async f =>
    {
        await MintFan(f);
        var before = f.Current;
        var row = f.Native.Player!.BitVector.ArrayFlags[194]; var timer = row.Timer;
        var nativeSpells = f.Native.MySpells!.ClientSpells;
        // Failure-only actual configured clock control, armed AFTER real
        // admission/query. The tick receives no timestamp, gain or readiness.
        f.Clock.RewindOnRelativeUtcRead(utcRead, TimeSpan.FromSeconds(1));
        await TickHeld(f);
        Assert.True(f.Clock.RewindFired); Assert.Same(before, f.Current);
        Assert.Same(nativeSpells, f.Native.MySpells.ClientSpells);
        Assert.Same(row, f.Native.Player.BitVector.ArrayFlags[194]); Assert.Equal(timer, row.Timer);

        // Restore only the injected wall-clock rollback. Both private MinValue
        // stamps are still unspent: actual source5 + actual fan10 must commit.
        f.Clock.Advance(TimeSpan.FromSeconds(1));
        await TickAndAssert(f, 15, effect: true); await TickHeld(f);
        Assert.Same(row, f.Native.Player.BitVector.ArrayFlags[194]); Assert.Equal(timer, row.Timer);
    });
}
