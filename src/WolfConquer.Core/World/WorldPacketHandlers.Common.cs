using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendLegacySetLocationAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        CharacterSnapshot character,
        CancellationToken cancellationToken)
    {
        var packet = new MsgActionPacket
        {
            ObjId = CheckedUInt32(character.Uid, nameof(character.Uid)),
            DwParam = CheckedUInt32(character.MapId, nameof(character.MapId)),
            Timestamp = 0,
            Type = MsgActionType.SetLocation,
            Facing = ReadAttributeUInt16(character.Attributes, "Angle"),
            PositionX = CheckedUInt32(character.X, nameof(character.X)),
            PositionY = CheckedUInt32(character.Y, nameof(character.Y)),
        };

        await connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
    }

    private static ulong ReadChampionPoints(CharacterSnapshot character)
    {
        return ReadAttributeUInt64(character.Attributes, "ChampionPoints");
    }



    private static int CalculateLegacyChiScore(LegacyChiPowerState power)
    {
        var score = 0;
        foreach (var attribute in power.Attributes)
        {
            var max = LegacyChiMaxValue(attribute.Type);
            var min = LegacyChiMinValue(attribute.Type);
            if (max <= min)
            {
                continue;
            }

            var boundedValue = Math.Clamp(attribute.Value, min, max);
            score += (int)((uint)(boundedValue - min) / ((double)(max - min) / 100.0));
        }

        return score;
    }

    private static int LegacyChiMaxValue(byte attributeType)
    {
        return (LegacyChiAttributeType)attributeType switch
        {
            LegacyChiAttributeType.CriticalStrike => 200,
            LegacyChiAttributeType.SkillCriticalStrike => 200,
            LegacyChiAttributeType.Immunity => 200,
            LegacyChiAttributeType.Breakthrough => 200,
            LegacyChiAttributeType.Counteraction => 200,
            LegacyChiAttributeType.HPAdd => 3_500,
            LegacyChiAttributeType.AddAttack => 2_000,
            LegacyChiAttributeType.AddMagicAttack => 2_500,
            LegacyChiAttributeType.AddMagicDefense => 250,
            LegacyChiAttributeType.PhysicalDamageIncrease => 500,
            LegacyChiAttributeType.PhysicalDamageDecrease => 500,
            LegacyChiAttributeType.MagicDamageIncrease => 300,
            LegacyChiAttributeType.MagicDamageDecrease => 300,
            _ => 100,
        };
    }

    private static int LegacyChiMinValue(byte attributeType)
    {
        return (LegacyChiAttributeType)attributeType switch
        {
            LegacyChiAttributeType.CriticalStrike => 10,
            LegacyChiAttributeType.SkillCriticalStrike => 10,
            LegacyChiAttributeType.Immunity => 10,
            LegacyChiAttributeType.Breakthrough => 10,
            LegacyChiAttributeType.Counteraction => 10,
            LegacyChiAttributeType.HPAdd => 1_000,
            LegacyChiAttributeType.AddAttack => 500,
            LegacyChiAttributeType.AddMagicAttack => 500,
            LegacyChiAttributeType.AddMagicDefense => 50,
            LegacyChiAttributeType.PhysicalDamageIncrease => 50,
            LegacyChiAttributeType.PhysicalDamageDecrease => 50,
            LegacyChiAttributeType.MagicDamageIncrease => 50,
            LegacyChiAttributeType.MagicDamageDecrease => 50,
            _ => 10,
        };
    }

    private static uint SaturatingUInt32(ulong value, uint multiplier)
    {
        var result = value * multiplier;
        return result > uint.MaxValue ? uint.MaxValue : (uint)result;
    }

    private static bool TryParseUInt32(string value, out uint parsed)
    {
        return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static bool TryReadInt32(IReadOnlyList<string> fields, int index, out int parsed)
    {
        parsed = 0;
        return fields.Count > index &&
            int.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static CharacterSnapshot WithLegacyDynamicMapTeleport(
        CharacterSnapshot character,
        long mapId,
        long x,
        long y,
        long dynamicId)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture),
        };

        return character with
        {
            MapId = mapId,
            X = x,
            Y = y,
            Attributes = attributes,
        };
    }

    private static string? TryDeriveLegacyDbRoot(string? usersRoot)
    {
        if (string.IsNullOrWhiteSpace(usersRoot))
        {
            return null;
        }

        var trimmed = Path.TrimEndingDirectorySeparator(usersRoot);
        return string.Equals(Path.GetFileName(trimmed), "Users", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(trimmed)
            : trimmed;
    }







    private static Encoding CreateLegacyCharacterSnapshotEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }





    private static CharacterSnapshot WithoutLegacyDeathRevivePromptAttributes(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyPlayerDeathState.RevivePromptVisibleAttribute);
        if (attributes.TryGetValue(RuntimePendingMessageBoxAttribute, out var pending) &&
            pending.Equals(LegacyPlayerDeathState.RevivePromptPendingKey, StringComparison.Ordinal))
        {
            attributes.Remove(RuntimePendingMessageBoxAttribute);
            attributes.Remove(RuntimePendingMessageBoxMapIdAttribute);
            attributes.Remove(RuntimePendingMessageBoxXAttribute);
            attributes.Remove(RuntimePendingMessageBoxYAttribute);
            attributes.Remove(RuntimePendingMessageBoxDynamicIdAttribute);
            attributes.Remove(RuntimePendingMessageBoxSecondsAttribute);
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithoutLegacyStatusFlags(CharacterSnapshot character)
    {
        character = LegacyLightningShieldBaseLifetime.Forget(character);
        character = LegacyLightningShieldActivationLifetime.Forget(character);
        character = LegacyArrowBladesFlagRegistration.Forget(character, LegacyArrowBladesFlagRegistration.FireFlag);
        character = LegacyArrowBladesFlagRegistration.Forget(character, LegacyArrowBladesFlagRegistration.WaterFlag);
        character = LegacyArrowBladesFlagRegistration.Forget(character, LegacyArrowBladesFlagRegistration.StarFlowFlag);
        character = LegacyArrowBladesFlagRegistration.Forget(character, LegacyArrowBladesFlagRegistration.StarFlowTriggerFlag);
        return WithoutCharacterAttribute(
            WithoutCharacterAttribute(character, "StatusFlags"),
            "BitVector");
    }







































    private static bool IsLegacyBoyBody(long body)
    {
        return body / 1000 == 1;
    }

    private static bool IsLegacyGirlBody(long body)
    {
        return body / 1000 == 2;
    }





    private static CharacterSnapshot WithoutRuntimeMessageBoxAttributes(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        attributes.Remove(RuntimePendingMessageBoxAttribute);
        attributes.Remove(RuntimePendingMessageBoxMapIdAttribute);
        attributes.Remove(RuntimePendingMessageBoxXAttribute);
        attributes.Remove(RuntimePendingMessageBoxYAttribute);
        attributes.Remove(RuntimePendingMessageBoxDynamicIdAttribute);
        attributes.Remove(RuntimePendingMessageBoxSecondsAttribute);
        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithRuntimeMessageBoxPrompt(
        CharacterSnapshot character,
        LegacyRuntimeMessageBoxPrompt prompt)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            [RuntimePendingMessageBoxAttribute] = prompt.PendingKey,
            [RuntimePendingMessageBoxMapIdAttribute] = prompt.MapId.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxXAttribute] = prompt.X.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxYAttribute] = prompt.Y.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxDynamicIdAttribute] = prompt.DynamicId.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxSecondsAttribute] = prompt.Seconds.ToString(CultureInfo.InvariantCulture),
        };

        return character with { Attributes = attributes };
    }

    private static bool IsRuntimeTopInHoursInvitationMessageBox(string pending)
    {
        return pending.Equals(RuntimeTopInHoursInvitationMessageBox, StringComparison.Ordinal) ||
            pending.StartsWith("TopInHours:", StringComparison.Ordinal);
    }

    private static bool TryReadTopInHoursPendingDestination(
        string pending,
        out long mapId,
        out long x,
        out long y)
    {
        mapId = 0;
        x = 0;
        y = 0;
        const string prefix = "TopInHours:";
        if (!pending.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(
                pending.AsSpan(prefix.Length),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var minute) ||
            !LegacyTopInHoursInvitationScheduler.TryGetInvitationForMinute(minute, out var invitation))
        {
            return false;
        }

        mapId = invitation.MapId;
        x = invitation.X;
        y = invitation.Y;
        return true;
    }

    private static bool TryReadPendingMessageBoxDestination(
        IReadOnlyDictionary<string, string> attributes,
        out long mapId,
        out long x,
        out long y)
    {
        mapId = 0;
        x = 0;
        y = 0;
        return TryReadInt64Attribute(attributes, RuntimePendingMessageBoxMapIdAttribute, out mapId) &&
            TryReadInt64Attribute(attributes, RuntimePendingMessageBoxXAttribute, out x) &&
            TryReadInt64Attribute(attributes, RuntimePendingMessageBoxYAttribute, out y);
    }

    private static bool HasActivePick(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(character.Attributes, "ActivePick");
        return raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value != 0);
    }

    private static CharacterSnapshot WithLegacyStatusFlag(CharacterSnapshot character, int flag)
    {
        if (flag == 212 && !HasLegacyStatusFlag(character, flag))
            character = LegacyFineRain212Invocation.Forget(character);
        if (flag == LegacyLightningShieldActivationLifetime.StatusFlag && !HasLegacyStatusFlag(character, flag))
            character = LegacyLightningShieldActivationLifetime.Forget(character);
        if (flag == LegacyLightningShieldBaseLifetime.StatusFlag && !HasLegacyStatusFlag(character, flag))
            character = LegacyLightningShieldBaseLifetime.Forget(character);
        if (LegacyArrowBladesFlagRegistration.IsFamilyFlag(flag) && !HasLegacyStatusFlag(character, flag))
            character = LegacyArrowBladesFlagRegistration.Forget(character, flag);
        character = LegacyStatusFlagDeathRegistration.PrepareAdd(character, flag);
        if (flag < 0)
        {
            return character;
        }

        var key = "StatusFlags";
        var raw = "";
        if (TryGetLegacyStatusFlagsAttribute(character.Attributes, out var existingKey, out var existingRaw))
        {
            key = existingKey;
            raw = existingRaw;
        }

        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wordCount = Math.Max(Math.Max(10, parts.Length), wordIndex + 1);
        var words = new ulong[wordCount];
        for (var index = 0; index < Math.Min(parts.Length, words.Length); index++)
        {
            ulong.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out words[index]);
        }

        words[wordIndex] |= 1UL << bitIndex;
        return WithCharacterAttribute(character, key, string.Join('/', words) + "/");
    }

    private static CharacterSnapshot WithOnlyLegacyStatusFlag(CharacterSnapshot character, params int[] flags)
    {
        if (flags.Length == 0 || flags.All(flag => flag < 0))
        {
            return character;
        }

        var key = "StatusFlags";
        if (TryGetLegacyStatusFlagsAttribute(character.Attributes, out var existingKey, out _))
        {
            key = existingKey;
        }

        var maxWordIndex = flags.Where(flag => flag >= 0).Select(flag => flag / 32).Max();
        var words = new ulong[Math.Max(10, maxWordIndex + 1)];
        foreach (var flag in flags)
        {
            if (flag < 0)
            {
                continue;
            }

            var wordIndex = flag / 32;
            var bitIndex = flag % 32;
            words[wordIndex] |= 1UL << bitIndex;
        }

        return WithCharacterAttribute(character, key, string.Join('/', words) + "/");
    }

    private static IReadOnlyList<ushort> ReadShadowCloneFlags(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(character.Attributes, "ShadowCloneFlags", "MyCloneFlags", "CloneFlags");
        if (raw.Length == 0)
        {
            return [];
        }

        var fields = raw.Split(
            ['/', ',', ';', '|'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length == 0)
        {
            return [];
        }

        var flags = new List<ushort>(fields.Length);
        foreach (var field in fields)
        {
            if (ushort.TryParse(field, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flag))
            {
                flags.Add(flag);
            }
        }

        return flags;
    }

    private static CharacterSnapshot WithLegacyShadowCloneFlags(
        CharacterSnapshot character,
        IReadOnlyList<ushort> flags)
    {
        if (flags.Count == 0)
        {
            return WithoutLegacyShadowCloneFlags(character);
        }

        return WithCharacterAttribute(
            character,
            "ShadowCloneFlags",
            string.Join('/', flags.Select(flag => flag.ToString(CultureInfo.InvariantCulture))) + "/");
    }

    private static CharacterSnapshot WithoutLegacyShadowCloneFlags(CharacterSnapshot character)
    {
        return WithoutCharacterAttribute(
            WithoutCharacterAttribute(
                WithoutCharacterAttribute(character, "ShadowCloneFlags"),
                "MyCloneFlags"),
            "CloneFlags");
    }

    private static bool TryParseLegacyBool(string value, out bool parsed)
    {
        if (bool.TryParse(value, out parsed))
        {
            return true;
        }

        if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            parsed = number != 0;
            return true;
        }

        return false;
    }

    private static bool HasLegacyStatusFlag(CharacterSnapshot character, int flag)
    {
        if (flag < 0)
        {
            return false;
        }

        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        var raw = ReadAttributeString(character.Attributes, "StatusFlags", "BitVector");
        if (raw.Length == 0)
        {
            return false;
        }

        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (wordIndex >= parts.Length ||
            !ulong.TryParse(parts[wordIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var word))
        {
            return false;
        }

        var mask = 1UL << bitIndex;
        return (word & mask) == mask;
    }

    private static ulong[] CreateLegacyRuntimeStatusFlagVector(CharacterSnapshot character)
    {
        var values = new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength];
        var raw = ReadAttributeString(character.Attributes, "StatusFlags", "BitVector");
        if (raw.Length == 0)
        {
            return values;
        }

        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < Math.Min(parts.Length, values.Length); index++)
        {
            ulong.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[index]);
        }

        return values;
    }

    private static bool TryRemoveLegacyMovementStatusFlags(
        IReadOnlyDictionary<string, string> attributes,
        out IReadOnlyDictionary<string, string> updatedAttributes)
    {
        updatedAttributes = attributes;
        if (!TryGetLegacyStatusFlagsAttribute(attributes, out var key, out var raw) ||
            raw.Length == 0)
        {
            return false;
        }

        var parts = raw.Split('/');
        var removed = false;
        var removedAurasMagicDefender = false;
        foreach (var flag in LegacyMovementClearedFlags)
        {
            var wordIndex = flag / 32;
            if (wordIndex >= parts.Length ||
                !ulong.TryParse(parts[wordIndex].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var word))
            {
                continue;
            }

            var mask = 1UL << (flag % 32);
            if ((word & mask) != mask)
            {
                continue;
            }

            parts[wordIndex] = (word & ~mask).ToString(CultureInfo.InvariantCulture);
            removed = true;
            removedAurasMagicDefender |= flag == LegacyMagicDefenderFlag;
        }

        if (!removed)
        {
            return false;
        }

        var updated = new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase)
        {
            [key] = string.Join('/', parts),
        };
        if (removedAurasMagicDefender)
        {
            RemoveLegacyAurasMagicDefenderMetadata(updated);
        }

        updatedAttributes = updated;
        return true;
    }

    private static bool TryCancelLegacyIntensifyArm(
        IReadOnlyDictionary<string, string> attributes,
        out IReadOnlyDictionary<string, string> updatedAttributes)
    {
        updatedAttributes = attributes;
        if (!ReadAttributeBool(attributes, RuntimeLegacyInUseIntensifyAttribute))
        {
            return false;
        }

        updatedAttributes = new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeLegacyInUseIntensifyAttribute] = "false",
        };
        return true;
    }

    private static bool TryGetLegacyStatusFlagsAttribute(
        IReadOnlyDictionary<string, string> attributes,
        out string key,
        out string value)
    {
        if (attributes.TryGetValue("StatusFlags", out var statusFlags))
        {
            key = "StatusFlags";
            value = statusFlags ?? "";
            return true;
        }

        if (attributes.TryGetValue("BitVector", out var bitVector))
        {
            key = "BitVector";
            value = bitVector ?? "";
            return true;
        }

        key = "";
        value = "";
        return false;
    }

    private static uint ComputeLegacyRuntimeMaxHitpoints(
        CharacterSnapshot character,
        uint currentHitpoints,
        PlayerItemSnapshot playerItems,
        CombatGearSnapshot combatGear,
        WorldDataCatalog? worldData,
        bool alternateEquipmentActive = false)
    {
        return LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(
            character,
            currentHitpoints,
            playerItems,
            combatGear,
            worldData,
            alternateEquipmentActive);
    }

    private static async ValueTask SendScalarUpdateAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        LegacyMsgUpdateDataType dataType,
        ulong value,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(character.Uid, nameof(character.Uid)),
            [new LegacyMsgUpdateEntry(dataType, value)]);

        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
    }

    private static ValueTask SendConquerPointsUpdateAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken)
    {
        return SendScalarUpdateAsync(
            context,
            character,
            LegacyMsgUpdateDataType.ConquerPoints,
            checked((ulong)character.ConquerPoints),
            cancellationToken);
    }
}
