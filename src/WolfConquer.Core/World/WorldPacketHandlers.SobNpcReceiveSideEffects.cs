using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(WorldSession Session, bool Applied, LegacyPlayerMagicReceiveAbortReason AbortReason)> TrySendLegacySobNpcReceiveCombatGearUpdateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpc sobNpc,
        CancellationToken cancellationToken)
    {
        var targetX = CheckedUInt16(sobNpc.X, nameof(sobNpc.X));
        var targetY = CheckedUInt16(sobNpc.Y, nameof(sobNpc.Y));
        var updatedSession = session;
        var applied = false;

        if (HasLegacyCombatGearEquippedForClass(
                updatedSession.CombatGear ?? CombatGearSnapshot.Empty,
                updatedSession.Character.Class,
                LegacyCombatGearStoneCrackerItemId))
        {
            (updatedSession, var stoneCrackerApplied, var abortReason) =
                await TrySendLegacyCombatGearStoneCrackerSobCounterUpdateAsync(
                        context with { Session = updatedSession },
                        updatedSession,
                        targetX,
                        targetY,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
            {
                return (updatedSession, applied, abortReason);
            }

            applied |= stoneCrackerApplied;
        }

        (updatedSession, var dragonhowlApplied) =
            await TrySendLegacyCombatGearDragonhowlReceivePlayerUpdateAsync(
                    context with { Session = updatedSession },
                    updatedSession,
                    targetX,
                    targetY,
                    cancellationToken)
                .ConfigureAwait(false);
        applied |= dragonhowlApplied;

        (updatedSession, var bloodlustApplied) =
            await TrySendLegacyCombatGearBloodlustReceivePlayerUpdateAsync(
                    context with { Session = updatedSession },
                    updatedSession,
                    targetX,
                    targetY,
                    cancellationToken)
                .ConfigureAwait(false);
        applied |= bloodlustApplied;

        if (context.WorldData is { } worldData)
        {
            if (HasLegacyCombatGearEquippedForClass(
                    updatedSession.CombatGear ?? CombatGearSnapshot.Empty,
                    updatedSession.Character.Class,
                    LegacyCombatGearTaoist1ItemId))
            {
                (updatedSession, var deadwoodCurseApplied) =
                    await TrySendLegacyCombatGearDeadwoodCurseReceiveUpdateAsync(
                            context with { Session = updatedSession },
                            updatedSession,
                            worldData,
                            CheckedUInt32(sobNpc.Id, nameof(sobNpc.Id)),
                            cancellationToken)
                        .ConfigureAwait(false);
                applied |= deadwoodCurseApplied;
            }

            (updatedSession, var pirateArmedApplied) =
                await TrySendLegacyCombatGearPirateArmedReceivePlayerUpdateAsync(
                        context with { Session = updatedSession },
                        updatedSession,
                        worldData,
                        CheckedUInt32(sobNpc.Id, nameof(sobNpc.Id)),
                        targetX,
                        targetY,
                        cancellationToken)
                    .ConfigureAwait(false);
            applied |= pirateArmedApplied;

            (updatedSession, var pirateSenseApplied) =
                await TrySendLegacyCombatGearPirateSenseReceivePlayerUpdateAsync(
                        context with { Session = updatedSession },
                        updatedSession,
                        worldData,
                        cancellationToken)
                    .ConfigureAwait(false);
            applied |= pirateSenseApplied;

            (updatedSession, var pirateDarkApplied) =
                await TrySendLegacyCombatGearPirateDarkReceiveSobNpcUpdateAsync(
                        context with { Session = updatedSession },
                        updatedSession,
                        worldData,
                        CheckedUInt32(sobNpc.Id, nameof(sobNpc.Id)),
                        targetX,
                        targetY,
                        cancellationToken)
                    .ConfigureAwait(false);
            applied |= pirateDarkApplied;

            (updatedSession, var pirateBlackJadeUseRuneApplied) =
                await TrySendLegacyCombatGearPirateBlackJadeUseRuneAsync(
                        context with { Session = updatedSession },
                        updatedSession,
                        worldData,
                        CheckedUInt32(sobNpc.Id, nameof(sobNpc.Id)),
                        targetX,
                        targetY,
                        cancellationToken)
                    .ConfigureAwait(false);
            applied |= pirateBlackJadeUseRuneApplied;
        }

        return (updatedSession, applied, LegacyPlayerMagicReceiveAbortReason.None);
    }

    private static WorldSession WithLegacySobNpcReceiveHitAttributes(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpc sobNpc)
    {
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(
                WithCharacterAttribute(
                    session.Character,
                    RuntimeLegacyThundercloudSightAttribute,
                    sobNpc.Id.ToString(CultureInfo.InvariantCulture)),
                RuntimeLegacyAttackHitAttribute,
                "true"),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        return updatedSession;
    }

    private static uint ApplyLegacySobNpcReceiveDamageModifiers(WorldSession session, uint damage)
    {
        if (damage == 0)
        {
            return 0;
        }

        var adjustedDamage = damage;
        if (HasLegacyStatusFlag(session.Character, LegacyHookMoonCasterFlag))
        {
            var hookMoonPower = ReadAttributeUInt32(session.Character.Attributes, "HookMoonPower");
            adjustedDamage = MulDivToUInt32(adjustedDamage, hookMoonPower, 100);
        }

        if (HasLegacyStatusFlag(session.Character, LegacyCleanSweepFlag))
        {
            var cleanSweepPower = ReadAttributeUInt32(
                session.Character.Attributes,
                "CleanSweepPower",
                "LegacyCleanSweepPower");
            adjustedDamage = AddLegacySaturating(adjustedDamage, MulDivToUInt32(adjustedDamage, cleanSweepPower, 100));
        }

        if (HasLegacyStatusFlag(session.Character, LegacyAxeShadowFlag))
        {
            var axeShadowPower = ReadAttributeUInt32(
                session.Character.Attributes,
                "AxeShadowPower",
                "LegacyAxeShadowPower");
            adjustedDamage = AddLegacySaturating(adjustedDamage, MulDivToUInt32(adjustedDamage, axeShadowPower, 100));
        }

        if (HasLegacyStatusFlag(session.Character, LegacyCelestialDanceFlag))
        {
            var celestialDancePower = ReadAttributeUInt32(
                session.Character.Attributes,
                "CelestialDancePower",
                "LegacyCelestialDancePower");
            adjustedDamage = MulDivToUInt32(adjustedDamage, celestialDancePower, 100);
        }

        return adjustedDamage;
    }
}
