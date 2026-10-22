using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyFirstD13MonsterUid = 400_000;
    private const uint LegacyLastD13MonsterUid = 999_999;
    private const uint LegacyLottusMonsterSettingsFlag = 2_048;
    private const uint LegacyReviverMonsterSettingsFlag = 0x62;
    private const uint LegacyGuardMonsterSettingsFlag = 0xA4;

    private static async ValueTask SendVisibleMonstersAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken,
        CharacterSnapshot? previousCharacter = null)
    {
        var sendOnlyNewlyVisible = previousCharacter is not null;
        if (character.MapId is < int.MinValue or > int.MaxValue)
        {
            if (!sendOnlyNewlyVisible)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP skipped LegacyMonsterSpawn visibility " +
                        $"{FormatSession(context)}; map id is out of Int32 range.")
                    .ConfigureAwait(false);
            }

            return;
        }

        if (context.WorldData is not { } worldData)
        {
            if (!sendOnlyNewlyVisible)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP skipped LegacyMonsterSpawn visibility " +
                        $"{FormatSession(context)}; world data is not loaded.")
                    .ConfigureAwait(false);
            }

            return;
        }

        if (!worldData.MonsterSpawnsByMapId.TryGetValue((int)character.MapId, out var spawns))
        {
            if (!sendOnlyNewlyVisible)
            {
                if (IsLegacyInstanceInfoDynamicMap(character))
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped normal LegacyMonsterSpawn visibility " +
                            $"{FormatSession(context)} map={character.MapId} dynamicId={ReadCharacterDynamicId(character)}; " +
                            "source LegacyInstance.Info sends its custom AddMapMonster spawn separately.")
                        .ConfigureAwait(false);
                }
                else
                {
                    await context.Log.WriteLineAsync(
                            "PARITY-GAP no LegacyMonsterSpawn rows loaded " +
                            $"{FormatSession(context)} map={character.MapId}.")
                        .ConfigureAwait(false);
                }
            }

            return;
        }

        var sentCount = 0;
        var totalInstances = 0;
        var outOfViewCount = 0;
        var missingFamilyCount = spawns.Count(spawn => !worldData.MonstersById.ContainsKey(spawn.MonsterTypeId));
        var runtimeHiddenCount = 0;
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in LegacyStaticMonsterMaterializer.Enumerate(worldData, spawns))
        {
            totalInstances++;

            var activeInstance = entry.Instance;
            if (context.MonsterStates is { } monsterStates)
            {
                if (!monsterStates.TryGetVisibleInstance(entry.Instance, now, out var visibleInstance))
                {
                    runtimeHiddenCount++;
                    continue;
                }

                activeInstance = visibleInstance;
            }

            if (!CanSeeMonster(character, activeInstance))
            {
                outOfViewCount++;
                continue;
            }

            if (previousCharacter is { } previous &&
                CanSeeMonster(previous, activeInstance))
            {
                continue;
            }

            var statusFlags = context.MonsterStates?.CreateStatusFlagVector(
                activeInstance,
                now,
                LegacyMonsterSpawnPacket.StatusFlagVectorLength);
            var packet = activeInstance.ToSpawnPacket(
                clientCompatibility: context.ClientCompatibility,
                statusFlags: statusFlags);
            await SendLegacyTeleportFrameAsync(context, context.Connection, packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(activeInstance))
            {
                await SendLegacyTeleportFrameAsync(context, context.Connection, update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }

            if (context.MonsterStates?.IsBlackSpotted(activeInstance, now) == true)
            {
                await SendLegacyTeleportFrameAsync(context, context.Connection,
                        LegacyBlackspotPacket.CreateActive(activeInstance.Uid).ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            sentCount++;
        }

        if (sentCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent LegacyMonsterSpawn " +
                    $"{FormatSession(context)} map={character.MapId} monsterCount={sentCount}.")
                .ConfigureAwait(false);
        }
        else
        {
            if (!sendOnlyNewlyVisible)
            {
                if (IsLegacyInstanceInfoDynamicMap(character))
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped normal LegacyMonsterSpawn visibility " +
                            $"{FormatSession(context)} map={character.MapId} dynamicId={ReadCharacterDynamicId(character)} " +
                            $"spawnRows={spawns.Count} instances={totalInstances} outOfView={outOfViewCount} " +
                            $"runtimeHidden={runtimeHiddenCount} missingMonsterFamilies={missingFamilyCount}; " +
                            "source LegacyInstance.Info sends its custom AddMapMonster spawn separately.")
                        .ConfigureAwait(false);
                }
                else
                {
                    await context.Log.WriteLineAsync(
                            "PARITY-GAP no visible LegacyMonsterSpawn " +
                            $"{FormatSession(context)} map={character.MapId} spawnRows={spawns.Count} " +
                            $"instances={totalInstances} outOfView={outOfViewCount} " +
                            $"runtimeHidden={runtimeHiddenCount} missingMonsterFamilies={missingFamilyCount}.")
                        .ConfigureAwait(false);
                }
            }
        }
    }

    private static bool CanSeeMonster(CharacterSnapshot character, LegacyMonsterInstance monster)
    {
        return character.MapId == monster.MapId &&
            ReadCharacterDynamicId(character) == monster.DynamicId &&
            LegacyGetDistance(character.X, character.Y, monster.X, monster.Y) <= LegacyViewThreshold;
    }

    private static ulong ReadCharacterDynamicId(CharacterSnapshot character)
    {
        return ReadAttributeUInt64(character.Attributes, "DynamicID", "DynamicId");
    }

    private static bool TryFindMonsterInstanceByUid(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        uint uid,
        out LegacyMonsterInstance monsterInstance)
    {
        return TryFindMonsterInstanceByUid(context, character, uid, out monsterInstance, out _);
    }

    private static bool TryFindMonsterInstanceByUid(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        uint uid,
        out LegacyMonsterInstance monsterInstance,
        out Monster monsterFamily)
    {
        monsterInstance = default!;
        monsterFamily = default!;

        if (character.MapId is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        var mapId = (int)character.MapId;
        if (context.MonsterStates is { } monsterStates &&
            monsterStates.TryGetDynamicMonsterByUid(
                mapId,
                ReadCharacterDynamicId(character),
                uid,
                out monsterInstance,
                out monsterFamily))
        {
            return true;
        }

        if (context.WorldData is not { } worldData ||
            !worldData.MonsterSpawnsByMapId.TryGetValue(mapId, out var spawns))
        {
            return false;
        }

        foreach (var entry in LegacyStaticMonsterMaterializer.Enumerate(worldData, spawns))
        {
            if (entry.Instance.Uid != uid)
            {
                continue;
            }

            monsterInstance = entry.Instance;
            monsterFamily = entry.MonsterFamily;
            return true;
        }

        return false;
    }

    private static bool TryFindVisibleMonsterInstanceByUid(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        uint uid,
        out LegacyMonsterInstance monsterInstance)
    {
        monsterInstance = default!;
        if (!TryFindMonsterInstanceByUid(context, character, uid, out var candidate))
        {
            return false;
        }

        var visible = candidate;
        if (context.MonsterStates is { } monsterStates)
        {
            if (!monsterStates.TryGetVisibleInstance(candidate, DateTimeOffset.UtcNow, out visible))
            {
                return false;
            }
        }

        if (!CanSeeMonster(character, visible))
        {
            return false;
        }

        monsterInstance = visible;
        return true;
    }

    private static bool CanLegacyPlayerAttackMonster(
        CharacterSnapshot character,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        out string failureDiagnostic,
        bool enforceD13UidRange = false)
    {
        failureDiagnostic = string.Empty;

        if (enforceD13UidRange && !IsLegacyD13MonsterUid(monster.Uid))
        {
            failureDiagnostic =
                $"D13 client monster identity rejected uid={monster.Uid} " +
                $"expectedRange={LegacyFirstD13MonsterUid}..{LegacyLastD13MonsterUid}; " +
                "matching source MobCollection.GenerateUid starts at 400000 and exact D13 classifies high UIDs as PK targets";
            return false;
        }

        if (monster.Hitpoints == 0)
        {
            failureDiagnostic = "source CanAttackMonster.Verified rejected target because monster is not alive";
            return false;
        }

        if (IsLegacyOwnedLottusMonster(character, monster, monsterFamily))
        {
            failureDiagnostic = "source CanAttackMonster.Verified rejected owned Lottus floor monster";
            return false;
        }

        if (IsLegacySourceBlockedMonsterFamily(monsterFamily))
        {
            failureDiagnostic = "source CanAttackMonster.Verified rejected source-blocked monster family id 5385";
            return false;
        }

        if (IsLegacyReviverMonsterFamily(monsterFamily))
        {
            failureDiagnostic =
                "source CanAttackMonster.Verified rejected Reviver monster family " +
                $"settings={ReadLegacyMonsterSettings(monsterFamily)}";
            return false;
        }

        if (HasLegacyGmOrPmMonsterName(monster, monsterFamily))
        {
            failureDiagnostic = "source CanAttackMonster.Verified rejected GM/PM monster name";
            return false;
        }

        if (TryGetLegacyGuardMonsterFamilySource(monsterFamily, out var guardSettings, out var guardSource))
        {
            var pkMode = ReadAttributeUInt64(character.Attributes, RuntimePkModeAttribute, "PKMode") & byte.MaxValue;
            if (pkMode != LegacyPkModePk)
            {
                failureDiagnostic =
                    $"source CanAttackMonster.Verified rejected Guard monster family because pkMode={pkMode} " +
                    $"familyId={monsterFamily.Id} familyName={monsterFamily.Name} monsterName={monster.Name} " +
                    $"settings={guardSettings} guardSource={guardSource}";
                return false;
            }
        }

        return true;
    }

    private static bool IsLegacyD13MonsterUid(uint uid)
    {
        return uid is >= LegacyFirstD13MonsterUid and <= LegacyLastD13MonsterUid;
    }

    private static bool IsLegacySourceBlockedMonsterFamily(Monster monsterFamily)
    {
        return monsterFamily.Id == 5_385;
    }

    private static bool IsLegacyOwnedLottusMonster(
        CharacterSnapshot character,
        LegacyMonsterInstance monster,
        Monster monsterFamily)
    {
        if ((ReadLegacyMonsterSettings(monsterFamily) & LegacyLottusMonsterSettingsFlag) != LegacyLottusMonsterSettingsFlag)
        {
            return false;
        }

        var ownerUid = monster.RuntimeOwnerUid;
        if (ownerUid == 0)
        {
            ownerUid = ReadAttributeUInt32(
                monsterFamily.Attributes,
                "RuntimeOwnerUid",
                "OwnerFloorUid",
                "OwnerUID",
                "OwnerUid",
                "ItemOwnerUID",
                "ItemOwnerUid");
        }

        return ownerUid != 0 &&
            character.Uid >= 0 &&
            ownerUid == CheckedUInt32(character.Uid, nameof(character.Uid));
    }

    private static uint ReadLegacyMonsterSettings(Monster monsterFamily)
    {
        return ReadAttributeUInt32(monsterFamily.Attributes, "settings", "Settings");
    }

    private static async ValueTask<WorldSession> TryApplyLegacyGuardAttackFlashingNameAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (HasLegacyStatusFlag(session.Character, LegacyFlashingNameStatusFlag))
        {
            return session;
        }

        var updatedSession = session with
        {
            Character = WithLegacyStatusFlag(session.Character, LegacyFlashingNameStatusFlag),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy guard attack flashing-name " +
                $"{FormatSession(updatedContext)} statusFlag={LegacyFlashingNameStatusFlag}; " +
                "source=CanAttackMonster.Verified Guard AddFlag(FlashingName, 30, RemoveOnDead: true).")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool IsLegacyReviverMonsterFamily(Monster monsterFamily)
    {
        return (ReadLegacyMonsterSettings(monsterFamily) & LegacyReviverMonsterSettingsFlag) ==
            LegacyReviverMonsterSettingsFlag ||
            monsterFamily.Name.Contains("Reviver", StringComparison.Ordinal);
    }

    private static bool IsLegacyGuardMonsterFamily(LegacyMonsterInstance monster, Monster monsterFamily)
    {
        return TryGetLegacyGuardMonsterFamilySource(monsterFamily, out _, out _);
    }

    private static bool TryGetLegacyGuardMonsterFamilySource(
        Monster monsterFamily,
        out uint settings,
        out string source)
    {
        settings = ReadLegacyMonsterSettings(monsterFamily);
        if ((settings & LegacyGuardMonsterSettingsFlag) == LegacyGuardMonsterSettingsFlag)
        {
            source = "family.settings";
            return true;
        }

        if (IsLegacyGuardMonsterName(monsterFamily.Name))
        {
            source = "MonsterFamily.CreateMonsterSettings name rule";
            return true;
        }

        source = string.Empty;
        return false;
    }

    private static bool IsLegacyGuardMonsterName(string name)
    {
        return (name.Contains("Guard", StringComparison.Ordinal) &&
                !name.Contains("Chaos", StringComparison.Ordinal)) ||
            name.Contains("CitySoldier", StringComparison.Ordinal);
    }

    private static bool HasLegacyGmOrPmMonsterName(LegacyMonsterInstance monster, Monster monsterFamily)
    {
        return HasLegacyGmOrPmMarker(monster.Name) || HasLegacyGmOrPmMarker(monsterFamily.Name);
    }

    private static bool HasLegacyGmOrPmMarker(string name)
    {
        return name.Contains("[GM]", StringComparison.Ordinal) ||
            name.Contains("[PM]", StringComparison.Ordinal);
    }

    private static long LegacyGetDistance(long x, long y, long x2, long y2)
    {
        return Math.Max(Math.Abs(x - x2), Math.Abs(y - y2));
    }
}
