using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    private async Task<(WorldSession UpdatedSession, List<byte[]> Sent, string Log, WorldDataCatalog WorldData)>
        KillMap1002Family2TurtledoveQuest1007MonsterAsync(
            uint objective0 = 0,
            PlayerQuestStatus status = PlayerQuestStatus.Accepted,
            bool existingPlume = false,
            bool includePlumeItemType = true,
            uint plumeDurability = 0,
            uint? occupiedUid = null,
            IReadOnlyList<uint>? intentions = null,
            string? playerQuestsRoot = null)
    {
        var family = CreateBossFamily(id: 2, name: "Turtledove", boss: false);
        var spawn = CreateBossSpawn(family, mapId: 1_002);
        var finishAnchor = new QuestNpcAnchor(
            "Finish",
            "FinishNpcId",
            Variant: null,
            NpcId: 7_991,
            MapId: 1_002,
            X: 525,
            Y: 318,
            Name: "TCViceCaptain",
            MapName: "TwinCity",
            RawValue: "7991,1002,525,318,TCViceCaptain,TwinCity",
            SourceLine: 4_276,
            Fields: ["7991", "1002", "525", "318", "TCViceCaptain", "TwinCity"]);
        var worldData = CreateWorldDataCatalog(
            family,
            spawn,
            items: includePlumeItemType ? [CreateItemType(711_236, durability: plumeDurability)] : [],
            questMissions:
            [
                CreateQuestMission(
                    1_007,
                    intentAmount: 1,
                    finishNpcAnchors: [finishAnchor]),
            ]);
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, family, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single() with
        {
            Hitpoints = 1,
        };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            KillerUid,
            "LastHit",
            mapId: 1_002,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMonsterDropRoll"] = "40",
            });
        if (existingPlume)
        {
            session = session with
            {
                PlayerItems = new PlayerItemSnapshot(
                    [
                        new PlayerItemSnapshotItem
                        {
                            Uid = 1,
                            ItemId = 711_236,
                            StackSize = 1,
                        },
                    ],
                    [],
                    [],
                    [],
                    []),
            };
        }

        if (occupiedUid is { } uid)
        {
            session = session with
            {
                PlayerItems = session.PlayerItems with
                {
                    InventoryItems = session.PlayerItems.InventoryItems.Concat(
                        [new PlayerItemSnapshotItem { Uid = uid, ItemId = 999_001, StackSize = 1 }]).ToArray(),
                },
            };
        }

        session = session with
        {
            Character = session.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                    [
                        new PlayerQuestSnapshotEntry(
                            1_007,
                            status,
                            0,
                            intentions ?? [objective0]),
                    ]),
            },
        };
        sessions.Set(connection, session);

        await KillBossAsync(
            connection,
            session,
            worldData,
            sessions,
            monsterStates,
            mailboxes,
            monster,
            log,
            damage: int.MaxValue,
            playerQuestsRoot: playerQuestsRoot);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return (updatedSession!, sent, log.ToString(), worldData);
    }

}
