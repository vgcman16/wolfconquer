using System.Collections.Frozen;
using System.Globalization;

namespace WolfConquer.Core.World;

public sealed record LegacyClientCompatibilityOptions(
    bool EnableNewerClientRevelatorStartupEffect,
    bool EnableNewerClientOverwhelmStartupEffect,
    bool EnableNewerClientCatalogItems,
    bool EnableNewerClientAppearanceMapping,
    bool DisableLegacyProactiveMonsterAttacks,
    bool DisableLegacyCombatGearMonsterHitPassives,
    bool DisableLegacyWeaponProcMonsterAttacks)
{
    public const string EnableNewerClientRevelatorStartupEffectEnvironmentVariable =
        "WOLFCONQUER_ENABLE_NEWER_CLIENT_REVELATOR";
    public const string EnableNewerClientOverwhelmStartupEffectEnvironmentVariable =
        "WOLFCONQUER_ENABLE_NEWER_CLIENT_OVERWHELM";
    public const string EnableNewerClientCatalogItemsEnvironmentVariable =
        "WOLFCONQUER_ENABLE_NEWER_CLIENT_CATALOG_ITEMS";
    public const string EnableNewerClientAppearanceMappingEnvironmentVariable =
        "WOLFCONQUER_ENABLE_NEWER_CLIENT_APPEARANCE_MAPPING";
    public const string DisableLegacyProactiveMonsterAttacksEnvironmentVariable =
        "WOLFCONQUER_DISABLE_LEGACY_PROACTIVE_MONSTER_ATTACKS";
    public const string DisableLegacyCombatGearMonsterHitPassivesEnvironmentVariable =
        "WOLFCONQUER_DISABLE_LEGACY_COMBAT_GEAR_MONSTER_HIT_PASSIVES";
    public const string DisabledLegacyCombatGearMonsterHitPassiveSpellsEnvironmentVariable =
        "WOLFCONQUER_DISABLED_LEGACY_COMBAT_GEAR_MONSTER_HIT_PASSIVE_SPELLS";
    public const string DisableLegacyWeaponProcMonsterAttacksEnvironmentVariable =
        "WOLFCONQUER_DISABLE_LEGACY_WEAPON_PROC_MONSTER_ATTACKS";

    private static readonly FrozenSet<ushort> NewerClientDisabledLegacyCombatGearMonsterHitPassiveSpellIds =
        new ushort[] { 21680, 22090, 22100 }.ToFrozenSet();

    public FrozenSet<ushort> DisabledLegacyCombatGearMonsterHitPassiveSpellIds { get; init; } =
        FrozenSet<ushort>.Empty;

    public static LegacyClientCompatibilityOptions DecompiledSourceCompatible { get; } =
        new(
            EnableNewerClientRevelatorStartupEffect: false,
            EnableNewerClientOverwhelmStartupEffect: false,
            EnableNewerClientCatalogItems: false,
            EnableNewerClientAppearanceMapping: false,
            DisableLegacyProactiveMonsterAttacks: false,
            DisableLegacyCombatGearMonsterHitPassives: false,
            DisableLegacyWeaponProcMonsterAttacks: false);

    public static LegacyClientCompatibilityOptions NewerClient { get; } =
        new(
            EnableNewerClientRevelatorStartupEffect: true,
            EnableNewerClientOverwhelmStartupEffect: true,
            EnableNewerClientCatalogItems: true,
            EnableNewerClientAppearanceMapping: true,
            DisableLegacyProactiveMonsterAttacks: true,
            DisableLegacyCombatGearMonsterHitPassives: false,
            DisableLegacyWeaponProcMonsterAttacks: true)
        {
            DisabledLegacyCombatGearMonsterHitPassiveSpellIds =
                NewerClientDisabledLegacyCombatGearMonsterHitPassiveSpellIds,
        };

    public bool IsLegacyCombatGearMonsterHitPassiveDisabled(ushort spellId)
    {
        return DisableLegacyCombatGearMonsterHitPassives ||
            DisabledLegacyCombatGearMonsterHitPassiveSpellIds.Contains(spellId);
    }

    public static LegacyClientCompatibilityOptions FromEnvironment(
        Func<string, string?>? readEnvironmentVariable = null)
    {
        readEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        return new LegacyClientCompatibilityOptions(
            IsEnabled(readEnvironmentVariable(EnableNewerClientRevelatorStartupEffectEnvironmentVariable)),
            IsEnabled(readEnvironmentVariable(EnableNewerClientOverwhelmStartupEffectEnvironmentVariable)),
            IsEnabled(readEnvironmentVariable(EnableNewerClientCatalogItemsEnvironmentVariable)),
            IsEnabled(readEnvironmentVariable(EnableNewerClientAppearanceMappingEnvironmentVariable)),
            IsEnabled(readEnvironmentVariable(DisableLegacyProactiveMonsterAttacksEnvironmentVariable)),
            IsEnabled(readEnvironmentVariable(DisableLegacyCombatGearMonsterHitPassivesEnvironmentVariable)),
            IsEnabled(readEnvironmentVariable(DisableLegacyWeaponProcMonsterAttacksEnvironmentVariable)))
        {
            DisabledLegacyCombatGearMonsterHitPassiveSpellIds =
                ParseDisabledLegacyCombatGearMonsterHitPassiveSpellIds(
                    readEnvironmentVariable(DisabledLegacyCombatGearMonsterHitPassiveSpellsEnvironmentVariable)),
        };
    }

    private static bool IsEnabled(string? value)
    {
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static FrozenSet<ushort> ParseDisabledLegacyCombatGearMonsterHitPassiveSpellIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return FrozenSet<ushort>.Empty;
        }

        var spellIds = new HashSet<ushort>();
        foreach (var segment in value.Split(
            [',', ';'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var token in segment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    throw new InvalidOperationException(
                        $"{DisabledLegacyCombatGearMonsterHitPassiveSpellsEnvironmentVariable} contains " +
                        $"invalid spell ID token '{token}'. Expected a base-10 UInt16 value from 0 through " +
                        $"{ushort.MaxValue}.");
                }

                if (parsed is < ushort.MinValue or > ushort.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"{DisabledLegacyCombatGearMonsterHitPassiveSpellsEnvironmentVariable} spell ID " +
                        $"'{token}' is outside the UInt16 range 0 through {ushort.MaxValue}.");
                }

                spellIds.Add((ushort)parsed);
            }
        }

        return spellIds.ToFrozenSet();
    }
}
