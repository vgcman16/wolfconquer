using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort HeavensWrathRuntimeSpellId = 15_710;
    private const byte HeavensWrathRuntimeSort = 95;
    private const int HeavensWrathRuntimeRideFlag = 0x32;
    private const int HeavensWrathRuntimeFlyFlag = 0x1B;
    private const int HeavensWrathRuntimeStatusFlag = 0xEC;

    [Fact]
    public async Task HandlerHeavensWrathFirstCastPreparesDismountsAnimatesThenAppliesDurationFlags()
    {
        const uint duration = 37;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateHeavensWrathRuntimeRow(duration: duration));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellIds: [HeavensWrathRuntimeSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(HeavensWrathRuntimeRideFlag),
                [WorldPacketHandlers.RuntimeLegacyRideExpiresAtUtcAttribute] =
                    DateTimeOffset.UtcNow.AddMinutes(10).ToString("O"),
                [WorldPacketHandlers.RuntimeLegacyRideRemoveOnDeadAttribute] = "true",
                [WorldPacketHandlers.RuntimeLegacyRideStampSecondsAttribute] = "1",
            },
            classCode: 9_001);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateHeavensWrathRuntimeQuery()).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Equal("97", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(updatedSession.MagicCooldowns?.ContainsKey(HeavensWrathRuntimeSpellId) ?? false);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, HeavensWrathRuntimeRideFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, HeavensWrathRuntimeStatusFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, HeavensWrathRuntimeFlyFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyRideExpiresAtUtcAttribute));
        Assert.False(updatedSession.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyRideRemoveOnDeadAttribute));
        Assert.False(updatedSession.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyRideStampSecondsAttribute));
        Assert.Equal(
            "true",
            updatedSession.Character.Attributes[
                WorldPacketHandlers.RuntimeLegacyHeavensWrathRemoveOnDeadAttribute]);
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyFlyRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            WorldPacketHandlers.RuntimeLegacyHeavensWrathExpiresAtUtcAttribute,
            beforeCast,
            afterCast,
            checked((int)duration));
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            WorldPacketHandlers.RuntimeLegacyFlyExpiresAtUtcAttribute,
            beforeCast,
            afterCast,
            checked((int)duration));

        var frames = sent.ToArray();
        var statusIndexes = Enumerable.Range(0, frames.Length)
            .Where(index => LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag))
            .ToArray();
        Assert.Equal(3, statusIndexes.Length);
        var statusValues = statusIndexes
            .Select(index => ParseMsgUpdates([frames[index]])
                .Single()
                .Updates
                .Single(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag)
                .Values)
            .ToArray();
        Assert.All(statusValues, value => Assert.NotNull(value));
        Assert.False(IsLegacyStatusFlagSet(statusValues[0]!, HeavensWrathRuntimeRideFlag));
        Assert.False(IsLegacyStatusFlagSet(statusValues[0]!, HeavensWrathRuntimeStatusFlag));
        Assert.False(IsLegacyStatusFlagSet(statusValues[0]!, HeavensWrathRuntimeFlyFlag));
        Assert.True(IsLegacyStatusFlagSet(statusValues[1]!, HeavensWrathRuntimeStatusFlag));
        Assert.False(IsLegacyStatusFlagSet(statusValues[1]!, HeavensWrathRuntimeFlyFlag));
        Assert.True(IsLegacyStatusFlagSet(statusValues[2]!, HeavensWrathRuntimeStatusFlag));
        Assert.True(IsLegacyStatusFlagSet(statusValues[2]!, HeavensWrathRuntimeFlyFlag));

        var animationIndex = SingleFrameIndex(
            frames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == HeavensWrathRuntimeSpellId);
        Assert.True(statusIndexes[0] < animationIndex);
        Assert.True(animationIndex < statusIndexes[1]);
        Assert.True(statusIndexes[1] < statusIndexes[2]);
        Assert.Empty(ParseMagicColdTimePackets(frames));

        var animation = Assert.Single(ParseSpellAnimations(frames));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Equal(0, animation.X);
        Assert.Equal(0, animation.Y);
        Assert.Equal(0, animation.SpellEffect);
        var selfTarget = Assert.Single(animation.Targets);
        Assert.Equal(PlayerUid, selfTarget.Uid);
        Assert.Equal(0U, selfTarget.Damage);
        Assert.False(selfTarget.Hit);
        Assert.Equal(MsgAttackEffect.None, selfTarget.Effect);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy HeavensWrath first cast", output);
        Assert.Contains("removedRide=True firstCast=True", output);
        Assert.Contains("source=HeavensWrath.cs case 15710", output);
    }

    [Fact]
    public async Task HandlerHeavensWrathWrongClassReturnsAfterResourcesAndCooldown()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateHeavensWrathRuntimeRow());
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [HeavensWrathRuntimeSpellId],
            classCode: 142);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateHeavensWrathRuntimeQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(43, updatedSession.Character.MinMana);
        Assert.Equal("97", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(updatedSession.MagicCooldowns?.ContainsKey(HeavensWrathRuntimeSpellId) ?? false);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.DoesNotContain(ParseMsgUpdates(sent), update =>
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        Assert.Empty(ParseMagicColdTimePackets(sent));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, HeavensWrathRuntimeStatusFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, HeavensWrathRuntimeFlyFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer source-backed returned legacy HeavensWrath magic", output);
        Assert.Contains("AtributesStatus.IsThunderStriker is false", output);
        Assert.Contains("returns after CanUseSpell.Verified", output);
    }

    [Fact]
    public async Task HandlerHeavensWrathActiveRecastUsesAttackCenterProgressThenEffectOneAnimation()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var edgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 843, boundY: 584);
        var outsideSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 844, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_710, x: 836, y: 591);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [edgeSpawn, outsideSpawn],
            [sobNpc],
            [
                CreateHeavensWrathRuntimeRow(experience: 1),
                CreateHeavensWrathRuntimeRow(level: 1, experience: 100),
            ]);
        var edgeMonster = CreateRuntimeMonsterInstance(worldData, edgeSpawn, monsterFamily);
        var outsideMonster = CreateRuntimeMonsterInstance(worldData, outsideSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var expiry = DateTimeOffset.UtcNow.AddSeconds(37).ToString("O");
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            minMana: 500,
            strength: 300,
            stamina: 100,
            spellIds: [HeavensWrathRuntimeSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(
                    HeavensWrathRuntimeStatusFlag,
                    HeavensWrathRuntimeFlyFlag),
                [WorldPacketHandlers.RuntimeLegacyHeavensWrathExpiresAtUtcAttribute] = expiry,
                [WorldPacketHandlers.RuntimeLegacyHeavensWrathRemoveOnDeadAttribute] = "true",
                [WorldPacketHandlers.RuntimeLegacyFlyExpiresAtUtcAttribute] = expiry,
                ["LegacyFlyRemoveOnDead"] = "true",
            },
            classCode: 9_001);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "HeavensWrathTarget",
            x: 842,
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
            SobNpcStates: sobNpcStates);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            HeavensWrathRuntimeSpellId,
            out _,
            out var spell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            edgeMonster,
            monsterFamily,
            spell);
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateHeavensWrathRuntimeQuery(
                opponentUid: edgeMonster.Uid,
                x: 836,
                y: 584)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(830, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(1, animation.SpellEffect);
        AssertBombEqualsOneIfParserSupportsIt(animation);
        Assert.Equal(
            new[] { edgeMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) },
            animation.Targets.Select(target => target.Uid).ToArray());
        Assert.Equal(expectedMonsterDamage, animation.Targets[0].Damage);
        Assert.Equal(expectedPlayerDamage, animation.Targets[1].Damage);
        Assert.Equal(expectedSobNpcDamage, animation.Targets[2].Damage);
        Assert.All(animation.Targets, target => Assert.True(target.Hit));

        Assert.True(monsterStates.TryGetVisibleInstance(edgeMonster, DateTimeOffset.UtcNow, out var damagedEdge));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideMonster, DateTimeOffset.UtcNow, out var untouchedOutside));
        Assert.Equal(edgeMonster.MaxLife - expectedMonsterDamage, damagedEdge.Hitpoints);
        Assert.Equal(outsideMonster.MaxLife, untouchedOutside.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);
        Assert.Equal("97", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.False(updatedCasterSession.MagicCooldowns?.ContainsKey(HeavensWrathRuntimeSpellId) ?? false);
        Assert.Equal(1, Assert.Single(updatedCasterSession.PlayerSpells.Spells).Level);
        Assert.Empty(ParseMagicColdTimePackets(casterSent));
        var progressIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellPacket.TryParse(frame, out var packet) &&
                packet.Id == HeavensWrathRuntimeSpellId);
        var animationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == HeavensWrathRuntimeSpellId);
        Assert.True(progressIndex < animationIndex);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy HeavensWrath active recast", output);
        Assert.Contains("centerX=836 centerY=584", output);
        Assert.Contains("animationX=830 animationY=584 radius=7 spellEffect=1", output);
        Assert.Contains("monsterTargets=1 playerTargets=1 sobNpcTargets=1", output);
        Assert.Contains("source=HeavensWrath.cs case 15710", output);
    }

    private static MagicType CreateHeavensWrathRuntimeRow(
        byte level = 0,
        uint experience = 0,
        uint duration = 37)
    {
        return CreateMagicRow(
            HeavensWrathRuntimeSpellId,
            sort: HeavensWrathRuntimeSort,
            level: level,
            useMana: 7,
            damage: 100,
            experience: experience,
            maxTargets: 30,
            range: 15,
            weaponType: 61_681_680,
            useStamina: 3,
            coolDown: 1_000,
            coldTime: 300,
            duration: duration,
            damageOnMonster: 30_125);
    }

    private static MsgAttackQuery CreateHeavensWrathRuntimeQuery(
        uint opponentUid = PlayerUid,
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
            SpellId: HeavensWrathRuntimeSpellId);
    }
}
