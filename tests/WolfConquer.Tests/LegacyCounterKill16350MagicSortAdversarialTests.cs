using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const byte CounterKillRuntimeSort = 29;
    private const ushort HeavensWonderRuntimeSpellId = 16_350;
    private const ushort ExistingCounterKillRuntimeSpellId = 6_003;

    [Fact]
    public async Task HandlerCounterKillPreparesResourcesAndCooldownBeforeUnsupportedIdFallthrough()
    {
        const ushort unsupportedSpellId = HeavensWonderRuntimeSpellId - 1;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateCounterKillRuntimeRow(unsupportedSpellId));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellIds: [unsupportedSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(
                CreateCounterKillRuntimeQuery(unsupportedSpellId, x: 841, y: 589)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(493, updated.Character.MinMana);
        Assert.Equal("95", updated.Character.Attributes["Stamina"]);
        Assert.True(updated.MagicCooldowns?.ContainsKey(unsupportedSpellId));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseLegacySpellPackets(sent));
        Assert.Empty(ParseProficiencyPackets(sent));

        var staminaIndex = SingleFrameIndex(
            sent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.SequenceEqual(
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)]));
        var manaIndex = SingleFrameIndex(
            sent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.SequenceEqual(
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)]));
        var coldTimeIndex = SingleFrameIndex(
            sent,
            frame => LegacyMagicColdTimePacket.TryParse(frame, out var packet) &&
                packet.Spells.SequenceEqual([new LegacyMagicColdTimeSpell(unsupportedSpellId, 300)]));
        Assert.True(staminaIndex < manaIndex);
        Assert.True(manaIndex < coldTimeIndex);
    }

    [Fact]
    public async Task HandlerCounterKillAlwaysEmitsEmptyCasterToZeroAnimationAtQueryCoordinates()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateCounterKillRuntimeRow(HeavensWonderRuntimeSpellId, coolDown: 0, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellEntries:
            [
                CreateCounterKillLearnedSpell(HeavensWonderRuntimeSpellId, experience: 77, useSpellSoul: 2),
            ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(
                CreateCounterKillRuntimeQuery(HeavensWonderRuntimeSpellId, x: 843, y: 592)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(843, animation.X);
        Assert.Equal(592, animation.Y);
        Assert.Equal(HeavensWonderRuntimeSpellId, animation.SpellId);
        Assert.Equal(2, animation.LevelHu);
        Assert.Empty(animation.Targets);
        Assert.Empty(ParseLegacySpellPackets(sent));
        Assert.Empty(ParseProficiencyPackets(sent));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(493, updated.Character.MinMana);
        Assert.Equal("95", updated.Character.Attributes["Stamina"]);
        Assert.Equal(77, Assert.Single(updated.PlayerSpells.Spells).Experience);
    }

    [Fact]
    public async Task HandlerCounterKillTargetsMonstersBeforePlayersAtInclusiveRadiusSixWithSoulNoOpAndPlayerSeventyPercent()
    {
        const long startingCharacterExperience = 12_345;
        var monsterFamily = CreateHighLifeMonsterFamily();
        var edgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 836, boundY: 590);
        var outsideSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 837, boundY: 591);
        var sobNpc = CreateMoveLineSobNpc(id: 66_350, x: 831, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [edgeSpawn, outsideSpawn],
            [sobNpc],
            [CreateCounterKillRuntimeRow(HeavensWonderRuntimeSpellId, experience: 1_000_000)]);
        var edgeMonster = CreateRuntimeMonsterInstance(worldData, edgeSpawn, monsterFamily);
        var outsideMonster = CreateRuntimeMonsterInstance(worldData, outsideSpawn, monsterFamily);
        var startingProficiency = new PlayerProficiencySnapshotEntry(
            421,
            Level: 5,
            PreviousLevel: 0,
            Experience: 123,
            NeededExperience: LegacyProficiencyPacket.GetNeededExperience(5));
        var playerItems = new PlayerItemSnapshot(
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
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellEntries:
            [
                CreateCounterKillLearnedSpell(
                    HeavensWonderRuntimeSpellId,
                    experience: 77,
                    useSpellSoul: 3),
            ],
            attributes: new Dictionary<string, string>
            {
                ["LegacyPhysicalLuckyStrikeRoll"] = "99",
                ["LegacyPhysicalRefineryRoll"] = "99",
            },
            playerItems: playerItems,
            playerProficiencies: new PlayerProficiencySnapshot([startingProficiency]));
        casterSession = casterSession with
        {
            Character = casterSession.Character with { Experience = startingCharacterExperience },
        };
        var edgeTargetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "CounterKillEdge",
            x: 824,
            y: 590,
            minHitPoints: 100_000);
        var outsideTargetSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "CounterKillOutside",
            x: 823,
            y: 591,
            minHitPoints: 100_000);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var edgeTargetSent = new List<byte[]>();
        var outsideTargetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var edgeTargetConnection = CreateSendingConnection(edgeTargetSent);
        var outsideTargetConnection = CreateSendingConnection(outsideTargetSent);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(edgeTargetConnection, edgeTargetSession);
        sessions.Set(outsideTargetConnection, outsideTargetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            HeavensWonderRuntimeSpellId,
            out _,
            out var spell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            edgeMonster,
            monsterFamily,
            spell);
        var unscaledPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            edgeTargetSession,
            spell,
            worldData,
            1U);
        var expectedPlayerDamage = checked(unscaledPlayerDamage * 70 / 100);
        var sobLifeBefore = sobNpcStates.GetVisible(sobNpc).Life;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(
                CreateCounterKillRuntimeQuery(HeavensWonderRuntimeSpellId, x: 845, y: 593)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(845, animation.X);
        Assert.Equal(593, animation.Y);
        Assert.Equal(3, animation.LevelHu);
        Assert.Equal(
            [edgeMonster.Uid, TargetPlayerUid],
            animation.Targets.Select(target => target.Uid).ToArray());
        Assert.Equal(expectedMonsterDamage, animation.Targets[0].Damage);
        Assert.Equal(expectedPlayerDamage, animation.Targets[1].Damage);
        Assert.All(animation.Targets, target => Assert.True(target.Hit));
        Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideMonster.Uid);
        Assert.DoesNotContain(animation.Targets, target => target.Uid == TargetPlayerUid + 1);
        Assert.DoesNotContain(animation.Targets, target => target.Uid == checked((uint)sobNpc.Id));

        Assert.True(monsterStates.TryGetVisibleInstance(edgeMonster, DateTimeOffset.UtcNow, out var visibleEdge));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideMonster, DateTimeOffset.UtcNow, out var visibleOutside));
        Assert.Equal(edgeMonster.MaxLife - expectedMonsterDamage, visibleEdge.Hitpoints);
        Assert.Equal(outsideMonster.MaxLife, visibleOutside.Hitpoints);
        Assert.Equal(sobLifeBefore, sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(edgeTargetConnection, out var updatedEdgeTarget));
        Assert.NotNull(updatedEdgeTarget);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedEdgeTarget.Character.MinHitPoints);
        Assert.True(sessions.TryGet(outsideTargetConnection, out var unchangedOutsideTarget));
        Assert.NotNull(unchangedOutsideTarget);
        Assert.Equal(100_000, unchangedOutsideTarget.Character.MinHitPoints);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(startingCharacterExperience, updatedCaster.Character.Experience);
        Assert.Equal(77, Assert.Single(updatedCaster.PlayerSpells.Spells).Experience);
        Assert.Equal([startingProficiency], updatedCaster.PlayerProficiencies.Proficiencies);
        Assert.Empty(ParseLegacySpellPackets(casterSent));
        Assert.Empty(ParseProficiencyPackets(casterSent));
        Assert.DoesNotContain(
            ParseMsgUpdates(casterSent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
    }

    [Fact]
    public async Task HandlerCounterKillAppliesNormalMonsterAndPlayerAttackGatesButStillAnimatesEmpty()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var guardSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 1,
            boundX: 831,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var worldData = CreateMagicCatalog(
            guardFamily,
            [guardSpawn],
            CreateCounterKillRuntimeRow(HeavensWonderRuntimeSpellId));
        var guard = CreateRuntimeMonsterInstance(worldData, guardSpawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        Assert.True(monsterStates.TryGetVisibleInstance(guard, DateTimeOffset.UtcNow, out var visibleBefore));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [HeavensWonderRuntimeSpellId],
            attributes: new Dictionary<string, string> { ["PkMode"] = "1" });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "PeaceModeTarget",
            x: 832,
            y: 584,
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
            SobNpcStates: new SobNpcRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(
                CreateCounterKillRuntimeQuery(HeavensWonderRuntimeSpellId, x: 833, y: 585)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Empty(animation.Targets);
        Assert.True(monsterStates.TryGetVisibleInstance(guard, DateTimeOffset.UtcNow, out var visibleAfter));
        Assert.Equal(visibleBefore.Hitpoints, visibleAfter.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var unchangedTarget));
        Assert.NotNull(unchangedTarget);
        Assert.Equal(100_000, unchangedTarget.Character.MinHitPoints);
    }

    [Fact]
    public async Task ExistingCounterKill6003SwitchRemainsSeparateAndTogglesWithoutSpellAnimation()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                ExistingCounterKillRuntimeSpellId,
                sort: CounterKillRuntimeSort,
                useMana: 0,
                damage: 100,
                useStamina: 0,
                coolDown: 0,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [ExistingCounterKillRuntimeSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var toggle = new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: PlayerUid,
            X: 830,
            Y: 584,
            AttackType: MsgAttackType.CounterKillSwitch,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(toggle).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);
        Assert.Equal("1", activeSession.Character.Attributes["LegacyCounterKillActive"]);
        var firstToggle = Assert.Single(ParseCounterKillMsgAttackPackets(sent));
        Assert.Equal(MsgAttackType.CounterKillSwitch, firstToggle.Query.AttackType);
        Assert.Equal(ExistingCounterKillRuntimeSpellId, firstToggle.Query.SpellId);
        Assert.Equal(1, firstToggle.Query.Damage);
        Assert.Empty(ParseSpellAnimations(sent));

        sent.Clear();
        await PacketHandlers.HandleAsync(
            context with { Session = activeSession },
            ReadFrame(new MsgAttackPacket(toggle).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var inactiveSession));
        Assert.NotNull(inactiveSession);
        Assert.Equal("0", inactiveSession.Character.Attributes["LegacyCounterKillActive"]);
        var secondToggle = Assert.Single(ParseCounterKillMsgAttackPackets(sent));
        Assert.Equal(MsgAttackType.CounterKillSwitch, secondToggle.Query.AttackType);
        Assert.Equal(ExistingCounterKillRuntimeSpellId, secondToggle.Query.SpellId);
        Assert.Equal(0, secondToggle.Query.Damage);
        Assert.Empty(ParseSpellAnimations(sent));
    }

    private static MagicType CreateCounterKillRuntimeRow(
        ushort spellId,
        uint experience = 0,
        ushort coolDown = 800,
        int coldTime = 300)
    {
        return CreateMagicRow(
            spellId,
            sort: CounterKillRuntimeSort,
            useMana: 7,
            damage: 120,
            experience: experience,
            maxTargets: 30,
            range: 6,
            weaponType: 421,
            useStamina: 5,
            coolDown: coolDown,
            coldTime: coldTime);
    }

    private static PlayerSpellSnapshotEntry CreateCounterKillLearnedSpell(
        ushort spellId,
        int experience,
        byte useSpellSoul)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            Level: 0,
            Experience: experience,
            PreviousLevel: 0,
            SoulLevel: useSpellSoul,
            UseSpellSoul: useSpellSoul);
    }

    private static MsgAttackQuery CreateCounterKillRuntimeQuery(
        ushort spellId,
        ushort x,
        ushort y)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: 0,
            X: x,
            Y: y,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);
    }

    private static IReadOnlyList<MsgAttackPacket> ParseCounterKillMsgAttackPackets(
        IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
