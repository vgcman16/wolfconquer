using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyPitchingRollAttribute = "LegacyPitchingRoll";

    private const ushort LegacyPitchingSpellId = 14_270;

    private static async Task<bool> HandleLegacyPlayerPitchingMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Pitching)
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
        if (spell.Id != LegacyPitchingSpellId)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer source-backed returned legacy Pitching magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id} sort={(int)spell.Sort}; " +
                    "source Pitching.cs returns after CanUseSpell.Verified for non-14270 rows.")
                .ConfigureAwait(false);
            return true;
        }

        if (!TryGetVisibleSessionByUid(
                resourceContext,
                resourceSession,
                query.OpponentUid,
                out var activeTarget))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy Pitching magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id} targetUid={query.OpponentUid}; " +
                    "source Pitching.cs returns after CanUseSpell.Verified when the visible player target is absent.")
                .ConfigureAwait(false);
            return true;
        }

        var targetSession = activeTarget.Session;
        var casterBattlePower = CalculateLegacyPitchingBattlePower(resourceSession);
        var targetBattlePower = CalculateLegacyPitchingBattlePower(targetSession);
        var successRate = unchecked(100 - (5 * (targetBattlePower - casterBattlePower)));
        var succeeded = CheckLegacyPitchingRateRoll(resourceSession.Character, successRate);
        var removedXpSkillFlags = Array.Empty<int>();
        var removedXpList = false;

        if (succeeded)
        {
            removedXpSkillFlags = LegacyXpSkillFlags
                .Where(flag => HasLegacyStatusFlag(targetSession.Character, flag))
                .ToArray();
            if (removedXpSkillFlags.Length > 0)
            {
                var updatedCharacter = targetSession.Character;
                foreach (var xpSkillFlag in LegacyXpSkillFlags)
                {
                    updatedCharacter = WithoutLegacyStatusFlag(updatedCharacter, xpSkillFlag);
                }

                foreach (var expiresAtAttribute in LegacyXpSkillExpiresAtAttributes)
                {
                    updatedCharacter = WithoutCharacterAttribute(updatedCharacter, expiresAtAttribute);
                }

                targetSession = targetSession with { Character = updatedCharacter };
                context.Sessions?.Set(activeTarget.Connection, targetSession);
                await SendPlayerStatusFlagVectorToVisibleAsync(
                        resourceContext,
                        activeTarget.Connection,
                        targetSession,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (HasLegacyStatusFlag(targetSession.Character, LegacyRuntimeUpdateTicker.XpListFlag))
            {
                targetSession = targetSession with
                {
                    Character = WithoutLegacyStatusFlag(
                        targetSession.Character,
                        LegacyRuntimeUpdateTicker.XpListFlag),
                };
                context.Sessions?.Set(activeTarget.Connection, targetSession);
                var targetUid = CheckedUInt32(targetSession.Character.Uid, nameof(targetSession.Character.Uid));

                // Player.RemoveFlag(XPList) sends the scalar before its status-vector update.
                await activeTarget.Connection.SendAsync(
                        LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(targetUid, value: 0).ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                await SendPlayerStatusFlagVectorToVisibleAsync(
                        resourceContext,
                        activeTarget.Connection,
                        targetSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                removedXpList = true;
            }

            targetSession = targetSession with
            {
                Character = WithCharacterAttribute(targetSession.Character, "XPCount", 0UL),
            };
            context.Sessions?.Set(activeTarget.Connection, targetSession);
        }

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var animationTargetUid = CheckedUInt32(targetSession.Character.Uid, nameof(targetSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
                casterUid,
                CheckedUInt16(targetSession.Character.X, nameof(targetSession.Character.X)),
                CheckedUInt16(targetSession.Character.Y, nameof(targetSession.Character.Y)),
                spell.Id,
                (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                playerSpell.UseSpellSoul,
                [new LegacySpellAnimationTarget(
                    animationTargetUid,
                    Damage: 0,
                    Hit: succeeded,
                    MsgAttackEffect.None)])
            with
        {
            AttackedUid = animationTargetUid,
        };
        await SendPlayerSpellAnimationToVisibleAsync(
                resourceContext,
                resourceSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Pitching magic " +
                $"{FormatSession(resourceContext)} spellId={spell.Id} targetUid={animationTargetUid} " +
                $"casterBattlePower={casterBattlePower} targetBattlePower={targetBattlePower} " +
                $"successRate={successRate} succeeded={succeeded} removedXpSkillCount={removedXpSkillFlags.Length} " +
                $"removedXpList={removedXpList} xpCountReset={succeeded} animationX={targetSession.Character.X} " +
                $"animationY={targetSession.Character.Y}; source=Pitching.cs case 14270.")
            .ConfigureAwait(false);
        return true;
    }

    private static int CalculateLegacyPitchingBattlePower(WorldSession session)
    {
        var battlePower = CalculateLegacySoulShackleBattlePower(session);
        return battlePower > int.MaxValue ? int.MaxValue : (int)battlePower;
    }

    private static bool CheckLegacyPitchingRateRoll(CharacterSnapshot caster, int successRate)
    {
        var roll = TryReadUInt32Attribute(
                caster.Attributes,
                out var forcedRoll,
                RuntimeLegacyPitchingRollAttribute,
                "PitchingRoll")
            ? checked((int)(forcedRoll % 100))
            : Random.Shared.Next(0, 100);
        return successRate > roll;
    }

}
