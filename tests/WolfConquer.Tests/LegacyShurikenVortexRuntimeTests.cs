using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort ShurikenVortexSpellId = 6_010;
    private const ushort ShurikenVortexDamageRowId = 6_009;
    private const ushort ShurikenVortexDamageTypeId = 6_001;
    private const int LegacyShurikenVortexFlag = 0x2E;
    private const string RuntimeLegacyShurikenVortexExpiresAtUtcAttribute = "LegacyShurikenVortexExpiresAtUtc";

    [Fact]
    public async Task HandlerShurikenVortexWithXpListOpensFlagAndSendsSelfAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateShurikenVortexRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [ShurikenVortexSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag, LegacySupermanFlag),
                ["XPCount"] = "11",
            });
        sessions.Set(connection, session);

        var beforeCast = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: ShurikenVortexSpellId,
                x: 833,
                y: 585)).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyXpListFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacySupermanFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyShurikenVortexFlag));
        Assert.Equal("0", updatedSession.Character.Attributes["XPCount"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            RuntimeLegacyShurikenVortexExpiresAtUtcAttribute,
            beforeCast,
            afterCast,
            expectedDurationSeconds: 20);

        var transitionUpdates = ParseMsgUpdates(sent)
            .Where(update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type is LegacyMsgUpdateDataType.XPList or
                    LegacyMsgUpdateDataType.XPCircle or
                    LegacyMsgUpdateDataType.StatusFlag)
            .ToList();
        Assert.Equal(5, transitionUpdates.Count);
        Assert.Equal(LegacyMsgUpdateDataType.XPCircle, transitionUpdates[0].Updates[0].Type);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, transitionUpdates[1].Updates[0].Type);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, transitionUpdates[2].Updates[0].Type);
        Assert.Equal(LegacyMsgUpdateDataType.XPList, transitionUpdates[3].Updates[0].Type);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, transitionUpdates[4].Updates[0].Type);

        var oldSkillRemoved = Assert.Single(transitionUpdates[1].Updates).Values;
        Assert.NotNull(oldSkillRemoved);
        Assert.True(IsLegacyStatusFlagSet(oldSkillRemoved!, LegacyXpListFlag));
        Assert.False(IsLegacyStatusFlagSet(oldSkillRemoved!, LegacySupermanFlag));
        Assert.False(IsLegacyStatusFlagSet(oldSkillRemoved!, LegacyShurikenVortexFlag));

        var shurikenOpened = Assert.Single(transitionUpdates[2].Updates).Values;
        Assert.NotNull(shurikenOpened);
        Assert.True(IsLegacyStatusFlagSet(shurikenOpened!, LegacyXpListFlag));
        Assert.False(IsLegacyStatusFlagSet(shurikenOpened!, LegacySupermanFlag));
        Assert.True(IsLegacyStatusFlagSet(shurikenOpened!, LegacyShurikenVortexFlag));

        var xpListRemoved = Assert.Single(transitionUpdates[4].Updates).Values;
        Assert.NotNull(xpListRemoved);
        Assert.False(IsLegacyStatusFlagSet(xpListRemoved!, LegacyXpListFlag));
        Assert.False(IsLegacyStatusFlagSet(xpListRemoved!, LegacySupermanFlag));
        Assert.True(IsLegacyStatusFlagSet(xpListRemoved!, LegacyShurikenVortexFlag));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(833, animation.X);
        Assert.Equal(585, animation.Y);
        Assert.Equal(ShurikenVortexSpellId, animation.SpellId);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy ShurikenVortex XPList transition", output);
        Assert.Contains("durationSeconds=20", output);
        Assert.Contains("source=CombatGearAttack.cs case 6010 XPList branch", output);
        Assert.DoesNotContain("active damage", output);
    }

    [Fact]
    public async Task HandlerShurikenVortexActiveDamageUsesBorrowed6009RowAndCharacterXpOnly()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var inRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31121, boundX: 839, boundY: 584);
        var outsideStrictRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31122, boundX: 840, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_181, x: 830, y: 593) with
        {
            Life = 50_000,
            MaxLife = 50_000,
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [inRangeSpawn, outsideStrictRangeSpawn],
            [sobNpc],
            [
                CreateShurikenVortexRow(experience: 1_000_000),
                CreateShurikenVortexDamageRow(),
            ]);
        var inRangeMonster = CreateRuntimeMonsterInstance(worldData, inRangeSpawn, monsterFamily);
        var outsideStrictRangeMonster = CreateRuntimeMonsterInstance(worldData, outsideStrictRangeSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    ShurikenVortexSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0),
            ],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyShurikenVortexFlag),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 838,
            y: 584,
            minHitPoints: 80_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        Assert.True(LegacyMagicRuntime.TryResolveSpellByRowId(
            worldData,
            ShurikenVortexDamageRowId,
            out var damageSpell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            inRangeMonster,
            monsterFamily,
            damageSpell);
        var expectedPlayerDamageBeforeNoBreakthrough = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            damageSpell,
            worldData,
            1U);
        var expectedPlayerDamage =
            (expectedPlayerDamageBeforeNoBreakthrough +
                (expectedPlayerDamageBeforeNoBreakthrough * 50 / 100)) / 6;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            damageSpell,
            worldData,
            1U);
        var expectedSobNpcExperience = 0UL;

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: 0,
                X: 830,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ShurikenVortexSpellId)).ToFrameBytes()));

        Assert.DoesNotContain(casterSent, frame =>
            LegacySpellPacket.TryParse(ReadFrame(frame), out var packet) &&
            packet.Id == ShurikenVortexSpellId);

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(830, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(ShurikenVortexSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            Assert.Equal(
                new[] { inRangeMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
                animation.Targets.Select(target => target.Uid).Order().ToArray());
            Assert.Contains(animation.Targets, target =>
                target.Uid == inRangeMonster.Uid &&
                target.Damage == expectedMonsterDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == TargetPlayerUid &&
                target.Damage == expectedPlayerDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == checked((uint)sobNpc.Id) &&
                target.Damage == expectedSobNpcDamage &&
                target.Hit);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideStrictRangeMonster.Uid);
        }

        Assert.True(monsterStates.TryGetVisibleInstance(inRangeMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(inRangeMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(
            outsideStrictRangeMonster,
            DateTimeOffset.UtcNow,
            out var untouchedMonster));
        Assert.Equal(outsideStrictRangeMonster.MaxLife, untouchedMonster.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(80_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(0, Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);
        var adjustedMonsterExperience = InvokePrivateInt32(
            "AdjustLegacyMonsterExperience",
            checked((int)Math.Min(expectedMonsterDamage, inRangeMonster.MaxLife)),
            checked((int)casterSession.Character.Level),
            inRangeMonster.Level);
        var expectedReturnExperience = checked(
            (ulong)Math.Max(adjustedMonsterExperience, 0) + expectedSobNpcExperience);
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(casterSession, expectedReturnExperience));
        Assert.Equal(
            checked((long)expectedExperience.AppliedExperienceGain),
            updatedCasterSession.Character.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy ShurikenVortex active damage", output);
        Assert.Contains("damageSpellRowId=6009", output);
        Assert.Contains("damageSpellTypeId=6001", output);
        Assert.Contains("range=10", output);
        Assert.Contains($"monsterExperience={adjustedMonsterExperience}", output);
        Assert.Contains($"sobNpcExperience={expectedSobNpcExperience}", output);
        Assert.Contains($"returnExperience={expectedReturnExperience}", output);
        Assert.Contains("spellExperienceApplied=False", output);
        Assert.Contains("source=CombatGearAttack.cs case 6010 active branch", output);
        Assert.DoesNotContain("WorldServer updated legacy player magic spell", output);
    }

    private static MagicType CreateShurikenVortexRow(uint experience = 0)
    {
        return CreateMagicRow(
            ShurikenVortexSpellId,
            sort: 28,
            level: 0,
            useMana: 0,
            damage: 30_130,
            experience: experience,
            maxTargets: 12,
            range: 15,
            useStamina: 0,
            coldTime: 0);
    }

    private static MagicType CreateShurikenVortexDamageRow()
    {
        var row = CreateMagicRow(
            ShurikenVortexDamageRowId,
            sort: 27,
            level: 0,
            useMana: 0,
            damage: 30_050,
            range: 10,
            coldTime: 0);
        var fields = row.Fields.ToArray();
        fields[1] = ShurikenVortexDamageTypeId.ToString(CultureInfo.InvariantCulture);
        fields[3] = "ToxicFog";
        return new MagicType(ShurikenVortexDamageRowId, ShurikenVortexDamageTypeId, "ToxicFog", fields);
    }
}
