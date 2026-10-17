using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const long LegacyFirstCharacterStarterLevel = 1;
    private const long LegacyBoostedFirstCharacterStarterLevel = 100;
    private const long TunedFirstCharacterStarterMoney = LegacyFirstCharacterStarterProfiles.TunedMoney;
    private const long TunedFirstCharacterStarterConquerPoints = LegacyFirstCharacterStarterProfiles.TunedConquerPoints;
    private const long SourceFirstCharacterStarterMoney = LegacyFirstCharacterStarterProfiles.SourceMoney;
    private const long SourceFirstCharacterStarterConquerPoints = LegacyFirstCharacterStarterProfiles.SourceConquerPoints;
    private static readonly uint[] SourceFirstCharacterStarterRewardItemIds = [3_000_550, 3_001_027];

    private async ValueTask<LegacyFirstCharacterStarterState> EnsureLegacyFirstCharacterStarterStateAsync(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyFirstCharacterStarterProfile(snapshot.Class, out var profile) ||
            !ShouldApplyLegacyFirstCharacterStarterState(snapshot, playerItems, profile))
        {
            return new LegacyFirstCharacterStarterState(snapshot, playerItems, Applied: false);
        }

        var starterItems = CreateLegacyFirstCharacterStarterItems(snapshot, profile);
        var updated = ApplyLegacyFirstCharacterStarterScalars(snapshot, starterItems, profile);
        await PersistLegacyFirstCharacterStarterStateAsync(updated, starterItems, cancellationToken)
            .ConfigureAwait(false);

        await _log.WriteLineAsync(
                "WorldServer applied tuned legacy first-character starter state " +
                $"uid={updated.Uid.ToString(CultureInfo.InvariantCulture)} " +
                $"class={updated.Class.ToString(CultureInfo.InvariantCulture)} " +
                $"hp={updated.MinHitPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"money={updated.Money.ToString(CultureInfo.InvariantCulture)} " +
                $"sourceMoney={SourceFirstCharacterStarterMoney.ToString(CultureInfo.InvariantCulture)} " +
                $"conquerPoints={updated.ConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"sourceConquerPoints={SourceFirstCharacterStarterConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                "sourceInventoryPacksSkipped=2.")
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        return new LegacyFirstCharacterStarterState(updated, starterItems, Applied: true);
    }

    private static bool ShouldApplyLegacyFirstCharacterStarterState(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        LegacyFirstCharacterStarterProfile profile)
    {
        if (!IsLegacyFirstGenerationStarterCandidate(snapshot))
        {
            return false;
        }

        if (snapshot.Level == LegacyFirstCharacterStarterLevel)
        {
            return HasNoLegacyFirstCharacterStarterItems(playerItems) ||
                ((IsLegacyLevelOneFirstCharacterStarterRepairCandidate(snapshot, profile) ||
                    HasLegacyFirstCharacterSourceRewardInventoryItems(playerItems.InventoryItems)) &&
                    HasOnlyLegacyFirstCharacterStarterOrSourceRewardItems(playerItems, profile));
        }

        return IsLegacyBoostedFirstCharacterStarterRepairCandidate(snapshot, profile) &&
            HasOnlyLegacyFirstCharacterStarterOrSourceRewardItems(playerItems, profile);
    }

    private CharacterSnapshot ApplyLegacyFirstCharacterStarterScalars(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot starterItems,
        LegacyFirstCharacterStarterProfile profile)
    {
        var updated = snapshot with
        {
            Level = LegacyFirstCharacterStarterLevel,
            Experience = 0,
            Strength = profile.Strength,
            Agility = profile.Agility,
            Vitality = profile.Vitality,
            Spirit = profile.Spirit,
            AttributePoints = 0,
            MinMana = LegacyFirstCharacterStarterProfiles.CalculateMana(snapshot.Class, profile.Spirit),
            MaxMana = LegacyFirstCharacterStarterProfiles.CalculateMana(snapshot.Class, profile.Spirit),
            Money = TunedFirstCharacterStarterMoney,
            ConquerPoints = TunedFirstCharacterStarterConquerPoints,
        };

        var sourceLevelOneHitpoints = LegacyFirstCharacterStarterProfiles.CalculateHitpoints(
            updated.Class,
            updated.Strength,
            updated.Agility,
            updated.Vitality,
            updated.Spirit);
        var maxHitpoints = LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(
            updated,
            currentHitpoints: 0,
            starterItems,
            CombatGearSnapshot.Empty,
            _worldData);
        var starterHitpoints = Math.Max(sourceLevelOneHitpoints, maxHitpoints);
        updated = updated with
        {
            MinHitPoints = starterHitpoints,
            Attributes = CreateUpdatedLegacyFirstCharacterAttributes(updated, starterHitpoints),
        };
        return updated;
    }

    private PlayerItemSnapshot CreateLegacyFirstCharacterStarterItems(
        CharacterSnapshot snapshot,
        LegacyFirstCharacterStarterProfile profile)
    {
        var equipment = new List<PlayerItemSnapshotItem>(capacity: 4);
        AddStarterEquipment(equipment, snapshot.Uid, profile.RingItemId, position: 6);
        AddStarterEquipment(equipment, snapshot.Uid, profile.RightWeaponItemId, position: 4);
        AddStarterEquipment(equipment, snapshot.Uid, profile.LeftWeaponItemId, position: 5);
        AddStarterEquipment(equipment, snapshot.Uid, profile.ArmorItemId, position: 3);

        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: equipment
                .OrderBy(item => item.Position)
                .ThenBy(item => item.Uid)
                .ToArray(),
            RuneItems: []);
    }

    private void AddStarterEquipment(
        List<PlayerItemSnapshotItem> equipment,
        long characterUid,
        uint itemId,
        ushort position)
    {
        if (itemId == 0)
        {
            return;
        }

        var index = equipment.Count;
        var durability = GetLegacyStarterDurability(itemId);
        var itemUid = CreateLegacyStarterItemUid(characterUid, index);
        equipment.Add(new PlayerItemSnapshotItem
        {
            Uid = itemUid,
            ItemId = itemId,
            Durability = durability,
            MaximumDurability = durability,
            Position = position,
            Color = CreateLegacyStarterColor(itemUid, itemId),
        });
    }

    private ushort GetLegacyStarterDurability(uint itemId)
    {
        if (_worldData?.ItemsById.TryGetValue(checked((int)itemId), out var entries) == true)
        {
            foreach (var entry in entries)
            {
                if (entry.LegacyDurability == 0)
                {
                    continue;
                }

                return entry.LegacyDurability > ushort.MaxValue
                    ? ushort.MaxValue
                    : checked((ushort)entry.LegacyDurability);
            }
        }

        return 1;
    }

    private async ValueTask PersistLegacyFirstCharacterStarterStateAsync(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot starterItems,
        CancellationToken cancellationToken)
    {
        if (_snapshotLoader is null)
        {
            return;
        }

        try
        {
            if (!CharacterSnapshotScalarPersistence.TryPersist(
                    _snapshotLoader.UsersRoot,
                    snapshot,
                    out var path,
                    out var reason))
            {
                LogCharacterScalarPersistenceGap(snapshot.Uid, snapshot, path, reason);
            }
        }
        catch (Exception ex)
        {
            LogCharacterScalarPersistenceGap(snapshot.Uid, snapshot, string.Empty, ex.GetType().Name);
        }

        if (string.IsNullOrWhiteSpace(_snapshotLoader.PlayerItemsRoot))
        {
            await _log.WriteLineAsync(
                    "WorldServer skipped legacy first-character starter item persistence " +
                    $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} reason=playerItemsRootMissing.")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            new PlayerItemSnapshotLoader(_snapshotLoader.PlayerItemsRoot)
                .SaveByCharacterUid(snapshot.Uid, starterItems);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _log.WriteLineAsync(
                    "WorldServer failed legacy first-character starter item persistence " +
                    $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} reason={ex.GetType().Name}.")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask PersistLegacyFirstCharacterEmptyProficienciesAsync(
        CharacterSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (_snapshotLoader is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_snapshotLoader.PlayerProficienciesRoot))
        {
            await _log.WriteLineAsync(
                    "WorldServer skipped legacy first-character starter proficiency persistence " +
                    $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} reason=playerProficienciesRootMissing.")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            var path = Path.Combine(
                _snapshotLoader.PlayerProficienciesRoot,
                snapshot.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            if (File.Exists(path))
            {
                return;
            }

            new PlayerProficiencySnapshotLoader(_snapshotLoader.PlayerProficienciesRoot)
                .SaveByCharacterUid(snapshot.Uid, PlayerProficiencySnapshot.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _log.WriteLineAsync(
                    "WorldServer failed legacy first-character starter proficiency persistence " +
                    $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} reason={ex.GetType().Name}.")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static IReadOnlyDictionary<string, string> CreateUpdatedLegacyFirstCharacterAttributes(
        CharacterSnapshot snapshot,
        uint maxHitpoints)
    {
        return new Dictionary<string, string>(snapshot.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Strength"] = snapshot.Strength.ToString(CultureInfo.InvariantCulture),
            ["Agility"] = snapshot.Agility.ToString(CultureInfo.InvariantCulture),
            ["Vitaliti"] = snapshot.Vitality.ToString(CultureInfo.InvariantCulture),
            ["Spirit"] = snapshot.Spirit.ToString(CultureInfo.InvariantCulture),
            ["Level"] = snapshot.Level.ToString(CultureInfo.InvariantCulture),
            ["Experience"] = snapshot.Experience.ToString(CultureInfo.InvariantCulture),
            ["Atributes"] = snapshot.AttributePoints.ToString(CultureInfo.InvariantCulture),
            ["MinHitPoints"] = maxHitpoints.ToString(CultureInfo.InvariantCulture),
            ["MinMana"] = snapshot.MinMana.ToString(CultureInfo.InvariantCulture),
            ["MaxMana"] = snapshot.MaxMana.ToString(CultureInfo.InvariantCulture),
            ["Money"] = snapshot.Money.ToString(CultureInfo.InvariantCulture),
            ["ConquerPoints"] = snapshot.ConquerPoints.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static bool IsLegacyFirstGenerationStarterCandidate(CharacterSnapshot snapshot)
    {
        return snapshot.Reborn == 0 &&
            snapshot.FirstClass == 0 &&
            snapshot.SecondClass == 0 &&
            snapshot.Experience == 0 &&
            snapshot.Money <= SourceFirstCharacterStarterMoney &&
            snapshot.ConquerPoints <= SourceFirstCharacterStarterConquerPoints &&
            snapshot.BoundConquerPoints == 0 &&
            snapshot.BankConquerPoints == 0 &&
            snapshot.PkPoints == 0 &&
            snapshot.AttributePoints >= 0;
    }

    private static bool HasNoLegacyFirstCharacterStarterItems(PlayerItemSnapshot playerItems)
    {
        return playerItems.InventoryItems.Count == 0 &&
            playerItems.RelicFuseItems.Count == 0 &&
            playerItems.MythsoulBagItems.Count == 0 &&
            playerItems.EquipmentItems.Count == 0 &&
            playerItems.RuneItems.Count == 0 &&
            playerItems.WardrobeItems.Count == 0 &&
            playerItems.WarehouseItems.Count == 0;
    }

    private static bool IsLegacyBoostedFirstCharacterStarterRepairCandidate(
        CharacterSnapshot snapshot,
        LegacyFirstCharacterStarterProfile profile)
    {
        return snapshot.Level == LegacyBoostedFirstCharacterStarterLevel &&
            snapshot.Strength == profile.SourceStrength &&
            snapshot.Agility == profile.SourceAgility &&
            snapshot.Vitality == profile.SourceVitality &&
            snapshot.Spirit == profile.SourceSpirit &&
            snapshot.Money <= SourceFirstCharacterStarterMoney &&
            snapshot.ConquerPoints <= SourceFirstCharacterStarterConquerPoints;
    }

    private static bool IsLegacyLevelOneFirstCharacterStarterRepairCandidate(
        CharacterSnapshot snapshot,
        LegacyFirstCharacterStarterProfile profile)
    {
        return (snapshot.Strength != profile.Strength ||
                snapshot.Agility != profile.Agility ||
                snapshot.Vitality != profile.Vitality ||
                snapshot.Spirit != profile.Spirit) &&
            snapshot.Agility == 0 &&
            snapshot.Vitality == 0 &&
            snapshot.Spirit == 0 &&
            snapshot.MinHitPoints <= 1 &&
            snapshot.Money <= SourceFirstCharacterStarterMoney &&
            snapshot.ConquerPoints <= SourceFirstCharacterStarterConquerPoints;
    }

    private static bool HasOnlyLegacyFirstCharacterStarterOrSourceRewardItems(
        PlayerItemSnapshot playerItems,
        LegacyFirstCharacterStarterProfile profile)
    {
        return playerItems.RelicFuseItems.Count == 0 &&
            playerItems.MythsoulBagItems.Count == 0 &&
            playerItems.RuneItems.Count == 0 &&
            playerItems.WardrobeItems.Count == 0 &&
            playerItems.WarehouseItems.Count == 0 &&
            HasOnlyLegacyFirstCharacterStarterEquipment(playerItems, profile) &&
            HasOnlyLegacyFirstCharacterSourceRewardInventoryItems(playerItems.InventoryItems);
    }

    private static bool HasOnlyLegacyFirstCharacterStarterEquipment(
        PlayerItemSnapshot playerItems,
        LegacyFirstCharacterStarterProfile profile)
    {
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

    private static bool HasOnlyLegacyFirstCharacterSourceRewardInventoryItems(
        IReadOnlyList<PlayerItemSnapshotItem> inventoryItems)
    {
        if (inventoryItems.Count == 0)
        {
            return true;
        }

        return inventoryItems
            .Select(item => item.ItemId)
            .Order()
            .SequenceEqual(SourceFirstCharacterStarterRewardItemIds.Order());
    }

    private static bool HasLegacyFirstCharacterSourceRewardInventoryItems(
        IReadOnlyList<PlayerItemSnapshotItem> inventoryItems)
    {
        return inventoryItems.Any(item => SourceFirstCharacterStarterRewardItemIds.Contains(item.ItemId));
    }

    private static uint CreateLegacyStarterItemUid(long characterUid, int index)
    {
        var seed = characterUid > 0
            ? (ulong)characterUid
            : 1UL;
        return checked((uint)((seed % 4_000_000_000UL) + 100UL + (uint)index));
    }

    private static uint CreateLegacyStarterColor(uint itemUid, uint itemId)
    {
        return 3U + ((itemUid + itemId) % 6U);
    }

    private static bool TryGetLegacyFirstCharacterStarterProfile(
        long classCode,
        out LegacyFirstCharacterStarterProfile profile)
    {
        return LegacyFirstCharacterStarterProfiles.TryGet(classCode, out profile);
    }

    private readonly record struct LegacyFirstCharacterStarterState(
        CharacterSnapshot Snapshot,
        PlayerItemSnapshot PlayerItems,
        bool Applied);

}
