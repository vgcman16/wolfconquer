namespace WolfConquer.Core.World;

public enum LegacyConsumableItemKind
{
    Unknown = 0,
    Hitpoints = 1,
    Mana = 2,
    FullRestore = 3,
}

public enum LegacyConsumableItemUseOutcome
{
    Rejected = 0,
    MissingHitpointAmount = 1,
    MissingManaAmount = 2,
    NoChange = 3,
    Applied = 4,
}

public readonly record struct LegacyConsumableItemUseResult(
    uint ItemTypeId,
    LegacyConsumableItemKind Kind,
    LegacyConsumableItemUseOutcome Outcome,
    uint CurrentHitpoints,
    uint UpdatedHitpoints,
    uint MaxHitpoints,
    uint HitpointAmount,
    uint CurrentMana,
    uint UpdatedMana,
    uint MaxMana,
    uint ManaAmount)
{
    public bool IsRecognized => Kind != LegacyConsumableItemKind.Unknown;

    public bool ShouldConsumeItem => Outcome == LegacyConsumableItemUseOutcome.Applied;

    public bool HitpointsChanged => UpdatedHitpoints != CurrentHitpoints;

    public bool ManaChanged => UpdatedMana != CurrentMana;

    public bool RequiresItemTypeAmounts =>
        Kind is LegacyConsumableItemKind.Hitpoints or LegacyConsumableItemKind.Mana;
}

public static class LegacyConsumableItemUse
{
    public const uint FullRestoreItemTypeId = 723_726;

    public static LegacyConsumableItemKind Classify(uint itemTypeId)
    {
        if (IsLegacyFullRestoreConsumable(itemTypeId))
        {
            return LegacyConsumableItemKind.FullRestore;
        }

        if (IsLegacyHealthConsumable(itemTypeId))
        {
            return LegacyConsumableItemKind.Hitpoints;
        }

        return IsLegacyManaConsumable(itemTypeId)
            ? LegacyConsumableItemKind.Mana
            : LegacyConsumableItemKind.Unknown;
    }

    public static bool IsLegacyFullRestoreConsumable(uint itemTypeId)
    {
        return itemTypeId == FullRestoreItemTypeId;
    }

    public static bool IsLegacyHealthConsumable(uint itemTypeId)
    {
        return (itemTypeId >= 1_000_000 && itemTypeId <= 1_000_040) ||
            (itemTypeId >= 1_002_000 && itemTypeId < 1_002_030) ||
            itemTypeId is 1_002_050 or 725_065 or 1_003_010;
    }

    public static bool IsLegacyManaConsumable(uint itemTypeId)
    {
        return (itemTypeId >= 1_001_000 && itemTypeId <= 1_001_040) ||
            itemTypeId is 1_002_030 or 1_002_040 or 725_066 or 1_004_010;
    }

    public static LegacyConsumableItemUseResult Calculate(
        uint itemTypeId,
        uint currentHitpoints,
        uint maxHitpoints,
        uint currentMana,
        uint maxMana,
        uint itemHitpoints = 0,
        uint itemMana = 0,
        uint hitpointMultiplierPercent = 100)
    {
        var kind = Classify(itemTypeId);
        var updatedHitpoints = currentHitpoints;
        var updatedMana = currentMana;
        var hitpointAmount = 0u;
        var manaAmount = 0u;
        var outcome = LegacyConsumableItemUseOutcome.Applied;

        switch (kind)
        {
            case LegacyConsumableItemKind.FullRestore:
                if (currentHitpoints != maxHitpoints)
                {
                    updatedHitpoints = Math.Max(currentHitpoints, maxHitpoints);
                    hitpointAmount = updatedHitpoints - currentHitpoints;
                    updatedMana = Math.Max(currentMana, maxMana);
                    manaAmount = updatedMana - currentMana;
                }

                break;

            case LegacyConsumableItemKind.Hitpoints:
                hitpointAmount = itemHitpoints;
                if (hitpointAmount == 0)
                {
                    outcome = LegacyConsumableItemUseOutcome.MissingHitpointAmount;
                    break;
                }

                hitpointAmount = ApplyLegacyHitpointMultiplier(hitpointAmount, hitpointMultiplierPercent);
                updatedHitpoints = AddAndClamp(currentHitpoints, hitpointAmount, maxHitpoints);
                break;

            case LegacyConsumableItemKind.Mana:
                manaAmount = itemMana;
                if (manaAmount == 0)
                {
                    outcome = LegacyConsumableItemUseOutcome.MissingManaAmount;
                    break;
                }

                updatedMana = AddAndClamp(currentMana, manaAmount, maxMana);
                break;

            default:
                outcome = LegacyConsumableItemUseOutcome.Rejected;
                break;
        }

        if (outcome == LegacyConsumableItemUseOutcome.Applied &&
            updatedHitpoints == currentHitpoints &&
            updatedMana == currentMana)
        {
            outcome = LegacyConsumableItemUseOutcome.NoChange;
        }

        return new LegacyConsumableItemUseResult(
            itemTypeId,
            kind,
            outcome,
            currentHitpoints,
            updatedHitpoints,
            maxHitpoints,
            hitpointAmount,
            currentMana,
            updatedMana,
            maxMana,
            manaAmount);
    }

    private static uint AddAndClamp(uint currentValue, uint amount, uint maxValue)
    {
        if (currentValue >= maxValue) return currentValue;
        return (uint)Math.Min((ulong)maxValue, (ulong)currentValue + amount);
    }

    private static uint ApplyLegacyHitpointMultiplier(uint amount, uint multiplierPercent)
    {
        if (multiplierPercent == 100)
        {
            return amount;
        }

        return (uint)Math.Min(uint.MaxValue, ((ulong)amount * multiplierPercent) / 100);
    }
}
