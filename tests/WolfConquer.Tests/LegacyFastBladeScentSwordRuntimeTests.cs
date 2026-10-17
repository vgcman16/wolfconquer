using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint ExpectedManualLineMonsterRuntimeDamage = 207_015;
    private const uint ExpectedManualLineMonsterAnimationDamage = ExpectedManualLineMonsterRuntimeDamage * 2;
    private const uint ExpectedManualLinePlayerDamage = 235;
    private const uint ExpectedManualLineSobNpcDamage = 90_345;
    private const uint ManualLineProficiencyId = 421;

    [Theory]
    [InlineData(FastBladeSpellId)]
    [InlineData(ScentSwordSpellId)]
    public async Task HandlerFastBladeScentSwordManualLineHitsMixedTargetsWithSourceDamageProgressAndSelfEcho(
        ushort spellId)
    {
        var spellsRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-manual-line-spell-xp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            "PlayersSpells");
        var proficienciesRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-manual-line-proficiency-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        try
        {
            var monsterFamily = CreateHighLifeMonsterFamily();
            var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 834, boundY: 584);
            var nearLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 838, boundY: 585);
            var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 838, boundY: 587);
            var rangeEdgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 841, boundY: 584);
            var sobNpc = CreateManualLineSobNpc(id: 65_345, x: 836, y: 586);
            var worldData = CreateMagicCatalogCore(
                monsterFamily,
                [onLineSpawn, nearLineSpawn, outsideWidthSpawn, rangeEdgeSpawn],
                [sobNpc],
                [
                    CreateManualLineSpellRow(spellId, experience: 100),
                    CreateManualLineSpellRow(spellId, level: 1, experience: 200),
                ]);
            var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
            var nearLineMonster = CreateRuntimeMonsterInstance(worldData, nearLineSpawn, monsterFamily);
            var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
            var rangeEdgeMonster = CreateRuntimeMonsterInstance(worldData, rangeEdgeSpawn, monsterFamily);
            var monsterStates = new MonsterRuntimeStateStore();
            var sobNpcStates = new SobNpcRuntimeStateStore();
            var sessions = new WorldSessionStore();
            var casterSent = new List<byte[]>();
            var targetSent = new List<byte[]>();
            using var log = new StringWriter();
            var casterConnection = CreateSendingConnection(casterSent);
            var targetConnection = CreateSendingConnection(targetSent);
            var initialProficiency = new PlayerProficiencySnapshotEntry(
                ManualLineProficiencyId,
                Level: 5,
                PreviousLevel: 0,
                Experience: 123,
                NeededExperience: LegacyProficiencyPacket.GetNeededExperience(5));
            var playerItems = CreateManualLinePlayerItems();
            var casterSession = CreateSession(
                strength: 300,
                spirit: 300,
                stamina: 100,
                spellIds: [spellId],
                playerItems: playerItems,
                playerProficiencies: new PlayerProficiencySnapshot([initialProficiency]));
            var targetSession = CreateSession(
                uid: TargetPlayerUid,
                name: "Target",
                x: 838,
                y: 585,
                minHitPoints: 100_000);
            sessions.Set(casterConnection, casterSession);
            sessions.Set(targetConnection, targetSession);
            var context = new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                SobNpcStates: sobNpcStates,
                PlayerSpellsRoot: spellsRoot,
                PlayerProficienciesRoot: proficienciesRoot);

            await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                    onLineMonster,
                    legacyEncodedQuery: false,
                    spellId: spellId,
                    x: 842,
                    y: 584)).ToFrameBytes()));

            var casterSpells = ParseSpellAnimations(casterSent);
            Assert.Equal(2, casterSpells.Count);
            var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
            foreach (var spell in casterSpells.Append(targetSpell))
            {
                AssertManualLineAnimationHeader(spell, spellId, x: 842, y: 584);
            }

            var damageByUid = casterSpells[0].Targets.ToDictionary(target => target.Uid, target => target.Damage);
            Assert.Equal(
                new[]
                {
                    onLineMonster.Uid,
                    nearLineMonster.Uid,
                    TargetPlayerUid,
                    checked((uint)sobNpc.Id),
                }.Order().ToArray(),
                damageByUid.Keys.Order().ToArray());
            Assert.Equal(ExpectedManualLineMonsterAnimationDamage, damageByUid[onLineMonster.Uid]);
            Assert.Equal(ExpectedManualLineMonsterAnimationDamage, damageByUid[nearLineMonster.Uid]);
            Assert.Equal(ExpectedManualLinePlayerDamage, damageByUid[TargetPlayerUid]);
            Assert.Equal(ExpectedManualLineSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);
            Assert.Equal(
                casterSpells[0].Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
                casterSpells[1].Targets.Select(target => (target.Uid, target.Damage)).ToArray());
            Assert.Equal(
                casterSpells[0].Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
                targetSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray());

            Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
            Assert.True(monsterStates.TryGetVisibleInstance(nearLineMonster, DateTimeOffset.UtcNow, out var visibleNearLine));
            Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideWidth));
            Assert.True(monsterStates.TryGetVisibleInstance(rangeEdgeMonster, DateTimeOffset.UtcNow, out var visibleRangeEdge));
            Assert.Equal(onLineMonster.MaxLife - ExpectedManualLineMonsterRuntimeDamage, visibleOnLine.Hitpoints);
            Assert.Equal(nearLineMonster.MaxLife - ExpectedManualLineMonsterRuntimeDamage, visibleNearLine.Hitpoints);
            Assert.Equal(outsideWidthMonster.MaxLife, visibleOutsideWidth.Hitpoints);
            Assert.Equal(rangeEdgeMonster.MaxLife, visibleRangeEdge.Hitpoints);

            Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
            Assert.NotNull(updatedTargetSession);
            Assert.Equal(100_000 - ExpectedManualLinePlayerDamage, updatedTargetSession.Character.MinHitPoints);
            Assert.Equal(sobNpc.Life - checked((int)ExpectedManualLineSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

            var staminaUpdateIndexes = Enumerable.Range(0, casterSent.Count)
                .Where(index => LegacyMsgUpdatePacket.TryParse(ReadFrame(casterSent[index]), out var update) &&
                    update.Uid == PlayerUid &&
                    update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 85)]))
                .ToArray();
            Assert.NotEmpty(staminaUpdateIndexes);
            var firstSpellIndex = Enumerable.Range(0, casterSent.Count)
                .First(index => LegacySpellAnimationPacket.TryParse(ReadFrame(casterSent[index]), out _));
            Assert.True(staminaUpdateIndexes[0] < firstSpellIndex);
            Assert.Equal(
                [new LegacyMagicColdTimeSpell(spellId, Time: 300)],
                Assert.Single(ParseMagicColdTimePackets(casterSent)).Spells);

            Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
            Assert.NotNull(updatedCasterSession);
            Assert.Equal("85", updatedCasterSession.Character.Attributes["Stamina"]);
            var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
            Assert.Equal(spellId, updatedSpell.Id);
            Assert.Equal(1, updatedSpell.Level);
            Assert.Equal(0, updatedSpell.Experience);

            var expectedProficiency = LegacyProficiencyPacket.ApplyExperience(
                initialProficiency,
                checked((ExpectedManualLineMonsterRuntimeDamage * 2 + ExpectedManualLineSobNpcDamage) *
                    SourceDefaultExpRateProf)).Proficiency;
            Assert.Contains(ParseProficiencyPackets(casterSent), packet =>
                packet.Id == expectedProficiency.Id &&
                packet.Level == expectedProficiency.Level &&
                packet.Experience == expectedProficiency.Experience);
            Assert.Equal([expectedProficiency], updatedCasterSession.PlayerProficiencies.Proficiencies);
            var persistedProficiency = new PlayerProficiencySnapshotLoader(proficienciesRoot)
                .LoadByCharacterUid(PlayerUid);
            Assert.Equal([expectedProficiency], persistedProficiency.Proficiencies);
        }
        finally
        {
            var spellTempRoot = Path.GetDirectoryName(spellsRoot);
            if (!string.IsNullOrWhiteSpace(spellTempRoot) && Directory.Exists(spellTempRoot))
            {
                Directory.Delete(spellTempRoot, recursive: true);
            }

            if (Directory.Exists(proficienciesRoot))
            {
                Directory.Delete(proficienciesRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(FastBladeSpellId)]
    [InlineData(ScentSwordSpellId)]
    public async Task HandlerFastBladeScentSwordManualLineSkipsGuardMonstersAndStillHitsPlayerAndSobTargets(
        ushort spellId)
    {
        var guardFamily = CreateGuardMonsterFamily() with
        {
            Attributes = new Dictionary<string, string>(CreateGuardMonsterFamily().Attributes)
            {
                ["life"] = "500000",
            },
        };
        var guardSpawn = CreateSingleInstanceMonsterSpawn(
            boundX: 834,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var sobNpc = CreateManualLineSobNpc(id: 65_346, x: 836, y: 586);
        var worldData = CreateMagicCatalogCore(
            guardFamily,
            [guardSpawn],
            [sobNpc],
            [CreateManualLineSpellRow(spellId)]);
        var guard = CreateRuntimeMonsterInstance(worldData, guardSpawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellIds: [spellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 838,
            y: 585,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                guard,
                legacyEncodedQuery: false,
                spellId: spellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        var casterSpells = ParseSpellAnimations(casterSent);
        Assert.Equal(2, casterSpells.Count);
        Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in casterSpells)
        {
            AssertManualLineAnimationHeader(spell, spellId, x: 842, y: 584);
        }

        var damageByUid = casterSpells[0].Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(ExpectedManualLinePlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(ExpectedManualLineSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);

        Assert.True(monsterStates.TryGetVisibleInstance(guard, DateTimeOffset.UtcNow, out var visibleGuard));
        Assert.Equal(guard.MaxLife, visibleGuard.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - ExpectedManualLinePlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)ExpectedManualLineSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);
    }

    [Theory]
    [InlineData(FastBladeSpellId)]
    [InlineData(ScentSwordSpellId)]
    public async Task HandlerFastBladeScentSwordMonsterDamageAppliesMonsterHunterBonus(ushort spellId)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 834, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateManualLineSpellRow(spellId),
            CreateMagicRow(MonsterHunterSpellId, sort: 1, useMana: 0, damage: 30_300, coldTime: 0));
        var baselineDamage = await RunFastBladeScentSwordMonsterDamageAsync(
            worldData,
            monsterFamily,
            spawn,
            spellId,
            CreateSession(strength: 300, stamina: 100, spellIds: [spellId]));
        var boostedDamage = await RunFastBladeScentSwordMonsterDamageAsync(
            worldData,
            monsterFamily,
            spawn,
            spellId,
            CreateSession(
                strength: 300,
                stamina: 100,
                spellEntries:
                [
                    CreatePlayerSpell(spellId),
                    CreatePlayerSpell(MonsterHunterSpellId),
                ]));

        Assert.True(
            boostedDamage > baselineDamage,
            $"Expected MonsterHunter to add source Physical.OnMonster damage percent. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Theory]
    [InlineData(FastBladeSpellId)]
    [InlineData(ScentSwordSpellId)]
    public async Task HandlerFastBladeScentSwordManualLineShapeMissStillPreparesCooldownAndSendsEmptySelfEcho(
        ushort spellId)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 845, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateManualLineSpellRow(spellId));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var peerConnection = CreateSendingConnection(peerSent);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellIds: [spellId]);
        var peerSession = CreateSession(uid: TargetPlayerUid, name: "Peer", x: 827, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(peerConnection, peerSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: spellId,
                x: 830,
                y: 595)).ToFrameBytes()));

        var casterSpells = ParseSpellAnimations(casterSent);
        Assert.Equal(2, casterSpells.Count);
        var peerSpell = Assert.Single(ParseSpellAnimations(peerSent));
        foreach (var spell in casterSpells.Append(peerSpell))
        {
            AssertManualLineAnimationHeader(spell, spellId, x: 830, y: 595);
            Assert.Empty(spell.Targets);
        }

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        Assert.True(sessions.TryGet(casterConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("85", updatedSession.Character.Attributes["Stamina"]);
        Assert.NotNull(updatedSession.MagicCooldowns);
        Assert.True(updatedSession.MagicCooldowns.ContainsKey(spellId));
        Assert.Equal(
            [new LegacyMagicColdTimeSpell(spellId, Time: 300)],
            Assert.Single(ParseMagicColdTimePackets(casterSent)).Spells);
    }

    [Theory]
    [InlineData(FastBladeSpellId)]
    [InlineData(ScentSwordSpellId)]
    public async Task HandlerFastBladeScentSwordManualLineWidthIncludesCasterOrigin(
        ushort spellId)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var behindCasterSpawn = CreateSingleInstanceMonsterSpawn(boundX: 829, boundY: 584);
        var outsideOriginWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 827, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [behindCasterSpawn, outsideOriginWidthSpawn],
            CreateManualLineSpellRow(spellId));
        var behindCasterMonster = CreateRuntimeMonsterInstance(worldData, behindCasterSpawn, monsterFamily);
        var outsideOriginWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideOriginWidthSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, stamina: 100, spellIds: [spellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                behindCasterMonster,
                legacyEncodedQuery: false,
                spellId: spellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent).Take(1));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(behindCasterMonster.Uid, target.Uid);
        Assert.Equal(ExpectedManualLineMonsterAnimationDamage, target.Damage);

        Assert.True(monsterStates.TryGetVisibleInstance(behindCasterMonster, DateTimeOffset.UtcNow, out var visibleBehindCaster));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideOriginWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideOriginWidth));
        Assert.Equal(behindCasterMonster.MaxLife - ExpectedManualLineMonsterRuntimeDamage, visibleBehindCaster.Hitpoints);
        Assert.Equal(outsideOriginWidthMonster.MaxLife, visibleOutsideOriginWidth.Hitpoints);

        Assert.Contains("source=Line.cs case 1045/1046", log.ToString());
    }

    [Theory]
    [InlineData(13_450)]
    [InlineData(13_460)]
    [InlineData(18_003)]
    public async Task HandlerFastBladeScentSwordLineSourceSilentSpendsResourcesWithoutAnimationOrVisibleMonsterDamage(
        ushort spellId)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateSourceSilentLineSpellRow(spellId));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, stamina: 100, spellIds: [spellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: spellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.Contains(sent, frame =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 80)]));
        Assert.Empty(ParseMagicColdTimePackets(sent));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("80", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(updatedSession.MagicCooldowns is null || !updatedSession.MagicCooldowns.ContainsKey(spellId));

        var output = log.ToString();
        Assert.Contains("WorldServer handled source-silent Line magic", output);
        Assert.Contains("source=Line.cs missing case after CanUseSpell.Verified.", output);
    }

    private static MagicType CreateManualLineSpellRow(
        ushort spellId,
        byte level = 0,
        uint experience = 0)
    {
        return CreateMagicRow(
            spellId,
            sort: 96,
            level: level,
            experience: experience,
            useMana: 0,
            damage: level == 0 ? 30_115U : 30_120U,
            maxTargets: 11,
            range: 11,
            weaponType: spellId == FastBladeSpellId ? 614_410U : 614_420_620U,
            useStamina: 15,
            coolDown: 600,
            coldTime: 300);
    }

    private static MagicType CreateSourceSilentLineSpellRow(ushort spellId)
    {
        return CreateMagicRow(
            spellId,
            sort: 14,
            useMana: 0,
            damage: 120,
            maxTargets: 11,
            range: 15,
            weaponType: spellId == 13_450 ? 614_410U : 614_420_620U,
            useStamina: 20,
            coolDown: 600,
            coldTime: 0);
    }

    private static SobNpc CreateManualLineSobNpc(int id, int x, int y)
    {
        return new SobNpc(
            Id: id,
            Name: "ManualLinePole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: 1_015,
            X: x,
            Y: y,
            Life: 500_000,
            MaxLife: 500_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 11,
            RawLine: $"{id},ManualLinePole,10,1137,1015,{x},{y},500000,500000,17,0",
            Fields:
            [
                id.ToString(CultureInfo.InvariantCulture),
                "ManualLinePole",
                "10",
                "1137",
                "1015",
                x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture),
                "500000",
                "500000",
                "17",
                "0",
            ]);
    }

    private static PlayerItemSnapshot CreateManualLinePlayerItems()
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    ItemId = 421_003,
                    Durability = 100,
                    Position = 4,
                },
            ],
            RuneItems: []);
    }

    private static async Task<uint> RunFastBladeScentSwordMonsterDamageAsync(
        WorldDataCatalog worldData,
        Monster monsterFamily,
        MonsterSpawn spawn,
        ushort spellId,
        WorldSession session)
    {
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                    monster,
                    legacyEncodedQuery: false,
                    spellId: spellId,
                    x: 842,
                    y: 584)).ToFrameBytes()))
            .ConfigureAwait(false);

        var spell = Assert.Single(ParseSpellAnimations(sent).Take(1));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        var runtimeDamage = monster.MaxLife - damagedMonster.Hitpoints;
        Assert.Equal(runtimeDamage * 2, target.Damage);
        Assert.Contains("WorldServer handled legacy FastBlade/ScentSword ManualLine magic", log.ToString());
        return runtimeDamage;
    }

    private static void AssertManualLineAnimationHeader(
        LegacySpellAnimationPacket spell,
        ushort spellId,
        ushort x,
        ushort y)
    {
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(x, spell.X);
        Assert.Equal(y, spell.Y);
        Assert.Equal(spellId, spell.SpellId);
        Assert.Equal(0, spell.SpellLevel);
        Assert.Equal(0, spell.LevelHu);
        Assert.All(spell.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });
    }
}
