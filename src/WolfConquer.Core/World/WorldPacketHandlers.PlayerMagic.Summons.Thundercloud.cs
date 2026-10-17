using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static void RegisterLegacyThundercloudFloorRuntimeEffects(
        WorldPacketHandlerContext context,
        WorldSession session,
        IReadOnlyList<LivePetProjection> pets)
    {
        if (context.FloorItems is not { } floorItems ||
            pets.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var ownerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var guildId = ReadAttributeUInt32(session.Character.Attributes, "GuildID", "GuildId");
        var ownerX = CheckedUInt16(session.Character.X, nameof(session.Character.X));
        var ownerY = CheckedUInt16(session.Character.Y, nameof(session.Character.Y));
        foreach (var pet in pets)
        {
            var petX = pet.X ?? ownerX;
            var petY = pet.Y ?? ownerY;
            var packet = new LegacyFloorItemPacket(
                pet.Uid,
                LegacyThundercloudFloorItemId,
                petX,
                petY,
                MaxLife: 2,
                LegacyFloorItemDropType.Effect,
                Life: pet.MaxLife,
                PlusOrColor: 13,
                ownerUid,
                guildId,
                FlowerType: 2,
                Timer: 0,
                ownerX,
                ownerY,
                pet.Name);
            var effect = new FloorItemRuntimeEffect(
                LegacyThundercloudRuntimeSpellId,
                petX,
                petY,
                now.AddMilliseconds(LegacyThundercloudPulseMilliseconds),
                Distance: LegacyThundercloudRandCoordRadius,
                HitRadius: LegacyThundercloudRandCoordRadius,
                ExpiresAt: now.AddMilliseconds(LegacyThundercloudLifetimeMilliseconds),
                RepeatMilliseconds: LegacyThundercloudPulseMilliseconds);
            floorItems.AddVisible(FloorItemRuntimeEntry.ForEffect(session.Character.MapId, packet, effect), now);
        }
    }

    internal static async ValueTask<int> SendDueLegacyThundercloudFloorSpellsAsync(
        WorldPacketHandlerContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.FloorItems is not { } floorItems ||
            context.Sessions is not { } sessions)
        {
            return 0;
        }

        var activeSessions = sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return 0;
        }

        var due = floorItems.ConsumeDueRepeatingEffects(LegacyThundercloudRuntimeSpellId, now);
        if (due.Invoked.Count == 0 && due.Expired.Count == 0)
        {
            return 0;
        }

        var removeViewerCount = 0;
        foreach (var entry in due.Expired)
        {
            removeViewerCount += await SendLegacyThundercloudRemoveAsync(
                    entry,
                    activeSessions,
                    cancellationToken)
                .ConfigureAwait(false);
            RemoveLegacyLivePetProjection(sessions, activeSessions, entry.Packet.ItemOwnerUid, entry.Packet.Uid);
        }

        if (due.Expired.Count > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy Thundercloud floor runtime " +
                    $"pulseCount={due.Invoked.Count} expiredCount={due.Expired.Count} " +
                    $"removeViewerCount={removeViewerCount}; source=PoolProcesses floor m_ID 3843 RemoveFloor action 135.")
                .ConfigureAwait(false);
        }

        return due.Invoked.Count + due.Expired.Count;
    }

    private static async ValueTask<int> SendLegacyThundercloudRemoveAsync(
        FloorItemRuntimeEntry entry,
        IReadOnlyList<WorldActiveSession> activeSessions,
        CancellationToken cancellationToken)
    {
        return await SendLegacyThundercloudRemoveFrameAsync(
                entry.MapId,
                entry.Packet.X,
                entry.Packet.Y,
                entry.Packet.Uid,
                activeSessions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<int> SendLegacyThundercloudRemoveFrameAsync(
        long mapId,
        ushort x,
        ushort y,
        uint uid,
        IReadOnlyList<WorldActiveSession> activeSessions,
        CancellationToken cancellationToken)
    {
        if (mapId == 0)
        {
            return 0;
        }

        var frame = LegacyRemoveEntityPacket.FromEntity(uid).ToFrameBytes();
        var sentCount = 0;
        foreach (var active in activeSessions)
        {
            if (!CanSeeFloorItem(active.Session.Character, mapId, x, y))
            {
                continue;
            }

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        return sentCount;
    }

    private static async ValueTask<WorldSession> SendLegacyOwnedThundercloudDeathCleanupAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection ownerConnection,
        WorldSession ownerSession,
        CancellationToken cancellationToken)
    {
        var pets = ReadLivePets(ownerSession.Character);
        if (pets.Count == 0)
        {
            return ownerSession;
        }

        var thunderclouds = pets
            .Where(IsLegacyThundercloudLivePet)
            .ToArray();
        if (thunderclouds.Length == 0)
        {
            return ownerSession;
        }

        IReadOnlyList<WorldActiveSession> activeSessions = context.Sessions?.Snapshot() ??
            [new WorldActiveSession(ownerConnection, ownerSession)];
        var removeViewerCount = 0;
        foreach (var pet in thunderclouds)
        {
            if (context.FloorItems is { } floorItems)
            {
                floorItems.TryRemoveVisible(pet.Uid, out _);
            }

            var x = pet.X ?? CheckedUInt16(ownerSession.Character.X, nameof(ownerSession.Character.X));
            var y = pet.Y ?? CheckedUInt16(ownerSession.Character.Y, nameof(ownerSession.Character.Y));
            removeViewerCount += await SendLegacyThundercloudRemoveFrameAsync(
                    ownerSession.Character.MapId,
                    x,
                    y,
                    pet.Uid,
                    activeSessions,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var remaining = pets
            .Where(pet => !IsLegacyThundercloudLivePet(pet))
            .ToArray();
        var updatedSession = ownerSession with
        {
            Character = WithLegacyLivePetProjections(ownerSession.Character, remaining),
        };
        context.Sessions?.Set(ownerConnection, updatedSession);
        await context.Log.WriteLineAsync(
                "WorldServer removed legacy Thundercloud floor projections on owner death " +
                $"{FormatSession(context)} ownerUid={ownerSession.Character.Uid} " +
                $"removedCount={thunderclouds.Length} removeViewerCount={removeViewerCount}; " +
                "source=Player.cs death Thundercloud action 135 cleanup.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool IsLegacyThundercloudLivePet(LivePetProjection pet)
    {
        return pet.MonsterId == 999_996_641U ||
            string.Equals(pet.Name, "Thundercloud", StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveLegacyLivePetProjection(
        WorldSessionStore sessions,
        IReadOnlyList<WorldActiveSession> activeSessions,
        uint ownerUid,
        uint petUid)
    {
        foreach (var active in activeSessions)
        {
            if (active.Session.Character.Uid != ownerUid)
            {
                continue;
            }

            var ownerSession = active.Session;
            if (sessions.TryGet(active.Connection, out var currentSession) &&
                currentSession is not null)
            {
                ownerSession = currentSession;
            }

            var pets = ReadLivePets(ownerSession.Character);
            if (pets.Count == 0)
            {
                return;
            }

            var remaining = pets.Where(pet => pet.Uid != petUid).ToArray();
            if (remaining.Length == pets.Count)
            {
                return;
            }

            sessions.Set(
                active.Connection,
                ownerSession with
                {
                    Character = WithLegacyLivePetProjections(ownerSession.Character, remaining),
                });
            return;
        }
    }

    private static IReadOnlyList<LivePetProjection> CreateLegacyThundercloudPetProjections(
        WorldSession session,
        LegacyMagicSupportOutcome outcome,
        ushort x,
        ushort y,
        IReadOnlyList<LivePetProjection> existingPets,
        LegacyGameMapTerrainQuery? terrain)
    {
        var pets = new List<LivePetProjection>();
        var occupied = existingPets
            .Where(pet => pet.X.HasValue && pet.Y.HasValue)
            .Select(pet => (pet.X!.Value, pet.Y!.Value))
            .ToHashSet();
        var existingPetCount = existingPets.Count;
        foreach (var summon in outcome.Summons)
        {
            if (summon.Kind != LegacyMagicSupportKind.Thundercloud)
            {
                continue;
            }

            var nextX = x;
            var nextY = y;
            for (var index = 0; index < summon.Count; index++)
            {
                var uid = checked(LegacyLivePetCounterStart + (uint)existingPetCount + (uint)pets.Count + 1U);
                var petX = nextX;
                var petY = nextY;
                pets.Add(new LivePetProjection(
                    uid,
                    summon.Mesh,
                    summon.PetFlag,
                    summon.MonsterFamilyId,
                    Level: 140,
                    Hitpoints: 14,
                    MaxLife: 14,
                    summon.Name,
                    petX,
                    petY));
                occupied.Add((petX, petY));
                if (index + 1 < summon.Count)
                {
                    (nextX, nextY) = SelectLegacyThundercloudRandCoord(
                        session,
                        petX,
                        petY,
                        existingPetCount + pets.Count,
                        occupied,
                        terrain);
                }
            }
        }

        return pets;
    }

    private static bool HasLegacyDoubleThunderRuneEquipped(WorldSession session, WorldDataCatalog? worldData)
    {
        if (TryReadAttributeBool(
                session.Character.Attributes,
                out var forced,
                "DoubleThunder",
                "HasDoubleThunder",
                "DoubleThunderRune",
                "DoubleThunderRuneEquipped",
                "LegacyDoubleThunder"))
        {
            return forced;
        }

        foreach (var rune in session.PlayerItems.RuneItems)
        {
            if (!IsLegacyEquippedRunePosition(rune.Position) ||
                !TryGetLegacyItemType(worldData?.ItemsById, rune.ItemId, out var itemType) ||
                !ContainsLegacyItemText(itemType, "DoubleThunder"))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static (ushort X, ushort Y) SelectLegacyThundercloudRandCoord(
        WorldSession session,
        ushort baseX,
        ushort baseY,
        int ordinal,
        ISet<(ushort X, ushort Y)> occupied,
        LegacyGameMapTerrainQuery? terrain)
    {
        var hasTerrain = false;
        var mapId = 0;
        if (terrain is not null &&
            session.Character.MapId >= int.MinValue &&
            session.Character.MapId <= int.MaxValue)
        {
            mapId = (int)session.Character.MapId;
            hasTerrain = terrain.TryGetTerrain(mapId, out _);
        }

        var seed = unchecked(
            ((uint)session.Character.Uid * 16_777_619U) ^
            ((uint)baseX << 16) ^
            baseY ^
            ((uint)ordinal * 2_654_435_761U));
        var offsets = LegacyThundercloudRandCoordOffsets;
        var start = offsets.Length == 0 ? 0 : (int)(seed % (uint)offsets.Length);
        for (var attempt = 0; attempt < offsets.Length; attempt++)
        {
            var offset = offsets[(start + attempt) % offsets.Length];
            var candidateX = (long)baseX + offset.X;
            var candidateY = (long)baseY + offset.Y;
            if (candidateX is < ushort.MinValue or > ushort.MaxValue ||
                candidateY is < ushort.MinValue or > ushort.MaxValue)
            {
                continue;
            }

            var candidate = ((ushort)candidateX, (ushort)candidateY);
            if (occupied.Contains(candidate))
            {
                continue;
            }

            if (hasTerrain && !terrain!.IsWalkable(mapId, candidate.Item1, candidate.Item2))
            {
                continue;
            }

            return candidate;
        }

        return (baseX, baseY);
    }

    private static (int X, int Y)[] CreateLegacyThundercloudRandCoordOffsets()
    {
        var offsets = new List<(int X, int Y)>();
        for (var distance = 1; distance <= LegacyThundercloudRandCoordRadius; distance++)
        {
            for (var x = -distance; x <= distance; x++)
            {
                for (var y = -distance; y <= distance; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) == distance)
                    {
                        offsets.Add((x, y));
                    }
                }
            }
        }

        return offsets.ToArray();
    }

    private static LegacyMonsterSpawnPacket CreateLegacyLivePetSpawnPacket(
        WorldSession session,
        LivePetProjection pet,
        LegacyClientCompatibilityOptions? clientCompatibility)
    {
        var ownerX = CheckedUInt16(session.Character.X, nameof(session.Character.X));
        var ownerY = CheckedUInt16(session.Character.Y, nameof(session.Character.Y));
        return LegacyMonsterSpawnPacket.FromOwnerPet(
            pet.Uid,
            pet.Mesh,
            pet.Level,
            pet.Hitpoints,
            pet.MaxLife,
            pet.X ?? ownerX,
            pet.Y ?? ownerY,
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            ReadAttributeUInt16(session.Character.Attributes, "Angle"),
            ReadAttributeUInt32(session.Character.Attributes, "GuildID", "GuildId"),
            pet.PetFlag,
            ownerPet1: 15,
            pet.MonsterId,
            pet.Name,
            clientCompatibility: clientCompatibility);
    }

    private static LegacyPetInfoPacket CreateLegacyPetInfoPacket(
        WorldSession session,
        LivePetProjection pet)
    {
        var ownerX = CheckedUInt16(session.Character.X, nameof(session.Character.X));
        var ownerY = CheckedUInt16(session.Character.Y, nameof(session.Character.Y));
        return new LegacyPetInfoPacket(
            pet.Uid,
            PetId: 4_264,
            PetType: pet.PetFlag,
            pet.Mesh,
            Range: 14,
            pet.X ?? ownerX,
            pet.Y ?? ownerY,
            pet.Name);
    }
}
