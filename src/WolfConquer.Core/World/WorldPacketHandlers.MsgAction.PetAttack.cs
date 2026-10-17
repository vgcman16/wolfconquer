using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyThundercloudMonsterFamilyId = 999_996_641;
    private const ushort LegacyThundercloudCallPetSpellId = 12_840;
    private const ushort LegacyThundercloudAttackSpellId = 13_190;
    private const int LegacyThundercloudAttackSpeedMilliseconds = 1_000;
    private const int LegacyThundercloudThunderboltAttackSpeedMilliseconds = 500;

    private sealed record LegacyThundercloudPetAttackResult(
        WorldSession Session,
        string TargetKind,
        ushort TargetX,
        ushort TargetY,
        LegacySpellAnimationTarget AnimationTarget,
        ulong ProgressDamage = 0,
        LegacyPlayerMagicDamageResult? PlayerResult = null);

    private static async ValueTask HandleLegacyPetAttackActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session ||
            context.WorldData is not { } worldData ||
            packet.TargetUid == 0)
        {
            return;
        }

        var petUid = unchecked((uint)packet.NpcId);
        var pets = ReadLivePets(session.Character);
        var petIndex = FindLegacyThundercloudPetIndex(pets, petUid);
        if (petIndex < 0)
        {
            return;
        }

        var pet = pets[petIndex];
        if (!IsOwnedVisibleLegacyThundercloud(session.Character, pet))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (pet.NextAttackAtUtc is { } readyAtUtc && readyAtUtc > now)
        {
            return;
        }

        if (!LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyThundercloudCallPetSpellId,
                out _,
                out var callPetSpell) ||
            !LegacyMagicRuntime.TryResolveSpell(
                worldData,
                LegacyThundercloudAttackSpellId,
                spellLevel: 0,
                out var thundercloudAttackSpell))
        {
            return;
        }

        LegacyThundercloudPetAttackResult? attackResult = null;
        if (TryFindVisibleMonsterTargetByUid(
                context,
                session.Character,
                packet.TargetUid,
                out var monsterTarget))
        {
            attackResult = await TryHandleLegacyThundercloudMonsterPetAttackAsync(
                    context,
                    session,
                    pet,
                    monsterTarget,
                    callPetSpell,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (TryGetVisibleSessionByUid(
                     context,
                     session,
                     packet.TargetUid,
                     out var playerTarget))
        {
            attackResult = await TryHandleLegacyThundercloudPlayerPetAttackAsync(
                    context,
                    session,
                    playerTarget,
                    callPetSpell,
                    thundercloudAttackSpell,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (TryFindVisibleSobNpcByUid(
                     context,
                     session.Character,
                     packet.TargetUid,
                     out var sobNpcTarget))
        {
            attackResult = await TryHandleLegacyThundercloudSobNpcPetAttackAsync(
                    context,
                    session,
                    sobNpcTarget,
                    callPetSpell,
                    thundercloudAttackSpell,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (attackResult is null)
        {
            return;
        }

        var thunderboltActive = pet.ThunderboltExpiresAtUtc is { } expiresAtUtc && expiresAtUtc > now;
        var nextAttackAtUtc = now.AddMilliseconds(
            thunderboltActive
                ? LegacyThundercloudThunderboltAttackSpeedMilliseconds
                : LegacyThundercloudAttackSpeedMilliseconds);
        var updatedPets = pets.ToArray();
        updatedPets[petIndex] = pet with
        {
            NextAttackAtUtc = nextAttackAtUtc,
            ThunderboltExpiresAtUtc = thunderboltActive ? pet.ThunderboltExpiresAtUtc : null,
        };
        var updatedSession = attackResult.Session with
        {
            Character = WithLegacyLivePetProjections(attackResult.Session.Character, updatedPets),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        context = context with { Session = updatedSession };

        if (attackResult.ProgressDamage != 0)
        {
            updatedSession = await ApplyLegacyStrikeIncreaseExperienceAsync(
                    context,
                    updatedSession,
                    attackResult.ProgressDamage,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };
        }

        if (attackResult.PlayerResult is { } playerResult)
        {
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    context,
                    updatedSession,
                    playerResult,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var animation = LegacySpellAnimationPacket.ForMonsterMagic(
            petUid,
            attackResult.AnimationTarget.Uid,
            attackResult.TargetX,
            attackResult.TargetY,
            LegacyThundercloudAttackSpellId,
            attackResult.AnimationTarget.Damage,
            attackResult.AnimationTarget.Effect);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        if (attackResult.PlayerResult is { } damagedPlayerResult)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    damagedPlayerResult,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Thundercloud PetAttack MsgAction " +
                $"{FormatSession(context)} petUid={petUid} targetUid={attackResult.AnimationTarget.Uid} " +
                $"targetKind={attackResult.TargetKind} damage={attackResult.AnimationTarget.Damage} " +
                $"nextAttackAtUtc={nextAttackAtUtc:O} thunderbolt={thunderboltActive}; " +
                "source=matching DecompiledSource MsgDataPacket.cs Data(447) PetAttack.")
            .ConfigureAwait(false);
    }

    private static int FindLegacyThundercloudPetIndex(
        IReadOnlyList<LivePetProjection> pets,
        uint petUid)
    {
        for (var index = 0; index < pets.Count; index++)
        {
            if (pets[index].Uid == petUid)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsOwnedVisibleLegacyThundercloud(
        CharacterSnapshot owner,
        LivePetProjection pet)
    {
        return IsLegacyThundercloudLivePet(pet) &&
            pet.MonsterId == LegacyThundercloudMonsterFamilyId &&
            pet.Hitpoints != 0 &&
            pet.X is { } petX &&
            pet.Y is { } petY &&
            LegacyGetDistance(owner.X, owner.Y, petX, petY) <= LegacyViewThreshold;
    }
}
