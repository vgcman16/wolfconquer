namespace WolfConquer.Core.World;

public readonly record struct LegacyCombatGearDamageInputs(
    uint MinAttack,
    uint MaxAttack,
    uint PhysicalPercent,
    uint PhysicalDamageIncrease,
    uint PhysicalDefense,
    uint TortoiseGemDamagePercent,
    uint AdjustedPhysicalDefense,
    uint ItemBless,
    uint Dodge)
{
    public static LegacyCombatGearDamageInputs FromStatusPacket(
        LegacyPlayerStatusPacket status,
        uint minimumAdjustedPhysicalDefense = 0,
        LegacyPhysicalDefenseAdjustments physicalDefenseAdjustments = default)
    {
        ArgumentNullException.ThrowIfNull(status);

        return FromStatusFields(status.Fields, minimumAdjustedPhysicalDefense, physicalDefenseAdjustments);
    }

    public static LegacyCombatGearDamageInputs FromStatusFields(
        IReadOnlyList<uint> statusFields,
        uint minimumAdjustedPhysicalDefense = 0,
        LegacyPhysicalDefenseAdjustments physicalDefenseAdjustments = default)
    {
        ArgumentNullException.ThrowIfNull(statusFields);
        if (statusFields.Count < LegacyPlayerStatusPacket.FieldCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(statusFields),
                statusFields.Count,
                "Legacy combat gear damage inputs require a complete legacy player status field set.");
        }

        var physicalDefense = statusFields[LegacyPlayerStatusPacket.DefenceIndex];
        var tortoiseDamagePercent = statusFields[LegacyPlayerStatusPacket.DamageIndex];
        var adjustedPhysicalDefense = LegacyCombatDamageCalculator.CalculateAdjustedPhysicalDefense(
            physicalDefense,
            tortoiseDamagePercent,
            physicalDefenseAdjustments);

        return new LegacyCombatGearDamageInputs(
            MinAttack: statusFields[LegacyPlayerStatusPacket.MinAttackIndex],
            MaxAttack: statusFields[LegacyPlayerStatusPacket.MaxAttackIndex],
            PhysicalPercent: statusFields[LegacyPlayerStatusPacket.PhysicalPercentIndex],
            PhysicalDamageIncrease: statusFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            PhysicalDefense: physicalDefense,
            TortoiseGemDamagePercent: tortoiseDamagePercent,
            AdjustedPhysicalDefense: Math.Max(adjustedPhysicalDefense, minimumAdjustedPhysicalDefense),
            ItemBless: statusFields[LegacyPlayerStatusPacket.ItemBlessIndex],
            Dodge: statusFields[LegacyPlayerStatusPacket.DodgeIndex]);
    }
}
