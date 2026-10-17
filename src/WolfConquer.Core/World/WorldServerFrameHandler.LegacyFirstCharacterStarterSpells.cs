using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const long LegacyFirstCharacterStarterSpellRepairMaxLevel = 10;

    private static readonly ushort[] TunedLegacyTrojanStarterSpellIds = [1046];
    private static readonly ushort[] TunedLegacyWarriorStarterSpellIds = [1020, 1015, 1040];
    private static readonly ushort[] TunedLegacyArcherStarterSpellIds = [8002];
    private static readonly ushort[] TunedLegacyNinjaStarterSpellIds = [6001];
    private static readonly ushort[] TunedLegacyMonkStarterSpellIds = [10390];
    private static readonly ushort[] TunedLegacyPirateStarterSpellIds = [11050];
    private static readonly ushort[] TunedLegacyLeeStarterSpellIds = [12210];
    private static readonly ushort[] TunedLegacyThunderStrikerStarterSpellIds = [15670];
    private static readonly ushort[] TunedLegacyTaoistStarterSpellIds = [1000, 1005];
    private static readonly ushort[] TunedLegacyWindWalkerStarterSpellIds = [12870];

    private static readonly ushort[] SourceLegacyTrojanStarterSpellIds = [1045, 1046, 1110];
    private static readonly ushort[] SourceLegacyWarriorStarterSpellIds = [1025, 1045, 1046, 1020, 1015, 1040];
    private static readonly ushort[] SourceLegacyArcherStarterSpellIds = [8002];
    private static readonly ushort[] SourceLegacyNinjaStarterSpellIds = [6011, 6001];
    private static readonly ushort[] SourceLegacyMonkStarterSpellIds = [10415, 10490, 10390];
    private static readonly ushort[] SourceLegacyPirateStarterSpellIds = [11050, 11110];
    private static readonly ushort[] SourceLegacyLeeStarterSpellIds =
        [12240, 12290, 12270, 12340, 12330, 12320, 12210, 12220, 12230];
    private static readonly ushort[] SourceLegacyThunderStrikerStarterSpellIds =
        [15800, 15700, 15770, 15670, 15680, 15690];
    private static readonly ushort[] SourceLegacyTaoistStarterSpellIds = [10309, 1010, 1000, 1005];
    private static readonly ushort[] SourceLegacyWindWalkerStarterSpellIds = [12980, 12870, 13050, 13060, 13040];

    private async ValueTask<LegacyFirstCharacterStarterSpellState> EnsureLegacyFirstCharacterStarterSpellsAsync(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        PlayerSpellSnapshot playerSpells,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyFirstCharacterStarterSpellProfile(snapshot.Class, out var spellProfile) ||
            !TryGetLegacyFirstCharacterStarterProfile(snapshot.Class, out var profile) ||
            !ShouldApplyLegacyFirstCharacterStarterSpells(snapshot, playerItems, playerSpells, spellProfile, profile))
        {
            return new LegacyFirstCharacterStarterSpellState(playerSpells, Applied: false);
        }

        var updatedSpells = CreateLegacyFirstCharacterStarterSpells(playerSpells, spellProfile.TunedSpellIds);
        if (updatedSpells.Spells.Select(spell => spell.Id).SequenceEqual(playerSpells.Spells.Select(spell => spell.Id)))
        {
            return new LegacyFirstCharacterStarterSpellState(playerSpells, Applied: false);
        }

        await PersistLegacyFirstCharacterStarterSpellsAsync(snapshot, updatedSpells, cancellationToken)
            .ConfigureAwait(false);

        await _log.WriteLineAsync(
                "WorldServer applied source-backed legacy first-character starter spells " +
                $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} " +
                $"class={snapshot.Class.ToString(CultureInfo.InvariantCulture)} " +
                $"spellIds={FormatLegacyStarterSpellIds(spellProfile.TunedSpellIds)} " +
                "sourceProficienciesSkipped=notGranted.")
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        return new LegacyFirstCharacterStarterSpellState(updatedSpells, Applied: true);
    }

    private static bool ShouldApplyLegacyFirstCharacterStarterSpells(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        PlayerSpellSnapshot playerSpells,
        LegacyFirstCharacterStarterSpellProfile spellProfile,
        LegacyFirstCharacterStarterProfile profile)
    {
        if (!HasOnlyLegacyFirstCharacterStarterItems(playerItems, profile, spellProfile))
        {
            return false;
        }

        if (snapshot.Level == LegacyFirstCharacterStarterLevel &&
            IsLegacyFirstGenerationStarterCandidate(snapshot) &&
            HasOnlyLegacyFirstCharacterStarterSpells(playerSpells, spellProfile.SourceSpellIds))
        {
            return true;
        }

        return IsLegacyFirstCharacterStarterSpellRepairCandidate(snapshot) &&
            HasOnlyLegacyZeroProgressSpells(playerSpells) &&
            HasLegacyFirstCharacterStarterSpellPollution(playerSpells, spellProfile.TunedSpellIds);
    }

    private static bool IsLegacyFirstCharacterStarterSpellRepairCandidate(CharacterSnapshot snapshot)
    {
        return snapshot.Reborn == 0 &&
            snapshot.Level is >= LegacyFirstCharacterStarterLevel and <= LegacyFirstCharacterStarterSpellRepairMaxLevel &&
            snapshot.Experience >= 0 &&
            snapshot.Money <= SourceFirstCharacterStarterMoney &&
            snapshot.ConquerPoints <= SourceFirstCharacterStarterConquerPoints &&
            snapshot.BoundConquerPoints == 0 &&
            snapshot.BankConquerPoints == 0 &&
            snapshot.PkPoints == 0 &&
            snapshot.AttributePoints >= 0;
    }

    private static bool HasOnlyLegacyFirstCharacterStarterItems(
        PlayerItemSnapshot playerItems,
        LegacyFirstCharacterStarterProfile profile,
        LegacyFirstCharacterStarterSpellProfile spellProfile)
    {
        if (!HasOnlyLegacyFirstCharacterStarterSpellCompatibleInventoryItems(playerItems.InventoryItems, spellProfile) ||
            playerItems.RelicFuseItems.Count != 0 ||
            playerItems.MythsoulBagItems.Count != 0 ||
            playerItems.RuneItems.Count != 0 ||
            playerItems.WardrobeItems.Count != 0 ||
            playerItems.WarehouseItems.Count != 0)
        {
            return false;
        }

        if (playerItems.EquipmentItems.Count == 0)
        {
            return true;
        }

        var expected = CreateLegacyFirstCharacterStarterEquipmentSignature(profile);
        if (playerItems.EquipmentItems.Count != expected.Count)
        {
            return false;
        }

        var actual = playerItems.EquipmentItems
            .OrderBy(item => item.Position)
            .ThenBy(item => item.ItemId)
            .ToArray();
        for (var index = 0; index < expected.Count; index++)
        {
            if (actual[index].Position != expected[index].Position ||
                actual[index].ItemId != expected[index].ItemId)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasOnlyLegacyFirstCharacterStarterSpellCompatibleInventoryItems(
        IReadOnlyList<PlayerItemSnapshotItem> inventoryItems,
        LegacyFirstCharacterStarterSpellProfile spellProfile)
    {
        foreach (var item in inventoryItems)
        {
            if (item.Position != 0)
            {
                return false;
            }

            if (SourceFirstCharacterStarterRewardItemIds.Contains(item.ItemId))
            {
                continue;
            }

            var skillBook = LegacySkillBookUse.Classify(item.ItemId);
            if (skillBook.Status == LegacySkillBookUseStatus.Recognized &&
                skillBook.SpellId is ushort spellId &&
                (spellProfile.SourceSpellIds.Contains(spellId) ||
                    spellProfile.TunedSpellIds.Contains(spellId)))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static List<LegacyStarterEquipmentSignature> CreateLegacyFirstCharacterStarterEquipmentSignature(
        LegacyFirstCharacterStarterProfile profile)
    {
        var expected = new List<LegacyStarterEquipmentSignature>(capacity: 4);
        AddLegacyStarterEquipmentSignature(expected, profile.ArmorItemId, position: 3);
        AddLegacyStarterEquipmentSignature(expected, profile.RightWeaponItemId, position: 4);
        AddLegacyStarterEquipmentSignature(expected, profile.LeftWeaponItemId, position: 5);
        AddLegacyStarterEquipmentSignature(expected, profile.RingItemId, position: 6);
        return expected;
    }

    private static void AddLegacyStarterEquipmentSignature(
        List<LegacyStarterEquipmentSignature> expected,
        uint itemId,
        ushort position)
    {
        if (itemId != 0)
        {
            expected.Add(new LegacyStarterEquipmentSignature(itemId, position));
        }
    }

    private static bool HasOnlyLegacyFirstCharacterStarterSpells(
        PlayerSpellSnapshot playerSpells,
        IReadOnlyList<ushort> starterSpellIds)
    {
        foreach (var spell in playerSpells.Spells)
        {
            if (!starterSpellIds.Contains(spell.Id))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasOnlyLegacyZeroProgressSpells(PlayerSpellSnapshot playerSpells)
    {
        foreach (var spell in playerSpells.Spells)
        {
            if (spell.Level != 0 ||
                spell.Experience != 0 ||
                spell.PreviousLevel != 0 ||
                spell.SoulLevel != 0 ||
                spell.UseSpellSoul != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasLegacyFirstCharacterStarterSpellPollution(
        PlayerSpellSnapshot playerSpells,
        IReadOnlyList<ushort> tunedStarterSpellIds)
    {
        if (playerSpells.Spells.Count == 0)
        {
            return false;
        }

        foreach (var spell in playerSpells.Spells)
        {
            if (!tunedStarterSpellIds.Contains(spell.Id))
            {
                return true;
            }
        }

        return false;
    }

    private static PlayerSpellSnapshot CreateLegacyFirstCharacterStarterSpells(
        PlayerSpellSnapshot playerSpells,
        IReadOnlyList<ushort> starterSpellIds)
    {
        var spells = new List<PlayerSpellSnapshotEntry>(starterSpellIds.Count);
        spells.AddRange(playerSpells.Spells.Where(spell => starterSpellIds.Contains(spell.Id)));

        foreach (var spellId in starterSpellIds)
        {
            if (spells.Any(spell => spell.Id == spellId))
            {
                continue;
            }

            spells.Add(new PlayerSpellSnapshotEntry(
                spellId,
                Level: 0,
                Experience: 0,
                PreviousLevel: 0,
                SoulLevel: 0,
                UseSpellSoul: 0));
        }

        return new PlayerSpellSnapshot(spells);
    }

    private async ValueTask PersistLegacyFirstCharacterStarterSpellsAsync(
        CharacterSnapshot snapshot,
        PlayerSpellSnapshot playerSpells,
        CancellationToken cancellationToken)
    {
        if (_snapshotLoader is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_snapshotLoader.PlayerSpellsRoot))
        {
            await _log.WriteLineAsync(
                    "WorldServer skipped legacy first-character starter spell persistence " +
                    $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} reason=playerSpellsRootMissing.")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            new PlayerSpellSnapshotLoader(_snapshotLoader.PlayerSpellsRoot)
                .SaveByCharacterUid(snapshot.Uid, playerSpells);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _log.WriteLineAsync(
                    "WorldServer failed legacy first-character starter spell persistence " +
                    $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} reason={ex.GetType().Name}.")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static bool TryGetLegacyFirstCharacterStarterSpellProfile(
        long classCode,
        out LegacyFirstCharacterStarterSpellProfile profile)
    {
        profile = (classCode / 1000) switch
        {
            1 => new LegacyFirstCharacterStarterSpellProfile(
                TunedLegacyTrojanStarterSpellIds,
                SourceLegacyTrojanStarterSpellIds),
            2 => new LegacyFirstCharacterStarterSpellProfile(
                TunedLegacyWarriorStarterSpellIds,
                SourceLegacyWarriorStarterSpellIds),
            4 => new LegacyFirstCharacterStarterSpellProfile(
                TunedLegacyArcherStarterSpellIds,
                SourceLegacyArcherStarterSpellIds),
            5 => new LegacyFirstCharacterStarterSpellProfile(
                TunedLegacyNinjaStarterSpellIds,
                SourceLegacyNinjaStarterSpellIds),
            6 => new LegacyFirstCharacterStarterSpellProfile(
                TunedLegacyMonkStarterSpellIds,
                SourceLegacyMonkStarterSpellIds),
            7 => new LegacyFirstCharacterStarterSpellProfile(
                TunedLegacyPirateStarterSpellIds,
                SourceLegacyPirateStarterSpellIds),
            8 => new LegacyFirstCharacterStarterSpellProfile(
                TunedLegacyLeeStarterSpellIds,
                SourceLegacyLeeStarterSpellIds),
            9 => new LegacyFirstCharacterStarterSpellProfile(
                TunedLegacyThunderStrikerStarterSpellIds,
                SourceLegacyThunderStrikerStarterSpellIds),
            10 => new LegacyFirstCharacterStarterSpellProfile(
                TunedLegacyTaoistStarterSpellIds,
                SourceLegacyTaoistStarterSpellIds),
            16 => new LegacyFirstCharacterStarterSpellProfile(
                TunedLegacyWindWalkerStarterSpellIds,
                SourceLegacyWindWalkerStarterSpellIds),
            _ => default,
        };
        return profile.TunedSpellIds is { Count: > 0 };
    }

    private static string FormatLegacyStarterSpellIds(IReadOnlyList<ushort> spellIds)
    {
        return string.Join(",", spellIds.Select(spellId => spellId.ToString(CultureInfo.InvariantCulture)));
    }

    private readonly record struct LegacyFirstCharacterStarterSpellState(
        PlayerSpellSnapshot PlayerSpells,
        bool Applied);

    private readonly record struct LegacyFirstCharacterStarterSpellProfile(
        IReadOnlyList<ushort> TunedSpellIds,
        IReadOnlyList<ushort> SourceSpellIds);

    private readonly record struct LegacyStarterEquipmentSignature(uint ItemId, ushort Position);
}
