using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const byte MortalDragSort = 65;
    private const ushort MortalDragSpellId = 11_180;
    private const int PlayerSpawnUidField = 2;
    private const int PlayerSpawnXField = 27;
    private const int PlayerSpawnYField = 28;

    [Fact]
    public async Task HandlerMortalDragRejectsAnotherSortWithoutSpendingResources()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMortalDragRow(
                MortalDragSpellId,
                MortalDragSpellId,
                sort: MortalDragSort - 1,
                useMana: 7,
                useStamina: 5));
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellIds: [MortalDragSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());
        var (playerSpell, spell) = ResolveMortalDragSpell(session, worldData, MortalDragSpellId);

        var handled = await InvokeLegacyMortalDragAsync(
            context,
            session,
            CreateMortalDragQuery(MortalDragSpellId, opponentUid: 0),
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
    public async Task HandlerMortalDragSpendsResourcesBeforeItsIdGate()
    {
        const ushort wrongSpellId = MortalDragSpellId + 1;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMortalDragRow(
                wrongSpellId,
                wrongSpellId,
                useMana: 7,
                useStamina: 5,
                coolDown: 0,
                coldTime: 0));
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
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
            MonsterStates: new MonsterRuntimeStateStore());
        var (playerSpell, spell) = ResolveMortalDragSpell(session, worldData, wrongSpellId);

        var handled = await InvokeLegacyMortalDragAsync(
            context,
            session,
            CreateMortalDragQuery(wrongSpellId, opponentUid: 0),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(493, updated.Character.MinMana);
        Assert.Equal("95", updated.Character.Attributes["Stamina"]);
        Assert.DoesNotContain("WorldServer handled legacy MortalDrag magic", log.ToString());
    }

    [Fact]
    public async Task HandlerMortalDragMonsterPullsBeforeDamageAndProgressesBeforeAnimation()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMortalDragRow(
                MortalDragSpellId,
                MortalDragSpellId,
                level: 0,
                experience: 1_000_000,
                useMana: 0,
                useStamina: 0,
                coolDown: 0,
                coldTime: 0),
            CreateMortalDragRow(
                MortalDragSpellId + 1,
                MortalDragSpellId,
                level: 1,
                experience: 1_000_000,
                useMana: 0,
                useStamina: 0,
                coolDown: 0,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    MortalDragSpellId,
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
            MonsterStates: monsterStates);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref session);
        context = combatSpellOwner.Context;
        var (playerSpell, spell) = ResolveMortalDragSpell(session, worldData, MortalDragSpellId);
        var expectedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
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

        var handled = await InvokeLegacyMortalDragAsync(
            context,
            session,
            CreateMortalDragQuery(MortalDragSpellId, monster.Uid, x: monster.X, y: monster.Y),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var movedMonster));
        Assert.Equal(session.Character.X, movedMonster.X);
        Assert.Equal(session.Character.Y, movedMonster.Y);
        Assert.Equal(monster.MaxLife - expectedDamage, movedMonster.Hitpoints);

        var spawnIndex = SingleFrameIndex(
            sent,
            frame => LegacyMonsterSpawnPacket.TryParse(frame, out var packet) &&
                packet.Uid == monster.Uid &&
                packet.X == session.Character.X &&
                packet.Y == session.Character.Y);
        Assert.True(LegacyMonsterSpawnPacket.TryParse(ReadFrame(sent[spawnIndex]), out var movedSpawn));
        Assert.Equal(monster.MaxLife, movedSpawn.Hitpoints);

        var experienceIndex = SingleFrameIndex(
            sent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
        var spellProgressIndex = SingleFrameIndex(
            sent,
            frame => LegacySpellPacket.TryParse(frame, out var packet) &&
                packet.Id == MortalDragSpellId &&
                packet.Experience == expectedProgress);
        var animationIndex = SingleFrameIndex(
            sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == MortalDragSpellId);
        Assert.True(spawnIndex < experienceIndex);
        Assert.True(experienceIndex < spellProgressIndex);
        Assert.True(spellProgressIndex < animationIndex);

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(monster.X, animation.X);
        Assert.Equal(monster.Y, animation.Y);
        Assert.Equal(3, animation.LevelHu);
        var animationTarget = Assert.Single(animation.Targets);
        Assert.Equal(monster.Uid, animationTarget.Uid);
        Assert.Equal(expectedDamage, animationTarget.Damage);
        Assert.True(animationTarget.Hit);
        Assert.Equal(0, animationTarget.NewX);
        Assert.Equal(0, animationTarget.NewY);

        Assert.True(sessions.TryGet(connection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.True(updatedCaster.Character.Experience > 0);
        Assert.Equal(expectedProgress, Assert.Single(updatedCaster.PlayerSpells.Spells).Experience);
        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy MortalDrag magic", output);
        Assert.Contains("spellId=11180", output);
        Assert.Contains("sort=65", output);
        Assert.Contains("targetKind=monster", output);
        Assert.Contains($"monsterProgress={expectedProgress}", output);
        Assert.Contains("source=MortalDrag.cs spell 11180", output);
    }

    [Fact]
    public async Task HandlerMortalDragPlayerPullsBeforePhysicalDamageWithoutPlayerProgress()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMortalDragRow(
                MortalDragSpellId,
                MortalDragSpellId,
                useMana: 0,
                useStamina: 0,
                coolDown: 0,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [MortalDragSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "DragTarget",
            x: 834,
            y: 584,
            minHitPoints: 100_000,
            spellIds: []);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());
        var (playerSpell, spell) = ResolveMortalDragSpell(casterSession, worldData, MortalDragSpellId);
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

        var handled = await InvokeLegacyMortalDragAsync(
            context,
            casterSession,
            CreateMortalDragQuery(
                MortalDragSpellId,
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

        var targetSpawnIndex = SingleFrameIndex(
            casterSent,
            frame => LegacyPlayerSpawnPacket.TryParse(frame, out var spawn) &&
                spawn.GetNumericField(PlayerSpawnUidField) == TargetPlayerUid &&
                spawn.GetNumericField(PlayerSpawnXField) == (ulong)casterSession.Character.X &&
                spawn.GetNumericField(PlayerSpawnYField) == (ulong)casterSession.Character.Y);
        Assert.Contains(
            targetSent,
            frame => LegacyPlayerSpawnPacket.TryParse(ReadFrame(frame), out var spawn) &&
                spawn.GetNumericField(PlayerSpawnUidField) == PlayerUid);
        var animationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == MortalDragSpellId);
        Assert.True(targetSpawnIndex < animationIndex);

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        var animationTarget = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, animationTarget.Uid);
        Assert.Equal(expectedDamage, animationTarget.Damage);
        Assert.True(animationTarget.Hit);
        Assert.Equal(MsgAttackEffect.None, animationTarget.Effect);
        Assert.Single(ParseSpellAnimations(targetSent));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(0, updatedCaster.Character.Experience);
        Assert.Equal(0, Assert.Single(updatedCaster.PlayerSpells.Spells).Experience);
        var output = log.ToString();
        Assert.Contains("targetKind=player", output);
        Assert.Contains("monsterProgress=0", output);
        Assert.Contains($"playerAppliedDamage={expectedDamage}", output);
    }

    [Fact]
    public async Task HandlerMortalDragNatureShieldAlwaysReturnsAfterPullWithoutDamageOrAnimation()
    {
        const uint natureShieldRuneItemId = 4_038_101;
        var worldData = CreateMagicCatalogWithItems(
            CreateMonsterFamily(),
            [],
            [CreateItemType((int)natureShieldRuneItemId, "NatureShield(+1)")],
            CreateMortalDragRow(
                MortalDragSpellId,
                MortalDragSpellId,
                useMana: 7,
                useStamina: 5,
                coolDown: 0,
                coldTime: 0));
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
            spellIds: [MortalDragSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "NatureShieldTarget",
            x: 834,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [],
            attributes: new Dictionary<string, string>
            {
                ["LegacyMortalDragNatureShieldRoll"] = "99",
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
            MonsterStates: new MonsterRuntimeStateStore());
        var (playerSpell, spell) = ResolveMortalDragSpell(casterSession, worldData, MortalDragSpellId);

        var handled = await InvokeLegacyMortalDragAsync(
            context,
            casterSession,
            CreateMortalDragQuery(
                MortalDragSpellId,
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
                spawn.GetNumericField(PlayerSpawnUidField) == TargetPlayerUid &&
                spawn.GetNumericField(PlayerSpawnXField) == (ulong)casterSession.Character.X &&
                spawn.GetNumericField(PlayerSpawnYField) == (ulong)casterSession.Character.Y);
        Assert.Contains(
            targetSent,
            frame => LegacyPlayerSpawnPacket.TryParse(ReadFrame(frame), out var spawn) &&
                spawn.GetNumericField(PlayerSpawnUidField) == PlayerUid);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(493, updatedCaster.Character.MinMana);
        Assert.Equal("95", updatedCaster.Character.Attributes["Stamina"]);
        Assert.Equal(0, updatedCaster.Character.Experience);
        Assert.Equal(0, Assert.Single(updatedCaster.PlayerSpells.Spells).Experience);
        Assert.DoesNotContain(casterSent, frame => LegacySpellPacket.TryParse(ReadFrame(frame), out _));
        var output = log.ToString();
        Assert.Contains("MortalDrag NatureShield early return after player pull", output);
        Assert.Contains("natureShieldLevel=1", output);
        Assert.Contains("earlyReturn=true", output);
        Assert.DoesNotContain("WorldServer handled legacy MortalDrag magic", output);
    }

    private static MagicType CreateMortalDragRow(
        int rowId,
        ushort typeId,
        byte sort = MortalDragSort,
        byte level = 0,
        ushort useMana = 0,
        byte useStamina = 0,
        ushort coolDown = 0,
        int coldTime = 0,
        uint experience = 0)
    {
        var template = CreateMagicRow(
            typeId,
            sort,
            level,
            useMana,
            damage: 120,
            experience,
            range: 9,
            useStamina: useStamina,
            coolDown: coolDown,
            coldTime: coldTime);
        var fields = template.Fields.ToArray();
        fields[0] = rowId.ToString(CultureInfo.InvariantCulture);
        fields[1] = typeId.ToString(CultureInfo.InvariantCulture);
        fields[3] = "MortalDrag";
        return new MagicType(rowId, typeId, fields[3], fields);
    }

    private static MsgAttackQuery CreateMortalDragQuery(
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
        ResolveMortalDragSpell(
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

    private static async Task<bool> InvokeLegacyMortalDragAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerMortalDragMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        return await Assert.IsType<Task<bool>>(result);
    }
}
