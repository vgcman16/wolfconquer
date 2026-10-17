using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryCreateLegacyFullAccountClassProjection(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyFullAccountClassGrant grant,
        out LegacyFullAccountClassProjection projection,
        out string reason)
    {
        projection = default!;
        reason = string.Empty;
        var character = session.Character;
        if (character.Uid is < 0 or > uint.MaxValue ||
            character.Strength is < 0 or > ushort.MaxValue ||
            character.Agility is < 0 or > ushort.MaxValue ||
            character.Vitality is < 0 or > ushort.MaxValue ||
            character.Spirit is < 0 or > ushort.MaxValue ||
            character.AttributePoints is < 0 or > ushort.MaxValue ||
            character.MinHitPoints is < 0 or > uint.MaxValue ||
            character.Experience < 0)
        {
            reason = "character-scalar-out-of-source-range";
            return false;
        }

        if (session.PlayerSpells.Spells.Select(spell => spell.Id).Distinct().Count() !=
            session.PlayerSpells.Spells.Count)
        {
            reason = "duplicate-player-spell-id";
            return false;
        }

        var explicitAttributes = character.AttributePoints + LegacyFullAccountClassAttributeBonus;
        if (explicitAttributes > ushort.MaxValue)
        {
            reason = "explicit-attribute-bonus-overflow";
            return false;
        }

        var initialCharacter = WithLegacyFullAccountClassAttributes(
            character with
            {
                Agility = 0,
                Strength = 0,
                Vitality = 1,
                Spirit = 0,
                AttributePoints = explicitAttributes,
            });

        var levelChanged = character.Level != LegacyFullAccountClassTargetLevel;
        var sendLevelSourceStats = false;
        var levelAttributePointsAdded = 0L;
        if (levelChanged && character.Level < LegacyFullAccountClassTargetLevel)
        {
            var firstLifeAutoStats = character.Reborn == 0 &&
                (IsLegacyFullAccountWaterClass(character.Class)
                    ? character.Level < 110
                    : character.Level < 120);
            if (firstLifeAutoStats)
            {
                sendLevelSourceStats = true;
            }
            else
            {
                levelAttributePointsAdded = checked(
                    (LegacyFullAccountClassTargetLevel - character.Level) * 3);
            }
        }

        var finalAttributes = checked(explicitAttributes + levelAttributePointsAdded);
        if (finalAttributes > ushort.MaxValue)
        {
            reason = "level-attribute-bonus-overflow";
            return false;
        }

        var leveledCharacter = WithLegacyFullAccountClassAttributes(
            initialCharacter with
            {
                Level = LegacyFullAccountClassTargetLevel,
                AttributePoints = finalAttributes,
            });
        if (levelChanged)
        {
            var maxHitpoints = LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(
                leveledCharacter,
                currentHitpoints: 0,
                session.PlayerItems,
                session.CombatGear ?? CombatGearSnapshot.Empty,
                context.WorldData,
                session.AlternateEquipmentActive);
            leveledCharacter = WithLegacyFullAccountClassAttributes(
                leveledCharacter with { MinHitPoints = maxHitpoints });
        }

        var updatedCharacter = WithLegacyFullAccountClassAttributes(
            leveledCharacter with
            {
                Reborn = 2,
                Class = grant.TargetClass,
            });
        var updatedSpellEntries = grant.ClearExistingSpells
            ? new List<PlayerSpellSnapshotEntry>()
            : session.PlayerSpells.Spells.ToList();
        var addPackets = new List<PlayerSpellSnapshotEntry>(grant.SpellIds.Count);
        foreach (var spellId in grant.SpellIds)
        {
            var resetSpell = new PlayerSpellSnapshotEntry(
                spellId,
                Level: 0,
                Experience: 0,
                PreviousLevel: 0,
                SoulLevel: 0,
                UseSpellSoul: 0);
            addPackets.Add(resetSpell);
            var existingIndex = updatedSpellEntries.FindIndex(spell => spell.Id == spellId);
            if (existingIndex >= 0)
            {
                updatedSpellEntries[existingIndex] = resetSpell;
            }
            else
            {
                updatedSpellEntries.Add(resetSpell);
            }
        }

        projection = new LegacyFullAccountClassProjection(
            initialCharacter,
            leveledCharacter,
            updatedCharacter,
            new PlayerSpellSnapshot(updatedSpellEntries.ToArray()),
            grant.ClearExistingSpells
                ? session.PlayerSpells.Spells.Select(spell => spell.Id).ToArray()
                : [],
            addPackets,
            levelChanged,
            sendLevelSourceStats,
            levelAttributePointsAdded);
        return true;
    }

    private static IReadOnlyList<LegacyMsgUpdatePacket> CreateLegacyFullAccountInitialStatUpdates(
        uint uid,
        CharacterSnapshot character)
    {
        return
        [
            CreateLegacyFullAccountScalarUpdate(uid, LegacyMsgUpdateDataType.Agility, character.Agility),
            CreateLegacyFullAccountScalarUpdate(uid, LegacyMsgUpdateDataType.Strength, character.Strength),
            CreateLegacyFullAccountScalarUpdate(uid, LegacyMsgUpdateDataType.Vitality, character.Vitality),
            CreateLegacyFullAccountScalarUpdate(uid, LegacyMsgUpdateDataType.Spirit, character.Spirit),
            CreateLegacyFullAccountScalarUpdate(uid, LegacyMsgUpdateDataType.Atributes, character.AttributePoints),
        ];
    }

    private static IReadOnlyList<LegacyMsgUpdatePacket> CreateLegacyFullAccountLevelSourceStatUpdates(
        uint uid,
        CharacterSnapshot character)
    {
        return
        [
            CreateLegacyFullAccountScalarUpdate(uid, LegacyMsgUpdateDataType.Strength, character.Strength),
            CreateLegacyFullAccountScalarUpdate(uid, LegacyMsgUpdateDataType.Agility, character.Agility),
            CreateLegacyFullAccountScalarUpdate(uid, LegacyMsgUpdateDataType.Spirit, character.Spirit),
            CreateLegacyFullAccountScalarUpdate(uid, LegacyMsgUpdateDataType.Vitality, character.Vitality),
        ];
    }

    private static LegacyMsgUpdatePacket CreateLegacyFullAccountScalarUpdate(
        uint uid,
        LegacyMsgUpdateDataType type,
        long value)
    {
        return new LegacyMsgUpdatePacket(uid, [new LegacyMsgUpdateEntry(type, CheckedUInt64(value, type.ToString()))]);
    }

    private static CharacterSnapshot WithLegacyFullAccountClassAttributes(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Class"] = character.Class.ToString(CultureInfo.InvariantCulture),
            ["Reborn"] = character.Reborn.ToString(CultureInfo.InvariantCulture),
            ["Level"] = character.Level.ToString(CultureInfo.InvariantCulture),
            ["Agility"] = character.Agility.ToString(CultureInfo.InvariantCulture),
            ["Strength"] = character.Strength.ToString(CultureInfo.InvariantCulture),
            ["Vitaliti"] = character.Vitality.ToString(CultureInfo.InvariantCulture),
            ["Spirit"] = character.Spirit.ToString(CultureInfo.InvariantCulture),
            ["Atributes"] = character.AttributePoints.ToString(CultureInfo.InvariantCulture),
            ["MinHitPoints"] = character.MinHitPoints.ToString(CultureInfo.InvariantCulture),
        };
        return character with { Attributes = attributes };
    }

    private static IReadOnlyDictionary<string, string> CreateLegacyFullAccountClassPersistenceValues(
        CharacterSnapshot character)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Class"] = character.Class.ToString(CultureInfo.InvariantCulture),
            ["Reborn"] = character.Reborn.ToString(CultureInfo.InvariantCulture),
            ["Level"] = character.Level.ToString(CultureInfo.InvariantCulture),
            ["Agility"] = character.Agility.ToString(CultureInfo.InvariantCulture),
            ["Strength"] = character.Strength.ToString(CultureInfo.InvariantCulture),
            ["Vitaliti"] = character.Vitality.ToString(CultureInfo.InvariantCulture),
            ["Spirit"] = character.Spirit.ToString(CultureInfo.InvariantCulture),
            ["Atributes"] = character.AttributePoints.ToString(CultureInfo.InvariantCulture),
            ["MinHitPoints"] = character.MinHitPoints.ToString(CultureInfo.InvariantCulture),
        };
    }
}
