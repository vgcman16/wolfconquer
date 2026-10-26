using System.Globalization;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task HandlerLegacyWorldLoginPreservesSavedLowLevelStatsUntilTrueProgression()
    {
        var worldData = OrdinaryLoginFixture.Catalog();
        var usersRoot = Path.Combine(_usersRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        WriteSnapshot(
            usersRoot,
            1293600,
            uid: 1293600,
            classCode: 1000,
            level: 31,
            reborn: 0,
            secondClass: 0,
            strength: 5,
            agility: 2,
            minHitPoints: 1);
        OrdinaryLoginFixture.PrepareLowLevelBaseline(usersRoot);
        using var log = new StringWriter();
        using var admission = new OrdinaryLoginFixture(new CharacterSnapshotLoader(usersRoot), worldData);
        var handler = admission.CreateHandler(log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1293600).ToFrameBytes()));

        var session = GetSession(handler, connection);
        Assert.Equal(31, session.Character.Level);
        Assert.Equal(5, session.Character.Strength);
        Assert.Equal(2, session.Character.Agility);
        Assert.Equal(3, session.Character.Vitality);
        Assert.Equal(0, session.Character.Spirit);
        Assert.Equal(0, session.Character.AttributePoints);
        Assert.Equal(1, session.Character.MinHitPoints);

        var persisted = new CharacterSnapshotLoader(usersRoot).LoadByAccountId(1293600);
        Assert.NotNull(persisted);
        Assert.Equal(5, persisted.Strength);
        Assert.Equal(2, persisted.Agility);
        Assert.Equal(3, persisted.Vitality);
        Assert.Equal(0, persisted.Spirit);
        Assert.Contains("WorldServer ordinary admission committed key=1293600.", log.ToString());
        await admission.AssertCommittedAndRepeatedAsync(handler, connection, sent);
    }

    [Fact]
    public async Task HandlerLegacyWorldLoginPreservesSavedUpperClassStatsOnOrdinaryLogin()
    {
        var worldData = OrdinaryLoginFixture.Catalog();
        var usersRoot = Path.Combine(_usersRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        WriteSnapshot(
            usersRoot,
            1293600,
            uid: 1293600,
            classCode: 1057,
            level: 31,
            reborn: 0,
            secondClass: 0,
            strength: 5,
            agility: 2,
            minHitPoints: 1);
        OrdinaryLoginFixture.PrepareLowLevelBaseline(usersRoot);
        using var log = new StringWriter();
        using var admission = new OrdinaryLoginFixture(new CharacterSnapshotLoader(usersRoot), worldData);
        var handler = admission.CreateHandler(log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1293600).ToFrameBytes()));

        var session = GetSession(handler, connection);
        Assert.Equal(31, session.Character.Level);
        Assert.Equal(5, session.Character.Strength);
        Assert.Equal(2, session.Character.Agility);
        Assert.Equal(3, session.Character.Vitality);
        Assert.Equal(0, session.Character.Spirit);
        Assert.Equal(0, session.Character.AttributePoints);
        Assert.Equal(1, session.Character.MinHitPoints);
        Assert.Empty(session.PlayerSpells.Spells); //1057 is outside source Trojan1000..1051 login grant.
        await admission.AssertCommittedAndRepeatedAsync(handler, connection, sent);
    }

    [Fact]
    public async Task LegacyClassLevelStatProgressionTreatsUpperBoundWaterAsFreePointAfterSourceCutoff()
    {
        var worldData = await LoadSeedWorldDataOrSkipAsync();
        if (worldData is null)
        {
            return;
        }

        var character = CreateProgressionSnapshot(classCode: 13057, level: 111, attributePoints: 0);

        var updated = LegacyClassLevelStatProgression.Apply(
            character,
            worldData,
            previousLevel: 110,
            out var change);

        Assert.False(change.AppliedSourceStats);
        Assert.True(change.AddedAttributePoints);
        Assert.Equal(3, change.AttributePointsAdded);
        Assert.Equal(3, updated.AttributePoints);
    }

    [Fact]
    public async Task HandlerMonsterExperienceLevelUpAppliesSourceStatsAndRefreshesClient()
    {
        var worldData = await LoadSeedWorldDataOrSkipAsync();
        if (worldData is null)
        {
            return;
        }

        Directory.CreateDirectory(_usersRoot);
        var level30Experience = worldData.LevelExperienceByTypeAndLevel[(0, 30)].Experience;
        WriteSnapshot(
            1293600,
            uid: 1293600,
            mapId: 1015,
            x: 837,
            y: 573,
            classCode: 1000,
            level: 30,
            reborn: 0,
            secondClass: 0,
            strength: 49,
            agility: 19,
            minHitPoints: 100,
            extraCharacterLines: string.Format(
                CultureInfo.InvariantCulture,
                "Vitaliti=29\r\nSpirit=0\r\nAtributes=0\r\nExperience={0}",
                Math.Max(0, checked((long)level30Experience - 1))));
        RaiseSeedMonsterLifeForRetaliationFixture(worldData, "Birdman");
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1293600, "user03", ConquerRuntimeDefaults.WorldServer);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_293_600,
                Timestamp = 1234,
                Type = MsgActionType.SetLocation,
            }.ToFrameBytes()));

        var monster = sent.ToArray()
            .Select(bytes => LegacyMonsterSpawnPacket.TryParse(ReadFrame(bytes), out var parsed) ? parsed : null)
            .FirstOrDefault(packet => packet is not null &&
                packet.Names[0] == "Birdman" &&
                packet.Level == 87 &&
                packet.X == 837 &&
                packet.Y == 573);
        Assert.NotNull(monster);

        var attackStart = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: 1_293_600,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 100,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        var session = GetSession(handler, connection);
        Assert.Equal(31, session.Character.Level);
        Assert.Equal(50, session.Character.Strength);
        Assert.Equal(20, session.Character.Agility);
        Assert.Equal(30, session.Character.Vitality);
        Assert.Equal(0, session.Character.Spirit);
        Assert.Equal(0, session.Character.AttributePoints);
        Assert.True(session.Character.MinHitPoints > 100);

        Assert.Contains(
            sent.Skip(attackStart + 1),
            bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == 1_293_600U &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Strength &&
                    entry.Value == 50));
        Assert.Contains(
            sent.Skip(attackStart + 1),
            bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == 1_293_600U &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Vitality &&
                    entry.Value == 30));
    }

    private static async Task<WorldDataCatalog?> LoadSeedWorldDataOrSkipAsync()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return null;
        }

        return await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
    }

    private static CharacterSnapshot CreateProgressionSnapshot(
        long classCode,
        long level,
        long attributePoints)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Class"] = classCode.ToString(CultureInfo.InvariantCulture),
            ["Level"] = level.ToString(CultureInfo.InvariantCulture),
            ["Atributes"] = attributePoints.ToString(CultureInfo.InvariantCulture),
        };

        return new CharacterSnapshot(
            Uid: 1293600,
            Name: "Progression",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: classCode,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: level,
            Experience: 0,
            MapId: 1002,
            X: 430,
            Y: 378,
            PreviousMapId: 1002,
            PreviousMapX: 430,
            PreviousMapY: 378,
            Strength: 0,
            Agility: 0,
            Vitality: 1,
            Spirit: 0,
            AttributePoints: attributePoints,
            MinHitPoints: 1,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "test.ini",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
