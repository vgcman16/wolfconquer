using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyFineRain1ExpiresAtUtcAttribute = "LegacyFineRain1ExpiresAtUtc";
    internal const string RuntimeLegacyFineRain2ExpiresAtUtcAttribute = "LegacyFineRain2ExpiresAtUtc";

    private const ushort LegacyFineRainAttachStatusSpellId = 14_500;
    private const string RuntimeLegacyFineRain1RemoveOnDeadAttribute = "LegacyFineRain1RemoveOnDead";
    private const string RuntimeLegacyFineRain2RemoveOnDeadAttribute = "LegacyFineRain2RemoveOnDead";
    private const string RuntimeLegacyFineRainPowerAttribute = "FineRainPower";
    private const string RuntimeLegacyFineRainDefaultPowerAttribute = "defFineRainPower";

    private static async ValueTask<bool> HandleLegacyPlayerFineRainAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyFineRainAttachStatusSpellId)
        {
            return false;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var resourceContext = context with { Session = resourceSession };
        var interaction = new MsgAttackPacket(query with { Uid = casterUid });
        await resourceContext.Connection.SendAsync(interaction.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        WorldSession currentCaster = resourceSession;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, casterUid, ref currentCaster))
        {
            return true;
        }

        var updatedCasterSession = ApplyLegacyFineRainPower(currentCaster, spell, context.WorldData);
        context.Sessions?.Set(context.Connection, updatedCasterSession);
        var updatedCasterContext = context with { Session = updatedCasterSession };

        var casterAcquisition = await ExecuteLegacyFineRainAcquisitionAsync(
                updatedCasterContext, updatedCasterSession, spell,
                new LegacyEquipmentQueryPublicationScope(casterUid), cancellationToken)
            .ConfigureAwait(false);
        if (!casterAcquisition.Completed) return true;
        updatedCasterSession = casterAcquisition.Session!;
        updatedCasterContext = context with { Session = updatedCasterSession };

        var animationTargets = new List<LegacySpellAnimationTarget>
        {
            new(casterUid, 0, Hit: true, MsgAttackEffect.None),
        };
        var teamMembersApplied = 0;
        var teamMembersOutOfRange = 0;
        var stoppedOnDeadMember = false;

        foreach (var teammate in LegacyTeamSupportTargetResolver.ResolveActiveTeammates(context, updatedCasterSession))
        {
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, casterUid, ref updatedCasterSession))
            {
                return true;
            }

            var memberSession = teammate.Session;
            var memberUid = memberSession.Character.Uid;
            var memberContext = context with { Connection = teammate.Connection, Session = memberSession };
            if (!TryRefreshLegacyPlayerMagicResourceSession(memberContext, memberUid, ref memberSession))
            {
                continue;
            }

            if (LegacyGetDistance(
                    updatedCasterSession.Character.X,
                    updatedCasterSession.Character.Y,
                    memberSession.Character.X,
                    memberSession.Character.Y) >= 18)
            {
                teamMembersOutOfRange++;
                continue;
            }

            if (HasLegacyStatusFlag(memberSession.Character, LegacyDeadFlag))
            {
                stoppedOnDeadMember = true;
                break;
            }

            var updatedMemberSession = ApplyLegacyFineRainPower(memberSession, spell, context.WorldData);
            context.Sessions?.Set(teammate.Connection, updatedMemberSession);
            memberContext = context with
            {
                Connection = teammate.Connection,
                Session = updatedMemberSession,
            };

            var memberScope = new LegacyEquipmentQueryPublicationScope(
                memberUid,
                RequiredParticipant: new WorldActiveSession(context.Connection, updatedCasterSession));
            var memberAcquisition = await ExecuteLegacyFineRainAcquisitionAsync(
                    memberContext, updatedMemberSession, spell, memberScope, cancellationToken)
                .ConfigureAwait(false);
            if (memberAcquisition.RequiredParticipantSession is { } currentCasterAfterAcquisition)
                updatedCasterSession = currentCasterAfterAcquisition;
            if ((memberAcquisition.Loss & LegacyEquipmentQueryPublicationLoss.RequiredParticipantLost) != 0)
                return true;
            if (!memberAcquisition.Completed) continue;
            updatedMemberSession = memberAcquisition.Session!;
            memberContext = memberContext with { Session = updatedMemberSession };
            var memberQuery = await ExecuteLegacyEquipmentQueryAsync(
                    memberContext, updatedMemberSession,
                    new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.FineRainChange, memberScope),
                    cancellationToken)
                .ConfigureAwait(false);
            if (memberQuery.RequiredParticipantSession is { } currentCasterAfterQuery)
            {
                updatedCasterSession = currentCasterAfterQuery;
            }

            if ((memberQuery.Loss & LegacyEquipmentQueryPublicationLoss.RequiredParticipantLost) != 0)
            {
                return true;
            }

            if (!memberQuery.Completed)
            {
                continue;
            }

            // A rejected query still fills its CURRENT cached maximum. This is a
            // separate source assignment, not a duplicate query HP publication.
            updatedMemberSession = memberQuery.Session!;
            memberContext = memberContext with { Session = updatedMemberSession };
            var memberFill = await AssignLegacyPlayerHitpointsAsync(
                    memberContext, updatedMemberSession, memberScope,
                    unchecked((int)GetLegacyEquipmentRuntimeMaxHitpoints(updatedMemberSession, context.WorldData)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (memberFill.RequiredParticipantSession is { } currentCasterAfterFill)
            {
                updatedCasterSession = currentCasterAfterFill;
            }

            if ((memberFill.Loss & LegacyEquipmentQueryPublicationLoss.RequiredParticipantLost) != 0)
            {
                return true;
            }

            if (!memberFill.Completed)
            {
                continue;
            }

            updatedMemberSession = memberFill.Session!;
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(updatedMemberSession.Character.Uid, nameof(updatedMemberSession.Character.Uid)),
                0,
                Hit: true,
                MsgAttackEffect.None));
            teamMembersApplied++;
        }

        var casterScope = new LegacyEquipmentQueryPublicationScope(casterUid);
        updatedCasterContext = context with { Session = updatedCasterSession };
        var casterQuery = await ExecuteLegacyEquipmentQueryAsync(
                updatedCasterContext, updatedCasterSession,
                new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.FineRainChange, casterScope),
                cancellationToken)
            .ConfigureAwait(false);
        if (!casterQuery.Completed)
        {
            return true;
        }

        updatedCasterSession = casterQuery.Session!;
        updatedCasterContext = context with { Session = updatedCasterSession };
        var casterFill = await AssignLegacyPlayerHitpointsAsync(
                updatedCasterContext, updatedCasterSession, casterScope,
                unchecked((int)GetLegacyEquipmentRuntimeMaxHitpoints(updatedCasterSession, context.WorldData)),
                cancellationToken)
            .ConfigureAwait(false);
        if (!casterFill.Completed)
        {
            return true;
        }

        updatedCasterSession = casterFill.Session!;
        updatedCasterContext = context with { Session = updatedCasterSession };

        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(updatedCasterContext, updatedCasterSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy FineRain AttachStatus magic " +
                $"{FormatSession(updatedCasterContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"statusFlags={LegacyFineRain1Flag}/{LegacyFineRain2Flag} duration={spell.Duration} " +
                $"fineRainPower={spell.Damage} teamMembersApplied={teamMembersApplied} " +
                $"teamMembersOutOfRange={teamMembersOutOfRange} stoppedOnDeadMember={stoppedOnDeadMember}; " +
                "source=AttachStatus.cs case 14500.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyFineRainPower(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog? worldData)
    {
        // Source powers precede countdowns; old Status.Max survives a rejected query.
        var previousMaxHitpoints = GetLegacyEquipmentRuntimeMaxHitpoints(session, worldData);
        var character = WithCharacterAttribute(session.Character, RuntimeLegacyFineRainPowerAttribute, spell.Damage);
        character = WithCharacterAttribute(character, RuntimeLegacyFineRainDefaultPowerAttribute, spell.Damage);
        return session with
        {
            Character = character,
            EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(session.Character.Uid, previousMaxHitpoints),
        };
    }

}
