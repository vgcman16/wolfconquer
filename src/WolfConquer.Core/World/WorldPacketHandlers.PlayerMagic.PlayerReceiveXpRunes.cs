using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyXpKillerRuneItemBase = 4_030_600;
    private const uint LegacyXpKillerRuneItemMax = 4_030_609;
    private const uint LegacyXpBoosterRuneItemBase = 4_031_100;
    private const uint LegacyXpBoosterRuneItemMax = 4_031_109;
    private const uint LegacyTidalWaveRuneItemBase = 4_033_100;
    private const uint LegacyTidalWaveRuneItemMax = 4_033_109;
    private const uint LegacyQuenchRuneItemBase = 4_033_600;
    private const uint LegacyQuenchRuneItemMax = 4_033_609;
    private const int LegacyTidalWaveFlag = 0xF5;
    private const int LegacyQuenchFlag = 0xF6;
    private const int LegacyPlayerReceiveXpRuneCooldownSeconds = 60;
    private const int LegacyPlayerReceiveXpRuneStatusSeconds = 5;
    private const string RuntimeLegacyXpBoosterAttribute = "XPBooster";
    private const string RuntimeLegacyXpKillerAttribute = "XPKiller";
    private const string RuntimeLegacyTidalWaveStampUtcAttribute = "LegacyTidalWaveStampUtc";
    private const string RuntimeLegacyQuenchStampUtcAttribute = "LegacyQuenchStampUtc";
    internal const string RuntimeLegacyTidalWaveExpiresAtUtcAttribute = "LegacyTidalWaveExpiresAtUtc";
    internal const string RuntimeLegacyQuenchExpiresAtUtcAttribute = "LegacyQuenchExpiresAtUtc";

    private readonly record struct LegacyPlayerReceiveXpRunesResult(
        bool XpBoosterApplied,
        bool XpBoosterDepleted,
        bool XpKillerApplied,
        bool TidalWaveApplied,
        bool QuenchApplied,
        byte TidalWaveValue,
        byte QuenchValue);

    private static LegacyPlayerReceiveXpRunesResult TryApplyLegacyPlayerReceiveXpRunes(
        CharacterSnapshot attacker,
        CharacterSnapshot target,
        WorldSession? attackerSession,
        out CharacterSnapshot updatedAttacker,
        out CharacterSnapshot updatedTarget)
    {
        updatedAttacker = attacker;
        updatedTarget = target;
        if (attackerSession is null)
        {
            return default;
        }

        var now = DateTimeOffset.UtcNow;
        var xpBoosterApplied = false;
        var xpBoosterDepleted = false;
        if (IsLegacyPlayerOnXpSkill(updatedAttacker) &&
            TryGetEquippedLegacyXpBoosterLevel(attackerSession.PlayerItems, out _) &&
            ReadAttributeUInt32(updatedAttacker.Attributes, RuntimeLegacyXpBoosterAttribute) > 0)
        {
            var remaining = ReadAttributeUInt32(updatedAttacker.Attributes, RuntimeLegacyXpBoosterAttribute) - 1;
            updatedAttacker = AdjustLegacyPlayerXpDuration(updatedAttacker, seconds: 1);
            updatedAttacker = WithCharacterAttribute(
                updatedAttacker,
                RuntimeLegacyXpBoosterAttribute,
                remaining);
            xpBoosterApplied = true;
            xpBoosterDepleted = remaining == 0;
        }

        var xpKillerApplied = false;
        if (IsLegacyPlayerOnXpSkill(updatedTarget) &&
            TryGetEquippedLegacyXpKillerLevel(attackerSession.PlayerItems, out var xpKillerLevel) &&
            (xpKillerLevel != 6 || ReadAttributeUInt32(updatedTarget.Attributes, "XPCount") < 30))
        {
            updatedTarget = AdjustLegacyPlayerXpDuration(updatedTarget, -xpKillerLevel);
            updatedTarget = WithCharacterAttribute(
                updatedTarget,
                RuntimeLegacyXpKillerAttribute,
                ReadAttributeUInt32(updatedTarget.Attributes, RuntimeLegacyXpKillerAttribute) + 1);
            xpKillerApplied = true;
        }

        var tidalWaveApplied = false;
        var tidalWaveValue = (byte)0;
        if (!HasLegacyStatusFlag(updatedAttacker, LegacyTidalWaveFlag) &&
            !HasLegacyStatusFlag(updatedAttacker, LegacyHealingSnowFlag) &&
            TryGetEquippedLegacyTidalWaveLevel(attackerSession.PlayerItems, out var tidalWaveLevel) &&
            IsLegacyPlayerReceiveXpRuneCooldownReady(
                updatedAttacker,
                RuntimeLegacyTidalWaveStampUtcAttribute,
                now))
        {
            tidalWaveValue = ComputeLegacyPlayerReceiveXpRuneValue(tidalWaveLevel);
            updatedAttacker = WithLegacyTimedPlayerReceiveXpRuneStatus(
                updatedAttacker,
                LegacyTidalWaveFlag,
                RuntimeLegacyTidalWaveStampUtcAttribute,
                RuntimeLegacyTidalWaveExpiresAtUtcAttribute,
                now);
            tidalWaveApplied = true;
        }

        var quenchApplied = false;
        var quenchValue = (byte)0;
        if (!HasLegacyStatusFlag(updatedTarget, LegacyQuenchFlag) &&
            TryGetEquippedLegacyQuenchLevel(attackerSession.PlayerItems, out var quenchLevel) &&
            IsLegacyPlayerReceiveXpRuneCooldownReady(
                updatedAttacker,
                RuntimeLegacyQuenchStampUtcAttribute,
                now))
        {
            quenchValue = ComputeLegacyPlayerReceiveXpRuneValue(quenchLevel);
            updatedAttacker = WithCharacterAttribute(
                updatedAttacker,
                RuntimeLegacyQuenchStampUtcAttribute,
                now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            updatedTarget = WithLegacyTimedPlayerReceiveXpRuneStatus(
                updatedTarget,
                LegacyQuenchFlag,
                stampAttribute: null,
                RuntimeLegacyQuenchExpiresAtUtcAttribute,
                now);
            quenchApplied = true;
        }

        return new LegacyPlayerReceiveXpRunesResult(
            xpBoosterApplied,
            xpBoosterDepleted,
            xpKillerApplied,
            tidalWaveApplied,
            quenchApplied,
            tidalWaveValue,
            quenchValue);
    }

    private static bool TryGetEquippedLegacyXpKillerLevel(
        PlayerItemSnapshot playerItems,
        out byte runeLevel)
    {
        return TryGetEquippedLegacyRuneLevel(
            playerItems,
            LegacyXpKillerRuneItemBase,
            LegacyXpKillerRuneItemMax,
            out runeLevel);
    }

    private static bool TryGetEquippedLegacyXpBoosterLevel(
        PlayerItemSnapshot playerItems,
        out byte runeLevel)
    {
        return TryGetEquippedLegacyRuneLevel(
            playerItems,
            LegacyXpBoosterRuneItemBase,
            LegacyXpBoosterRuneItemMax,
            out runeLevel);
    }

    private static bool TryGetEquippedLegacyTidalWaveLevel(
        PlayerItemSnapshot playerItems,
        out byte runeLevel)
    {
        return TryGetEquippedLegacyRuneLevel(
            playerItems,
            LegacyTidalWaveRuneItemBase,
            LegacyTidalWaveRuneItemMax,
            out runeLevel);
    }

    private static bool TryGetEquippedLegacyQuenchLevel(
        PlayerItemSnapshot playerItems,
        out byte runeLevel)
    {
        return TryGetEquippedLegacyRuneLevel(
            playerItems,
            LegacyQuenchRuneItemBase,
            LegacyQuenchRuneItemMax,
            out runeLevel);
    }

    private static byte ComputeLegacyPlayerReceiveXpRuneValue(byte runeLevel)
    {
        return checked((byte)(15 + (runeLevel * 5)));
    }

    private static bool IsLegacyPlayerOnXpSkill(CharacterSnapshot character)
    {
        foreach (var xpSkillFlag in LegacyXpSkillFlags)
        {
            if (HasLegacyStatusFlag(character, xpSkillFlag))
            {
                return true;
            }
        }

        return false;
    }

    private static CharacterSnapshot AdjustLegacyPlayerXpDuration(
        CharacterSnapshot character,
        int seconds)
    {
        var xpCount = ReadAttributeUInt32(character.Attributes, "XPCount", "XpCount");
        var adjustedXpCount = Math.Clamp((long)xpCount + seconds, 0, 100);
        var updatedCharacter = WithCharacterAttribute(character, "XPCount", checked((ulong)adjustedXpCount));
        foreach (var expiresAtAttribute in LegacyXpSkillExpiresAtAttributes)
        {
            if (!TryReadLegacyDateTimeOffsetAttribute(
                    updatedCharacter.Attributes,
                    expiresAtAttribute,
                    out var expiresAt))
            {
                continue;
            }

            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                expiresAtAttribute,
                expiresAt.ToUniversalTime()
                    .AddSeconds(seconds)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        return updatedCharacter;
    }

    private static bool IsLegacyPlayerReceiveXpRuneCooldownReady(
        CharacterSnapshot attacker,
        string stampAttribute,
        DateTimeOffset now)
    {
        return !TryReadLegacyDateTimeOffsetAttribute(attacker.Attributes, stampAttribute, out var stamp) ||
            now.ToUniversalTime() >= stamp.ToUniversalTime().AddSeconds(LegacyPlayerReceiveXpRuneCooldownSeconds);
    }

    private static CharacterSnapshot WithLegacyTimedPlayerReceiveXpRuneStatus(
        CharacterSnapshot character,
        int statusFlag,
        string? stampAttribute,
        string expiresAtAttribute,
        DateTimeOffset now)
    {
        var updatedCharacter = WithLegacyStatusFlag(character, statusFlag);
        var formattedNow = now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        if (stampAttribute is not null)
        {
            updatedCharacter = WithCharacterAttribute(updatedCharacter, stampAttribute, formattedNow);
        }

        return WithCharacterAttribute(
            updatedCharacter,
            expiresAtAttribute,
            now.ToUniversalTime()
                .AddSeconds(LegacyPlayerReceiveXpRuneStatusSeconds)
                .ToString("O", CultureInfo.InvariantCulture));
    }

    private static LegacyMsgUpdatePacket CreateLegacyPlayerReceiveTidalWaveUpdate(
        CharacterSnapshot target,
        byte value)
    {
        return CreateLegacyPlayerReceiveXpRuneUpdate(
            target,
            LegacyMsgUpdateDataType.TidalWave,
            LegacyTidalWaveFlag,
            value);
    }

    private static LegacyMsgUpdatePacket CreateLegacyPlayerReceiveQuenchUpdate(
        CharacterSnapshot target,
        byte value)
    {
        return CreateLegacyPlayerReceiveXpRuneUpdate(
            target,
            LegacyMsgUpdateDataType.Quench,
            LegacyQuenchFlag,
            value);
    }

    private static LegacyMsgUpdatePacket CreateLegacyPlayerReceiveXpRuneUpdate(
        CharacterSnapshot target,
        LegacyMsgUpdateDataType dataType,
        int statusFlag,
        byte value)
    {
        return new LegacyMsgUpdatePacket(
            CheckedUInt32(target.Uid, nameof(target.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    dataType,
                    Value: 0,
                    Flag: checked((uint)statusFlag),
                    Time: value),
            ]);
    }

    private static LegacyMsgUpdatePacket CreateLegacyPlayerReceiveXpDurationUpdate(
        CharacterSnapshot character,
        ulong time,
        ulong spellLevel)
    {
        return new LegacyMsgUpdatePacket(
            CheckedUInt32(character.Uid, nameof(character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.XPDuration,
                    Value: 0,
                    Flag: LegacyFlashingNameStatusFlag,
                    Time: checked((uint)time),
                    Damage: 0,
                    SpellLevel: checked((uint)spellLevel)),
            ]);
    }
}
