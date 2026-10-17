using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const string RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute =
        "LegacyShurikenVortexNextAttackAtUtc";
    private static readonly TimeSpan ShurikenVortexInvokeInterval = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task HandlerShurikenVortexWithoutXpListPreparesStaminaThenManaAndStopsBeforeFlag46()
    {
        var result = await RunShurikenVortexActivationContractCastAsync(hasXpList: false);

        Assert.Equal(493, result.Session.Character.MinMana);
        Assert.Equal("91", result.Session.Character.Attributes["Stamina"]);
        Assert.Equal("37", result.Session.Character.Attributes["XPCount"]);
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyShurikenVortexFlag));
        Assert.False(result.Session.Character.Attributes.ContainsKey(
            RuntimeLegacyShurikenVortexExpiresAtUtcAttribute));
        Assert.False(result.Session.Character.Attributes.ContainsKey(
            RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute));

        var staminaIndex = SingleFrameIndex(result.Sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 91)]));
        var manaIndex = SingleFrameIndex(result.Sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)]));
        Assert.True(staminaIndex < manaIndex);
        Assert.DoesNotContain(ParseMsgUpdates(result.Sent), update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.XPList or
                LegacyMsgUpdateDataType.XPCircle or
                LegacyMsgUpdateDataType.StatusFlag));
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Contains("XPList and ShurikenVortex status flags are not active", result.Log);
    }

    [Fact]
    public async Task HandlerShurikenVortexWithXpListPreparesResourcesOpensFlag46ForTwentySecondsWithOneSecondInvokeThenSelfAnimates()
    {
        var result = await RunShurikenVortexActivationContractCastAsync(hasXpList: true);

        Assert.Equal(493, result.Session.Character.MinMana);
        Assert.Equal("91", result.Session.Character.Attributes["Stamina"]);
        Assert.Equal("0", result.Session.Character.Attributes["XPCount"]);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyShurikenVortexFlag));
        AssertLegacyXpSkillExpiresAround(
            result.Session.Character.Attributes,
            RuntimeLegacyShurikenVortexExpiresAtUtcAttribute,
            result.BeforeCast,
            result.AfterCast,
            expectedDurationSeconds: 20);

        var animation = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(833, animation.X);
        Assert.Equal(585, animation.Y);
        Assert.Equal(ShurikenVortexSpellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(2, animation.LevelHu);
        var self = Assert.Single(animation.Targets);
        Assert.Equal(PlayerUid, self.Uid);
        Assert.Equal(0U, self.Damage);
        Assert.True(self.Hit);
        Assert.Equal(MsgAttackEffect.None, self.Effect);

        var staminaIndex = SingleFrameIndex(result.Sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 91)]));
        var manaIndex = SingleFrameIndex(result.Sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)]));
        var firstXpTransitionIndex = Enumerable.Range(0, result.Sent.Count)
            .Where(index =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(result.Sent[index]), out var packet) &&
                packet.Updates.Any(entry => entry.Type is
                    LegacyMsgUpdateDataType.XPList or
                    LegacyMsgUpdateDataType.XPCircle or
                    LegacyMsgUpdateDataType.StatusFlag))
            .Min();
        var animationIndex = SingleFrameIndex(result.Sent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
            packet.SpellId == ShurikenVortexSpellId);
        Assert.True(staminaIndex < manaIndex);
        Assert.True(manaIndex < firstXpTransitionIndex);
        Assert.True(firstXpTransitionIndex < animationIndex);
        Assert.Equal(LegacySpellAnimationPacket.Opcode, ReadFrame(result.Sent[^1]).Header.Opcode);

        AssertTimestampAround(
            result.Session.Character.Attributes,
            RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute,
            result.BeforeCast + ShurikenVortexInvokeInterval,
            result.AfterCast + ShurikenVortexInvokeInterval);
    }

    [Fact]
    public async Task HandlerShurikenVortexDuringAutoHuntConsumesXpListWithoutOpeningFlag46()
    {
        var result = await RunShurikenVortexActivationContractCastAsync(
            hasXpList: true,
            onAutoHunt: true);

        Assert.Equal("37", result.Session.Character.Attributes["XPCount"]);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyShurikenVortexFlag));
        Assert.False(result.Session.Character.Attributes.ContainsKey(
            RuntimeLegacyShurikenVortexExpiresAtUtcAttribute));
        Assert.False(result.Session.Character.Attributes.ContainsKey(
            RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute));

        var updates = ParseMsgUpdates(result.Sent);
        Assert.Contains(updates, update => update.Updates.SequenceEqual(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)]));
        Assert.DoesNotContain(updates, update => update.Updates.Any(entry =>
            entry.Type == LegacyMsgUpdateDataType.XPCircle));
        var statusUpdate = Assert.Single(updates, update => update.Updates.Any(entry =>
            entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyXpListFlag));
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacySupermanFlag));
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyShurikenVortexFlag));
        Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Contains("autoHunt=True", result.Log);
    }

    [Fact]
    public async Task HandlerShurikenVortexRefreshExtendsExpiryWithoutResettingPendingInvoke()
    {
        var pendingInvoke = DateTimeOffset.UtcNow.AddSeconds(8).ToUniversalTime();
        var result = await RunShurikenVortexActivationContractCastAsync(
            hasXpList: true,
            hasExistingShuriken: true,
            existingNextAttackAt: pendingInvoke);

        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyShurikenVortexFlag));
        AssertLegacyXpSkillExpiresAround(
            result.Session.Character.Attributes,
            RuntimeLegacyShurikenVortexExpiresAtUtcAttribute,
            result.BeforeCast,
            result.AfterCast,
            expectedDurationSeconds: 20);
        AssertTimestampEquals(
            result.Session.Character.Attributes,
            RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute,
            pendingInvoke);

        var transitionUpdates = ParseMsgUpdates(result.Sent)
            .Where(update => update.Updates.Count == 1 &&
                update.Updates[0].Type is LegacyMsgUpdateDataType.XPCircle or
                    LegacyMsgUpdateDataType.XPList or
                    LegacyMsgUpdateDataType.StatusFlag)
            .ToList();
        Assert.Equal(3, transitionUpdates.Count);
        Assert.Equal(LegacyMsgUpdateDataType.XPCircle, transitionUpdates[0].Updates[0].Type);
        Assert.Equal(LegacyMsgUpdateDataType.XPList, transitionUpdates[1].Updates[0].Type);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, transitionUpdates[2].Updates[0].Type);
        Assert.Contains("refreshedShurikenVortex=True", result.Log);
    }

    [Fact]
    public async Task DueShurikenVortexAtExpiryAnimatesBeforeFlag46AndCadenceMetadataAreRemoved()
    {
        var expiresAt = DateTimeOffset.Parse(
            "2026-07-19T20:00:00Z",
            CultureInfo.InvariantCulture);
        var worldData = CreateMagicCatalog(
            CreateHighLifeMonsterFamily(),
            [],
            CreateShurikenVortexAutomaticContractRow(),
            CreateShurikenVortexDamageRow());
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var owner = CreateSession(
            x: 830,
            y: 584,
            spellIds: [ShurikenVortexSpellId],
            attributes: CreateActiveShurikenVortexContractAttributes(
                expiresAt,
                nextAttackAt: expiresAt));
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log: log);
        var sessions = GetHandlerSessions(handler);
        sessions.Set(connection, owner);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            owner,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());

        Assert.True(await InvokeDueLegacyShurikenVortexAttackAsync(context, expiresAt));
        await InvokeShurikenVortexXpSkillExpirationsAsync(handler, expiresAt);

        var expired = AssertHandlerSession(handler, connection);
        Assert.False(HasLegacyStatusFlag(expired.Character.Attributes, LegacyShurikenVortexFlag));
        Assert.False(expired.Character.Attributes.ContainsKey(
            RuntimeLegacyShurikenVortexExpiresAtUtcAttribute));
        Assert.False(expired.Character.Attributes.ContainsKey(
            RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute));
        var animationIndex = SingleFrameIndex(sent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
            packet.SpellId == ShurikenVortexSpellId);
        var expiredStatusIndex = SingleFrameIndex(sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        Assert.True(animationIndex < expiredStatusIndex);
    }

    [Fact]
    public async Task DueShurikenVortexFlag46CadenceSynthesizes6010UsesRow6009StrictRangeSourceTargetOrderDamageAndCombinedCharacterXp()
    {
        var now = DateTimeOffset.UtcNow;
        var monsterFamily = CreateHighLifeMonsterFamily();
        var insideSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 36_091,
            boundX: 839,
            boundY: 584) with
        {
            MapId = 1_038,
            SourceRelativePath = "1038/36091.txt",
        };
        var strictBoundarySpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 36_092,
            boundX: 840,
            boundY: 584) with
        {
            MapId = 1_038,
            SourceRelativePath = "1038/36092.txt",
        };
        var insideSobNpc = CreateMoveLineSobNpc(id: 25_091, x: 839, y: 584) with
        {
            MapId = 1_038,
            Life = 500_000,
            MaxLife = 500_000,
        };
        var strictBoundarySobNpc = CreateMoveLineSobNpc(id: 25_092, x: 840, y: 584) with
        {
            MapId = 1_038,
            Life = 500_000,
            MaxLife = 500_000,
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [insideSpawn, strictBoundarySpawn],
            [insideSobNpc, strictBoundarySobNpc],
            [
                CreateShurikenVortexAutomaticContractRow(),
                CreateShurikenVortexDamageRow(),
            ]);
        var insideMonster = CreateRuntimeMonsterInstance(worldData, insideSpawn, monsterFamily);
        var strictBoundaryMonster = CreateRuntimeMonsterInstance(worldData, strictBoundarySpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var insidePlayerSent = new List<byte[]>();
        var strictBoundaryPlayerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var insidePlayerConnection = CreateSendingConnection(insidePlayerSent);
        var strictBoundaryPlayerConnection = CreateSendingConnection(strictBoundaryPlayerSent);
        var caster = CreateSession(
            x: 830,
            y: 584,
            mapId: 1_038,
            strength: 300,
            spellIds: [ShurikenVortexSpellId],
            attributes: CreateActiveShurikenVortexContractAttributes(
                now.AddSeconds(10),
                nextAttackAt: now.AddTicks(-1)));
        var insidePlayer = CreateSession(
            uid: TargetPlayerUid,
            name: "ShurikenInsidePlayer",
            x: 839,
            y: 584,
            mapId: 1_038,
            minHitPoints: 100_000);
        var strictBoundaryPlayer = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "ShurikenBoundaryPlayer",
            x: 840,
            y: 584,
            mapId: 1_038,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, caster);
        sessions.Set(insidePlayerConnection, insidePlayer);
        sessions.Set(strictBoundaryPlayerConnection, strictBoundaryPlayer);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);

        Assert.True(LegacyMagicRuntime.TryResolveSpellByRowId(
            worldData,
            ShurikenVortexDamageRowId,
            out var damageSpell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            caster,
            worldData,
            insideMonster,
            monsterFamily,
            damageSpell);
        var expectedPlayerDamageBeforeNoBreakthrough = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            caster,
            insidePlayer,
            damageSpell,
            worldData,
            1U);
        var expectedPlayerDamage =
            (expectedPlayerDamageBeforeNoBreakthrough +
                (expectedPlayerDamageBeforeNoBreakthrough * 50 / 100)) / 6;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            caster,
            damageSpell,
            worldData,
            1U);
        Assert.True(expectedSobNpcDamage < insideSobNpc.Life);

        Assert.True(await InvokeDueLegacyShurikenVortexAttackAsync(context, now));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(830, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(ShurikenVortexSpellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(0, animation.LevelHu);
        Assert.Equal(
            [insideMonster.Uid, TargetPlayerUid, checked((uint)insideSobNpc.Id)],
            animation.Targets.Select(target => target.Uid).ToArray());
        Assert.Equal(expectedMonsterDamage, animation.Targets[0].Damage);
        Assert.Equal(expectedPlayerDamage, animation.Targets[1].Damage);
        Assert.Equal(expectedSobNpcDamage, animation.Targets[2].Damage);
        Assert.All(animation.Targets, target => Assert.True(target.Hit));

        Assert.True(monsterStates.TryGetVisibleInstance(insideMonster, now, out var damagedMonster));
        Assert.Equal(insideMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(
            strictBoundaryMonster,
            now,
            out var untouchedBoundaryMonster));
        Assert.Equal(strictBoundaryMonster.MaxLife, untouchedBoundaryMonster.Hitpoints);
        Assert.True(sessions.TryGet(insidePlayerConnection, out var damagedPlayer));
        Assert.NotNull(damagedPlayer);
        Assert.Equal(100_000 - expectedPlayerDamage, damagedPlayer.Character.MinHitPoints);
        Assert.True(sessions.TryGet(strictBoundaryPlayerConnection, out var untouchedBoundaryPlayer));
        Assert.NotNull(untouchedBoundaryPlayer);
        Assert.Equal(100_000, untouchedBoundaryPlayer.Character.MinHitPoints);
        Assert.Equal(
            insideSobNpc.Life - checked((int)expectedSobNpcDamage),
            sobNpcStates.GetVisible(insideSobNpc).Life);
        Assert.Equal(strictBoundarySobNpc.Life, sobNpcStates.GetVisible(strictBoundarySobNpc).Life);

        var monsterReturnExperience = InvokePrivateInt32(
            "AdjustLegacyMonsterExperience",
            checked((int)Math.Min(expectedMonsterDamage, insideMonster.MaxLife)),
            checked((int)caster.Character.Level),
            insideMonster.Level);
        var sobNpcReturnExperience = expectedSobNpcDamage / 1_000U;
        var combinedSourceReturnExperience = checked(
            (ulong)monsterReturnExperience + sobNpcReturnExperience);
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(caster, combinedSourceReturnExperience));
        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(
            checked((long)expectedExperience.AppliedExperienceGain),
            updatedCaster.Character.Experience);
        Assert.Equal(
            0,
            Assert.Single(updatedCaster.PlayerSpells.Spells, spell =>
                spell.Id == ShurikenVortexSpellId).Experience);
        AssertTimestampEquals(
            updatedCaster.Character.Attributes,
            RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute,
            now + ShurikenVortexInvokeInterval);

        var experienceIndex = SingleFrameIndex(casterSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
        var animationIndex = SingleFrameIndex(casterSent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
            packet.SpellId == ShurikenVortexSpellId);
        Assert.True(experienceIndex < animationIndex);
    }

    [Fact]
    public async Task DueShurikenVortexFlag46CadenceWithNoTargetsSendsEmpty6010AnimationThenNextTickBeforeOneSecondIsSilent()
    {
        var now = DateTimeOffset.UtcNow;
        var worldData = CreateMagicCatalog(
            CreateHighLifeMonsterFamily(),
            [],
            CreateShurikenVortexAutomaticContractRow(),
            CreateShurikenVortexDamageRow());
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [ShurikenVortexSpellId],
            attributes: CreateActiveShurikenVortexContractAttributes(
                now.AddSeconds(10),
                nextAttackAt: now.AddTicks(-1)));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());

        Assert.True(await InvokeDueLegacyShurikenVortexAttackAsync(context, now));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(830, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(ShurikenVortexSpellId, animation.SpellId);
        Assert.Empty(animation.Targets);
        Assert.DoesNotContain(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
            entry.Type == LegacyMsgUpdateDataType.Experience));
        Assert.Equal(LegacySpellAnimationPacket.Opcode, ReadFrame(sent[^1]).Header.Opcode);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        AssertTimestampEquals(
            updatedSession.Character.Attributes,
            RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute,
            now + ShurikenVortexInvokeInterval);

        sent.Clear();
        var notDueContext = context with { Session = updatedSession };
        Assert.False(await InvokeDueLegacyShurikenVortexAttackAsync(
            notDueContext,
            now.AddMilliseconds(999)));
        Assert.Empty(sent);
    }

    private static async Task<ShurikenVortexActivationContractResult>
        RunShurikenVortexActivationContractCastAsync(
            bool hasXpList,
            bool onAutoHunt = false,
            bool hasExistingShuriken = false,
            DateTimeOffset? existingNextAttackAt = null)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateShurikenVortexAutomaticContractRow(useMana: 7, useStamina: 9));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var activeXpSkillFlag = hasExistingShuriken
            ? LegacyShurikenVortexFlag
            : LegacySupermanFlag;
        var flags = hasXpList
            ? BuildStatusFlags(LegacyXpListFlag, activeXpSkillFlag)
            : BuildStatusFlags(activeXpSkillFlag);
        var attributes = new Dictionary<string, string>
        {
            ["StatusFlags"] = flags,
            ["XPCount"] = "37",
            ["PkMode"] = "0",
            ["OnAutoHunt"] = onAutoHunt ? "1" : "0",
        };
        if (hasExistingShuriken)
        {
            attributes[RuntimeLegacyShurikenVortexExpiresAtUtcAttribute] =
                DateTimeOffset.UtcNow.AddSeconds(3).ToString("O", CultureInfo.InvariantCulture);
            attributes[RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute] =
                (existingNextAttackAt ?? DateTimeOffset.UtcNow.AddSeconds(1))
                .ToString("O", CultureInfo.InvariantCulture);
        }

        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    ShurikenVortexSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 2),
            ],
            attributes: attributes);
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

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, afterCast, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        return new ShurikenVortexActivationContractResult(
            updatedSession,
            sent,
            log.ToString(),
            beforeCast,
            afterCast);
    }

    private static MagicType CreateShurikenVortexAutomaticContractRow(
        ushort useMana = 0,
        byte useStamina = 0)
    {
        return CreateMagicRow(
            ShurikenVortexSpellId,
            sort: (byte)LegacyMagicSort.ShurikenVortex,
            level: 0,
            useMana: useMana,
            damage: 30_130,
            experience: 1_000_000,
            maxTargets: 12,
            range: 15,
            useStamina: useStamina,
            coolDown: 0,
            coldTime: 0);
    }

    private static Dictionary<string, string> CreateActiveShurikenVortexContractAttributes(
        DateTimeOffset expiresAt,
        DateTimeOffset nextAttackAt)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(LegacyShurikenVortexFlag),
            ["XPCount"] = "0",
            ["PkMode"] = "0",
            [RuntimeLegacyShurikenVortexExpiresAtUtcAttribute] =
                expiresAt.ToString("O", CultureInfo.InvariantCulture),
            [RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute] =
                nextAttackAt.ToString("O", CultureInfo.InvariantCulture),
        };
    }

    private static async ValueTask<bool> InvokeDueLegacyShurikenVortexAttackAsync(
        WorldPacketHandlerContext context,
        DateTimeOffset now)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "TrySendDueLegacyShurikenVortexAttackAsync",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var invocation = method!.Invoke(null, [context, now, CancellationToken.None]);
        return await Assert.IsType<ValueTask<bool>>(invocation);
    }

    private static async ValueTask InvokeShurikenVortexXpSkillExpirationsAsync(
        WorldServerFrameHandler handler,
        DateTimeOffset now)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "SendDueLegacyXpSkillExpirationsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var invocation = Assert.IsType<ValueTask>(method.Invoke(
            handler,
            [now, CancellationToken.None]));
        await invocation;
    }

    private static void AssertTimestampAround(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        DateTimeOffset minimum,
        DateTimeOffset maximum)
    {
        Assert.True(attributes.TryGetValue(key, out var raw));
        Assert.True(DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var value));
        Assert.InRange(value, minimum.ToUniversalTime(), maximum.ToUniversalTime());
    }

    private static void AssertTimestampEquals(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        DateTimeOffset expected)
    {
        Assert.True(attributes.TryGetValue(key, out var raw));
        Assert.True(DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var value));
        Assert.Equal(expected.ToUniversalTime(), value);
    }

    private sealed record ShurikenVortexActivationContractResult(
        WorldSession Session,
        IReadOnlyList<byte[]> Sent,
        string Log,
        DateTimeOffset BeforeCast,
        DateTimeOffset AfterCast);
}
