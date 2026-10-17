using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyToxicFogPlayerPoisonAvoidance
{
    None,
    ToxinEraser,
    Sturdiness,
}

internal readonly record struct LegacyToxicFogPlayerPoisonResult(
    WorldSession Session,
    bool Applied,
    LegacyToxicFogPlayerPoisonAvoidance Avoidance,
    byte PoisonLevel,
    DateTimeOffset? ExpiresAtUtc);

public static partial class WorldPacketHandlers
{
    internal const int LegacyToxicFogPoisonedFlag = 0x01;
    internal const string RuntimeLegacyToxicFogPoisonLevelAttribute = "PoisonLevel";
    internal const string RuntimeLegacyToxicFogPoisonExpiresAtUtcAttribute = "LegacyPoisonedExpiresAtUtc";
    internal const string RuntimeLegacyToxicFogSturdinessRollAttribute = "LegacyToxicFogSturdinessRoll";

    internal static LegacyToxicFogPlayerPoisonResult TryApplyLegacyToxicFogPlayerPoison(
        WorldSession targetSession,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog? worldData,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(targetSession);
        ArgumentNullException.ThrowIfNull(playerSpell);
        ArgumentNullException.ThrowIfNull(spell);

        if (HasLegacyToxicFogToxinEraserAvoidance(targetSession.Character))
        {
            return new LegacyToxicFogPlayerPoisonResult(
                targetSession,
                Applied: false,
                LegacyToxicFogPlayerPoisonAvoidance.ToxinEraser,
                PoisonLevel: 0,
                ExpiresAtUtc: null);
        }

        if (HasLegacyToxicFogSturdinessAvoidance(targetSession, worldData))
        {
            return new LegacyToxicFogPlayerPoisonResult(
                targetSession,
                Applied: false,
                LegacyToxicFogPlayerPoisonAvoidance.Sturdiness,
                PoisonLevel: 0,
                ExpiresAtUtc: null);
        }

        var poisonLevel = unchecked((byte)(playerSpell.Level + playerSpell.UseSpellSoul));
        var expiresAt = nowUtc.ToUniversalTime().AddSeconds(spell.Duration);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithLegacyStatusFlag(targetSession.Character, LegacyToxicFogPoisonedFlag),
                RuntimeLegacyToxicFogPoisonLevelAttribute,
                poisonLevel.ToString(CultureInfo.InvariantCulture)),
            RuntimeLegacyToxicFogPoisonExpiresAtUtcAttribute,
            expiresAt.ToString("O", CultureInfo.InvariantCulture));

        return new LegacyToxicFogPlayerPoisonResult(
            targetSession with { Character = updatedCharacter },
            Applied: true,
            LegacyToxicFogPlayerPoisonAvoidance.None,
            poisonLevel,
            expiresAt);
    }

    private static bool HasLegacyToxicFogToxinEraserAvoidance(CharacterSnapshot character)
    {
        return TryReadLegacyToxicFogBooleanAttribute(
                character.Attributes,
                out var avoids,
                "LegacyToxicFogToxinEraser",
                "ToxicFogToxinEraser",
                "ToxinEraserAvoidsPoison",
                "LegacyToxinEraserAvoidsPoison",
                "PerfectionStatus.ToxinEraser",
                "LegacyPerfectionStatus.ToxinEraser",
                "ToxinEraser",
                "PerfectionToxinEraser",
                "LegacyToxinEraser") &&
            avoids;
    }

    private static bool HasLegacyToxicFogSturdinessAvoidance(
        WorldSession targetSession,
        WorldDataCatalog? worldData)
    {
        if (!TryGetLegacyEquippedRuneLevel(targetSession.PlayerItems, worldData, "Sturdiness", out var itemLevel))
        {
            return false;
        }

        var rate = unchecked((byte)(5 + itemLevel * 5));
        if (rate >= 100)
        {
            return true;
        }

        var roll = TryReadUInt32Attribute(
                targetSession.Character.Attributes,
                out var forcedRoll,
                RuntimeLegacyToxicFogSturdinessRollAttribute,
                "ToxicFogSturdinessRoll",
                "LegacySturdinessRoll",
                "SturdinessRoll")
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < rate;
    }

    private static bool TryReadLegacyToxicFogBooleanAttribute(
        IReadOnlyDictionary<string, string> attributes,
        out bool parsed,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!attributes.TryGetValue(key, out var raw))
            {
                continue;
            }

            if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                parsed = true;
                return true;
            }

            if (raw.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                parsed = false;
                return true;
            }

            if (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
                value <= 1)
            {
                parsed = value != 0;
                return true;
            }
        }

        parsed = false;
        return false;
    }
}
