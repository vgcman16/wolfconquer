using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyToxicFogSpellId = 6_001;
    private const string LegacyToxicPoisonRuntimeResource = "LegacyToxicPoisonRuntime";
    private const uint LegacyToxicFogSpellProgressDamage = 1_000;
    private const byte LegacyToxicFogRadius = 5;

    private static async ValueTask<bool> HandleLegacyPlayerToxicMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Toxic)
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
        if (spell.Id != LegacyToxicFogSpellId)
        {
            await LogParityGapAsync(
                    resourceContext,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        skillName: spell.Name,
                        targetKind: "monster-player",
                        resource: LegacyToxicPoisonRuntimeResource,
                        source: "AttackHandler.Toxic.Execute",
                        reason:
                            "Toxic sort passed resources/cooldown, but only ToxicFog spell 6001 has reviewed source-backed handling."))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy Toxic magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id} sort={(int)spell.Sort}; " +
                    "source Toxic.cs only handles ClientSpell.ID 6001 after CanUseSpell.Verified.")
                .ConfigureAwait(false);
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        var poisonLevel = checked((byte)Math.Min(byte.MaxValue, playerSpell.Level + playerSpell.UseSpellSoul));
        var poisonDuration = spell.Duration <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(spell.Duration);
        var updatedSession = resourceSession;
        var monsterTargets = SelectLegacyToxicMonsterTargets(resourceContext, updatedSession, query);
        var playerTargets = SelectLegacyToxicPlayerTargets(resourceContext, updatedSession, query, spell);
        var poisonedMonsters = 0;
        var poisonedPlayers = 0;
        var toxinEraserAvoidedPlayers = 0;
        var sturdinessAvoidedPlayers = 0;

        if (monsterTargets.Any(static target => IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily)))
        {
            updatedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                    resourceContext,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
            resourceContext = resourceContext with { Session = updatedSession };
        }

        if (monsterTargets.Count > 0 &&
            resourceContext.MonsterStates is not { } monsterStates)
        {
            await LogLegacyToxicRuntimeStateGapAsync(
                    resourceContext,
                    spell,
                    monsterTargets.Count)
                .ConfigureAwait(false);
        }
        else if (resourceContext.MonsterStates is { } loadedMonsterStates)
        {
            foreach (var target in monsterTargets)
            {
                var result = loadedMonsterStates.ApplyPoison(
                    target.Monster,
                    poisonLevel,
                    poisonDuration,
                    now);
                if (result.Applied)
                {
                    poisonedMonsters++;
                }
            }
        }

        foreach (var target in playerTargets)
        {
            var poisonResult = TryApplyLegacyToxicFogPlayerPoison(
                target.ActiveSession.Session,
                playerSpell,
                spell,
                resourceContext.WorldData,
                now);
            switch (poisonResult.Avoidance)
            {
                case LegacyToxicFogPlayerPoisonAvoidance.ToxinEraser:
                    toxinEraserAvoidedPlayers++;
                    continue;
                case LegacyToxicFogPlayerPoisonAvoidance.Sturdiness:
                    sturdinessAvoidedPlayers++;
                    continue;
            }

            if (!poisonResult.Applied)
            {
                continue;
            }

            var updatedTargetSession = poisonResult.Session;
            resourceContext.Sessions?.Set(target.ActiveSession.Connection, updatedTargetSession);
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    resourceContext,
                    target.ActiveSession.Connection,
                    updatedTargetSession,
                    cancellationToken)
                .ConfigureAwait(false);
            poisonedPlayers++;
        }

        var poisonedTargets = poisonedMonsters + poisonedPlayers;
        var progressDamage = poisonedTargets > 0
            ? LegacyToxicFogSpellProgressDamage
            : 0U;
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                resourceContext,
                updatedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        resourceContext = resourceContext with { Session = updatedSession };

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            []);
        await SendPlayerSpellAnimationToVisibleAsync(resourceContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy ToxicFog magic " +
                $"{FormatSession(resourceContext)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} useSpellSoul={playerSpell.UseSpellSoul} " +
                $"poisonLevel={poisonLevel} duration={spell.Duration} originX={query.X} originY={query.Y} " +
                $"monsterTargets={monsterTargets.Count} playerTargets={playerTargets.Count} " +
                $"poisonedMonsters={poisonedMonsters} poisonedPlayers={poisonedPlayers} " +
                $"toxinEraserAvoidedPlayers={toxinEraserAvoidedPlayers} " +
                $"sturdinessAvoidedPlayers={sturdinessAvoidedPlayers} " +
                $"spellProgressDamage={progressDamage} animationTargets=0; " +
                "source Toxic.cs bounded runtime slice.")
            .ConfigureAwait(false);
        return true;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyToxicMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (candidate.Monster.Boss ||
                LegacyGetDistance(query.X, query.Y, candidate.Monster.X, candidate.Monster.Y) >= LegacyToxicFogRadius)
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyToxicPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(query.X, query.Y, character.X, character.Y) >= LegacyToxicFogRadius)
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static async ValueTask LogLegacyToxicRuntimeStateGapAsync(
        WorldPacketHandlerContext context,
        LegacyMagicRuntimeSpell spell,
        int monsterTargetCount)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    MonsterRuntimeStateResource,
                    "toxicfog-monster-poison",
                    "ToxicFog selected source-shaped monster targets, but MonsterRuntimeStateStore is not loaded so monster PoisonLevel/Poisoned state was not applied.",
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                        LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                        LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                        LegacyParityGapDiagnostics.Fact("targetKind", "monster"),
                        LegacyParityGapDiagnostics.Fact("targets", monsterTargetCount),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("sort", (int)spell.Sort),
                        LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                        LegacyParityGapDiagnostics.Fact("targetKind", "monster"),
                    ]))
            .ConfigureAwait(false);
    }
}
