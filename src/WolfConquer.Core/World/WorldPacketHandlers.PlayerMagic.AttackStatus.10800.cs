using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyAttackStatus10800SpellId = 10_800;
    private const uint LegacyAttackStatus10800SpellExperience = 10_000;
    private const int LegacyAttackStatus10800TeamDistance = 12;
    private const int LegacyAttackStatus10800ShieldSeconds = 30;
    private const int LegacyAttackStatus10800ShieldOfTruthFlag = 0x138;
    private const string RuntimeLegacyShieldofTruthExpiresAtUtcAttribute = "LegacyShieldofTruthExpiresAtUtc";
    private const string RuntimeLegacyShieldofTruthRemoveOnDeadAttribute = "LegacyShieldofTruthRemoveOnDead";

    private static async ValueTask<bool> HandleLegacyPlayerAttackStatus10800MagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttackStatus ||
            spell.Id != LegacyAttackStatus10800SpellId)
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

        var resourceContext = context with { Session = resourceSession };
        var teamResult = await ApplyLegacyAttackStatus10800TeamBuffAsync(
                resourceContext,
                resourceSession,
                cancellationToken)
            .ConfigureAwait(false);
        resourceSession = teamResult.CasterSession;
        resourceContext = context with { Session = resourceSession };

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
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
            [
                new LegacySpellAnimationTarget(
                    casterUid,
                    Damage: 0,
                    Hit: true,
                    Effect: MsgAttackEffect.None,
                    NewY: CheckedUInt16(resourceSession.Character.Y, nameof(resourceSession.Character.Y))),
            ]);
        await SendPlayerSpellAnimationToVisibleAsync(resourceContext, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                resourceContext,
                resourceSession,
                playerSpell,
                spell,
                LegacyAttackStatus10800SpellExperience,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy AttackStatus 10800 magic " +
                $"{FormatSession(resourceContext with { Session = updatedSession })} spellId={spell.Id} " +
                $"sort={(int)spell.Sort} spellLevel={playerSpell.Level} targets=1 " +
                $"teamMembersApplied={teamResult.AppliedMembers} teamMembersOutOfRange={teamResult.OutOfRangeMembers} " +
                $"equipmentRefreshPackets={teamResult.EquipmentRefreshPackets} " +
                $"statusFlagRefreshPackets={teamResult.StatusFlagRefreshPackets} " +
                $"totalDamage={LegacyAttackStatus10800SpellExperience}; " +
                "source=AttackStatus.cs case 10800.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<LegacyAttackStatus10800TeamBuffResult> ApplyLegacyAttackStatus10800TeamBuffAsync(
        WorldPacketHandlerContext context,
        WorldSession casterSession,
        CancellationToken cancellationToken)
    {
        if (context.TeamStates is null ||
            context.Sessions is not { } sessions ||
            !context.TeamStates.HasTeam(context.Connection))
        {
            return new LegacyAttackStatus10800TeamBuffResult(
                casterSession,
                AppliedMembers: 0,
                OutOfRangeMembers: 0,
                EquipmentRefreshPackets: 0,
                StatusFlagRefreshPackets: 0);
        }

        var appliedMembers = 0;
        var outOfRangeMembers = 0;
        var equipmentRefreshPackets = 0;
        var statusFlagRefreshPackets = 0;
        var currentCasterSession = casterSession;
        var expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(LegacyAttackStatus10800ShieldSeconds);
        foreach (var connection in context.TeamStates.GetConnections(context.Connection))
        {
            if (!TryGetLegacyAttackStatus10800TeamMemberSession(
                    context,
                    connection,
                    currentCasterSession,
                    out var memberSession))
            {
                continue;
            }

            if (LegacyGetDistance(
                    currentCasterSession.Character.X,
                    currentCasterSession.Character.Y,
                    memberSession.Character.X,
                    memberSession.Character.Y) >= LegacyAttackStatus10800TeamDistance)
            {
                outOfRangeMembers++;
                continue;
            }

            var updatedMemberSession = ApplyLegacyAttackStatus10800TeamMemberBuff(
                memberSession,
                context.WorldData,
                expiresAtUtc);
            sessions.Set(connection, updatedMemberSession);
            if (ReferenceEquals(connection, context.Connection))
            {
                currentCasterSession = updatedMemberSession;
            }

            await connection.SendAsync(
                    LegacyShowEquipmentPacket.CreateEquipmentRefresh(
                        updatedMemberSession.PlayerItems,
                        updatedMemberSession.AlternateEquipmentActive).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            equipmentRefreshPackets++;

            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context,
                    connection,
                    updatedMemberSession,
                    cancellationToken)
                .ConfigureAwait(false);
            statusFlagRefreshPackets++;

            appliedMembers++;
        }

        return new LegacyAttackStatus10800TeamBuffResult(
            currentCasterSession,
            appliedMembers,
            outOfRangeMembers,
            equipmentRefreshPackets,
            statusFlagRefreshPackets);
    }

    private static bool TryGetLegacyAttackStatus10800TeamMemberSession(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession casterSession,
        out WorldSession session)
    {
        if (ReferenceEquals(connection, context.Connection))
        {
            session = casterSession;
            return true;
        }

        if (context.Sessions?.TryGet(connection, out var memberSession) == true &&
            memberSession is not null)
        {
            session = memberSession;
            return true;
        }

        session = default!;
        return false;
    }

    private static WorldSession ApplyLegacyAttackStatus10800TeamMemberBuff(
        WorldSession memberSession,
        WorldDataCatalog? worldData,
        DateTimeOffset expiresAtUtc)
    {
        var statusFields = CalculateLegacyRuntimeStatusFields(
            memberSession with { AlternateEquipmentActive = true },
            worldData);
        var currentDodge = Math.Max(
            ReadAttributeUInt32(memberSession.Character.Attributes, "Dodge", "DodgeRate"),
            statusFields[LegacyPlayerStatusPacket.DodgeIndex]);
        var updatedDodge = AddLegacyAttackStatus10800DodgeBonus(currentDodge);
        var updatedCharacter = WithCharacterAttribute(
            memberSession.Character,
            "Dodge",
            updatedDodge.ToString(CultureInfo.InvariantCulture));
        updatedCharacter = WithLegacyStatusFlag(updatedCharacter, LegacyAttackStatus10800ShieldOfTruthFlag);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyShieldofTruthExpiresAtUtcAttribute,
            expiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyShieldofTruthRemoveOnDeadAttribute,
            "true");

        return memberSession with
        {
            Character = updatedCharacter,
            AlternateEquipmentActive = true,
        };
    }

    private static uint AddLegacyAttackStatus10800DodgeBonus(uint dodge)
    {
        var updated = (ulong)dodge + ((ulong)dodge * 60 / 100);
        return updated > uint.MaxValue ? uint.MaxValue : (uint)updated;
    }

    private readonly record struct LegacyAttackStatus10800TeamBuffResult(
        WorldSession CasterSession,
        int AppliedMembers,
        int OutOfRangeMembers,
        int EquipmentRefreshPackets,
        int StatusFlagRefreshPackets);
}
