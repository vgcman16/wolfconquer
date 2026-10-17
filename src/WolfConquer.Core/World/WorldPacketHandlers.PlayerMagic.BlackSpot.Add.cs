using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerAddBlackSpotMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AddBlackSpot)
        {
            return false;
        }

        if (spell.Id != LegacyAddBlackSpotSpellId)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        reason:
                            "AddBlackSpot handling is currently modeled only for spell 11100 visible-monster targets."))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy AddBlackSpot magic " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; unsupported AddBlackSpot spell id.")
                .ConfigureAwait(false);
            return true;
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

        if (context.WorldData is not { } worldData ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                resourceSession.PlayerSpells,
                worldData,
                LegacyBlackSpotSpellId,
                out _,
                out var blackSpotSpell))
        {
            await SendLegacyAddBlackSpotAnimationAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    [],
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy AddBlackSpot magic with no learned BlackSpot duration spell " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} requiredSpellId={LegacyBlackSpotSpellId}.")
                .ConfigureAwait(false);
            return true;
        }

        if (context.MonsterStates is not { } monsterStates)
        {
            await SendLegacyAddBlackSpotAnimationAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    [],
                    cancellationToken)
                .ConfigureAwait(false);
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        targetKind: "monster",
                        resource: MonsterRuntimeStateResource,
                        reason:
                            "AddBlackSpot visible-monster targets were requested, but monster runtime state is not loaded."))
                .ConfigureAwait(false);
            return true;
        }

        var targetBudget = GetLegacyAddBlackSpotTargetBudget(playerSpell.Level);
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var now = DateTimeOffset.UtcNow;
        var blackSpotExpiresAt = now + TimeSpan.FromSeconds(blackSpotSpell.Duration);

        foreach (var target in SelectLegacyAddBlackSpotMonsterTargets(
            context,
            resourceSession,
            spell,
            query.X,
            query.Y,
            now))
        {
            if (animationTargets.Count >= targetBudget)
            {
                break;
            }

            if (IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily))
            {
                resourceSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                        context,
                        resourceSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = resourceSession };
            }

            if (!monsterStates.TryMarkBlackSpotUntil(target.Monster, now, blackSpotExpiresAt))
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                target.Monster.Uid,
                0,
                Hit: true,
                MsgAttackEffect.None));

            var blackSpot = LegacyBlackspotPacket.CreateActive(target.Monster.Uid);
            await SendFrameToPlayerAndVisiblePeersAsync(
                    context,
                    context.Connection,
                    resourceSession,
                    blackSpot.ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var playerTargetCount = 0;
        foreach (var target in SelectLegacyAddBlackSpotPlayerTargets(
            context,
            resourceSession,
            spell,
            query.X,
            query.Y))
        {
            if (animationTargets.Count >= targetBudget)
            {
                break;
            }

            if (!TryMarkLegacyPlayerBlackSpotUntil(context, target.ActiveSession, blackSpotExpiresAt, out var updatedTargetSession))
            {
                continue;
            }

            var targetUid = CheckedUInt32(
                updatedTargetSession.Character.Uid,
                nameof(updatedTargetSession.Character.Uid));
            animationTargets.Add(new LegacySpellAnimationTarget(
                targetUid,
                0,
                Hit: true,
                MsgAttackEffect.None));
            playerTargetCount++;

            var blackSpot = LegacyBlackspotPacket.CreateActive(targetUid);
            await SendFrameToPlayerAndVisiblePeersAsync(
                    context,
                    context.Connection,
                    resourceSession,
                    blackSpot.ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyAddBlackSpotAnimationAsync(
                context,
                resourceSession,
                query,
                playerSpell,
                spell,
                animationTargets,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy AddBlackSpot monster/player magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targets={animationTargets.Count} " +
                $"playerTargets={playerTargetCount} " +
                $"blackSpotDuration={blackSpotSpell.Duration} blackSpotExpiresAtUtc={blackSpotExpiresAt:O}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyAddBlackSpotAnimationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<LegacySpellAnimationTarget> targets,
        CancellationToken cancellationToken)
    {
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            targets);
        await SendPlayerSpellAnimationToVisibleAsync(context, session, animation, cancellationToken)
            .ConfigureAwait(false);
    }

    private static IEnumerable<LegacyPlayerMonsterMagicTarget> SelectLegacyAddBlackSpotMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY,
        DateTimeOffset now)
    {
        if (session.Character.MapId is < int.MinValue or > int.MaxValue ||
            context.WorldData is not { } worldData ||
            !worldData.MonsterSpawnsByMapId.TryGetValue((int)session.Character.MapId, out var spawns))
        {
            yield break;
        }

        var yieldedSpawns = new HashSet<MonsterSpawn>();
        foreach (var entry in LegacyStaticMonsterMaterializer.Enumerate(worldData, spawns))
        {
            var visible = entry.Instance;
            if (context.MonsterStates is { } monsterStates &&
                !monsterStates.TryGetVisibleInstance(entry.Instance, now, out visible))
            {
                continue;
            }

            if (!CanSeeMonster(session.Character, visible) ||
                !CanLegacyPlayerAttackMonster(session.Character, visible, entry.MonsterFamily, out _) ||
                LegacyGetDistance(originX, originY, visible.X, visible.Y) > spell.Range)
            {
                continue;
            }

            if (yieldedSpawns.Add(entry.Spawn))
            {
                yield return new LegacyPlayerMonsterMagicTarget(visible, entry.MonsterFamily);
            }
        }
    }

    private static int CountLegacyAddBlackSpotPendingPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var count = 0;
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(originX, originY, character.X, character.Y) <= spell.Range)
            {
                count++;
            }
        }

        return count;
    }

    private static IEnumerable<LegacyPlayerMagicTarget> SelectLegacyAddBlackSpotPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(originX, originY, character.X, character.Y) > spell.Range)
            {
                continue;
            }

            yield return candidate;
        }
    }

    private static int GetLegacyAddBlackSpotTargetBudget(ushort spellLevel)
    {
        return spellLevel switch
        {
            0 or 1 => 3,
            2 or 3 => 4,
            _ => 5,
        };
    }
}
