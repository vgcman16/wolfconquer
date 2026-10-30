using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyEquipmentQuerySelector
{
    internal static IReadOnlyList<PlayerItemSnapshotItem> Select(WorldSession session)
    {
        // OnDequeue's dictionary: current NON-weapons first, then ComputeWeapons
        // Item1 and eligible Item2. Relative nonweapon source enumeration is not canonical.
        var result = LegacyEquipmentSelector.SelectCurrentEquipment(
                session.PlayerItems, session.AlternateEquipmentActive)
            .Where(item => item.Position % 50 is not (4 or 5)).ToList();
        var (right, left) = ComputeWeapons(session);
        if (right is null) return result;
        Add(right);
        if (left is not null && IsEligibleLeftWeapon(session.Character, session.PlayerSpells, right, left)) Add(left);
        return result;

        void Add(PlayerItemSnapshotItem item)
        {
            if (!result.Any(existing => existing.Position == item.Position)) result.Add(item);
        }
    }

    private static (PlayerItemSnapshotItem? Right, PlayerItemSnapshotItem? Left) ComputeWeapons(
        WorldSession session) => ComputeWeapons(session.PlayerItems, session.AlternateEquipmentActive, session.PlayerSpells);

    internal static PlayerItemSnapshotItem? SelectLeftWeapon(CharacterSnapshot character, PlayerItemSnapshot items,
        bool alternateEquipmentActive, PlayerSpellSnapshot? spells)
    {
        var (right, left) = ComputeWeapons(items, alternateEquipmentActive, spells);
        return right is not null && left is not null && IsEligibleLeftWeapon(character, spells, right, left) ? left : null;
    }

    private static (PlayerItemSnapshotItem? Right, PlayerItemSnapshotItem? Left) ComputeWeapons(
        PlayerItemSnapshot items, bool alternateEquipmentActive, PlayerSpellSnapshot? spells)
    {
        PlayerItemSnapshotItem? At(ushort position) =>
            items.EquipmentItems.FirstOrDefault(item => item.Position == position);
        var right = At(4);
        var left = At(5);
        var alternateRight = At(54);
        var alternateLeft = At(55);
        if (!alternateEquipmentActive || alternateRight is null) return (right, left);
        if (right is null || alternateLeft is not null) return (alternateRight, alternateLeft);
        if (left is null) return (alternateRight, null);
        if (IsTwoHand(alternateRight.ItemId))
        {
            if (IsArrow(left.ItemId))
                return IsBow(alternateRight.ItemId) ? (alternateRight, left) : (alternateRight, null);
            return IsShield(left.ItemId) && spells?.ContainsSpell(10_311) is true
                ? (alternateRight, left) : (alternateRight, null);
        }

        return !IsTwoHand(left.ItemId) ? (alternateRight, left) : (alternateRight, null);
    }

    private static bool IsEligibleLeftWeapon(
        CharacterSnapshot character, PlayerSpellSnapshot? spells, PlayerItemSnapshotItem right, PlayerItemSnapshotItem left)
    {
        var job = character.Class;
        var windWalker = job is >= 16_000 and <= 16_051;
        var taoist = job is >= 10_000 and <= 14_051;
        var warrior = job is >= 2_000 and <= 2_051;
        var flag = job is >= 1_001 and <= 8_051 || windWalker;
        var flag2 = job is >= 1_000 and <= 1_051 || warrior ||
            job is >= 5_001 and <= 14_051 || windWalker;
        if (IsPistol(left.ItemId) && !IsRapier(right.ItemId)) return false;
        // Source IsAsasin is initialized false and has no writer in this supplied
        // tree, so its Archer/two-hand jump is not invented from an attribute.
        if (job is >= 4_000 and <= 4_051 && IsKnife(left.ItemId)) flag2 = true;
        if ((taoist && !IsHossu(left.ItemId)) ||
            (IsTwoHand(right.ItemId) && !IsShield(left.ItemId)) ||
            (!IsBacksword(right.ItemId) && !IsTaoistEpic(right.ItemId) && IsHossu(left.ItemId)))
            return false;
        if (IsKnife(right.ItemId) && !IsKnife(left.ItemId)) return false;
        if (IsDagger(right.ItemId) && !IsDagger(left.ItemId)) return false;
        if (IsDagger(left.ItemId) || (IsBacksword(right.ItemId) && !IsShield(left.ItemId)) ||
            (taoist && IsShield(left.ItemId)) || !(flag || flag2) ||
            (right.ItemId / 1_000 != 500 && IsArrow(left.ItemId)))
            return false;
        return !IsTwoHander(right.ItemId) || IsArrow(left.ItemId) || warrior ||
            spells?.ContainsSpell(10_311) is true;
    }
}
