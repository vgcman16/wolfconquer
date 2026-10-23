using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMyCombatGearStatusTests
{
    // Literal composite flag INDICES from native integer OR, not bit masks.
    [Theory]
    [InlineData(376, "HolyProtection", "ItemBless", 25u, null, 0u)]
    [InlineData(383, "PhoenixBlessing", "ItemBless", 25u, null, 0u)]
    [InlineData(372, "NobleSpirit", "MagicDefence", 0u, "Penetration", 2500u)]
    [InlineData(380, "FlameShield", "PhysicalDamageDecrease", 25u, "MagicDamageDecrease", 59u)]
    [InlineData(361, "FlowKnack", "PhysicalDamageDecrease", 25u, "MagicDamageDecrease", 59u)]
    [InlineData(381, "DivineEmptiness", "DodgeRate", 25u, null, 0u)]
    [InlineData(374, "WackeSpirit", "DodgeRate", 25u, null, 0u)]
    [InlineData(378, "CrackMantra1", "Defence", 75u, null, 0u)]
    [InlineData(379, "CrackMantra2", "MagicDefence", 75u, null, 0u)]
    [InlineData(412, "Sense", "DodgeRate", 59u, null, 0u)]
    [InlineData(394, "Diabolize", "DodgeRate", 25u, null, 0u)]
    [InlineData(397, "Barrier", "Counteraction", 25u, null, 0u)]
    [InlineData(407, "Shell", "Immunity", 25u, null, 0u)]
    [InlineData(402, "Storm", "CriticalStrike", 25u, null, 0u)]
    [InlineData(403, "Thrash", "PhysicalDamageDecrease", 25u, null, 0u)]
    [InlineData(401, "Thunder", "PhysicalDamageDecrease", 25u, null, 0u)]
    [InlineData(400, "Torrent", "MinAttack", 25u, "MaxAttack", 25u)]
    [InlineData(399, "Tide", "Defence", 25u, null, 0u)]
    [InlineData(406, "Splash", "MaxHitpoints", 25u, null, 0u)]
    [InlineData(419, "Sailing", "Block", 2500u, null, 0u)]
    [InlineData(398, "Vast", "Breakthrough", 25u, null, 0u)]
    [InlineData(410, "ColdBloodline", "Defence", 25u, null, 0u)]
    public Task EveryFlagBranchReadsItsActualNamedValueAndMutatesOnlyNativeTargets(int index, string name,
        string first, uint firstValue, string? second, uint secondValue) => NativePacketTestThread.Run(() =>
    {
        using var metadata = new NativeCombatGearQueryScope();
        using var fixture = new NativeSpellTestFixture(); // Class0 bypasses ONLY the class-specific prelude.
        var gear = new LegacyNativeMyCombatGear(fixture.Owner);
        gear.SetValue(name, 25.9, 59.9);
        var expected = new Dictionary<string, uint>();
        if (name == "DivineEmptiness") fixture.Owner.Status.Dodge = expected["Dodge"] = 10000;
        if (name == "CrackMantra1") fixture.Owner.Status.Defence = 100;
        if (name == "CrackMantra2") fixture.Owner.Status.MagicDefence = 100;
        expected[first] = firstValue;
        if (second != null) expected[second] = secondValue;
        Assert.True(fixture.Owner.Player.BitVector.TryAdd(index, 60, false, 0));

        gear.Status();

        foreach (var field in typeof(LegacyNativeMsgStatus).GetFields().Where(field => field.FieldType == typeof(uint)))
            Assert.Equal(expected.GetValueOrDefault(field.Name), (uint)field.GetValue(fixture.Owner.Status)!);
        Assert.Empty(fixture.Queue);
    });

    [Theory]
    [InlineData(0.29, 124554u)] [InlineData(0.59, 253403u)]
    [InlineData(25.9, 11123965u)] [InlineData(99.999, 42949243u)]
    [InlineData(-1.5, 4294323051u)] [InlineData(double.NaN, 0u)]
    [InlineData(double.PositiveInfinity, 0u)] [InlineData(2147483648.0, 1566589321u)]
    public Task DivineEmptinessPreservesTheWholeNativeDodgeExpressionFamily(double value, uint expected) =>
        NativePacketTestThread.Run(() =>
        {
            using var metadata = new NativeCombatGearQueryScope();
            using var fixture = new NativeSpellTestFixture();
            var gear = new LegacyNativeMyCombatGear(fixture.Owner);
            gear.SetValue("DivineEmptiness", value);
            fixture.Owner.Status.Dodge = uint.MaxValue;
            fixture.Owner.Player.BitVector.TryAdd(381, 60, false, 0);
            gear.Status();
            Assert.Equal(expected, fixture.Owner.Status.DodgeRate);
            Assert.Equal(uint.MaxValue, fixture.Owner.Status.Dodge);
        });

    [Theory]
    [InlineData(378, "CrackMantra1", "Defence")]
    [InlineData(379, "CrackMantra2", "MagicDefence")]
    public Task CrackBelowThresholdSetsZeroInsteadOfUnsignedSubtraction(int index, string name, string target) =>
        NativePacketTestThread.Run(() =>
        {
            using var metadata = new NativeCombatGearQueryScope();
            using var fixture = new NativeSpellTestFixture();
            var gear = new LegacyNativeMyCombatGear(fixture.Owner);
            gear.SetValue(name, 25.9);
            typeof(LegacyNativeMsgStatus).GetField(target)!.SetValue(fixture.Owner.Status, 25u);
            fixture.Owner.Player.BitVector.TryAdd(index, 60, false, 0);
            gear.Status();
            Assert.Equal(0u, (uint)typeof(LegacyNativeMsgStatus).GetField(target)!.GetValue(fixture.Owner.Status)!);
        });

    [Fact]
    public Task BothBlessBranchesClampAfterEachAdditionAndMissingSecondValueRetainsTheFirstMutation() =>
        NativePacketTestThread.Run(() =>
        {
            using var metadata = new NativeCombatGearQueryScope();
            using var fixture = new NativeSpellTestFixture();
            var gear = new LegacyNativeMyCombatGear(fixture.Owner);
            gear.SetValue("HolyProtection", 25.9);
            fixture.Owner.Status.ItemBless = 50;
            fixture.Owner.Player.BitVector.TryAdd(376, 60, false, 0);
            fixture.Owner.Player.BitVector.TryAdd(383, 60, false, 0);
            Assert.Throws<IndexOutOfRangeException>(() => gear.Status());
            Assert.Equal(60u, fixture.Owner.Status.ItemBless);
            gear.SetValue("PhoenixBlessing", 1000.0);
            gear.Status();
            Assert.Equal(60u, fixture.Owner.Status.ItemBless);
        });

    [Fact]
    public Task DisabledClassMetadataDoesNotSkipTheGenuineSpellAndCompositeFlagPostlude() =>
        NativePacketTestThread.Run(() =>
        {
            using var metadata = new NativeCombatGearQueryScope(false);
            using var fixture = new NativeSpellTestFixture();
            var prior = LegacyNativePoolMagic.Magic;
            try
            {
                LegacyNativePoolMagic.Magic = new()
                {
                    [18160] = new() { [2] = new() { GDamage = -1, DamageOnHuman = 101, Damage2 = 202, Damage3 = 303 } }
                };
                fixture.Storage.ClientSpells[18160] = new() { ID = 18160, Level = 2 };
                foreach (int flag in new[] { 317, 318, 319 }) fixture.Owner.Player.BitVector.TryAdd(flag, 60, false, 0);
                new LegacyNativeMyCombatGear(fixture.Owner).Status();
                Assert.Equal(uint.MaxValue, fixture.Owner.Status.MaxAttack);
                Assert.Equal(uint.MaxValue, fixture.Owner.Status.MinAttack);
                Assert.Equal(101u, fixture.Owner.Status.Defence);
                Assert.Equal(202u, fixture.Owner.Status.MagicDefence);
                Assert.Equal(303u, fixture.Owner.Status.Breakthrough);
                Assert.Equal(303u, fixture.Owner.Status.Counteraction);
                Assert.Empty(fixture.Queue);
            }
            finally { LegacyNativePoolMagic.Magic = prior; }
        });
}
