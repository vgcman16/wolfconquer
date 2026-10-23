using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMyCombatGearStatusTests
{
    [Fact]
    public Task BothBlessBranchesClampAfterEachAdditionButMissingValueRefusesBeforeMutation() =>
        NativePacketTestThread.Run(() =>
        {
            using var metadata = new NativeCombatGearQueryScope();
            using var fixture = new NativeSpellTestFixture();
            var gear = new LegacyNativeMyCombatGear(fixture.Owner);
            gear.SetValue("HolyProtection", 25.9);
            fixture.Owner.Status.ItemBless = 50;
            Assert.True(fixture.Owner.Player.BitVector.TryAdd(376, 60, false, 0));
            Assert.True(fixture.Owner.Player.BitVector.TryAdd(383, 60, false, 0));
            var before = SelectedStatusImage.Capture(fixture);

            var failure = Record.Exception(gear.Status);
            before.AssertUnchanged(fixture);
            Assert.IsType<InvalidDataException>(failure);
            Assert.Equal(50U, fixture.Owner.Status.ItemBless);

            gear.SetValue("PhoenixBlessing", 1000.0);
            gear.Status();
            Assert.Equal(60U, fixture.Owner.Status.ItemBless);
        });

    [Theory]
    [InlineData(376, "HolyProtection")]
    [InlineData(383, "PhoenixBlessing")]
    [InlineData(372, "NobleSpirit")]
    [InlineData(380, "FlameShield")]
    [InlineData(361, "FlowKnack")]
    [InlineData(381, "DivineEmptiness")]
    [InlineData(374, "WackeSpirit")]
    [InlineData(378, "CrackMantra1")]
    [InlineData(379, "CrackMantra2")]
    [InlineData(412, "Sense")]
    [InlineData(394, "Diabolize")]
    [InlineData(397, "Barrier")]
    [InlineData(407, "Shell")]
    [InlineData(402, "Storm")]
    [InlineData(403, "Thrash")]
    [InlineData(401, "Thunder")]
    [InlineData(400, "Torrent")]
    [InlineData(399, "Tide")]
    [InlineData(406, "Splash")]
    [InlineData(419, "Sailing")]
    [InlineData(398, "Vast")]
    [InlineData(410, "ColdBloodline")]
    public Task MissingSelectedValueCannotApplyEarlierRankStatsOrRemoveSpells(int index, string name) =>
        NativePacketTestThread.Run(() =>
        {
            using var test = new SelectedStatusCase();
            test.SelectEarlyAndTarget(index, name);
            var root = test.Gear.Info;
            var before = SelectedStatusImage.Capture(test.Fixture);

            var failure = Record.Exception(test.Gear.Status);

            before.AssertUnchanged(test.Fixture);
            Assert.IsType<InvalidDataException>(failure);
            Assert.Same(root, test.Gear.Info);
            Assert.False(root.ContainsKey(name));
        });

    [Theory]
    [InlineData(380, "FlameShield")]
    [InlineData(361, "FlowKnack")]
    [InlineData(412, "Sense")]
    public Task ShortSelectedTwoSlotValueCannotLeaveAnEarlierStatOrSpellPrefix(int index, string name) =>
        NativePacketTestThread.Run(() =>
        {
            using var test = new SelectedStatusCase();
            test.SelectEarlyAndTarget(index, name);
            double[] shortRow = [25.9];
            test.Gear.SetValue(name, shortRow);
            var before = SelectedStatusImage.Capture(test.Fixture);

            var failure = Record.Exception(test.Gear.Status);

            before.AssertUnchanged(test.Fixture);
            Assert.IsType<InvalidDataException>(failure);
            Assert.Same(shortRow, test.Gear.Info[name]);
            Assert.Equal([25.9], shortRow);
        });

    [Theory]
    [InlineData(376, "HolyProtection")]
    [InlineData(380, "FlameShield")]
    [InlineData(410, "ColdBloodline")]
    public Task NullSelectedArrayIsUnavailableBeforeEarlierEffects(int index, string name) =>
        NativePacketTestThread.Run(() =>
        {
            using var test = new SelectedStatusCase();
            test.SelectEarlyAndTarget(index, name);
            test.Gear.Info[name] = null!;
            var before = SelectedStatusImage.Capture(test.Fixture);

            var failure = Record.Exception(test.Gear.Status);

            before.AssertUnchanged(test.Fixture);
            Assert.IsType<InvalidDataException>(failure);
            Assert.Null(test.Gear.Info[name]);
        });

    [Fact]
    public Task MissingActiveValueOwnerIsNotAZeroCoefficientDefault() =>
        NativePacketTestThread.Run(() =>
        {
            using var test = new SelectedStatusCase();
            Assert.True(test.Fixture.Owner.Player.BitVector.TryAdd(410, 60, false, 0));
            test.Gear.Info = null!;
            var before = SelectedStatusImage.Capture(test.Fixture);

            var failure = Record.Exception(test.Gear.Status);

            before.AssertUnchanged(test.Fixture);
            Assert.IsType<InvalidDataException>(failure);
            Assert.Null(test.Gear.Info);
        });

    [Fact]
    public Task DisabledSelectedMetadataRefusesBeforeTheReal18160StatPrelude() =>
        NativePacketTestThread.Run(() =>
        {
            using var test = new SelectedStatusCase(enabled: false);
            test.EnableReal18160Prelude();
            Assert.True(test.Fixture.Owner.Player.BitVector.TryAdd(376, 60, false, 0));
            test.Gear.Info["HolyProtection"] = [25.9]; // Actual GetValue still returns null when disabled.
            var before = SelectedStatusImage.Capture(test.Fixture);

            var failure = Record.Exception(test.Gear.Status);

            before.AssertUnchanged(test.Fixture);
            Assert.IsType<InvalidDataException>(failure);
            Assert.Equal([25.9], test.Gear.Info["HolyProtection"]);
        });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task NoSelectedFlagsDoNotConsultTheOptionalNamedValueOwner(bool enabled) =>
        NativePacketTestThread.Run(() =>
        {
            using var test = new SelectedStatusCase(enabled, withClassPrelude: false);
            test.Gear.Info = null!;
            var before = SelectedStatusImage.Capture(test.Fixture);
            test.Gear.Status();
            before.AssertUnchanged(test.Fixture);
            Assert.Null(test.Gear.Info);
        });

    [Fact]
    public Task MissingLateSelectedValueRefusesBeforeTheReal18160AndBlessPrefixes() =>
        NativePacketTestThread.Run(() =>
        {
            using var test = new SelectedStatusCase();
            test.EnableReal18160Prelude();
            test.SelectEarlyAndTarget(410, "ColdBloodline");
            var before = SelectedStatusImage.Capture(test.Fixture);

            var failure = Record.Exception(test.Gear.Status);

            before.AssertUnchanged(test.Fixture);
            Assert.IsType<InvalidDataException>(failure);
            Assert.False(test.Gear.Info.ContainsKey("ColdBloodline"));
        });

    [Fact]
    public Task SelectedValuesKeepNonfiniteInputsAndIgnoreUnusedExtraTailSlots() =>
        NativePacketTestThread.Run(() =>
        {
            using var metadata = new NativeCombatGearQueryScope();
            using var fixture = new NativeSpellTestFixture();
            var gear = new LegacyNativeMyCombatGear(fixture.Owner);
            double[] holy = [25.9, double.NaN, double.PositiveInfinity];
            double[] sense = [double.NegativeInfinity, double.NaN, 123.0];
            gear.SetValue("HolyProtection", holy);
            gear.SetValue("Sense", sense);
            Assert.True(fixture.Owner.Player.BitVector.TryAdd(376, 60, false, 0));
            Assert.True(fixture.Owner.Player.BitVector.TryAdd(412, 60, false, 0));

            gear.Status();

            Assert.Equal(25U, fixture.Owner.Status.ItemBless);
            Assert.Equal(0U, fixture.Owner.Status.DodgeRate);
            Assert.Same(holy, gear.Info["HolyProtection"]);
            Assert.Same(sense, gear.Info["Sense"]);
            Assert.Equal(3, holy.Length);
            Assert.Equal(3, sense.Length);
            Assert.Empty(fixture.Queue);
        });
}
