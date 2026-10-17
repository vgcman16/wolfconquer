using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyOblivionExpiresAtUtcAttribute = "LegacyOblivionExpiresAtUtc";

    private const ushort LegacyOblivionSpellId = 10_390;
    private static readonly TimeSpan LegacyOblivionDuration = TimeSpan.FromSeconds(30);

    private static async ValueTask<bool> HandleLegacyPlayerOblivionMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Oblivion ||
            spell.Id != LegacyOblivionSpellId)
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

        var updatedSession = resourceSession;
        var updatedContext = context with { Session = updatedSession };
        var consumedXpList = HasLegacyStatusFlag(
            updatedSession.Character,
            LegacyRuntimeUpdateTicker.XpListFlag);
        if (consumedXpList)
        {
            updatedSession = updatedSession with
            {
                Character = WithoutLegacyStatusFlag(
                    updatedSession.Character,
                    LegacyRuntimeUpdateTicker.XpListFlag),
            };
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = context with { Session = updatedSession };

            var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(casterUid, value: 0).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    updatedContext,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var autoHunt = ReadAttributeBool(
            updatedSession.Character.Attributes,
            RuntimeAutoHuntAttribute);
        int? replacedXpSkillFlag = null;
        var refreshedOblivion = false;
        if (!autoHunt)
        {
            replacedXpSkillFlag = FindLegacyOblivionCurrentXpSkillFlag(updatedSession.Character);
            refreshedOblivion = replacedXpSkillFlag == LegacyOblivionFlag;

            updatedSession = updatedSession with
            {
                Character = WithCharacterAttribute(updatedSession.Character, "XPCount", 0UL),
            };
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = context with { Session = updatedSession };

            var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeXPCircleTickUpdate(casterUid, count: 0).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            if (replacedXpSkillFlag is { } activeFlag && activeFlag != LegacyOblivionFlag)
            {
                updatedSession = updatedSession with
                {
                    Character = WithoutLegacyStatusFlag(updatedSession.Character, activeFlag),
                };
                context.Sessions?.Set(context.Connection, updatedSession);
                updatedContext = context with { Session = updatedSession };
                await SendPlayerStatusFlagVectorToVisibleAsync(
                        updatedContext,
                        updatedSession,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            updatedSession = updatedSession with
            {
                Character = WithLegacyOpenXpSkill(
                    updatedSession.Character,
                    LegacyOblivionFlag,
                    RuntimeLegacyOblivionExpiresAtUtcAttribute,
                    DateTimeOffset.UtcNow,
                    LegacyOblivionDuration),
            };
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = context with { Session = updatedSession };

            if (!refreshedOblivion)
            {
                await SendPlayerStatusFlagVectorToVisibleAsync(
                        updatedContext,
                        updatedSession,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var animationCasterUid = CheckedUInt32(
            updatedSession.Character.Uid,
            nameof(updatedSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            animationCasterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(animationCasterUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(
                updatedContext,
                updatedSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Oblivion magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"consumedXpList={consumedXpList} autoHunt={autoHunt} " +
                $"replacedXpSkillFlag={replacedXpSkillFlag?.ToString() ?? "none"} " +
                $"openedOblivion={!autoHunt} refreshedOblivion={refreshedOblivion} " +
                "durationSeconds=30; source=Oblivion.cs.")
            .ConfigureAwait(false);
        return true;
    }

    private static int? FindLegacyOblivionCurrentXpSkillFlag(CharacterSnapshot character)
    {
        foreach (var xpSkillFlag in LegacyXpSkillFlags)
        {
            if (HasLegacyStatusFlag(character, xpSkillFlag))
            {
                return xpSkillFlag;
            }
        }

        return null;
    }
}
