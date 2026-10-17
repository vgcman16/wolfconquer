using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGuardMonsterRuntimeTests : IDisposable
{
    private readonly string _usersRoot = Path.Combine(
        Path.GetTempPath(),
        "wolf-guard-runtime-" + Guid.NewGuid());

    [Fact]
    public async Task StackedGuardTileDoesNotAttackFlashingPlayer()
    {
        const int flashingNameFlag = 0;
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1011,
            x: 197,
            y: 230,
            minHitPoints: 1_000_000,
            statusFlags: BuildStatusFlags(flashingNameFlag));
        var guardFamily = CreateGuardFamily();
        var firstGuardSpawn = CreateSpawn(115, 1011, 900, "Guard1", 197, 230);
        var secondGuardSpawn = firstGuardSpawn with
        {
            GeneratorId = 116,
            SourceRelativePath = Path.Combine("1011", "Guard1", "2.ini"),
            Attributes = new Dictionary<string, string>
            {
                ["id"] = "0116",
                ["mapid"] = "1011",
                ["npctype"] = "0900",
            },
        };
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            monsters: [guardFamily],
            monsterSpawns: [firstGuardSpawn, secondGuardSpawn]);
        var firstGuard = Assert.Single(LegacyMonsterInstance.FromGenerator(
            firstGuardSpawn,
            guardFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(firstGuardSpawn)));
        var secondGuard = Assert.Single(LegacyMonsterInstance.FromGenerator(
            secondGuardSpawn,
            guardFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(secondGuardSpawn)));
        var handler = CreateRuntimeHandler(worldData, out var handoff, out var log, out var cancel);
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        try
        {
            var start = sent.Count;
            await EnterAndSetLocationAsync(handler, connection, ticket.Token, cancel.Token);
            await Task.Delay(250, cancel.Token);

            Assert.Equal(
                -1,
                FindFrameIndexAfter(
                    sent,
                    start,
                    frame => LegacySpellAnimationPacket.TryParse(frame, out var spell) &&
                        (spell.AttackerUid == firstGuard.Uid || spell.AttackerUid == secondGuard.Uid)));
            Assert.DoesNotContain("WorldServer sent legacy guard attacks", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
            cancel.Dispose();
            log.Dispose();
        }
    }

    [Fact]
    public async Task GuardMonsterProtectionSkipsDuplicateOccupiedTargetTile()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1011,
            x: 197,
            y: 230,
            minHitPoints: 1_000_000);
        var guardFamily = CreateGuardFamily();
        var targetFamily = new Monster(
            901,
            "Birdman",
            new Dictionary<string, string>
            {
                ["life"] = "1000",
                ["lookface"] = "0205",
                ["level"] = "0087",
                ["attack_range"] = "0",
                ["view_range"] = "0015",
                ["attack_speed"] = "1",
                ["move_speed"] = "1",
                ["defense"] = "25",
            },
            []);
        var guardSpawn = CreateSpawn(117, 1011, 900, "Guard1", 197, 230);
        var firstTargetSpawn = CreateSpawn(118, 1011, 901, "Birdman", 198, 230);
        var secondTargetSpawn = firstTargetSpawn with
        {
            GeneratorId = 119,
            SourceRelativePath = Path.Combine("1011", "Birdman", "2.ini"),
            Attributes = new Dictionary<string, string>
            {
                ["id"] = "0119",
                ["mapid"] = "1011",
                ["npctype"] = "0901",
            },
        };
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            monsters: [guardFamily, targetFamily],
            monsterSpawns: [guardSpawn, firstTargetSpawn, secondTargetSpawn]);
        var guardInstance = Assert.Single(LegacyMonsterInstance.FromGenerator(
            guardSpawn,
            guardFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(guardSpawn)));
        var firstTarget = Assert.Single(LegacyMonsterInstance.FromGenerator(
            firstTargetSpawn,
            targetFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(firstTargetSpawn)));
        var secondTarget = Assert.Single(LegacyMonsterInstance.FromGenerator(
            secondTargetSpawn,
            targetFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(secondTargetSpawn)));
        var handler = CreateRuntimeHandler(worldData, out var handoff, out var log, out var cancel);
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        try
        {
            var start = sent.Count;
            await EnterAndSetLocationAsync(handler, connection, ticket.Token, cancel.Token);
            await Task.Delay(250, cancel.Token);

            Assert.Equal(
                -1,
                FindFrameIndexAfter(
                    sent,
                    start,
                    frame => LegacySpellAnimationPacket.TryParse(frame, out var spell) &&
                        spell.AttackerUid == guardInstance.Uid &&
                        spell.Targets.Any(target => target.Uid == firstTarget.Uid || target.Uid == secondTarget.Uid)));
            Assert.True(GetMonsterStates(handler).TryGetVisibleInstance(
                firstTarget,
                DateTimeOffset.UtcNow.AddSeconds(20),
                out var firstVisible));
            Assert.True(GetMonsterStates(handler).TryGetVisibleInstance(
                secondTarget,
                DateTimeOffset.UtcNow.AddSeconds(20),
                out var secondVisible));
            Assert.Equal(firstTarget.Hitpoints, firstVisible.Hitpoints);
            Assert.Equal(secondTarget.Hitpoints, secondVisible.Hitpoints);
            Assert.DoesNotContain("WorldServer sent legacy guard monster-protection attacks", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
            cancel.Dispose();
            log.Dispose();
        }
    }

    [Fact]
    public async Task DuplicateStaticMonsterTileMaterializesOnlyFirstSourceRow()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1011,
            x: 197,
            y: 230,
            minHitPoints: 1_000_000);
        var birdmanFamily = new Monster(
            901,
            "Birdman",
            new Dictionary<string, string>
            {
                ["life"] = "1000",
                ["lookface"] = "0205",
                ["level"] = "0087",
                ["attack_range"] = "0",
                ["view_range"] = "0015",
                ["attack_speed"] = "1",
                ["move_speed"] = "1",
                ["defense"] = "25",
            },
            []);
        var firstSpawn = CreateSpawn(118, 1011, 901, "Birdman", 198, 230);
        var duplicateSpawn = firstSpawn with
        {
            GeneratorId = 119,
            SourceRelativePath = Path.Combine("1011", "Birdman", "2.ini"),
            Attributes = new Dictionary<string, string>
            {
                ["id"] = "0119",
                ["mapid"] = "1011",
                ["npctype"] = "0901",
            },
        };
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            monsters: [birdmanFamily],
            monsterSpawns: [firstSpawn, duplicateSpawn]);
        var firstInstance = Assert.Single(LegacyMonsterInstance.FromGenerator(
            firstSpawn,
            birdmanFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(firstSpawn)));
        var duplicateInstance = Assert.Single(LegacyMonsterInstance.FromGenerator(
            duplicateSpawn,
            birdmanFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(duplicateSpawn)));
        var handler = CreateRuntimeHandler(worldData, out var handoff, out var log, out var cancel);
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        try
        {
            var start = sent.Count;
            await EnterAndSetLocationAsync(handler, connection, ticket.Token, cancel.Token);

            var spawns = ParseMonsterSpawns(sent.Skip(start)).Where(spawn => spawn.MonsterId == 901U).ToList();
            var spawn = Assert.Single(spawns);
            Assert.Equal(firstInstance.Uid, spawn.Uid);
            Assert.DoesNotContain(spawns, packet => packet.Uid == duplicateInstance.Uid);
            Assert.Contains("monsterCount=1", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
            cancel.Dispose();
            log.Dispose();
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }

    private WorldServerFrameHandler CreateRuntimeHandler(
        WorldDataCatalog worldData,
        out AccountHandoffService handoff,
        out StringWriter log,
        out CancellationTokenSource cancel)
    {
        handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        log = new StringWriter();
        cancel = new CancellationTokenSource();
        return new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(20),
                new AnchoredRuntimeTimeProvider(DateTimeOffset.UtcNow.AddSeconds(20))),
            log);
    }

    private static async Task EnterAndSetLocationAsync(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        string ticket,
        CancellationToken cancellationToken)
    {
        await handler.HandleFrameAsync(
                connection,
                ReadFrame(new InternalWorldEnterRequestPacket(ticket).ToFrameBytes()),
                cancellationToken)
            .ConfigureAwait(false);
        await handler.HandleFrameAsync(
                connection,
                ReadFrame(new MsgActionPacket
                {
                    ObjId = 1_293_600,
                    Timestamp = 1234,
                    Type = MsgActionType.SetLocation,
                }.ToFrameBytes()),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static Monster CreateGuardFamily()
    {
        return new Monster(
            900,
            "Guard1",
            new Dictionary<string, string>
            {
                ["life"] = "70000",
                ["lookface"] = "0900",
                ["level"] = "0200",
                ["attack_range"] = "0015",
                ["view_range"] = "0015",
                ["attack_speed"] = "1",
                ["move_speed"] = "1",
                ["magic_type"] = "1234",
                ["attack_min"] = "200",
                ["attack_max"] = "200",
            },
            []);
    }

    private static MonsterSpawn CreateSpawn(
        int generatorId,
        int mapId,
        int monsterTypeId,
        string monsterName,
        int x,
        int y)
    {
        return new MonsterSpawn(
            GeneratorId: generatorId,
            MapId: mapId,
            MonsterTypeId: monsterTypeId,
            BoundX: x,
            BoundY: y,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 10,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: mapId,
            SourceMonsterName: monsterName,
            SourceRelativePath: Path.Combine(
                mapId.ToString(CultureInfo.InvariantCulture),
                monsterName,
                "1.ini"),
            Attributes: new Dictionary<string, string>
            {
                ["id"] = generatorId.ToString("0000", CultureInfo.InvariantCulture),
                ["mapid"] = mapId.ToString(CultureInfo.InvariantCulture),
                ["npctype"] = monsterTypeId.ToString("0000", CultureInfo.InvariantCulture),
            });
    }

    private void WriteSnapshot(
        long accountId,
        long uid,
        long mapId,
        long x,
        long y,
        long minHitPoints,
        string? statusFlags = null)
    {
        var statusFlagsLine = statusFlags is null ? "" : $"StatusFlags={statusFlags}\r\n";
        File.WriteAllText(
            Path.Combine(_usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map={{mapId}}
            X={{x}}
            Y={{y}}
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints={{minHitPoints}}
            MinMana=5000
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            {{statusFlagsLine}}
            PkPoints=0
            Online=7
            """);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static MonsterRuntimeStateStore GetMonsterStates(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<MonsterRuntimeStateStore>(field.GetValue(handler));
    }

    private static int FindFrameIndexAfter(
        List<byte[]> sent,
        int startIndex,
        Func<PacketFrame, bool> predicate)
    {
        var snapshot = sent.ToArray();
        for (var index = Math.Max(0, startIndex); index < snapshot.Length; index++)
        {
            var frame = ReadFrame(snapshot[index]);
            if (predicate(frame))
            {
                return index;
            }
        }

        return -1;
    }

    private static List<LegacyMonsterSpawnPacket> ParseMonsterSpawns(IEnumerable<byte[]> frames)
    {
        var spawns = new List<LegacyMonsterSpawnPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMonsterSpawnPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                spawns.Add(packet);
            }
        }

        return spawns;
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var wordCount = Math.Max(
            10,
            flags
                .Where(flag => flag >= 0)
                .Select(flag => flag / 32 + 1)
                .DefaultIfEmpty(10)
                .Max());
        var words = new ulong[wordCount];
        foreach (var flag in flags)
        {
            if (flag < 0)
            {
                continue;
            }

            var wordIndex = flag / 32;
            var bitIndex = flag % 32;
            words[wordIndex] |= 1UL << bitIndex;
        }

        return string.Join('/', words) + "/";
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
