using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const byte RemoveStaminSort = 88;
    private const ushort FrostGazeISpellId = 12_830;
    private const ushort FrostGazeIISpellId = 13_070;
    private const ushort FrostGazeIIISpellId = 13_080;
    private const ushort FrostArrowsSpellId = 14_280;

    [Fact]
    public async Task HandlerRemoveStaminRejectsAdjacentSortWithoutSpendingResources()
    {
        var row = CreateMagicRow(
            FrostGazeISpellId,
            sort: RemoveStaminSort - 1,
            useMana: 7,
            damage: 15,
            range: 3,
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
            spellIds: [FrostGazeISpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var (playerSpell, spell) = ResolveRemoveStaminSpell(
            session,
            worldData,
            FrostGazeISpellId);

        var handled = await InvokeLegacyRemoveStaminAsync(
            context,
            session,
            CreateRemoveStaminQuery(FrostGazeISpellId),
            playerSpell,
            spell);

        Assert.False(handled);
        Assert.Empty(sent);
        var unchanged = GetRemoveStaminSession(sessions, connection);
        Assert.Equal(500, unchanged.Character.MinMana);
        Assert.Equal("100", unchanged.Character.Attributes["Stamina"]);
        Assert.Null(unchanged.MagicCooldowns);
    }

    [Fact]
    public async Task HandlerRemoveStaminUnsupportedAdjacentIdPreparesResourcesBeforeIdSwitch()
    {
        const ushort unsupportedSpellId = FrostArrowsSpellId - 1;
        var row = CreateMagicRow(
            unsupportedSpellId,
            sort: RemoveStaminSort,
            useMana: 7,
            damage: 20,
            range: 3,
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
            spellIds: [unsupportedSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var (playerSpell, spell) = ResolveRemoveStaminSpell(
            session,
            worldData,
            unsupportedSpellId);

        var handled = await InvokeLegacyRemoveStaminAsync(
            context,
            session,
            CreateRemoveStaminQuery(unsupportedSpellId),
            playerSpell,
            spell);

        Assert.True(handled);
        var staminaIndex = FindRemoveStaminFrameIndex(sent, frame =>
            IsRemoveStaminScalarUpdate(
                frame,
                PlayerUid,
                LegacyMsgUpdateDataType.Stamina,
                95));
        var manaIndex = FindRemoveStaminFrameIndex(sent, frame =>
            IsRemoveStaminScalarUpdate(
                frame,
                PlayerUid,
                LegacyMsgUpdateDataType.Mana,
                493));
        var cooldownIndex = FindRemoveStaminFrameIndex(sent, frame =>
            LegacyMagicColdTimePacket.TryParse(frame, out var packet) &&
            packet.Spells.SequenceEqual([new LegacyMagicColdTimeSpell(unsupportedSpellId, 300)]));
        Assert.True(staminaIndex >= 0);
        Assert.True(manaIndex > staminaIndex);
        Assert.True(cooldownIndex > manaIndex);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseLegacySpellPackets(sent));

        var updated = GetRemoveStaminSession(sessions, connection);
        Assert.Equal(493, updated.Character.MinMana);
        Assert.Equal("95", updated.Character.Attributes["Stamina"]);
        Assert.True(updated.MagicCooldowns?.ContainsKey(unsupportedSpellId));
        Assert.Contains(
            "source RemoveStamin.cs returns after CanUseSpell.Verified for unsupported IDs",
            log.ToString());
    }

    [Theory]
    [InlineData(FrostGazeISpellId, 15U)]
    [InlineData(FrostGazeIISpellId, 25U)]
    [InlineData(FrostGazeIIISpellId, 40U)]
    public async Task HandlerFrostGazeSupportedIdsUseCasterRangeAndClampStamina(
        ushort spellId,
        uint staminaDamage)
    {
        var row = CreateMagicRow(
            spellId,
            sort: RemoveStaminSort,
            useMana: 0,
            damage: staminaDamage,
            range: 3,
            useStamina: 0,
            coldTime: 0);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            stamina: 100,
            spellIds: [spellId],
            attributes: RemoveStaminAttributes(battlePower: 100, dynamicId: 1, forcedRoll: 0));
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "FrostGazeTarget",
            x: 833,
            y: 587,
            stamina: 10,
            attributes: RemoveStaminAttributes(battlePower: 100, dynamicId: 1));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var (playerSpell, spell) = ResolveRemoveStaminSpell(casterSession, worldData, spellId);
        var query = CreateRemoveStaminQuery(spellId, x: 845, y: 599);

        var handled = await InvokeLegacyRemoveStaminAsync(
            context,
            casterSession,
            query,
            playerSpell,
            spell);

        Assert.True(handled);
        var updatedTarget = GetRemoveStaminSession(sessions, targetConnection);
        Assert.Equal("0", updatedTarget.Character.Attributes["Stamina"]);
        Assert.Contains(ParseMsgUpdates(targetSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 0)]));
        Assert.DoesNotContain(ParseMsgUpdates(casterSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(845, animation.X);
        Assert.Equal(599, animation.Y);
        Assert.Equal(spellId, animation.SpellId);
        var animationTarget = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, animationTarget.Uid);
        Assert.Equal(staminaDamage, animationTarget.Damage);
        Assert.True(animationTarget.Hit);
        Assert.Equal(MsgAttackEffect.None, animationTarget.Effect);
        Assert.Empty(ParseLegacySpellPackets(casterSent));
        Assert.Contains("progressDamage=0", log.ToString());
    }

    [Fact]
    public async Task HandlerFrostGazeUsesBattlePowerChanceForHitAndMissAnimationTargets()
    {
        const uint staminaDamage = 20;
        var row = CreateMagicRow(
            FrostGazeISpellId,
            sort: RemoveStaminSort,
            useMana: 0,
            damage: staminaDamage,
            range: 5,
            useStamina: 0,
            coldTime: 0);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var hitSent = new List<byte[]>();
        var missSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var hitConnection = CreateSendingConnection(hitSent);
        var missConnection = CreateSendingConnection(missSent);
        var casterSession = CreateSession(
            stamina: 100,
            spellIds: [FrostGazeISpellId],
            attributes: RemoveStaminAttributes(battlePower: 100, dynamicId: 1, forcedRoll: 94));
        var hitSession = CreateSession(
            uid: TargetPlayerUid,
            name: "FrostGazeHit",
            x: 831,
            y: 584,
            stamina: 100,
            attributes: RemoveStaminAttributes(battlePower: 101, dynamicId: 1));
        var missSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "FrostGazeMiss",
            x: 832,
            y: 584,
            stamina: 100,
            attributes: RemoveStaminAttributes(battlePower: 102, dynamicId: 1));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(hitConnection, hitSession);
        sessions.Set(missConnection, missSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var (playerSpell, spell) = ResolveRemoveStaminSpell(
            casterSession,
            worldData,
            FrostGazeISpellId);

        await InvokeLegacyRemoveStaminAsync(
            context,
            casterSession,
            CreateRemoveStaminQuery(FrostGazeISpellId),
            playerSpell,
            spell);

        Assert.Equal(
            "80",
            GetRemoveStaminSession(sessions, hitConnection).Character.Attributes["Stamina"]);
        Assert.Equal(
            "100",
            GetRemoveStaminSession(sessions, missConnection).Character.Attributes["Stamina"]);
        Assert.Contains(ParseMsgUpdates(hitSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 80)]));
        Assert.DoesNotContain(ParseMsgUpdates(missSent), update =>
            update.Uid == TargetPlayerUid + 1 &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        var hit = Assert.Single(animation.Targets, target => target.Uid == TargetPlayerUid);
        Assert.Equal(staminaDamage, hit.Damage);
        Assert.True(hit.Hit);
        var miss = Assert.Single(animation.Targets, target => target.Uid == TargetPlayerUid + 1);
        Assert.Equal(0U, miss.Damage);
        Assert.False(miss.Hit);
        Assert.Contains("hitCount=1 missCount=1", log.ToString());
    }

    [Fact]
    public async Task HandlerFrostGazeConsolidationSuccessBreaksRemainingPlayerLoop()
    {
        const uint consolidationRuneItemId = 4_036_204;
        const uint staminaDamage = 20;
        var row = CreateMagicRow(
            FrostGazeISpellId,
            sort: RemoveStaminSort,
            useMana: 0,
            damage: staminaDamage,
            range: 5,
            useStamina: 0,
            coldTime: 0);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var casterSession = CreateSession(
            stamina: 100,
            spellIds: [FrostGazeISpellId],
            attributes: RemoveStaminAttributes(battlePower: 100, dynamicId: 1, forcedRoll: 0));
        sessions.Set(casterConnection, casterSession);

        var targetConnections = new Dictionary<uint, ConquerTcpConnection>();
        var targetSent = new Dictionary<uint, List<byte[]>>();
        var targetRunes = new Dictionary<uint, List<PlayerItemSnapshotItem>>();
        foreach (var (uid, name, x) in new (uint Uid, string Name, long X)[]
        {
            (TargetPlayerUid, "BeforeConsolidation", 831),
            (TargetPlayerUid + 1, "Consolidation", 832),
            (TargetPlayerUid + 2, "AfterConsolidation", 833),
        })
        {
            var sent = new List<byte[]>();
            var connection = CreateSendingConnection(sent);
            var runeItems = new List<PlayerItemSnapshotItem>();
            targetConnections[uid] = connection;
            targetSent[uid] = sent;
            targetRunes[uid] = runeItems;
            sessions.Set(
                connection,
                CreateSession(
                    uid: uid,
                    name: name,
                    x: x,
                    y: 584,
                    stamina: 100,
                    attributes: new Dictionary<string, string>(
                        RemoveStaminAttributes(battlePower: 100, dynamicId: 1))
                    {
                        ["LegacyConsolidationRoll"] = "0",
                    },
                    playerItems: CreatePlayerItems(runeItems)));
        }

        var targetOrder = GetVisibleTargetOrder(sessions, casterConnection, PlayerUid);
        Assert.Equal(3, targetOrder.Count);
        var beforeUid = targetOrder[0];
        var protectedUid = targetOrder[1];
        var afterUid = targetOrder[2];
        targetRunes[protectedUid].Add(new PlayerItemSnapshotItem
        {
            Uid = consolidationRuneItemId,
            ItemId = consolidationRuneItemId,
            Position = 101,
        });

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var (playerSpell, spell) = ResolveRemoveStaminSpell(
            casterSession,
            worldData,
            FrostGazeISpellId);

        await InvokeLegacyRemoveStaminAsync(
            context,
            casterSession,
            CreateRemoveStaminQuery(FrostGazeISpellId),
            playerSpell,
            spell);

        Assert.Equal(
            "80",
            GetRemoveStaminSession(sessions, targetConnections[beforeUid]).Character.Attributes["Stamina"]);
        Assert.Equal(
            "100",
            GetRemoveStaminSession(sessions, targetConnections[protectedUid]).Character.Attributes["Stamina"]);
        Assert.Equal(
            "100",
            GetRemoveStaminSession(sessions, targetConnections[afterUid]).Character.Attributes["Stamina"]);
        Assert.Contains(ParseMsgUpdates(targetSent[beforeUid]), update =>
            update.Uid == beforeUid &&
            update.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 80)]));
        Assert.DoesNotContain(ParseMsgUpdates(targetSent[protectedUid]), update =>
            update.Uid == protectedUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));
        Assert.DoesNotContain(ParseMsgUpdates(targetSent[afterUid]), update =>
            update.Uid == afterUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        var animationTarget = Assert.Single(animation.Targets);
        Assert.Equal(beforeUid, animationTarget.Uid);
        Assert.Equal(staminaDamage, animationTarget.Damage);
        Assert.True(animationTarget.Hit);
        Assert.Contains("consolidationBreak=True", log.ToString());
    }

    [Fact]
    public async Task HandlerFrostArrowsAppliesTimedRemoveOnDeadStatusAtTargetPoint()
    {
        const uint duration = 8;
        var row = CreateMagicRow(
            FrostArrowsSpellId,
            sort: RemoveStaminSort,
            useMana: 7,
            damage: 0,
            range: 2,
            useStamina: 5,
            coldTime: 300,
            duration: duration);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var hitSent = new List<byte[]>();
        var outsideSent = new List<byte[]>();
        var deadSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var hitConnection = CreateSendingConnection(hitSent);
        var outsideConnection = CreateSendingConnection(outsideSent);
        var deadConnection = CreateSendingConnection(deadSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            minMana: 500,
            stamina: 100,
            spellIds: [FrostArrowsSpellId],
            attributes: RemoveStaminAttributes(battlePower: 100, dynamicId: 1));
        var hitSession = CreateSession(
            uid: TargetPlayerUid,
            name: "FrostArrowsHit",
            x: 838,
            y: 586,
            attributes: RemoveStaminAttributes(battlePower: 100, dynamicId: 1));
        var outsideSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "FrostArrowsOutside",
            x: 839,
            y: 584,
            attributes: RemoveStaminAttributes(battlePower: 100, dynamicId: 1));
        var deadSession = CreateSession(
            uid: TargetPlayerUid + 2,
            name: "FrostArrowsDead",
            x: 836,
            y: 585,
            minHitPoints: 0,
            attributes: RemoveStaminAttributes(battlePower: 100, dynamicId: 1));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(hitConnection, hitSession);
        sessions.Set(outsideConnection, outsideSession);
        sessions.Set(deadConnection, deadSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var (playerSpell, spell) = ResolveRemoveStaminSpell(
            casterSession,
            worldData,
            FrostArrowsSpellId);
        var beforeCast = DateTimeOffset.UtcNow;

        await InvokeLegacyRemoveStaminAsync(
            context,
            casterSession,
            CreateRemoveStaminQuery(FrostArrowsSpellId, x: 836, y: 584),
            playerSpell,
            spell);
        var afterCast = DateTimeOffset.UtcNow;

        var updatedCaster = GetRemoveStaminSession(sessions, casterConnection);
        var updatedHit = GetRemoveStaminSession(sessions, hitConnection);
        var updatedOutside = GetRemoveStaminSession(sessions, outsideConnection);
        var updatedDead = GetRemoveStaminSession(sessions, deadConnection);
        Assert.Equal(493, updatedCaster.Character.MinMana);
        Assert.Equal("95", updatedCaster.Character.Attributes["Stamina"]);
        Assert.True(updatedCaster.MagicCooldowns?.ContainsKey(FrostArrowsSpellId));
        Assert.True(HasLegacyStatusFlag(
            updatedHit.Character.Attributes,
            WorldPacketHandlers.LegacyFrostArrowsStatusFlag));
        Assert.False(HasLegacyStatusFlag(
            updatedOutside.Character.Attributes,
            WorldPacketHandlers.LegacyFrostArrowsStatusFlag));
        Assert.False(HasLegacyStatusFlag(
            updatedDead.Character.Attributes,
            WorldPacketHandlers.LegacyFrostArrowsStatusFlag));
        Assert.Equal(
            "true",
            updatedHit.Character.Attributes[
                WorldPacketHandlers.RuntimeLegacyFrostArrowsRemoveOnDeadAttribute]);
        Assert.True(DateTimeOffset.TryParse(
            updatedHit.Character.Attributes[
                WorldPacketHandlers.RuntimeLegacyFrostArrowsExpiresAtUtcAttribute],
            out var expiresAtUtc));
        Assert.True(expiresAtUtc >= beforeCast.AddSeconds(duration));
        Assert.True(expiresAtUtc <= afterCast.AddSeconds(duration));

        var hitStatusUpdate = Assert.Single(ParseMsgUpdates(casterSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusFlags = Assert.Single(
            hitStatusUpdate.Updates,
            entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag);
        Assert.NotNull(statusFlags.Values);
        Assert.True(IsLegacyStatusFlagSet(
            statusFlags.Values!,
            WorldPacketHandlers.LegacyFrostArrowsStatusFlag));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(836, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(FrostArrowsSpellId, animation.SpellId);
        var animationTarget = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, animationTarget.Uid);
        Assert.Equal(0U, animationTarget.Damage);
        Assert.True(animationTarget.Hit);
        Assert.Empty(ParseLegacySpellPackets(casterSent));

        var staminaIndex = FindRemoveStaminFrameIndex(casterSent, frame =>
            IsRemoveStaminScalarUpdate(
                frame,
                PlayerUid,
                LegacyMsgUpdateDataType.Stamina,
                95));
        var manaIndex = FindRemoveStaminFrameIndex(casterSent, frame =>
            IsRemoveStaminScalarUpdate(
                frame,
                PlayerUid,
                LegacyMsgUpdateDataType.Mana,
                493));
        var cooldownIndex = FindRemoveStaminFrameIndex(casterSent, frame =>
            LegacyMagicColdTimePacket.TryParse(frame, out var packet) &&
            packet.Spells.SequenceEqual([new LegacyMagicColdTimeSpell(FrostArrowsSpellId, 300)]));
        var statusIndex = FindRemoveStaminFrameIndex(casterSent, frame =>
            IsRemoveStaminStatusUpdate(
                frame,
                TargetPlayerUid,
                WorldPacketHandlers.LegacyFrostArrowsStatusFlag));
        var animationIndex = FindRemoveStaminFrameIndex(casterSent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
            packet.SpellId == FrostArrowsSpellId);
        Assert.True(staminaIndex >= 0);
        Assert.True(manaIndex > staminaIndex);
        Assert.True(cooldownIndex > manaIndex);
        Assert.True(statusIndex > cooldownIndex);
        Assert.True(animationIndex > statusIndex);
        Assert.Contains("targetCount=1 statusAppliedCount=1", log.ToString());
        Assert.Contains("removeOnDead=True progressDamage=0", log.ToString());
    }

    [Fact]
    public async Task HandlerFrostArrowsBeyondEightTileCastPointSendsEmptyAnimation()
    {
        var row = CreateMagicRow(
            FrostArrowsSpellId,
            sort: RemoveStaminSort,
            useMana: 0,
            damage: 0,
            range: 2,
            useStamina: 0,
            coldTime: 0,
            duration: 8);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            spellIds: [FrostArrowsSpellId],
            attributes: RemoveStaminAttributes(battlePower: 100, dynamicId: 1));
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "FrostArrowsPointTarget",
            x: 839,
            y: 584,
            attributes: RemoveStaminAttributes(battlePower: 100, dynamicId: 1));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var (playerSpell, spell) = ResolveRemoveStaminSpell(
            casterSession,
            worldData,
            FrostArrowsSpellId);

        await InvokeLegacyRemoveStaminAsync(
            context,
            casterSession,
            CreateRemoveStaminQuery(FrostArrowsSpellId, x: 839, y: 584),
            playerSpell,
            spell);

        var updatedTarget = GetRemoveStaminSession(sessions, targetConnection);
        Assert.False(HasLegacyStatusFlag(
            updatedTarget.Character.Attributes,
            WorldPacketHandlers.LegacyFrostArrowsStatusFlag));
        Assert.False(updatedTarget.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyFrostArrowsExpiresAtUtcAttribute));
        Assert.False(updatedTarget.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyFrostArrowsRemoveOnDeadAttribute));
        Assert.DoesNotContain(ParseMsgUpdates(targetSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(839, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Empty(animation.Targets);
        Assert.Single(ParseSpellAnimations(targetSent));
        Assert.Contains("castPointDistance=9", log.ToString());
        Assert.Contains("targetCount=0 statusAppliedCount=0", log.ToString());
    }

    private static Dictionary<string, string> RemoveStaminAttributes(
        int battlePower,
        uint dynamicId,
        uint? forcedRoll = null)
    {
        var attributes = new Dictionary<string, string>
        {
            ["BattlePower"] = battlePower.ToString(),
            ["DynamicID"] = dynamicId.ToString(),
        };
        if (forcedRoll is { } roll)
        {
            attributes[WorldPacketHandlers.RuntimeLegacyPitchingRollAttribute] = roll.ToString();
        }

        return attributes;
    }

    private static MsgAttackQuery CreateRemoveStaminQuery(
        ushort spellId,
        ushort x = 830,
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
        ResolveRemoveStaminSpell(
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

    private static async Task<bool> InvokeLegacyRemoveStaminAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerRemoveStaminMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        return await Assert.IsType<Task<bool>>(result);
    }

    private static WorldSession GetRemoveStaminSession(
        WorldSessionStore sessions,
        ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static int FindRemoveStaminFrameIndex(
        IReadOnlyList<byte[]> frames,
        Func<PacketFrame, bool> predicate)
    {
        for (var index = 0; index < frames.Count; index++)
        {
            if (predicate(ReadFrame(frames[index])))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsRemoveStaminScalarUpdate(
        PacketFrame frame,
        uint uid,
        LegacyMsgUpdateDataType type,
        ulong value)
    {
        return LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
            update.Uid == uid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(type, value)]);
    }

    private static bool IsRemoveStaminStatusUpdate(
        PacketFrame frame,
        uint uid,
        int statusFlag)
    {
        return LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
            update.Uid == uid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, statusFlag));
    }
}
