using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const string RevengeTailGateChangesAttribute = "LegacyRevengeTailChange";
    private const uint RevengeTailGateDamage = 5_000;

    [Theory]
    [InlineData(1U)]
    [InlineData(2U)]
    public async Task HandlerRevengeTailForbiddenPiratePrecedesEmptyBombWithoutHellOrReceiveHooks(uint charges)
    {
        var result = await RunRevengeTailGateCounterAsync(charges: charges, forbiddenPirateGate: true);
        var forbiddenIndex = SingleFrameIndex(result.AttackerSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ForbiddenPirate));
        var forbiddenFrame = ReadFrame(result.AttackerSent[forbiddenIndex]);
        Assert.Equal((ushort)2176, forbiddenFrame.Header.Opcode);
        Assert.True(LegacyMsgUpdatePacket.TryParse(forbiddenFrame, out var forbidden));
        Assert.Equal(PlayerUid, forbidden.Uid);
        var scalar = Assert.Single(forbidden.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ForbiddenPirate, scalar.Type);
        Assert.Equal(1UL, scalar.Value);
        Assert.True(forbiddenIndex <
            RevengeTailGateAnimationIndex(result.AttackerSent, RevengeTailAttachStatusSpellId));
        Assert.DoesNotContain(ParseMsgUpdates(result.DefenderSent), update =>
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ForbiddenPirate));
        foreach (var frames in new[] { result.AttackerSent, result.DefenderSent })
        {
            Assert.Single(ParseSpellAnimations(frames));
            AssertRevengeTailGateEmptyBomb(frames);
            Assert.Empty(RevengeTailGateHitpointIndices(frames));
            Assert.Empty(ParseDeathAttacks(frames));
            Assert.DoesNotContain(ParseMsgUpdates(frames), update => update.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.ArchiveSkill or
                    LegacyMsgUpdateDataType.BloodthirstElan or LegacyMsgUpdateDataType.Stamina));
        }
        foreach (var session in new[] { result.Attacker, result.Defender })
        {
            Assert.Equal(100_000, session.Character.MinHitPoints);
            Assert.Equal(500, session.Character.MinMana);
            Assert.Equal("37", session.Character.Attributes["ThundercloudSight"]);
            Assert.Equal("29", session.Character.Attributes["AttackHit"]);
            Assert.False(session.Character.Attributes.ContainsKey("Solid"));
            Assert.False(HasLegacyStatusFlag(session.Character.Attributes, 0x164));
            Assert.False(session.MagicCooldowns?.ContainsKey(HellVortexSpellId) == true);
        }
        Assert.Equal("100", result.Attacker.Character.Attributes["Stamina"]);
        Assert.Equal("89", result.Defender.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(result.Defender.Character.Attributes, LegacyImmortalForceFlag));
        AssertRevengeTailGateChargeAndReturnedState(result, charges - 1);
    }

    [Theory]
    [InlineData(1U)]
    [InlineData(2U)]
    public async Task HandlerRevengeTailGateCounterFinishesHellBeforeEmptyBombAndRetainsDefenderCosts(uint charges)
    {
        var result = await RunRevengeTailGateCounterAsync(charges: charges);
        foreach (var frames in new[] { result.AttackerSent, result.DefenderSent })
        {
            var hell = Assert.Single(ParseSpellAnimations(frames), animation => animation.SpellId == HellVortexSpellId);
            Assert.Equal(TargetPlayerUid, hell.AttackerUid);
            Assert.Equal(0U, hell.AttackedUid);
            Assert.Equal((0, 0), (hell.X, hell.Y));
            Assert.Equal(2, hell.LevelHu);
            var hit = Assert.Single(hell.Targets);
            Assert.Equal(PlayerUid, hit.Uid);
            Assert.Equal(result.HellDamage, hit.Damage);
            Assert.True(hit.Hit);
            AssertRevengeTailGateEmptyBomb(frames);
            Assert.True(RevengeTailGateAnimationIndex(frames, HellVortexSpellId) <
                RevengeTailGateAnimationIndex(frames, RevengeTailAttachStatusSpellId));
        }

        Assert.Equal(100_000L - result.HellDamage, result.Attacker.Character.MinHitPoints);
        Assert.Equal(500, result.Attacker.Character.MinMana);
        Assert.Equal("100", result.Attacker.Character.Attributes["Stamina"]);
        Assert.Equal(100_000, result.Defender.Character.MinHitPoints);
        AssertRevengeTailGateCosts(result.Defender);
        AssertRevengeTailGateChargeAndReturnedState(result, charges - 1);
        var hp = Assert.Single(RevengeTailGateHitpointIndices(result.AttackerSent));
        Assert.True(hp < RevengeTailGateAnimationIndex(result.AttackerSent, HellVortexSpellId));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.AttackerSent[hp]), out var update));
        Assert.Equal(100_000UL - result.HellDamage,
            Assert.Single(update.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).Value);
        Assert.Empty(RevengeTailGateHitpointIndices(result.DefenderSent));
    }

    [Fact]
    public async Task HandlerRevengeTailGateCounterReturnsLethallyUpdatedAttackerWithoutResurrectionOrSecondDeath()
    {
        var result = await RunRevengeTailGateCounterAsync(attackerHitpoints: 1);
        var hell = Assert.Single(ParseSpellAnimations(result.AttackerSent), animation => animation.SpellId == HellVortexSpellId);
        Assert.True(Assert.Single(hell.Targets).Damage > 1);
        Assert.Equal(0, result.Attacker.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.Attacker.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        AssertRevengeTailGateCosts(result.Defender);
        AssertRevengeTailGateChargeAndReturnedState(result, remaining: 0);
        AssertRevengeTailGateEmptyBomb(result.AttackerSent);
        var deathHp = AssertExistingNormalDeathCombatPhases(result.AttackerSent, PlayerUid,
            (900, 1), (900, 1), (900, 1), (900, 0));
        var hp = deathHp[^1];
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.AttackerSent[hp]), out var update));
        Assert.Equal(0UL, Assert.Single(update.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).Value);
        Assert.True(hp < RevengeTailGateAnimationIndex(result.AttackerSent, HellVortexSpellId));
        Assert.True(RevengeTailGateAnimationIndex(result.AttackerSent, HellVortexSpellId) <
            RevengeTailGateAnimationIndex(result.AttackerSent, RevengeTailAttachStatusSpellId));
        Assert.Single(result.AttackerSent, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Death &&
            attack.Query.Uid == TargetPlayerUid && attack.Query.OpponentUid == PlayerUid);
    }

    [Fact]
    public async Task HandlerRevengeTailGateKeepsSourceOriginalAttackerToDefenderDirection()
    {
        // BackDmg.cs:40 gates player.Owner -> target, not the ensuing Tail hit's direction.
        var result = await RunRevengeTailGateCounterAsync(defenderHellCounter: false, attackerHellCounter: true);
        Assert.DoesNotContain(ParseSpellAnimations(result.AttackerSent), animation => animation.SpellId == HellVortexSpellId);
        var tail = Assert.Single(ParseSpellAnimations(result.AttackerSent));
        Assert.Equal(RevengeTailAttachStatusSpellId, tail.SpellId);
        Assert.Equal(TargetPlayerUid, tail.AttackerUid);
        Assert.Equal(1, tail.BombFlag);
        var hit = Assert.Single(tail.Targets);
        Assert.Equal(PlayerUid, hit.Uid);
        Assert.Equal(RevengeTailGateDamage, hit.Damage);
        Assert.Equal(100_000L - RevengeTailGateDamage, result.Attacker.Character.MinHitPoints);
        Assert.Equal(500, result.Attacker.Character.MinMana);
        Assert.Equal(500, result.Defender.Character.MinMana);
        Assert.Equal("100", result.Defender.Character.Attributes["Stamina"]);
        Assert.False(result.Attacker.MagicCooldowns?.ContainsKey(HellVortexSpellId) == true);
        Assert.False(result.Defender.MagicCooldowns?.ContainsKey(HellVortexSpellId) == true);
        AssertRevengeTailGateChargeAndReturnedState(result, remaining: 0);
        var hp = Assert.Single(RevengeTailGateHitpointIndices(result.AttackerSent));
        Assert.True(hp < RevengeTailGateAnimationIndex(result.AttackerSent, RevengeTailAttachStatusSpellId));
    }

    [Fact]
    public async Task HandlerRevengeTailGateCounterCarriesBothCasterCostsAndBoundsReciprocalHellCounters()
    {
        var result = await RunRevengeTailGateCounterAsync(attackerHellCounter: true);
        var hell = ParseSpellAnimations(result.AttackerSent).Where(animation => animation.SpellId == HellVortexSpellId).ToArray();
        Assert.Equal([PlayerUid, TargetPlayerUid], hell.Select(animation => animation.AttackerUid));
        Assert.All(hell, animation => Assert.Empty(animation.Targets));
        AssertRevengeTailGateEmptyBomb(result.AttackerSent);
        AssertRevengeTailGateCosts(result.Attacker);
        AssertRevengeTailGateCosts(result.Defender);
        Assert.Equal(100_000, result.Attacker.Character.MinHitPoints);
        Assert.Equal(100_000, result.Defender.Character.MinHitPoints);
        Assert.Empty(RevengeTailGateHitpointIndices(result.AttackerSent));
        AssertRevengeTailGateChargeAndReturnedState(result, remaining: 0);
        Assert.True(RevengeTailGateAnimationIndex(result.AttackerSent, RevengeTailAttachStatusSpellId) >
            Enumerable.Range(0, result.AttackerSent.Count).Last(index =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(result.AttackerSent[index]), out var animation) &&
                animation.SpellId == HellVortexSpellId));
    }

    [Fact]
    public async Task HandlerRevengeTailGateCounterDecrementsLiveSignedChargesAcrossReentrantCallbacks()
    {
        // All three BackDmg entries see charge 1 before the awaited counter callbacks unwind.
        // Player.cs:495 is signed int; BackDmg.cs:57 therefore unwinds through 0, -1, -2.
        var result = await RunRevengeTailGateCounterAsync(reentrantTailDepth: 2);
        Assert.Equal(new long[] { 0, -1 }, result.ReentrantRemainingChanges);
        AssertRevengeTailGateChargeAndReturnedState(result, remaining: -2);
        AssertRevengeTailGateCosts(result.Defender);
        Assert.Equal(100_000L - result.HellDamage, result.Attacker.Character.MinHitPoints);
        Assert.Equal(100_000, result.Defender.Character.MinHitPoints);
        Assert.Equal(500, result.Attacker.Character.MinMana);
        Assert.Equal("100", result.Attacker.Character.Attributes["Stamina"]);
        foreach (var frames in new[] { result.AttackerSent, result.DefenderSent })
        {
            var hell = Assert.Single(ParseSpellAnimations(frames), animation => animation.SpellId == HellVortexSpellId);
            Assert.Equal(TargetPlayerUid, hell.AttackerUid);
            Assert.Equal(result.HellDamage, Assert.Single(hell.Targets).Damage);
            var tails = Enumerable.Range(0, frames.Count).Where(index =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(frames[index]), out var animation) &&
                animation.SpellId == RevengeTailAttachStatusSpellId).ToArray();
            Assert.Equal(3, tails.Length);
            Assert.All(tails, index =>
            {
                var frame = ReadFrame(frames[index]);
                Assert.Equal((ushort)2239, frame.Header.Opcode);
                Assert.True(LegacySpellAnimationPacket.TryParse(frame, out var tail));
                Assert.Equal(TargetPlayerUid, tail.AttackerUid);
                Assert.Equal(0U, tail.AttackedUid);
                Assert.Equal((842, 584), (tail.X, tail.Y));
                Assert.Equal(3, tail.LevelHu);
                Assert.Equal(1, tail.BombFlag);
                Assert.Empty(tail.Targets);
                Assert.True(RevengeTailGateAnimationIndex(frames, HellVortexSpellId) < index);
            });
        }
        var hp = Assert.Single(RevengeTailGateHitpointIndices(result.AttackerSent));
        Assert.True(hp < RevengeTailGateAnimationIndex(result.AttackerSent, HellVortexSpellId));
        Assert.Empty(RevengeTailGateHitpointIndices(result.DefenderSent));
    }

    [Fact]
    public async Task HandlerRevengeTailRefreshesChargesAndLethalAttackerAfterReentrantBombSend()
    {
        // BackDmg.cs:55 sends before :57 decrements the same live player counter.
        var result = await RunRevengeTailGateCounterAsync(attackerHitpoints: 12_000,
            defenderHellCounter: false, reentrantTailBombDepth: 2);
        Assert.Equal(new long[] { 0, -1 }, result.ReentrantRemainingChanges);
        AssertRevengeTailGateChargeAndReturnedState(result, remaining: -2);
        Assert.Equal(0, result.Attacker.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.Attacker.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.Equal(100_000, result.Defender.Character.MinHitPoints);
        Assert.Equal(500, result.Defender.Character.MinMana);
        Assert.Equal("100", result.Defender.Character.Attributes["Stamina"]);
        Assert.False(result.Defender.MagicCooldowns?.ContainsKey(HellVortexSpellId) == true);
        foreach (var frames in new[] { result.AttackerSent, result.DefenderSent })
        {
            var tails = ParseSpellAnimations(frames);
            Assert.Equal(3, tails.Count);
            Assert.All(tails, tail =>
            {
                Assert.Equal(RevengeTailAttachStatusSpellId, tail.SpellId);
                Assert.Equal(TargetPlayerUid, tail.AttackerUid);
                Assert.Equal(0U, tail.AttackedUid);
                Assert.Equal((842, 584), (tail.X, tail.Y));
                Assert.Equal(3, tail.LevelHu);
                Assert.Equal(1, tail.BombFlag);
                var hit = Assert.Single(tail.Targets);
                Assert.Equal(PlayerUid, hit.Uid);
                Assert.Equal(RevengeTailGateDamage, hit.Damage);
                Assert.True(hit.Hit);
            });
            Assert.All(frames.Where(bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out _)),
                bytes => Assert.Equal((ushort)2239, ReadFrame(bytes).Header.Opcode));
        }

        AssertExistingNormalDeathCombatPhases(result.AttackerSent, PlayerUid,
            (12_000, 7_000), (7_000, 2_000),
            (900, 2_000), (900, 900), (900, 900), (900, 0));
        var hp = RevengeTailGateHitpointIndices(result.AttackerSent);
        Assert.Equal(6, hp.Length);
        var remainingHitpoints = hp.Select(index =>
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.AttackerSent[index]), out var update));
            return Assert.Single(update.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).Value;
        });
        Assert.Equal(new ulong[] { 7_000, 2_000, 2_000, 900, 900, 0 }, remainingHitpoints);
        var firstTail = Enumerable.Range(0, result.AttackerSent.Count).First(index =>
            LegacySpellAnimationPacket.TryParse(ReadFrame(result.AttackerSent[index]), out _));
        Assert.True(hp[^1] < firstTail);
        Assert.Single(result.AttackerSent, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Death &&
            attack.Query.Uid == TargetPlayerUid && attack.Query.OpponentUid == PlayerUid);
        Assert.Empty(RevengeTailGateHitpointIndices(result.DefenderSent));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerRevengeTailGateCounterPreservesAliveAtBackDamageEntryGuard(bool enteredAlive)
    {
        var result = await RunRevengeTailGateCounterAsync(attackerHitpoints: 0, enteredAlive: enteredAlive);
        Assert.Equal(0, result.Attacker.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.Attacker.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.Empty(RevengeTailGateHitpointIndices(result.AttackerSent));
        AssertRevengeTailGateChargeAndReturnedState(result, enteredAlive ? 0U : 1U);
        if (enteredAlive)
        {
            var hell = Assert.Single(ParseSpellAnimations(result.AttackerSent), animation => animation.SpellId == HellVortexSpellId);
            Assert.Empty(hell.Targets);
            AssertRevengeTailGateEmptyBomb(result.AttackerSent);
            AssertRevengeTailGateCosts(result.Defender);
            Assert.True(RevengeTailGateAnimationIndex(result.AttackerSent, HellVortexSpellId) <
                RevengeTailGateAnimationIndex(result.AttackerSent, RevengeTailAttachStatusSpellId));
        }
        else
        {
            Assert.Empty(result.AttackerSent);
            Assert.Empty(result.DefenderSent);
            Assert.Equal(500, result.Defender.Character.MinMana);
            Assert.Equal("100", result.Defender.Character.Attributes["Stamina"]);
        }
    }

    private static void AssertRevengeTailGateCosts(WorldSession caster)
    {
        Assert.Equal(493, caster.Character.MinMana);
        Assert.Equal("89", caster.Character.Attributes["Stamina"]);
        Assert.True(caster.MagicCooldowns?.ContainsKey(HellVortexSpellId) == true);
    }

    private static void AssertRevengeTailGateChargeAndReturnedState(RevengeTailGateCounterResult result, long remaining)
    {
        Assert.Equal(remaining.ToString(CultureInfo.InvariantCulture), result.Defender.Character.Attributes[RevengeTailGateChangesAttribute]);
        Assert.Equal(remaining > 0, HasLegacyStatusFlag(result.Defender.Character.Attributes, RevengeTailStatusFlag));
        Assert.Equal(result.Attacker, result.ReturnedContext.Session);
        Assert.Equal(result.Attacker.Character, result.ReturnedAttacker);
        Assert.Equal(result.Defender, result.ReturnedTarget.Session);
    }

    private static void AssertRevengeTailGateEmptyBomb(IReadOnlyList<byte[]> frames)
    {
        var tail = Assert.Single(ParseSpellAnimations(frames), animation => animation.SpellId == RevengeTailAttachStatusSpellId);
        Assert.Equal(TargetPlayerUid, tail.AttackerUid);
        Assert.Equal(0U, tail.AttackedUid);
        Assert.Equal((842, 584), (tail.X, tail.Y));
        Assert.Equal(3, tail.LevelHu);
        Assert.Equal(1, tail.BombFlag);
        Assert.Empty(tail.Targets);
        RevengeTailGateAnimationIndex(frames, RevengeTailAttachStatusSpellId);
    }

    private static int RevengeTailGateAnimationIndex(IReadOnlyList<byte[]> frames, ushort spellId)
    {
        var index = SingleFrameIndex(frames, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var animation) && animation.SpellId == spellId);
        Assert.Equal((ushort)2239, ReadFrame(frames[index]).Header.Opcode);
        return index;
    }

    private static int[] RevengeTailGateHitpointIndices(IReadOnlyList<byte[]> frames)
    {
        var indices = Enumerable.Range(0, frames.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.All(indices, index => Assert.Equal((ushort)2176, ReadFrame(frames[index]).Header.Opcode));
        return indices;
    }

    private static async Task<RevengeTailGateCounterResult> RunRevengeTailGateCounterAsync(
        uint charges = 1,
        uint attackerHitpoints = 100_000,
        bool defenderHellCounter = true,
        bool attackerHellCounter = false,
        bool enteredAlive = false,
        int reentrantTailDepth = 0,
        int reentrantTailBombDepth = 0,
        bool forbiddenPirateGate = false)
    {
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [],
            CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 7, damage: 200,
                useStamina: 11, coolDown: 0, coldTime: 1_000, damage3: 30_030),
            CreateMagicRow(RevengeTailAttachStatusSpellId, sort: 6, useMana: 0,
                damage: RevengeTailGateDamage, coldTime: 0));
        var attackerAttributes = new Dictionary<string, string>();
        if (forbiddenPirateGate)
        {
            attackerAttributes["ThundercloudSight"] = "37";
            attackerAttributes["AttackHit"] = "29";
            attackerAttributes["LegacySpeffectLevel.Solid"] = "4";
            attackerAttributes["LegacySpeffectRoll.Solid"] = "0";
            attackerAttributes["LegacySpeffectLevel.Bloodthirst"] = "4";
            attackerAttributes["LegacySpeffectRoll.Bloodthirst"] = "0";
        }
        if (attackerHellCounter)
        {
            attackerAttributes[RuntimeLegacyNinjaHellVortexEquippedAttribute] = "1";
            attackerAttributes[RuntimeLegacyHellVortexRollAttribute] = "0";
        }
        if (attackerHitpoints == 0)
        {
            attackerAttributes["StatusFlags"] = BuildStatusFlags(LegacyPlayerDeathState.DeadStatusFlag);
        }
        var attacker = CreateSession(x: 840, minHitPoints: attackerHitpoints, stamina: 100,
            spellIds: attackerHellCounter ? [HellVortexSpellId] : [], attributes: attackerAttributes);
        var defenderAttributes = new Dictionary<string, string>
        {
            ["StatusFlags"] = forbiddenPirateGate
                ? BuildStatusFlags(RevengeTailStatusFlag, LegacyImmortalForceFlag)
                : BuildStatusFlags(RevengeTailStatusFlag),
            [RevengeTailGateChangesAttribute] = charges.ToString(CultureInfo.InvariantCulture),
        };
        if (forbiddenPirateGate)
        {
            defenderAttributes["ThundercloudSight"] = "37";
            defenderAttributes["AttackHit"] = "29";
            defenderAttributes["PerfectionStatus.LightOfStamina"] = "100";
            defenderAttributes["LegacyLightOfStaminaRoll"] = "0";
            defenderAttributes["LegacySpeffectLevel.Solid"] = "4";
            defenderAttributes["LegacySpeffectRoll.Solid"] = "0";
            defenderAttributes["LegacySpeffectLevel.Bloodthirst"] = "4";
            defenderAttributes["LegacySpeffectRoll.Bloodthirst"] = "0";
        }
        if (defenderHellCounter)
        {
            defenderAttributes[RuntimeLegacyNinjaHellVortexEquippedAttribute] = "1";
            defenderAttributes[RuntimeLegacyHellVortexRollAttribute] = "0";
        }
        var defender = CreateSession(uid: TargetPlayerUid, name: "RevengeTailGateDefender", x: 842,
            minHitPoints: 100_000, strength: 1_000, stamina: forbiddenPirateGate ? 89UL : 100UL,
            spellEntries:
            [
                CreatePlayerSpell(RevengeTailAttachStatusSpellId) with { SoulLevel = 3, UseSpellSoul = 3 },
                CreatePlayerSpell(HellVortexSpellId) with { SoulLevel = 2, UseSpellSoul = 2 },
            ],
            attributes: defenderAttributes);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(defender.PlayerSpells, worldData,
            HellVortexSpellId, out _, out var hellSpell));
        var baseDamage = InvokePrivateUInt32("CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            defender, attacker, hellSpell, worldData, 1U);
        // Physical's nonpenetrating +50% precedes HellVortex.cs:45's identity soul and 12/28.
        var hellDamage = (baseDamage + baseDamage * 50 / 100) * 12 / 28;
        Assert.True(hellDamage > 1);
        var sessions = new WorldSessionStore();
        var attackerSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        var reentrantRemainingChanges = new List<long>();
        var reentrantCalls = 0;
        var reentrantBombCalls = 0;
        WorldPacketHandlerContext? context = null;
        var attackerConnection = CreateSendingConnection(attackerSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        if (reentrantTailBombDepth > 0)
        {
            defenderConnection = new ConquerTcpConnection(localEndPoint: null, remoteEndPoint: null,
                sendAsync: async (frame, _) =>
                {
                    var bytes = frame.ToArray();
                    lock (defenderSent)
                    {
                        defenderSent.Add(bytes);
                    }
                    if (reentrantBombCalls >= reentrantTailBombDepth ||
                        !LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) ||
                        animation.SpellId != RevengeTailAttachStatusSpellId)
                    {
                        return;
                    }

                    reentrantBombCalls++;
                    await ReenterTailAsync();
                });
        }
        sessions.Set(attackerConnection, attacker);
        sessions.Set(defenderConnection, defender);

        async Task ReenterTailAsync()
        {
            var currentContext = context;
            Assert.NotNull(currentContext);
            Assert.True(sessions.TryGet(attackerConnection, out var currentAttacker));
            Assert.NotNull(currentAttacker);
            Assert.True(sessions.TryGet(defenderConnection, out var currentDefender));
            Assert.NotNull(currentDefender);
            var nested = await InvokeRevengeTailGateAsync(currentContext with { Session = currentAttacker },
                new WorldActiveSession(defenderConnection, currentDefender));
            reentrantRemainingChanges.Add(long.Parse(
                nested.Target.Session.Character.Attributes[RevengeTailGateChangesAttribute], CultureInfo.InvariantCulture));
        }

        using var log = new RevengeTailGateCounterLog(async line =>
        {
            if (reentrantCalls >= reentrantTailDepth || context is null ||
                !(line?.StartsWith("WorldServer sent legacy player HellVortex counter ", StringComparison.Ordinal) == true ||
                  line?.StartsWith("WorldServer skipped legacy player HellVortex counter cascade ", StringComparison.Ordinal) == true))
            {
                return;
            }

            reentrantCalls++;
            await ReenterTailAsync();
        });
        context = new WorldPacketHandlerContext(attackerConnection, log, attacker,
            WorldData: worldData, Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(), ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        var (returnedContext, returnedTarget, returnedAttacker) = await InvokeRevengeTailGateAsync(
            context, new WorldActiveSession(defenderConnection, defender), enteredAlive);
        Assert.True(sessions.TryGet(attackerConnection, out var finalAttacker));
        Assert.NotNull(finalAttacker);
        Assert.True(sessions.TryGet(defenderConnection, out var finalDefender));
        Assert.NotNull(finalDefender);
        return new(attackerSent, defenderSent, finalAttacker, finalDefender,
            returnedContext, returnedTarget, returnedAttacker, hellDamage, reentrantRemainingChanges);
    }

    private static async ValueTask<(WorldPacketHandlerContext Context, WorldActiveSession Target, CharacterSnapshot Attacker)>
        InvokeRevengeTailGateAsync(WorldPacketHandlerContext context, WorldActiveSession target, bool enteredAlive = false)
    {
        var method = typeof(WorldPacketHandlers).GetMethod("TryApplyLegacyPlayerReceiveRevengeTailAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        // Invoke only BackDmg.cs:40's second gate; a fixed Hell roll on an outer attack
        // would counter at the first CanAttackPlayer check before reaching BackDmg.
        Assert.NotNull(context.Session);
        var invocation = method.Invoke(null,
            [context, target, context.Session.Character, 1U,
                CancellationToken.None, enteredAlive]);
        Assert.NotNull(invocation);
        var asTask = invocation.GetType().GetMethod(nameof(ValueTask.AsTask), Type.EmptyTypes);
        Assert.NotNull(asTask);
        var task = Assert.IsAssignableFrom<Task>(asTask.Invoke(invocation, null));
        await task.ConfigureAwait(false);
        var resultProperty = task.GetType().GetProperty(nameof(Task<object>.Result));
        Assert.NotNull(resultProperty);
        var tuple = Assert.IsAssignableFrom<System.Runtime.CompilerServices.ITuple>(resultProperty.GetValue(task));
        Assert.Equal(4, tuple.Length);
        Assert.Equal("None", tuple[3]?.ToString());
        return (
            Assert.IsType<WorldPacketHandlerContext>(tuple[0]),
            Assert.IsType<WorldActiveSession>(tuple[1]),
            Assert.IsType<CharacterSnapshot>(tuple[2]));
    }

    private sealed class RevengeTailGateCounterLog(Func<string?, Task> onWrite) : StringWriter
    {
        public override async Task WriteLineAsync(string? value)
        {
            await onWrite(value);
            await base.WriteLineAsync(value);
        }
    }

    private sealed record RevengeTailGateCounterResult(
        IReadOnlyList<byte[]> AttackerSent,
        IReadOnlyList<byte[]> DefenderSent,
        WorldSession Attacker,
        WorldSession Defender,
        WorldPacketHandlerContext ReturnedContext,
        WorldActiveSession ReturnedTarget,
        CharacterSnapshot ReturnedAttacker,
        uint HellDamage,
        IReadOnlyList<long> ReentrantRemainingChanges);
}
