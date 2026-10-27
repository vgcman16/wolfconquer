using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterPersistenceStarterTests : IDisposable
{
    private const uint AccountKey = 1_293_600;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-character-persistence-starter-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<StarterCase> StarterProfiles
    {
        get
        {
            var profiles = new TheoryData<StarterCase>
            {
                new(
                    ClassSelection: 0,
                    ExpectedClass: 10_000,
                    ExpectedStrength: 0,
                    ExpectedAgility: 2,
                    ExpectedVitality: 3,
                    ExpectedSpirit: 5,
                    ExpectedHitpoints: 93,
                    ExpectedMana: 25,
                    ExpectedItemIds: [132_009, 421_301, 152_005],
                    ExpectedItemPositions: [3, 4, 6],
                    ExpectedSpellIds: [1000, 1005]),
                new(
                    ClassSelection: 2,
                    ExpectedClass: 1_000,
                    ExpectedStrength: 5,
                    ExpectedAgility: 2,
                    ExpectedVitality: 3,
                    ExpectedSpirit: 0,
                    ExpectedHitpoints: 93,
                    ExpectedMana: 0,
                    ExpectedItemIds: [132_009, 420_301, 420_301, 150_003],
                    ExpectedItemPositions: [3, 4, 5, 6],
                    // MsgLoginHandler.cs:392-394 appends Cyclone after the tuned creation projection.
                    ExpectedSpellIds: [1046, 1110]),
                new(
                    ClassSelection: 4,
                    ExpectedClass: 4_000,
                    ExpectedStrength: 2,
                    ExpectedAgility: 7,
                    ExpectedVitality: 1,
                    ExpectedSpirit: 0,
                    ExpectedHitpoints: 51,
                    ExpectedMana: 0,
                    ExpectedItemIds: [132_009, 500_006, 150_003],
                    ExpectedItemPositions: [3, 4, 6],
                    ExpectedSpellIds: [8002]),
                new(
                    ClassSelection: 6,
                    ExpectedClass: 2_000,
                    ExpectedStrength: 5,
                    ExpectedAgility: 2,
                    ExpectedVitality: 3,
                    ExpectedSpirit: 0,
                    ExpectedHitpoints: 93,
                    ExpectedMana: 0,
                    ExpectedItemIds: [132_009, 561_301, 150_003],
                    ExpectedItemPositions: [3, 4, 6],
                    ExpectedSpellIds: [1020, 1015, 1040]),
                new(
                    ClassSelection: 8,
                    ExpectedClass: 5_000,
                    ExpectedStrength: 1,
                    ExpectedAgility: 8,
                    ExpectedVitality: 1,
                    ExpectedSpirit: 0,
                    ExpectedHitpoints: 51,
                    ExpectedMana: 0,
                    ExpectedItemIds: [132_009, 601_301, 601_301, 150_003],
                    ExpectedItemPositions: [3, 4, 5, 6],
                    ExpectedSpellIds: [6001]),
                new(
                    ClassSelection: 10,
                    ExpectedClass: 6_000,
                    ExpectedStrength: 3,
                    ExpectedAgility: 1,
                    ExpectedVitality: 4,
                    ExpectedSpirit: 2,
                    ExpectedHitpoints: 114,
                    ExpectedMana: 10,
                    ExpectedItemIds: [132_009, 610_301, 610_301, 150_003],
                    ExpectedItemPositions: [3, 4, 5, 6],
                    ExpectedSpellIds: [10390]),
                new(
                    ClassSelection: 12,
                    ExpectedClass: 7_000,
                    ExpectedStrength: 3,
                    ExpectedAgility: 1,
                    ExpectedVitality: 4,
                    ExpectedSpirit: 2,
                    ExpectedHitpoints: 114,
                    ExpectedMana: 10,
                    ExpectedItemIds: [132_009, 611_301, 612_301, 150_003],
                    ExpectedItemPositions: [3, 4, 5, 6],
                    ExpectedSpellIds: [11050]),
                new(
                    ClassSelection: 14,
                    ExpectedClass: 8_000,
                    ExpectedStrength: 3,
                    ExpectedAgility: 1,
                    ExpectedVitality: 4,
                    ExpectedSpirit: 2,
                    ExpectedHitpoints: 114,
                    ExpectedMana: 10,
                    ExpectedItemIds: [138_313, 617_301, 617_301, 150_003],
                    ExpectedItemPositions: [3, 4, 5, 6],
                    ExpectedSpellIds: [12210]),
                new(
                    ClassSelection: 16,
                    ExpectedClass: 16_000,
                    ExpectedStrength: 5,
                    ExpectedAgility: 2,
                    ExpectedVitality: 3,
                    ExpectedSpirit: 0,
                    ExpectedHitpoints: 93,
                    ExpectedMana: 0,
                    ExpectedItemIds: [101_319, 626_301, 626_301, 150_003],
                    ExpectedItemPositions: [3, 4, 5, 6],
                    ExpectedSpellIds: [12870]),
                new(
                    ClassSelection: 20,
                    ExpectedClass: 9_000,
                    ExpectedStrength: 5,
                    ExpectedAgility: 2,
                    ExpectedVitality: 3,
                    ExpectedSpirit: 0,
                    ExpectedHitpoints: 93,
                    ExpectedMana: 0,
                    ExpectedItemIds: [102_313, 681_301, 680_301, 150_003],
                    ExpectedItemPositions: [3, 4, 5, 6],
                    // Matching MsgNewRole.cs:356-381 has no 15760 grant; keep the tuned base spell.
                    ExpectedSpellIds: [15670]),
            };
            return profiles;
        }
    }

    [Theory]
    [MemberData(nameof(StarterProfiles))]
    public async Task LegacyNewRoleCreatesBoundedSourceShapedStarterForEveryClassFamily(StarterCase starter)
    {
        var usersRoot = UsersRoot();
        using var log = new StringWriter();
        var loader = new CharacterSnapshotLoader(usersRoot);
        var handler = new WorldServerFrameHandler(loader, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(AccountKey).ToFrameBytes()));
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyNewRolePacket(
                "Starter" + starter.ClassSelection.ToString(CultureInfo.InvariantCulture),
                1008,
                starter.ClassSelection).ToFrameBytes()));

        var snapshot = loader.LoadByAccountId(CharacterUid);
        Assert.NotNull(snapshot);
        Assert.Equal(starter.ExpectedClass, snapshot!.Class);
        Assert.Equal(1, snapshot.Level);
        Assert.NotEqual(100, snapshot.Level);
        Assert.Equal(0, snapshot.Experience);
        Assert.Equal(starter.ExpectedStrength, snapshot.Strength);
        Assert.Equal(starter.ExpectedAgility, snapshot.Agility);
        Assert.Equal(starter.ExpectedVitality, snapshot.Vitality);
        Assert.Equal(starter.ExpectedSpirit, snapshot.Spirit);
        Assert.Equal(starter.ExpectedHitpoints, snapshot.MinHitPoints);
        Assert.InRange(snapshot.MinHitPoints, 50, 200);
        Assert.Equal(starter.ExpectedMana, snapshot.MinMana);
        Assert.Equal(starter.ExpectedMana, snapshot.MaxMana);
        Assert.Equal(100_000, snapshot.Money);
        Assert.Equal(0, snapshot.ConquerPoints);
        Assert.Equal(1002, snapshot.MapId);
        Assert.Equal(410, snapshot.X);
        Assert.Equal(350, snapshot.Y);

        var playerItems = loader.LoadPlayerItemsByCharacterUid(CharacterUid);
        Assert.Empty(playerItems.InventoryItems);
        Assert.Equal(
            starter.ExpectedItemIds,
            playerItems.EquipmentItems.Select(item => item.ItemId).ToArray());
        Assert.Equal(
            starter.ExpectedItemPositions,
            playerItems.EquipmentItems.Select(item => item.Position).ToArray());
        Assert.All(playerItems.EquipmentItems, item =>
        {
            Assert.Equal(1, item.Durability);
            Assert.Equal(1, item.MaximumDurability);
            Assert.Equal(0, item.Bound);
            Assert.InRange(item.Color, 3U, 8U);
        });

        var playerSpells = loader.LoadPlayerSpellsByCharacterUid(CharacterUid);
        Assert.Equal(starter.ExpectedSpellIds, playerSpells.SpellIds.ToArray());
        Assert.Equal(
            CreateSpellSnapshot(starter.ExpectedSpellIds.Select(id => checked((ushort)id)).ToArray()).Spells,
            playerSpells.Spells);
        Assert.Equal(playerSpells.Spells, GetSession(handler, connection).PlayerSpells.Spells);
        var playerProficienciesPath = Path.Combine(
            _root,
            "PlayersProfs",
            CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        Assert.True(File.Exists(playerProficienciesPath));
        Assert.Equal(sizeof(int), new FileInfo(playerProficienciesPath).Length);
        Assert.Empty(loader.LoadPlayerProficienciesByCharacterUid(CharacterUid).Proficiencies);
        Assert.Contains("sourceInventoryPacksSkipped=2", log.ToString());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(20)]
    public async Task LegacyNewRolePostStartupStarterSpellsRemainExactAcrossRelog(ushort classSelection)
    {
        var expectedClass = classSelection == 2 ? 1_000L : 9_000L;
        var expectedSpells = classSelection == 2
            ? CreateSpellSnapshot(1046, 1110)
            : CreateSpellSnapshot(15670);
        var loader = new CharacterSnapshotLoader(UsersRoot());
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(loader, log);
        var connection = CreateSendingConnection([]);
        await handler.HandleFrameAsync(connection,
            ReadFrame(new LegacyMsgLoginGamePacket(AccountKey).ToFrameBytes()));
        await handler.HandleFrameAsync(connection,
            ReadFrame(new LegacyNewRolePacket("RelogStarter" + classSelection, 1008, classSelection).ToFrameBytes()));

        Assert.Equal(expectedSpells.Spells, loader.LoadPlayerSpellsByCharacterUid(CharacterUid).Spells);
        Assert.Equal(expectedSpells.Spells, GetSession(handler, connection).PlayerSpells.Spells);
        var spellPath = Path.Combine(_root, "PlayersSpells", CharacterUid + ".bin");
        var originalBytes = File.ReadAllBytes(spellPath);
        var relogLoader = new CharacterSnapshotLoader(UsersRoot());
        using var relogLog = new StringWriter();
        var relogHandler = new WorldServerFrameHandler(relogLoader, relogLog);
        var relogConnection = CreateSendingConnection([]);
        await relogHandler.HandleFrameAsync(relogConnection,
            ReadFrame(new LegacyMsgLoginGamePacket(AccountKey).ToFrameBytes()));

        var relogSession = GetSession(relogHandler, relogConnection);
        Assert.Equal(expectedClass, relogSession.Character.Class);
        Assert.Equal(1, relogSession.Character.Level);
        Assert.Equal(93, relogSession.Character.MinHitPoints);
        Assert.Equal(100_000, relogSession.Character.Money);
        Assert.Equal(0, relogSession.Character.ConquerPoints);
        Assert.Equal(expectedSpells.Spells, relogSession.PlayerSpells.Spells);
        Assert.Equal(expectedSpells.Spells, relogLoader.LoadPlayerSpellsByCharacterUid(CharacterUid).Spells);
        Assert.Equal(originalBytes, File.ReadAllBytes(spellPath));
        Assert.Empty(relogSession.PlayerProficiencies.Proficiencies);
    }

    [Fact]
    public async Task LegacyNewRoleCreatesCanonicalEmptyCombatGearAndQuestSidecars()
    {
        var usersRoot = UsersRoot();
        using var log = new StringWriter();
        var loader = new CharacterSnapshotLoader(usersRoot);
        var handler = new WorldServerFrameHandler(loader, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(AccountKey).ToFrameBytes()));
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyNewRolePacket("SidecarStarter", 1008, 2).ToFrameBytes()));

        var combatGearPath = Path.Combine(
            _root,
            "PlayersCombatGear",
            CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini");
        var questPath = Path.Combine(
            _root,
            "Quests",
            CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var expectedCombatGear = string.Join(
            Environment.NewLine,
            "[CombatGear]",
            "MasteryPoints=0",
            "TaoistReiki=0",
            "PirateExp=0",
            "Lottery=0",
            "Count=0",
            "[AirPowers]",
            "SenseLevel=0",
            "ArmedLevel=0",
            "OverLoadLevel=0",
            "Points=0",
            "SenseAipPower=0",
            "ArmedAipPower=0",
            "OverLoadAipPower=0",
            "[BlackJadeBag]",
            "Count=0",
            string.Empty);

        Assert.True(File.Exists(combatGearPath));
        Assert.True(File.Exists(questPath));
        Assert.Equal(Encoding.UTF8.GetBytes(expectedCombatGear), File.ReadAllBytes(combatGearPath));
        Assert.Equal([0, 0, 0, 0], File.ReadAllBytes(questPath));

        var reloaded = new CharacterSnapshotLoader(usersRoot);
        var reloadedCombatGear = reloaded.LoadCombatGearByCharacterUid(CharacterUid);
        Assert.Equal(0U, reloadedCombatGear.MasteryPoints);
        Assert.Equal(0U, reloadedCombatGear.TaoistReiki);
        Assert.Equal(0U, reloadedCombatGear.PirateExp);
        Assert.Equal(0U, reloadedCombatGear.Lottery);
        Assert.Equal(0, reloadedCombatGear.Count);
        Assert.Equal(CombatGearAirPowersSnapshot.Empty, reloadedCombatGear.AirPowers);
        Assert.Empty(reloadedCombatGear.Items);
        Assert.Equal(0, reloadedCombatGear.BlackJadeBag.Count);
        Assert.Empty(reloadedCombatGear.BlackJadeBag.BlackJades);
        Assert.Empty(reloaded.LoadPlayerQuestsByCharacterUid(CharacterUid).Quests);
    }

    [Fact]
    public async Task LegacyExistingRoleLoginDoesNotCreateFirstCharacterSidecars()
    {
        var usersRoot = UsersRoot();
        WriteSnapshot(
            usersRoot,
            CharacterUid,
            name: "Existing",
            classCode: 1_000,
            level: 1,
            experience: 0,
            mapId: 1_002,
            x: 410,
            y: 350,
            strength: 5,
            agility: 2,
            vitality: 3,
            spirit: 0,
            minHitPoints: 93,
            money: 100_000);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(AccountKey).ToFrameBytes()));

        Assert.False(File.Exists(Path.Combine(_root, "PlayersCombatGear", CharacterUid + ".ini")));
        Assert.False(File.Exists(Path.Combine(_root, "Quests", CharacterUid + ".bin")));
    }

    [Theory]
    [InlineData(1005, 1069)]
    [InlineData(1058, 1069)]
    [InlineData(1059, 930)]
    public void StarterHitpointsMatchLegacyGameClientUpperTrojanBounds(long classCode, uint expected)
    {
        var hitpoints = LegacyFirstCharacterStarterProfiles.CalculateHitpoints(
            classCode,
            strength: 10,
            agility: 20,
            vitality: 30,
            spirit: 40);

        Assert.Equal(expected, hitpoints);
    }

    [Theory]
    [InlineData(13051, 7, 210)]
    [InlineData(13057, 7, 210)]
    [InlineData(14051, 7, 210)]
    [InlineData(14057, 7, 210)]
    [InlineData(14058, 7, 35)]
    public void StarterManaMatchesLegacyGameClientTaoistBounds(long classCode, long spirit, long expected)
    {
        var mana = LegacyFirstCharacterStarterProfiles.CalculateMana(classCode, spirit);

        Assert.Equal(expected, mana);
    }

    [Fact]
    public async Task OrdinarySavedLoginPreservesLevel100TrojanEconomyItemsAndLearnedSpellsAcrossRelog()
    {
        // The helper deletes only after confirmed owner drains; failed fixtures are retained.
        var fixture = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        WriteSnapshot(fixture.Users, LegacyOwnedAdmissionRuntimeFixture.Uid,
            name: "SavedTrojan", classCode: 1_000, level: 100, experience: 0,
            mapId: 1_002, x: 410, y: 350, strength: 155, agility: 60,
            vitality: 92, spirit: 0, minHitPoints: 2_853, money: 10_000_000);

        await AssertSavedAdmissionWithOwnerAcrossRelogAsync(fixture,
            CreateSourceBoostedTrojanItems(), CreateSpellSnapshot(1045, 1046, 1110),
            expectedClass: 1_000, expectedLevel: 100, expectedMoney: 10_000_000,
            expectedConquerPoints: 50_000, proficiencyId: 420);
    }

    [Fact]
    public async Task OrdinarySavedLoginPreservesAllSixWarriorLearnedSpellsWithoutInferringBirthFromLevelOne()
    {
        // The helper deletes only after confirmed owner drains; failed fixtures are retained.
        var fixture = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        WriteSnapshot(fixture.Users, LegacyOwnedAdmissionRuntimeFixture.Uid,
            name: "SavedWarrior", classCode: 2_000, level: 1, experience: 0,
            mapId: 1_002, x: 410, y: 350, strength: 5, agility: 2,
            vitality: 3, spirit: 0, minHitPoints: 93, money: 100_000);

        await AssertSavedAdmissionWithOwnerAcrossRelogAsync(fixture,
            CreateTunedWarriorStarterItems(), CreateSpellSnapshot(1025, 1045, 1046, 1020, 1015, 1040),
            expectedClass: 2_000, expectedLevel: 1, expectedMoney: 100_000,
            expectedConquerPoints: 0, proficiencyId: 561);
    }

    [Fact]
    public async Task JumpPositionPersistenceRoundTripsAcrossRelog()
    {
        var usersRoot = UsersRoot();
        WriteSnapshot(
            usersRoot,
            CharacterUid,
            name: "Traveler",
            classCode: 1_000,
            level: 55,
            experience: 12_345,
            mapId: 1_015,
            x: 830,
            y: 584,
            strength: 155,
            agility: 60,
            vitality: 92,
            spirit: 0,
            minHitPoints: 2_853,
            money: 12_000_000);
        using var log = new StringWriter();
        var loader = new CharacterSnapshotLoader(usersRoot);
        var handler = new WorldServerFrameHandler(loader, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(AccountKey).ToFrameBytes()));
        var beforeJump = GetSession(handler, connection).Character;
        Assert.Equal(1_015, beforeJump.MapId);
        Assert.Equal(830, beforeJump.X);
        Assert.Equal(584, beforeJump.Y);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgActionPacket
            {
                ObjId = AccountKey,
                Timestamp = 987,
                Type = MsgActionType.Jump,
                Facing = 6,
                TargetPositionX = 840,
                TargetPositionY = 590,
            }.ToFrameBytes()));

        var afterJump = GetSession(handler, connection).Character;
        Assert.Equal(1_015, afterJump.MapId);
        Assert.Equal(840, afterJump.X);
        Assert.Equal(590, afterJump.Y);

        var persisted = loader.LoadByAccountId(CharacterUid);
        Assert.NotNull(persisted);
        Assert.Equal(1_015, persisted!.MapId);
        Assert.Equal(840, persisted.X);
        Assert.Equal(590, persisted.Y);

        using var reloadLog = new StringWriter();
        var reloadHandler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), reloadLog);
        var reloadSent = new List<byte[]>();
        var reloadConnection = CreateSendingConnection(reloadSent);
        await reloadHandler.HandleFrameAsync(
            reloadConnection,
            ReadFrame(new LegacyMsgLoginGamePacket(AccountKey).ToFrameBytes()));

        var reloaded = GetSession(reloadHandler, reloadConnection).Character;
        Assert.Equal(1_015, reloaded.MapId);
        Assert.Equal(840, reloaded.X);
        Assert.Equal(590, reloaded.Y);
        Assert.Contains("WorldServer handled MsgAction Jump", log.ToString());
    }

    [Fact]
    public async Task JumpPositionPersistenceFailureLogIncludesSelectedAccountAndCharacterUid()
    {
        var usersRoot = UsersRoot();
        WriteSnapshot(
            usersRoot,
            CharacterUid,
            name: "Traveler",
            classCode: 1_000,
            level: 55,
            experience: 12_345,
            mapId: 1_015,
            x: 830,
            y: 584,
            strength: 155,
            agility: 60,
            vitality: 92,
            spirit: 0,
            minHitPoints: 2_853,
            money: 12_000_000);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(AccountKey).ToFrameBytes()));
        File.Delete(Path.Combine(usersRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini"));

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgActionPacket
            {
                ObjId = AccountKey,
                Timestamp = 987,
                Type = MsgActionType.Jump,
                Facing = 6,
                TargetPositionX = 840,
                TargetPositionY = 590,
            }.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("PARITY-BUG character scalar persistence failed", output);
        Assert.Contains("accountId=1293600", output);
        Assert.Contains("uid=1293600", output);
        Assert.Contains("file=1293600.ini", output);
        Assert.Contains("reason=snapshotMissing", output);
    }

    [Fact]
    public async Task LegacyTeleportPositionPersistenceRoundTripsAcrossRelog()
    {
        var usersRoot = UsersRoot();
        WriteSnapshot(
            usersRoot,
            CharacterUid,
            name: "Traveler",
            classCode: 1_000,
            level: 55,
            experience: 12_345,
            mapId: 1_015,
            x: 830,
            y: 584,
            strength: 155,
            agility: 60,
            vitality: 92,
            spirit: 0,
            minHitPoints: 2_853,
            money: 12_000_000);
        using var log = new StringWriter();
        var loader = new CharacterSnapshotLoader(usersRoot);
        var handler = new WorldServerFrameHandler(loader, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(AccountKey).ToFrameBytes()));
        var beforeTeleport = GetSession(handler, connection).Character;
        Assert.Equal(1_015, beforeTeleport.MapId);
        Assert.Equal(830, beforeTeleport.X);
        Assert.Equal(584, beforeTeleport.Y);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyVipHandlerPacket(
                LegacyVipTeleportType.SelfTeleport,
                LegacyVipTeleportLocation.PhoenixCastle,
                Countdown: 0,
                Name: string.Empty).ToFrameBytes()));

        var afterTeleport = GetSession(handler, connection).Character;
        Assert.Equal(1_011, afterTeleport.MapId);
        Assert.Equal(184, afterTeleport.X);
        Assert.Equal(275, afterTeleport.Y);
        Assert.Equal(1_015, afterTeleport.PreviousMapId);
        Assert.Equal(830, afterTeleport.PreviousMapX);
        Assert.Equal(584, afterTeleport.PreviousMapY);

        var persisted = loader.LoadByAccountId(CharacterUid);
        Assert.NotNull(persisted);
        Assert.Equal(1_011, persisted!.MapId);
        Assert.Equal(184, persisted.X);
        Assert.Equal(275, persisted.Y);
        Assert.Equal(1_015, persisted.PreviousMapId);
        Assert.Equal(830, persisted.PreviousMapX);
        Assert.Equal(584, persisted.PreviousMapY);

        using var reloadLog = new StringWriter();
        var reloadHandler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), reloadLog);
        var reloadSent = new List<byte[]>();
        var reloadConnection = CreateSendingConnection(reloadSent);
        await reloadHandler.HandleFrameAsync(
            reloadConnection,
            ReadFrame(new LegacyMsgLoginGamePacket(AccountKey).ToFrameBytes()));

        var reloaded = GetSession(reloadHandler, reloadConnection).Character;
        Assert.Equal(1_011, reloaded.MapId);
        Assert.Equal(184, reloaded.X);
        Assert.Equal(275, reloaded.Y);
        Assert.Equal(1_015, reloaded.PreviousMapId);
        Assert.Equal(830, reloaded.PreviousMapX);
        Assert.Equal(584, reloaded.PreviousMapY);
        Assert.Contains("WorldServer applied LegacyVipHandler teleport", log.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string UsersRoot()
    {
        return Path.Combine(_root, "Users");
    }

    private static PlayerItemSnapshot CreateSourceBoostedTrojanItems()
    {
        return new PlayerItemSnapshot(
            InventoryItems:
            [
                CreateItem(3_000_550, position: 0, uidOffset: 10),
                CreateItem(3_001_027, position: 0, uidOffset: 11),
            ],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                CreateItem(132_009, position: 3, uidOffset: 0),
                CreateItem(420_301, position: 4, uidOffset: 1),
                CreateItem(420_301, position: 5, uidOffset: 2),
                CreateItem(150_003, position: 6, uidOffset: 3),
            ],
            RuneItems: []);
    }

    private static PlayerItemSnapshot CreateTunedWarriorStarterItems()
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                CreateItem(132_009, position: 3, uidOffset: 0),
                CreateItem(561_301, position: 4, uidOffset: 1),
                CreateItem(150_003, position: 6, uidOffset: 2),
            ],
            RuneItems: []);
    }

    private static PlayerItemSnapshotItem CreateItem(uint itemId, ushort position, uint uidOffset)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = checked((uint)CharacterUid + 100 + uidOffset),
            ItemId = itemId,
            Position = position,
            Durability = 1,
            MaximumDurability = 1,
        };
    }

    private static PlayerSpellSnapshot CreateSpellSnapshot(params ushort[] spellIds)
    {
        return new PlayerSpellSnapshot(
            spellIds
                .Select(spellId => new PlayerSpellSnapshotEntry(
                    spellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0))
                .ToArray());
    }

    private static void RewriteAsSourceBoostedTrojanSnapshot(string usersRoot)
    {
        var snapshotPath = Path.Combine(usersRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini");
        var sourceBoosted = File.ReadAllText(snapshotPath)
            .Replace("Level=1", "Level=100", StringComparison.Ordinal)
            .Replace("Strength=5", "Strength=155", StringComparison.Ordinal)
            .Replace("Agility=2", "Agility=60", StringComparison.Ordinal)
            .Replace("Vitaliti=3", "Vitaliti=92", StringComparison.Ordinal)
            .Replace("MinHitPoints=93", "MinHitPoints=2853", StringComparison.Ordinal)
            .Replace("ConquerPoints=0\r\nBankCps=0", "ConquerPoints=50000\r\nBankCps=0", StringComparison.Ordinal)
            .Replace("ConquerPoints=0\nBankCps=0", "ConquerPoints=50000\nBankCps=0", StringComparison.Ordinal)
            .Replace("Money=100000", "Money=10000000", StringComparison.Ordinal);
        File.WriteAllText(snapshotPath, sourceBoosted);
    }

    private static void WriteSnapshot(
        string usersRoot,
        long uid,
        string name,
        long classCode,
        long level,
        long experience,
        long mapId,
        long x,
        long y,
        long strength,
        long agility,
        long vitality,
        long spirit,
        long minHitPoints,
        long money)
    {
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, uid.ToString(CultureInfo.InvariantCulture) + ".ini"),
            string.Create(
                CultureInfo.InvariantCulture,
                $$"""
                [Character]
                UID={{uid}}
                Body=1008
                Face=154
                Name={{name}}
                Class={{classCode}}
                Map={{mapId}}
                X={{x}}
                Y={{y}}
                FirstClass=0
                SecoundeClass=0
                Conquer=0
                Reborn=0
                Level={{level}}
                Haire=27
                Experience={{experience}}
                PMap=1000
                PMapX=500
                PMapY=650
                Agility={{agility}}
                Strength={{strength}}
                Vitaliti={{vitality}}
                Spirit={{spirit}}
                Atributes=0
                MinHitPoints={{minHitPoints}}
                MinMana=0
                MaxMana=0
                ConquerPoints=0
                BankCps=0
                BoundConquerPoints=0
                Money={{money}}
                GuildDontion=0
                PkPoints=0
                Online=0
                ExpBallUsed=0
                Day=1
                DonationNobility=0

                [PaidNobility]
                Rank=Serf
                Identifier=0
                LastNobilityDonation=0
                PaidRank=0
                PeriodTime=0
                IsActive=False
                Position=-1
                """));
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static WorldSession GetSession(WorldServerFrameHandler handler, ConquerTcpConnection connection)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var sessions = Assert.IsType<WorldSessionStore>(field.GetValue(handler));
        Assert.True(sessions.TryGet(connection, out var session));
        Assert.NotNull(session);
        return session;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    public sealed record StarterCase(
        ushort ClassSelection,
        long ExpectedClass,
        long ExpectedStrength,
        long ExpectedAgility,
        long ExpectedVitality,
        long ExpectedSpirit,
        long ExpectedHitpoints,
        long ExpectedMana,
        IReadOnlyList<uint> ExpectedItemIds,
        IReadOnlyList<ushort> ExpectedItemPositions,
        IReadOnlyList<uint> ExpectedSpellIds);
}
