using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerCallPetMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell runtimeSpell,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session ||
            context.WorldData is not { } worldData ||
            !LegacyMagicSupportRuntime.TryResolveSupportSpell(
                worldData,
                runtimeSpell.Id,
                playerSpell.Level,
                out var supportSpell))
        {
            return false;
        }

        var support = LegacyMagicSupportRuntime.Classify(supportSpell);
        if (!support.Supported ||
            support.Kind is not (LegacyMagicSupportKind.ShadowClone or LegacyMagicSupportKind.Thundercloud))
        {
            return false;
        }

        var existingCloneFlags = ReadShadowCloneFlags(session.Character);
        var casterActor = CreateLegacyMagicSupportActor(
            session,
            LegacyMagicSupportTargetKind.Caster,
            worldData);
        var doubleThunderRuneEquipped = support.Kind == LegacyMagicSupportKind.Thundercloud &&
            HasLegacyDoubleThunderRuneEquipped(session, worldData);
        var outcome = LegacyMagicSupportRuntime.CalculateCallPet(
            supportSpell,
            casterActor,
            query.X,
            query.Y,
            existingCloneFlags.Count,
            doubleThunderRuneEquipped);
        if (!outcome.Supported)
        {
            var fallthroughSession = await TryPrepareLegacyPlayerMagicCastAsync(
                    context,
                    session,
                    runtimeSpell,
                    cancellationToken)
                .ConfigureAwait(false);
            if (fallthroughSession is null)
            {
                return true;
            }

            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy CallPet fallthrough magic " +
                    $"{FormatSession(context with { Session = fallthroughSession })} spellId={runtimeSpell.Id}; " +
                    $"{outcome.Reason} source=CallPet.cs switch default fallthrough after CanUseSpell.Verified.")
                .ConfigureAwait(false);
            return true;
        }

        var casterUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        if (outcome.Kind == LegacyMagicSupportKind.RemoveExistingSummons)
        {
            var removalSession = session with
            {
                Character = WithoutLegacyShadowCloneFlags(session.Character),
            };
            context.Sessions?.Set(context.Connection, removalSession);
            var ownerX = CheckedUInt16(removalSession.Character.X, nameof(removalSession.Character.X));
            var ownerY = CheckedUInt16(removalSession.Character.Y, nameof(removalSession.Character.Y));
            await SendLegacySummonActionFramesAsync(
                    context with { Session = removalSession },
                    removalSession,
                    CreateLegacyShadowCloneUids(existingCloneFlags.Count),
                    MsgActionType.RemoveEntity,
                    ownerX,
                    ownerY,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy CallPet summon removal " +
                    $"{FormatSession(context)} spellId={runtimeSpell.Id} removed={existingCloneFlags.Count}.")
                .ConfigureAwait(false);
            return true;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                runtimeSpell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        var updatedSession = resourceSession;
        var selfOnlyFrames = new List<byte[]>();
        var summonFrames = new List<byte[]>();
        IReadOnlyList<uint> summonActionUids = [];
        IReadOnlyList<LivePetProjection> thundercloudActionPets = [];
        if (outcome.Kind == LegacyMagicSupportKind.ShadowClone)
        {
            var cloneFlags = outcome.Summons
                .SelectMany(summon => summon.CloneFlags)
                .Select(flag => (ushort)Math.Clamp(flag, ushort.MinValue, ushort.MaxValue))
                .ToArray();
            updatedSession = resourceSession with
            {
                Character = WithLegacyShadowCloneFlags(resourceSession.Character, cloneFlags),
            };
            context.Sessions?.Set(context.Connection, updatedSession);

            foreach (var cloneSpawn in CreateShadowCloneSpawnPackets(updatedSession, worldData))
            {
                summonFrames.Add(cloneSpawn.ToFrameBytes());
            }

            summonActionUids = CreateLegacyShadowCloneUids(cloneFlags.Length);
        }
        else if (outcome.Kind == LegacyMagicSupportKind.Thundercloud)
        {
            var existingPets = ReadLivePets(resourceSession.Character);
            var newPets = CreateLegacyThundercloudPetProjections(
                resourceSession,
                outcome,
                query.X,
                query.Y,
                existingPets,
                context.Terrain);
            var allPets = existingPets.Concat(newPets).ToArray();
            updatedSession = resourceSession with
            {
                Character = WithLegacyLivePetProjections(resourceSession.Character, allPets),
            };
            context.Sessions?.Set(context.Connection, updatedSession);

            foreach (var pet in newPets)
            {
                selfOnlyFrames.Add(CreateLegacyPetInfoPacket(updatedSession, pet).ToFrameBytes());
                summonFrames.Add(CreateLegacyLivePetSpawnPacket(
                        updatedSession,
                        pet,
                        context.ClientCompatibility)
                    .ToFrameBytes());
            }

            RegisterLegacyThundercloudFloorRuntimeEffects(context, updatedSession, newPets);
            summonActionUids = newPets.Select(pet => pet.Uid).ToArray();
            thundercloudActionPets = newPets;
        }

        var updatedContext = context with { Session = updatedSession };
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            runtimeSpell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            []);
        var sendAnimationBeforeSummons = outcome.Kind == LegacyMagicSupportKind.Thundercloud;
        if (sendAnimationBeforeSummons)
        {
            await SendPlayerSpellAnimationToVisibleAsync(
                    updatedContext,
                    updatedSession,
                    animation,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var frame in selfOnlyFrames)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        var actionX = outcome.Kind == LegacyMagicSupportKind.ShadowClone
            ? CheckedUInt16(updatedSession.Character.X, nameof(updatedSession.Character.X))
            : query.X;
        var actionY = outcome.Kind == LegacyMagicSupportKind.ShadowClone
            ? CheckedUInt16(updatedSession.Character.Y, nameof(updatedSession.Character.Y))
            : query.Y;
        foreach (var frame in summonFrames)
        {
            await SendFrameToVisiblePlayersAsync(updatedContext, updatedSession, frame, cancellationToken)
                .ConfigureAwait(false);
        }

        if (thundercloudActionPets.Count == 0)
        {
            await SendLegacySummonActionFramesAsync(
                    updatedContext,
                    updatedSession,
                    summonActionUids,
                    MsgActionType.MonsterRespawnEffect,
                    actionX,
                    actionY,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            foreach (var pet in thundercloudActionPets)
            {
                await SendLegacySummonActionFramesAsync(
                        updatedContext,
                        updatedSession,
                        [pet.Uid],
                        MsgActionType.MonsterRespawnEffect,
                        pet.X ?? query.X,
                        pet.Y ?? query.Y,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (!sendAnimationBeforeSummons)
        {
            await SendPlayerSpellAnimationToVisibleAsync(
                    updatedContext,
                    updatedSession,
                    animation,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                runtimeSpell,
                outcome.SpellExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CallPet magic " +
                $"{FormatSession(context)} spellId={runtimeSpell.Id} kind={outcome.Kind} " +
                $"summons={summonActionUids.Count}.")
            .ConfigureAwait(false);
        return true;
    }

    private static IReadOnlyList<uint> CreateLegacyShadowCloneUids(int count)
    {
        var uids = new uint[count];
        for (var index = 0; index < count; index++)
        {
            uids[index] = checked(LegacyShadowCloneCounterStart + (uint)index + 1U);
        }

        return uids;
    }

    private static async ValueTask SendLegacySummonActionFramesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        IReadOnlyList<uint> uids,
        MsgActionType actionType,
        ushort x,
        ushort y,
        CancellationToken cancellationToken)
    {
        foreach (var uid in uids)
        {
            var action = new MsgActionPacket
            {
                ObjId = uid,
                Type = actionType,
                PositionX = x,
                PositionY = y,
            };
            await SendFrameToVisiblePlayersAsync(context, session, action.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
