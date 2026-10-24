using System.Globalization;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SharedCanUseBoundaryRegisteredZeroManaSkipsOnlyUnrelatedMpMaster(bool zeroMana)
    {
        using var fixture = new SharedCanUseBoundaryFixture(
            CreateMagicRow(6_003, sort: 29, useMana: zeroMana ? (ushort)0 : (ushort)40,
                useStamina: 10, coolDown: 1_000, coldTime: 0), optionalRuneFault: true);
        Assert.Equal(0, fixture.OptionalRuneReads);
        var before = fixture.Capture();
        var error = await Record.ExceptionAsync(fixture.SwitchRegisteredAsync).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(error);
        Assert.Equal(1L, fixture.CompletedRateDraws); // Real Process policy; no RNG refund is asserted.
        if (zeroMana)
        {
            Assert.Equal(0, fixture.OptionalRuneReads);
            fixture.AssertRegisteredSuccess(90, 1_000, deadline: null);
        }
        else
        {
            Assert.Equal(1, fixture.OptionalRuneReads); // Proves the real metadata fault is armed/reachable when relevant.
            fixture.AssertUnchanged(before);
            Assert.Contains("preparationInvalid=FormatException", fixture.Log, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(-1, 1_000L, 0)]
    [InlineData(int.MinValue, 1_000L, 0)]
    [InlineData(-1, 1_039L, 0)]
    [InlineData(-1, 1_000L, 0x78)]
    [InlineData(-1, 1_000L, 0x79)]
    public async Task SharedCanUseBoundaryRegisteredNegativeColdCannotHideBehindBypass(
        int coldTime, long map, int flag)
    {
        using var fixture = new SharedCanUseBoundaryFixture(
            CreateMagicRow(6_003, sort: 29, useMana: 40, useStamina: 10,
                coolDown: 1_000, coldTime: coldTime),
            map: map, exclusionFlag: flag == 0 ? null : flag);
        var before = fixture.Capture();
        var error = await Record.ExceptionAsync(fixture.SwitchRegisteredAsync).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(error);
        fixture.AssertUnchanged(before);
        Assert.Contains("coldTimeInvalid", fixture.Log, StringComparison.Ordinal);
        Assert.Equal(map == 1_039 ? 0L : 1L, fixture.CompletedRateDraws);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedCanUseBoundaryRegisteredDateMaximumHasExactRepresentabilityBoundary(bool overflow)
    {
        var now = overflow ? DateTimeOffset.MaxValue : DateTimeOffset.MaxValue.AddMilliseconds(-1);
        using var fixture = new SharedCanUseBoundaryFixture(
            CreateMagicRow(6_003, sort: 29, useMana: 40, useStamina: 10,
                coolDown: 1_000, coldTime: 1), now: now);
        var before = fixture.Capture();
        var error = await Record.ExceptionAsync(fixture.SwitchRegisteredAsync).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(error);
        Assert.Equal(1L, fixture.CompletedRateDraws);
        if (overflow)
        {
            fixture.AssertUnchanged(before);
            Assert.Contains("preparationInvalid=ArgumentOutOfRangeException", fixture.Log, StringComparison.Ordinal);
        }
        else fixture.AssertRegisteredSuccess(90, 960, DateTimeOffset.MaxValue);
    }

    [Theory]
    [InlineData((ushort)16_520, -1)]
    [InlineData((ushort)16_520, int.MinValue)]
    [InlineData((ushort)16_540, -1)]
    [InlineData((ushort)16_540, int.MinValue)]
    [InlineData((ushort)16_460, -1)]
    [InlineData((ushort)16_460, int.MinValue)]
    [InlineData((ushort)16_500, -1)]
    [InlineData((ushort)16_500, int.MinValue)]
    public async Task SharedCanUseBoundaryPrivateComponentRejectsNegativeRelevantPower(ushort id, int power)
    {
        using var fixture = new SharedCanUseBoundaryFixture(SharedBoundarySigilMagic(id),
            SharedBoundaryGouyu(id, power));
        var before = fixture.Capture();
        var result = await fixture.InvokeExactCanUseComponentAsync();
        Assert.Null(result);
        fixture.AssertUnchanged(before);
        Assert.Contains("coldSigilPowerInvalid", fixture.Log, StringComparison.Ordinal);
        Assert.Equal(0L, fixture.CompletedRateDraws);
    }

    [Theory]
    [InlineData((ushort)16_520, "zero")]
    [InlineData((ushort)16_520, "partial")]
    [InlineData((ushort)16_520, "equal")]
    [InlineData((ushort)16_520, "excessive")]
    [InlineData((ushort)16_540, "zero")]
    [InlineData((ushort)16_540, "equal")]
    [InlineData((ushort)16_540, "excessive")]
    [InlineData((ushort)16_460, "zero")]
    [InlineData((ushort)16_460, "equal")]
    [InlineData((ushort)16_460, "excessive")]
    [InlineData((ushort)16_500, "zero")]
    [InlineData((ushort)16_500, "equal")]
    [InlineData((ushort)16_500, "excessive")]
    public async Task SharedCanUseBoundaryPrivateComponentClampsOnlyNonnegativeReduction(ushort id, string kind)
    {
        var row = SharedBoundarySigilMagic(id);
        var coldTime = int.Parse(row.Fields[47], CultureInfo.InvariantCulture);
        var power = kind switch
        {
            "zero" => 0, "partial" => 1, "equal" => coldTime,
            "excessive" => int.MaxValue, _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var expectedMilliseconds = kind is "equal" or "excessive" ? 0 : coldTime - power;
        using var fixture = new SharedCanUseBoundaryFixture(row, SharedBoundaryGouyu(id, power));
        var before = fixture.Capture();
        var result = await fixture.InvokeExactCanUseComponentAsync();
        fixture.AssertComponentDeadline(before, result, expectedMilliseconds);
    }

    [Theory]
    [InlineData(0)] // No cold duration: no sigil query.
    [InlineData(0x78)] // CannonBarrage.
    [InlineData(0x79)] // BlackbeardsRage.
    public async Task SharedCanUseBoundaryPrivateComponentDoesNotValidateUnrelatedSigil(int bypass)
    {
        const ushort id = 16_520;
        using var fixture = new SharedCanUseBoundaryFixture(
            SharedBoundarySigilMagic(id, coldTime: bypass == 0 ? 0 : null),
            SharedBoundaryGouyu(id, int.MinValue), exclusionFlag: bypass == 0 ? null : bypass);
        var before = fixture.Capture();
        var result = await fixture.InvokeExactCanUseComponentAsync();
        Assert.Same(before.Owner, result);
        fixture.AssertUnchanged(before);
        Assert.DoesNotContain("coldSigilPowerInvalid", fixture.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("WorldServer skipped legacy player magic", fixture.Log, StringComparison.Ordinal);
        Assert.Equal(0L, fixture.CompletedRateDraws);
    }

    [Fact]
    public async Task SharedCanUseBoundaryPrivateComponentMap1039StillValidatesRelevantCooldown()
    {
        const ushort id = 16_520;
        using var fixture = new SharedCanUseBoundaryFixture(SharedBoundarySigilMagic(id),
            SharedBoundaryGouyu(id, -1), map: 1_039);
        var before = fixture.Capture();
        var result = await fixture.InvokeExactCanUseComponentAsync();
        Assert.Null(result);
        fixture.AssertUnchanged(before);
        Assert.Contains("coldSigilPowerInvalid", fixture.Log, StringComparison.Ordinal);
        Assert.Equal(0L, fixture.CompletedRateDraws);
    }
}
