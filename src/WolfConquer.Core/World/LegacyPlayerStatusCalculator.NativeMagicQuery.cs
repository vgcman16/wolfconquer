using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Query-phase contributions only: never called by an attack/receive consumer.
public static partial class LegacyPlayerStatusCalculator
{
    internal static LegacyNativeMagicQueryFields NativeMagicItemAfterCircle(
        PlayerItemSnapshotItem item, WorldDataCatalog? data)
    {
        var position = item.Position % 50;
        if (!TryGetItemType(data?.ItemsById, item.ItemId, out var row)) return default;
        uint attack = 0, critical = 0, increase = 0;
        unchecked
        {
            var itemAttack = NativeMagicItemUShort(row, MagicAttackFieldIndex);
            if (position is 10 or 19) increase += itemAttack;
            else attack += position == 5 ? (uint)(itemAttack / 2) : itemAttack;
            if (position is not (11 or 19)) critical += ReadUIntField(row, SkillCriticalStrikeFieldIndex);
            if (NativeMagicEffectInLife(item.PurificationItemId, item.PurificationDuration, item.PurificationAddedOn) &&
                TryGetItemType(data?.ItemsById, item.PurificationItemId, out var purification))
            {
                attack += NativeMagicItemUShort(purification, MagicAttackFieldIndex);
                critical += ReadUIntField(purification, SkillCriticalStrikeFieldIndex);
            }
            if (NativeMagicEffectInLife(item.RefineryEffectId, item.RefineryEffectDuration, item.RefineryEffectAddedOn) &&
                TryGetRefineryMaterial(data?.RefineryMaterialsByItemType, item.RefineryEffectId, out var refinery))
            {
                // Source Rifinery reads uint Procent and Procent2, byte Type/Type2.
                AddRefinery(3, 2); AddRefinery(4, 5);
                void AddRefinery(int typeAt, int valueAt)
                {
                    if (refinery.Fields.Count <= Math.Max(typeAt, valueAt) ||
                        !byte.TryParse(refinery.Fields[typeAt], NumberStyles.Integer, CultureInfo.InvariantCulture, out var type) ||
                        !uint.TryParse(refinery.Fields[valueAt], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return;
                    if (type == 3) critical = unchecked(critical + value * 100);
                    else if (type == 12) increase = unchecked(increase + value);
                }
            }
            // Source Equip645-647 clamps Plus13+ to12 BEFORE its later item contribution.
            var plus = Math.Min((int)item.Plus, 12);
            if (!IsLegacyPlusClearedPosition(position) && plus > 0 &&
                TryGetItemAddBonus(data?.ItemAddBonusByItemTypeIdAndLevel, item.ItemId, plus, out var addition))
            {
                var plusAttack = (ushort)ReadItemAddValue(addition, ItemAddMagicAttackValueIndex);
                var percent = TryGetItemType(data?.ItemsById, item.AnimaItemId, out var plusAnima)
                    ? (byte)(NativeMagicItemUShort(plusAnima, ItemHitpointsFieldIndex) / 100) : (byte)0;
                if (position is 10 or 19) increase += plusAttack;
                else attack += (uint)(plusAttack + plusAttack * percent / 100); // ushort operands promote to int.
            }
            if (TryGetItemType(data?.ItemsById, item.AnimaItemId, out var anima))
                increase += NativeMagicItemUShort(anima, MagicDamageIncreaseFieldIndex);
            if (position == RelicPosition)
                foreach (var encoded in new[] { item.Attribute1, item.Attribute2, item.Attribute3, item.Attribute4, item.Attribute5 })
                {
                    if (encoded == 0) continue;
                    var value = unchecked((ushort)(encoded / 1_000)); // Native RelicAttribute.Value is ushort.
                    switch (encoded % 100)
                    {
                        case 2: critical += value; break;
                        case 8: attack += value; break;
                        case 11: increase += value; break;
                    }
                }
        }
        return new(attack, 0, critical, increase);
    }

    private static ushort NativeMagicItemUShort(ItemType item, int at) =>
        item.Fields.Count > at && ushort.TryParse(item.Fields[at], NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var value) ? value : (ushort)0;

    private static bool NativeMagicEffectInLife(uint id, uint duration, long addedOn)
    {
        if (duration == 0) return id > 1_000;
        try
        {
            // Source MsgItemExtra uses two absolute TimeSpan.TotalSeconds values then an int truncation.
            var expires = DateTime.FromBinary(addedOn).AddSeconds(duration);
            return (int)(new TimeSpan(expires.Ticks).TotalSeconds -
                new TimeSpan(DateTime.Now.Ticks).TotalSeconds) > 0;
        }
        catch (ArgumentException) { return false; }
    }

    internal static LegacyNativeMagicQueryFields NativeMagicItemGems(
        LegacyNativeMagicQueryFields current, PlayerItemSnapshotItem item)
    {
        var position = item.Position % 50;
        if (IsLegacySocketClearedPosition(position)) return current;
        ushort first = item.SocketOne, second = item.SocketTwo;
        NormalizeSocketPair(position, ref first, ref second);
        foreach (var gem in new[] { first, second })
        {
            // Source Equip.AddGem omits NormalPhoenix(1); do not copy the legacy projection's extra case.
            ushort value = gem switch
            {
                2 or 12 => 10, 3 or 13 => 15, 11 => 5,
                101 or 121 => 100, 102 or 122 => 300, 103 or 123 => 500,
                71 => 2, 72 => 4, 73 => 6, 21 => 10, 22 => 15, 23 => 25, _ => 0,
            };
            if (value == 0) continue;
            var count = current.TortoiseGemCount;
            if (gem is >= 71 and <= 73) count = unchecked((byte)(count + 1));
            var percent = current.MagicPercent;
            if (value is 5 or 10 or 15 && gem is not (11 or 12 or 13))
                percent = unchecked(percent + value);
            var thunder = current.ThunderGemValue;
            if (count <= 8 && gem / 10 == 10)
                thunder = unchecked((ushort)(thunder + value));
            current = current with { MagicPercent = percent, ThunderGemValue = thunder, TortoiseGemCount = count };
        }
        return current;
    }

    internal static LegacyNativeMagicQueryFields NativeMagicBeforeGear(
        WorldSession current, WorldDataCatalog? data, uint currentPrestige)
    {
        var f = new uint[LegacyPlayerStatusPacket.FieldCount];
        var source = NativeMagicBeastAndProfession(current.Character, data);
        ApplyChiAttributes(f, current.Character, data?.FateExperienceByType);
        // Native Equip1837 narrows CURRENT shared PrestigeLevel to ushort, including wrap to0.
        if (data is not null && data.ItemRefineEffectsById.TryGetValue(unchecked((ushort)currentPrestige), out var effects) &&
            effects.Count != 0 && effects[0].Values.Count > 1)
            f[LegacyPlayerStatusPacket.MagicAttackIndex] = unchecked(
                f[LegacyPlayerStatusPacket.MagicAttackIndex] + (uint)effects[0].Values[1]);
        var other = NativeMagicFields(f);
        return new(unchecked(source.MagicAttack + other.MagicAttack), 0,
            unchecked(source.SkillCStrike + other.SkillCStrike),
            unchecked(source.MagicDamageIncrease + other.MagicDamageIncrease));
    }

    internal static LegacyNativeMagicQueryFields NativeMagicGear(
        WorldSession current, WorldDataCatalog? data)
    {
        var f = new uint[LegacyPlayerStatusPacket.FieldCount];
        ApplyCombatGearAttributes(f, current.Character.Class, current.Character.Attributes,
            current.CombatGear ?? CombatGearSnapshot.Empty, current.AlternateEquipmentActive,
            current.PlayerSpells, data?.CombatGearByGearTypeAndLevel, data?.DaoqiDictTypesById,
            data?.CollectionRewardTiersByCollectionId, data?.GouyuTypesByType, data?.MagicByTypeId);
        return NativeMagicFields(f);
    }

    internal static LegacyNativeMagicQueryFields NativeMagicMythsoul(
        WorldSession current, WorldDataCatalog? data)
    {
        return NativeMagicMythsoulRows(current.PlayerItems, data);
    }

    internal static LegacyNativeMagicQueryFields NativeMagicRuneStorage(
        WorldSession current, WorldDataCatalog? data)
    {
        return NativeMagicRuneStorageRows(current.PlayerItems, data);
    }

    private static LegacyNativeMagicQueryFields NativeMagicFields(uint[] fields) =>
        new(fields[LegacyPlayerStatusPacket.MagicAttackIndex], fields[LegacyPlayerStatusPacket.MagicPercentIndex],
            fields[LegacyPlayerStatusPacket.SkillCStrikeIndex], fields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex]);
}
