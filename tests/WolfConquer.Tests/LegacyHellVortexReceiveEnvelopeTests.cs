using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int HellVortexReceiveEnvelopeSageFlag = 0x111;
    private const int HellVortexReceiveEnvelopeSolidFlag = 0x164;
    private const int HellVortexReceiveEnvelopeHawksFlag = 0x100;
    private const uint HellVortexReceiveEnvelopeBoundaryUid = TargetPlayerUid + 1;
    private const uint HellVortexReceiveEnvelopeOutsideUid = TargetPlayerUid + 2;
    private const uint HellVortexReceiveEnvelopeCasterNearUid = TargetPlayerUid + 3;

    [Theory]
    [InlineData(0, 0, 0, 387U)]
    [InlineData(0, 3, 0, 387U)]
    [InlineData(DefensiveStanceStatusFlag, 3, 0, 193U)]
    [InlineData(DefensiveStanceStatusFlag, 3, 150, 289U)]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag, 3, 0, 0U)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag, 3, 0, 0U)]
    public async Task HandlerHellVortexPlayerRunsOpeningReceiveBeforeOneIncomingPublication(
        int targetStatusFlag,
        int useSpellSoul,
        int infinitySpellPercent,
        uint expectedRequestedDamage)
    {
        var result = await RunHellVortexReceiveEnvelopeAsync(
            targetStatusFlag: targetStatusFlag,
            useSpellSoul: checked((byte)useSpellSoul),
            infinitySpellPercent: infinitySpellPercent);

        var target = Assert.Single(result.Targets);
        // Normal: 602 + 301 = 903; floor(903 * 12 / 28) = 387.
        // Stance: 602 / 2 = 301; 301 + 150 = 451; caller damage = 193.
        // Stance + Infinity: floor(301 * 150 / 100) = 451; +225 = 676; caller = 289.
        Assert.Equal(expectedRequestedDamage, target.RequestedDamage);
        AssertHellVortexReceiveEnvelope(result);
    }

    [Theory]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    public async Task HandlerHellVortexPhysicalStatusReturnPrecedesWildPhoenixAndNormalTailButStillReceives(
        int targetStatusFlag)
    {
        var result = await RunHellVortexReceiveEnvelopeAsync(
            targetStatusFlag: targetStatusFlag,
            defensiveStance: true,
            wildPhoenix: true,
            infinitySpellPercent: 150);

        var target = Assert.Single(result.Targets);
        Assert.Equal(0U, target.RequestedDamage); // identity soul(1) * 12 / 28.
        Assert.Equal("1", target.Session.Character.Attributes[
            RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(target.Session.Character.Attributes, LegacyWildPhoenixFlag));
        Assert.DoesNotContain(
            ParseMsgUpdates(target.Sent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == LegacyWildPhoenixFlag);
        AssertHellVortexReceiveEnvelope(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerHellVortexWildPhoenixReturnPrecedesStanceAndBonusButStillRunsOpeningReceive(
        bool defensiveStance)
    {
        var result = await RunHellVortexReceiveEnvelopeAsync(
            defensiveStance: defensiveStance,
            wildPhoenix: true,
            infinitySpellPercent: 150);

        var target = Assert.Single(result.Targets);
        Assert.Equal(0U, target.RequestedDamage);
        Assert.Equal("0", target.Session.Character.Attributes[
            RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.False(HasLegacyStatusFlag(target.Session.Character.Attributes, LegacyWildPhoenixFlag));
        var phoenixIndex = AssertHellVortexReceiveEnvelopeArchive(
            target.Sent, target.Uid, LegacyWildPhoenixFlag, duration: 32, damage: 18_007);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(target.Sent[phoenixIndex]), out var phoenix));
        Assert.Equal(0U, Assert.Single(phoenix.Updates).SpellLevel);
        var solidIndex = AssertHellVortexReceiveEnvelopeArchive(
            target.Sent, target.Uid, HellVortexReceiveEnvelopeSolidFlag, duration: 5, damage: 2_400);
        Assert.True(phoenixIndex < solidIndex);
        AssertHellVortexReceiveEnvelope(result);
    }

    [Theory]
    [InlineData(49, true)]
    [InlineData(50, false)]
    public async Task HandlerHellVortexReceiveNinjaUsesChanceDividedBy100NotRate(
        int ninjaRoll,
        bool expectedSage)
    {
        var result = await RunHellVortexReceiveEnvelopeAsync(ninjaRoll: ninjaRoll);

        AssertHellVortexReceiveEnvelope(result, expectedSage);
    }

    [Fact]
    public async Task HandlerHellVortexSelectedPlayerFanoutCompletesEachReceiveBeforeNextPlayersHooks()
    {
        var result = await RunHellVortexReceiveEnvelopeAsync(fanout: true);

        Assert.Equal(2, result.TargetOrder.Count);
        Assert.Equal(
            new[] { TargetPlayerUid, HellVortexReceiveEnvelopeBoundaryUid }.OrderBy(uid => uid),
            result.TargetOrder.OrderBy(uid => uid));
        Assert.Equal(847, Assert.Single(result.Targets, target => target.Uid == HellVortexReceiveEnvelopeOutsideUid).OriginalX);
        Assert.Equal(831, Assert.Single(result.Targets, target => target.Uid == HellVortexReceiveEnvelopeCasterNearUid).OriginalX);
        AssertHellVortexReceiveEnvelope(result);

        for (var order = 0; order < result.TargetOrder.Count; order++)
        {
            var uid = result.TargetOrder[order];
            var observation = Assert.Single(result.HookObservations, observed => observed.TargetUid == uid);
            var beforeHit = observation.Sessions[uid];
            Assert.Equal(100_000, beforeHit.Character.MinHitPoints);
            Assert.True(HasLegacyStatusFlag(beforeHit.Character.Attributes, HellVortexReceiveEnvelopeSolidFlag));
            Assert.True(HasLegacyStatusFlag(beforeHit.Character.Attributes, HellVortexReceiveEnvelopeHawksFlag));

            foreach (var previousUid in result.TargetOrder.Take(order))
            {
                var previous = Assert.Single(result.Targets, target => target.Uid == previousUid);
                Assert.Equal(
                    100_000L - previous.AppliedDamage,
                    observation.Sessions[previousUid].Character.MinHitPoints);
                Assert.False(HasLegacyStatusFlag(
                    observation.Sessions[previousUid].Character.Attributes,
                    HellVortexReceiveEnvelopeHawksFlag));
                var hitpointIndices = AssertHellVortexReceiveEnvelopeHitpoints(previous);
                var solidIndex = AssertHellVortexReceiveEnvelopeArchive(
                    Assert.Single(result.Targets, target => target.Uid == uid).Sent,
                    uid, HellVortexReceiveEnvelopeSolidFlag, duration: 5, damage: 2_400);
                var current = Assert.Single(result.Targets, target => target.Uid == uid);
                Assert.True(
                    result.DeliveryOrder.IndexOf(previous.Sent[hitpointIndices[^1]]) <
                    result.DeliveryOrder.IndexOf(current.Sent[solidIndex]));
                Assert.True(HasLegacyStatusFlag(
                    observation.Sessions[PlayerUid].Character.Attributes,
                    HellVortexReceiveEnvelopeSageFlag));
            }

            foreach (var nextUid in result.TargetOrder.Skip(order + 1))
            {
                Assert.Equal(100_000, observation.Sessions[nextUid].Character.MinHitPoints);
                Assert.False(HasLegacyStatusFlag(
                    observation.Sessions[nextUid].Character.Attributes,
                    HellVortexReceiveEnvelopeSolidFlag));
            }
        }

        foreach (var excluded in result.Targets.Where(target => !result.TargetOrder.Contains(target.Uid)))
        {
            Assert.Equal(100_000, excluded.Session.Character.MinHitPoints);
            Assert.False(HasLegacyStatusFlag(excluded.Session.Character.Attributes, HellVortexReceiveEnvelopeSolidFlag));
            Assert.True(HasLegacyStatusFlag(excluded.Session.Character.Attributes, HellVortexReceiveEnvelopeHawksFlag));
            Assert.DoesNotContain(
                ParseMsgUpdates(result.DeliveryOrder),
                update => update.Uid == excluded.Uid && update.Updates.Any(entry =>
                    entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints or
                        LegacyMsgUpdateDataType.ArchiveSkill));
        }
    }

    private static async Task<HellVortexReceiveEnvelopeResult> RunHellVortexReceiveEnvelopeAsync(
        int targetStatusFlag = 0,
        bool defensiveStance = false,
        bool wildPhoenix = false,
        int infinitySpellPercent = 0,
        byte useSpellSoul = 3,
        int ninjaRoll = 0,
        bool fanout = false,
        uint targetHitpoints = 100_000,
        long targetSpirit = 300,
        uint? lightningShieldLeft = null,
        bool attackerNeptuneCurse = false,
        uint? expectedReceiveDamage = null)
    {
        var ninjaValues = Enumerable.Repeat(0, 15).ToArray();
        ninjaValues[0] = 17;
        ninjaValues[5] = 5_000; // Receive uses Chance / 100, not Rate (index 7 remains zero).
        ninjaValues[6] = 10;
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [],
            magicRows:
            [
                CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 0, damage: 200, coldTime: 0, damage3: 30_030),
                CreateMagicRow(
                    WildPhoenixSpellId, sort: 6, useMana: 0, damage: 4, coldTime: 0,
                    duration: 32, damageOnHuman: 18, damage2: 7),
                CreateMagicRow(LegacyLightningShieldSpellId, sort: 0, level: 3, useMana: 0, damage: 0, coldTime: 0),
            ],
            gouyuImmortals:
            [
                new GouyuImmortal(17, ninjaValues, "fixture://gouyu_immortal", 1, "", []),
            ]);
        var casterAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PkMode"] = "0",
            ["LegacyNinjaLevels"] = "17",
            ["LegacyNinjaSageModeRoll"] = ninjaRoll.ToString(CultureInfo.InvariantCulture),
            ["HawksAmbitionPower"] = "25",
        };
        var casterFlags = new List<int>();
        if (infinitySpellPercent > 0)
        {
            casterFlags.Add(InfinityStatusFlag);
            casterAttributes["InfinitySDamage"] = infinitySpellPercent.ToString(CultureInfo.InvariantCulture);
        }
        if (attackerNeptuneCurse)
        {
            casterFlags.Add(LegacyNeptuneCurseFlag);
        }
        casterAttributes["StatusFlags"] = BuildStatusFlags(casterFlags.ToArray());

        var casterSession = CreateSession(
            strength: 301,
            classCode: 5_005,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    HellVortexSpellId, Level: 0, Experience: 0, PreviousLevel: 0,
                    SoulLevel: 3, UseSpellSoul: useSpellSoul),
            ],
            attributes: casterAttributes);
        var targetFlags = new List<int> { HellVortexReceiveEnvelopeHawksFlag };
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
        }
        if (lightningShieldLeft is not null)
        {
            targetFlags.Add(LegacyLightningShieldActivatedFlag);
        }

        var targetXs = fanout ? new[] { 840, 846, 847, 831 } : new[] { 840 };
        var targetSpells = new List<PlayerSpellSnapshotEntry>();
        if (wildPhoenix)
        {
            targetSpells.Add(CreatePlayerSpell(WildPhoenixSpellId));
        }
        if (lightningShieldLeft is not null)
        {
            targetSpells.Add(CreatePlayerSpell(LegacyLightningShieldSpellId, level: 3));
        }
        var originalTargets = targetXs.Select((x, index) => CreateSession(
            uid: TargetPlayerUid + index,
            name: "HellVortexReceiveTarget" + index,
            x: x,
            y: 584,
            minHitPoints: targetHitpoints,
            spirit: targetSpirit,
            spellEntries: targetSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(targetFlags.ToArray()),
                ["MaxHitpoints"] = targetHitpoints.ToString(CultureInfo.InvariantCulture),
                ["LegacySpeffectLevel.Solid"] = "4",
                ["LegacySpeffectRoll.Solid"] = "0",
                ["LegacySpeffectLevel.Bloodthirst"] = "4",
                ["LegacySpeffectRoll.Bloodthirst"] = "0",
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = wildPhoenix ? "1" : "0",
                ["LightningShieldLeft"] = lightningShieldLeft.GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
            })).ToArray();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = originalTargets.Select(_ => new List<byte[]>()).ToArray();
        var deliveryOrder = new List<byte[]>();
        var observations = new List<HellVortexReceiveEnvelopeObservation>();
        var casterConnection = CreateHellVortexReceiveEnvelopeConnection(
            PlayerUid, casterSent, deliveryOrder, sessions, observations);
        var targetConnections = targetSent.Select((sent, index) => CreateHellVortexReceiveEnvelopeConnection(
            checked(TargetPlayerUid + (uint)index), sent, deliveryOrder, sessions, observations)).ToArray();
        sessions.Set(casterConnection, casterSession);
        for (var index = 0; index < originalTargets.Length; index++)
        {
            sessions.Set(targetConnections[index], originalTargets[index]);
        }

        var targetOrder = GetVisibleTargetOrder(sessions, casterConnection, PlayerUid)
            .Where(uid => uid == TargetPlayerUid || uid == HellVortexReceiveEnvelopeBoundaryUid)
            .ToArray();
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells, worldData, HellVortexSpellId, out _, out var spell));
        Assert.Equal(98, (int)spell.Sort);
        var attackerFields = LegacyPlayerStatusCalculator.CalculateFields(
            casterSession.Character, casterSession.PlayerItems, worldData, 5_000, 0);
        Assert.Equal(0U, attackerFields[LegacyPlayerStatusPacket.BreakthroughIndex]);
        uint requestedDamage;
        if (targetStatusFlag is LegacyPhysicalSpellsShurikenVortexFlag or LegacyPhysicalSpellsManiacDanceFlag ||
            wildPhoenix)
        {
            requestedDamage = 0; // Physical.cs returns 1; identity soul(1) * 12 / 28 = 0.
        }
        else
        {
            // Source arithmetic, independent of production damage calculators: 301 * 200 / 100 = 602.
            var damage = 602U;
            if (defensiveStance || targetStatusFlag == DefensiveStanceStatusFlag)
            {
                damage /= 2;
            }
            if (infinitySpellPercent > 0)
            {
                damage = damage * checked((uint)infinitySpellPercent) / 100;
            }
            // Physical.cs: no breakthrough => +50% after stance/Infinity. Soul is identity.
            damage += damage * 50 / 100;
            requestedDamage = damage * 12 / 28;
        }
        var receiveDamage = expectedReceiveDamage ?? (requestedDamage - requestedDamage * 25 / 100);

        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            casterConnection, log, casterSession,
            WorldData: worldData, Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 900, Y: 901,
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                SpellId: HellVortexSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        var targets = originalTargets.Select((original, index) =>
        {
            Assert.True(sessions.TryGet(targetConnections[index], out var finalTarget));
            Assert.NotNull(finalTarget);
            return new HellVortexReceiveEnvelopeTarget(
                checked((uint)original.Character.Uid), finalTarget, targetSent[index], original.Character.X,
                targetHitpoints, requestedDamage, receiveDamage, Math.Min(targetHitpoints, receiveDamage),
                requestedDamage != 0 && (defensiveStance || targetStatusFlag == DefensiveStanceStatusFlag)
                    ? MsgAttackEffect.Block : MsgAttackEffect.None);
        }).ToArray();
        return new HellVortexReceiveEnvelopeResult(
            finalCaster, casterSent, targets, targetOrder, deliveryOrder, observations, useSpellSoul, log.ToString());
    }

    private static ConquerTcpConnection CreateHellVortexReceiveEnvelopeConnection(
        uint recipientUid,
        List<byte[]> sent,
        List<byte[]> deliveryOrder,
        WorldSessionStore sessions,
        List<HellVortexReceiveEnvelopeObservation> observations)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                sent.Add(bytes);
                deliveryOrder.Add(bytes);
                if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == recipientUid &&
                    update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                        (entry.Flag == HellVortexReceiveEnvelopeSolidFlag ||
                         entry.Flag == HellVortexReceiveEnvelopeSageFlag)))
                {
                    observations.Add(new HellVortexReceiveEnvelopeObservation(
                        recipientUid,
                        sessions.Snapshot().ToDictionary(
                            active => checked((uint)active.Session.Character.Uid), active => active.Session)));
                }
                return ValueTask.CompletedTask;
            });
    }

    private static void AssertHellVortexReceiveEnvelope(
        HellVortexReceiveEnvelopeResult result,
        bool expectedSage = true)
    {
        Assert.Equal((830L, 584L), (result.Caster.Character.X, result.Caster.Character.Y));
        Assert.Equal(expectedSage, HasLegacyStatusFlag(result.Caster.Character.Attributes, HellVortexReceiveEnvelopeSageFlag));
        AssertHellVortexReceiveEnvelopeAnimation(result.CasterSent, result);
        var firstReceiveBoundary = int.MaxValue;
        foreach (var target in result.Targets)
        {
            AssertHellVortexReceiveEnvelopeAnimation(target.Sent, result);
            Assert.Equal((target.OriginalX, 584L), (target.Session.Character.X, target.Session.Character.Y));
            if (!result.TargetOrder.Contains(target.Uid))
            {
                continue;
            }

            Assert.Equal((long)target.InitialHitpoints - target.AppliedDamage, target.Session.Character.MinHitPoints);
            Assert.Equal(
                target.Session.Character.MinHitPoints > 0,
                HasLegacyStatusFlag(target.Session.Character.Attributes, HellVortexReceiveEnvelopeSolidFlag));
            Assert.Equal("2400", target.Session.Character.Attributes["Solid"]);
            Assert.False(HasLegacyStatusFlag(target.Session.Character.Attributes, HellVortexReceiveEnvelopeHawksFlag));
            var solidStatusIndex = SingleFrameIndex(
                target.Sent,
                frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == target.Uid &&
                    update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                        entry.Values is not null && IsLegacyStatusFlagSet(entry.Values, HellVortexReceiveEnvelopeSolidFlag) &&
                        IsLegacyStatusFlagSet(entry.Values, HellVortexReceiveEnvelopeHawksFlag)));
            Assert.Equal((ushort)2176, ReadFrame(target.Sent[solidStatusIndex]).Header.Opcode);
            var solidArchiveIndex = AssertHellVortexReceiveEnvelopeArchive(
                target.Sent, target.Uid, HellVortexReceiveEnvelopeSolidFlag, duration: 5, damage: 2_400);
            var hitpointIndices = AssertHellVortexReceiveEnvelopeHitpoints(target);
            var boundaryIndex = hitpointIndices[0];
            Assert.True(solidStatusIndex < boundaryIndex);
            Assert.True(solidArchiveIndex < boundaryIndex);
            firstReceiveBoundary = Math.Min(firstReceiveBoundary, result.DeliveryOrder.IndexOf(target.Sent[boundaryIndex]));
            var observation = Assert.Single(result.HookObservations, observed => observed.TargetUid == target.Uid);
            Assert.Equal((long)target.InitialHitpoints, observation.Sessions[target.Uid].Character.MinHitPoints);
            Assert.True(HasLegacyStatusFlag(
                observation.Sessions[target.Uid].Character.Attributes, HellVortexReceiveEnvelopeHawksFlag));
        }

        if (expectedSage)
        {
            var sageStatusIndex = SingleFrameIndex(
                result.CasterSent,
                frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == PlayerUid &&
                    update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                        entry.Values is not null && IsLegacyStatusFlagSet(entry.Values, HellVortexReceiveEnvelopeSageFlag)));
            Assert.Equal((ushort)2176, ReadFrame(result.CasterSent[sageStatusIndex]).Header.Opcode);
            var sageArchiveIndex = AssertHellVortexReceiveEnvelopeArchive(
                result.CasterSent, PlayerUid, HellVortexReceiveEnvelopeSageFlag, duration: 10, damage: 0);
            Assert.True(result.DeliveryOrder.IndexOf(result.CasterSent[sageStatusIndex]) < firstReceiveBoundary);
            Assert.True(result.DeliveryOrder.IndexOf(result.CasterSent[sageArchiveIndex]) < firstReceiveBoundary);
            var sageObservation = Assert.Single(result.HookObservations, observed => observed.TargetUid == PlayerUid);
            foreach (var uid in result.TargetOrder)
            {
                var target = Assert.Single(result.Targets, candidate => candidate.Uid == uid);
                Assert.Equal((long)target.InitialHitpoints, sageObservation.Sessions[uid].Character.MinHitPoints);
                Assert.True(HasLegacyStatusFlag(
                    sageObservation.Sessions[uid].Character.Attributes, HellVortexReceiveEnvelopeHawksFlag));
            }
            Assert.Contains("Chance / 100", result.Log);
        }
        else
        {
            Assert.DoesNotContain(
                ParseMsgUpdates(result.CasterSent).SelectMany(update => update.Updates),
                entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == HellVortexReceiveEnvelopeSageFlag);
        }

        // Bloodthirst must inspect full pre-hit HP, not heal after the incoming damage.
        Assert.DoesNotContain(
            ParseMsgUpdates(result.DeliveryOrder).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.BloodthirstElan);
    }

    private static int[] AssertHellVortexReceiveEnvelopeHitpoints(
        HellVortexReceiveEnvelopeTarget target,
        bool expectFinalAnimation = true)
    {
        var indices = Enumerable.Range(0, target.Sent.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(target.Sent[index]), out var update) && update.Uid == target.Uid &&
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints))
            .ToArray();
        Assert.All(indices, index => Assert.Equal((ushort)2176, ReadFrame(target.Sent[index]).Header.Opcode));
        if (target.InitialHitpoints > 0 && target.AppliedDamage == target.InitialHitpoints)
        {
            // Source normal Dead: ClearFlags queries BEFORE its final HP0
            // setter. Neutral query has three pairs at the still-live100,
            // followed by the fourth pair(100,0), not a final-zero filter.
            Assert.Equal(8, indices.Length);
            for (var phase = 0; phase < 4; phase++)
            {
                Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(target.Sent[indices[phase * 2]]), out var maximum));
                Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(target.Sent[indices[phase * 2 + 1]]), out var pair));
                Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints], maximum.Updates.Select(entry => entry.Type));
                Assert.Equal(
                    [LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints],
                    pair.Updates.Select(entry => entry.Type));
                Assert.Equal((ulong)target.InitialHitpoints, maximum.Updates[0].Value);
                Assert.Equal((ulong)target.InitialHitpoints, pair.Updates[0].Value);
                Assert.Equal(phase < 3 ? (ulong)target.InitialHitpoints : 0UL, pair.Updates[1].Value);
                Assert.Equal(indices[phase * 2] + 1, indices[phase * 2 + 1]);
            }
            var queryIndex = SingleFrameIndex(target.Sent, frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
            var statusIndex = SingleFrameIndex(target.Sent, frame =>
                LegacyPlayerStatusPacket.TryParse(frame, out var status) && status.Uid == target.Uid);
            Assert.True(queryIndex < indices[0]);
            Assert.True(indices[1] < statusIndex && statusIndex < indices[2]);
        }
        else
        {
            // ReceiveAttack.Player.cs:799 still assigns HP for ordinary zero damage;
            // Role.Player.cs:2613-2628 and 3751-3761 publish exactly one pair even unchanged.
            Assert.Equal(2, indices.Length);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(target.Sent[indices[0]]), out var first));
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(target.Sent[indices[1]]), out var second));
            Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints], first.Updates.Select(entry => entry.Type));
            Assert.Equal(
                [LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints],
                second.Updates.Select(entry => entry.Type));
            Assert.Equal((ulong)target.InitialHitpoints, first.Updates[0].Value);
            Assert.Equal((ulong)target.InitialHitpoints, second.Updates[0].Value);
            Assert.Equal((ulong)(target.InitialHitpoints - target.AppliedDamage), second.Updates[1].Value);
        }
        if (expectFinalAnimation)
        {
            var animationIndex = SingleFrameIndex(
                target.Sent, frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) && animation.SpellId == HellVortexSpellId);
            Assert.All(indices, index => Assert.True(index < animationIndex));
        }
        return indices;
    }

    private static int AssertHellVortexReceiveEnvelopeArchive(
        IReadOnlyList<byte[]> frames,
        uint uid,
        int flag,
        uint duration,
        uint damage)
    {
        var index = SingleFrameIndex(
            frames,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == uid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == flag && entry.Time == duration && entry.Damage == damage));
        Assert.Equal((ushort)2176, ReadFrame(frames[index]).Header.Opcode);
        return index;
    }

    private static void AssertHellVortexReceiveEnvelopeAnimation(
        IReadOnlyList<byte[]> frames,
        HellVortexReceiveEnvelopeResult result)
    {
        var animation = Assert.Single(ParseSpellAnimations(frames));
        var index = SingleFrameIndex(frames, frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        Assert.Equal((ushort)2239, ReadFrame(frames[index]).Header.Opcode);
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((900, 901), (animation.X, animation.Y));
        Assert.Equal(HellVortexSpellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(result.UseSpellSoul, animation.LevelHu);
        Assert.Equal(0, animation.BombFlag);
        Assert.Equal(result.TargetOrder, animation.Targets.Select(target => target.Uid).ToArray());
        foreach (var hit in animation.Targets)
        {
            var target = Assert.Single(result.Targets, candidate => candidate.Uid == hit.Uid);
            // HellVortex.cs:44-47 enqueues the same mutable SpellObj after receive, not capped HP loss.
            Assert.Equal(target.ReceiveDamage, hit.Damage);
            Assert.True(hit.Hit);
            Assert.Equal(target.Effect, hit.Effect);
            Assert.Equal((0, 0), (hit.NewX, hit.NewY));
        }
    }

    private sealed record HellVortexReceiveEnvelopeTarget(
        uint Uid,
        WorldSession Session,
        IReadOnlyList<byte[]> Sent,
        long OriginalX,
        uint InitialHitpoints,
        uint RequestedDamage,
        uint ReceiveDamage,
        uint AppliedDamage,
        MsgAttackEffect Effect);

    private sealed record HellVortexReceiveEnvelopeObservation(
        uint TargetUid,
        IReadOnlyDictionary<uint, WorldSession> Sessions);

    private sealed record HellVortexReceiveEnvelopeResult(
        WorldSession Caster,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<HellVortexReceiveEnvelopeTarget> Targets,
        IReadOnlyList<uint> TargetOrder,
        List<byte[]> DeliveryOrder,
        IReadOnlyList<HellVortexReceiveEnvelopeObservation> HookObservations,
        byte UseSpellSoul,
        string Log);
}
