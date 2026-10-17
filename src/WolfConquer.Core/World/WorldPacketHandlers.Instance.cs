using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyInstanceInfoMapId = 10_429;
    private const long LegacyInstanceInfoFallbackX = 61;
    private const long LegacyInstanceInfoFallbackY = 66;
    private const long LegacyInstanceInfoSourceMapId = 10_428;
    private const uint LegacyInstanceInfoMonsterId = 3_546;
    private const uint LegacyInstanceInfoMonsterMesh = 171;
    private const byte LegacyInstanceInfoMonsterLevel = 140;
    private const uint LegacyInstanceInfoMonsterLife = 40_000_000;
    private const string LegacyInstanceInfoMonsterName = "FlameDevil";
    private const long LegacyInstanceQuitX = 410;
    private const long LegacyInstanceQuitY = 354;
    private const string LegacySkyTowerActiveChallengeAttribute = "SkyTowerActiveChallengeId";
    private const string LegacySkyTowerActiveCompletionAttribute = "SkyTowerActiveCompletionId";
    private const string LegacySkyTowerCompletedChallengeAttribute = "SkyTowerCompletedChallengeId";
    private const string LegacySkyTowerFloorAttribute = "SkyTowerFloor";
    private const string LegacySkyTowerAvailableAttemptsAttribute = "SkyTowerAvailableAttempts";
    private const uint LegacySkyTowerDefaultAvailableAttempts = 10;
    private static readonly (short X, short Y)[] s_legacySkyTowerSpawnOffsets =
    [
        (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (-1, 1), (1, -1), (-1, -1), (2, 0),
        (-2, 0), (0, 2), (0, -2), (2, 1), (-2, 1),
        (2, -1), (-2, -1), (1, 2), (-1, 2), (1, -2),
        (-1, -2), (2, 2), (-2, 2), (2, -2), (-2, -2),
    ];
    private static long s_legacyInstanceDynamicId = 10_428_000;

    private static async ValueTask HandleVisibleDx8D13InstanceInfoAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (context.ProtocolProfile != ConquerProtocolProfile.VisibleDx8D13)
        {
            await context.Log.WriteLineAsync(
                    "PROTOCOL-COLLISION ignored CMsgInstanceInfo " +
                    $"{FormatSession(context)} opcode={frame.Header.Opcode} profile={context.ProtocolProfile}; " +
                    "opcode 2382 is only verified for the exact VisibleDx8D13 client.")
                .ConfigureAwait(false);
            return;
        }

        if (!VisibleDx8D13InstanceInfoPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    "PROTOCOL-MALFORMED consumed VisibleDx8D13 CMsgInstanceInfo " +
                    $"{FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type != (uint)VisibleDx8D13InstanceInfoType.AvailableAttempts ||
            packet.Completions.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WIRE-EVIDENCE VisibleDx8D13 CMsgInstanceInfo query " +
                    $"{FormatSession(context)} type={packet.Type} completionCount={packet.Completions.Count} mutation=none.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped VisibleDx8D13 CMsgInstanceInfo attempt query; " +
                    "no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var query = packet.Completions[0];
        var completedChallengeId = ReadAttributeUInt32(
            session.Character.Attributes,
            LegacySkyTowerCompletedChallengeAttribute);
        if (LegacySkyTowerCatalog.TryResolve(completedChallengeId, out _))
        {
            await context.Connection.SendAsync(
                    VisibleDx8D13InstanceInfoPacket.CreateUserInstanceProgress(
                            completedChallengeId,
                            completed: true)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var availableAttempts = ReadLegacySkyTowerAvailableAttempts(session.Character.Attributes);
        var response = VisibleDx8D13InstanceInfoPacket.CreateAvailableAttempts(
            query.ChallengeId,
            availableAttempts);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer answered VisibleDx8D13 CMsgInstanceInfo attempt query " +
                $"{FormatSession(context)} challengeId={query.ChallengeId} " +
                $"availableAttempts={availableAttempts} completedChallengeId={completedChallengeId} " +
                "opcode=2382 type=1; D13 stores field5 field2 as the available challenge count.")
            .ConfigureAwait(false);
    }

    [PacketHandler(WorldPacketOpcodes.LegacyInstance)]
    private static async ValueTask HandleLegacyInstanceAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyInstancePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyInstance packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        var actionName = packet.Action?.ToString() ?? packet.Type.ToString(CultureInfo.InvariantCulture);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyInstance " +
                $"{FormatSession(context)} type={actionName} typeId={packet.Type} " +
                $"weather={packet.Weather} userId={packet.UserId}.")
            .ConfigureAwait(false);

        if (!packet.TryGetAction(out var action))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP unmodeled LegacyInstance " +
                    $"{FormatSession(context)} type={packet.Type}; source logs unknown CMsgInstance type; source no-op fallback in this lane.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyInstance {FormatSession(context)} type={action}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (action == LegacyInstanceAction.Info)
        {
            await HandleLegacyInstanceInfoAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        await HandleLegacyInstanceQuitAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyInstanceInfoAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyInstancePacket packet,
        CancellationToken cancellationToken)
    {
        var source = session.Character;
        var isSkyTowerChallenge = LegacySkyTowerCatalog.TryResolve(packet.InstanceId, out var skyTowerChallenge);
        var activeChallengeId = ReadAttributeUInt32(
            source.Attributes,
            LegacySkyTowerActiveChallengeAttribute);
        var activeCompletionId = ReadAttributeUInt32(
            source.Attributes,
            LegacySkyTowerActiveCompletionAttribute);
        var completedChallengeId = ReadAttributeUInt32(
            source.Attributes,
            LegacySkyTowerCompletedChallengeAttribute);
        var availableAttempts = ReadLegacySkyTowerAvailableAttempts(source.Attributes);

        if (isSkyTowerChallenge &&
            LegacySkyTowerCatalog.TryResolve(completedChallengeId, out var completedChallenge) &&
            completedChallenge.Floor >= skyTowerChallenge.Floor)
        {
            await context.Connection.SendAsync(
                    VisibleDx8D13InstanceInfoPacket.CreateUserInstanceProgress(
                            completedChallengeId,
                            completed: true)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    VisibleDx8D13InstanceInfoPacket.CreateAvailableAttempts(
                            LegacySkyTowerCatalog.RootChallengeId,
                            availableAttempts)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer rejected completed Sky Tower challenge replay " +
                    $"{FormatSession(context)} challengeId={packet.InstanceId} floor={skyTowerChallenge.Floor} " +
                    $"completedChallengeId={completedChallengeId} completedFloor={completedChallenge.Floor}; " +
                    "D13 STR_INSTANCE_RULE_TIP_2 forbids replaying completed floors.")
                .ConfigureAwait(false);
            return;
        }

        if (isSkyTowerChallenge && availableAttempts == 0)
        {
            await context.Connection.SendAsync(
                    VisibleDx8D13InstanceInfoPacket.CreateAvailableAttempts(
                            LegacySkyTowerCatalog.RootChallengeId,
                            availableAttempts)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer rejected Sky Tower challenge with no available attempts " +
                    $"{FormatSession(context)} challengeId={packet.InstanceId} floor={skyTowerChallenge.Floor}; " +
                    "D13 STR_INSTANCE_RULE_TIP_2 requires a remaining challenge attempt.")
                .ConfigureAwait(false);
            return;
        }

        if (IsLegacyInstanceInfoDynamicMap(source) &&
            (!isSkyTowerChallenge ||
             (activeChallengeId == packet.InstanceId && activeCompletionId != packet.InstanceId)))
        {
            await ReplayLegacyInstanceInfoAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        var dynamicId = Interlocked.Increment(ref s_legacyInstanceDynamicId);
        var teleportRequest = new LegacyTeleportRequest(
            "CMsgInstance.Process3 Info -> GameClient.Teleport",
            LegacyInstanceInfoMapId,
            dynamicId);
        var preflight = await PrepareLegacyTeleportAsync(
                context,
                session,
                teleportRequest,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return;
        }

        session = preflight.Session;
        source = session.Character;

        if (IsLegacyInstanceInfoDynamicMap(source) &&
            context.MonsterStates is { } existingMonsterStates)
        {
            existingMonsterStates.RemoveDynamicMonsters(
                CheckedInt32(LegacyInstanceInfoMapId, nameof(LegacyInstanceInfoMapId)),
                ReadCharacterDynamicId(source));
        }

        if (isSkyTowerChallenge)
        {
            var attributes = CopyCharacterAttributes(source.Attributes);
            attributes[LegacySkyTowerActiveChallengeAttribute] =
                packet.InstanceId.ToString(CultureInfo.InvariantCulture);
            attributes[LegacySkyTowerFloorAttribute] =
                skyTowerChallenge.Floor.ToString(CultureInfo.InvariantCulture);
            attributes[LegacySkyTowerAvailableAttemptsAttribute] =
                availableAttempts.ToString(CultureInfo.InvariantCulture);
            attributes.Remove(LegacySkyTowerActiveCompletionAttribute);
            source = source with { Attributes = attributes };
        }

        var preparedSession = session with { Character = source };
        var updated = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = preparedSession },
                preparedSession,
                new LegacyTeleportDestination(
                    teleportRequest.RequestedMapId,
                    LegacyInstanceInfoFallbackX,
                    LegacyInstanceInfoFallbackY,
                    checked((ulong)teleportRequest.RequestedDynamicId)),
                timestamp: 0,
                cancellationToken,
                expectedSession: session)
            .ConfigureAwait(false);
        if (updated is null)
        {
            return;
        }

        var updatedContext = context with { Session = updated };

        var monsters = ResolveLegacyInstanceInfoMonsters(
            updatedContext,
            dynamicId,
            isSkyTowerChallenge ? skyTowerChallenge : null);

        await SendLegacyInstanceInfoMonstersAsync(updatedContext, monsters, cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyInstance Info teleport " +
                $"{FormatSession(updatedContext)} dynamicId={dynamicId} " +
                $"map={LegacyInstanceInfoMapId} x={LegacyInstanceInfoFallbackX} y={LegacyInstanceInfoFallbackY} " +
                $"dynamicSourceMap={LegacyInstanceInfoSourceMapId} instanceId={packet.InstanceId} playerUid={packet.PlayerUid}; " +
                "source CMsgInstance.Info random landing coordinate modeled with deterministic safe fallback.")
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyInstance Info monster spawn " +
                $"{FormatSession(updatedContext)} dynamicId={dynamicId} " +
                $"sourceMap={LegacyInstanceInfoSourceMapId} map={LegacyInstanceInfoMapId} " +
                $"challengeId={packet.InstanceId} floor={(isSkyTowerChallenge ? skyTowerChallenge.Floor : 0)} " +
                $"monsterId={(monsters.Count == 0 ? 0 : monsters[0].MonsterId)} count={monsters.Count} " +
                $"clientCatalogMap={(isSkyTowerChallenge ? skyTowerChallenge.ClientMapId : 0)}; " +
                (isSkyTowerChallenge
                    ? "D13 ini/instancemgr.lua objective modeled on the proven compatibility map."
                    : "source AddMapMonster monsterId=3546 count=1 modeled with deterministic safe fallback coordinate."))
            .ConfigureAwait(false);
    }

    private static async ValueTask ReplayLegacyInstanceInfoAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyInstancePacket packet,
        CancellationToken cancellationToken)
    {
        var dynamicId = checked((long)ReadCharacterDynamicId(session.Character));
        var teleportRequest = new LegacyTeleportRequest(
            "CMsgInstance.Process3 Info compatibility replay -> GameClient.Teleport",
            session.Character.MapId,
            dynamicId);
        var preflight = await PrepareLegacyTeleportAsync(
                context,
                session,
                teleportRequest,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return;
        }

        session = preflight.Session;
        var replayedSession = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = session },
                session,
                new LegacyTeleportDestination(
                    teleportRequest.RequestedMapId,
                    session.Character.X,
                    session.Character.Y,
                    checked((ulong)teleportRequest.RequestedDynamicId)),
                timestamp: 0,
                cancellationToken,
                expectedSession: session)
            .ConfigureAwait(false);
        if (replayedSession is null)
        {
            return;
        }

        var replayContext = context with { Session = replayedSession };

        var hasSkyTowerChallenge = LegacySkyTowerCatalog.TryResolve(packet.InstanceId, out var skyTowerChallenge);
        var monsters = ResolveLegacyInstanceInfoMonsters(
            replayContext,
            dynamicId,
            hasSkyTowerChallenge ? skyTowerChallenge : null);

        await SendLegacyInstanceInfoMonstersAsync(replayContext, monsters, cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer replayed LegacyInstance Info response " +
                $"{FormatSession(replayContext)} dynamicId={dynamicId} " +
                $"clientMap={ResolveLegacyTeleportClientMapId(replayedSession.Character.MapId)} " +
                $"monsterUid={(monsters.Count == 0 ? 0 : monsters[0].Uid)} " +
                $"monsterCount={monsters.Count} instanceId={packet.InstanceId} playerUid={packet.PlayerUid}; " +
                "reused the active logical map and dynamic instance.")
            .ConfigureAwait(false);
    }

    private static LegacyMonsterInstance CreateLegacyInstanceInfoMonster(
        long dynamicId,
        uint uid,
        Monster monsterFamily,
        int spawnIndex)
    {
        var offset = s_legacySkyTowerSpawnOffsets[spawnIndex % s_legacySkyTowerSpawnOffsets.Length];
        var x = CheckedUInt16(LegacyInstanceInfoFallbackX + offset.X, nameof(LegacyInstanceInfoFallbackX));
        var y = CheckedUInt16(LegacyInstanceInfoFallbackY + offset.Y, nameof(LegacyInstanceInfoFallbackY));
        var mesh = ReadAttributeUInt32(monsterFamily.Attributes, "lookface");
        var level = ReadAttributeUInt32(monsterFamily.Attributes, "level");
        var life = ReadAttributeUInt32(monsterFamily.Attributes, "life");

        return new LegacyMonsterInstance(
            uid,
            CheckedInt32(LegacyInstanceInfoMapId, nameof(LegacyInstanceInfoMapId)),
            x,
            y,
            mesh == 0 ? LegacyInstanceInfoMonsterMesh : mesh,
            level is 0 or > byte.MaxValue ? LegacyInstanceInfoMonsterLevel : checked((byte)level),
            life == 0 ? LegacyInstanceInfoMonsterLife : life,
            life == 0 ? LegacyInstanceInfoMonsterLife : life,
            CheckedUInt32(monsterFamily.Id, nameof(monsterFamily.Id)),
            monsterFamily.Name,
            Boss: ReadAttributeUInt32(monsterFamily.Attributes, "Boss", "boss") != 0,
            GeneratorId: CheckedInt32(dynamicId, nameof(dynamicId)),
            SpawnIndex: spawnIndex,
            DynamicId: CheckedUInt32(dynamicId, nameof(dynamicId)));
    }

    private static IReadOnlyList<LegacyMonsterInstance> ResolveLegacyInstanceInfoMonsters(
        WorldPacketHandlerContext context,
        long dynamicId,
        LegacySkyTowerChallenge? skyTowerChallenge)
    {
        var monsterFamily = ResolveLegacyInstanceMonsterFamily(context, skyTowerChallenge);
        var monsterCount = skyTowerChallenge?.MonsterCount ?? 1;
        var generatorId = CheckedInt32(dynamicId, nameof(dynamicId));
        var monsters = new List<LegacyMonsterInstance>(monsterCount);

        if (context.MonsterStates is not { } monsterStates)
        {
            const uint sourceMonsterUidBase = 400_000;
            const uint sourceMonsterUidSpan = 500_000;
            var fallbackUid = sourceMonsterUidBase +
                checked((uint)(checked((ulong)dynamicId) % sourceMonsterUidSpan));
            for (var spawnIndex = 0; spawnIndex < monsterCount; spawnIndex++)
            {
                monsters.Add(CreateLegacyInstanceInfoMonster(
                    dynamicId,
                    checked(fallbackUid + (uint)spawnIndex),
                    monsterFamily,
                    spawnIndex));
            }

            return monsters;
        }

        var now = DateTimeOffset.UtcNow;
        for (var spawnIndex = 0; spawnIndex < monsterCount; spawnIndex++)
        {
            if (monsterStates.TryGetDynamicMonsterBySpawnIdentity(
                    CheckedInt32(LegacyInstanceInfoMapId, nameof(LegacyInstanceInfoMapId)),
                    checked((ulong)dynamicId),
                    generatorId,
                    spawnIndex,
                    out var registered,
                    out _))
            {
                if (monsterStates.TryGetVisibleInstance(registered, now, out var visible))
                {
                    monsters.Add(visible);
                }

                continue;
            }

            var monster = CreateLegacyInstanceInfoMonster(
                dynamicId,
                monsterStates.NextLegacyDynamicMonsterUid(),
                monsterFamily,
                spawnIndex);
            monster = monsterStates.RegisterDynamicMonster(monster, monsterFamily);
            if (monsterStates.TryGetVisibleInstance(monster, now, out var current))
            {
                monsters.Add(current);
            }
        }

        return monsters;
    }

    private static Monster ResolveLegacyInstanceMonsterFamily(
        WorldPacketHandlerContext context,
        LegacySkyTowerChallenge? skyTowerChallenge)
    {
        var monsterFamilyId = skyTowerChallenge?.MonsterFamilyId ?? LegacyInstanceInfoMonsterId;
        if (context.WorldData is { } worldData)
        {
            if (worldData.MonstersById.TryGetValue(checked((int)monsterFamilyId), out var exactFamily))
            {
                return exactFamily;
            }

            if (worldData.MonstersById.TryGetValue(
                    checked((int)LegacyInstanceInfoMonsterId),
                    out var compatibilityFamily))
            {
                return compatibilityFamily with { Id = checked((int)monsterFamilyId) };
            }
        }

        return new Monster(
            checked((int)monsterFamilyId),
            LegacyInstanceInfoMonsterName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["life"] = LegacyInstanceInfoMonsterLife.ToString(CultureInfo.InvariantCulture),
                ["lookface"] = LegacyInstanceInfoMonsterMesh.ToString(CultureInfo.InvariantCulture),
                ["level"] = LegacyInstanceInfoMonsterLevel.ToString(CultureInfo.InvariantCulture),
                ["attack_range"] = "1",
                ["attack_user"] = "3",
                ["defence"] = "5000",
                ["defence2"] = "10000",
            },
            []);
    }

    private static async ValueTask SendLegacyInstanceInfoMonstersAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<LegacyMonsterInstance> monsters,
        CancellationToken cancellationToken)
    {
        foreach (var monster in monsters)
        {
            await SendLegacyInstanceInfoMonsterAsync(context, monster, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask SendLegacyInstanceInfoMonsterAsync(
        WorldPacketHandlerContext context,
        LegacyMonsterInstance monster,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                new MsgActionPacket
                {
                    ObjId = monster.Uid,
                    Type = MsgActionType.MonsterRespawnEffect,
                    PositionX = monster.X,
                    PositionY = monster.Y,
                }.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                monster.ToSpawnPacket(clientCompatibility: context.ClientCompatibility).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(monster))
        {
            await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask HandleLegacyInstanceQuitAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyInstancePacket packet,
        CancellationToken cancellationToken)
    {
        var teleportRequest = new LegacyTeleportRequest(
            "CMsgInstance.Process3 Quit -> GameClient.Teleport",
            LegacyTwinCityMapId);
        var preflight = await PrepareLegacyTeleportAsync(
                context,
                session,
                teleportRequest,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return;
        }

        session = preflight.Session;

        if (IsLegacyInstanceInfoDynamicMap(session.Character) &&
            context.MonsterStates is { } monsterStates)
        {
            monsterStates.RemoveDynamicMonsters(
                CheckedInt32(LegacyInstanceInfoMapId, nameof(LegacyInstanceInfoMapId)),
                ReadCharacterDynamicId(session.Character));
        }

        var source = session.Character;
        source = WithoutCharacterAttribute(source, LegacySkyTowerActiveChallengeAttribute);
        source = WithoutCharacterAttribute(source, LegacySkyTowerActiveCompletionAttribute);
        source = WithoutCharacterAttribute(source, LegacySkyTowerFloorAttribute);
        var preparedSession = session with { Character = source };
        var updated = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = preparedSession },
                preparedSession,
                new LegacyTeleportDestination(
                    teleportRequest.RequestedMapId,
                    LegacyInstanceQuitX,
                    LegacyInstanceQuitY,
                    checked((ulong)teleportRequest.RequestedDynamicId)),
                timestamp: 0,
                cancellationToken,
                expectedSession: session)
            .ConfigureAwait(false);
        if (updated is null)
        {
            return;
        }

        updated = updated with
        {
            Character = WithoutCharacterAttribute(updated.Character, "DynamicID"),
        };
        context.Sessions?.Set(context.Connection, updated);
        var updatedContext = context with { Session = updated };

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyInstance Quit teleport " +
                $"{FormatSession(updatedContext)} instanceId={packet.InstanceId} playerUid={packet.PlayerUid}.")
            .ConfigureAwait(false);
    }

    private static bool IsLegacyInstanceInfoDynamicMap(CharacterSnapshot character)
    {
        return character.MapId == LegacyInstanceInfoMapId &&
            ReadCharacterDynamicId(character) != 0;
    }

    private static uint ReadLegacySkyTowerAvailableAttempts(
        IReadOnlyDictionary<string, string> attributes)
    {
        if (!attributes.TryGetValue(LegacySkyTowerAvailableAttemptsAttribute, out var rawValue) ||
            !uint.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return LegacySkyTowerDefaultAvailableAttempts;
        }

        return value;
    }

}
