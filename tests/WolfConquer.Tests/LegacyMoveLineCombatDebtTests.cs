using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(120U, 360U)]
    [InlineData(200U, 600U)]
    public async Task HandlerMoveLinePlayerCoefficientIgnoresSpellDamageRowWhileSobUsesIt(
        uint spellDamage,
        uint expectedSobDamage)
    {
        // Matching lqtxfwd Physical.OnPlayer:217-220 sets 101% for spell 11110
        // within 50 cells and bypasses the generic DBSpell.Damage multiplier.
        // Physical.cs:930-934 adds floor(303 * 50 / 100) when !flag2, then
        // MoveLine.Execute applies CalculateSoul (identity) and 12 / 16.
        // This pins those bounded source clauses, not the entire Physical pipeline.
        const uint expectedPlayerDamage = 340; // 300 -> 303 -> 454 -> floor(454 * 12 / 16).
        var sobNpc = CreateMoveLineSobNpc(id: 65_064, x: 838, y: 584);
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [sobNpc],
            magicRows: [CreateMoveLineRow(damage: spellDamage)]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellIds: [MoveLineSpellId]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 836, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        using var log = new StringWriter();

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: MoveLineSpellId)).ToFrameBytes()));

        foreach (var frames in new[] { casterSent, targetSent })
        {
            var animation = Assert.Single(ParseSpellAnimations(frames));
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(MoveLineSpellId, animation.SpellId);
            Assert.Equal((842, 584), (animation.X, animation.Y));
            Assert.Equal(
                new[] { TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
                animation.Targets.Select(target => target.Uid).Order().ToArray());
            Assert.Equal(
                expectedPlayerDamage,
                Assert.Single(animation.Targets, target => target.Uid == TargetPlayerUid).Damage);
            Assert.Equal(
                expectedSobDamage,
                Assert.Single(animation.Targets, target => target.Uid == checked((uint)sobNpc.Id)).Damage);
            Assert.All(animation.Targets, target =>
            {
                Assert.True(target.Damage > 0);
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            });
            Assert.Contains(frames, bytes => ReadFrame(bytes).Header.Opcode == 2_239);
            Assert.Empty(ParseLegacyBlackspotPackets(frames));
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTarget));
        Assert.NotNull(updatedTarget);
        Assert.Equal(5_000L - expectedPlayerDamage, updatedTarget.Character.MinHitPoints);
        Assert.Contains(targetSent, bytes => ReadFrame(bytes).Header.Opcode == 2_176);
        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life - (int)expectedSobDamage, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(490, updatedCaster.Character.MinMana);
        Assert.Equal("95", updatedCaster.Character.Attributes["Stamina"]);
        Assert.Equal((842L, 584L), (updatedCaster.Character.X, updatedCaster.Character.Y));
        Assert.Equal(
            TargetPlayerUid.ToString(System.Globalization.CultureInfo.InvariantCulture),
            updatedCaster.Character.Attributes[RuntimeLegacyThundercloudSightAttribute]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    public async Task HandlerMoveLinePhysicalEarlyReturnSkipsBreakthroughStanceInfinityAndBonus(
        int earlyReturnStatusFlag)
    {
        var result = await RunMoveLinePhysicalTailPlayerAttackAsync(
            targetStatusFlag: earlyReturnStatusFlag,
            wildPhoenix: true,
            penetrate: true,
            defensiveStance: true,
            infinitySpellPercent: 150);

        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            var animation = Assert.Single(ParseSpellAnimations(frames));
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(0U, target.Damage); // Physical returns 1 before MoveLine's 12 / 16.
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.Equal(5_000, result.Target.Character.MinHitPoints);
        Assert.Equal(
            earlyReturnStatusFlag == 0 ? "1" : "2",
            result.Target.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(result.Target.Character.Attributes, LegacyWildPhoenixFlag));
    }

    [Fact]
    public async Task HandlerMoveLineSuccessfulBreakthroughSkipsFiftyPercentBeforeCallerScaling()
    {
        var result = await RunMoveLinePhysicalTailPlayerAttackAsync(penetrate: true);
        // 303 + floor(303 * 30 / 100) = 393; floor(393 / 0.67) = 586;
        // the penetrating path skips +50%, then MoveLine applies floor(586 * 12 / 16).
        const uint expectedDamage = 439;
        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            var target = Assert.Single(Assert.Single(ParseSpellAnimations(frames)).Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.Equal(MsgAttackEffect.Penetration, target.Effect);
            Assert.True(target.Hit);
        }

        Assert.Equal(5_000L - expectedDamage, result.Target.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerMoveLineStanceThenInfinityPrecedeNonpenetratingBonusAndCallerScaling()
    {
        var result = await RunMoveLinePhysicalTailPlayerAttackAsync(
            defensiveStance: true,
            infinitySpellPercent: 150);
        // 303 / 2 = 151; floor(151 * 150 / 100) = 226; 226 + 113 = 339;
        // floor(339 * 12 / 16) = 254, with DefensiveStance's overwrite-to-Block.
        const uint expectedDamage = 254;
        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            var target = Assert.Single(Assert.Single(ParseSpellAnimations(frames)).Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.Equal(MsgAttackEffect.Block, target.Effect);
        }

        Assert.Equal(5_000L - expectedDamage, result.Target.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerMoveLineOrdinaryPhysicalDamageRoundingToZeroStillEnqueuesAcceptedPlayer()
    {
        var result = await RunMoveLinePhysicalTailPlayerAttackAsync(strength: 1);
        // floor(1 * 101 / 100) = 1; the +50% delta floors to zero;
        // MoveLine then applies floor(1 * 12 / 16) = 0 but still enqueues SpellObj2.
        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            var target = Assert.Single(Assert.Single(ParseSpellAnimations(frames)).Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(0U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.Equal(5_000, result.Target.Character.MinHitPoints);
    }

    private static async Task<MoveLinePhysicalTailPlayerResult> RunMoveLinePhysicalTailPlayerAttackAsync(
        int targetStatusFlag = 0,
        bool wildPhoenix = false,
        bool penetrate = false,
        bool defensiveStance = false,
        uint infinitySpellPercent = 0,
        long strength = 300)
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMoveLineRow(),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var attackerAttributes = new Dictionary<string, string>();
        var targetAttributes = new Dictionary<string, string>();
        if (penetrate)
        {
            attackerAttributes["BattlePower"] = "100";
            attackerAttributes["LegacyPhysicalBreakthroughRefineryRoll"] = "114"; // 14 < 20 - 5.
            attackerAttributes["Dragon"] = CreatePhysicalBreakthroughChiPower(
                LegacyChiAttributeType.Breakthrough,
                20);
            targetAttributes["BattlePower"] = "200";
            targetAttributes["Dragon"] = CreatePhysicalBreakthroughChiPower(
                LegacyChiAttributeType.Counteraction,
                5);
        }

        if (infinitySpellPercent > 0)
        {
            attackerAttributes["StatusFlags"] = BuildStatusFlags(InfinityStatusFlag);
            attackerAttributes["InfinitySDamage"] = infinitySpellPercent.ToString();
        }

        var targetFlags = new List<int>();
        if (targetStatusFlag != 0)
        {
            targetFlags.Add(targetStatusFlag);
        }
        if (defensiveStance)
        {
            targetFlags.Add(DefensiveStanceStatusFlag);
        }
        if (wildPhoenix)
        {
            targetFlags.Add(LegacyWildPhoenixFlag);
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2";
        }
        targetAttributes["StatusFlags"] = BuildStatusFlags(targetFlags.ToArray());

        var casterSession = CreateSession(
            strength: strength,
            stamina: 100,
            spellIds: [MoveLineSpellId],
            attributes: attackerAttributes);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 836,
            y: 584,
            spellIds: wildPhoenix ? [WildPhoenixSpellId] : [],
            attributes: targetAttributes);
        if (penetrate)
        {
            var attackerFields = LegacyPlayerStatusCalculator.CalculateFields(
                casterSession.Character, casterSession.PlayerItems, worldData, 5_000, 0);
            var targetFields = LegacyPlayerStatusCalculator.CalculateFields(
                targetSession.Character, targetSession.PlayerItems, worldData, 5_000, 0);
            Assert.Equal(20U, attackerFields[LegacyPlayerStatusPacket.BreakthroughIndex]);
            Assert.Equal(5U, targetFields[LegacyPlayerStatusPacket.CounteractionIndex]);
        }

        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        using var log = new StringWriter();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: MoveLineSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.Equal(490, finalCaster.Character.MinMana);
        Assert.Equal("95", finalCaster.Character.Attributes["Stamina"]);
        Assert.Equal((842L, 584L), (finalCaster.Character.X, finalCaster.Character.Y));
        Assert.Equal(
            TargetPlayerUid.ToString(System.Globalization.CultureInfo.InvariantCulture),
            finalCaster.Character.Attributes[RuntimeLegacyThundercloudSightAttribute]);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        return new MoveLinePhysicalTailPlayerResult(casterSent, targetSent, finalTarget);
    }

    private sealed record MoveLinePhysicalTailPlayerResult(
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        WorldSession Target);
}
