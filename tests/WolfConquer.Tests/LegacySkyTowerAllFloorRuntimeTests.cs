using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySkyTowerAllFloorRuntimeTests
{
    private const int FloorCount = 160;
    private const int InstanceMapId = 10_429;
    private const int InstanceSourceMapId = 10_428;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task LegacyInstanceInfoRegistersExactCatalogObjectiveAcrossAllD13SkyTowerFloors()
    {
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var connection = CreateDiscardingConnection();
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            maps: [CreateMapMetadata(InstanceSourceMapId, type: 18_685_966)]);
        var challengeIds = new HashSet<uint>();
        var dynamicIds = new HashSet<uint>();
        var monsterUids = new HashSet<uint>();
        var expectedMonsterTotal = 0;

        for (var floor = 1; floor <= FloorCount; floor++)
        {
            Assert.True(
                LegacySkyTowerCatalog.TryResolveByFloor(floor, out var challenge),
                $"D13 Sky Tower floor {floor} is missing from the runtime catalog.");
            Assert.True(
                challengeIds.Add(challenge.ChallengeId),
                $"D13 Sky Tower floor {floor} reused challenge {challenge.ChallengeId}.");

            var session = CreateSession();
            var dynamicId = 0U;
            sessions.Set(connection, session);

            try
            {
                var infoFrame = ReadFrame(
                    new LegacyInstancePacket(
                            LegacyInstanceAction.Info,
                            weather: challenge.ChallengeId,
                            userId: checked((uint)session.Character.Uid))
                        .ToFrameBytes());
                Assert.Equal((ushort)2299, infoFrame.Header.Opcode);

                await PacketHandlers.HandleAsync(
                    CreateContext(connection, session, worldData, sessions, monsterStates),
                    infoFrame);

                Assert.True(sessions.TryGet(connection, out var enteredSession));
                Assert.NotNull(enteredSession);
                Assert.Equal(InstanceMapId, enteredSession!.Character.MapId);
                Assert.True(
                    uint.TryParse(
                        enteredSession.Character.Attributes["DynamicID"],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out dynamicId));
                Assert.NotEqual(0U, dynamicId);
                Assert.True(
                    dynamicIds.Add(dynamicId),
                    $"D13 Sky Tower floor {floor} reused dynamic ID {dynamicId}.");

                var registered = monsterStates.GetVisibleDynamicMonsters(
                    InstanceMapId,
                    dynamicId,
                    DateTimeOffset.UtcNow);
                Assert.True(
                    registered.Count == challenge.MonsterCount,
                    $"D13 Sky Tower floor {floor} challenge {challenge.ChallengeId} registered " +
                    $"{registered.Count} monsters; catalog requires {challenge.MonsterCount}.");

                var floorUids = new HashSet<uint>();
                foreach (var entry in registered)
                {
                    Assert.True(
                        entry.Instance.MapId == InstanceMapId,
                        $"D13 Sky Tower floor {floor} monster {entry.Instance.Uid} registered on map " +
                        $"{entry.Instance.MapId} instead of {InstanceMapId}.");
                    Assert.True(
                        entry.Instance.DynamicId == dynamicId,
                        $"D13 Sky Tower floor {floor} monster {entry.Instance.Uid} registered under dynamic ID " +
                        $"{entry.Instance.DynamicId} instead of session dynamic ID {dynamicId}.");
                    Assert.True(
                        entry.Instance.MonsterId == challenge.MonsterFamilyId,
                        $"D13 Sky Tower floor {floor} spawned monster family {entry.Instance.MonsterId}; " +
                        $"catalog requires {challenge.MonsterFamilyId}.");
                    Assert.True(
                        entry.MonsterFamily.Id == checked((int)challenge.MonsterFamilyId),
                        $"D13 Sky Tower floor {floor} registered runtime family {entry.MonsterFamily.Id}; " +
                        $"catalog requires {challenge.MonsterFamilyId}.");
                    Assert.NotEqual(0U, entry.Instance.Uid);
                    Assert.True(
                        floorUids.Add(entry.Instance.Uid),
                        $"D13 Sky Tower floor {floor} reused monster UID {entry.Instance.Uid}.");
                    Assert.True(
                        monsterUids.Add(entry.Instance.Uid),
                        $"Dynamic monster UID {entry.Instance.Uid} was reused across D13 Sky Tower floors.");
                }

                Assert.Equal(challenge.MonsterCount, floorUids.Count);
                expectedMonsterTotal += challenge.MonsterCount;

                await PacketHandlers.HandleAsync(
                    CreateContext(connection, enteredSession, worldData, sessions, monsterStates),
                    ReadFrame(
                        new LegacyInstancePacket(
                                LegacyInstanceAction.Quit,
                                weather: challenge.ChallengeId,
                                userId: checked((uint)enteredSession.Character.Uid))
                            .ToFrameBytes()));

                Assert.Empty(monsterStates.GetVisibleDynamicMonsters(
                    InstanceMapId,
                    dynamicId,
                    DateTimeOffset.UtcNow));
                Assert.True(sessions.TryGet(connection, out var returnedSession));
                Assert.NotNull(returnedSession);
                Assert.Equal(1_002, returnedSession!.Character.MapId);
                Assert.False(returnedSession.Character.Attributes.ContainsKey("DynamicID"));
                Assert.True(sessions.TryRemove(connection, out var removedSession));
                Assert.NotNull(removedSession);
                Assert.False(sessions.TryGet(connection, out _));
                Assert.Empty(sessions.Snapshot());
            }
            finally
            {
                if (dynamicId != 0)
                {
                    monsterStates.RemoveDynamicMonsters(InstanceMapId, dynamicId);
                }

                sessions.TryRemove(connection, out _);
            }
        }

        Assert.Equal(FloorCount, challengeIds.Count);
        Assert.Equal(FloorCount, dynamicIds.Count);
        Assert.Equal(expectedMonsterTotal, monsterUids.Count);
    }

    private static WorldPacketHandlerContext CreateContext(
        ConquerTcpConnection connection,
        WorldSession session,
        WorldDataCatalog worldData,
        WorldSessionStore sessions,
        MonsterRuntimeStateStore monsterStates)
    {
        return new WorldPacketHandlerContext(
            connection,
            TextWriter.Null,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "HeroOne",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "HeroOne",
                Body: 1008,
                Face: 154,
                Hair: 27,
                Class: 7051,
                FirstClass: 0,
                SecondClass: 7050,
                Reborn: 2,
                Level: 140,
                Experience: 0,
                MapId: 1002,
                X: 425,
                Y: 404,
                PreviousMapId: 1000,
                PreviousMapX: 500,
                PreviousMapY: 650,
                Strength: 0,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 61_258,
                MinMana: 5_000,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 7,
                SourceFile: "fixture://sky-tower-all-floor-runtime",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static MapMetadata CreateMapMetadata(int id, long type)
    {
        return new MapMetadata(
            id,
            $"{id}.ini",
            "Anima",
            MapDoc: id,
            TypeRaw: type.ToString(CultureInfo.InvariantCulture),
            Type: type,
            Portal0X: null,
            Portal0Y: null,
            RebornMap: 1002,
            RebornPortal: 0,
            ResLev: 0,
            LinkMap: 0,
            LinkX: 0,
            LinkY: 0,
            ColorRaw: "0",
            Color: 0,
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static ConquerTcpConnection CreateDiscardingConnection()
    {
        return new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            static (_, _) => ValueTask.CompletedTask);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
