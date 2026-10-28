using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const byte FatalSpinRuntimeSort = 73;
    private const ushort FatalSpinRuntimeSpellId = 12_110;
    private const ushort DragonPunchRuntimeSpellId = 12_240;
    private const ushort ShadowFistRuntimeSpellId = 15_410;
    private const uint ShadowFistRuneItemId = 4_021_301;

    [Fact]
    public async Task FatalSpinHandlerRejectsAnotherSortWithoutSpendingResources()
    {
        var row = CreateFatalSpinRuntimeRow(
            FatalSpinRuntimeSpellId,
            sort: FatalSpinRuntimeSort - 1,
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
            spellIds: [FatalSpinRuntimeSpellId]);
        sessions.Set(connection, session);
        var context = CreateFatalSpinContext(
            connection,
            log,
            session,
            worldData,
            sessions,
            new MonsterRuntimeStateStore(),
            new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolveFatalSpinRuntimeSpell(session, worldData, FatalSpinRuntimeSpellId);

        var handled = await InvokeLegacyFatalSpinAsync(
            context,
            session,
            CreateFatalSpinRuntimeQuery(FatalSpinRuntimeSpellId),
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
    public async Task FatalSpinUnsupportedAdjacentIdSpendsResourcesAndStartsCooldownBeforeIdSwitch()
    {
        const ushort adjacentSpellId = FatalSpinRuntimeSpellId + 1;
        var row = CreateFatalSpinRuntimeRow(
            adjacentSpellId,
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
            spellIds: [adjacentSpellId]);
        sessions.Set(connection, session);
        var context = CreateFatalSpinContext(
            connection,
            log,
            session,
            worldData,
            sessions,
            new MonsterRuntimeStateStore(),
            new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolveFatalSpinRuntimeSpell(session, worldData, adjacentSpellId);

        var handled = await InvokeLegacyFatalSpinAsync(
            context,
            session,
            CreateFatalSpinRuntimeQuery(adjacentSpellId),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseFatalSpinInteractions(sent, adjacentSpellId));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(493, updated.Character.MinMana);
        Assert.Equal("95", updated.Character.Attributes["Stamina"]);
        Assert.True(updated.MagicCooldowns?.ContainsKey(adjacentSpellId));

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
                packet.Spells.SequenceEqual([new LegacyMagicColdTimeSpell(adjacentSpellId, 300)]));
        Assert.True(staminaIndex < manaIndex);
        Assert.True(manaIndex < coldTimeIndex);
        Assert.Contains("CanUseSpell before unsupported ID switch", log.ToString());
    }

    [Fact]
    public async Task FatalSpin12110UsesWidthTwoStrictMonsterSobAndInclusiveOneTilePlayerGeometry()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var hitMonsterSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 834, boundY: 586);
        var boundaryMonsterSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 835, boundY: 584);
        var outsideWidthMonsterSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 834, boundY: 587);
        var hitSobNpc = CreateFatalSpinSobNpc(id: 62_110, x: 834, y: 586);
        var boundarySobNpc = CreateFatalSpinSobNpc(id: 62_111, x: 835, y: 584);
        var row = CreateFatalSpinRuntimeRow(
            FatalSpinRuntimeSpellId,
            range: 5,
            experience: 1_000_000);
        var nextLevelRow = CreateFatalSpinRuntimeRow(
            FatalSpinRuntimeSpellId,
            level: 1,
            range: 5,
            experience: 1_000_000);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [hitMonsterSpawn, boundaryMonsterSpawn, outsideWidthMonsterSpawn],
            [hitSobNpc, boundarySobNpc],
            [row, nextLevelRow]);
        var hitMonster = CreateRuntimeMonsterInstance(worldData, hitMonsterSpawn, monsterFamily);
        var boundaryMonster = CreateRuntimeMonsterInstance(worldData, boundaryMonsterSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthMonsterSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var playerSent = new List<byte[]>();
        var outsidePlayerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var playerConnection = CreateSendingConnection(playerSent);
        var outsidePlayerConnection = CreateSendingConnection(outsidePlayerSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    FatalSpinRuntimeSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 3,
                    UseSpellSoul: 3),
            ]);
        var playerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "FatalSpinPlayer",
            x: 831,
            y: 585,
            minHitPoints: 100_000);
        var outsidePlayerSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "FatalSpinOutsidePlayer",
            x: 832,
            y: 586,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(playerConnection, playerSession);
        sessions.Set(outsidePlayerConnection, outsidePlayerSession);
        var context = CreateFatalSpinContext(
            casterConnection,
            log,
            casterSession,
            worldData,
            sessions,
            monsterStates,
            sobNpcStates);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;
        var (playerSpell, spell) = ResolveFatalSpinRuntimeSpell(
            casterSession,
            worldData,
            FatalSpinRuntimeSpellId);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            hitMonster,
            monsterFamily,
            spell);
        var expectedPlayerBaseDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            playerSession,
            spell,
            worldData,
            1U);
        var expectedPlayerDamage = checked(expectedPlayerBaseDamage * 3 / 9);
        var expectedSobNpcBaseDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcDamage = checked(expectedSobNpcBaseDamage * 30 / 100);
        var expectedProgress = checked((uint)InvokePrivateInt32(
            "AdjustLegacyMonsterExperience",
            checked((int)Math.Min(expectedMonsterDamage, hitMonster.MaxLife)),
            checked((int)casterSession.Character.Level),
            hitMonster.Level));
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(casterSession, expectedProgress));

        var handled = await InvokeLegacyFatalSpinAsync(
            context,
            casterSession,
            CreateFatalSpinRuntimeQuery(FatalSpinRuntimeSpellId, x: 840, y: 584),
            playerSpell,
            spell);

        Assert.True(handled);
        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(FatalSpinRuntimeSpellId, animation.SpellId);
        Assert.Equal(3, animation.LevelHu);
        Assert.Equal(840, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(
            [hitMonster.Uid, TargetPlayerUid, checked((uint)hitSobNpc.Id)],
            animation.Targets.Select(target => target.Uid).ToArray());
        Assert.Equal(expectedMonsterDamage, animation.Targets[0].Damage);
        Assert.Equal(expectedPlayerDamage, animation.Targets[1].Damage);
        Assert.Equal(expectedSobNpcDamage, animation.Targets[2].Damage);

        Assert.True(monsterStates.TryGetVisibleInstance(hitMonster, DateTimeOffset.UtcNow, out var visibleHitMonster));
        Assert.True(monsterStates.TryGetVisibleInstance(boundaryMonster, DateTimeOffset.UtcNow, out var visibleBoundaryMonster));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideWidthMonster));
        Assert.Equal(hitMonster.MaxLife - expectedMonsterDamage, visibleHitMonster.Hitpoints);
        Assert.Equal(boundaryMonster.MaxLife, visibleBoundaryMonster.Hitpoints);
        Assert.Equal(outsideWidthMonster.MaxLife, visibleOutsideWidthMonster.Hitpoints);
        Assert.Equal(hitSobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(hitSobNpc).Life);
        Assert.Equal(boundarySobNpc.Life, sobNpcStates.GetVisible(boundarySobNpc).Life);

        Assert.True(sessions.TryGet(playerConnection, out var updatedPlayer));
        Assert.NotNull(updatedPlayer);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedPlayer.Character.MinHitPoints);
        Assert.True(sessions.TryGet(outsidePlayerConnection, out var unchangedOutsidePlayer));
        Assert.NotNull(unchangedOutsidePlayer);
        Assert.Equal(100_000, unchangedOutsidePlayer.Character.MinHitPoints);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(checked((long)expectedExperience.AppliedExperienceGain), updatedCaster.Character.Experience);
        Assert.Equal(checked((int)expectedProgress), Assert.Single(updatedCaster.PlayerSpells.Spells).Experience);

        Assert.Single(ParseFatalSpinInteractions(casterSent, FatalSpinRuntimeSpellId));
        Assert.Single(ParseFatalSpinInteractions(playerSent, FatalSpinRuntimeSpellId));
        Assert.Single(ParseFatalSpinInteractions(outsidePlayerSent, FatalSpinRuntimeSpellId));
        var interactionIndex = SingleFrameIndex(
            casterSent,
            frame => TryParseFatalSpinInteraction(frame, FatalSpinRuntimeSpellId, out _));
        var animationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == FatalSpinRuntimeSpellId);
        Assert.True(interactionIndex < animationIndex);
        Assert.Contains("playerRange=1 playerRangeInclusive=True", log.ToString());
        Assert.Contains("animationSent=True", log.ToString());
    }

    [Fact]
    public async Task FatalSpin12110EchoesInteractionButSuppressesAnimationWhenNoTargetMatches()
    {
        var row = CreateFatalSpinRuntimeRow(FatalSpinRuntimeSpellId, range: 5);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [FatalSpinRuntimeSpellId]);
        sessions.Set(connection, session);
        var context = CreateFatalSpinContext(
            connection,
            log,
            session,
            worldData,
            sessions,
            new MonsterRuntimeStateStore(),
            new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolveFatalSpinRuntimeSpell(session, worldData, FatalSpinRuntimeSpellId);

        var handled = await InvokeLegacyFatalSpinAsync(
            context,
            session,
            CreateFatalSpinRuntimeQuery(FatalSpinRuntimeSpellId),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.Single(ParseFatalSpinInteractions(sent, FatalSpinRuntimeSpellId));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Contains("animationSent=False", log.ToString());
    }

    [Fact]
    public async Task FatalSpin12240UsesExactLineStrictRangesAndSourcePlayerSobScaling()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var hitMonsterSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 834, boundY: 584);
        var offLineMonsterSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 834, boundY: 585);
        var hitSobNpc = CreateFatalSpinSobNpc(id: 62_240, x: 832, y: 584);
        var offLineSobNpc = CreateFatalSpinSobNpc(id: 62_241, x: 832, y: 585);
        var row = CreateFatalSpinRuntimeRow(
            DragonPunchRuntimeSpellId,
            range: 5,
            experience: 1_000_000);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [hitMonsterSpawn, offLineMonsterSpawn],
            [hitSobNpc, offLineSobNpc],
            [row]);
        var hitMonster = CreateRuntimeMonsterInstance(worldData, hitMonsterSpawn, monsterFamily);
        var offLineMonster = CreateRuntimeMonsterInstance(worldData, offLineMonsterSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var playerSent = new List<byte[]>();
        var offLinePlayerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var playerConnection = CreateSendingConnection(playerSent);
        var offLinePlayerConnection = CreateSendingConnection(offLinePlayerSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [DragonPunchRuntimeSpellId]);
        var playerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "DragonPunchPlayer",
            x: 833,
            y: 584,
            minHitPoints: 100_000);
        var offLinePlayerSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "DragonPunchOffLine",
            x: 833,
            y: 585,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(playerConnection, playerSession);
        sessions.Set(offLinePlayerConnection, offLinePlayerSession);
        var context = CreateFatalSpinContext(
            casterConnection,
            log,
            casterSession,
            worldData,
            sessions,
            monsterStates,
            sobNpcStates);
        var (playerSpell, spell) = ResolveFatalSpinRuntimeSpell(
            casterSession,
            worldData,
            DragonPunchRuntimeSpellId);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            hitMonster,
            monsterFamily,
            spell);
        var expectedPlayerBaseDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            playerSession,
            spell,
            worldData,
            1U);
        var expectedPlayerDamage = checked(expectedPlayerBaseDamage * 12 / 40);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);

        var handled = await InvokeLegacyFatalSpinAsync(
            context,
            casterSession,
            CreateFatalSpinRuntimeQuery(DragonPunchRuntimeSpellId, x: 840, y: 584),
            playerSpell,
            spell);

        Assert.True(handled);
        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(DragonPunchRuntimeSpellId, animation.SpellId);
        Assert.Equal(
            [hitMonster.Uid, TargetPlayerUid, checked((uint)hitSobNpc.Id)],
            animation.Targets.Select(target => target.Uid).ToArray());
        Assert.Equal(expectedMonsterDamage, animation.Targets[0].Damage);
        Assert.Equal(expectedPlayerDamage, animation.Targets[1].Damage);
        Assert.Equal(expectedSobNpcDamage, animation.Targets[2].Damage);

        Assert.True(monsterStates.TryGetVisibleInstance(hitMonster, DateTimeOffset.UtcNow, out var visibleHitMonster));
        Assert.True(monsterStates.TryGetVisibleInstance(offLineMonster, DateTimeOffset.UtcNow, out var visibleOffLineMonster));
        Assert.Equal(hitMonster.MaxLife - expectedMonsterDamage, visibleHitMonster.Hitpoints);
        Assert.Equal(offLineMonster.MaxLife, visibleOffLineMonster.Hitpoints);
        Assert.Equal(hitSobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(hitSobNpc).Life);
        Assert.Equal(offLineSobNpc.Life, sobNpcStates.GetVisible(offLineSobNpc).Life);
        Assert.True(sessions.TryGet(playerConnection, out var updatedPlayer));
        Assert.NotNull(updatedPlayer);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedPlayer.Character.MinHitPoints);
        Assert.True(sessions.TryGet(offLinePlayerConnection, out var unchangedOffLinePlayer));
        Assert.NotNull(unchangedOffLinePlayer);
        Assert.Equal(100_000, unchangedOffLinePlayer.Character.MinHitPoints);
        Assert.Single(ParseFatalSpinInteractions(casterSent, DragonPunchRuntimeSpellId));
        Assert.Contains("shadowFistTriggered=False", log.ToString());
    }

    [Fact]
    public async Task FatalSpin12240AlwaysSendsEmptyAnimationAfterInteraction()
    {
        var row = CreateFatalSpinRuntimeRow(DragonPunchRuntimeSpellId, range: 5);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [DragonPunchRuntimeSpellId]);
        sessions.Set(connection, session);
        var context = CreateFatalSpinContext(
            connection,
            log,
            session,
            worldData,
            sessions,
            new MonsterRuntimeStateStore(),
            new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolveFatalSpinRuntimeSpell(session, worldData, DragonPunchRuntimeSpellId);

        var handled = await InvokeLegacyFatalSpinAsync(
            context,
            session,
            CreateFatalSpinRuntimeQuery(DragonPunchRuntimeSpellId),
            playerSpell,
            spell);

        Assert.True(handled);
        var interaction = Assert.Single(ParseFatalSpinInteractions(sent, DragonPunchRuntimeSpellId));
        Assert.Equal(840, interaction.Query.X);
        Assert.Equal(584, interaction.Query.Y);
        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(DragonPunchRuntimeSpellId, animation.SpellId);
        Assert.Empty(animation.Targets);
        var interactionIndex = SingleFrameIndex(
            sent,
            frame => TryParseFatalSpinInteraction(frame, DragonPunchRuntimeSpellId, out _));
        var animationIndex = SingleFrameIndex(
            sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == DragonPunchRuntimeSpellId);
        Assert.True(interactionIndex < animationIndex);
    }

    [Fact]
    public async Task FatalSpin12240ShadowFistReusesOriginalLineAndCreditsSourceCumulativeProgress()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var primarySpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 832, boundY: 584);
        var followUpOnlySpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 834, boundY: 584);
        var primaryRow = CreateFatalSpinRuntimeRow(
            DragonPunchRuntimeSpellId,
            range: 4,
            experience: 1_000_000);
        var primaryNextLevelRow = CreateFatalSpinRuntimeRow(
            DragonPunchRuntimeSpellId,
            level: 1,
            range: 4,
            experience: 1_000_000);
        var shadowFistRow = CreateShadowFistRuntimeRow(range: 5);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [primarySpawn, followUpOnlySpawn],
            sobNpcs: [],
            magicRows: [primaryRow, primaryNextLevelRow, shadowFistRow],
            items: [CreateItemType(checked((int)ShadowFistRuneItemId), "ShadowFist(+1)")]);
        var primaryMonster = CreateRuntimeMonsterInstance(worldData, primarySpawn, monsterFamily);
        var followUpOnlyMonster = CreateRuntimeMonsterInstance(worldData, followUpOnlySpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var runeItems = CreatePlayerItems(
            new PlayerItemSnapshotItem
            {
                Uid = ShadowFistRuneItemId,
                ItemId = ShadowFistRuneItemId,
                Durability = 100,
                MaximumDurability = 100,
                Position = 101,
            });
        var session = CreateSession(
            strength: 300,
            minMana: 500,
            stamina: 100,
            spellIds: [DragonPunchRuntimeSpellId, ShadowFistRuntimeSpellId],
            playerItems: runeItems);
        sessions.Set(connection, session);
        var context = CreateFatalSpinContext(
            connection,
            log,
            session,
            worldData,
            sessions,
            monsterStates,
            new SobNpcRuntimeStateStore());
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref session);
        context = combatSpellOwner.Context;
        var (playerSpell, spell) = ResolveFatalSpinRuntimeSpell(session, worldData, DragonPunchRuntimeSpellId);
        var (_, shadowFistSpell) = ResolveFatalSpinRuntimeSpell(session, worldData, ShadowFistRuntimeSpellId);
        var expectedPrimaryDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            session,
            worldData,
            primaryMonster,
            monsterFamily,
            spell);
        var expectedFollowUpDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            session,
            worldData,
            primaryMonster,
            monsterFamily,
            shadowFistSpell);
        var primaryProgress = checked((uint)InvokePrivateInt32(
            "AdjustLegacyMonsterExperience",
            checked((int)Math.Min(expectedPrimaryDamage, primaryMonster.MaxLife)),
            checked((int)session.Character.Level),
            primaryMonster.Level));
        var followUpProgressPerMonster = checked((uint)InvokePrivateInt32(
            "AdjustLegacyMonsterExperience",
            checked((int)Math.Min(expectedFollowUpDamage, primaryMonster.MaxLife)),
            checked((int)session.Character.Level),
            primaryMonster.Level));
        var followUpProgress = checked(followUpProgressPerMonster * 2);
        var cumulativeProgress = checked(primaryProgress + followUpProgress);
        var expectedFirstExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(session, primaryProgress));
        var sessionAfterFirstExperience = session with
        {
            Character = session.Character with
            {
                Experience = checked(
                    session.Character.Experience +
                    (long)expectedFirstExperience.AppliedExperienceGain),
            },
        };
        var expectedSecondExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(sessionAfterFirstExperience, cumulativeProgress));
        var expectedFinalExperience = checked(
            (long)expectedFirstExperience.AppliedExperienceGain +
            (long)expectedSecondExperience.AppliedExperienceGain);

        var handled = await InvokeLegacyFatalSpinAsync(
            context,
            session,
            CreateFatalSpinRuntimeQuery(DragonPunchRuntimeSpellId, x: 840, y: 584),
            playerSpell,
            spell);

        Assert.True(handled);
        var animations = ParseSpellAnimations(sent);
        Assert.Equal(2, animations.Count);
        var primaryAnimation = animations[0];
        var followUpAnimation = animations[1];
        Assert.Equal(DragonPunchRuntimeSpellId, primaryAnimation.SpellId);
        Assert.Equal([primaryMonster.Uid], primaryAnimation.Targets.Select(target => target.Uid).ToArray());
        Assert.Equal(expectedPrimaryDamage, Assert.Single(primaryAnimation.Targets).Damage);
        Assert.Equal(ShadowFistRuntimeSpellId, followUpAnimation.SpellId);
        Assert.Equal(832, followUpAnimation.X);
        Assert.Equal(584, followUpAnimation.Y);
        Assert.Equal(
            [primaryMonster.Uid, followUpOnlyMonster.Uid],
            followUpAnimation.Targets.Select(target => target.Uid).ToArray());
        Assert.All(followUpAnimation.Targets, target => Assert.Equal(expectedFollowUpDamage, target.Damage));

        Assert.True(monsterStates.TryGetVisibleInstance(primaryMonster, DateTimeOffset.UtcNow, out var visiblePrimary));
        Assert.True(monsterStates.TryGetVisibleInstance(followUpOnlyMonster, DateTimeOffset.UtcNow, out var visibleFollowUpOnly));
        Assert.Equal(
            primaryMonster.MaxLife - expectedPrimaryDamage - expectedFollowUpDamage,
            visiblePrimary.Hitpoints);
        Assert.Equal(
            followUpOnlyMonster.MaxLife - expectedFollowUpDamage,
            visibleFollowUpOnly.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(expectedFinalExperience, updated.Character.Experience);
        Assert.Equal(
            checked((int)primaryProgress),
            Assert.Single(updated.PlayerSpells.Spells, entry => entry.Id == DragonPunchRuntimeSpellId).Experience);
        Assert.Equal(
            0,
            Assert.Single(updated.PlayerSpells.Spells, entry => entry.Id == ShadowFistRuntimeSpellId).Experience);
        Assert.Equal(
            1,
            sent.Count(frame => LegacyMagicColdTimePacket.TryParse(ReadFrame(frame), out _)));
        Assert.Contains("playerSafetyFence=CanLegacyPlayerMagicDamageTarget", log.ToString());
        Assert.Contains($"followUpProgress={followUpProgress}", log.ToString());
        Assert.Contains($"cumulativeProgress={cumulativeProgress}", log.ToString());
    }

    private static WorldPacketHandlerContext CreateFatalSpinContext(
        WolfConquer.Core.Networking.ConquerTcpConnection connection,
        TextWriter log,
        WorldSession session,
        WorldDataCatalog worldData,
        WorldSessionStore sessions,
        MonsterRuntimeStateStore monsterStates,
        SobNpcRuntimeStateStore sobNpcStates)
    {
        return new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
    }

    private static MagicType CreateFatalSpinRuntimeRow(
        ushort spellId,
        byte sort = FatalSpinRuntimeSort,
        byte level = 0,
        ushort useMana = 7,
        byte useStamina = 5,
        ushort coolDown = 800,
        int coldTime = 300,
        byte range = 5,
        uint experience = 0)
    {
        var template = CreateMagicRow(
            spellId,
            sort,
            level,
            useMana,
            damage: 100,
            experience,
            maxTargets: 10,
            range,
            useStamina: useStamina,
            coolDown: coolDown,
            coldTime: coldTime,
            damageOnHuman: 30_020,
            damageOnMonster: 30_060);
        var fields = template.Fields.ToArray();
        fields[3] = spellId == FatalSpinRuntimeSpellId ? "FatalSpin" : "DragonPunch";
        return new MagicType(spellId, spellId, fields[3], fields);
    }

    private static MagicType CreateShadowFistRuntimeRow(byte range)
    {
        var template = CreateMagicRow(
            ShadowFistRuntimeSpellId,
            sort: 69,
            useMana: 0,
            damage: 100,
            experience: 1_000_000,
            maxTargets: 10,
            range: range,
            useStamina: 0,
            coolDown: 1_000,
            coldTime: 0,
            damageOnHuman: 30_020,
            damageOnMonster: 30_060);
        var fields = template.Fields.ToArray();
        fields[3] = "ShadowFist";
        return new MagicType(ShadowFistRuntimeSpellId, ShadowFistRuntimeSpellId, fields[3], fields);
    }

    private static SobNpc CreateFatalSpinSobNpc(int id, int x, int y)
    {
        return new SobNpc(
            Id: id,
            Name: "FatalSpinPole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: 1_015,
            X: x,
            Y: y,
            Life: 100_000,
            MaxLife: 100_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: id,
            RawLine: $"{id},FatalSpinPole,10,1137,1015,{x},{y},100000,100000,17,0",
            Fields:
            [
                id.ToString(CultureInfo.InvariantCulture),
                "FatalSpinPole",
                "10",
                "1137",
                "1015",
                x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture),
                "100000",
                "100000",
                "17",
                "0",
            ]);
    }

    private static MsgAttackQuery CreateFatalSpinRuntimeQuery(
        ushort spellId,
        ushort x = 840,
        ushort y = 584)
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
        ResolveFatalSpinRuntimeSpell(WorldSession session, WorldDataCatalog worldData, ushort spellId)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            spellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static async ValueTask<bool> InvokeLegacyFatalSpinAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerFatalSpinMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        return await Assert.IsType<ValueTask<bool>>(result);
    }

    private static IReadOnlyList<MsgAttackPacket> ParseFatalSpinInteractions(
        IEnumerable<byte[]> frames,
        ushort spellId)
    {
        var interactions = new List<MsgAttackPacket>();
        foreach (var frame in frames)
        {
            if (TryParseFatalSpinInteraction(frame, spellId, out var interaction))
            {
                interactions.Add(interaction);
            }
        }

        return interactions;
    }

    private static bool TryParseFatalSpinInteraction(
        byte[] frame,
        ushort spellId,
        out MsgAttackPacket interaction)
    {
        return TryParseFatalSpinInteraction(ReadFrame(frame), spellId, out interaction);
    }

    private static bool TryParseFatalSpinInteraction(
        PacketFrame frame,
        ushort spellId,
        out MsgAttackPacket interaction)
    {
        return MsgAttackPacket.TryParse(frame, out interaction) &&
            interaction.Query.AttackType == MsgAttackType.Magic &&
            interaction.Query.SpellId == spellId;
    }
}
