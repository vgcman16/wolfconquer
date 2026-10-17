using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class DataToolSeedJsonTests
{
    private static readonly JsonSerializerOptions SeedJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void DeserializesDataToolCamelCaseSeedShapes()
    {
        var clientConfigs = DeserializeRequired<ClientConfigRecord[]>(
            """
            [
              {
                "serverId": 2,
                "serverName": "TestSrv",
                "clientBuild": 3935,
                "loginXRaw": "0222",
                "loginX": 222,
                "loginYRaw": "0214",
                "loginY": 214,
                "mapXRaw": "0001",
                "mapX": 1,
                "mapYRaw": "0000",
                "mapY": 0,
                "region": 1,
                "status": 3,
                "host": "127.0.0.1",
                "port": 9220,
                "sourceFile": "client_config.ini",
                "lineNumber": 1,
                "rawLine": "2 TestSrv 3935 0222 0214 0001 0000 1 3 127.0.0.1 9220",
                "fields": ["2", "TestSrv", "3935", "0222", "0214", "0001", "0000", "1", "3", "127.0.0.1", "9220"]
              }
            ]
            """);
        var items = DeserializeRequired<ItemType[]>(
            """
            [
              {
                "id": 50000,
                "name": "SpeedArrow",
                "fields": ["50000", "SpeedArrow", "0", "0010", ""]
              }
            ]
            """);
        var itemTypeSubs = DeserializeRequired<ItemTypeSub[]>(
            """
            [
              {
                "itemTypeId": 1000000,
                "sourceFile": "ItemtypeSub.txt",
                "lineNumber": 7,
                "rawLine": "1000000@@7@@Stancher@@0@@",
                "fields": ["1000000", "7", "Stancher", "0", ""]
              }
            ]
            """);
        var itemAddBonuses = DeserializeRequired<ItemAddBonus[]>(
            """
            [
              {
                "itemTypeId": 101000,
                "level": 2,
                "values": [0, 0, 0, 4, 0, 2, 0, 0],
                "sourceLine": 37,
                "rawLine": "101000 2 0 0 0 4 0 2 0 0"
              }
            ]
            """);
        var levelExperience = DeserializeRequired<LevelExperience[]>(
            """
            [
              {
                "type": 0,
                "level": 139,
                "experience": 2641561501496,
                "upLevTime": 3925301,
                "mentorUpLevTime": 600,
                "sourceLine": 139,
                "rawLine": "0 139 2641561501496 3925301 600",
                "fields": ["0", "139", "2641561501496", "3925301", "600"]
              }
            ]
            """);
        var classLevelStats = DeserializeRequired<ClassLevelStat[]>(
            """
            [
              {
                "className": "Thunderstriker",
                "level": 120,
                "strength": 91,
                "agility": 132,
                "vitality": 144,
                "spirit": 0,
                "sourceFile": "Stats.ini",
                "lineNumber": 1201,
                "rawKey": "Thunderstriker[120]",
                "rawValue": "91,144,132,0",
                "rawLine": "Thunderstriker[120]=91,144,132,0",
                "fields": ["91", "144", "132", "0"]
              }
            ]
            """);
        var professionLevelUps = DeserializeRequired<ProfessionLevelUp[]>(
            """
            [
              {
                "id": 571,
                "professionCode": 16051,
                "requiredLevel": 120,
                "requiredMetempsychosis": 0,
                "requiredMoney": 920000,
                "requiredProficiency": 800000,
                "sourceFile": "prof_lev_up.txt",
                "lineNumber": 571,
                "rawLine": "571@@16051@@120@@0@@920000@@800000@@",
                "fields": ["571", "16051", "120", "0", "920000", "800000", ""]
              }
            ]
            """);
        var professionLevelBenefits = DeserializeRequired<ProfessionLevelBenefit[]>(
            """
            [
              {
                "id": 571,
                "professionCode": 16051,
                "rank": 5,
                "bonusValues": [62600, 71200, 0, 0, 2600],
                "sourceFile": "prof_lev_benefit.txt",
                "lineNumber": 571,
                "rawLine": "571@@16051@@5@@62600@@71200@@0@@0@@2600@@",
                "fields": ["571", "16051", "5", "62600", "71200", "0", "0", "2600", ""]
              }
            ]
            """);
        var professionTitleBenefits = DeserializeRequired<ProfessionTitleBenefit[]>(
            """
            [
              {
                "id": 33,
                "professionGroup": 16,
                "rank": 3,
                "bonusValues": [60500, 70100, 100100, 120100, 0],
                "sourceFile": "prof_title_benefit.txt",
                "lineNumber": 33,
                "rawLine": "33@@16@@3@@60500@@70100@@100100@@120100@@0@@",
                "fields": ["33", "16", "3", "60500", "70100", "100100", "120100", "0", ""]
              }
            ]
            """);
        var questInfo = DeserializeRequired<QuestInfoMetadata>(
            """
            {
              "actionBase": 20000000,
              "recommendInterval": 300,
              "totalMission": 1
            }
            """);
        var questTaskTypes = DeserializeRequired<QuestTaskType[]>(
            """
            [
              {
                "id": 1,
                "name": "Recruit Quest",
                "taskNameColor": "0xFFCFC058",
                "position": 1,
                "sourceLine": 3,
                "rawIdKey": "TypeId1",
                "rawNameKey": "TypeName1"
              }
            ]
            """);
        var questMissions = DeserializeRequired<QuestMission[]>(
            """
            [
              {
                "sectionId": 1,
                "sourceLine": 14,
                "typeId": 1,
                "missionId": 650,
                "name": "The Armorer",
                "levelMin": 8,
                "levelMax": 50,
                "auto": 0,
                "first": 1,
                "prerequisiteRaw": "608|609",
                "prerequisiteMissionIds": [608, 609],
                "mapId": 1002,
                "profession": "1000,1001",
                "sex": 0,
                "finishTime": 0,
                "activityBeginTime": "0",
                "activityEndTime": "0",
                "completeFlag": 2,
                "prize": "EXP: 1000 [item 130006,0,0,0,3,0,0]",
                "intentionDescription": "Speak with the Armorer.",
                "intentAmount": 1,
                "content": "Have a word with the Armorer.",
                "beginNpcAnchors": [
                  {
                    "kind": "Begin",
                    "key": "BeginNpcId",
                    "npcId": 2001,
                    "mapId": 1002,
                    "x": 439,
                    "y": 464,
                    "name": "TCCaptain",
                    "mapName": "TwinCity",
                    "rawValue": "2001,1002,439,464,TCCaptain,TwinCity",
                    "sourceLine": 33,
                    "fields": ["2001", "1002", "439", "464", "TCCaptain", "TwinCity"]
                  }
                ],
                "finishNpcAnchors": [],
                "intentions": [
                  {
                    "position": 1,
                    "variant": null,
                    "key": "Intention1",
                    "text": "Pay a visit.",
                    "sourceLine": 38
                  }
                ],
                "prizeItems": [
                  {
                    "itemTypeId": 130006,
                    "rawArguments": ",0,0,0,3,0,0",
                    "position": 0
                  }
                ],
                "entries": [
                  {
                    "key": "ActivityType",
                    "value": "0",
                    "sourceLine": 17,
                    "rawLine": "ActivityType=0"
                  }
                ]
              }
            ]
            """);
        var quizQuestions = DeserializeRequired<QuizQuestionEntry[]>(
            """
            [
              {
                "position": 1,
                "question": "In~which~channel~can~all~the~players~in~the~same~server~see~messages?",
                "options": ["Team", "Broadcast", "Guild", "Talk"],
                "correctOption": 2,
                "correctAnswer": "Broadcast",
                "sourceFile": "quizquestins.ini",
                "lineNumber": 1,
                "rawLine": "In~which~channel~can~all~the~players~in~the~same~server~see~messages?#Team#Broadcast#Guild#Talk#2",
                "fields": ["In~which~channel~can~all~the~players~in~the~same~server~see~messages?", "Team", "Broadcast", "Guild", "Talk", "2"]
              }
            ]
            """);
        var magic = DeserializeRequired<MagicType[]>(
            """
            [
              {
                "id": 10,
                "typeId": 10,
                "name": "HuntingPunch",
                "fields": ["10", "10", "1", "HuntingPunch", "0001"]
              }
            ]
            """);
        var magicEffects = DeserializeRequired<MagicEffect[]>(
            """
            [
              {
                "id": 5002142000,
                "occurrence": 1,
                "name": "TenDigit",
                "description": "",
                "descriptionEx": "Long~id",
                "sourceFile": "MagicEffect.ini",
                "sourceLine": 1,
                "sourceSection": "5002142000",
                "entries": [
                  {
                    "key": "Name",
                    "value": "TenDigit",
                    "sourceFile": "MagicEffect.ini",
                    "sourceLine": 2,
                    "rawLine": "Name=TenDigit"
                  },
                  {
                    "key": "Desc",
                    "value": "",
                    "sourceFile": "MagicEffect.ini",
                    "sourceLine": 3,
                    "rawLine": "Desc="
                  }
                ],
                "attributes": {
                  "Name": "TenDigit",
                  "Desc": "",
                  "DescEx": "Long~id"
                }
              }
            ]
            """);
        var magicTypeOperations = DeserializeRequired<MagicTypeOperationEntry[]>(
            """
            [
              {
                "id": 1,
                "mode": 1,
                "group": 1,
                "level": 1,
                "flag": 2,
                "referencedMagicTypeIds": [1360, 1270, 1045],
                "values": [1, 1, 1, 1, 2, 1360, 1270, 1045, 0],
                "sourceFile": "magictypeop.txt",
                "lineNumber": 1,
                "rawLine": "1,1,1,1,2,1360,1270,1045,0",
                "fields": ["1", "1", "1", "1", "2", "1360", "1270", "1045", "0"]
              }
            ]
            """);
        var titleTypes = DeserializeRequired<TitleType[]>(
            """
            [
              {
                "id": 8043,
                "typeId": 8043,
                "name": "CS Team PK Champion Halo",
                "unknown0": 0,
                "unknown1": 0,
                "unknown2": 0,
                "unknown3": 0,
                "score": 400,
                "sourceFile": "title_type.txt",
                "lineNumber": 269,
                "rawLine": "8043@@8043@@CS Team PK Champion Halo@@0@@0@@0@@0@@400@@",
                "fields": ["8043", "8043", "CS Team PK Champion Halo", "0", "0", "0", "0", "400", ""]
              }
            ]
            """);
        var combatGear = DeserializeRequired<CombatGear[]>(
            """
            [
              {
                "id": 1,
                "gearTypeId": 201,
                "level": 1,
                "upExperience": 300,
                "life": 60,
                "attack": 30,
                "defense": 0,
                "unknownStat0": 0,
                "magicDefense": 0,
                "magicTypeIds": [17640000, 0, 0, 17650000, 0, 0, 0, 0, 0],
                "score": 240,
                "unknown0": 0,
                "unknown1": 0,
                "unknown2": 0,
                "sourceFile": "combat_gear.txt",
                "lineNumber": 1,
                "rawLine": "1@@201@@1@@300@@60@@30@@0@@0@@0@@17640000@@0@@0@@17650000@@0@@0@@0@@0@@0@@240@@0@@0@@0@@",
                "fields": ["1", "201", "1", "300", "60", "30", "0", "0", "0", "17640000", "0", "0", "17650000", "0", "0", "0", "0", "0", "240", "0", "0", "0", ""]
              }
            ]
            """);
        var traps = DeserializeRequired<Trap[]>(
            """
            [
              {
                "mapId": 44463,
                "x": 77,
                "y": 190,
                "trapTypeId": 1618,
                "optionalFlag": 1,
                "sourceFile": "Traps.txt",
                "lineNumber": 1,
                "rawLine": "44463,77,190,1618,1",
                "fields": ["44463", "77", "190", "1618", "1"]
              }
            ]
            """);
        var npcs = DeserializeRequired<Npc[]>(
            """
            [
              {
                "id": 200124,
                "kind": 2,
                "lookface": 45310,
                "mapId": 44462,
                "x": 354,
                "y": 286,
                "optionalFlag": 1
              }
            ]
            """);
        var sobNpcs = DeserializeRequired<SobNpc[]>(
            """
            [
              {
                "id": 1021,
                "name": "Awakening",
                "kind": 2,
                "lookface": 58460,
                "mapId": 11724,
                "x": 44,
                "y": 29,
                "life": 0,
                "maxLife": 0,
                "unknown9": 1,
                "unknown10": 11,
                "localizedName": "Archive",
                "sourceLine": 5,
                "rawLine": "01021,Awakening,2,58460,11724,44,29,0,0,1,11,Archive",
                "fields": ["01021", "Awakening", "2", "58460", "11724", "44", "29", "0", "0", "1", "11", "Archive"]
              }
            ]
            """);
        var portals = DeserializeRequired<Portal[]>(
            """
            [
              {
                "sourceMapId": 1000,
                "sourceX": 77,
                "sourceY": 319,
                "targetMapId": 1001,
                "targetX": 312,
                "targetY": 646
              }
            ]
            """);
        var maps = DeserializeRequired<MapMetadata[]>(
            """
            [
              {
                "id": 1000,
                "sourceRelativePath": "1000.ini",
                "name": "Desert",
                "mapDoc": 1000,
                "typeRaw": "8192",
                "type": 8192,
                "portal0X": 499,
                "portal0Y": 650,
                "rebornMap": 1000,
                "rebornPortal": 0,
                "resLev": 0,
                "linkMap": 0,
                "linkX": 0,
                "linkY": 0,
                "colorRaw": "4294967295",
                "color": 4294967295,
                "attributes": {
                  "id": "1000",
                  "mapdoc": "1000",
                  "type": "8192",
                  "color": "4294967295"
                }
              }
            ]
            """);
        var mapNames = DeserializeRequired<MapName[]>(
            """
            [
              {
                "mapId": 1002,
                "name": "CentralPlain",
                "sourceFile": "GameMapEx.ini",
                "sectionLine": 13,
                "nameLine": 14,
                "sourceSection": "1002",
                "rawNameLine": "Name=CentralPlain"
              }
            ]
            """);
        var legacyGameMaps = DeserializeRequired<LegacyGameMapEntry[]>(
            """
            [
              {
                "mapId": 1999,
                "rawMapFile": "map/map/icecrypt-lev3.7z",
                "mapFile": "map/map/icecrypt-lev3.dmap",
                "extra": 128
              }
            ]
            """);
        var monsters = DeserializeRequired<Monster[]>(
            """
            [
              {
                "id": 4711,
                "name": "300-StarWarDevil",
                "level": 145,
                "attributes": {
                  "id": "4711",
                  "name": "300-StarWarDevil",
                  "type": "0001",
                  "lookface": "0915",
                  "drop_money": "0005"
                },
                "specialDrops": []
              }
            ]
            """);
        var monsterSpawns = DeserializeRequired<MonsterSpawn[]>(
            """
            [
              {
                "generatorId": 3638,
                "mapId": 1000,
                "monsterTypeId": 2179,
                "boundX": 171,
                "boundY": 231,
                "boundWidth": 50,
                "boundHeight": 50,
                "maxNpc": 12,
                "restSeconds": 60,
                "maxPerGenerator": 10,
                "timerBegin": 0,
                "timerEnd": 0,
                "bornX": 0,
                "bornY": 0,
                "sourceMapFolderId": 1000,
                "sourceMonsterName": "DesertCondor",
                "sourceRelativePath": "1000\\\\DesertCondor\\\\1884.ini",
                "attributes": {
                  "id": "3638",
                  "mapid": "1000",
                  "bound_cx": "50",
                  "return_mode": "1"
                }
              }
            ]
            """);
        var lotteryDictionary = DeserializeRequired<LotteryDictionaryEntry[]>(
            """
            [
              {
                "id": 17,
                "type": 2,
                "subType": 3,
                "value": 18007,
                "unknown0": 0,
                "weight": 2,
                "sourceFile": "dict_lottery.txt",
                "lineNumber": 17,
                "rawLine": "17@@2@@3@@18007@@0@@2@@",
                "fields": ["17", "2", "3", "18007", "0", "2", ""]
              }
            ]
            """);
        var collectionRewardTiers = DeserializeRequired<CollectionRewardTier[]>(
            """
            [
              {
                "id": 11,
                "collectionId": 700,
                "minInclusive": 8400,
                "maxInclusive": 2147483647,
                "rewardType": 1,
                "rewardValue": 1500,
                "rewardAmount": 600,
                "sourceFile": "dict_collection.txt",
                "lineNumber": 11,
                "rawLine": "11@@700@@8400@@2147483647@@1@@1500@@600@@",
                "fields": ["11", "700", "8400", "2147483647", "1", "1500", "600", ""]
              }
            ]
            """);
        var taskRewardTypes = DeserializeRequired<TaskRewardTypeEntry[]>(
            """
            [
              {
                "id": 755,
                "parameters": [2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                "rewardValues": [3301408, 3301409, 3308803, 3301410, 3301411, 3301412, 3301413, 3301414],
                "rewardWeights": [3000, 3000, 3000, 600, 280, 100, 10, 10],
                "extra0": 0,
                "extra1": 0,
                "sourceFile": "task_reward_type.txt",
                "lineNumber": 1,
                "rawLine": "755@@2@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@3301408@@3301409@@3308803@@3301410@@3301411@@3301412@@3301413@@3301414@@3000@@3000@@3000@@600@@280@@100@@10@@10@@0@@0@@",
                "fields": ["755", "2", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "3301408", "3301409", "3308803", "3301410", "3301411", "3301412", "3301413", "3301414", "3000", "3000", "3000", "600", "280", "100", "10", "10", "0", "0", ""]
              }
            ]
            """);
        var taskRewardTypeIniEntries = DeserializeRequired<TaskRewardTypeIniEntry[]>(
            """
            [
              {
                "id": 3651,
                "parameters": [2, 1, 2, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0],
                "rewardValues": [3007285, 3007284, 3007386, 3008731, 3007311, 3008729, 3600012, 3008732],
                "rewardWeights": [1000, 1000, 1000, 1000, 1500, 1500, 1500, 1500],
                "extra0": 98579160,
                "extra1": 98579161,
                "sourceFile": "task_reward_type.ini",
                "lineNumber": 2,
                "rawLine": "3651 2 1 0002 0000 0002 0 0 0 0 0 0 0 0 3007285 3007284 3007386 3008731 3007311 3008729 3600012 3008732 1000 1000 1000 1000 1500 1500 1500 1500 98579160 98579161",
                "fields": ["3651", "2", "1", "0002", "0000", "0002", "0", "0", "0", "0", "0", "0", "0", "0", "3007285", "3007284", "3007386", "3008731", "3007311", "3008729", "3600012", "3008732", "1000", "1000", "1000", "1000", "1500", "1500", "1500", "1500", "98579160", "98579161"]
              }
            ]
            """);
        var neiGongHeader = DeserializeRequired<NeiGongHeader>(
            """
            {
              "secretCount": 1,
              "attributeTips": [
                { "attributeType": 0, "text": "Potency~Cost" },
                { "attributeType": 14, "text": "Anti-break" }
              ],
              "sourceFile": "NeiGongInfo.ini",
              "lineNumber": 1,
              "rawSectionName": "NeiGong",
              "fields": {
                "Num": "1",
                "Attri0Tip": "Potency~Cost",
                "Attri14Tip": "Anti-break"
              }
            }
            """);
        var neiGongSecrets = DeserializeRequired<NeiGongSecret[]>(
            """
            [
              {
                "id": 1,
                "name": "Universal~Concept",
                "aniSection": "NeiGong_HeartSkill14Btn",
                "neiGongCount": 1,
                "attributeTypes": [7, 9],
                "attributeValues": [100, 100],
                "getWayContent": "PS:~Claim~the~book~[Universal~Concept]~from~the~Celestial~Sage~at~(TwinCity~202,416).",
                "sourceFile": "NeiGongInfo.ini",
                "lineNumber": 6,
                "rawSectionName": "1",
                "fields": {
                  "id": "1",
                  "Name": "Universal~Concept",
                  "AniSection": "NeiGong_HeartSkill14Btn",
                  "NeiGongNum": "1",
                  "AttriType": "7-9",
                  "AttriValue": "100-100",
                  "GetWayContent": "PS:~Claim~the~book~[Universal~Concept]~from~the~Celestial~Sage~at~(TwinCity~202,416)."
                }
              }
            ]
            """);
        var neiGongTypes = DeserializeRequired<NeiGongType[]>(
            """
            [
              {
                "secretId": 1,
                "slot": 1,
                "typeId": 1,
                "name": "Universal~Concept~(A)",
                "aniSection": "NeiGong_HeartSkill14Btn",
                "maxLevel": 5,
                "attributeTypes": [2, 4],
                "attributeValues": [100, 100],
                "cultureValues": [1500, 1500, 1500, 1500, 1500],
                "condition": "<item 3005365>[Universal~Concept~(A)]</item>",
                "requiredLevel": 2015,
                "requiredNeiGongValue": 0,
                "requiredPrerequisiteNeiGong": 0,
                "requiredItemTypeId": 3005365,
                "sourceFile": "NeiGongInfo.ini",
                "lineNumber": 16,
                "rawSectionName": "1-1",
                "fields": {
                  "Type": "1",
                  "Name": "Universal~Concept~(A)",
                  "AniSection": "NeiGong_HeartSkill14Btn",
                  "MaxLev": "5",
                  "AttriType": "2-4",
                  "AttriValue": "100-100",
                  "NeiGongValue": "1500-1500-1500-1500-1500",
                  "Condition": "<item 3005365>[Universal~Concept~(A)]</item>",
                  "ReqLev": "2015",
                  "ReqNeiGongValue": "0",
                  "ReqPreNeiGong": "0",
                  "ReqItemType": "3005365"
                }
              }
            ]
            """);
        var subProfessions = DeserializeRequired<SubProfessionEntry[]>(
            """
            [
              {
                "id": 11,
                "title": "Martial Artist",
                "intro": "Critical Strike +1%",
                "requiredLevel": 70,
                "requiredMetempsychosis": 0,
                "requiredProfessions": [41, 31],
                "promotion": "or 800 StudyPoints",
                "suit": 0,
                "levelLimit": 9,
                "requiredJobLevel": 1,
                "effectProportion": 1,
                "upLevelEffect": "LevelUp",
                "upStepEffect": "LevelUp",
                "upStepScreenEffect": null,
                "upEffect": null,
                "subSpecial": "None",
                "suitPower": null,
                "bigClass": null,
                "accessionRoute": null,
                "stepUpRoute": null,
                "animationSection": null,
                "sourceFile": "SubProfessionInfo.ini",
                "lineNumber": 54,
                "rawSectionName": "11",
                "entries": [
                  {
                    "key": "req_joblv",
                    "value": "0",
                    "sourceFile": "SubProfessionInfo.ini",
                    "lineNumber": 61,
                    "rawLine": "req_joblv=0"
                  },
                  {
                    "key": "req_joblv",
                    "value": "1",
                    "sourceFile": "SubProfessionInfo.ini",
                    "lineNumber": 64,
                    "rawLine": "req_joblv=1"
                  }
                ]
              }
            ]
            """);
        var syndicateLevels = DeserializeRequired<SyndicateLevel[]>(
            """
            [
              {
                "level": 1,
                "parameters": [999, 2, 8, 10, 3, 3, 3, 1, 3, 5, 5],
                "sourceFile": "syndicate_level.txt",
                "lineNumber": 1,
                "rawLine": "1@@999@@2@@8@@10@@3@@3@@3@@1@@3@@5@@5@@",
                "fields": ["1", "999", "2", "8", "10", "3", "3", "3", "1", "3", "5", "5", ""]
              }
            ]
            """);
        var runeLevelExperience = DeserializeRequired<RuneLevelExperience[]>(
            """
            [
              {
                "id": 28,
                "runeType": 402,
                "level": 27,
                "experience": 341500,
                "sourceFile": "rune_levexp.txt",
                "lineNumber": 28,
                "rawLine": "28@@402@@27@@341500@@",
                "fields": ["28", "402", "27", "341500", ""]
              }
            ]
            """);
        var runeStorageAttributes = DeserializeRequired<RuneStorageAttribute[]>(
            """
            [
              {
                "id": 21,
                "requiredStorageScore": 335000,
                "bonuses": [
                  { "attributeType": 11, "value": 300 },
                  { "attributeType": 12, "value": 300 },
                  { "attributeType": 0, "value": 0 }
                ],
                "sourceFile": "rune_storage_attr.txt",
                "lineNumber": 21,
                "rawLine": "21@@335000@@11@@300@@12@@300@@0@@0@@",
                "fields": ["21", "335000", "11", "300", "12", "300", "0", "0", ""]
              }
            ]
            """);
        var itemRefineEffects = DeserializeRequired<ItemRefineEffect[]>(
            """
            [
              {
                "id": 648,
                "values": [6480, 6480, 8080, 2020, 1010, 2007, 3007, 4008, 5005, 6005, 7008, 8008, 9005, 10005, 11008, 12008, 13008, 14006, 15006, 16002],
                "sourceFile": "item_refine_effect.txt",
                "lineNumber": 648,
                "rawLine": "648@@6480@@6480@@8080@@2020@@1010@@2007@@3007@@4008@@5005@@6005@@7008@@8008@@9005@@10005@@11008@@12008@@13008@@14006@@15006@@16002@@",
                "fields": ["648", "6480", "6480", "8080", "2020", "1010", "2007", "3007", "4008", "5005", "6005", "7008", "8008", "9005", "10005", "11008", "12008", "13008", "14006", "15006", "16002", ""]
              }
            ]
            """);
        var itemRefineEffectEx = DeserializeRequired<ItemRefineEffectEx[]>(
            """
            [
              {
                "id": 1,
                "itemTypeId": 2100095,
                "values": [100, 5, 7],
                "sourceFile": "item_refine_effect_ex.txt",
                "lineNumber": 1,
                "rawLine": "1@@2100095@@100@@5@@7@@",
                "fields": ["1", "2100095", "100", "5", "7", ""]
              }
            ]
            """);
        var itemRefineUpgrades = DeserializeRequired<ItemRefineUpgrade[]>(
            """
            [
              {
                "id": 1,
                "level": 54,
                "value": 12000,
                "sourceFile": "item_refine_upgrade.txt",
                "lineNumber": 54,
                "rawLine": "1@@54@@12000@@",
                "fields": ["1", "54", "12000", ""]
              }
            ]
            """);
        var transformInfo = DeserializeRequired<TransformInfo[]>(
            """
            [
              {
                "transformId": 1350,
                "level": 3,
                "name": "DivineHare",
                "lookface": 267,
                "maxLife": 3000,
                "sourceFile": "TransformInfo.txt",
                "lineNumber": 4,
                "rawLine": "1350 3 DivineHare 267 3000",
                "fields": ["1350", "3", "DivineHare", "267", "3000"]
              }
            ]
            """);
        var furniture = DeserializeRequired<Furniture[]>(
            """
            [
              {
                "id": 8924,
                "kind": 26,
                "lookface": 837,
                "mapId": 1511,
                "x": 143,
                "y": 130,
                "name": "FlowerDressingTable",
                "itemTypeId": 726083,
                "price": 23000,
                "sourceFile": "furnitures.txt",
                "lineNumber": 1,
                "rawLine": "8924,26,837,1511,143,130,FlowerDressingTable,726083,23000",
                "fields": ["8924", "26", "837", "1511", "143", "130", "FlowerDressingTable", "726083", "23000"]
              }
            ]
            """);
        var hairColorTypes = DeserializeRequired<HairColorType[]>(
            """
            [
              {
                "hairColorId": 78,
                "colorIndex": 5,
                "unlockType": 1,
                "itemTypeId": 3326778,
                "cost": 1,
                "sourceFile": "hair_color_type.txt",
                "lineNumber": 275,
                "rawLine": "78@@5@@1@@3326778@@1@@",
                "fields": ["78", "5", "1", "3326778", "1", ""]
              }
            ]
            """);
        var hairFaceStorageTypes = DeserializeRequired<HairFaceStorageType[]>(
            """
            [
              {
                "categoryType": 0,
                "id": 1,
                "unknown0": 0,
                "name": "YouthSpark",
                "unlockType": 0,
                "requiredValue": 0,
                "unknown1": 0,
                "cost": 10000,
                "unknown2": 0,
                "linkedColorIdsRaw": "",
                "sort": 0,
                "sourceFile": "hairface_storage_type.txt",
                "lineNumber": 1,
                "rawLine": "0@@1@@0@@YouthSpark@@0@@0@@0@@10000@@0@@@@0@@",
                "fields": ["0", "1", "0", "YouthSpark", "0", "0", "0", "10000", "0", "", "0", ""]
              }
            ]
            """);
        var coatStorageTypes = DeserializeRequired<CoatStorageType[]>(
            """
            [
              {
                "itemTypeId": 181305,
                "storageType": 31,
                "parameters": [1, 0, 0, 0, 0, 1],
                "sourceFile": "coat_storage_type.txt",
                "lineNumber": 1,
                "rawLine": "181305@@31@@1@@0@@0@@0@@0@@1@@",
                "fields": ["181305", "31", "1", "0", "0", "0", "0", "1", ""]
              }
            ]
            """);
        var coatStorageAttributes = DeserializeRequired<CoatStorageAttribute[]>(
            """
            [
              {
                "id": 17,
                "storageType": 1000000000,
                "requiredStorageScore": 30,
                "bonuses": [
                  { "attributeType": 3, "value": 300 },
                  { "attributeType": 5, "value": 100 },
                  { "attributeType": 0, "value": 0 },
                  { "attributeType": 0, "value": 0 }
                ],
                "reserved0": 0,
                "sourceFile": "coat_storage_attr.txt",
                "lineNumber": 3,
                "rawLine": "17@@1000000000@@30@@3@@300@@5@@100@@0@@0@@0@@0@@0@@",
                "fields": ["17", "1000000000", "30", "3", "300", "5", "100", "0", "0", "0", "0", "0", ""]
              }
            ]
            """);
        var coatColorRules = DeserializeRequired<CoatColorRule[]>(
            """
            [
              {
                "itemTypeId": 200412,
                "parameters": [1, 5, 499, 3326059, 1, 3328359, 5, 300, 232503150, 232503150, 232503150, 232503150, 0, 0, 0, 0, 250200, 50],
                "sourceFile": "coat_color_rule.txt",
                "lineNumber": 1,
                "rawLine": "200412@@1@@5@@499@@3326059@@1@@3328359@@5@@300@@232503150@@232503150@@232503150@@232503150@@0@@0@@0@@0@@250200@@50@@",
                "fields": ["200412", "1", "5", "499", "3326059", "1", "3328359", "5", "300", "232503150", "232503150", "232503150", "232503150", "0", "0", "0", "0", "250200", "50", ""]
              }
            ]
            """);
        var xuanbaoAdditionAttributes = DeserializeRequired<XuanbaoAdditionAttribute[]>(
            """
            [
              {
                "id": 1,
                "attributeType": 14,
                "level": 1,
                "values": [0, 50],
                "sourceFile": "xuanbao_addition_attr.txt",
                "lineNumber": 1,
                "rawLine": "1@@14@@1@@0@@50@@",
                "fields": ["1", "14", "1", "0", "50", ""]
              }
            ]
            """);
        var xuanbaoComposeAttributeLimits = DeserializeRequired<XuanbaoComposeAttributeLimit[]>(
            """
            [
              {
                "id": 1,
                "values": [0, 4, 400],
                "sourceFile": "xuanbao_compose_attr_limit.txt",
                "lineNumber": 1,
                "rawLine": "1@@0@@4@@400@@",
                "fields": ["1", "0", "4", "400", ""]
              }
            ]
            """);
        var xuanbaoRandomAttributes = DeserializeRequired<XuanbaoRandomAttribute[]>(
            """
            [
              {
                "itemTypeId": 4100001,
                "attributeType": 1,
                "minValue": 30,
                "maxValue": 101,
                "sourceFile": "xuanbao_rand_attr.txt",
                "lineNumber": 1,
                "rawLine": "4100001@@1@@30@@101@@",
                "fields": ["4100001", "1", "30", "101", ""]
              }
            ]
            """);
        var christmasNpcs = DeserializeRequired<ChristmasNpc[]>(
            """
            [
              {
                "id": 52301,
                "name": null,
                "kind": 2,
                "lookface": 15820,
                "mapId": 1002,
                "x": 313,
                "y": 231,
                "parameters": [0, 0, 1, 1],
                "sourceFile": "christmasnpcs.txt",
                "lineNumber": 1,
                "rawLine": "52301,,2,15820,1002,313,231,0,0,1,1",
                "fields": ["52301", "", "2", "15820", "1002", "313", "231", "0", "0", "1", "1"]
              }
            ]
            """);
        var tutorTypes = DeserializeRequired<TutorType[]>(
            """
            [
              {
                "id": 1,
                "minLevel": 70,
                "maxLevel": 109,
                "values": [2, 10],
                "sourceFile": "cq_tutor_type.txt",
                "lineNumber": 1,
                "rawLine": "1,70,109,2,10",
                "fields": ["1", "70", "109", "2", "10"]
              }
            ]
            """);
        var tutorBattleLimitTypes = DeserializeRequired<TutorBattleLimitType[]>(
            """
            [
              {
                "id": 400,
                "battlePowerLimit": 5,
                "sourceFile": "cq_tutor_battle_limit_type.txt",
                "lineNumber": 400,
                "rawLine": "400,5",
                "fields": ["400", "5"]
              }
            ]
            """);
        var beastAttributes = DeserializeRequired<BeastAttribute[]>(
            """
            [
              {
                "level": 1,
                "parameters": [2400, 200, 200, 500, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                "sourceFile": "beasts_attr.txt",
                "lineNumber": 1,
                "rawLine": "1@@2400@@200@@200@@500@@100@@100@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@",
                "fields": ["1", "2400", "200", "200", "500", "100", "100", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", ""]
              }
            ]
            """);
        var fateExperience = DeserializeRequired<FateExperience[]>(
            """
            [
              {
                "id": 1,
                "type": 1,
                "level": 1,
                "experience": 500,
                "values": [60000, 120000, 130000, 90000, 0, 0, 0, 10, 0, 0, 3300576],
                "sourceFile": "fate_exp.txt",
                "lineNumber": 1,
                "rawLine": "1@@1@@1@@500@@60000@@120000@@130000@@90000@@0@@0@@0@@10@@0@@0@@3300576@@",
                "fields": ["1", "1", "1", "500", "60000", "120000", "130000", "90000", "0", "0", "0", "10", "0", "0", "3300576", ""]
              }
            ]
            """);
        var spiritRates = DeserializeRequired<SpiritRate[]>(
            """
            [
              {
                "id": 76,
                "type": 1,
                "itemTypeId": 4200001,
                "parameters": [0, 0, 5, 0, 0],
                "sourceFile": "spirit_rate.txt",
                "lineNumber": 1,
                "rawLine": "76@@1@@4200001@@0@@0@@5@@0@@0@@",
                "fields": ["76", "1", "4200001", "0", "0", "5", "0", "0", ""]
              }
            ]
            """);
        var hundredWeapons = DeserializeRequired<HundredWeapon[]>(
            """
            [
              {
                "id": 1,
                "weaponType": 410,
                "level": 1,
                "values": [0, 100, 10100, 20050, 30030, 15870, 0, 0],
                "sourceFile": "hundred_weapon.txt",
                "lineNumber": 1,
                "rawLine": "1@@410@@1@@0@@100@@10100@@20050@@30030@@15870@@0@@0@@",
                "fields": ["1", "410", "1", "0", "100", "10100", "20050", "30030", "15870", "0", "0", ""]
              }
            ]
            """);
        var gouyuTypes = DeserializeRequired<GouyuType[]>(
            """
            [
              {
                "id": 1,
                "type": 100,
                "level": 1,
                "values": [50, 10537, 0, 16520, 16530, 0, 0, 0, 100, 10],
                "sourceFile": "gouyu_type.txt",
                "lineNumber": 1,
                "rawLine": "1@@100@@1@@50@@10537@@0@@16520@@16530@@0@@0@@0@@100@@10@@",
                "fields": ["1", "100", "1", "50", "10537", "0", "16520", "16530", "0", "0", "0", "100", "10", ""]
              }
            ]
            """);
        var gouyuImmortals = DeserializeRequired<GouyuImmortal[]>(
            """
            [
              {
                "id": 8,
                "values": [8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                "sourceFile": "gouyu_immortal.txt",
                "lineNumber": 1,
                "rawLine": "8@@8@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@",
                "fields": ["8", "8", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", ""]
              }
            ]
            """);
        var specialEffectAttributes = DeserializeRequired<SpecialEffectAttribute[]>(
            """
            [
              {
                "id": 40701011,
                "type": 1,
                "values": [10, 10, 100, 10, 60, 5],
                "sourceFile": "speffect_attribute.txt",
                "lineNumber": 1,
                "rawLine": "40701011@@1@@10@@10@@100@@10@@60@@5@@",
                "fields": ["40701011", "1", "10", "10", "100", "10", "60", "5", ""]
              }
            ]
            """);
        var daoqiDictTypes = DeserializeRequired<DaoqiDictType[]>(
            """
            [
              {
                "id": 113,
                "type": 0,
                "level": 1,
                "values": [100, 0, 0, 100, 1000, 900, 113, 1, 0, 0, 0, 0],
                "sourceFile": "daoqi_dict_type.txt",
                "lineNumber": 1,
                "rawLine": "113@@0@@1@@100@@0@@0@@100@@1000@@900@@113@@1@@0@@0@@0@@0@@",
                "fields": ["113", "0", "1", "100", "0", "0", "100", "1000", "900", "113", "1", "0", "0", "0", "0", ""]
              }
            ]
            """);
        var syndicateFormationTypes = DeserializeRequired<SyndicateFormationType[]>(
            """
            [
              {
                "id": 1,
                "type": 100,
                "level": 1,
                "values": [100000000, 120000000, 700, 1000, 0],
                "sourceFile": "syn_formtype.txt",
                "lineNumber": 1,
                "rawLine": "1@@100@@1@@100000000@@120000000@@700@@1000@@0@@",
                "fields": ["1", "100", "1", "100000000", "120000000", "700", "1000", "0", ""]
              }
            ]
            """);
        var syndicateSkillTypes = DeserializeRequired<SyndicateSkillType[]>(
            """
            [
              {
                "id": 1,
                "skillType": 1,
                "level": 1,
                "values": [500, 75050, 50000, 50],
                "sourceFile": "syn_skill_type.txt",
                "lineNumber": 1,
                "rawLine": "1@@1@@1@@500@@75050@@50000@@50@@",
                "fields": ["1", "1", "1", "500", "75050", "50000", "50", ""]
              }
            ]
            """);
        var disdainRules = DeserializeRequired<DisdainRule[]>(
            """
            [
              {
                "id": 1,
                "values": [0, 400, 2800, 0, 60000, 60000, -90, -90, 1, 2000, 100, 1, 2000, 100, 1, 2000, 100, 1, 2000, 100],
                "sourceFile": "cq_disdain.txt",
                "lineNumber": 1,
                "rawLine": "1,0,400,2800,0,60000,60000,-90,-90,1,2000,100,1,2000,100,1,2000,100,1,2000,100",
                "fields": ["1", "0", "400", "2800", "0", "60000", "60000", "-90", "-90", "1", "2000", "100", "1", "2000", "100", "1", "2000", "100", "1", "2000", "100"]
              }
            ]
            """);
        var jiangHuAttributes = DeserializeRequired<JiangHuAttribute[]>(
            """
            [
              {
                "id": 1,
                "attributeType": 101,
                "level": 1,
                "value": 500,
                "sourceFile": "JianghuAttributes.txt",
                "lineNumber": 2,
                "rawLine": "1 101 1 500",
                "fields": ["1", "101", "1", "500"]
              }
            ]
            """);
        var jiangHuCultivateStatuses = DeserializeRequired<JiangHuCultivateStatus[]>(
            """
            [
              {
                "id": 1,
                "declaredValueCount": 3,
                "values": [10, 20, 30],
                "sourceFile": "JingHuCultivateStatus.txt",
                "lineNumber": 2,
                "rawLine": "1 3 10 20 30",
                "fields": ["1", "3", "10", "20", "30"]
              }
            ]
            """);
        var refineryBoxes = DeserializeRequired<RefineryBox[]>(
            """
            [
              {
                "itemType": 3300001,
                "values": [1, 2, 3],
                "sourceFile": "databaserefineryboxes.txt",
                "lineNumber": 2,
                "rawLine": "3300001,1,2,3",
                "fields": ["3300001", "1", "2", "3"]
              }
            ]
            """);
        var refinerySouls = DeserializeRequired<RefinerySoul[]>(
            """
            [
              {
                "soulLevel": 1,
                "equipmentName": "WeaponSoul",
                "itemType": 800001,
                "sourceFile": "Souls1.ini",
                "lineNumber": 2,
                "rawLine": "WeaponSoul 800001",
                "fields": ["WeaponSoul", "800001"]
              }
            ]
            """);
        var refineryMaterials = DeserializeRequired<RefineryMaterial[]>(
            """
            [
              {
                "itemType": 724350,
                "name": "Detoxication(Unique)Material",
                "primaryValue": 11,
                "effectType": 7,
                "parameters": [11, 7, 0],
                "target": "Necklace",
                "description": "Level~3~Refinery~item.~Right~click~to~refine~a~Necklace.~Reduces~11%~Poison~Damage.",
                "tailValues": [7, 0, 0, 0, 13010, 500],
                "sourceFile": "Rifinery.txt",
                "lineNumber": 1,
                "rawLine": "724350 Detoxication(Unique)Material 11 7",
                "fields": ["724350", "Detoxication(Unique)Material", "11", "7"]
              }
            ]
            """);
        var pokerTables = DeserializeRequired<PokerTable[]>(
            """
            [
              {
                "id": 1,
                "values": [13, 60, 3, 2, 1, 1000, 2000, 3000],
                "sourceFile": "PokerTables.txt",
                "lineNumber": 1,
                "rawLine": "1 013 060 3 2 1 1000 2000 3000",
                "fields": ["1", "013", "060", "3", "2", "1", "1000", "2000", "3000"]
              }
            ]
            """);
        var rouletteTables = DeserializeRequired<RouletteTable[]>(
            """
            [
              {
                "id": 1,
                "values": [3001, 3002, 3003, 1, 2, 3],
                "sourceFile": "Roulettes.txt",
                "lineNumber": 1,
                "rawLine": "1,3001,3002,3003,1,2,3",
                "fields": ["1", "3001", "3002", "3003", "1", "2", "3"]
              }
            ]
            """);
        var npcTimeEntries = DeserializeRequired<NpcTimeEntry[]>(
            """
            [
              {
                "id": 1,
                "label": "Arena",
                "values": [1, 2, 3, 4, 5, 6, 7, 8, 9],
                "sourceFile": "NpcTime.txt",
                "lineNumber": 1,
                "rawLine": "1,Arena,1,2,3,4,5,6,7,8,9",
                "fields": ["1", "Arena", "1", "2", "3", "4", "5", "6", "7", "8", "9"]
              }
            ]
            """);
        var activityTasks = DeserializeRequired<ActivityTaskCatalog>(
            """
            {
              "metadata": {
                "declaredCount": 1,
                "sourceSection": "ActivityTask",
                "sourceLine": 1,
                "entries": [
                  {
                    "key": "Num",
                    "value": "1",
                    "sourceLine": 2,
                    "rawLine": "Num=1"
                  }
                ]
              },
              "tasks": [
                {
                  "sectionId": 7,
                  "id": 1007,
                  "title": "Daily Gift",
                  "type": 1,
                  "progress": 3,
                  "activeValue": 25,
                  "closeLevelRaw": "0141",
                  "closeLevel": 141,
                  "tip": "Claim it from Welfare.",
                  "openLevelRaw": "0005",
                  "openLevel": 5,
                  "previewRaw": "1",
                  "preview": 1,
                  "intro": "Finish three rounds.",
                  "animationSection": "Sparkle",
                  "param": "[npc,Guide,9534,1002,301,529]",
                  "sourceSection": "7",
                  "sourceLine": 5,
                  "entries": [
                    {
                      "key": "CloseLev",
                      "value": "0141",
                      "sourceLine": 11,
                      "rawLine": "CloseLev=0141"
                    }
                  ]
                }
              ],
              "sourceFile": "ActivityTask.ini"
            }
            """);
        var meltingTypeRules = DeserializeRequired<MeltingTypeRule[]>(
            """
            [
              {
                "ruleType": 1,
                "itemTypeId": 700002,
                "parameters": [0, 0, 1],
                "sourceFile": "MeltingType.ini",
                "lineNumber": 3,
                "rawLine": "1 700002 0 0 1",
                "fields": ["1", "700002", "0", "0", "1"]
              }
            ]
            """);
        var lotteryPrizes = DeserializeRequired<LotteryPrize[]>(
            """
            [
              {
                "type": 1,
                "level": 70,
                "itemName": "WavyHeavyRing+8",
                "itemTypeRaw": "151159",
                "itemTypeId": 151159,
                "socketOne": 2,
                "socketTwo": 0,
                "plusLevel": 8,
                "sourceFile": "lottery.ini",
                "lineNumber": 2,
                "rawLine": "1 70 WavyHeavyRing+8 151159 2 0 8",
                "fields": ["1", "70", "WavyHeavyRing+8", "151159", "2", "0", "8"]
              }
            ]
            """);
        var arenaRecords = DeserializeRequired<ArenaRecord[]>(
            """
            [
              {
                "playerId": 1001,
                "playerName": "ArenaOne",
                "values": [120, 8000, 100001, 4000, 0, 0, 0, 0, 0, 0, 4000, 0, 0, 1],
                "sourceFile": "Arena.ini",
                "lineNumber": 2,
                "rawLine": "1001/ArenaOne/120/8000/100001/4000/0/0/0/0/0/0/4000/0/0/1/",
                "fields": ["1001", "ArenaOne", "120", "8000", "100001", "4000", "0", "0", "0", "0", "0", "0", "4000", "0", "0", "1", ""]
              }
            ]
            """);
        var teamArenaRecords = DeserializeRequired<TeamArenaRecord[]>(
            """
            [
              {
                "playerId": 1293600,
                "playerName": "TeamArenaOne",
                "values": [140, 7051, 1541008, 4000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                "sourceFile": "TeamArena.ini",
                "lineNumber": 2,
                "rawLine": "1293600/TeamArenaOne/140/7051/1541008/4000/0/0/0/0/0/0/0/0/0/0/",
                "fields": ["1293600", "TeamArenaOne", "140", "7051", "1541008", "4000", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", ""]
              }
            ]
            """);
        var elitePkRecords = DeserializeRequired<PkTournamentRecord[]>(
            """
            [
              {
                "group": 3,
                "slot": 7,
                "level": 140,
                "playerName": "EliteOne",
                "lookface": 100001,
                "status": 1,
                "playerId": 7001,
                "sourceFile": "ElitePk.ini",
                "lineNumber": 2,
                "rawLine": "3/7/140/EliteOne/100001/1/7001/",
                "fields": ["3", "7", "140", "EliteOne", "100001", "1", "7001", ""]
              }
            ]
            """);
        var teamElitePkRecords = DeserializeRequired<PkTournamentRecord[]>(
            """
            [
              {
                "group": 2,
                "slot": 6,
                "level": 130,
                "playerName": "TeamOne",
                "lookface": 100003,
                "status": 1,
                "playerId": 7003,
                "sourceFile": "TeamElitePK.ini",
                "lineNumber": 2,
                "rawLine": "2/6/130/TeamOne/100003/1/7003/",
                "fields": ["2", "6", "130", "TeamOne", "100003", "1", "7003", ""]
              }
            ]
            """);
        var skillTeamPkRecords = DeserializeRequired<PkTournamentRecord[]>(
            """
            [
              {
                "group": 1,
                "slot": 5,
                "level": 120,
                "playerName": "SkillOne",
                "lookface": 100005,
                "status": 1,
                "playerId": 7005,
                "sourceFile": "SkillTeamPK.ini",
                "lineNumber": 2,
                "rawLine": "1/5/120/SkillOne/100005/1/7005/",
                "fields": ["1", "5", "120", "SkillOne", "100005", "1", "7005", ""]
              }
            ]
            """);
        var classPkWarRecords = DeserializeRequired<ClassPkWarRecord[]>(
            """
            [
              {
                "classId": 10,
                "slot": 3,
                "playerId": 7001,
                "points": 100,
                "sourceFile": "ClassPkWar.ini",
                "lineNumber": 2,
                "rawLine": "10/3/7001/100/",
                "fields": ["10", "3", "7001", "100", ""]
              }
            ]
            """);
        var combatGearRanks = DeserializeRequired<CombatGearRankEntry[]>(
            """
            [
              {
                "characterId": 5001,
                "characterName": "RankOne",
                "rankingFields": ["gear", "100"],
                "sourceFile": "CombatGearRank.txt",
                "lineNumber": 2,
                "rawLine": "5001/RankOne/gear/100/",
                "fields": ["5001", "RankOne", "gear", "100", ""]
              }
            ]
            """);
        var prestigeRanks = DeserializeRequired<PrestigeRankEntry[]>(
            """
            [
              {
                "characterId": 5001,
                "characterName": "PrestigeOne",
                "rankingFields": ["prestige", "100"],
                "sourceFile": "PrestigeRanking.txt",
                "lineNumber": 2,
                "rawLine": "5001/PrestigeOne/prestige/100/",
                "fields": ["5001", "PrestigeOne", "prestige", "100", ""]
              }
            ]
            """);
        var innerPowerRanks = DeserializeRequired<InnerPowerRankEntry[]>(
            """
            [
              {
                "characterName": "InnerOne",
                "characterId": 5001,
                "rankingFields": ["inner", "100"],
                "sourceFile": "InnerPower.txt",
                "lineNumber": 2,
                "rawLine": "InnerOne/5001/inner/100/",
                "fields": ["InnerOne", "5001", "inner", "100", ""]
              }
            ]
            """);
        var runeRankings = DeserializeRequired<RuneRankingRecord[]>(
            """
            [
              {
                "characterName": "RuneOne",
                "score": 314800,
                "characterId": 5001,
                "sourceFile": "RuneRanking.txt",
                "lineNumber": 2,
                "rawLine": "RuneOne/314800/5001/",
                "fields": ["RuneOne", "314800", "5001", ""]
              }
            ]
            """);
        var staticStatues = DeserializeRequired<StaticStatueRecord[]>(
            """
            [
              {
                "recordId": 298,
                "values": [298, 34, 1, 179],
                "sourceFile": "StaticStatue.txt",
                "lineNumber": 2,
                "rawLine": "298/34/1/179/",
                "fields": ["298", "34", "1", "179", ""]
              }
            ]
            """);
        var newPlayerRewards = DeserializeRequired<NewPlayerRewardRecord[]>(
            """
            [
              {
                "ipAddress": "10.0.0.1",
                "deviceId": "device-a",
                "sourceFile": "NewPlayerRewords.txt",
                "lineNumber": 2,
                "rawLine": "10.0.0.1/device-a/",
                "fields": ["10.0.0.1", "device-a", ""]
              }
            ]
            """);
        var votes = DeserializeRequired<VoteRecord[]>(
            """
            [
              {
                "characterId": 7001,
                "ipAddress": "127.0.0.1",
                "timestamp": "638379291346047487",
                "sourceFile": "Votes.txt",
                "lineNumber": 2,
                "rawLine": "7001/127.0.0.1/638379291346047487/",
                "fields": ["7001", "127.0.0.1", "638379291346047487", ""]
              }
            ]
            """);
        var stateCountFiles = DeserializeRequired<StateCountFileData[]>(
            """
            [
              {
                "sourceFile": "Votes.txt",
                "declaredCount": 1,
                "entries": [
                  {
                    "sourceLine": 2,
                    "rawLine": "7001/127.0.0.1/638379291346047487/",
                    "fields": ["7001", "127.0.0.1", "638379291346047487", ""]
                  }
                ],
                "rawLines": ["Count=1", "7001/127.0.0.1/638379291346047487/"]
              }
            ]
            """);
        var binarySeedFiles = DeserializeRequired<BinarySeedFile[]>(
            """
            [
              {
                "sourceFile": "Insults.bin",
                "length": 4,
                "sha256": "df3f619804a92fdb4057192dc43dd748ea778adc52bc498ce80524c014b81119",
                "base64": "AAAAAA=="
              }
            ]
            """);
        var emptySeedFiles = DeserializeRequired<EmptySeedFile[]>(
            """
            [
              {
                "sourceFile": "HeroRewards.txt",
                "length": 0,
                "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                "base64": ""
              }
            ]
            """);
        var backupSeedFiles = DeserializeRequired<BackupSeedFile[]>(
            """
            [
              {
                "sourceFile": "SobNpcs.txt.bak",
                "length": 4,
                "sha256": "5abdb6175f820f0ac3d8647fbb1f7a0bcc91757a782a8a145570944ca6a00c96",
                "base64": "77u/MQ=="
              }
            ]
            """);
        var couplesPkRecords = DeserializeRequired<CouplesPkRecord[]>(
            """
            [
              {
                "playerName": "PlayerOne",
                "partnerName": "PartnerOne",
                "sourceFile": "CouplesPK.ini",
                "lineNumber": 2,
                "rawLine": "PlayerOne/PartnerOne/",
                "fields": ["PlayerOne", "PartnerOne", ""]
              }
            ]
            """);
        var socialAssociations = DeserializeRequired<SocialAssociationRecord[]>(
            """
            [
              {
                "ownerId": 1293116,
                "relationType": 4,
                "targetId": 1293600,
                "targetName": "Coder[GM]",
                "values": [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1541008, 100, 0],
                "note": "hey",
                "tailValue": 7000,
                "sourceFile": "Associate.txt",
                "lineNumber": 2,
                "rawLine": "1293116/4/0/0/0/1293600/0/0/0/0/Coder[GM]/0/0/0/0/1541008/100/0/hey/7000/",
                "fields": ["1293116", "4", "0", "0", "0", "1293600", "0", "0", "0", "0", "Coder[GM]", "0", "0", "0", "0", "1541008", "100", "0", "hey", "7000", ""]
              }
            ]
            """);
        var guildWarInfo = DeserializeRequired<GuildWarData>(
            """
            {
              "sourceFile": "GuildWarInfo.ini",
              "sections": [
                {
                  "name": "Info",
                  "sourceFile": "GuildWarInfo.ini",
                  "lineNumber": 1,
                  "rawLine": "[Info]",
                  "entries": []
                }
              ],
              "entries": [
                {
                  "sectionName": "Info",
                  "key": "ID",
                  "value": "1363",
                  "rawKey": "ID",
                  "rawValue": "1363",
                  "sourceFile": "GuildWarInfo.ini",
                  "lineNumber": 2,
                  "rawLine": "ID=1363"
                }
              ],
              "diagnostics": [],
              "info": {
                "id": 1363,
                "name": "GuildFixture",
                "leaderReward": 0,
                "deputiLeaderReward": 7,
                "leaderTop": { "1": 100 },
                "deputiTop": { "1": 50 }
              },
              "pole": {
                "name": "AttackPole",
                "hitPoints": 8000
              },
              "conductors": []
            }
            """);
        var superGuildWarInfo = DeserializeRequired<GuildWarData>(
            """
            {
              "sourceFile": "SuperGuildWarInfo.ini",
              "sections": [],
              "entries": [],
              "diagnostics": [],
              "info": {
                "id": 2000,
                "name": "SuperGuildFixture",
                "leaderReward": 0,
                "deputiLeaderReward": 7,
                "leaderTop": { "1": 100 },
                "deputiTop": { "1": 50 }
              },
              "pole": {
                "name": "AttackPole",
                "hitPoints": 16000
              },
              "conductors": []
            }
            """);
        var stageGoalInfo = DeserializeRequired<StageGoalInfo>(
            """
            {
              "stageAmount": 1
            }
            """);
        var stageGoalStages = DeserializeRequired<StageGoalStage[]>(
            """
            [
              {
                "stageId": 1,
                "openLevelRaw": "0001",
                "openLevel": 1,
                "stageAnimation": "Stage_01goalBtn",
                "goalCount": 1,
                "rewards": [
                  {
                    "position": 1,
                    "itemTypeId": 3005107,
                    "amount": 1,
                    "boundFlag": 0,
                    "key": "Prize1",
                    "rawValue": "3005107@@1@@0",
                    "sourceLine": 5,
                    "fields": ["3005107", "1", "0"]
                  }
                ],
                "sourceLine": 2,
                "entries": []
              }
            ]
            """);
        var stageGoalTasks = DeserializeRequired<StageGoalTask[]>(
            """
            [
              {
                "stageId": 1,
                "goalId": 1,
                "title": "[Novice] Level-up",
                "intro": "Upgrade to Level 20+.",
                "progress": 20,
                "rewards": [
                  {
                    "position": 1,
                    "itemTypeId": 723700,
                    "amount": 1,
                    "boundFlag": 1,
                    "key": "Prize1",
                    "rawValue": "723700@@1@@1",
                    "sourceLine": 12,
                    "fields": ["723700", "1", "1"]
                  }
                ],
                "taskType": 1,
                "requireRaw": null,
                "require": null,
                "sourceLine": 8,
                "entries": []
              }
            ]
            """);
        var shops = DeserializeRequired<ShopDefinition[]>(
            """
            [
              {
                "id": 3,
                "name": "Drugstore",
                "type": 1,
                "moneyType": 0,
                "sourceFile": "Shop.dat",
                "sourceDialect": "ShopDat",
                "attributes": {
                  "ItemAmount": "20"
                }
              }
            ]
            """);
        var shopItems = DeserializeRequired<ShopItem[]>(
            """
            [
              {
                "shopId": 2888,
                "itemTypeId": 102008,
                "position": 0,
                "category": "present",
                "status": "new",
                "isBound": true,
                "description": "This~armor~is~specially~designed~for~Thunderstrikers.",
                "flags": ["present", "new"],
                "sourceFile": "emoneyShopV2.ini",
                "sourceDialect": "EmoneyShopV2",
                "rawLine": "102008 present|new This~armor~is~specially~designed~for~Thunderstrikers.",
                "attributes": {}
              }
            ]
            """);
        var exchangeShopRows = DeserializeRequired<ExchangeShopRow[]>(
            """
            [
              {
                "sourceFile": "exchange_shop_goods.txt",
                "lineNumber": 1,
                "fields": ["444", "3303526", ""]
              }
            ]
            """);
        var honorShopItems = DeserializeRequired<HonorShopItem[]>(
            """
            [
              {
                "itemTypeId": 3303085,
                "cost": 3000,
                "unknown0": 0,
                "category": "item",
                "isRecommended": false,
                "status": "new",
                "position": 1,
                "sourceFile": "HonorShop.ini",
                "rawLine": "3303085,3000,0,item,,new",
                "fields": ["3303085", "3000", "0", "item", "", "new"]
              }
            ]
            """);
        var honorShopRecommendations = DeserializeRequired<HonorShopRecommendation[]>(
            """
            [
              {
                "itemTypeId": 3008101,
                "text": "Open~to~receive~a~Class~6~House~Pack.",
                "position": 0,
                "sourceFile": "HonorShop.ini",
                "rawLine": "3008101=Open~to~receive~a~Class~6~House~Pack."
              }
            ]
            """);
        var pointShopItems = DeserializeRequired<PointShopItem[]>(
            """
            [
              {
                "itemTypeId": 3304720,
                "cost": 15000,
                "itemClass": "item",
                "category": "other",
                "status": "new",
                "section": "Normal",
                "position": 0,
                "sourceFile": "RacePointShop.ini",
                "rawLine": "3304720 15000 item other new",
                "fields": ["3304720", "15000", "item", "other", "new"]
              }
            ]
            """);
        var pointShopRecommendations = DeserializeRequired<PointShopRecommendation[]>(
            """
            [
              {
                "itemTypeId": 3304720,
                "text": "Open~to~get~5~pills.",
                "section": "recommend",
                "position": 0,
                "sourceFile": "RacePointShop.ini",
                "rawLine": "3304720 Open~to~get~5~pills."
              }
            ]
            """);
        var goldenLeagueShopItems = DeserializeRequired<PointShopItem[]>(
            """
            [
              {
                "itemTypeId": 730001,
                "cost": 10,
                "itemClass": "item",
                "category": "equip",
                "status": "other",
                "section": "Normal",
                "position": 0,
                "sourceFile": "GoldenLeagueShop.ini",
                "rawLine": "730001 10 item equip other",
                "fields": ["730001", "10", "item", "equip", "other"]
              }
            ]
            """);
        var goldenLeagueShopRecommendations = DeserializeRequired<PointShopRecommendation[]>(
            """
            [
              {
                "itemTypeId": 3306562,
                "text": "Open~to~select~a~red~Rune~(B).",
                "section": "Normal",
                "position": 0,
                "sourceFile": "GoldenLeagueShop.ini",
                "rawLine": "3306562 Open~to~select~a~red~Rune~(B)."
              }
            ]
            """);
        var goldenLeagueShopConfig = DeserializeRequired<Dictionary<string, string>>(
            """
            {
              "PointLimit.GoldenLeaguePointLimit": "304400",
              "Color.TipColor": "0xffffffff"
            }
            """);
        var booths = DeserializeRequired<BoothDefinition[]>(
            """
            [
              {
                "id": 700050,
                "name": "Promoted",
                "mesh": 38194,
                "mapId": 1004,
                "x": 43,
                "y": 58,
                "declaredItemAmount": 99,
                "sourceSection": "booth700050",
                "sourceLine": 2,
                "position": 0,
                "sourceFile": "Booths.txt",
                "attributes": {
                  "ID": "700050",
                  "Name": "Promoted",
                  "ItemAmount": "99"
                }
              }
            ]
            """);
        var boothItems = DeserializeRequired<BoothItem[]>(
            """
            [
              {
                "boothId": 700050,
                "itemTypeId": 1080001,
                "price": 2000,
                "unknown0": 0,
                "tail": "0@0@0@0",
                "extraField": "",
                "itemKey": 1,
                "position": 1,
                "sourceFile": "Booths.txt",
                "sourceLine": 11,
                "rawLine": "Item1=1080001@@2000@@0@@0@0@0@0@@",
                "fields": ["1080001", "2000", "0", "0@0@0@0", ""]
              }
            ]
            """);
        var manifest = DeserializeRequired<SeedManifest>(
            """
            {
              "exportedAtUtc": "2026-06-02T02:35:00.2965322+00:00",
              "sourcePath": "fixture://DBServer",
              "counts": {
                "clientConfigs": 1,
                "items": 1,
                "itemTypeSubs": 1,
                "itemAddBonuses": 1,
                "levelExperience": 1,
                "classLevelStats": 1,
                "professionLevelUps": 1,
                "professionLevelBenefits": 1,
                "professionTitleBenefits": 1,
                "questInfo": 1,
                "questTaskTypes": 1,
                "questMissions": 1,
                "quizQuestions": 1,
                "magic": 1,
                "magicEffects": 1,
                "magicTypeOperations": 1,
                "titleTypes": 1,
                "combatGear": 1,
                "traps": 1,
                "npcs": 1,
                "sobNpcs": 1,
                "portals": 1,
                "maps": 1,
                "mapNames": 1,
                "legacyGameMaps": 1,
                "monsters": 1,
                "monsterSpawns": 1,
                "lotteryDictionary": 1,
                "collectionRewardTiers": 1,
                "taskRewardTypes": 1,
                "taskRewardTypeIniEntries": 1,
                "neiGongHeader": 1,
                "neiGongSecrets": 1,
                "neiGongTypes": 1,
                "subProfessions": 1,
                "syndicateLevels": 1,
                "runeLevelExperience": 1,
                "runeStorageAttributes": 1,
                "itemRefineEffects": 1,
                "itemRefineEffectEx": 1,
                "itemRefineUpgrades": 1,
                "transformInfo": 1,
                "furniture": 1,
                "hairColorTypes": 1,
                "hairFaceStorageTypes": 1,
                "coatStorageTypes": 1,
                "coatStorageAttributes": 1,
                "coatColorRules": 1,
                "xuanbaoAdditionAttributes": 1,
                "xuanbaoComposeAttributeLimits": 1,
                "xuanbaoRandomAttributes": 1,
                "christmasNpcs": 1,
                "tutorTypes": 1,
                "tutorBattleLimitTypes": 1,
                "beastAttributes": 1,
                "fateExperience": 1,
                "spiritRates": 1,
                "hundredWeapons": 1,
                "gouyuTypes": 1,
                "gouyuImmortals": 1,
                "specialEffectAttributes": 1,
                "daoqiDictTypes": 1,
                "syndicateFormationTypes": 1,
                "syndicateSkillTypes": 1,
                "disdainRules": 1,
                "jiangHuAttributes": 1,
                "jiangHuCultivateStatuses": 1,
                "refineryBoxes": 1,
                "refinerySouls": 1,
                "refineryMaterials": 1,
                "pokerTables": 1,
                "rouletteTables": 1,
                "npcTimeEntries": 1,
                "activityTasks": 1,
                "meltingTypeRules": 1,
                "lotteryPrizes": 1,
                "arenaRecords": 1,
                "teamArenaRecords": 1,
                "elitePkRecords": 1,
                "teamElitePkRecords": 1,
                "skillTeamPkRecords": 1,
                "classPkWarRecords": 1,
                "combatGearRanks": 1,
                "prestigeRanks": 1,
                "innerPowerRanks": 1,
                "runeRankings": 1,
                "staticStatues": 1,
                "newPlayerRewards": 1,
                "votes": 1,
                "stateCountFiles": 1,
                "binarySeedFiles": 1,
                "emptySeedFiles": 1,
                "backupSeedFiles": 1,
                "couplesPkRecords": 1,
                "socialAssociations": 1,
                "guildWarInfo": 1,
                "superGuildWarInfo": 1,
                "stageGoalInfo": 1,
                "stageGoalStages": 1,
                "stageGoalTasks": 1,
                "shops": 1,
                "shopItems": 1,
                "exchangeShopGoods": 1,
                "honorShopItems": 1,
                "honorShopRecommendations": 1,
                "racePointShopItems": 1,
                "racePointShopRecommendations": 1,
                "goldenLeagueShopItems": 1,
                "goldenLeagueShopRecommendations": 1,
                "goldenLeagueShopConfig": 2,
                "booths": 1,
                "boothItems": 1
              }
            }
            """);

        Assert.Equal("TestSrv", clientConfigs.Single().ServerName);
        Assert.Equal("0001", clientConfigs.Single().MapXRaw);
        Assert.Equal(9220, clientConfigs.Single().Port);
        Assert.Single(items);
        Assert.Equal(1_000_000, itemTypeSubs.Single().ItemTypeId);
        Assert.Equal("ItemtypeSub.txt", itemTypeSubs.Single().SourceFile);
        Assert.Equal(["1000000", "7", "Stancher", "0", ""], itemTypeSubs.Single().Fields);
        Assert.Equal([0, 0, 0, 4, 0, 2, 0, 0], itemAddBonuses.Single().Values);
        Assert.Equal(2_641_561_501_496, levelExperience.Single().Experience);
        Assert.Equal("Thunderstriker", classLevelStats.Single().ClassName);
        Assert.Equal(120, classLevelStats.Single().Level);
        Assert.Equal(144, classLevelStats.Single().Vitality);
        Assert.Equal(16051, professionLevelUps.Single().ProfessionCode);
        Assert.Equal(800_000, professionLevelUps.Single().RequiredProficiency);
        Assert.Equal([62600, 71200, 0, 0, 2600], professionLevelBenefits.Single().BonusValues);
        Assert.Equal([60500, 70100, 100100, 120100, 0], professionTitleBenefits.Single().BonusValues);
        Assert.Equal(20_000_000, questInfo.ActionBase);
        Assert.Equal("Recruit Quest", questTaskTypes.Single().Name);
        Assert.Equal(650, questMissions.Single().MissionId);
        Assert.Equal("Broadcast", quizQuestions.Single().CorrectAnswer);
        Assert.Equal(["Team", "Broadcast", "Guild", "Talk"], quizQuestions.Single().Options);
        Assert.Equal(5_002_142_000, magicEffects.Single().Id);
        Assert.Equal("", magicEffects.Single().Description);
        Assert.Equal("Desc", magicEffects.Single().Entries[1].Key);
        Assert.Equal(1, magicTypeOperations.Single().Id);
        Assert.Equal([1360, 1270, 1045], magicTypeOperations.Single().ReferencedMagicTypeIds);
        Assert.Equal(9, magicTypeOperations.Single().Values.Count);
        Assert.Equal(8043, titleTypes.Single().Id);
        Assert.Equal("CS Team PK Champion Halo", titleTypes.Single().Name);
        Assert.Equal(400, titleTypes.Single().Score);
        Assert.Equal("", titleTypes.Single().Fields[^1]);
        Assert.Equal(201, combatGear.Single().GearTypeId);
        Assert.Equal(300, combatGear.Single().UpExperience);
        Assert.Equal([17640000, 0, 0, 17650000, 0, 0, 0, 0, 0], combatGear.Single().MagicTypeIds);
        Assert.Equal("", combatGear.Single().Fields[^1]);
        Assert.Equal(44463, traps.Single().MapId);
        Assert.Equal(1618, traps.Single().TrapTypeId);
        Assert.Equal(1, traps.Single().OptionalFlag);
        Assert.Equal("44463,77,190,1618,1", traps.Single().RawLine);
        Assert.Equal(["44463", "77", "190", "1618", "1"], traps.Single().Fields);
        Assert.Equal(130006, questMissions.Single().PrizeItems.Single().ItemTypeId);
        Assert.Single(magic);
        Assert.Single(npcs);
        Assert.Single(sobNpcs);
        Assert.Equal("01021", sobNpcs.Single().Fields[0]);
        Assert.Equal("Archive", sobNpcs.Single().LocalizedName);
        Assert.Single(portals);
        Assert.Single(maps);
        Assert.Equal("8192", maps.Single().TypeRaw);
        Assert.Equal(4_294_967_295, maps.Single().Color);
        Assert.Single(mapNames);
        Assert.Equal(1002, mapNames.Single().MapId);
        Assert.Equal("CentralPlain", mapNames.Single().Name);
        Assert.Equal("GameMapEx.ini", mapNames.Single().SourceFile);
        Assert.Equal(13, mapNames.Single().SectionLine);
        Assert.Equal(14, mapNames.Single().NameLine);
        Assert.Equal("1002", mapNames.Single().SourceSection);
        Assert.Equal("Name=CentralPlain", mapNames.Single().RawNameLine);
        Assert.Single(legacyGameMaps);
        Assert.Equal(1999, legacyGameMaps.Single().MapId);
        Assert.Equal("map/map/icecrypt-lev3.7z", legacyGameMaps.Single().RawMapFile);
        Assert.Equal("map/map/icecrypt-lev3.dmap", legacyGameMaps.Single().MapFile);
        Assert.Equal(128, legacyGameMaps.Single().Extra);
        Assert.Single(monsters);
        Assert.Single(monsterSpawns);
        Assert.Equal(2179, monsterSpawns.Single().MonsterTypeId);
        Assert.Equal("1", monsterSpawns.Single().Attributes["return_mode"]);
        Assert.Equal(18007, lotteryDictionary.Single().Value);
        Assert.Equal("", lotteryDictionary.Single().Fields[^1]);
        Assert.Equal(11, collectionRewardTiers.Single().Id);
        Assert.Equal(700, collectionRewardTiers.Single().CollectionId);
        Assert.Equal(8400, collectionRewardTiers.Single().MinInclusive);
        Assert.Equal(2_147_483_647, collectionRewardTiers.Single().MaxInclusive);
        Assert.Equal(1, collectionRewardTiers.Single().RewardType);
        Assert.Equal(1500, collectionRewardTiers.Single().RewardValue);
        Assert.Equal(600, collectionRewardTiers.Single().RewardAmount);
        Assert.Equal("", collectionRewardTiers.Single().Fields[^1]);
        Assert.Equal(755, taskRewardTypes.Single().Id);
        Assert.Equal(3308803, taskRewardTypes.Single().RewardValues[2]);
        Assert.Equal("", taskRewardTypes.Single().Fields[^1]);
        Assert.Equal(3651, taskRewardTypeIniEntries.Single().Id);
        Assert.Equal("0002", taskRewardTypeIniEntries.Single().Fields[3]);
        Assert.Equal(98_579_161, taskRewardTypeIniEntries.Single().Extra1);
        Assert.Equal(1, neiGongHeader.SecretCount);
        Assert.Equal(new NeiGongAttributeTip(14, "Anti-break"), neiGongHeader.AttributeTips[1]);
        Assert.Equal("Universal~Concept", neiGongSecrets.Single().Name);
        Assert.Equal([7, 9], neiGongSecrets.Single().AttributeTypes);
        Assert.Equal(1, neiGongTypes.Single().TypeId);
        Assert.Equal([1500, 1500, 1500, 1500, 1500], neiGongTypes.Single().CultureValues);
        Assert.Equal(11, subProfessions.Single().Id);
        Assert.Equal([41, 31], subProfessions.Single().RequiredProfessions);
        Assert.Equal(2, subProfessions.Single().Entries.Count(entry => entry.Key == "req_joblv"));
        Assert.Equal(1, syndicateLevels.Single().Level);
        Assert.Equal([999, 2, 8, 10, 3, 3, 3, 1, 3, 5, 5], syndicateLevels.Single().Parameters);
        Assert.Equal("", syndicateLevels.Single().Fields[^1]);
        Assert.Equal(28, runeLevelExperience.Single().Id);
        Assert.Equal(402, runeLevelExperience.Single().RuneType);
        Assert.Equal(27, runeLevelExperience.Single().Level);
        Assert.Equal(341_500, runeLevelExperience.Single().Experience);
        Assert.Equal("", runeLevelExperience.Single().Fields[^1]);
        Assert.Equal(21, runeStorageAttributes.Single().Id);
        Assert.Equal(335_000, runeStorageAttributes.Single().RequiredStorageScore);
        Assert.Equal(new RuneStorageAttributeBonus(11, 300), runeStorageAttributes.Single().Bonuses[0]);
        Assert.Equal(new RuneStorageAttributeBonus(0, 0), runeStorageAttributes.Single().Bonuses[2]);
        Assert.Equal("", runeStorageAttributes.Single().Fields[^1]);
        Assert.Equal(648, itemRefineEffects.Single().Id);
        Assert.Equal(20, itemRefineEffects.Single().Values.Count);
        Assert.Equal(16_002, itemRefineEffects.Single().Values[^1]);
        Assert.Equal("", itemRefineEffects.Single().Fields[^1]);
        Assert.Equal(2_100_095, itemRefineEffectEx.Single().ItemTypeId);
        Assert.Equal([100, 5, 7], itemRefineEffectEx.Single().Values);
        Assert.Equal("", itemRefineEffectEx.Single().Fields[^1]);
        Assert.Equal(54, itemRefineUpgrades.Single().Level);
        Assert.Equal(12_000, itemRefineUpgrades.Single().Value);
        Assert.Equal("", itemRefineUpgrades.Single().Fields[^1]);
        Assert.Equal(1350, transformInfo.Single().TransformId);
        Assert.Equal(3, transformInfo.Single().Level);
        Assert.Equal("DivineHare", transformInfo.Single().Name);
        Assert.Equal(267, transformInfo.Single().Lookface);
        Assert.Equal(3000, transformInfo.Single().MaxLife);
        Assert.Equal(["1350", "3", "DivineHare", "267", "3000"], transformInfo.Single().Fields);
        Assert.Equal(8924, furniture.Single().Id);
        Assert.Equal(26, furniture.Single().Kind);
        Assert.Equal(837, furniture.Single().Lookface);
        Assert.Equal(1511, furniture.Single().MapId);
        Assert.Equal(143, furniture.Single().X);
        Assert.Equal(130, furniture.Single().Y);
        Assert.Equal("FlowerDressingTable", furniture.Single().Name);
        Assert.Equal(726083, furniture.Single().ItemTypeId);
        Assert.Equal(23_000, furniture.Single().Price);
        Assert.Equal(78, hairColorTypes.Single().HairColorId);
        Assert.Equal(3_326_778, hairColorTypes.Single().ItemTypeId);
        Assert.Equal("YouthSpark", hairFaceStorageTypes.Single().Name);
        Assert.Equal("", hairFaceStorageTypes.Single().LinkedColorIdsRaw);
        Assert.Equal(181_305, coatStorageTypes.Single().ItemTypeId);
        Assert.Equal(31, coatStorageTypes.Single().StorageType);
        Assert.Equal([1, 0, 0, 0, 0, 1], coatStorageTypes.Single().Parameters);
        Assert.Equal(17, coatStorageAttributes.Single().Id);
        Assert.Equal(new CosmeticAttributeBonus(3, 300), coatStorageAttributes.Single().Bonuses[0]);
        Assert.Equal(200_412, coatColorRules.Single().ItemTypeId);
        Assert.Equal(18, coatColorRules.Single().Parameters.Count);
        Assert.Equal(14, xuanbaoAdditionAttributes.Single().AttributeType);
        Assert.Equal([0, 50], xuanbaoAdditionAttributes.Single().Values);
        Assert.Equal([0, 4, 400], xuanbaoComposeAttributeLimits.Single().Values);
        Assert.Equal(4_100_001, xuanbaoRandomAttributes.Single().ItemTypeId);
        Assert.Equal(101, xuanbaoRandomAttributes.Single().MaxValue);
        Assert.Null(christmasNpcs.Single().Name);
        Assert.Equal([0, 0, 1, 1], christmasNpcs.Single().Parameters);
        Assert.Equal(70, tutorTypes.Single().MinLevel);
        Assert.Equal([2, 10], tutorTypes.Single().Values);
        Assert.Equal(5, tutorBattleLimitTypes.Single().BattlePowerLimit);
        Assert.Equal(1, beastAttributes.Single().Level);
        Assert.Equal([2400, 200, 200], beastAttributes.Single().Parameters.Take(3).ToArray());
        Assert.Equal(500, fateExperience.Single().Experience);
        Assert.Equal(3_300_576, fateExperience.Single().Values[^1]);
        Assert.Equal(4_200_001, spiritRates.Single().ItemTypeId);
        Assert.Equal([0, 0, 5, 0, 0], spiritRates.Single().Parameters);
        Assert.Equal(410, hundredWeapons.Single().WeaponType);
        Assert.Equal([0, 100, 10100], hundredWeapons.Single().Values.Take(3).ToArray());
        Assert.Equal(100, gouyuTypes.Single().Type);
        Assert.Equal([50, 10_537, 0], gouyuTypes.Single().Values.Take(3).ToArray());
        Assert.Equal(8, gouyuImmortals.Single().Values[0]);
        Assert.Equal(40_701_011, specialEffectAttributes.Single().Id);
        Assert.Equal([10, 10, 100], specialEffectAttributes.Single().Values.Take(3).ToArray());
        Assert.Equal(113, daoqiDictTypes.Single().Id);
        Assert.Equal([100, 0, 0], daoqiDictTypes.Single().Values.Take(3).ToArray());
        Assert.Equal(120_000_000L, syndicateFormationTypes.Single().Values[1]);
        Assert.Equal(75_050, syndicateSkillTypes.Single().Values[1]);
        Assert.Equal(-90, disdainRules.Single().Values[6]);
        Assert.Equal(500, jiangHuAttributes.Single().Value);
        Assert.Equal([10, 20, 30], jiangHuCultivateStatuses.Single().Values);
        Assert.Equal([1, 2, 3], refineryBoxes.Single().Values);
        Assert.Equal("WeaponSoul", refinerySouls.Single().EquipmentName);
        Assert.Equal("Necklace", refineryMaterials.Single().Target);
        Assert.Equal([7, 0, 0, 0, 13_010, 500], refineryMaterials.Single().TailValues);
        Assert.Equal("013", pokerTables.Single().Fields[1]);
        Assert.Equal(3002, rouletteTables.Single().Values[1]);
        Assert.Equal("Arena", npcTimeEntries.Single().Label);
        Assert.Equal(1, activityTasks.Metadata.DeclaredCount);
        Assert.Equal("0141", activityTasks.Tasks.Single().CloseLevelRaw);
        Assert.Equal(141, activityTasks.Tasks.Single().CloseLevel);
        Assert.Equal([0, 0, 1], meltingTypeRules.Single().Parameters);
        Assert.Equal("WavyHeavyRing+8", lotteryPrizes.Single().ItemName);
        Assert.Equal("151159", lotteryPrizes.Single().ItemTypeRaw);
        Assert.Equal("ArenaOne", arenaRecords.Single().PlayerName);
        Assert.Equal(4000, arenaRecords.Single().Prize);
        Assert.Equal("TeamArenaOne", teamArenaRecords.Single().PlayerName);
        Assert.Equal(4000, teamArenaRecords.Single().Points);
        Assert.Equal("EliteOne", elitePkRecords.Single().PlayerName);
        Assert.Equal("TeamOne", teamElitePkRecords.Single().PlayerName);
        Assert.Equal("SkillOne", skillTeamPkRecords.Single().PlayerName);
        Assert.Equal(7001, classPkWarRecords.Single().PlayerId);
        Assert.Equal("RankOne", combatGearRanks.Single().CharacterName);
        Assert.Equal("PrestigeOne", prestigeRanks.Single().CharacterName);
        Assert.Equal("InnerOne", innerPowerRanks.Single().CharacterName);
        Assert.Equal(314_800, runeRankings.Single().Score);
        Assert.Equal(5001, runeRankings.Single().CharacterId);
        Assert.Equal(298, staticStatues.Single().RecordId);
        Assert.Equal([298, 34, 1, 179], staticStatues.Single().Values);
        Assert.Equal("device-a", newPlayerRewards.Single().DeviceId);
        Assert.Equal("638379291346047487", votes.Single().Timestamp);
        Assert.Equal("Votes.txt", stateCountFiles.Single().SourceFile);
        Assert.Equal(1, stateCountFiles.Single().DeclaredCount);
        Assert.Equal("127.0.0.1", stateCountFiles.Single().Entries.Single().Fields[1]);
        Assert.Equal("Insults.bin", binarySeedFiles.Single().SourceFile);
        Assert.Equal(4, binarySeedFiles.Single().Length);
        Assert.Equal("df3f619804a92fdb4057192dc43dd748ea778adc52bc498ce80524c014b81119", binarySeedFiles.Single().Sha256);
        Assert.Equal("AAAAAA==", binarySeedFiles.Single().Base64);
        Assert.Equal("HeroRewards.txt", emptySeedFiles.Single().SourceFile);
        Assert.Equal(0, emptySeedFiles.Single().Length);
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", emptySeedFiles.Single().Sha256);
        Assert.Equal("", emptySeedFiles.Single().Base64);
        Assert.Equal("SobNpcs.txt.bak", backupSeedFiles.Single().SourceFile);
        Assert.Equal(4, backupSeedFiles.Single().Length);
        Assert.Equal("5abdb6175f820f0ac3d8647fbb1f7a0bcc91757a782a8a145570944ca6a00c96", backupSeedFiles.Single().Sha256);
        Assert.Equal("77u/MQ==", backupSeedFiles.Single().Base64);
        Assert.Equal("PartnerOne", couplesPkRecords.Single().PartnerName);
        Assert.Equal("Coder[GM]", socialAssociations.Single().TargetName);
        Assert.Equal("hey", socialAssociations.Single().Note);
        Assert.Equal(7000, socialAssociations.Single().TailValue);
        Assert.Equal(1363, guildWarInfo.Info?.Id);
        Assert.Equal(2000, superGuildWarInfo.Info?.Id);
        Assert.Equal(1, stageGoalInfo.StageAmount);
        Assert.Equal("0001", stageGoalStages.Single().OpenLevelRaw);
        Assert.Equal(723700, stageGoalTasks.Single().Rewards.Single().ItemTypeId);
        Assert.Equal("ShopDat", shops.Single().SourceDialect);
        Assert.Equal("20", shops.Single().Attributes["ItemAmount"]);
        Assert.Equal("present", shopItems.Single().Category);
        Assert.Equal("new", shopItems.Single().Status);
        Assert.True(shopItems.Single().IsBound);
        Assert.Equal("", exchangeShopRows.Single().Fields[^1]);
        Assert.Equal(["3303085", "3000", "0", "item", "", "new"], honorShopItems.Single().Fields);
        Assert.Equal("Open~to~receive~a~Class~6~House~Pack.", honorShopRecommendations.Single().Text);
        Assert.Equal("Normal", pointShopItems.Single().Section);
        Assert.Equal("Open~to~get~5~pills.", pointShopRecommendations.Single().Text);
        Assert.Equal("equip", goldenLeagueShopItems.Single().Category);
        Assert.Equal("other", goldenLeagueShopItems.Single().Status);
        Assert.Equal("Open~to~select~a~red~Rune~(B).", goldenLeagueShopRecommendations.Single().Text);
        Assert.Equal("0xffffffff", goldenLeagueShopConfig["Color.TipColor"]);
        Assert.Equal(700050, booths.Single().Id);
        Assert.Equal(99, booths.Single().DeclaredItemAmount);
        Assert.Equal("", boothItems.Single().ExtraField);
        Assert.Equal(DateTimeOffset.Parse("2026-06-02T02:35:00.2965322+00:00"), manifest.ExportedAtUtc);
        Assert.Equal("fixture://DBServer", manifest.SourcePath);
        Assert.Equal(1, manifest.Counts["clientConfigs"]);
        Assert.Equal(1, manifest.Counts["items"]);
        Assert.Equal(1, manifest.Counts["itemTypeSubs"]);
        Assert.Equal(1, manifest.Counts["itemAddBonuses"]);
        Assert.Equal(1, manifest.Counts["levelExperience"]);
        Assert.Equal(1, manifest.Counts["classLevelStats"]);
        Assert.Equal(1, manifest.Counts["professionLevelUps"]);
        Assert.Equal(1, manifest.Counts["professionLevelBenefits"]);
        Assert.Equal(1, manifest.Counts["professionTitleBenefits"]);
        Assert.Equal(1, manifest.Counts["questInfo"]);
        Assert.Equal(1, manifest.Counts["questTaskTypes"]);
        Assert.Equal(1, manifest.Counts["questMissions"]);
        Assert.Equal(1, manifest.Counts["quizQuestions"]);
        Assert.Equal(1, manifest.Counts["magic"]);
        Assert.Equal(1, manifest.Counts["magicEffects"]);
        Assert.Equal(1, manifest.Counts["magicTypeOperations"]);
        Assert.Equal(1, manifest.Counts["titleTypes"]);
        Assert.Equal(1, manifest.Counts["combatGear"]);
        Assert.Equal(1, manifest.Counts["traps"]);
        Assert.Equal(1, manifest.Counts["sobNpcs"]);
        Assert.Equal(1, manifest.Counts["maps"]);
        Assert.Equal(1, manifest.Counts["mapNames"]);
        Assert.Equal(1, manifest.Counts["legacyGameMaps"]);
        Assert.Equal(1, manifest.Counts["monsters"]);
        Assert.Equal(1, manifest.Counts["monsterSpawns"]);
        Assert.Equal(1, manifest.Counts["lotteryDictionary"]);
        Assert.Equal(1, manifest.Counts["collectionRewardTiers"]);
        Assert.Equal(1, manifest.Counts["taskRewardTypes"]);
        Assert.Equal(1, manifest.Counts["taskRewardTypeIniEntries"]);
        Assert.Equal(1, manifest.Counts["neiGongHeader"]);
        Assert.Equal(1, manifest.Counts["neiGongSecrets"]);
        Assert.Equal(1, manifest.Counts["neiGongTypes"]);
        Assert.Equal(1, manifest.Counts["subProfessions"]);
        Assert.Equal(1, manifest.Counts["syndicateLevels"]);
        Assert.Equal(1, manifest.Counts["runeLevelExperience"]);
        Assert.Equal(1, manifest.Counts["runeStorageAttributes"]);
        Assert.Equal(1, manifest.Counts["itemRefineEffects"]);
        Assert.Equal(1, manifest.Counts["itemRefineEffectEx"]);
        Assert.Equal(1, manifest.Counts["itemRefineUpgrades"]);
        Assert.Equal(1, manifest.Counts["transformInfo"]);
        Assert.Equal(1, manifest.Counts["furniture"]);
        Assert.Equal(1, manifest.Counts["hairColorTypes"]);
        Assert.Equal(1, manifest.Counts["hairFaceStorageTypes"]);
        Assert.Equal(1, manifest.Counts["coatStorageTypes"]);
        Assert.Equal(1, manifest.Counts["coatStorageAttributes"]);
        Assert.Equal(1, manifest.Counts["coatColorRules"]);
        Assert.Equal(1, manifest.Counts["xuanbaoAdditionAttributes"]);
        Assert.Equal(1, manifest.Counts["xuanbaoComposeAttributeLimits"]);
        Assert.Equal(1, manifest.Counts["xuanbaoRandomAttributes"]);
        Assert.Equal(1, manifest.Counts["christmasNpcs"]);
        Assert.Equal(1, manifest.Counts["tutorTypes"]);
        Assert.Equal(1, manifest.Counts["tutorBattleLimitTypes"]);
        Assert.Equal(1, manifest.Counts["beastAttributes"]);
        Assert.Equal(1, manifest.Counts["fateExperience"]);
        Assert.Equal(1, manifest.Counts["spiritRates"]);
        Assert.Equal(1, manifest.Counts["hundredWeapons"]);
        Assert.Equal(1, manifest.Counts["gouyuTypes"]);
        Assert.Equal(1, manifest.Counts["gouyuImmortals"]);
        Assert.Equal(1, manifest.Counts["specialEffectAttributes"]);
        Assert.Equal(1, manifest.Counts["daoqiDictTypes"]);
        Assert.Equal(1, manifest.Counts["syndicateFormationTypes"]);
        Assert.Equal(1, manifest.Counts["syndicateSkillTypes"]);
        Assert.Equal(1, manifest.Counts["disdainRules"]);
        Assert.Equal(1, manifest.Counts["jiangHuAttributes"]);
        Assert.Equal(1, manifest.Counts["jiangHuCultivateStatuses"]);
        Assert.Equal(1, manifest.Counts["refineryBoxes"]);
        Assert.Equal(1, manifest.Counts["refinerySouls"]);
        Assert.Equal(1, manifest.Counts["refineryMaterials"]);
        Assert.Equal(1, manifest.Counts["pokerTables"]);
        Assert.Equal(1, manifest.Counts["rouletteTables"]);
        Assert.Equal(1, manifest.Counts["npcTimeEntries"]);
        Assert.Equal(1, manifest.Counts["activityTasks"]);
        Assert.Equal(1, manifest.Counts["meltingTypeRules"]);
        Assert.Equal(1, manifest.Counts["lotteryPrizes"]);
        Assert.Equal(1, manifest.Counts["arenaRecords"]);
        Assert.Equal(1, manifest.Counts["teamArenaRecords"]);
        Assert.Equal(1, manifest.Counts["elitePkRecords"]);
        Assert.Equal(1, manifest.Counts["teamElitePkRecords"]);
        Assert.Equal(1, manifest.Counts["skillTeamPkRecords"]);
        Assert.Equal(1, manifest.Counts["classPkWarRecords"]);
        Assert.Equal(1, manifest.Counts["combatGearRanks"]);
        Assert.Equal(1, manifest.Counts["prestigeRanks"]);
        Assert.Equal(1, manifest.Counts["innerPowerRanks"]);
        Assert.Equal(1, manifest.Counts["runeRankings"]);
        Assert.Equal(1, manifest.Counts["staticStatues"]);
        Assert.Equal(1, manifest.Counts["newPlayerRewards"]);
        Assert.Equal(1, manifest.Counts["votes"]);
        Assert.Equal(1, manifest.Counts["stateCountFiles"]);
        Assert.Equal(1, manifest.Counts["binarySeedFiles"]);
        Assert.Equal(1, manifest.Counts["emptySeedFiles"]);
        Assert.Equal(1, manifest.Counts["backupSeedFiles"]);
        Assert.Equal(1, manifest.Counts["couplesPkRecords"]);
        Assert.Equal(1, manifest.Counts["socialAssociations"]);
        Assert.Equal(1, manifest.Counts["guildWarInfo"]);
        Assert.Equal(1, manifest.Counts["superGuildWarInfo"]);
        Assert.Equal(1, manifest.Counts["stageGoalInfo"]);
        Assert.Equal(1, manifest.Counts["stageGoalStages"]);
        Assert.Equal(1, manifest.Counts["stageGoalTasks"]);
        Assert.Equal(1, manifest.Counts["shopItems"]);
        Assert.Equal(1, manifest.Counts["honorShopItems"]);
        Assert.Equal(1, manifest.Counts["racePointShopItems"]);
        Assert.Equal(1, manifest.Counts["goldenLeagueShopItems"]);
        Assert.Equal(2, manifest.Counts["goldenLeagueShopConfig"]);
        Assert.Equal(1, manifest.Counts["booths"]);
        Assert.Equal(1, manifest.Counts["boothItems"]);
    }

    [Fact]
    public void SerializesBoundFlagForEmoneyShopV2PresentShopItems()
    {
        const string content = """
        ID=2888
        MoneyType=1
        102008 present|new This~armor~is~specially~designed~for~Thunderstrikers.
        """;
        var item = Assert.Single(ShopParser.ParseEmoneyShopV2(content).Items);

        var json = JsonSerializer.Serialize(new[] { item }, SeedJsonOptions);

        Assert.Contains("\"isBound\":true", json);
    }

    [Fact]
    public void ItemTypeSubSeedJsonPreservesEmptyLegacyFileAsEmptyArray()
    {
        var entries = ItemTypeSubParser.Parse("", "ItemtypeSub.txt");

        var json = JsonSerializer.Serialize(entries, SeedJsonOptions);

        Assert.Equal("[]", json);
    }

    [Fact]
    public void PreservesRawFieldsAndMonsterStringAttributes()
    {
        var items = DeserializeRequired<ItemType[]>(
            """
            [
              {
                "id": 50000,
                "name": "SpeedArrow",
                "fields": ["50000", "SpeedArrow", "0", "0010", ""]
              }
            ]
            """);
        var magic = DeserializeRequired<MagicType[]>(
            """
            [
              {
                "id": 10,
                "typeId": 10,
                "name": "HuntingPunch",
                "fields": ["10", "10", "1", "HuntingPunch", "0001"]
              }
            ]
            """);
        var monsters = DeserializeRequired<Monster[]>(
            """
            [
              {
                "id": 4711,
                "name": "300-StarWarDevil",
                "level": 145,
                "attributes": {
                  "id": "4711",
                  "name": "300-StarWarDevil",
                  "type": "0001",
                  "lookface": "0915",
                  "drop_money": "0005"
                },
                "specialDrops": [
                  {
                    "itemTypeId": 721538,
                    "chanceOrCount": 4
                  }
                ]
              }
            ]
            """);

        Assert.Equal(["50000", "SpeedArrow", "0", "0010", ""], items.Single().Fields);
        Assert.Equal(["10", "10", "1", "HuntingPunch", "0001"], magic.Single().Fields);

        var monster = monsters.Single();
        Assert.Equal("0001", monster.Attributes["type"]);
        Assert.Equal("0915", monster.Attributes["lookface"]);
        Assert.Equal("0005", monster.Attributes["drop_money"]);

        var drop = Assert.Single(monster.SpecialDrops);
        Assert.Equal(721538, drop.ItemTypeId);
        Assert.Equal(4, drop.ChanceOrCount);
    }

    [Fact]
    public void DeserializesNpcOptionalFlagWhenPresentOmittedOrNull()
    {
        var npcs = DeserializeRequired<Npc[]>(
            """
            [
              {
                "id": 200124,
                "kind": 2,
                "lookface": 45310,
                "mapId": 44462,
                "x": 354,
                "y": 286,
                "optionalFlag": 1
              },
              {
                "id": 200002,
                "kind": 2,
                "lookface": 51700,
                "mapId": 44460,
                "x": 334,
                "y": 193
              },
              {
                "id": 20571,
                "kind": 2,
                "lookface": 47770,
                "mapId": 10137,
                "x": 568,
                "y": 372,
                "optionalFlag": null
              }
            ]
            """);

        Assert.Equal(1, npcs[0].OptionalFlag);
        Assert.Null(npcs[1].OptionalFlag);
        Assert.Null(npcs[2].OptionalFlag);
    }

    private static T DeserializeRequired<T>(string json)
    {
        var value = JsonSerializer.Deserialize<T>(json, SeedJsonOptions);

        Assert.NotNull(value);
        return value;
    }

}
