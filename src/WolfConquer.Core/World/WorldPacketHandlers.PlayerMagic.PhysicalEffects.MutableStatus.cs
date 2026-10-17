using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyPhysicalFrontBreakStatusFlag = 313;
    private const string RuntimeLegacyPhysicalImpregnabilityRollAttribute = "LegacyPhysicalImpregnabilityRoll";

    private static async ValueTask<(
        WorldSession Session,
        WorldActiveSession Target,
        bool? EnteredCriticalBlock)>
        ApplyLegacyPlayerPlayerPhysicalMutableStatusAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            WorldActiveSession target,
            WorldDataCatalog worldData,
            long attackerUid,
            long targetUid,
            CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref session, ref target))
        {
            return (session, target, null);
        }

        // Physical400 enters this block once, BEFORE AddFlag313 can publish.
        // Its later refinery/Dash reads must use current state without testing
        // this outer gate a second time after an awaited send.
        var attackerFields = CalculateLegacyRuntimeStatusFields(session, worldData);
        if (attackerFields[LegacyPlayerStatusPacket.CriticalStrikeIndex] == 0)
        {
            return (session, target, false);
        }

        if (!HasLegacyStatusFlag(session.Character, LegacyPhysicalFrontBreakStatusFlag) &&
            TryGetLegacyEligibleEquippedRuneLevel(session, worldData, "Front Break", out var frontBreakLevel))
        {
            var percent = frontBreakLevel == 9 ? 10U : frontBreakLevel;
            var overlay = LegacyPhysicalStatusOverlay.ForOwner(session) with
            {
                Breakthrough = AddLegacyPhysicalMutableStatusPercent(
                    attackerFields[LegacyPlayerStatusPacket.BreakthroughIndex], percent),
                FrontBreakExpiresAtUtc = (context.TimeProvider ?? TimeProvider.System).GetUtcNow().AddSeconds(10),
            };
            session = session with
            {
                Character = WithLegacyStatusFlag(session.Character, LegacyPhysicalFrontBreakStatusFlag),
                PhysicalStatusOverlay = overlay,
            };
            context.Sessions?.Set(context.Connection, session);
            context = context with { Session = session };

            var publication = await SendLegacyPhysicalFrontBreakStatusAsync(
                    context, session, target, attackerUid, targetUid, cancellationToken)
                .ConfigureAwait(false);
            session = publication.Session;
            target = publication.Target;
            if (!publication.IdentitiesRetained ||
                !TryRefreshLegacyDirectPhysicalParticipants(
                    context, attackerUid, targetUid, ref session, ref target))
            {
                return (session, target, null);
            }

            context = context with { Session = session };
        }

        // The source Rate(value2) call occurs independently inside the entered
        // block, including value2=0 when no Impregnability rune is eligible.
        TryGetLegacyEligibleEquippedRuneLevel(target.Session, worldData, "Impregnability", out var impregLevel);
        var chance = impregLevel switch
        {
            1 => 10U,
            2 => 11U,
            3 => 12U,
            4 => 13U,
            5 => 14U,
            6 => 15U,
            7 => 16U,
            8 => 18U,
            9 => 20U,
            _ => 0U,
        };
        var roll = ReadLegacyPhysicalEffectRoll(target.Session.Character, RuntimeLegacyPhysicalImpregnabilityRollAttribute);
        if (CheckLegacyPlayerPlayerPhysicalEffectRate(chance, roll))
        {
            var percent = impregLevel switch
            {
                1 => 30U,
                2 => 32U,
                3 => 34U,
                4 => 36U,
                5 => 38U,
                6 => 40U,
                7 => 42U,
                8 => 45U,
                9 => 50U,
                _ => 0U,
            };
            var targetFields = CalculateLegacyRuntimeStatusFields(target.Session, worldData);
            var overlay = LegacyPhysicalStatusOverlay.ForOwner(target.Session) with
            {
                Counteraction = AddLegacyPhysicalMutableStatusPercent(
                    targetFields[LegacyPlayerStatusPacket.CounteractionIndex], percent),
            };
            var updatedTarget = target.Session with { PhysicalStatusOverlay = overlay };
            context.Sessions?.Set(target.Connection, updatedTarget);
            target = target with { Session = updatedTarget };
        }

        return (session, target, true);
    }

    private static uint AddLegacyPhysicalMutableStatusPercent(uint current, uint percent)
    {
        // Match the source uint multiply/add widths, including unchecked wrap.
        return unchecked(current + current * percent / 100U);
    }
}
