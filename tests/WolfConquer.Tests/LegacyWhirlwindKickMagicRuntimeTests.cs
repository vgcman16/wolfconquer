using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const byte WhirlwindKickRuntimeSort = 54;
    private const ushort WhirlwindKickRuntimeSpellId = 10_415;
    private const ushort AirSweepRuntimeSpellId = 12_330;
    private const ushort AirRaidRuntimeSpellId = 12_340;
    private const ushort SourceSilentWhirlwindKickRuntimeSpellId = 13_490;

    [Theory]
    [InlineData(WhirlwindKickRuntimeSpellId, 80U, 3, 0, 5, 800, 800)]
    [InlineData(AirSweepRuntimeSpellId, 30U, 4, 20, 5, 0, 500)]
    [InlineData(AirRaidRuntimeSpellId, 50U, 4, 20, 0, 0, 500)]
    [InlineData(SourceSilentWhirlwindKickRuntimeSpellId, 65U, 15, 15, 20, 1_500, 2_500)]
    public void LegacyMagicRuntimeResolvesSourceWhirlwindKickRowsAndAttackStampCadence(
        ushort spellId,
        uint expectedDamage,
        byte expectedMaxTargets,
        byte expectedRange,
        byte expectedStamina,
        ushort expectedCoolDown,
        int expectedCadenceMilliseconds)
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateWhirlwindKickRuntimeRow(spellId));

        Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, spellId, 0, out var spell));
        Assert.Equal(expectedDamage, spell.Damage);
        Assert.Equal(expectedMaxTargets, spell.MaxTargets);
        Assert.Equal(expectedRange, spell.Range);
        Assert.Equal(expectedStamina, spell.UseStamina);
        Assert.Equal(expectedCoolDown, spell.CoolDown);
        Assert.Equal(0, spell.ColdTime);

        var cadenceMethod = typeof(WorldPacketHandlers).GetMethod(
            "TryGetLegacyPlayerMagicAttackStampCadence",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(cadenceMethod);
        object?[] cadenceArguments = [spell, null];
        Assert.True(Assert.IsType<bool>(cadenceMethod.Invoke(null, cadenceArguments)));
        Assert.Equal(
            TimeSpan.FromMilliseconds(expectedCadenceMilliseconds),
            Assert.IsType<TimeSpan>(cadenceArguments[1]));
    }

    [Fact]
    public async Task HandlerWhirlwindKickRejectsAnotherSortWithoutSpendingResources()
    {
        var row = CreateWhirlwindKickRuntimeRow(
            WhirlwindKickRuntimeSpellId,
            sort: WhirlwindKickRuntimeSort - 1,
            useMana: 7,
            useStamina: 5,
            coldTime: 300);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellIds: [WhirlwindKickRuntimeSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var (playerSpell, spell) = ResolveWhirlwindKickRuntimeSpell(
            session,
            worldData,
            WhirlwindKickRuntimeSpellId);

        var handled = await InvokeLegacyWhirlwindKickAsync(
            context,
            session,
            CreateWhirlwindKickRuntimeQuery(WhirlwindKickRuntimeSpellId),
            playerSpell,
            spell);

        Assert.False(handled);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var unchanged));
        Assert.NotNull(unchanged);
        Assert.Equal(500, unchanged.Character.MinMana);
        Assert.Equal("100", unchanged.Character.Attributes["Stamina"]);
        Assert.Null(unchanged.MagicCooldowns);
    }

    [Fact]
    public async Task HandlerWhirlwindKickSpendsResourcesAndStartsCooldownBeforeUnknownIdGate()
    {
        const ushort unknownSpellId = WhirlwindKickRuntimeSpellId + 1;
        var row = CreateWhirlwindKickRuntimeRow(
            unknownSpellId,
            useMana: 7,
            useStamina: 5,
            coolDown: 800,
            coldTime: 300);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellIds: [unknownSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var (playerSpell, spell) = ResolveWhirlwindKickRuntimeSpell(
            session,
            worldData,
            unknownSpellId);

        var handled = await InvokeLegacyWhirlwindKickAsync(
            context,
            session,
            CreateWhirlwindKickRuntimeQuery(unknownSpellId),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(493, updated.Character.MinMana);
        Assert.Equal("95", updated.Character.Attributes["Stamina"]);
        Assert.True(updated.MagicCooldowns?.ContainsKey(unknownSpellId));

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
                packet.Spells.SequenceEqual([new LegacyMagicColdTimeSpell(unknownSpellId, 300)]));
        Assert.True(staminaIndex < manaIndex);
        Assert.True(manaIndex < coldTimeIndex);
    }

    [Fact]
    public async Task HandlerWhirlwindKick13490SpendsSourceStaminaThenFallsThroughWithoutAnimation()
    {
        var row = CreateWhirlwindKickRuntimeRow(SourceSilentWhirlwindKickRuntimeSpellId);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            stamina: 100,
            spellIds: [SourceSilentWhirlwindKickRuntimeSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "SourceSilentWhirlwindTarget",
            x: 831,
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
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var (playerSpell, spell) = ResolveWhirlwindKickRuntimeSpell(
            casterSession,
            worldData,
            SourceSilentWhirlwindKickRuntimeSpellId);

        var handled = await InvokeLegacyWhirlwindKickAsync(
            context,
            casterSession,
            CreateWhirlwindKickRuntimeQuery(SourceSilentWhirlwindKickRuntimeSpellId),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal("80", updatedCaster.Character.Attributes["Stamina"]);
        Assert.True(updatedCaster.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
        Assert.Null(updatedCaster.MagicCooldowns);
        Assert.True(sessions.TryGet(targetConnection, out var unchangedTarget));
        Assert.NotNull(unchangedTarget);
        Assert.Equal(100_000, unchangedTarget.Character.MinHitPoints);
        Assert.Contains("source-silent legacy WhirlwindKick magic", log.ToString());
        Assert.Contains("spellId=13490", log.ToString());
    }

    [Theory]
    [InlineData(WhirlwindKickRuntimeSpellId)]
    [InlineData(AirSweepRuntimeSpellId)]
    [InlineData(AirRaidRuntimeSpellId)]
    public async Task HandlerWhirlwindKickHitsMixedTargetsInsideStrictRadiusAndProgressesBeforeSourceOpponentAnimation(
        ushort spellId)
    {
        const ushort queryX = 842;
        const ushort queryY = 590;
        var monsterFamily = CreateHighLifeMonsterFamily();
        var inRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 834, boundY: 584);
        var boundarySpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 835, boundY: 584);
        var inRangeSobNpc = CreateMoveLineSobNpc(id: 62_540, x: 834, y: 584);
        var boundarySobNpc = CreateMoveLineSobNpc(id: 62_541, x: 835, y: 584);
        var magicRows = new List<MagicType>
        {
            CreateWhirlwindKickRuntimeRow(spellId, experience: 1_000_000),
        };
        if (spellId != AirRaidRuntimeSpellId)
        {
            magicRows.Add(CreateWhirlwindKickRuntimeRow(
                spellId,
                rowId: spellId + 1,
                level: 1,
                experience: 1_000_000));
        }

        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [inRangeSpawn, boundarySpawn],
            [inRangeSobNpc, boundarySobNpc],
            magicRows);
        var inRangeMonster = CreateRuntimeMonsterInstance(worldData, inRangeSpawn, monsterFamily);
        var boundaryMonster = CreateRuntimeMonsterInstance(worldData, boundarySpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var inRangePlayerSent = new List<byte[]>();
        var boundaryPlayerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var inRangePlayerConnection = CreateSendingConnection(inRangePlayerSent);
        var boundaryPlayerConnection = CreateSendingConnection(boundaryPlayerSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            minMana: 500,
            strength: 300,
            stamina: 100,
            spellIds: [spellId]);
        var inRangePlayerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WhirlwindInRange",
            x: 834,
            y: 584,
            minHitPoints: 100_000);
        var boundaryPlayerSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "WhirlwindBoundary",
            x: 835,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(inRangePlayerConnection, inRangePlayerSession);
        sessions.Set(boundaryPlayerConnection, boundaryPlayerSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;
        var (playerSpell, spell) = ResolveWhirlwindKickRuntimeSpell(casterSession, worldData, spellId);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            inRangeMonster,
            monsterFamily,
            spell);
        var expectedBasePlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            inRangePlayerSession,
            spell,
            worldData,
            1U);
        var expectedPlayerDamage = checked(expectedBasePlayerDamage * 9 / 14);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);

        var handled = await InvokeLegacyWhirlwindKickAsync(
            context,
            casterSession,
            CreateWhirlwindKickRuntimeQuery(spellId, queryX, queryY),
            playerSpell,
            spell);

        Assert.True(handled);
        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        if (spellId == WhirlwindKickRuntimeSpellId)
        {
            Assert.InRange(animation.AttackedUid, 1U, 5U);
        }
        else
        {
            Assert.InRange(animation.AttackedUid, 2U, 7U);
        }

        Assert.Equal(0, animation.X);
        Assert.Equal(0, animation.Y);
        Assert.Equal(spellId, animation.SpellId);
        Assert.Equal(0, animation.NextSpell);
        Assert.Equal(
            [inRangeMonster.Uid, TargetPlayerUid, checked((uint)inRangeSobNpc.Id)],
            animation.Targets.Select(target => target.Uid).ToArray());
        Assert.Equal(expectedMonsterDamage, animation.Targets[0].Damage);
        Assert.Equal(expectedPlayerDamage, animation.Targets[1].Damage);
        Assert.Equal(expectedSobNpcDamage, animation.Targets[2].Damage);
        Assert.All(animation.Targets, target => Assert.True(target.Hit));
        Assert.All(animation.Targets, target => Assert.Equal(MsgAttackEffect.None, target.Effect));
        Assert.DoesNotContain(boundaryMonster.Uid, animation.Targets.Select(target => target.Uid));
        Assert.DoesNotContain(checked((uint)(TargetPlayerUid + 1)), animation.Targets.Select(target => target.Uid));
        Assert.DoesNotContain(checked((uint)boundarySobNpc.Id), animation.Targets.Select(target => target.Uid));

        Assert.True(monsterStates.TryGetVisibleInstance(inRangeMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(inRangeMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(boundaryMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(boundaryMonster.MaxLife, untouchedMonster.Hitpoints);
        Assert.Equal(
            inRangeSobNpc.Life - checked((int)expectedSobNpcDamage),
            sobNpcStates.GetVisible(inRangeSobNpc).Life);
        Assert.Equal(boundarySobNpc.Life, sobNpcStates.GetVisible(boundarySobNpc).Life);

        Assert.True(sessions.TryGet(inRangePlayerConnection, out var damagedPlayer));
        Assert.NotNull(damagedPlayer);
        Assert.Equal(100_000 - expectedPlayerDamage, damagedPlayer.Character.MinHitPoints);
        Assert.True(sessions.TryGet(boundaryPlayerConnection, out var untouchedPlayer));
        Assert.NotNull(untouchedPlayer);
        Assert.Equal(100_000, untouchedPlayer.Character.MinHitPoints);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(500, updatedCaster.Character.MinMana);
        Assert.Equal(
            spellId == AirRaidRuntimeSpellId ? "100" : "95",
            updatedCaster.Character.Attributes["Stamina"]);
        var updatedPlayerSpell = Assert.Single(updatedCaster.PlayerSpells.Spells);
        if (spellId == AirRaidRuntimeSpellId)
        {
            Assert.Equal(0, updatedPlayerSpell.Experience);
        }
        else
        {
            Assert.True(updatedPlayerSpell.Experience > 0);
        }

        var experienceIndex = SingleFrameIndex(
            casterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
        var animationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == spellId);
        if (spellId == AirRaidRuntimeSpellId)
        {
            Assert.DoesNotContain(
                casterSent.Select(ReadFrame),
                frame => LegacySpellPacket.TryParse(frame, out var packet) && packet.Id == spellId);
            Assert.True(experienceIndex < animationIndex);
        }
        else
        {
            var spellProgressIndex = SingleFrameIndex(
                casterSent,
                frame => LegacySpellPacket.TryParse(frame, out var packet) &&
                    packet.Id == spellId &&
                    packet.Experience > 0);
            Assert.True(experienceIndex < spellProgressIndex);
            Assert.True(spellProgressIndex < animationIndex);
        }
    }

    [Theory]
    [InlineData(WhirlwindKickRuntimeSpellId, false)]
    [InlineData(AirSweepRuntimeSpellId, true)]
    [InlineData(AirRaidRuntimeSpellId, true)]
    public async Task HandlerWhirlwindKickUsesSpecial10415PlayerGateButSharedGateFor12330And12340(
        ushort spellId,
        bool expectedHit)
    {
        var row = CreateWhirlwindKickRuntimeRow(spellId);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "[GM]WhirlwindTarget",
            x: 834,
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
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var (playerSpell, spell) = ResolveWhirlwindKickRuntimeSpell(casterSession, worldData, spellId);
        var expectedBaseDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedDamage = checked(expectedBaseDamage * 9 / 14);

        var handled = await InvokeLegacyWhirlwindKickAsync(
            context,
            casterSession,
            CreateWhirlwindKickRuntimeQuery(spellId),
            playerSpell,
            spell);

        Assert.True(handled);
        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        if (expectedHit)
        {
            var target = Assert.Single(animation.Targets, target => target.Uid == TargetPlayerUid);
            Assert.Equal(expectedDamage, target.Damage);
        }
        else
        {
            Assert.Empty(animation.Targets);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTarget));
        Assert.NotNull(updatedTarget);
        Assert.Equal(
            expectedHit ? 100_000 - expectedDamage : 100_000,
            updatedTarget.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerWhirlwindKickDoesNotCapTargetsAtSourceMaxTargetsField()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        MonsterSpawn[] spawns =
        [
            CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 831, boundY: 584),
            CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 832, boundY: 584),
            CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 833, boundY: 584),
            CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 834, boundY: 584),
            CreateSingleInstanceMonsterSpawn(generatorId: 5, boundX: 830, boundY: 585),
            CreateSingleInstanceMonsterSpawn(generatorId: 6, boundX: 830, boundY: 586),
        ];
        var worldData = CreateMagicCatalog(
            monsterFamily,
            spawns,
            CreateWhirlwindKickRuntimeRow(WhirlwindKickRuntimeSpellId));
        var expectedMonsterUids = spawns
            .Select(spawn => CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily).Uid)
            .OrderBy(uid => uid)
            .ToArray();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [WhirlwindKickRuntimeSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var (playerSpell, spell) = ResolveWhirlwindKickRuntimeSpell(
            session,
            worldData,
            WhirlwindKickRuntimeSpellId);
        Assert.Equal(3, spell.MaxTargets);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(
                CreateWhirlwindKickRuntimeQuery(WhirlwindKickRuntimeSpellId)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(6, animation.Targets.Count);
        Assert.Equal(
            expectedMonsterUids,
            animation.Targets.Select(target => target.Uid).OrderBy(uid => uid).ToArray());
    }

    [Fact]
    public async Task HandlerWhirlwindKickNatureShieldBreaksPlayerLoopButStillProcessesSobTargets()
    {
        const uint natureShieldRuneItemId = 4_038_109;
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateMoveLineSobNpc(id: 62_540, x: 834, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [sobNpc],
            magicRows: [CreateWhirlwindKickRuntimeRow(WhirlwindKickRuntimeSpellId)],
            items: [CreateItemType(checked((int)natureShieldRuneItemId), "NatureShield(+9)")]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [WhirlwindKickRuntimeSpellId]);
        sessions.Set(casterConnection, casterSession);

        var targetConnections = new Dictionary<uint, ConquerTcpConnection>();
        var targetRuneItems = new Dictionary<uint, List<PlayerItemSnapshotItem>>();
        var rollAttributes = new Dictionary<string, string>
        {
            ["LegacyWhirlwindKickNatureShieldRoll"] = "69",
        };
        foreach (var (uid, x) in new (uint Uid, long X)[]
        {
            (TargetPlayerUid, 831),
            (TargetPlayerUid + 1U, 832),
            (TargetPlayerUid + 2U, 833),
        })
        {
            var targetConnection = CreateSendingConnection([]);
            var runeItems = new List<PlayerItemSnapshotItem>();
            targetConnections[uid] = targetConnection;
            targetRuneItems[uid] = runeItems;
            sessions.Set(
                targetConnection,
                CreateSession(
                    uid: uid,
                    name: $"WhirlwindTarget{uid}",
                    x: x,
                    y: 584,
                    minHitPoints: 100_000,
                    attributes: rollAttributes,
                    playerItems: CreatePlayerItems(runeItems)));
        }

        var targetOrder = GetVisibleTargetOrder(sessions, casterConnection, PlayerUid);
        Assert.Equal(3, targetOrder.Count);
        targetRuneItems[targetOrder[0]].Add(new PlayerItemSnapshotItem
        {
            Uid = natureShieldRuneItemId,
            ItemId = natureShieldRuneItemId,
            Position = 101,
        });

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: sobNpcStates,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var (playerSpell, spell) = ResolveWhirlwindKickRuntimeSpell(
            casterSession,
            worldData,
            WhirlwindKickRuntimeSpellId);

        var handled = await InvokeLegacyWhirlwindKickAsync(
            context,
            casterSession,
            CreateWhirlwindKickRuntimeQuery(WhirlwindKickRuntimeSpellId),
            playerSpell,
            spell);

        Assert.True(handled);
        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal([checked((uint)sobNpc.Id)], animation.Targets.Select(target => target.Uid).ToArray());
        foreach (var targetUid in targetOrder)
        {
            Assert.True(sessions.TryGet(targetConnections[targetUid], out var updatedTarget));
            Assert.NotNull(updatedTarget);
            Assert.Equal(100_000, updatedTarget.Character.MinHitPoints);
        }

        Assert.True(sobNpcStates.GetVisible(sobNpc).Life < sobNpc.Life);
        Assert.Contains("runeAvoidance=NatureShield", log.ToString());
    }

    private static MagicType CreateWhirlwindKickRuntimeRow(
        ushort spellId,
        int? rowId = null,
        byte sort = WhirlwindKickRuntimeSort,
        byte level = 0,
        ushort? useMana = null,
        byte? useStamina = null,
        ushort? coolDown = null,
        int? coldTime = null,
        uint experience = 0)
    {
        var resolvedRowId = rowId ?? spellId;
        var resolvedDamage = spellId switch
        {
            WhirlwindKickRuntimeSpellId => 30_080U,
            AirSweepRuntimeSpellId => 30_030U,
            AirRaidRuntimeSpellId => 30_050U,
            SourceSilentWhirlwindKickRuntimeSpellId => 30_065U,
            _ => 100U,
        };
        var resolvedMaxTargets = spellId switch
        {
            WhirlwindKickRuntimeSpellId => (byte)3,
            SourceSilentWhirlwindKickRuntimeSpellId => (byte)15,
            _ => (byte)4,
        };
        var resolvedRange = spellId switch
        {
            WhirlwindKickRuntimeSpellId => (byte)0,
            SourceSilentWhirlwindKickRuntimeSpellId => (byte)15,
            _ => (byte)20,
        };
        var resolvedStamina = useStamina ?? (spellId switch
        {
            AirRaidRuntimeSpellId => (byte)0,
            SourceSilentWhirlwindKickRuntimeSpellId => (byte)20,
            _ => (byte)5,
        });
        var resolvedCoolDown = coolDown ?? (spellId switch
        {
            WhirlwindKickRuntimeSpellId or SourceSilentWhirlwindKickRuntimeSpellId => (ushort)1_500,
            _ => (ushort)0,
        });
        var template = CreateMagicRow(
            spellId,
            sort,
            level,
            useMana ?? 0,
            damage: resolvedDamage,
            experience,
            maxTargets: resolvedMaxTargets,
            range: resolvedRange,
            useStamina: resolvedStamina,
            coolDown: resolvedCoolDown,
            coldTime: coldTime ?? 0,
            damageOnHuman: 30_020,
            damageOnMonster: 30_060);
        var fields = template.Fields.ToArray();
        fields[0] = resolvedRowId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[3] = spellId switch
        {
            WhirlwindKickRuntimeSpellId => "WhirlwindKick",
            AirSweepRuntimeSpellId => "AirSweep",
            AirRaidRuntimeSpellId => "AirRaid",
            SourceSilentWhirlwindKickRuntimeSpellId => "WhirlwindKick",
            _ => $"WhirlwindKick{spellId}",
        };
        return new MagicType(resolvedRowId, spellId, fields[3], fields);
    }

    private static MsgAttackQuery CreateWhirlwindKickRuntimeQuery(
        ushort spellId,
        ushort x = 842,
        ushort y = 590)
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

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell)
        ResolveWhirlwindKickRuntimeSpell(WorldSession session, WorldDataCatalog worldData, ushort spellId)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            spellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static async ValueTask<bool> InvokeLegacyWhirlwindKickAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerWhirlwindKickMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        return await Assert.IsType<ValueTask<bool>>(result);
    }
}
