using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly IReadOnlyDictionary<byte, LegacyFullAccountClassGrant> LegacyFullAccountClassGrants =
        new Dictionary<byte, LegacyFullAccountClassGrant>
        {
            [1] = new(
                "Warrior",
                TargetClass: 2_005,
                FamilyMinimum: 2_000,
                FamilyMaximum: 2_051,
                ClearExistingSpells: false,
                SpellIds: [1_095, 1_020, 1_090, 1_025, 1_045, 1_046, 1_020, 1_015, 1_040]),
            [2] = new(
                "Trojan",
                TargetClass: 1_005,
                FamilyMinimum: 1_000,
                FamilyMaximum: 1_051,
                ClearExistingSpells: true,
                SpellIds: [1_095, 1_020, 1_090, 1_046, 1_045]),
            [3] = new(
                "Water",
                TargetClass: 13_005,
                FamilyMinimum: 13_002,
                FamilyMaximum: 13_051,
                ClearExistingSpells: true,
                SpellIds: [1_095, 1_020, 1_090, 10_309, 1_010, 1_000, 1_005]),
            [4] = new(
                "Fire",
                TargetClass: 14_005,
                FamilyMinimum: 14_002,
                FamilyMaximum: 14_051,
                ClearExistingSpells: true,
                SpellIds: [1_095, 1_090, 1_020, 10_309, 1_010, 1_000, 1_005]),
            [5] = new(
                "Archer",
                TargetClass: 4_005,
                FamilyMinimum: 4_000,
                FamilyMaximum: 4_051,
                ClearExistingSpells: true,
                SpellIds: [1_095, 1_020, 1_090, 8_002]),
            [6] = new(
                "Ninja",
                TargetClass: 5_005,
                FamilyMinimum: 5_000,
                FamilyMaximum: 5_051,
                ClearExistingSpells: true,
                SpellIds: [1_095, 1_020, 12_080, 12_070, 6_011, 6_001]),
            [7] = new(
                "Monk",
                TargetClass: 6_005,
                FamilyMinimum: 6_000,
                FamilyMaximum: 6_051,
                ClearExistingSpells: true,
                SpellIds: [1_095, 1_020, 1_090, 10_430, 10_415, 10_490, 10_390]),
            [8] = new(
                "Pirate",
                TargetClass: 7_005,
                FamilyMinimum: 7_000,
                FamilyMaximum: 7_051,
                ClearExistingSpells: true,
                SpellIds: [1_095, 1_020, 1_090, 11_050, 11_110]),
            [9] = new(
                "DragonWarrior",
                TargetClass: 8_005,
                FamilyMinimum: 8_000,
                FamilyMaximum: 8_051,
                ClearExistingSpells: true,
                SpellIds: [
                    1_095, 1_020, 1_090, 12_240, 12_290, 12_270, 12_340,
                    12_330, 12_320, 12_210, 12_220, 12_230,
                ]),
            [10] = new(
                "WindWalker",
                TargetClass: 16_005,
                FamilyMinimum: 16_000,
                FamilyMaximum: 16_051,
                ClearExistingSpells: true,
                SpellIds: [1_095, 1_020, 1_090, 12_980, 12_870, 13_050, 13_060, 13_040]),
            [11] = new(
                "ThunderStriker",
                TargetClass: 9_005,
                FamilyMinimum: 9_000,
                FamilyMaximum: 9_051,
                ClearExistingSpells: true,
                SpellIds: [1_095, 1_090, 1_020, 15_750, 15_800, 15_700, 15_770, 15_720, 15_670, 15_680, 15_690]),
        };

    private static bool IsLegacyFullAccountClassFamily(long classCode, LegacyFullAccountClassGrant grant)
    {
        return classCode >= grant.FamilyMinimum && classCode <= grant.FamilyMaximum;
    }

    private static bool IsLegacyFullAccountWaterClass(long classCode)
    {
        return classCode is >= 13_002 and <= 13_051;
    }

    private sealed record LegacyFullAccountClassGrant(
        string Name,
        uint TargetClass,
        uint FamilyMinimum,
        uint FamilyMaximum,
        bool ClearExistingSpells,
        IReadOnlyList<ushort> SpellIds);

    private sealed record LegacyFullAccountClassProjection(
        CharacterSnapshot InitialCharacter,
        CharacterSnapshot LeveledCharacter,
        CharacterSnapshot UpdatedCharacter,
        PlayerSpellSnapshot UpdatedSpells,
        IReadOnlyList<ushort> RemovedSpellIds,
        IReadOnlyList<PlayerSpellSnapshotEntry> AddedSpellPackets,
        bool LevelChanged,
        bool SendLevelSourceStats,
        long LevelAttributePointsAdded);
}
