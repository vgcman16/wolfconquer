using System.Globalization;
using System.Net;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int ShockPhysicalTailSolidFlag = 0x164;
    private const string ShockPhysicalTailChargesAttribute = "LegacyRevengeTailChange";

    [Theory]
    [InlineData(false, 0, 0, 261U)]
    [InlineData(false, 0, 3, 261U)]
    [InlineData(true, 0, 3, 130U)]
    [InlineData(false, 153, 3, 400U)]
    [InlineData(true, 153, 3, 198U)]
    public async Task HandlerShockPhysicalTailUsesLiteralStanceInfinityThenNonpenetratingBonus(
        bool stance, int infinityPercent, int soul, uint expectedDamage)
    {
        var result = await RunShockPhysicalTailAsync(stance: stance, infinityPercent: infinityPercent,
            soul: checked((byte)soul));
        // Physical.cs:257-290: floor(floor(301 * .82) * .95) = 233.
        // :913-934: stance /2, Infinity, then +50% (zero BreakthroughIndex).
        // Normal 349 -> 261; stance 174 -> 130; Infinity 534 -> 400;
        // stance + Infinity 265 -> 198. Shock.cs:47-48 leaves soul unchanged, then *75/100.
        AssertShockPhysicalTailIncoming(result, expectedDamage, stance ? MsgAttackEffect.Block : MsgAttackEffect.None);
        Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
        Assert.Equal(493, result.Caster.Character.MinMana);
        Assert.Equal("95", result.Caster.Character.Attributes["Stamina"]);
    }

    [Theory]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag, false)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag, false)]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag, true)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag, true)]
    public async Task HandlerShockPhysicalStatusReturnPrecedesPhoenixAndEntireNormalTail(
        int statusFlag, bool phoenix)
    {
        var result = await RunShockPhysicalTailAsync(statusFlag: statusFlag, phoenix: phoenix,
            stance: true, infinityPercent: 153, mudPower: 1_700, wonder: true, revengeDamage: 5_000);
        // Physical.cs:157-165 returns 1 before WildPhoenix, stance, Infinity or BackDmg.
        AssertShockPhysicalTailIncoming(result, 0, MsgAttackEffect.None);
        AssertShockPhysicalTailNoCounters(result);
        Assert.Equal("1", result.Target.Character.Attributes[ShockPhysicalTailChargesAttribute]);
        Assert.True(HasLegacyStatusFlag(result.Target.Character.Attributes, RevengeTailStatusFlag));
        Assert.DoesNotContain(ParseMsgUpdates(result.TargetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == LegacyWildPhoenixFlag);
        if (phoenix)
        {
            Assert.Equal("1", result.Target.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
            Assert.True(HasLegacyStatusFlag(result.Target.Character.Attributes, LegacyWildPhoenixFlag));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerShockPhysicalPhoenixReturnSkipsNormalTailButStillRunsOpeningReceive(bool stance)
    {
        var result = await RunShockPhysicalTailAsync(phoenix: true, stance: stance,
            infinityPercent: 153, mudPower: 1_700, wonder: true, revengeDamage: 5_000);
        AssertShockPhysicalTailIncoming(result, 0, MsgAttackEffect.None);
        AssertShockPhysicalTailNoCounters(result);
        Assert.Equal("0", result.Target.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.False(HasLegacyStatusFlag(result.Target.Character.Attributes, LegacyWildPhoenixFlag));
        Assert.Equal("1", result.Target.Character.Attributes[ShockPhysicalTailChargesAttribute]);
        var phoenixIndex = SingleFrameIndex(result.TargetSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag && entry.Time == 32 && entry.Damage == 18_007 && entry.SpellLevel == 0));
        Assert.Equal((ushort)2176, ReadFrame(result.TargetSent[phoenixIndex]).Header.Opcode);
        Assert.True(phoenixIndex < ShockPhysicalTailSolidIndex(result.TargetSent));
    }

    [Theory]
    [InlineData(200, 257U)]
    [InlineData(1_700, 217U)]
    public async Task HandlerShockPhysicalMudWallReducesBefore75PercentAndExactlyOnce(int power, uint expectedDamage)
    {
        var result = await RunShockPhysicalTailAsync(mudPower: power);
        // Physical 349 - floor(349 * 2/100) = 343; Shock floor(343 * .75) = 257.
        // Reducing the already-scaled 261 instead gives 256. At 17%, 349 -> 290 -> 217.
        AssertShockPhysicalTailIncoming(result, expectedDamage, MsgAttackEffect.None);
        Assert.True(HasLegacyStatusFlag(result.Target.Character.Attributes, MudWallStatusFlag));
        Assert.NotEqual(expectedDamage - expectedDamage * checked((uint)power / 100) / 100, expectedDamage);
    }

    [Theory]
    [InlineData(false, 0, 200, 349U, false, 257U)]
    [InlineData(false, 0, 200, 350U, true, 257U)]
    [InlineData(true, 153, 1_700, 265U, false, 165U)]
    [InlineData(true, 153, 1_700, 266U, true, 165U)]
    public async Task HandlerShockPhysicalBackDamageThresholdSeesTailBeforeMudWallAndCallerScaling(
        bool stance, int infinityPercent, int mudPower, uint threshold, bool retaliates, uint expectedDamage)
    {
        var result = await RunShockPhysicalTailAsync(stance: stance, infinityPercent: infinityPercent,
            mudPower: mudPower, revengeDamage: threshold);
        // BackDmg.cs:36 is strict: 349 < threshold (or stance+Infinity's 265).
        // Either MudWall or Shock's 75% applied before that test would incorrectly pass equality.
        AssertShockPhysicalTailIncoming(result, expectedDamage, stance ? MsgAttackEffect.Block : MsgAttackEffect.None);
        var tails = ParseSpellAnimations(result.CasterSent)
            .Where(animation => animation.SpellId == RevengeTailAttachStatusSpellId).ToArray();
        Assert.Equal(retaliates ? "0" : "1", result.Target.Character.Attributes[ShockPhysicalTailChargesAttribute]);
        Assert.Equal(!retaliates, HasLegacyStatusFlag(result.Target.Character.Attributes, RevengeTailStatusFlag));
        if (retaliates)
        {
            var tail = Assert.Single(tails);
            AssertShockPhysicalTailRevengeAnimation(tail, threshold);
            Assert.Equal(100_000L - threshold, result.Caster.Character.MinHitPoints);
            var casterHp = Assert.Single(ShockPhysicalTailHpIndices(result.CasterSent, PlayerUid));
            Assert.True(casterHp < ShockPhysicalTailAnimationIndex(result.CasterSent, RevengeTailAttachStatusSpellId));
            Assert.True(ShockPhysicalTailAnimationIndex(result.TargetSent, RevengeTailAttachStatusSpellId) <
                ShockPhysicalTailSolidIndex(result.TargetSent));
        }
        else
        {
            Assert.Empty(tails);
            Assert.Empty(ShockPhysicalTailHpIndices(result.CasterSent, PlayerUid));
            Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
        }
    }

    [Fact]
    public async Task HandlerShockPhysicalWonderThenRevengeCompleteBeforeOpeningReceiveAndRetainNestedState()
    {
        var result = await RunShockPhysicalTailAsync(stance: true, infinityPercent: 153,
            mudPower: 1_700, wonder: true, revengeDamage: 5_000);
        var wonder = Assert.Single(ParseSpellAnimations(result.CasterSent), animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Equal(TargetPlayerUid, wonder.AttackerUid);
        Assert.Equal(0U, wonder.AttackedUid);
        Assert.Equal((0, 0), (wonder.X, wonder.Y));
        var wonderHit = Assert.Single(wonder.Targets);
        Assert.Equal(PlayerUid, wonderHit.Uid);
        Assert.True(wonderHit.Damage > 0);
        var revenge = Assert.Single(ParseSpellAnimations(result.CasterSent), animation => animation.SpellId == RevengeTailAttachStatusSpellId);
        AssertShockPhysicalTailRevengeAnimation(revenge, 5_000);
        AssertShockPhysicalTailIncoming(result, 165, MsgAttackEffect.Block);
        // 233 / 2 = 116; floor(116 * 1.53) = 177; +88 = 265;
        // after both counters: 265 - floor(265 * .17) = 220; Shock floor(220 * .75) = 165.
        Assert.Equal(100_000L - wonderHit.Damage - 5_000, result.Caster.Character.MinHitPoints);
        Assert.Equal(493, result.Caster.Character.MinMana);
        Assert.Equal("95", result.Caster.Character.Attributes["Stamina"]);
        Assert.Equal(493, result.Target.Character.MinMana);
        Assert.Equal("95", result.Target.Character.Attributes["Stamina"]);
        Assert.True(result.Target.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
        Assert.Equal("0", result.Target.Character.Attributes[ShockPhysicalTailChargesAttribute]);
        var beforeHit = Assert.Single(result.Observations);
        // ReceiveAttack.Player:565 sets AttackHit after the opening Solid hook.
        Assert.False(beforeHit.Caster.Character.Attributes.ContainsKey("AttackHit"));
        Assert.Equal("true", result.Caster.Character.Attributes["AttackHit"]);
        Assert.Equal(result.Caster.Character.Attributes.Where(entry => entry.Key != "AttackHit").OrderBy(entry => entry.Key),
            beforeHit.Caster.Character.Attributes.OrderBy(entry => entry.Key));
        Assert.Equal(result.Caster, beforeHit.Caster with
        {
            Character = beforeHit.Caster.Character with { Attributes = result.Caster.Character.Attributes },
        });
        Assert.Equal(100_000, beforeHit.Target.Character.MinHitPoints);
        Assert.Equal(493, beforeHit.Target.Character.MinMana);
        Assert.Equal("0", beforeHit.Target.Character.Attributes[ShockPhysicalTailChargesAttribute]);
        Assert.True(beforeHit.Target.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
        var casterHp = ShockPhysicalTailHpIndices(result.CasterSent, PlayerUid);
        Assert.Equal(2, casterHp.Length);
        Assert.True(casterHp[0] < ShockPhysicalTailAnimationIndex(result.CasterSent, HeavensWonderRuntimeSpellId));
        Assert.True(casterHp[1] < ShockPhysicalTailAnimationIndex(result.CasterSent, RevengeTailAttachStatusSpellId));
        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            Assert.True(ShockPhysicalTailAnimationIndex(frames, HeavensWonderRuntimeSpellId) <
                ShockPhysicalTailAnimationIndex(frames, RevengeTailAttachStatusSpellId));
        }
        Assert.True(ShockPhysicalTailAnimationIndex(result.TargetSent, RevengeTailAttachStatusSpellId) <
            ShockPhysicalTailSolidIndex(result.TargetSent));
        Assert.DoesNotContain(ParseMsgUpdates(result.CasterSent), update => update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == ShockPhysicalTailSolidFlag));
    }

    [Theory]
    [InlineData(5_000U, 4_739U, 0U, 100_000U)]
    [InlineData(100U, 0U, 161U, 99_839U)]
    public async Task HandlerShockPhysicalShieldAbsorptionPublishesAdjustedAnimationAndOneHpPair(
        uint shieldLeft, uint remainingShield, uint receiveDamage, uint remainingHitpoints)
    {
        var result = await RunShockPhysicalTailAsync(lightningShieldLeft: shieldLeft);
        // SpellObj is a class (MsgSpellAnimation.cs:75). Receive.Player:214-224
        // mutates its 261 damage before Shock enqueues it, without capping it to HP.
        Assert.Equal(261U, shieldLeft - remainingShield + receiveDamage);
        AssertShockPhysicalTailIncoming(result, receiveDamage, MsgAttackEffect.None);
        Assert.Equal(remainingHitpoints, result.Target.Character.MinHitPoints);
        Assert.Equal(remainingShield.ToString(CultureInfo.InvariantCulture),
            result.Target.Character.Attributes["LightningShieldLeft"]);
        Assert.True(HasLegacyStatusFlag(result.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
        var beforeHit = Assert.Single(result.Observations);
        Assert.Equal(shieldLeft.ToString(CultureInfo.InvariantCulture),
            beforeHit.Target.Character.Attributes["LightningShieldLeft"]);
        Assert.Equal(100_000, beforeHit.Target.Character.MinHitPoints);

        var shieldIndex = SingleFrameIndex(result.TargetSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield &&
                entry.Flag == 237 && entry.Damage == remainingShield && entry.SpellLevel == 0));
        Assert.Equal((ushort)2176, ReadFrame(result.TargetSent[shieldIndex]).Header.Opcode);
        var hpIndices = Enumerable.Range(0, result.TargetSent.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(result.TargetSent[index]), out var update) &&
            update.Uid == TargetPlayerUid && update.Updates.Any(entry => entry.Type is
                LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(2, hpIndices.Length);
        Assert.All(hpIndices, index => Assert.Equal((ushort)2176, ReadFrame(result.TargetSent[index]).Header.Opcode));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.TargetSent[hpIndices[0]]), out var maximum));
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 100_000)], maximum.Updates);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.TargetSent[hpIndices[1]]), out var current));
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 100_000),
             new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, remainingHitpoints)], current.Updates);
        // Receive.Player:799 still invokes the HP setter after full absorption.
        Assert.True(ShockPhysicalTailSolidIndex(result.TargetSent) < shieldIndex);
        Assert.True(shieldIndex < hpIndices[0] && hpIndices[0] < hpIndices[1]);
        Assert.True(hpIndices[1] < ShockPhysicalTailAnimationIndex(result.TargetSent, CrackingShockSpellId));
    }

    private static async Task<ShockPhysicalTailResult> RunShockPhysicalTailAsync(
        int statusFlag = 0, bool phoenix = false, bool stance = false, int infinityPercent = 0,
        int mudPower = 0, bool wonder = false, uint? revengeDamage = null, byte soul = 3,
        uint? lightningShieldLeft = null, bool targetNeptune = false, bool attackerNeptune = false,
        uint targetHitpoints = 100_000, string? tournamentType = null)
    {
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [],
            CreateCrackingShockDebtRow(level: 0, experience: 0, damageOnMonster: 30_095),
            CreateCounterKillRuntimeRow(HeavensWonderRuntimeSpellId, coolDown: 0, coldTime: 1_000),
            CreateMagicRow(RevengeTailAttachStatusSpellId, sort: 6, useMana: 0,
                damage: revengeDamage ?? 5_000, coldTime: 0),
            CreateMagicRow(WildPhoenixSpellId, sort: 6, useMana: 0, damage: 4, coldTime: 0,
                duration: 32, damageOnHuman: 18, damage2: 7),
            CreateMagicRow(15_750, sort: 6, useMana: 0, damage: 0, coldTime: 0, duration: 10));
        if (mudPower > 0)
        {
            AddPhysicalMudWallGouyuRow(worldData,
                CreatePhysicalMudWallGouyuRow(PhysicalMudWallSigilType, PhysicalMudWallSigilLevel, mudPower));
        }
        var casterAttributes = tournamentType is null
            ? new Dictionary<string, string>()
            : CreatePlayerReceiveTournamentCasterAttributes(tournamentType);
        casterAttributes["PkMode"] = "0";
        var casterFlags = new List<int>();
        if (infinityPercent > 0)
        {
            casterFlags.Add(InfinityStatusFlag);
            casterAttributes["InfinitySDamage"] = infinityPercent.ToString(CultureInfo.InvariantCulture);
        }
        if (attackerNeptune) casterFlags.Add(LegacyNeptuneCurseFlag);
        if (casterFlags.Count > 0) casterAttributes["StatusFlags"] = BuildStatusFlags(casterFlags.ToArray());
        var caster = CreateSession(x: 840, minHitPoints: 100_000, strength: 301, spirit: 0, stamina: 100,
            classCode: 9_001, attributes: casterAttributes,
            spellEntries: [CreatePlayerSpell(CrackingShockSpellId) with { SoulLevel = 3, UseSpellSoul = soul }]);
        var targetFlags = new List<int>();
        var targetSpells = new List<ushort>();
        var targetAttributes = new Dictionary<string, string>
        {
            ["PkMode"] = "0",
            ["MaxHitpoints"] = "100000",
            ["LegacySpeffectLevel.Solid"] = "4",
            ["LegacySpeffectRoll.Solid"] = "0",
        };
        if (statusFlag != 0) targetFlags.Add(statusFlag);
        if (targetNeptune) targetFlags.Add(LegacyNeptuneCurseFlag);
        if (stance) targetFlags.Add(DefensiveStanceStatusFlag);
        if (tournamentType == "SkillTournament") targetAttributes["SkillTournamentLifes"] = "2";
        if (mudPower > 0)
        {
            targetFlags.Add(MudWallStatusFlag);
            targetAttributes["MsgNinja"] = BuildPhysicalMudWallMsgNinja(
                PhysicalMudWallSigilType, PhysicalMudWallSigilLevel, position: 1);
        }
        if (wonder)
        {
            targetSpells.Add(HeavensWonderRuntimeSpellId);
            targetAttributes[CounterKill16350BackDamageRollAttribute] = "0";
        }
        if (revengeDamage is not null)
        {
            targetSpells.Add(RevengeTailAttachStatusSpellId);
            targetFlags.Add(RevengeTailStatusFlag);
            targetAttributes[ShockPhysicalTailChargesAttribute] = "1";
        }
        if (phoenix)
        {
            targetSpells.Add(WildPhoenixSpellId);
            targetFlags.Add(LegacyWildPhoenixFlag);
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "1";
        }
        if (lightningShieldLeft is { } shieldLeft)
        {
            targetFlags.Add(LegacyLightningShieldActivatedFlag);
            targetSpells.Add(15_750);
            targetAttributes["LightningShieldLeft"] = shieldLeft.ToString(CultureInfo.InvariantCulture);
        }
        targetAttributes["StatusFlags"] = BuildStatusFlags(targetFlags.ToArray());
        var target = CreateSession(uid: TargetPlayerUid, name: "ShockPhysicalTailTarget", x: 842,
            minHitPoints: targetHitpoints, strength: 1_000, spirit: 0, stamina: 100, classCode: 5_005,
            spellIds: targetSpells, attributes: targetAttributes);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var observations = new List<ShockPhysicalTailObservation>();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                targetSent.Add(bytes);
                if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == TargetPlayerUid &&
                    update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == ShockPhysicalTailSolidFlag))
                {
                    observations.Add(new ShockPhysicalTailObservation(
                        Assert.Single(sessions.Snapshot(), active => active.Session.Character.Uid == PlayerUid).Session,
                        Assert.Single(sessions.Snapshot(), active => active.Session.Character.Uid == TargetPlayerUid).Session));
                }
                return ValueTask.CompletedTask;
            });
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        await PacketHandlers.HandleAsync(new WorldPacketHandlerContext(
                casterConnection, log, caster, WorldData: worldData, Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore(), SobNpcStates: new SobNpcRuntimeStateStore(),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 836, Y: 584, AttackType: MsgAttackType.Magic,
                Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None,
                Unknown: 0, SpellId: CrackingShockSpellId)).ToFrameBytes()));
        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        return new(finalCaster, finalTarget, casterSent, targetSent, observations, soul);
    }

    private static void AssertShockPhysicalTailIncoming(ShockPhysicalTailResult result, uint damage, MsgAttackEffect effect)
    {
        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            var animation = Assert.Single(ParseSpellAnimations(frames), packet => packet.SpellId == CrackingShockSpellId);
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal((836, 584), (animation.X, animation.Y));
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(result.Soul, animation.LevelHu);
            Assert.Equal(0, animation.BombFlag);
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, hit.Uid);
            Assert.Equal(damage, hit.Damage);
            Assert.Equal(effect, hit.Effect);
            Assert.True(hit.Hit);
            Assert.Equal((0, 0), (hit.NewX, hit.NewY));
            ShockPhysicalTailAnimationIndex(frames, CrackingShockSpellId);
        }
        Assert.Equal(100_000L - damage, result.Target.Character.MinHitPoints);
        Assert.Equal((842L, 584L), (result.Target.Character.X, result.Target.Character.Y));
        var beforeHit = Assert.Single(result.Observations);
        Assert.Equal(100_000, beforeHit.Target.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(beforeHit.Target.Character.Attributes, ShockPhysicalTailSolidFlag));
        Assert.True(HasLegacyStatusFlag(result.Target.Character.Attributes, ShockPhysicalTailSolidFlag));
        var hp = Assert.Single(ShockPhysicalTailHpIndices(result.TargetSent, TargetPlayerUid));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.TargetSent[hp]), out var update));
        Assert.Equal(100_000UL - damage,
            Assert.Single(update.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).Value);
        var maxHp = SingleFrameIndex(result.TargetSent, frame => LegacyMsgUpdatePacket.TryParse(frame, out var maximum) &&
            maximum.Uid == TargetPlayerUid && maximum.Updates.Count == 1 &&
            maximum.Updates[0].Type == LegacyMsgUpdateDataType.MaxHitpoints && maximum.Updates[0].Value == 100_000);
        Assert.Equal((ushort)2176, ReadFrame(result.TargetSent[maxHp]).Header.Opcode);
        Assert.True(ShockPhysicalTailSolidIndex(result.TargetSent) < maxHp && maxHp < hp);
        Assert.True(hp < ShockPhysicalTailAnimationIndex(result.TargetSent, CrackingShockSpellId));
    }

    private static void AssertShockPhysicalTailNoCounters(ShockPhysicalTailResult result)
    {
        Assert.DoesNotContain(ParseSpellAnimations(result.CasterSent), animation =>
            animation.SpellId is HeavensWonderRuntimeSpellId or RevengeTailAttachStatusSpellId);
        Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
        Assert.Equal(500, result.Target.Character.MinMana);
        Assert.Equal("100", result.Target.Character.Attributes["Stamina"]);
        Assert.False(result.Target.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
        Assert.Empty(ShockPhysicalTailHpIndices(result.CasterSent, PlayerUid));
    }

    private static void AssertShockPhysicalTailRevengeAnimation(LegacySpellAnimationPacket tail, uint damage)
    {
        Assert.Equal(TargetPlayerUid, tail.AttackerUid);
        Assert.Equal(0U, tail.AttackedUid);
        Assert.Equal((842, 584), (tail.X, tail.Y));
        Assert.Equal(1, tail.BombFlag);
        var hit = Assert.Single(tail.Targets);
        Assert.Equal(PlayerUid, hit.Uid);
        Assert.Equal(damage, hit.Damage);
        Assert.True(hit.Hit);
    }

    private static int ShockPhysicalTailSolidIndex(IReadOnlyList<byte[]> frames)
    {
        var index = SingleFrameIndex(frames, frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
            update.Uid == TargetPlayerUid && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == ShockPhysicalTailSolidFlag && entry.Damage == 2_400 && entry.Time == 5));
        Assert.Equal((ushort)2176, ReadFrame(frames[index]).Header.Opcode);
        return index;
    }

    private static int ShockPhysicalTailAnimationIndex(IReadOnlyList<byte[]> frames, ushort spellId)
    {
        var index = SingleFrameIndex(frames, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var animation) && animation.SpellId == spellId);
        Assert.Equal((ushort)2239, ReadFrame(frames[index]).Header.Opcode);
        return index;
    }

    private static int[] ShockPhysicalTailHpIndices(IReadOnlyList<byte[]> frames, uint uid)
    {
        var indices = Enumerable.Range(0, frames.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) && update.Uid == uid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.All(indices, index => Assert.Equal((ushort)2176, ReadFrame(frames[index]).Header.Opcode));
        return indices;
    }

    private sealed record ShockPhysicalTailObservation(WorldSession Caster, WorldSession Target);

    private sealed record ShockPhysicalTailResult(
        WorldSession Caster, WorldSession Target, IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent, IReadOnlyList<ShockPhysicalTailObservation> Observations, byte Soul);
}
