namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryPrepareLegacyGenericMagicResources(
        WorldPacketHandlerContext context, WorldSession expected, LegacyMagicRuntimeSpell spell,
        List<byte[]> frames, out WorldSession candidate, out bool graceOfHeaven, out string reason)
    {
        candidate = expected;
        graceOfHeaven = false;
        reason = string.Empty;
        if (expected.Character.MapId == 1_039) return true;

        // Keep the generic Scurvy + DragonPierce stamina rules. When mana is
        // zero, no optional rune catalog is needed: the existing calculator's
        // null-catalog branch returns before enumerating rune equipment.
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            spell, expected.PlayerItems, spell.UseMana == 0 ? null : context.WorldData,
            expected.Character.Attributes, expected.AlternateEquipmentActive, expected.Character.Class);
        var character = expected.Character;
        var stamina = ReadLegacyRuntimeStamina(character);
        if (stamina < cost.UseStamina)
        { reason = $"stamina={stamina} requiredStamina={cost.UseStamina}"; return false; }
        if (character.MinMana < cost.UseMana)
        { reason = $"mana={character.MinMana} requiredMana={cost.UseMana}"; return false; }

        // Grace still requires sufficient priced mana. It only prevents the
        // actual mana debit and cooldown; no new class/equipment rule is added.
        graceOfHeaven = (cost.UseMana > 0 || LegacyMagicRuntime.UsesLegacyCooldown(spell)) &&
            !LegacyMagicResourceCostCalculator.IsLegacyResourceFreeSpell(spell.Id) &&
            HasLegacyGraceOfHeavenResourceBypass(expected);
        if (cost.UseStamina > 0)
        {
            var remaining = stamina - cost.UseStamina;
            character = WithCharacterAttribute(character, "Stamina", remaining);
            frames.Add(LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(
                CheckedUInt32(character.Uid, nameof(character.Uid)), remaining).ToFrameBytes());
        }
        if (cost.UseMana > 0 && !graceOfHeaven)
        {
            var remaining = checked(character.MinMana - cost.UseMana);
            var wireMana = CheckedUInt32(remaining, nameof(character.MinMana));
            character = character with { MinMana = remaining };
            frames.Add(new LegacyMsgUpdatePacket(CheckedUInt32(character.Uid, nameof(character.Uid)),
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, wireMana)]).ToFrameBytes());
        }
        if (!ReferenceEquals(character, expected.Character)) candidate = expected with { Character = character };
        return true;
    }
}
