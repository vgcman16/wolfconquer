using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const byte StarArrowRuntimeSort = 38;
    private const ushort StarArrowRuntimeSpellId = 10_313;
    private const string StarArrowNatureShieldRollAttribute = "LegacyStarArrowNatureShieldRoll";
    private const int StarArrowPlayerSpawnUidField = 2;
    private const int StarArrowPlayerSpawnXField = 27;
    private const int StarArrowPlayerSpawnYField = 28;

    [Fact]
    public async Task HandlerStarArrowSpendsResourcesAndStartsCooldownBeforeItsAdjacentIdGate()
    {
        const ushort wrongSpellId = StarArrowRuntimeSpellId + 1;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateStarArrowRuntimeRow(
                rowId: wrongSpellId,
                typeId: wrongSpellId,
                useMana: 7,
                useStamina: 5,
                coolDown: 800,
                coldTime: 300));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellIds: [wrongSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolveStarArrowRuntimeSpell(session, worldData, wrongSpellId);

        var handled = await InvokeLegacyStarArrowAsync(
            context,
            session,
            CreateStarArrowRuntimeQuery(wrongSpellId, opponentUid: 0),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(493, updated.Character.MinMana);
        Assert.Equal("95", updated.Character.Attributes["Stamina"]);
        Assert.True(updated.MagicCooldowns?.ContainsKey(wrongSpellId));

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
                packet.Spells.SequenceEqual([new LegacyMagicColdTimeSpell(wrongSpellId, 300)]));
        Assert.True(staminaIndex < manaIndex);
        Assert.True(manaIndex < coldTimeIndex);
        Assert.DoesNotContain("WorldServer handled legacy StarArrow magic", log.ToString());
    }

    [Fact]
    public async Task HandlerStarArrowPullsMonsterBeforeRangeDamageAndProgressesBeforeAnimation()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateStarArrowRuntimeRow(
                experience: 1_000_000,
                range: 2),
            CreateStarArrowRuntimeRow(
                rowId: StarArrowRuntimeSpellId + 1,
                level: 1,
                experience: 1_000_000,
                range: 2));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            attributes: new Dictionary<string, string>
            {
                ["LegacyPhysicalLuckyStrikeRoll"] = "99",
                ["LegacyPhysicalRefineryRoll"] = "99",
            },
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    StarArrowRuntimeSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 3),
            ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolveStarArrowRuntimeSpell(session, worldData, StarArrowRuntimeSpellId);
        var (expectedDamage, expectedEffect) = InvokeStarArrowMonsterRangeDamage(
            session,
            worldData,
            monster,
            monsterFamily,
            spell);
        var expectedProgress = InvokePrivateInt32(
            "AdjustLegacyMonsterExperience",
            checked((int)Math.Min(expectedDamage, monster.MaxLife)),
            checked((int)session.Character.Level),
            monster.Level);

        var handled = await InvokeLegacyStarArrowAsync(
            context,
            session,
            CreateStarArrowRuntimeQuery(
                StarArrowRuntimeSpellId,
                monster.Uid,
                x: monster.X,
                y: monster.Y),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var movedMonster));
        Assert.Equal(session.Character.X, movedMonster.X);
        Assert.Equal(session.Character.Y, movedMonster.Y);
        Assert.Equal(monster.MaxLife - expectedDamage, movedMonster.Hitpoints);

        var pullRefreshIndex = SingleFrameIndex(
            sent,
            frame => LegacyMonsterSpawnPacket.TryParse(frame, out var packet) &&
                packet.Uid == monster.Uid &&
                packet.X == session.Character.X &&
                packet.Y == session.Character.Y);
        var experienceIndex = SingleFrameIndex(
            sent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
        var spellProgressIndex = SingleFrameIndex(
            sent,
            frame => LegacySpellPacket.TryParse(frame, out var packet) &&
                packet.Id == StarArrowRuntimeSpellId &&
                packet.Experience == expectedProgress);
        var animationIndex = SingleFrameIndex(
            sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == StarArrowRuntimeSpellId);
        Assert.True(pullRefreshIndex < experienceIndex);
        Assert.True(experienceIndex < spellProgressIndex);
        Assert.True(spellProgressIndex < animationIndex);

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(monster.X, animation.X);
        Assert.Equal(monster.Y, animation.Y);
        Assert.Equal(3, animation.LevelHu);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(expectedEffect, target.Effect);
        Assert.Equal(0, target.NewX);
        Assert.Equal(0, target.NewY);

        Assert.True(sessions.TryGet(connection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.True(updatedCaster.Character.Experience > 0);
        Assert.Equal(expectedProgress, Assert.Single(updatedCaster.PlayerSpells.Spells).Experience);
        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy StarArrow magic", output);
        Assert.Contains("spellId=10313", output);
        Assert.Contains("sort=38", output);
        Assert.Contains("targetKind=monster", output);
        Assert.Contains($"monsterProgress={expectedProgress}", output);
    }

    [Fact]
    public async Task HandlerStarArrowPullsPlayerBeforeRangeDamageAndCarriesMoveCoordinatesWhenNatureShieldMisses()
    {
        const uint natureShieldRuneItemId = 4_038_109;
        var worldData = CreateMagicCatalogWithItems(
            CreateMonsterFamily(),
            [],
            [CreateItemType((int)natureShieldRuneItemId, "NatureShield(+9)")],
            CreateStarArrowRuntimeRow(range: 2));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [StarArrowRuntimeSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "StarArrowTarget",
            x: 836,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [],
            attributes: new Dictionary<string, string>
            {
                [StarArrowNatureShieldRollAttribute] = "99",
            },
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = natureShieldRuneItemId,
                ItemId = natureShieldRuneItemId,
                Position = 101,
            }));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolveStarArrowRuntimeSpell(
            casterSession,
            worldData,
            StarArrowRuntimeSpellId);
        var pulledTargetSession = targetSession with
        {
            Character = targetSession.Character with
            {
                X = casterSession.Character.X,
                Y = casterSession.Character.Y,
            },
        };
        var expectedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            pulledTargetSession,
            spell,
            worldData,
            1U);

        var handled = await InvokeLegacyStarArrowAsync(
            context,
            casterSession,
            CreateStarArrowRuntimeQuery(
                StarArrowRuntimeSpellId,
                TargetPlayerUid,
                x: checked((ushort)targetSession.Character.X),
                y: checked((ushort)targetSession.Character.Y)),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTarget));
        Assert.NotNull(updatedTarget);
        Assert.Equal(casterSession.Character.X, updatedTarget.Character.X);
        Assert.Equal(casterSession.Character.Y, updatedTarget.Character.Y);
        Assert.Equal(100_000 - expectedDamage, updatedTarget.Character.MinHitPoints);

        var pullRefreshIndex = SingleFrameIndex(
            casterSent,
            frame => LegacyPlayerSpawnPacket.TryParse(frame, out var spawn) &&
                spawn.GetNumericField(StarArrowPlayerSpawnUidField) == TargetPlayerUid &&
                spawn.GetNumericField(StarArrowPlayerSpawnXField) == (ulong)casterSession.Character.X &&
                spawn.GetNumericField(StarArrowPlayerSpawnYField) == (ulong)casterSession.Character.Y);
        var animationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == StarArrowRuntimeSpellId);
        Assert.True(pullRefreshIndex < animationIndex);

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        var target = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(casterSession.Character.X, target.NewX);
        Assert.Equal(casterSession.Character.Y, target.NewY);
        Assert.Single(ParseSpellAnimations(targetSent));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(0, updatedCaster.Character.Experience);
        Assert.Equal(0, Assert.Single(updatedCaster.PlayerSpells.Spells).Experience);
        var output = log.ToString();
        Assert.Contains("targetKind=player", output);
        Assert.Contains($"playerAppliedDamage={expectedDamage}", output);
        Assert.DoesNotContain("StarArrow NatureShield early return", output);
    }

    [Fact]
    public async Task HandlerStarArrowNatureShieldForcedHitReturnsAfterPullWithoutDamageOrAnimation()
    {
        const uint natureShieldRuneItemId = 4_038_101;
        var worldData = CreateMagicCatalogWithItems(
            CreateMonsterFamily(),
            [],
            [CreateItemType((int)natureShieldRuneItemId, "NatureShield(+1)")],
            CreateStarArrowRuntimeRow(
                useMana: 7,
                useStamina: 5,
                range: 2));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            minMana: 500,
            stamina: 100,
            strength: 300,
            spellIds: [StarArrowRuntimeSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "NatureShieldTarget",
            x: 836,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [],
            attributes: new Dictionary<string, string>
            {
                [StarArrowNatureShieldRollAttribute] = "0",
            },
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = natureShieldRuneItemId,
                ItemId = natureShieldRuneItemId,
                Position = 101,
            }));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolveStarArrowRuntimeSpell(
            casterSession,
            worldData,
            StarArrowRuntimeSpellId);

        var handled = await InvokeLegacyStarArrowAsync(
            context,
            casterSession,
            CreateStarArrowRuntimeQuery(
                StarArrowRuntimeSpellId,
                TargetPlayerUid,
                x: checked((ushort)targetSession.Character.X),
                y: checked((ushort)targetSession.Character.Y)),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.True(sessions.TryGet(targetConnection, out var pulledTarget));
        Assert.NotNull(pulledTarget);
        Assert.Equal(casterSession.Character.X, pulledTarget.Character.X);
        Assert.Equal(casterSession.Character.Y, pulledTarget.Character.Y);
        Assert.Equal(100_000, pulledTarget.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains(
            casterSent,
            frame => LegacyPlayerSpawnPacket.TryParse(ReadFrame(frame), out var spawn) &&
                spawn.GetNumericField(StarArrowPlayerSpawnUidField) == TargetPlayerUid &&
                spawn.GetNumericField(StarArrowPlayerSpawnXField) == (ulong)casterSession.Character.X &&
                spawn.GetNumericField(StarArrowPlayerSpawnYField) == (ulong)casterSession.Character.Y);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(493, updatedCaster.Character.MinMana);
        Assert.Equal("95", updatedCaster.Character.Attributes["Stamina"]);
        Assert.Equal(0, updatedCaster.Character.Experience);
        Assert.Equal(0, Assert.Single(updatedCaster.PlayerSpells.Spells).Experience);
        Assert.DoesNotContain(casterSent, frame => LegacySpellPacket.TryParse(ReadFrame(frame), out _));
        var output = log.ToString();
        Assert.Contains("StarArrow NatureShield early return after player pull", output);
        Assert.Contains("natureShieldLevel=1", output);
        Assert.Contains("earlyReturn=true", output);
        Assert.DoesNotContain("WorldServer handled legacy StarArrow magic", output);
    }

    [Fact]
    public async Task HandlerStarArrowSobNpcUsesRangeDamageAndClearsItsAnimationHitMarker()
    {
        var sobNpc = CreateMoveLineSobNpc(id: 63_313, x: 833, y: 584);
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [sobNpc],
            magicRows: [CreateStarArrowRuntimeRow(range: 4)]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    StarArrowRuntimeSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 2),
            ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: sobNpcStates);
        var (playerSpell, spell) = ResolveStarArrowRuntimeSpell(session, worldData, StarArrowRuntimeSpellId);
        var expectedDamage = InvokePrivateUInt32(
            "CalculateLegacyAttackStatus7000SobNpcRangeDamage",
            session,
            spell,
            worldData);

        var handled = await InvokeLegacyStarArrowAsync(
            context,
            session,
            CreateStarArrowRuntimeQuery(
                StarArrowRuntimeSpellId,
                checked((uint)sobNpc.Id),
                x: checked((ushort)sobNpc.X),
                y: checked((ushort)sobNpc.Y)),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.Equal(sobNpc.Life - checked((int)expectedDamage), sobNpcStates.GetVisible(sobNpc).Life);
        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(2, animation.LevelHu);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.False(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.True(sessions.TryGet(connection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(0, updatedCaster.Character.Experience);
        Assert.Equal(0, Assert.Single(updatedCaster.PlayerSpells.Spells).Experience);
        var output = log.ToString();
        Assert.Contains("targetKind=sob-npc", output);
        Assert.Contains("sobNpcProgress=0", output);
    }

    private static MagicType CreateStarArrowRuntimeRow(
        int rowId = StarArrowRuntimeSpellId,
        ushort typeId = StarArrowRuntimeSpellId,
        byte level = 0,
        ushort useMana = 0,
        byte useStamina = 0,
        ushort coolDown = 0,
        int coldTime = 0,
        uint experience = 0,
        byte range = 9)
    {
        var template = CreateMagicRow(
            typeId,
            StarArrowRuntimeSort,
            level,
            useMana,
            damage: 120,
            experience,
            range: range,
            useStamina: useStamina,
            coolDown: coolDown,
            coldTime: coldTime);
        var fields = template.Fields.ToArray();
        fields[0] = rowId.ToString(CultureInfo.InvariantCulture);
        fields[1] = typeId.ToString(CultureInfo.InvariantCulture);
        fields[3] = "StarArrow";
        return new MagicType(rowId, typeId, fields[3], fields);
    }

    private static MsgAttackQuery CreateStarArrowRuntimeQuery(
        ushort spellId,
        uint opponentUid,
        ushort x = 836,
        ushort y = 584)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: opponentUid,
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
        ResolveStarArrowRuntimeSpell(
            WorldSession session,
            WorldDataCatalog worldData,
            ushort spellId)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            spellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static (uint Damage, MsgAttackEffect Effect) InvokeStarArrowMonsterRangeDamage(
        WorldSession session,
        WorldDataCatalog worldData,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "CalculateLegacyAttackStatus7000MonsterRangeDamage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(null, [session, worldData, monster, monsterFamily, spell]);
        Assert.NotNull(result);
        var resultType = result.GetType();
        var damageProperty = resultType.GetProperty("Damage", BindingFlags.Instance | BindingFlags.Public);
        var effectProperty = resultType.GetProperty("Effect", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(damageProperty);
        Assert.NotNull(effectProperty);
        return (
            Assert.IsType<uint>(damageProperty.GetValue(result)),
            Assert.IsType<MsgAttackEffect>(effectProperty.GetValue(result)));
    }

    private static async Task<bool> InvokeLegacyStarArrowAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerStarArrowMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        return await Assert.IsType<Task<bool>>(result);
    }
}
