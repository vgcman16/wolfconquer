using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeCalculatePhysical
{
    private readonly record struct PhysicalMonsterOwners(
        int MinimumDamage, LegacyNativeRuneItemStorage Rune, LegacyNativeMyCombatGear CombatGear,
        LegacyNativeNinja? Ninja, LegacyNativeNinja.EquippedSigilCapture? Sigil,
        LegacyNativeMonsterFamily Family, string Name,
        uint TargetUid, ushort? SpellId)
    {
        internal bool IsCurrent(LegacyNativeGameClientRuntime client, LegacyNativeMonsterRole monster,
            LegacyNativeMagicType.Magic? spell) =>
            Rune is not null && CombatGear is not null && Family is not null && Name is not null &&
            ReferenceEquals(client.Rune, Rune) && Rune.IsSaveOwnedBy(client) &&
            ReferenceEquals(client.CombatGear, CombatGear) && CombatGear.IsCombatOwnedBy(client) &&
            ReferenceEquals(monster.Family, Family) && monster.UID == TargetUid &&
            string.Equals(monster.Name, Name, StringComparison.Ordinal) && spell?.ID == SpellId &&
            (SpellId is not (16420 or 16430) ||
                (Ninja is not null && ReferenceEquals(client.MyNinja, Ninja) && Ninja.IsCombatOwnedBy(client))) &&
            (Sigil is null || Sigil.IsCurrent());
    }

    private static PhysicalMonsterOwners CapturePhysicalMonsterOwners(
        LegacyNativeGameClientRuntime client, LegacyNativeMonsterRole monster,
        LegacyNativeMagicType.Magic? spell, int minimumDamage)
    {
        var rune = client.Rune;
        var combatGear = client.CombatGear;
        var family = monster.Family;
        var name = monster.Name;
        var spellId = spell?.ID;
        // Unrelated spells do not query or require the optional Ninja system.
        var ninja = spellId is 16420 or 16430 ? client.MyNinja : null;
        if (rune is null || !rune.IsSaveOwnedBy(client) ||
            combatGear is null || !combatGear.IsCombatOwnedBy(client) ||
            family is null || name is null ||
            ((spellId is 16420 or 16430) && (ninja is null || !ninja.IsCombatOwnedBy(client))))
        {
            throw UnavailablePhysicalMonsterOwners();
        }

        var sigil = ninja?.CaptureEquippedSigil(client, LegacyNativeNinja.ItemType.WhirlSigilSage);
        var owners = new PhysicalMonsterOwners(minimumDamage, rune, combatGear, ninja, sigil,
            family, name, monster.UID, spellId);
        if (!owners.IsCurrent(client, monster, spell))
        {
            throw UnavailablePhysicalMonsterOwners();
        }

        return owners;
    }

    private static InvalidDataException UnavailablePhysicalMonsterOwners() =>
        new("Physical monster calculation requires its actual combat owners and complete monster metadata.");
}
