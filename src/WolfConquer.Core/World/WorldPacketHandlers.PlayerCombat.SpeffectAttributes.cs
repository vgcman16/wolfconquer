using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacySpeffectRollAttribute = "LegacySpeffectRoll";
    private const string RuntimeLegacySpeffectLevelAttribute = "LegacySpeffectLevel";
    private const uint LegacySpeffectVenomGroup = 10;
    private const uint LegacySpeffectSolidGroup = 20;
    private const uint LegacySpeffectBloodthirstGroup = 30;
    private const uint LegacySpeffectElanGroup = 40;
    private const string LegacyReceiveAttackPlayerVenomSpeffectResolutionGap =
        "receiveattack-player-venom-speffect-spell-resolution";

    private static async ValueTask<(WorldSession AttackerSession, LegacyPlayerMagicDamageResult Result)>
        TrySendLegacyReceiveAttackPlayerSpeffectAttributesAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            LegacyPlayerMagicDamageResult result,
            CancellationToken cancellationToken)
    {
        var defenderSession = result.UpdatedSession;
        var defenderContext = context with
        {
            Connection = result.ActiveSession.Connection,
            Session = defenderSession,
        };

        (attackerSession, result) = await TryApplyLegacyReceiveAttackPlayerVenomSpeffectAsync(
                context,
                attackerSession,
                result,
                cancellationToken)
            .ConfigureAwait(false);
        defenderSession = result.UpdatedSession;
        defenderContext = defenderContext with { Session = defenderSession };

        defenderSession = await TryApplyLegacyReceiveAttackPlayerSolidSpeffectAsync(
                defenderContext,
                defenderSession,
                cancellationToken)
            .ConfigureAwait(false);
        defenderContext = defenderContext with { Session = defenderSession };

        defenderSession = await TryApplyLegacyReceiveAttackPlayerBloodthirstSpeffectAsync(
                defenderContext,
                defenderSession,
                cancellationToken)
            .ConfigureAwait(false);
        defenderContext = defenderContext with { Session = defenderSession };

        defenderSession = await TryApplyLegacyReceiveAttackPlayerElanSpeffectAsync(
                defenderContext,
                defenderSession,
                cancellationToken)
            .ConfigureAwait(false);

        var updatedResult = result with
        {
            ActiveSession = result.ActiveSession with { Session = defenderSession },
            UpdatedSession = defenderSession,
        };
        return (attackerSession, updatedResult);
    }

    private static async ValueTask<WorldSession> TryApplyLegacyReceiveAttackPlayerSolidSpeffectAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var level = GetLegacyReceiveAttackSpeffectLevel(
            session,
            LegacySpeffectSolidGroup,
            "SolidLevel",
            RuntimeLegacySpeffectLevelAttribute + ".Solid",
            RuntimeLegacySpeffectLevelAttribute + ".3");
        if (level == 0 ||
            !CheckLegacyReceiveAttackSpeffectRateRoll(session.Character, "Solid", ratePercent: 5))
        {
            return session;
        }

        var (solidValue, duration) = GetLegacyReceiveAttackSolidValues(level);
        if (solidValue == 0 || duration == 0)
        {
            return session;
        }

        var updatedSession = session with
        {
            Character = WithCharacterAttribute(
                WithLegacyStatusFlag(session.Character, LegacySolidFlag),
                "Solid",
                solidValue),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                updatedContext,
                updatedSession,
                LegacySolidFlag,
                duration,
                solidValue,
                spellLevel: 0,
                visible: false,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy ReceiveAttack.Player speffect Solid " +
                $"{FormatSession(updatedContext)} level={level} solid={solidValue} duration={duration}; " +
                "source=speffect_attribute.Get(attacked.Owner, 3).")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyReceiveAttackPlayerBloodthirstSpeffectAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var level = GetLegacyReceiveAttackSpeffectLevel(
            session,
            LegacySpeffectBloodthirstGroup,
            "BloodthirstLevel",
            RuntimeLegacySpeffectLevelAttribute + ".Bloodthirst",
            RuntimeLegacySpeffectLevelAttribute + ".4");
        var currentHitpoints = CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            session.Character,
            currentHitpoints,
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            context.WorldData,
            session.AlternateEquipmentActive);
        if (level == 0 ||
            currentHitpoints >= maxHitpoints ||
            !CheckLegacyReceiveAttackSpeffectRateRoll(
                session.Character,
                "Bloodthirst",
                GetLegacyReceiveAttackBloodthirstRate(level)))
        {
            return session;
        }

        var updatedSession = session with
        {
            Character = session.Character with { MinHitPoints = maxHitpoints },
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendScalarUpdateAsync(
                updatedContext,
                updatedSession.Character,
                LegacyMsgUpdateDataType.BloodthirstElan,
                4,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy ReceiveAttack.Player speffect Bloodthirst " +
                $"{FormatSession(updatedContext)} level={level} hitpoints={maxHitpoints}; " +
                "source=speffect_attribute.Get(attacked.Owner, 4).")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyReceiveAttackPlayerElanSpeffectAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var level = GetLegacyReceiveAttackSpeffectLevel(
            session,
            LegacySpeffectElanGroup,
            "ElanLevel",
            RuntimeLegacySpeffectLevelAttribute + ".Elan",
            RuntimeLegacySpeffectLevelAttribute + ".5");
        var currentMana = CheckedUInt32(session.Character.MinMana, nameof(session.Character.MinMana));
        var maxMana = CheckedUInt32(session.Character.MaxMana, nameof(session.Character.MaxMana));
        if (level == 0 ||
            currentMana >= maxMana ||
            !CheckLegacyReceiveAttackSpeffectRateRoll(session.Character, "Elan", ratePercent: 5))
        {
            return session;
        }

        var restoredMana = checked((uint)Math.Min(
            maxMana - currentMana,
            (uint)Math.Floor(maxMana * GetLegacyReceiveAttackElanPercent(level) / 100.0)));
        if (restoredMana == 0)
        {
            return session;
        }

        var newMana = checked(currentMana + restoredMana);
        var updatedSession = session with
        {
            Character = session.Character with { MinMana = newMana },
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendScalarUpdateAsync(
                updatedContext,
                updatedSession.Character,
                LegacyMsgUpdateDataType.BloodthirstElan,
                17,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy ReceiveAttack.Player speffect Elan " +
                $"{FormatSession(updatedContext)} level={level} mana={newMana} restored={restoredMana}; " +
                "source=speffect_attribute.Get(attacked.Owner, 5).")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static uint GetLegacyReceiveAttackSpeffectLevel(
        WorldSession session,
        uint mythSoulGroup,
        params string[] attributeKeys)
    {
        if (TryReadUInt32Attribute(session.Character.Attributes, out var forcedLevel, attributeKeys))
        {
            return forcedLevel;
        }

        var level = 0U;
        foreach (var item in session.PlayerItems.EquipmentItems)
        {
            if (item.MythSoul == 0)
            {
                continue;
            }

            var current = item.MythSoul % 10;
            if ((item.MythSoul % 1000) - current == mythSoulGroup)
            {
                level = checked(level + current);
            }
        }

        return level;
    }

    private static bool CheckLegacyReceiveAttackSpeffectRateRoll(
        CharacterSnapshot character,
        string effectName,
        double ratePercent)
    {
        if (ratePercent <= 0)
        {
            return false;
        }

        const uint denominator = 10_000;
        var threshold = checked((uint)Math.Min(denominator, Math.Floor(ratePercent * 100.0)));
        if (threshold >= denominator)
        {
            return true;
        }

        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacySpeffectRollAttribute + "." + effectName,
            RuntimeLegacySpeffectRollAttribute + effectName,
            RuntimeLegacySpeffectRollAttribute)
            ? forcedRoll % denominator
            : checked((uint)Random.Shared.Next(0, (int)denominator));
        return roll < threshold;
    }

    private static (double RatePercent, uint Damage) GetLegacyReceiveAttackVenomValues(uint sourceLevel)
    {
        return sourceLevel switch
        {
            <= 0 => (0, 0),
            <= 17 => ((25 + (sourceLevel * 5)) / 10.0, checked(sourceLevel * 500U)),
            <= 49 => ((111 + (sourceLevel - 18)) / 10.0, checked(9000U + ((sourceLevel - 18) * 100U))),
            _ => (0, 0),
        };
    }

    private static (uint Value, uint Duration) GetLegacyReceiveAttackSolidValues(uint level)
    {
        return level switch
        {
            <= 0 => (0, 0),
            <= 3 => (checked(2000U + ((level - 1) * 100U)), 5),
            <= 10 => (checked(2400U + ((level - 4) * 50U)), 5),
            <= 20 => (checked(2400U + ((level - 4) * 50U)), 6),
            <= 30 => (checked(2400U + ((level - 4) * 50U)), 7),
            <= 49 => (checked(2400U + ((level - 4) * 50U)), 8),
            _ => (0, 0),
        };
    }

    private static double GetLegacyReceiveAttackBloodthirstRate(uint level)
    {
        return level switch
        {
            <= 0 => 0,
            <= 19 => 0.5 + (level * 0.5),
            <= 49 => 10.0 + ((level - 18) * 0.1),
            _ => 0,
        };
    }

    private static double GetLegacyReceiveAttackElanPercent(uint level)
    {
        return level is >= 1 and <= 49
            ? 44.0 + level
            : 0;
    }

    private static async ValueTask<(WorldSession AttackerSession, LegacyPlayerMagicDamageResult Result)>
        TryApplyLegacyReceiveAttackPlayerVenomSpeffectAsync(
        WorldPacketHandlerContext context,
        WorldSession attackerSession,
        LegacyPlayerMagicDamageResult result,
        CancellationToken cancellationToken)
    {
        var level = GetLegacyReceiveAttackSpeffectLevel(
            attackerSession,
            LegacySpeffectVenomGroup,
            "VenomLevel",
            RuntimeLegacySpeffectLevelAttribute + ".Venom",
            RuntimeLegacySpeffectLevelAttribute + ".2");
        if (level == 0)
        {
            return (attackerSession, result);
        }

        var sourceLevel = level == uint.MaxValue ? uint.MaxValue : level + 1;
        var (ratePercent, sourceDamage) = GetLegacyReceiveAttackVenomValues(sourceLevel);
        if (ratePercent <= 0 ||
            !CheckLegacyReceiveAttackSpeffectRateRoll(attackerSession.Character, "Venom", ratePercent))
        {
            return (attackerSession, result);
        }

        if (context.WorldData is not { } worldData)
        {
            await LogLegacyReceiveAttackPlayerVenomSpeffectResolutionGapAsync(
                    context,
                    attackerSession,
                    result.UpdatedSession,
                    level,
                    sourceLevel,
                    ratePercent,
                    sourceDamage,
                    "WorldDataCatalog is not loaded; source speffect_attribute.Get(client, 2, attacked.UID) " +
                    "routes spell 20000 through MsgAttackPacket.ProcescMagic(ignoreStamp: true).",
                    resource: WorldDataResource)
                .ConfigureAwait(false);
            return (attackerSession, result);
        }

        if (!TryResolveLegacyPlayerMagicRuntimeSpell(
                attackerSession,
                worldData,
                LegacyAttackStatusVenomSpellId,
                out var playerSpell,
                out var spell))
        {
            await LogLegacyReceiveAttackPlayerVenomSpeffectResolutionGapAsync(
                    context,
                    attackerSession,
                    result.UpdatedSession,
                    level,
                    sourceLevel,
                    ratePercent,
                    sourceDamage,
                    "learned/combat-gear spell 20000 was not available or its magic row was missing; " +
                    "source speffect_attribute.Get(client, 2, attacked.UID) builds InteractQuery SpellID=20000 " +
                    "and calls MsgAttackPacket.ProcescMagic(ignoreStamp: true).",
                    resource: "PlayerSpells+CombatGear+MagicType")
                .ConfigureAwait(false);
            return (attackerSession, result);
        }

        if (spell.Sort != LegacyMagicSort.AttackStatus)
        {
            await LogLegacyReceiveAttackPlayerVenomSpeffectResolutionGapAsync(
                    context,
                    attackerSession,
                    result.UpdatedSession,
                    level,
                    sourceLevel,
                    ratePercent,
                    sourceDamage,
                    $"spell 20000 resolved as sort={(int)spell.Sort}, not AttackStatus; " +
                    "source speffect_attribute.Get(client, 2, attacked.UID) routes spell 20000 to AttackStatus case 20000.",
                    resource: "MagicType",
                    sort: (int)spell.Sort)
                .ConfigureAwait(false);
            return (attackerSession, result);
        }

        var activeTarget = result.ActiveSession with { Session = result.UpdatedSession };
        var updatedTargetSession = await ApplyLegacyAttackStatusVenomAsync(
                context,
                attackerSession,
                activeTarget,
                playerSpell,
                spell,
                "handled legacy ReceiveAttack.Player speffect Venom attacker branch",
                "speffectLevel=" + level.ToString(CultureInfo.InvariantCulture) +
                " sourceLevel=" + sourceLevel.ToString(CultureInfo.InvariantCulture) +
                " sourceRatePercent=" + ratePercent.ToString("0.###", CultureInfo.InvariantCulture) +
                " sourceDamage=" + sourceDamage.ToString(CultureInfo.InvariantCulture) + "; ",
                "source=speffect_attribute.Get(client, 2, attacked.UID) routes spell 20000 through " +
                "MsgAttackPacket.ProcescMagic(ignoreStamp: true), preserving passive no-attack-stamp/no-cooldown semantics.",
                cancellationToken)
            .ConfigureAwait(false);

        return (
            attackerSession,
            result with
            {
                ActiveSession = activeTarget with { Session = updatedTargetSession },
                UpdatedSession = updatedTargetSession,
            });
    }

    private static async ValueTask LogLegacyReceiveAttackPlayerVenomSpeffectResolutionGapAsync(
        WorldPacketHandlerContext context,
        WorldSession attackerSession,
        WorldSession defenderSession,
        uint level,
        uint sourceLevel,
        double ratePercent,
        uint sourceDamage,
        string reason,
        string resource,
        int? sort = null)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.Create(
                    LegacyParityGapCategory.Magic,
                    LegacyReceiveAttackPlayerVenomSpeffectResolutionGap,
                    reason,
                    severity: LegacyParityGapSeverity.Warning,
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("attackerUid", attackerSession.Character.Uid),
                        LegacyParityGapDiagnostics.Fact("defenderUid", defenderSession.Character.Uid),
                        LegacyParityGapDiagnostics.Fact("level", level),
                        LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                        LegacyParityGapDiagnostics.Fact("resource", resource),
                        LegacyParityGapDiagnostics.Fact("sort", sort),
                        LegacyParityGapDiagnostics.Fact("source", "speffect_attribute.Get(client,2,attacked.UID)"),
                        LegacyParityGapDiagnostics.Fact("sourceDamage", sourceDamage),
                        LegacyParityGapDiagnostics.Fact("sourceLevel", sourceLevel),
                        LegacyParityGapDiagnostics.Fact("sourceRatePercent", ratePercent),
                        LegacyParityGapDiagnostics.Fact("spellId", LegacyAttackStatusVenomSpellId),
                        LegacyParityGapDiagnostics.Fact("targetKind", "player"),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("resource", resource),
                        LegacyParityGapDiagnostics.Fact("sort", sort),
                        LegacyParityGapDiagnostics.Fact("source", "speffect_attribute.Get(client,2,attacked.UID)"),
                        LegacyParityGapDiagnostics.Fact("spellId", LegacyAttackStatusVenomSpellId),
                    ]))
            .ConfigureAwait(false);
    }
}
