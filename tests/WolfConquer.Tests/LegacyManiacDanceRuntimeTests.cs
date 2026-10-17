using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort ManiacDanceRuntimeSpellId = 12_700;
    private const int ManiacDanceRuntimeFlag = 0xB5;
    private const int ManiacDanceInitialAttackDelayMilliseconds = 700;
    private static readonly TimeSpan ManiacDanceRuntimeDuration = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ManiacDanceRecurringAttackDelay = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task HandlerManiacDanceWithoutXpListPreparesResourcesThenSilentlyStops()
    {
        var result = await RunManiacDanceCastAsync(hasXpList: false);

        Assert.Equal(493, result.Session.Character.MinMana);
        Assert.Equal("91", result.Session.Character.Attributes["Stamina"]);
        Assert.Equal("37", result.Session.Character.Attributes["XPCount"]);
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, ManiacDanceRuntimeFlag));
        Assert.False(result.Session.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyManiacDanceExpiresAtUtcAttribute));
        Assert.False(result.Session.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyManiacDanceNextAttackAtUtcAttribute));

        var updates = ParseMsgUpdates(result.Sent);
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 91)]));
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)]));
        Assert.DoesNotContain(updates, update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.XPList or
                LegacyMsgUpdateDataType.XPCircle or
                LegacyMsgUpdateDataType.StatusFlag));
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.DoesNotContain(result.Sent, bytes =>
            LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Id == ManiacDanceRuntimeSpellId);

        Assert.Contains(
            "WorldServer source-backed returned legacy ManiacDance magic after resource preparation",
            result.Log);
        Assert.Contains("XPList status flag is not active", result.Log);
    }

    [Fact]
    public async Task HandlerManiacDanceWithXpListConsumesXpOpensFlagAndRecordsFirstAutomaticAttack()
    {
        var result = await RunManiacDanceCastAsync(hasXpList: true);

        Assert.Equal(493, result.Session.Character.MinMana);
        Assert.Equal("91", result.Session.Character.Attributes["Stamina"]);
        Assert.Equal("0", result.Session.Character.Attributes["XPCount"]);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, ManiacDanceRuntimeFlag));
        AssertLegacyXpSkillExpiresAround(
            result.Session.Character.Attributes,
            WorldPacketHandlers.RuntimeLegacyManiacDanceExpiresAtUtcAttribute,
            result.BeforeCast,
            result.AfterCast,
            expectedDurationSeconds: checked((int)ManiacDanceRuntimeDuration.TotalSeconds));
        AssertManiacDanceTimestampAround(
            result.Session.Character.Attributes,
            WorldPacketHandlers.RuntimeLegacyManiacDanceNextAttackAtUtcAttribute,
            result.BeforeCast.AddMilliseconds(ManiacDanceInitialAttackDelayMilliseconds),
            result.AfterCast.AddMilliseconds(ManiacDanceInitialAttackDelayMilliseconds));

        var updates = ParseMsgUpdates(result.Sent);
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)]));
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPCircle, 0)]));
        var statusUpdates = updates
            .Where(update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag)
            .ToArray();
        Assert.Equal(3, statusUpdates.Length);
        var xpListRemovedStatus = Assert.Single(statusUpdates[0].Updates);
        Assert.NotNull(xpListRemovedStatus.Values);
        Assert.False(IsLegacyStatusFlagSet(xpListRemovedStatus.Values!, LegacyXpListFlag));
        Assert.True(IsLegacyStatusFlagSet(xpListRemovedStatus.Values!, LegacySupermanFlag));
        Assert.False(IsLegacyStatusFlagSet(xpListRemovedStatus.Values!, ManiacDanceRuntimeFlag));
        var priorSkillRemovedStatus = Assert.Single(statusUpdates[1].Updates);
        Assert.NotNull(priorSkillRemovedStatus.Values);
        Assert.False(IsLegacyStatusFlagSet(priorSkillRemovedStatus.Values!, LegacyXpListFlag));
        Assert.False(IsLegacyStatusFlagSet(priorSkillRemovedStatus.Values!, LegacySupermanFlag));
        Assert.False(IsLegacyStatusFlagSet(priorSkillRemovedStatus.Values!, ManiacDanceRuntimeFlag));
        var maniacDanceOpenedStatus = Assert.Single(statusUpdates[2].Updates);
        Assert.NotNull(maniacDanceOpenedStatus.Values);
        Assert.False(IsLegacyStatusFlagSet(maniacDanceOpenedStatus.Values!, LegacyXpListFlag));
        Assert.False(IsLegacyStatusFlagSet(maniacDanceOpenedStatus.Values!, LegacySupermanFlag));
        Assert.True(IsLegacyStatusFlagSet(maniacDanceOpenedStatus.Values!, ManiacDanceRuntimeFlag));

        var animationFrame = Assert.Single(result.Sent, bytes =>
            ReadFrame(bytes).Header.Opcode == LegacySpellAnimationPacket.Opcode);
        var animationPacketFrame = ReadFrame(animationFrame);
        Assert.Equal(2_239, animationPacketFrame.Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(animationPacketFrame, out var animation));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(833, animation.X);
        Assert.Equal(585, animation.Y);
        Assert.Equal(ManiacDanceRuntimeSpellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(2, animation.LevelHu);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var staminaIndex = SingleFrameIndex(result.Sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 91)]));
        var manaIndex = SingleFrameIndex(result.Sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)]));
        var xpListIndex = SingleFrameIndex(result.Sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)]));
        var xpCircleIndex = SingleFrameIndex(result.Sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPCircle, 0)]));
        var statusIndexes = Enumerable.Range(0, result.Sent.Count)
            .Where(index =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(result.Sent[index]), out var packet) &&
                packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag))
            .ToArray();
        Assert.Equal(3, statusIndexes.Length);
        var animationIndex = SingleFrameIndex(result.Sent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
            packet.SpellId == ManiacDanceRuntimeSpellId);
        Assert.True(staminaIndex < manaIndex);
        Assert.True(manaIndex < xpListIndex);
        Assert.True(xpListIndex < statusIndexes[0]);
        Assert.True(statusIndexes[0] < xpCircleIndex);
        Assert.True(xpCircleIndex < statusIndexes[1]);
        Assert.True(statusIndexes[1] < statusIndexes[2]);
        Assert.True(statusIndexes[2] < animationIndex);
        Assert.DoesNotContain(result.Sent, bytes =>
            LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Id == ManiacDanceRuntimeSpellId);

        Assert.Contains("WorldServer handled legacy ManiacDance XPList transition", result.Log);
        Assert.Contains("durationSeconds=15", result.Log);
        Assert.Contains("initialAttackDelayMs=700", result.Log);
    }

    [Fact]
    public async Task HandlerManiacDanceDuringAutoHuntRemovesXpListButOpenXpSkillIsSourceNoOp()
    {
        var result = await RunManiacDanceCastAsync(hasXpList: true, onAutoHunt: true);

        Assert.Equal("37", result.Session.Character.Attributes["XPCount"]);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, ManiacDanceRuntimeFlag));
        Assert.False(result.Session.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyManiacDanceExpiresAtUtcAttribute));
        Assert.True(result.Session.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyManiacDanceNextAttackAtUtcAttribute));

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
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, ManiacDanceRuntimeFlag));
        Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Contains("openedManiacDance=False", result.Log);
    }

    [Fact]
    public void ManiacDanceInitialAttackDelayUsesSourceEquipmentAgilityCycloneAndDeadlySightSequence()
    {
        const int rightWeaponId = 410_001;
        const int leftWeaponId = 420_001;
        const int ringId = 150_001;
        const int deadlySightRuneId = 800_003;
        var worldData = CreateMagicCatalogWithItems(
            CreateHighLifeMonsterFamily(),
            [],
            [
                CreateManiacDanceFrequencyItemType(rightWeaponId, "RightWeapon", frequency: 20),
                CreateManiacDanceFrequencyItemType(leftWeaponId, "LeftWeapon", frequency: 30),
                CreateManiacDanceFrequencyItemType(ringId, "Ring", frequency: 40),
                CreateManiacDanceFrequencyItemType(deadlySightRuneId, "DeadlySight", frequency: 0),
            ],
            CreateManiacDanceRuntimeRow());
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem { ItemId = rightWeaponId, Position = 4 },
                new PlayerItemSnapshotItem { ItemId = leftWeaponId, Position = 5 },
                new PlayerItemSnapshotItem { ItemId = ringId, Position = 6 },
            ],
            RuneItems:
            [
                new PlayerItemSnapshotItem { ItemId = deadlySightRuneId, Position = 101 },
            ]);
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyCycloneFlag),
            },
            playerItems: playerItems);
        session = session with { Character = session.Character with { Agility = 100 } };
        var method = typeof(WorldPacketHandlers).GetMethod(
            "CalculateLegacyManiacDanceInitialAttackDelayMilliseconds",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var delay = Assert.IsType<int>(method.Invoke(null, [session, worldData]));

        Assert.Equal(355, delay);
    }

    [Fact]
    public async Task ManiacDanceExpiryIsStrictAndRemovesFlagMetadataAndPublishesStatus()
    {
        var expiresAt = DateTimeOffset.Parse(
            "2026-07-19T18:00:00Z",
            CultureInfo.InvariantCulture);
        var ownerSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        using var log = new StringWriter();
        var ownerConnection = CreateSendingConnection(ownerSent);
        var peerConnection = CreateSendingConnection(peerSent);
        var owner = CreateSession(
            attributes: CreateActiveManiacDanceAttributes(
                expiresAt,
                nextAttackAt: expiresAt.AddSeconds(-1)));
        var peer = CreateSession(
            uid: TargetPlayerUid,
            name: "ManiacDanceExpiryPeer",
            x: 831,
            y: 584);
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log: log);
        GetHandlerSessions(handler).Set(ownerConnection, owner);
        GetHandlerSessions(handler).Set(peerConnection, peer);

        await InvokeManiacDanceXpSkillExpirationsAsync(handler, expiresAt.AddTicks(-1));

        var beforeExpiry = AssertHandlerSession(handler, ownerConnection);
        Assert.True(HasLegacyStatusFlag(beforeExpiry.Character.Attributes, ManiacDanceRuntimeFlag));
        Assert.True(beforeExpiry.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyManiacDanceExpiresAtUtcAttribute));
        Assert.True(beforeExpiry.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyManiacDanceNextAttackAtUtcAttribute));
        Assert.Empty(ownerSent);
        Assert.Empty(peerSent);

        await InvokeManiacDanceXpSkillExpirationsAsync(handler, expiresAt);

        var expired = AssertHandlerSession(handler, ownerConnection);
        Assert.False(HasLegacyStatusFlag(expired.Character.Attributes, ManiacDanceRuntimeFlag));
        Assert.False(expired.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyManiacDanceExpiresAtUtcAttribute));
        Assert.False(expired.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyManiacDanceNextAttackAtUtcAttribute));
        Assert.Equal("preserved", expired.Character.Attributes["Unrelated"]);
        AssertManiacDanceStatusPublication(ownerSent);
        AssertManiacDanceStatusPublication(peerSent);
        Assert.Contains(
            "WorldServer expired legacy XP skill flags playerCount=1 viewerCount=2",
            log.ToString());
    }

    [Fact]
    public async Task DueManiacDanceAutomaticAttackHitsVisibleTargetsWithinInclusiveFiveAndAwardsCharacterXpOnly()
    {
        var now = DateTimeOffset.Parse("2026-07-19T18:30:00Z", CultureInfo.InvariantCulture);
        var monsterFamily = CreateHighLifeMonsterFamily();
        var boundarySpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 31_270,
            boundX: 835,
            boundY: 584);
        var outsideSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 31_271,
            boundX: 836,
            boundY: 584);
        var innerSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 31_272,
            boundX: 831,
            boundY: 584);
        var boundarySobNpc = CreateMoveLineSobNpc(id: 65_270, x: 830, y: 589);
        var outsideSobNpc = CreateMoveLineSobNpc(id: 65_271, x: 830, y: 590);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [boundarySpawn, outsideSpawn, innerSpawn],
            [boundarySobNpc, outsideSobNpc],
            [CreateManiacDanceRuntimeRow(experience: 1_000_000)]);
        var boundaryMonster = CreateRuntimeMonsterInstance(worldData, boundarySpawn, monsterFamily);
        var outsideMonster = CreateRuntimeMonsterInstance(worldData, outsideSpawn, monsterFamily);
        var innerMonster = CreateRuntimeMonsterInstance(worldData, innerSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var boundaryPlayerSent = new List<byte[]>();
        var outsidePlayerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var boundaryPlayerConnection = CreateSendingConnection(boundaryPlayerSent);
        var outsidePlayerConnection = CreateSendingConnection(outsidePlayerSent);
        var caster = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    ManiacDanceRuntimeSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0),
            ],
            attributes: CreateActiveManiacDanceAttributes(
                now.AddSeconds(10),
                nextAttackAt: now.AddTicks(-1)));
        var boundaryPlayer = CreateSession(
            uid: TargetPlayerUid,
            name: "ManiacDanceBoundaryPlayer",
            x: 835,
            y: 584,
            minHitPoints: 100_000);
        var outsidePlayer = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "ManiacDanceOutsidePlayer",
            x: 836,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, caster);
        sessions.Set(boundaryPlayerConnection, boundaryPlayer);
        sessions.Set(outsidePlayerConnection, outsidePlayer);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);

        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            caster.PlayerSpells,
            worldData,
            ManiacDanceRuntimeSpellId,
            out _,
            out var damageSpell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            caster,
            worldData,
            boundaryMonster,
            monsterFamily,
            damageSpell);
        var expectedPlayerDamageBeforeNoBreakthrough = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            caster,
            boundaryPlayer,
            damageSpell,
            worldData,
            1U);
        var expectedPlayerDamage =
            expectedPlayerDamageBeforeNoBreakthrough +
            (expectedPlayerDamageBeforeNoBreakthrough * 50 / 100);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            caster,
            damageSpell,
            worldData,
            1U);

        Assert.True(await WorldPacketHandlers.TrySendDueLegacyManiacDanceAttackAsync(
            context,
            now,
            CancellationToken.None));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(830, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(ManiacDanceRuntimeSpellId, animation.SpellId);
        Assert.Equal(
            new[]
            {
                boundaryMonster.Uid,
                innerMonster.Uid,
                TargetPlayerUid,
                checked((uint)boundarySobNpc.Id),
            }.Order().ToArray(),
            animation.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.Contains(animation.Targets, target =>
            target.Uid == boundaryMonster.Uid &&
            target.Damage == expectedMonsterDamage &&
            target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == innerMonster.Uid &&
            target.Damage == expectedMonsterDamage &&
            target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == TargetPlayerUid &&
            target.Damage == expectedPlayerDamage &&
            target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == checked((uint)boundarySobNpc.Id) &&
            target.Damage == expectedSobNpcDamage &&
            target.Hit);
        Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideMonster.Uid);
        Assert.DoesNotContain(animation.Targets, target => target.Uid == TargetPlayerUid + 1);
        Assert.DoesNotContain(animation.Targets, target => target.Uid == checked((uint)outsideSobNpc.Id));

        Assert.True(monsterStates.TryGetVisibleInstance(
            boundaryMonster,
            now,
            out var damagedBoundaryMonster));
        Assert.Equal(boundaryMonster.MaxLife - expectedMonsterDamage, damagedBoundaryMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideMonster, now, out var untouchedOutsideMonster));
        Assert.Equal(outsideMonster.MaxLife, untouchedOutsideMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(innerMonster, now, out var damagedInnerMonster));
        Assert.Equal(innerMonster.MaxLife - expectedMonsterDamage, damagedInnerMonster.Hitpoints);
        Assert.True(sessions.TryGet(boundaryPlayerConnection, out var damagedBoundaryPlayer));
        Assert.NotNull(damagedBoundaryPlayer);
        Assert.Equal(100_000 - expectedPlayerDamage, damagedBoundaryPlayer.Character.MinHitPoints);
        Assert.True(sessions.TryGet(outsidePlayerConnection, out var untouchedOutsidePlayer));
        Assert.NotNull(untouchedOutsidePlayer);
        Assert.Equal(100_000, untouchedOutsidePlayer.Character.MinHitPoints);
        Assert.Equal(
            boundarySobNpc.Life - checked((int)expectedSobNpcDamage),
            sobNpcStates.GetVisible(boundarySobNpc).Life);
        Assert.Equal(outsideSobNpc.Life, sobNpcStates.GetVisible(outsideSobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.True(updatedCaster.Character.Experience > caster.Character.Experience);
        Assert.Equal(
            0,
            Assert.Single(updatedCaster.PlayerSpells.Spells, spell =>
                spell.Id == ManiacDanceRuntimeSpellId).Experience);
        AssertManiacDanceTimestampEquals(
            updatedCaster.Character.Attributes,
            WorldPacketHandlers.RuntimeLegacyManiacDanceNextAttackAtUtcAttribute,
            now + ManiacDanceRecurringAttackDelay);
        Assert.Single(ParseMsgUpdates(casterSent), update => update.Updates.Any(entry =>
            entry.Type == LegacyMsgUpdateDataType.Experience));
        Assert.Equal(LegacySpellAnimationPacket.Opcode, ReadFrame(casterSent[^1]).Header.Opcode);
        Assert.DoesNotContain(casterSent, bytes =>
            LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Id == ManiacDanceRuntimeSpellId);

        var output = log.ToString();
        Assert.Contains("WorldServer handled due legacy ManiacDance automatic damage", output);
        Assert.Contains("range=5 targets=4", output);
        Assert.Contains("monsterTargets=2 playerTargets=1 sobNpcTargets=1", output);
        Assert.Contains("combined return experience", output);
        Assert.Contains("spellExperienceApplied=False", output);
    }

    [Fact]
    public async Task DueManiacDanceAutomaticAttackWithNoTargetsStillAnimatesAndSchedulesNextSecond()
    {
        var now = DateTimeOffset.Parse("2026-07-19T19:00:00Z", CultureInfo.InvariantCulture);
        var worldData = CreateMagicCatalog(
            CreateHighLifeMonsterFamily(),
            [],
            CreateManiacDanceRuntimeRow(experience: 1_000_000));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [ManiacDanceRuntimeSpellId],
            attributes: CreateActiveManiacDanceAttributes(
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

        Assert.True(await WorldPacketHandlers.TrySendDueLegacyManiacDanceAttackAsync(
            context,
            now,
            CancellationToken.None));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(830, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(ManiacDanceRuntimeSpellId, animation.SpellId);
        Assert.Empty(animation.Targets);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(session.Character.Experience, updatedSession.Character.Experience);
        Assert.Equal(
            0,
            Assert.Single(updatedSession.PlayerSpells.Spells, spell =>
                spell.Id == ManiacDanceRuntimeSpellId).Experience);
        AssertManiacDanceTimestampEquals(
            updatedSession.Character.Attributes,
            WorldPacketHandlers.RuntimeLegacyManiacDanceNextAttackAtUtcAttribute,
            now + ManiacDanceRecurringAttackDelay);
        Assert.DoesNotContain(sent, bytes =>
            LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Id == ManiacDanceRuntimeSpellId);
        Assert.Contains("range=5 targets=0", log.ToString());
    }

    private static async Task<ManiacDanceCastResult> RunManiacDanceCastAsync(
        bool hasXpList,
        bool onAutoHunt = false)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateManiacDanceRuntimeRow(useMana: 7, useStamina: 9));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var flags = hasXpList
            ? BuildStatusFlags(LegacyXpListFlag, LegacySupermanFlag)
            : BuildStatusFlags(LegacySupermanFlag);
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    ManiacDanceRuntimeSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 2),
            ],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = flags,
                ["XPCount"] = "37",
                ["PkMode"] = "0",
                ["OnAutoHunt"] = onAutoHunt ? "1" : "0",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        var beforeCast = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: ManiacDanceRuntimeSpellId,
                x: 833,
                y: 585)).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, afterCast, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        return new ManiacDanceCastResult(
            updatedSession,
            sent,
            log.ToString(),
            beforeCast,
            afterCast);
    }

    private static MagicType CreateManiacDanceRuntimeRow(
        ushort useMana = 0,
        byte useStamina = 0,
        uint experience = 0)
    {
        return CreateMagicRow(
            ManiacDanceRuntimeSpellId,
            sort: (byte)LegacyMagicSort.ManiacDance,
            level: 0,
            useMana: useMana,
            damage: 30_100,
            experience: experience,
            maxTargets: 1,
            range: 5,
            useStamina: useStamina,
            coolDown: 0,
            coldTime: 0,
            duration: 1);
    }

    private static ItemType CreateManiacDanceFrequencyItemType(int id, string name, uint frequency)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = id.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.FrequencyFieldIndex] = frequency.ToString(CultureInfo.InvariantCulture);
        return new ItemType(id, name, fields);
    }

    private static Dictionary<string, string> CreateActiveManiacDanceAttributes(
        DateTimeOffset expiresAt,
        DateTimeOffset nextAttackAt)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(ManiacDanceRuntimeFlag),
            ["XPCount"] = "0",
            ["PkMode"] = "0",
            [WorldPacketHandlers.RuntimeLegacyManiacDanceExpiresAtUtcAttribute] =
                expiresAt.ToString("O", CultureInfo.InvariantCulture),
            [WorldPacketHandlers.RuntimeLegacyManiacDanceNextAttackAtUtcAttribute] =
                nextAttackAt.ToString("O", CultureInfo.InvariantCulture),
            ["Unrelated"] = "preserved",
        };
    }

    private static async ValueTask InvokeManiacDanceXpSkillExpirationsAsync(
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

    private static void AssertManiacDanceStatusPublication(IReadOnlyList<byte[]> frames)
    {
        var update = Assert.Single(ParseMsgUpdates(frames));
        Assert.Equal(PlayerUid, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, entry.Type);
        Assert.NotNull(entry.Values);
        Assert.False(IsLegacyStatusFlagSet(entry.Values!, ManiacDanceRuntimeFlag));
    }

    private static void AssertManiacDanceTimestampAround(
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
        Assert.InRange(value, minimum, maximum);
    }

    private static void AssertManiacDanceTimestampEquals(
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

    private sealed record ManiacDanceCastResult(
        WorldSession Session,
        IReadOnlyList<byte[]> Sent,
        string Log,
        DateTimeOffset BeforeCast,
        DateTimeOffset AfterCast);
}
