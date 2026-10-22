using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendVisiblePlayersAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        var character = session.Character;
        var sentCount = 0;
        var cloneSentCount = 0;
        var petSentCount = 0;
        var selfSpawn = CreatePlayerSpawnPacket(session, context.WorldData);
        var selfCloneSpawns = CreateShadowCloneSpawnPackets(session, context.WorldData);
        var selfPetSpawns = CreateLivePetSpawnPackets(session, context.ClientCompatibility);
        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == character.Uid ||
                !CanSeePlayer(character, active.Session.Character))
            {
                continue;
            }

            await SendLegacyTeleportFrameAsync(context, context.Connection,
                    CreatePlayerSpawnPacket(active.Session, context.WorldData).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            var activeCloneSpawns = CreateShadowCloneSpawnPackets(active.Session, context.WorldData);
            foreach (var cloneSpawn in activeCloneSpawns)
            {
                await SendLegacyTeleportFrameAsync(context, context.Connection, cloneSpawn.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }

            var activePetSpawns = CreateLivePetSpawnPackets(active.Session, context.ClientCompatibility);
            foreach (var petSpawn in activePetSpawns)
            {
                await SendLegacyTeleportFrameAsync(context, context.Connection, petSpawn.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }

            await SendLegacyTeleportFrameAsync(context, active.Connection, selfSpawn.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            foreach (var cloneSpawn in selfCloneSpawns)
            {
                await SendLegacyTeleportFrameAsync(context, active.Connection, cloneSpawn.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var petSpawn in selfPetSpawns)
            {
                await SendLegacyTeleportFrameAsync(context, active.Connection, petSpawn.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }

            sentCount++;
            cloneSentCount += activeCloneSpawns.Count + selfCloneSpawns.Count;
            petSentCount += activePetSpawns.Count + selfPetSpawns.Count;
        }

        if (sentCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent LegacyPlayerSpawn " +
                    $"{FormatSession(context)} map={character.MapId} playerCount={sentCount} cloneCount={cloneSentCount} petCount={petSentCount}.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask SendPlayerSpawnToVisiblePeersAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        var character = session.Character;
        var spawn = CreatePlayerSpawnPacket(session, context.WorldData).ToFrameBytes();
        var sentCount = 0;
        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == character.Uid ||
                !CanSeePlayer(active.Session.Character, character))
            {
                continue;
            }

            await SendLegacyTeleportFrameAsync(context, active.Connection, spawn, cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        if (sentCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer refreshed LegacyPlayerSpawn " +
                    $"{FormatSession(context)} map={character.MapId} playerCount={sentCount}.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask SendPlayerEquipmentAppearanceRefreshAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var equipmentRefresh = LegacyShowEquipmentPacket.CreateEquipmentRefresh(
            session.PlayerItems,
            session.AlternateEquipmentActive);
        await SendLegacyTeleportFrameAsync(context, context.Connection,
                equipmentRefresh.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyShowEquipment appearance refresh " +
                $"{FormatSession(context with { Session = session })} alternate={equipmentRefresh.Alternante} " +
                $"headUid={equipmentRefresh.EquipmentSlots.Head} armorUid={equipmentRefresh.EquipmentSlots.Armor} " +
                $"rightWeaponUid={equipmentRefresh.EquipmentSlots.RightWeapon} leftWeaponUid={equipmentRefresh.EquipmentSlots.LeftWeapon} " +
                $"garmentUid={equipmentRefresh.EquipmentSlots.Garment} wingUid={equipmentRefresh.EquipmentSlots.Wing}.")
            .ConfigureAwait(false);
        await SendPlayerSpawnToSelfAsync(context, session, cancellationToken).ConfigureAwait(false);
        await SendPlayerSpawnToVisiblePeersAsync(context, session, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask SendPlayerSpawnToSelfAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is null)
        {
            return;
        }

        await SendLegacyTeleportFrameAsync(context, context.Connection,
                CreatePlayerSpawnPacket(session, context.WorldData).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer refreshed local LegacyPlayerSpawn " +
                $"{FormatSession(context with { Session = session })} map={session.Character.MapId}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTeleportLeaveToVisiblePeersAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        await SendLegacyTeleportStatusCleanupFramesAsync(context, session, cancellationToken)
            .ConfigureAwait(false);

        var character = session.Character;
        var removeFrame = LegacyRemoveEntityPacket
            .FromEntity(CheckedUInt32(character.Uid, nameof(character.Uid)))
            .ToFrameBytes();
        var sentCount = 0;
        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == character.Uid ||
                !CanSeePlayer(active.Session.Character, character))
            {
                continue;
            }

            await SendLegacyTeleportFrameAsync(context, active.Connection, removeFrame, cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        if (sentCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent legacy teleport leave RemoveEntity " +
                    $"{FormatSession(context)} oldMap={character.MapId} playerCount={sentCount}; " +
                    "source=GameClient.Teleport -> Player.View.Clear(stream).")
                .ConfigureAwait(false);
        }
    }

    private static LegacyPlayerSpawnPacket CreatePlayerSpawnPacket(
        WorldSession session,
        WorldDataCatalog? worldData)
    {
        var currentHitpoints = CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            session.Character,
            currentHitpoints,
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            worldData,
            session.AlternateEquipmentActive);

        return LegacyPlayerSpawnPacket.FromSnapshot(
            session.Character,
            session.PlayerItems,
            alternateEquipmentActive: session.AlternateEquipmentActive,
            maxLife: maxHitpoints);
    }

    private static IReadOnlyList<LegacyPlayerSpawnPacket> CreateShadowCloneSpawnPackets(
        WorldSession session,
        WorldDataCatalog? worldData)
    {
        var cloneFlags = ReadShadowCloneFlags(session.Character);
        if (cloneFlags.Count == 0)
        {
            return [];
        }

        var currentHitpoints = CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            session.Character,
            currentHitpoints,
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            worldData,
            session.AlternateEquipmentActive);
        var packets = new LegacyPlayerSpawnPacket[cloneFlags.Count];
        for (var index = 0; index < cloneFlags.Count; index++)
        {
            packets[index] = LegacyPlayerSpawnPacket.FromShadowClone(
                session.Character,
                cloneUid: checked(LegacyShadowCloneCounterStart + (uint)index + 1U),
                cloneFlags[index],
                session.PlayerItems,
                alternateEquipmentActive: session.AlternateEquipmentActive,
                maxLife: maxHitpoints);
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMonsterSpawnPacket> CreateLivePetSpawnPackets(
        WorldSession session,
        LegacyClientCompatibilityOptions? clientCompatibility)
    {
        var pets = ReadLivePets(session.Character);
        if (pets.Count == 0)
        {
            return [];
        }

        var ownerX = CheckedUInt16(session.Character.X, nameof(session.Character.X));
        var ownerY = CheckedUInt16(session.Character.Y, nameof(session.Character.Y));
        var ownerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var ownerFacing = ReadAttributeUInt16(session.Character.Attributes, "Angle");
        var ownerGuildId = ReadAttributeUInt32(session.Character.Attributes, "GuildID", "GuildId");
        var packets = new LegacyMonsterSpawnPacket[pets.Count];
        for (var index = 0; index < pets.Count; index++)
        {
            var pet = pets[index];
            packets[index] = LegacyMonsterSpawnPacket.FromOwnerPet(
                pet.Uid,
                pet.Mesh,
                pet.Level,
                pet.Hitpoints,
                pet.MaxLife,
                pet.X ?? ownerX,
                pet.Y ?? ownerY,
                ownerUid,
                ownerFacing,
                ownerGuildId,
                pet.PetFlag,
                ownerPet1: 15,
                pet.MonsterId,
                pet.Name,
                clientCompatibility: clientCompatibility);
        }

        return packets;
    }

    private static bool CanSeePlayer(CharacterSnapshot character, CharacterSnapshot other)
    {
        return character.MapId == other.MapId &&
            character.MapId != LegacyAlienWorldMapId &&
            ReadAttributeUInt32(character.Attributes, "DynamicID", "DynamicId") ==
                ReadAttributeUInt32(other.Attributes, "DynamicID", "DynamicId") &&
            LegacyGetDistance(character.X, character.Y, other.X, other.Y) <= LegacyViewThreshold;
    }

    private static bool TryGetVisibleSessionByUid(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint uid,
        out WorldActiveSession activeSession)
    {
        if (TryGetSessionByUid(context, uid, out activeSession) &&
            !ReferenceEquals(activeSession.Connection, context.Connection) &&
            CanSeePlayer(session.Character, activeSession.Session.Character))
        {
            return true;
        }

        activeSession = default!;
        return false;
    }

    private static bool TryGetMovementInteractionPartner(
        WorldPacketHandlerContext context,
        WorldSession session,
        out WorldActiveSession activeSession)
    {
        if (session.InteractionPartnerUid is { } partnerUid &&
            TryGetSessionByUid(context, partnerUid, out activeSession) &&
            activeSession.Session.InteractionPartnerUid == CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)))
        {
            return true;
        }

        activeSession = default!;
        return false;
    }

    private static bool TryGetSessionByUid(
        WorldPacketHandlerContext context,
        uint uid,
        out WorldActiveSession activeSession)
    {
        if (context.Sessions is { } sessions)
        {
            foreach (var active in sessions.Snapshot())
            {
                if (active.Session.Character.Uid == uid)
                {
                    activeSession = active;
                    return true;
                }
            }
        }

        activeSession = default!;
        return false;
    }
}
