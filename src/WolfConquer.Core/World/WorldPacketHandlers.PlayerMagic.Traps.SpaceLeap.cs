using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerSpaceLeapLayTrapMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Id != LegacyLayTrap.SpaceLeapSpellId)
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

        var character = resourceSession.Character;
        if (!IsLegacyWaterTaoistClass(character.Class) ||
            IsLegacySpaceLeapQualifierBlocked(character) ||
            LegacyLayTrap.IsSpaceLeapBlockedMap(character.MapId))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer returned legacy SpaceLeap LayTrap magic after preparation " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                    $"class={character.Class} map={character.MapId} inQualifier={IsLegacySpaceLeapQualifierBlocked(character)}; " +
                    "source=LayTrap.cs case 15620 gate breaks without floor effect.")
                .ConfigureAwait(false);
            return true;
        }

        var casterUid = CheckedUInt32(character.Uid, nameof(character.Uid));
        var casterY = CheckedUInt16(character.Y, nameof(character.Y));
        var floorPacket = new LegacyFloorItemPacket(
            NextLegacyFloorItemUid(),
            LegacyLayTrap.SpaceLeapFloorItemId,
            query.X,
            query.Y,
            LegacyLayTrap.SpaceLeapFloorLife,
            LegacyFloorItemDropType.Effect,
            Life: LegacyLayTrap.SpaceLeapFloorLife,
            LegacyLayTrap.SpaceLeapFloorColor,
            casterUid,
            ReadAttributeUInt32(character.Attributes, "GuildID", "GuildId"),
            FlowerType: LegacyLayTrap.SpaceLeapFlowerType,
            Timer: 0,
            OwnerX: 0,
            OwnerY: 0,
            LegacyLayTrap.SpaceLeapFloorName);

        var now = DateTimeOffset.UtcNow;
        var storedFloorEffectCount = 0;
        if (context.FloorItems is { } floorItems)
        {
            var effect = new FloorItemRuntimeEffect(
                spell.Id,
                query.X,
                query.Y,
                now.AddMilliseconds(LegacyLayTrap.SpaceLeapFloorLifetimeMilliseconds),
                Distance: 0,
                HitRadius: 0);
            floorItems.AddVisible(
                FloorItemRuntimeEntry.ForEffect(character.MapId, floorPacket, effect),
                now);
            storedFloorEffectCount = 1;
        }
        else
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        FloorItemRuntimeStateResource,
                        "spaceleap-floor-effect-runtime",
                        "SpaceLeap floor effect was sent, but floor item runtime state is not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("floorUid", floorPacket.Uid),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                        ]))
                .ConfigureAwait(false);
        }

        await SendFloorItemVisibleAsync(context, character, floorPacket, cancellationToken)
            .ConfigureAwait(false);

        var teamActionRecipientCount = await SendLegacySpaceLeapTeamActionAsync(
                context with { Session = resourceSession },
                resourceSession,
                casterUid,
                cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                resourceSession,
                playerSpell,
                spell,
                totalDamage: 10_000,
                cancellationToken)
            .ConfigureAwait(false);

        var animation = new LegacySpellAnimationPacket(
            casterUid,
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            playerSpell.UseSpellSoul,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(
                casterUid,
                0,
                Hit: true,
                MsgAttackEffect.None,
                NewY: casterY)]);
        await SendPlayerSpellAnimationToVisibleAsync(
                context with { Session = updatedSession },
                updatedSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy SpaceLeap LayTrap magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} soulLevel={playerSpell.UseSpellSoul} targetX={query.X} targetY={query.Y} " +
                $"floorItemId={floorPacket.ItemId} storedFloorEffectCount={storedFloorEffectCount} " +
                $"teamActionRecipientCount={teamActionRecipientCount}; source=LayTrap.cs case 15620.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacySpaceLeapQualifierBlocked(CharacterSnapshot character)
    {
        return ReadAttributeBool(
            character.Attributes,
            "InQualifier",
            "Qualifier",
            "InQualifierMatch",
            "InTeamQualifier",
            "TeamQualifier",
            "InTeamQualifierMatch");
    }

    private static async ValueTask<int> SendLegacySpaceLeapTeamActionAsync(
        WorldPacketHandlerContext context,
        WorldSession casterSession,
        uint casterUid,
        CancellationToken cancellationToken)
    {
        if (context.TeamStates is not { } teamStates ||
            context.Sessions is not { } sessions ||
            !teamStates.HasTeam(context.Connection))
        {
            return 0;
        }

        var caster = casterSession.Character;
        var action = new MsgActionPacket
        {
            DwParam = casterUid,
            Timestamp = unchecked((uint)Environment.TickCount),
            Type = (MsgActionType)LegacyLayTrap.SpaceLeapTeamActionType,
            RemainTime = LegacyLayTrap.SpaceLeapTeamRemainTime,
            Strings = [caster.Name],
        };
        var frame = action.ToFrameBytes();
        var sentCount = 0;
        foreach (var connection in teamStates.GetConnections(context.Connection))
        {
            if (!sessions.TryGet(connection, out var memberSession) ||
                memberSession is null ||
                LegacyGetDistance(
                    caster.X,
                    caster.Y,
                    memberSession.Character.X,
                    memberSession.Character.Y) >= LegacyLayTrap.SpaceLeapTeamDistance)
            {
                continue;
            }

            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        return sentCount;
    }
}
