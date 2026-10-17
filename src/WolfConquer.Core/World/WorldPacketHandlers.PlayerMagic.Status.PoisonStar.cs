using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyPoisonStarExpiresAtUtcAttribute = "LegacyPoisonStarExpiresAtUtc";
    internal const string RuntimeLegacyPoisonStarRateRollAttribute = "LegacyPoisonStarRoll";
    internal const string RuntimeLegacyPoisonStarSturdinessRollAttribute = "LegacyPoisonStarSturdinessRoll";

    private const ushort LegacyPoisonStarAttachStatusSpellId = 6_002;
    private const uint LegacyPoisonStarSpellProgressDamage = 250;
    private const uint LegacyPoisonStarSuccessRate = 80;
    private const string RuntimeLegacyPoisonStarRemoveOnDeadAttribute = "LegacyPoisonStarRemoveOnDead";

    private static async ValueTask<bool> HandleLegacyPlayerPoisonStarAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyPoisonStarAttachStatusSpellId)
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
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var targetApplied = false;
        var targetMatched = TryGetVisibleSessionByUid(
            resourceContext,
            resourceSession,
            query.OpponentUid,
            out var activeTarget);
        var sturdinessAvoided = false;
        var successRollPassed = false;

        if (targetMatched)
        {
            if (HasLegacyPoisonStarSturdinessAvoidance(activeTarget.Session, context.WorldData))
            {
                sturdinessAvoided = true;
            }
            else if (CheckLegacyPoisonStarSuccessRateRoll(
                         resourceSession.Character,
                         activeTarget.Session.Character))
            {
                successRollPassed = true;
                var updatedTargetSession = ApplyLegacyPoisonStarStatus(
                    activeTarget.Session,
                    spell,
                    DateTimeOffset.UtcNow);
                context.Sessions?.Set(activeTarget.Connection, updatedTargetSession);
                var targetContext = context with
                {
                    Connection = activeTarget.Connection,
                    Session = updatedTargetSession,
                };
                await SendPlayerStatusFlagVectorToVisibleAsync(
                        targetContext,
                        updatedTargetSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                await activeTarget.Connection.SendAsync(
                        LegacyMsgMessagePacket
                            .SystemTalk(CreateLegacyPoisonStarSystemMessage(spell.Duration))
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(updatedTargetSession.Character.Uid, nameof(updatedTargetSession.Character.Uid)),
                    1,
                    Hit: true,
                    MsgAttackEffect.None));
                targetApplied = true;
            }
            else
            {
                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(activeTarget.Session.Character.Uid, nameof(activeTarget.Session.Character.Uid)),
                    spell.Id,
                    Hit: false,
                    MsgAttackEffect.None));
            }
        }

        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: 0,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(
                resourceContext,
                resourceSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        var updatedResourceSession = resourceSession;
        if (!sturdinessAvoided)
        {
            updatedResourceSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                    resourceContext,
                    resourceSession,
                    playerSpell,
                    spell,
                    LegacyPoisonStarSpellProgressDamage,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy PoisonStar AttachStatus magic " +
                $"{FormatSession(resourceContext with { Session = updatedResourceSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} targetUid={query.OpponentUid} targetMatched={targetMatched} " +
                $"statusFlag={LegacyPoisonStarFlag} targetApplied={targetApplied} duration={spell.Duration} " +
                $"sturdinessAvoided={sturdinessAvoided} successRollPassed={successRollPassed} " +
                $"spellProgressDamage={(sturdinessAvoided ? 0 : LegacyPoisonStarSpellProgressDamage)}; " +
                "source=AttachStatus.cs case 6002.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyPoisonStarStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithLegacyStatusFlag(session.Character, LegacyPoisonStarFlag),
            RuntimeLegacyPoisonStarRemoveOnDeadAttribute,
            "true");
        if (spell.Duration > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyPoisonStarExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        return session with { Character = updatedCharacter };
    }

    private static bool HasLegacyPoisonStarSturdinessAvoidance(
        WorldSession targetSession,
        WorldDataCatalog? worldData)
    {
        if (!TryGetLegacyEquippedRuneLevel(targetSession.PlayerItems, worldData, "Sturdiness", out var itemLevel))
        {
            return false;
        }

        var rate = unchecked((byte)(5 + itemLevel * 5));
        return CheckLegacyPoisonStarRateRoll(
            targetSession.Character.Attributes,
            rate,
            RuntimeLegacyPoisonStarSturdinessRollAttribute,
            "PoisonStarSturdinessRoll",
            "LegacySturdinessRoll",
            "SturdinessRoll");
    }

    private static bool CheckLegacyPoisonStarSuccessRateRoll(
        CharacterSnapshot caster,
        CharacterSnapshot target)
    {
        if (TryReadUInt32Attribute(
                caster.Attributes,
                out var casterRoll,
                RuntimeLegacyPoisonStarRateRollAttribute,
                "PoisonStarRoll"))
        {
            return casterRoll % 100 < LegacyPoisonStarSuccessRate;
        }

        return CheckLegacyPoisonStarRateRoll(
            target.Attributes,
            LegacyPoisonStarSuccessRate,
            RuntimeLegacyPoisonStarRateRollAttribute,
            "PoisonStarRoll");
    }

    private static bool CheckLegacyPoisonStarRateRoll(
        IReadOnlyDictionary<string, string> attributes,
        uint rate,
        params string[] attributeNames)
    {
        if (rate == 0)
        {
            return false;
        }

        if (rate >= 100)
        {
            return true;
        }

        var roll = TryReadUInt32Attribute(attributes, out var forcedRoll, attributeNames)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < rate;
    }

    private static string CreateLegacyPoisonStarSystemMessage(uint duration)
    {
        return "Poison star activated. You will not be able to use drugs for " +
            duration.ToString(CultureInfo.InvariantCulture) +
            " seconds.";
    }
}
