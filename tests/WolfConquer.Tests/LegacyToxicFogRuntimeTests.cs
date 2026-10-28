using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort ToxicFogSpellId = 6_001;
    private const int LegacyPoisonedFlag = 0x01;

    [Fact]
    public async Task HandlerToxicFogPoisonsVisibleMonsterAndPlayerSendsEmptyAnimationAndProgressesBy1000()
    {
        var spellsRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-toxicfog-spell-xp-" + Guid.NewGuid().ToString("N"),
            "PlayersSpells");
        try
        {
            var monsterFamily = CreateMonsterFamily();
            var poisonedSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 838, boundY: 584);
            var outOfRadiusSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 841, boundY: 584);
            var worldData = CreateMagicCatalogCore(
                monsterFamily,
                [poisonedSpawn, outOfRadiusSpawn],
                sobNpcs: [],
                magicRows:
                [
                    CreateToxicFogRow(experience: 2_000),
                    CreateToxicFogRow(level: 1, experience: 4_000),
                ]);
            var poisonedMonster = CreateRuntimeMonsterInstance(worldData, poisonedSpawn, monsterFamily);
            var outOfRadiusMonster = CreateRuntimeMonsterInstance(worldData, outOfRadiusSpawn, monsterFamily);
            var monsterStates = new MonsterRuntimeStateStore();
            var sessions = new WorldSessionStore();
            var casterSent = new List<byte[]>();
            var targetSent = new List<byte[]>();
            using var log = new StringWriter();
            var casterConnection = CreateSendingConnection(casterSent);
            var targetConnection = CreateSendingConnection(targetSent);
            var casterSession = CreateSession(
                minMana: 50,
                spellEntries:
                [
                    new PlayerSpellSnapshotEntry(
                        ToxicFogSpellId,
                        Level: 0,
                        Experience: 0,
                        PreviousLevel: 0,
                        SoulLevel: 2,
                        UseSpellSoul: 2),
                ]);
            var targetSession = CreateSession(
                uid: TargetPlayerUid,
                name: "Target",
                x: 839,
                y: 584);
            sessions.Set(casterConnection, casterSession);
            sessions.Set(targetConnection, targetSession);
            var context = new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                PlayerSpellsRoot: spellsRoot);
            using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
            context = combatSpellOwner.Context;

            await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(CreateToxicFogQuery(x: 836, y: 584)).ToFrameBytes()));

            var animationIndex = Assert.Single(
                Enumerable.Range(0, casterSent.Count),
                index => LegacySpellAnimationPacket.TryParse(ReadFrame(casterSent[index]), out _));
            var manaUpdateIndex = Assert.Single(
                Enumerable.Range(0, casterSent.Count),
                index => LegacyMsgUpdatePacket.TryParse(ReadFrame(casterSent[index]), out var update) &&
                    update.Updates.Count == 1 &&
                    update.Updates[0].Type == LegacyMsgUpdateDataType.Mana);
            var coldTimeIndex = Assert.Single(
                Enumerable.Range(0, casterSent.Count),
                index => LegacyMagicColdTimePacket.TryParse(ReadFrame(casterSent[index]), out _));
            Assert.True(manaUpdateIndex < animationIndex);
            Assert.True(coldTimeIndex < animationIndex);

            var animation = Assert.Single(ParseSpellAnimations(casterSent));
            AssertToxicFogEmptyAnimation(animation, x: 836, y: 584, levelHu: 2);

            AssertMonsterPoisoned(monsterStates, poisonedMonster, expectedPoisonLevel: 2);
            AssertMonsterNotPoisoned(monsterStates, outOfRadiusMonster);

            Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
            Assert.NotNull(updatedTargetSession);
            AssertPlayerPoisoned(updatedTargetSession, expectedPoisonLevel: 2);

            var spellUpdate = Assert.Single(ParseToxicFogSpellPackets(casterSent));
            Assert.Equal(ToxicFogSpellId, spellUpdate.Id);
            Assert.Equal(0, spellUpdate.Level);
            Assert.Equal(1_000, spellUpdate.Experience);

            Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
            Assert.NotNull(updatedCasterSession);
            Assert.Equal(43, updatedCasterSession.Character.MinMana);
            Assert.NotNull(updatedCasterSession.MagicCooldowns);
            Assert.True(updatedCasterSession.MagicCooldowns.ContainsKey(ToxicFogSpellId));
            var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
            Assert.Equal(ToxicFogSpellId, updatedSpell.Id);
            Assert.Equal(0, updatedSpell.Level);
            Assert.Equal(1_000, updatedSpell.Experience);

            AssertNoToxicFogRuntimeGap(log.ToString());
        }
        finally
        {
            var tempRoot = Path.GetDirectoryName(spellsRoot);
            if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerToxicFogSkipsBossMonsterWithoutProgressButStillSendsEmptyAnimation()
    {
        var bossFamily = CreateLegacyBossFamily(
            monsterTypeId: 9_101,
            sourceMonsterName: "ToxicFogBoss",
            life: "5000",
            lookface: "100");
        var bossSpawn = CreateSingleInstanceMonsterSpawn(
            monsterTypeId: bossFamily.Id,
            sourceMonsterName: bossFamily.Name,
            boundX: 838,
            boundY: 584);
        var worldData = CreateMagicCatalogCore(
            bossFamily,
            [bossSpawn],
            sobNpcs: [],
            magicRows:
            [
                CreateToxicFogRow(experience: 2_000),
                CreateToxicFogRow(level: 1, experience: 4_000),
            ]);
        var bossMonster = CreateRuntimeMonsterInstance(worldData, bossSpawn, bossFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            spellIds: [ToxicFogSpellId]);
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
            ReadFrame(new MsgAttackPacket(CreateToxicFogQuery(x: 836, y: 584)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        AssertToxicFogEmptyAnimation(animation, x: 836, y: 584);
        AssertMonsterNotPoisoned(monsterStates, bossMonster);
        Assert.Empty(ParseToxicFogSpellPackets(sent));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(43, updatedSession.Character.MinMana);
        Assert.Equal(0, Assert.Single(updatedSession.PlayerSpells.Spells).Experience);

        AssertNoToxicFogRuntimeGap(log.ToString());
    }

    [Fact]
    public async Task HandlerToxicFogAgainstGuardMonsterInPkModeAppliesLegacyFlashingName()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var guardSpawn = CreateSingleInstanceMonsterSpawn(
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name,
            boundX: 838,
            boundY: 584);
        var worldData = CreateMagicCatalogCore(
            guardFamily,
            [guardSpawn],
            sobNpcs: [],
            magicRows: [CreateToxicFogRow()]);
        var guardMonster = CreateRuntimeMonsterInstance(worldData, guardSpawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            spellIds: [ToxicFogSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            });
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
            ReadFrame(new MsgAttackPacket(CreateToxicFogQuery(x: 836, y: 584)).ToFrameBytes()));

        AssertMonsterPoisoned(monsterStates, guardMonster, expectedPoisonLevel: 0);
        AssertLegacyGuardFlashingNameApplied(sessions, connection, sent, log);
        AssertNoToxicFogRuntimeGap(log.ToString());
    }

    [Fact]
    public async Task HandlerToxicFogSkipsToxinEraserPlayerWithoutProgressButStillSendsEmptyAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [],
            magicRows:
            [
                CreateToxicFogRow(experience: 2_000),
                CreateToxicFogRow(level: 1, experience: 4_000),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            minMana: 50,
            spellIds: [ToxicFogSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 838,
            y: 584,
            attributes: new Dictionary<string, string>
            {
                ["ToxinEraser"] = "1",
                ["LegacyToxinEraser"] = "1",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateToxicFogQuery(x: 836, y: 584)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        AssertToxicFogEmptyAnimation(animation, x: 836, y: 584);
        Assert.Empty(ParseToxicFogSpellPackets(casterSent));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        AssertPlayerNotPoisoned(updatedTargetSession);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(43, updatedCasterSession.Character.MinMana);
        Assert.Equal(0, Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);

        AssertNoToxicFogRuntimeGap(log.ToString());
    }

    [Fact]
    public async Task HandlerToxicFogDoesNotLogPoisonRuntimeGapWhenResourceGateFails()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateToxicFogRow()]);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(minMana: 0, spellIds: [ToxicFogSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateToxicFogQuery(x: 835, y: 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.Character.MinMana);
        Assert.Null(updatedSession.MagicCooldowns);
        Assert.Empty(ParseMagicColdTimePackets(sent));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseToxicFogSpellPackets(sent));

        var output = log.ToString();
        Assert.Contains("spellId=6001 mana=0 requiredMana=7", output);
        Assert.DoesNotContain("toxic-poison-runtime", output);
        Assert.DoesNotContain("WorldServer skipped legacy ToxicFog magic", output);
    }

    private static MsgAttackQuery CreateToxicFogQuery(ushort x, ushort y)
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
            SpellId: ToxicFogSpellId);
    }

    private static MagicType CreateToxicFogRow(
        byte level = 0,
        uint experience = 0,
        int coldTime = 300,
        uint duration = 30)
    {
        return CreateMagicRow(
            ToxicFogSpellId,
            sort: 27,
            level: level,
            useMana: 7,
            damage: 0,
            experience: experience,
            maxTargets: 0,
            range: 4,
            coldTime: coldTime,
            duration: duration);
    }

    private static void AssertToxicFogEmptyAnimation(
        LegacySpellAnimationPacket animation,
        ushort x,
        ushort y,
        ushort spellLevel = 0,
        byte levelHu = 0)
    {
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(x, animation.X);
        Assert.Equal(y, animation.Y);
        Assert.Equal(ToxicFogSpellId, animation.SpellId);
        Assert.Equal(spellLevel, animation.SpellLevel);
        Assert.Equal(levelHu, animation.LevelHu);
        Assert.Empty(animation.Targets);
    }

    private static void AssertPlayerPoisoned(WorldSession session, byte expectedPoisonLevel)
    {
        Assert.True(
            HasLegacyStatusFlag(session.Character.Attributes, LegacyPoisonedFlag),
            "ToxicFog should add the legacy Poisoned status flag to poisoned players.");
        Assert.True(
            TryReadPoisonLevel(session.Character.Attributes, out var poisonLevel),
            "ToxicFog should expose the player's source PoisonLevel in runtime attributes.");
        Assert.Equal(expectedPoisonLevel, poisonLevel);
    }

    private static void AssertPlayerNotPoisoned(WorldSession session)
    {
        Assert.False(HasLegacyStatusFlag(session.Character.Attributes, LegacyPoisonedFlag));
        if (TryReadPoisonLevel(session.Character.Attributes, out var poisonLevel))
        {
            Assert.Equal((byte)0, poisonLevel);
        }
    }

    private static void AssertMonsterPoisoned(
        MonsterRuntimeStateStore monsterStates,
        LegacyMonsterInstance monster,
        byte expectedPoisonLevel)
    {
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.True(
            TryReadPoisonLevel(visibleMonster, out var poisonLevel) ||
            TryReadMonsterPoisonLevel(monsterStates, monster, out poisonLevel),
            "ToxicFog should expose the monster's source PoisonLevel through visible runtime state.");
        Assert.Equal(expectedPoisonLevel, poisonLevel);
    }

    private static void AssertMonsterNotPoisoned(
        MonsterRuntimeStateStore monsterStates,
        LegacyMonsterInstance monster)
    {
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        if (TryReadPoisonLevel(visibleMonster, out var poisonLevel))
        {
            Assert.Equal((byte)0, poisonLevel);
        }

        Assert.False(
            TryReadMonsterPoisonLevel(monsterStates, monster, out poisonLevel) &&
            poisonLevel != 0);
    }

    private static bool TryReadMonsterPoisonLevel(
        MonsterRuntimeStateStore monsterStates,
        LegacyMonsterInstance monster,
        out byte poisonLevel)
    {
        foreach (var method in typeof(MonsterRuntimeStateStore).GetMethods())
        {
            if (!string.Equals(method.Name, "TryGetPoisonState", StringComparison.Ordinal) ||
                method.ReturnType != typeof(bool))
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != 3 ||
                parameters[0].ParameterType != typeof(LegacyMonsterInstance) ||
                parameters[1].ParameterType != typeof(DateTimeOffset) ||
                !parameters[2].ParameterType.IsByRef)
            {
                continue;
            }

            object?[] arguments = [monster, DateTimeOffset.UtcNow, null];
            if (method.Invoke(monsterStates, arguments) is true &&
                arguments[2] is { } poisonState &&
                TryReadPoisonLevel(poisonState, out poisonLevel))
            {
                return true;
            }
        }

        poisonLevel = 0;
        return false;
    }

    private static bool TryReadPoisonLevel(object value, out byte poisonLevel)
    {
        foreach (var propertyName in new[] { "PoisonLevel", "LegacyPoisonLevel", "LegacyToxicPoisonLevel" })
        {
            var property = value.GetType().GetProperty(propertyName);
            if (property is not null &&
                TryConvertPoisonLevel(property.GetValue(value), out poisonLevel))
            {
                return true;
            }
        }

        var attributesProperty = value.GetType().GetProperty("Attributes");
        if (attributesProperty?.GetValue(value) is IReadOnlyDictionary<string, string> attributes)
        {
            return TryReadPoisonLevel(attributes, out poisonLevel);
        }

        poisonLevel = 0;
        return false;
    }

    private static bool TryReadPoisonLevel(
        IReadOnlyDictionary<string, string> attributes,
        out byte poisonLevel)
    {
        foreach (var key in new[] { "PoisonLevel", "LegacyPoisonLevel", "LegacyToxicPoisonLevel" })
        {
            if (attributes.TryGetValue(key, out var raw) &&
                byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out poisonLevel))
            {
                return true;
            }
        }

        poisonLevel = 0;
        return false;
    }

    private static bool TryConvertPoisonLevel(object? raw, out byte poisonLevel)
    {
        switch (raw)
        {
            case byte value:
                poisonLevel = value;
                return true;
            case ushort value when value <= byte.MaxValue:
                poisonLevel = (byte)value;
                return true;
            case int value when value is >= 0 and <= byte.MaxValue:
                poisonLevel = (byte)value;
                return true;
            case uint value when value <= byte.MaxValue:
                poisonLevel = (byte)value;
                return true;
            case string value when byte.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed):
                poisonLevel = parsed;
                return true;
            default:
                poisonLevel = 0;
                return false;
        }
    }

    private static List<LegacySpellPacket> ParseToxicFogSpellPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacySpellPacket>();
        foreach (var bytes in frames)
        {
            if (LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static void AssertNoToxicFogRuntimeGap(string output)
    {
        Assert.DoesNotContain("gap=toxic-poison-runtime", output);
        Assert.DoesNotContain("WorldServer skipped legacy ToxicFog magic", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
        Assert.DoesNotContain("gap=unsupported-magic", output);
    }
}
