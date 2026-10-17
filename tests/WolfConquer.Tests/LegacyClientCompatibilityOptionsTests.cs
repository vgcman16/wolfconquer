using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClientCompatibilityOptionsTests
{
    private static readonly ushort[] NewerClientBlockedSpellIds = [21680, 22090, 22100];

    [Fact]
    public void FromEnvironmentParsesCurrentClientCombatDisableFlags()
    {
        var readVariables = new HashSet<string>();
        var options = LegacyClientCompatibilityOptions.FromEnvironment(name =>
        {
            readVariables.Add(name);
            return name is LegacyClientCompatibilityOptions.DisableLegacyProactiveMonsterAttacksEnvironmentVariable or
                LegacyClientCompatibilityOptions.DisableLegacyCombatGearMonsterHitPassivesEnvironmentVariable or
                LegacyClientCompatibilityOptions.DisableLegacyWeaponProcMonsterAttacksEnvironmentVariable
                ? "1"
                : null;
        });

        Assert.True(options.DisableLegacyProactiveMonsterAttacks);
        Assert.True(options.DisableLegacyCombatGearMonsterHitPassives);
        Assert.True(options.DisableLegacyWeaponProcMonsterAttacks);
        Assert.DoesNotContain(
            "WOLFCONQUER_ENABLE_NEWER_CLIENT_COMBAT_GEAR_FLOOR_PASSIVES",
            readVariables);
    }

    [Fact]
    public void FromEnvironmentParsesMixedSpellIdSeparatorsAndDeduplicates()
    {
        var options = LegacyClientCompatibilityOptions.FromEnvironment(name =>
            name == LegacyClientCompatibilityOptions.DisabledLegacyCombatGearMonsterHitPassiveSpellsEnvironmentVariable
                ? "22090,21680; 22100\t22090\r\n21680"
                : null);

        Assert.False(options.DisableLegacyCombatGearMonsterHitPassives);
        Assert.Equal(
            NewerClientBlockedSpellIds,
            options.DisabledLegacyCombatGearMonsterHitPassiveSpellIds.Order());
        Assert.All(NewerClientBlockedSpellIds, spellId =>
            Assert.True(options.IsLegacyCombatGearMonsterHitPassiveDisabled(spellId)));
        Assert.False(options.IsLegacyCombatGearMonsterHitPassiveDisabled(21681));
    }

    [Fact]
    public void CompatibilityDefaultsUseExpectedSpellBlocklists()
    {
        var fromEnvironment = LegacyClientCompatibilityOptions.FromEnvironment(_ => null);

        Assert.Empty(fromEnvironment.DisabledLegacyCombatGearMonsterHitPassiveSpellIds);
        Assert.False(fromEnvironment.IsLegacyCombatGearMonsterHitPassiveDisabled(21680));
        Assert.Empty(
            LegacyClientCompatibilityOptions.DecompiledSourceCompatible
                .DisabledLegacyCombatGearMonsterHitPassiveSpellIds);
        Assert.False(
            LegacyClientCompatibilityOptions.DecompiledSourceCompatible
                .IsLegacyCombatGearMonsterHitPassiveDisabled(21680));

        Assert.False(LegacyClientCompatibilityOptions.NewerClient.DisableLegacyCombatGearMonsterHitPassives);
        Assert.Equal(
            NewerClientBlockedSpellIds,
            LegacyClientCompatibilityOptions.NewerClient
                .DisabledLegacyCombatGearMonsterHitPassiveSpellIds
                .Order());
        Assert.False(
            LegacyClientCompatibilityOptions.NewerClient
                .IsLegacyCombatGearMonsterHitPassiveDisabled(21681));
    }

    [Fact]
    public void BroadDisableSwitchOverridesPerSpellBlocklist()
    {
        var options = LegacyClientCompatibilityOptions.DecompiledSourceCompatible with
        {
            DisableLegacyCombatGearMonsterHitPassives = true,
        };

        Assert.Empty(options.DisabledLegacyCombatGearMonsterHitPassiveSpellIds);
        Assert.True(options.IsLegacyCombatGearMonsterHitPassiveDisabled(0));
        Assert.True(options.IsLegacyCombatGearMonsterHitPassiveDisabled(21680));
        Assert.True(options.IsLegacyCombatGearMonsterHitPassiveDisabled(ushort.MaxValue));
    }

    [Theory]
    [InlineData("not-a-spell", "invalid spell ID token")]
    [InlineData("-1", "outside the UInt16 range")]
    [InlineData("65536", "outside the UInt16 range")]
    public void FromEnvironmentRejectsInvalidOrOutOfRangeSpellIds(
        string value,
        string expectedMessage)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            LegacyClientCompatibilityOptions.FromEnvironment(name =>
                name == LegacyClientCompatibilityOptions.DisabledLegacyCombatGearMonsterHitPassiveSpellsEnvironmentVariable
                    ? value
                    : null));

        Assert.Contains(
            LegacyClientCompatibilityOptions.DisabledLegacyCombatGearMonsterHitPassiveSpellsEnvironmentVariable,
            exception.Message);
        Assert.Contains($"'{value}'", exception.Message);
        Assert.Contains(expectedMessage, exception.Message);
    }
}
