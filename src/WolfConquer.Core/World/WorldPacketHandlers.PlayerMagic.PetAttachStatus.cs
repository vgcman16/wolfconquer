using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyThundercloudPetAttachStatusProgressDamage = 500;

    private static async ValueTask<bool> HandleLegacyPlayerPetAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!LegacyMagicSortCapabilities.IsSupportedPetAttachStatusRuntimeSpell(spell.Sort, spell.Id))
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
        await resourceContext.Connection.SendAsync(
                new MsgAttackPacket(query with { Uid = casterUid }).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (HasLegacyStatusFlag(resourceSession.Character, LegacyRevengeTailFlag))
        {
            var updatedSession = resourceSession with
            {
                Character = ClearLegacyRevengeTailStatus(resourceSession.Character),
            };
            context.Sessions?.Set(context.Connection, updatedSession);
            var updatedContext = context with { Session = updatedSession };
            await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy Thundercloud PetAttachStatus RevengeTail removal " +
                    $"{FormatSession(updatedContext)} spellId={spell.Id} removedStatusFlag={LegacyRevengeTailFlag}; " +
                    "source=matching DecompiledSource PetAttachStatus.cs case 12970.")
                .ConfigureAwait(false);
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        var thunderboltExpiresAtUtc = now.AddSeconds(spell.Duration);
        var pets = ReadLivePets(resourceSession.Character);
        var updatedPets = pets.ToArray();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        for (var index = 0; index < updatedPets.Length; index++)
        {
            var pet = updatedPets[index];
            if (!IsOwnedVisibleLegacyThundercloud(resourceSession.Character, pet))
            {
                continue;
            }

            var activeExpiry = pet.ThunderboltExpiresAtUtc is { } existingExpiry && existingExpiry > now
                ? existingExpiry
                : thunderboltExpiresAtUtc;
            updatedPets[index] = pet with { ThunderboltExpiresAtUtc = activeExpiry };
            animationTargets.Add(new LegacySpellAnimationTarget(
                pet.Uid,
                Damage: 0,
                Hit: true,
                MsgAttackEffect.None));
        }

        var updatedCharacter = animationTargets.Count == 0
            ? resourceSession.Character
            : WithLegacyLivePetProjections(resourceSession.Character, updatedPets);
        var castSession = resourceSession with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, castSession);
        var castContext = context with { Session = castSession };

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
        await SendPlayerSpellAnimationToVisibleAsync(castContext, castSession, animation, cancellationToken)
            .ConfigureAwait(false);

        castSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                castContext,
                castSession,
                playerSpell,
                spell,
                LegacyThundercloudPetAttachStatusProgressDamage,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Thundercloud PetAttachStatus magic " +
                $"{FormatSession(castContext with { Session = castSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} floorPetCount={animationTargets.Count} " +
                $"attackSpeedMs={LegacyThundercloudThunderboltAttackSpeedMilliseconds} " +
                $"durationSeconds={spell.Duration} progressDamage={LegacyThundercloudPetAttachStatusProgressDamage}; " +
                "source=matching DecompiledSource PetAttachStatus.cs case 12970.")
            .ConfigureAwait(false);
        return true;
    }
}
