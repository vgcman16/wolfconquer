using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint PounceRevengeTailDamage = 5_000;
    private const string PounceRevengeTailChangeAttribute = "LegacyRevengeTailChange";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerPounceBackDamageHeavensWonderRunsBeforeIncomingReceiveAndCarriesNestedState(
        bool defensiveStance)
    {
        var result = await RunPounceBackDamageAttackAsync(
            targetStatusFlag: defensiveStance ? DefensiveStanceStatusFlag : null);

        var counter = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Equal(TargetPlayerUid, counter.AttackerUid);
        Assert.Equal(0U, counter.AttackedUid);
        Assert.Equal((0, 0), (counter.X, counter.Y));
        Assert.Equal(
            [result.Monster.Uid, PlayerUid],
            counter.Targets.Select(target => target.Uid).ToArray());
        var counterHit = Assert.Single(counter.Targets, target => target.Uid == PlayerUid);
        Assert.True(counterHit.Damage > 0);
        Assert.Equal(100_000L - counterHit.Damage, result.FinalCaster.Character.MinHitPoints);
        Assert.Equal("50", result.FinalCaster.Character.Attributes["Stamina"]);
        Assert.Equal((840L, 584L), (result.FinalCaster.Character.X, result.FinalCaster.Character.Y));
        Assert.Contains(RuntimeLegacyAttackStampUtcAttribute, result.FinalCaster.Character.Attributes.Keys);
        Assert.Equal(493, result.FinalTarget.Character.MinMana);
        Assert.Equal("95", result.FinalTarget.Character.Attributes["Stamina"]);
        Assert.True(result.FinalTarget.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
        Assert.True(result.MonsterStates.TryGetVisibleInstance(
            result.Monster,
            DateTimeOffset.UtcNow,
            out var damagedMonster));
        Assert.Equal(
            result.Monster.MaxLife - counter.Targets[0].Damage,
            damagedMonster.Hitpoints);

        AssertPounceBackDamageIncomingReceive(
            result,
            defensiveStance ? result.NormalDamage / 2 : result.NormalDamage,
            defensiveStance ? MsgAttackEffect.Block : MsgAttackEffect.None);
        Assert.True(
            PounceBackDamageAnimationIndex(result.TargetSent, HeavensWonderRuntimeSpellId) <
                PounceBackDamageHitpointIndex(result.TargetSent, TargetPlayerUid),
            "Physical.OnPlayer reaches BackDmg before Pounce applies the incoming ReceiveAttack.Player hit.");
    }

    [Fact]
    public async Task HandlerPounceBackDamageMudWallReducesRequestedAnimationAndFinalReceiveExactlyOnce()
    {
        var result = await RunPounceBackDamageAttackAsync(equipMudWallSigil: true);
        var reduction = result.NormalDamage * (PhysicalMudWallSigilPower / 100U) / 100;
        Assert.True(reduction > 0);
        var expectedDamage = result.NormalDamage - reduction;

        var counter = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        var counterHit = Assert.Single(counter.Targets, target => target.Uid == PlayerUid);
        Assert.Equal(100_000L - counterHit.Damage, result.FinalCaster.Character.MinHitPoints);
        Assert.True(result.FinalTarget.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
        Assert.True(HasLegacyStatusFlag(result.FinalTarget.Character.Attributes, MudWallStatusFlag));
        AssertPounceBackDamageIncomingReceive(result, expectedDamage, MsgAttackEffect.None);
        Assert.True(
            PounceBackDamageAnimationIndex(result.TargetSent, HeavensWonderRuntimeSpellId) <
                PounceBackDamageHitpointIndex(result.TargetSent, TargetPlayerUid));
        Assert.NotEqual(
            expectedDamage - expectedDamage * (PhysicalMudWallSigilPower / 100U) / 100,
            expectedDamage);
    }

    [Theory]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    public async Task HandlerPounceBackDamageStatusReturnsBeforeWildPhoenixAndBothCounters(
        int targetStatusFlag)
    {
        var result = await RunPounceBackDamageAttackAsync(
            targetStatusFlag,
            wildPhoenixCharges: 1,
            revengeTailCharges: 2);

        AssertPounceBackDamageNoCounters(result);
        AssertPounceBackDamageIncomingReceive(result, 1, MsgAttackEffect.None);
        Assert.Equal("1", result.FinalTarget.Character.Attributes[
            RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.Equal("2", result.FinalTarget.Character.Attributes[PounceRevengeTailChangeAttribute]);
        Assert.True(HasLegacyStatusFlag(result.FinalTarget.Character.Attributes, RevengeTailStatusFlag));
    }

    [Fact]
    public async Task HandlerPounceBackDamageWildPhoenixReturnsBeforeDefensiveStanceAndBothCounters()
    {
        var result = await RunPounceBackDamageAttackAsync(
            DefensiveStanceStatusFlag,
            wildPhoenixCharges: 1,
            revengeTailCharges: 2);

        AssertPounceBackDamageNoCounters(result);
        AssertPounceBackDamageIncomingReceive(result, 1, MsgAttackEffect.None);
        Assert.Equal("0", result.FinalTarget.Character.Attributes[
            RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.Equal("2", result.FinalTarget.Character.Attributes[PounceRevengeTailChangeAttribute]);
    }

    [Theory]
    [InlineData(1U)]
    [InlineData(2U)]
    public async Task HandlerPounceBackDamageRevengeTailReceivesBeforeBombAndRetainsChargeMutation(
        uint charges)
    {
        var result = await RunPounceBackDamageAttackAsync(
            learnHeavensWonder: false,
            revengeTailCharges: charges);
        Assert.True(result.NormalDamage < PounceRevengeTailDamage);

        var counter = Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == RevengeTailAttachStatusSpellId);
        Assert.Equal(TargetPlayerUid, counter.AttackerUid);
        Assert.Equal(0U, counter.AttackedUid);
        Assert.Equal((842, 584), (counter.X, counter.Y));
        Assert.Equal(1, counter.BombFlag);
        var counterHit = Assert.Single(counter.Targets);
        Assert.Equal(PlayerUid, counterHit.Uid);
        Assert.Equal(PounceRevengeTailDamage, counterHit.Damage);
        Assert.True(counterHit.Hit);
        Assert.Equal(100_000L - PounceRevengeTailDamage, result.FinalCaster.Character.MinHitPoints);
        Assert.Equal(
            (charges - 1).ToString(CultureInfo.InvariantCulture),
            result.FinalTarget.Character.Attributes[PounceRevengeTailChangeAttribute]);
        Assert.Equal(
            charges > 1,
            HasLegacyStatusFlag(result.FinalTarget.Character.Attributes, RevengeTailStatusFlag));
        Assert.Equal("50", result.FinalCaster.Character.Attributes["Stamina"]);
        Assert.Equal((840L, 584L), (result.FinalCaster.Character.X, result.FinalCaster.Character.Y));
        AssertPounceBackDamageIncomingReceive(result, result.NormalDamage, MsgAttackEffect.None);
        Assert.True(
            PounceBackDamageHitpointIndex(result.CasterSent, PlayerUid) <
                PounceBackDamageAnimationIndex(result.CasterSent, RevengeTailAttachStatusSpellId),
            "BackDmg receives RevengeTail counter damage before publishing the defender's bomb animation.");
        Assert.True(
            PounceBackDamageAnimationIndex(result.TargetSent, RevengeTailAttachStatusSpellId) <
                PounceBackDamageHitpointIndex(result.TargetSent, TargetPlayerUid));
    }

    [Fact]
    public async Task HandlerPounceBackDamageHeavensWonderThenRevengeTailCarryBothNestedMutations()
    {
        var result = await RunPounceBackDamageAttackAsync(revengeTailCharges: 2);
        var wonder = Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        var wonderHit = Assert.Single(wonder.Targets, target => target.Uid == PlayerUid);
        Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == RevengeTailAttachStatusSpellId);
        Assert.Equal(
            100_000L - wonderHit.Damage - PounceRevengeTailDamage,
            result.FinalCaster.Character.MinHitPoints);
        Assert.True(result.FinalTarget.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
        Assert.Equal("95", result.FinalTarget.Character.Attributes["Stamina"]);
        Assert.Equal(493, result.FinalTarget.Character.MinMana);
        Assert.Equal("1", result.FinalTarget.Character.Attributes[PounceRevengeTailChangeAttribute]);
        Assert.True(HasLegacyStatusFlag(result.FinalTarget.Character.Attributes, RevengeTailStatusFlag));
        AssertPounceBackDamageIncomingReceive(result, result.NormalDamage, MsgAttackEffect.None);
        Assert.True(
            PounceBackDamageAnimationIndex(result.TargetSent, HeavensWonderRuntimeSpellId) <
                PounceBackDamageAnimationIndex(result.TargetSent, RevengeTailAttachStatusSpellId));
        Assert.True(
            PounceBackDamageAnimationIndex(result.TargetSent, RevengeTailAttachStatusSpellId) <
                PounceBackDamageHitpointIndex(result.TargetSent, TargetPlayerUid));
    }

    [Fact]
    public async Task HandlerPounceBackDamageRevengeTailUsesStrictIncomingDamageThreshold()
    {
        var baseline = await RunPounceBackDamageAttackAsync(learnHeavensWonder: false);
        var result = await RunPounceBackDamageAttackAsync(
            learnHeavensWonder: false,
            revengeTailCharges: 1,
            revengeTailDamage: baseline.NormalDamage);

        AssertPounceBackDamageNoCounters(result);
        AssertPounceBackDamageIncomingReceive(result, baseline.NormalDamage, MsgAttackEffect.None);
        Assert.Equal("1", result.FinalTarget.Character.Attributes[PounceRevengeTailChangeAttribute]);
        Assert.True(HasLegacyStatusFlag(result.FinalTarget.Character.Attributes, RevengeTailStatusFlag));
    }

    private static void AssertPounceBackDamageIncomingReceive(
        PounceBackDamageResult result,
        uint expectedDamage,
        MsgAttackEffect expectedEffect)
    {
        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == PounceRuntimeSpellId);
        AssertPounceSourceAnimation(incoming, 840, 584, expectedSoul: 3);
        var hit = Assert.Single(incoming.Targets);
        Assert.Equal(TargetPlayerUid, hit.Uid);
        Assert.Equal(expectedDamage, hit.Damage);
        Assert.Equal(expectedEffect, hit.Effect);
        Assert.True(hit.Hit);
        Assert.Equal(100_000L - expectedDamage, result.FinalTarget.Character.MinHitPoints);
        var hpIndices = Enumerable.Range(0, result.TargetSent.Count)
            .Where(index => LegacyMsgUpdatePacket.TryParse(ReadFrame(result.TargetSent[index]), out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type is
                    LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints))
            .ToArray();
        Assert.Equal(2, hpIndices.Length);
        Assert.All(hpIndices, index =>
            Assert.Equal((ushort)2176, ReadFrame(result.TargetSent[index]).Header.Opcode));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.TargetSent[hpIndices[0]]), out var maxHp));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.TargetSent[hpIndices[1]]), out var hp));
        Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints], maxHp.Updates.Select(entry => entry.Type));
        Assert.Equal(
            [LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints],
            hp.Updates.Select(entry => entry.Type));
        Assert.Equal(maxHp.Updates[0].Value, hp.Updates[0].Value);
        Assert.Equal(
            checked((ulong)result.FinalTarget.Character.MinHitPoints),
            Assert.Single(hp.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).Value);
        Assert.True(
            hpIndices[0] < hpIndices[1]);
        Assert.True(
            hpIndices[1] < PounceBackDamageAnimationIndex(result.TargetSent, PounceRuntimeSpellId),
            "Pounce receives and publishes one incoming player HP change before its shared animation.");
        Assert.Equal(
            (ushort)2239,
            ReadFrame(result.TargetSent[PounceBackDamageAnimationIndex(result.TargetSent, PounceRuntimeSpellId)])
                .Header.Opcode);
    }

    private static void AssertPounceBackDamageNoCounters(PounceBackDamageResult result)
    {
        Assert.DoesNotContain(
            ParseSpellAnimations(result.CasterSent.Concat(result.TargetSent)),
            animation => animation.SpellId is HeavensWonderRuntimeSpellId or RevengeTailAttachStatusSpellId);
        Assert.False(result.FinalTarget.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
        Assert.Equal(100_000, result.FinalCaster.Character.MinHitPoints);
        Assert.Equal(500, result.FinalTarget.Character.MinMana);
        Assert.Equal("100", result.FinalTarget.Character.Attributes["Stamina"]);
    }

    private static int PounceBackDamageAnimationIndex(IReadOnlyList<byte[]> frames, ushort spellId)
    {
        return SingleFrameIndex(
            frames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == spellId);
    }

    private static int PounceBackDamageHitpointIndex(IReadOnlyList<byte[]> frames, uint uid)
    {
        return SingleFrameIndex(
            frames,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == uid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
    }

    private static async Task<PounceBackDamageResult> RunPounceBackDamageAttackAsync(
        int? targetStatusFlag = null,
        uint wildPhoenixCharges = 0,
        bool learnHeavensWonder = true,
        uint revengeTailCharges = 0,
        uint revengeTailDamage = PounceRevengeTailDamage,
        bool equipMudWallSigil = false)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        // Outside Pounce's shifted radius, inside the defender's Heaven's Wonder radius.
        var spawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 1,
            boundX: 847,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreatePounceRuntimeRow(),
            CreateCounterKillRuntimeRow(HeavensWonderRuntimeSpellId, coolDown: 0, coldTime: 1_000),
            CreateMagicRow(
                RevengeTailAttachStatusSpellId,
                sort: 6,
                useMana: 0,
                damage: revengeTailDamage,
                coldTime: 0),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        if (equipMudWallSigil)
        {
            AddPhysicalMudWallGouyuRow(
                worldData,
                CreatePhysicalMudWallGouyuRow(
                    PhysicalMudWallSigilType,
                    PhysicalMudWallSigilLevel,
                    PhysicalMudWallSigilPower));
        }

        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var targetAttributes = new Dictionary<string, string>();
        var targetStatusFlags = new List<int>();
        var targetSpellIds = new List<ushort>();
        if (targetStatusFlag is { } statusFlag)
        {
            targetStatusFlags.Add(statusFlag);
        }

        if (equipMudWallSigil)
        {
            targetStatusFlags.Add(MudWallStatusFlag);
            targetAttributes["MsgNinja"] = BuildPhysicalMudWallMsgNinja(
                PhysicalMudWallSigilType,
                PhysicalMudWallSigilLevel,
                position: 1);
        }

        if (learnHeavensWonder)
        {
            targetSpellIds.Add(HeavensWonderRuntimeSpellId);
            targetAttributes[CounterKill16350BackDamageRollAttribute] = "0";
        }

        if (revengeTailCharges > 0)
        {
            targetSpellIds.Add(RevengeTailAttachStatusSpellId);
            targetStatusFlags.Add(RevengeTailStatusFlag);
            targetAttributes[PounceRevengeTailChangeAttribute] =
                revengeTailCharges.ToString(CultureInfo.InvariantCulture);
        }

        if (wildPhoenixCharges > 0)
        {
            targetSpellIds.Add(WildPhoenixSpellId);
            targetStatusFlags.Add(LegacyWildPhoenixFlag);
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] =
                wildPhoenixCharges.ToString(CultureInfo.InvariantCulture);
        }

        if (targetStatusFlags.Count > 0)
        {
            targetAttributes["StatusFlags"] = BuildStatusFlags(targetStatusFlags.ToArray());
        }

        var caster = CreateSession(
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    PounceRuntimeSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 3,
                    UseSpellSoul: 3),
            ],
            playerItems: CreatePounceEquipment(mainShield: true, alternateShield: false));
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "PounceBackDamageTarget",
            x: 842,
            y: 584,
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: targetSpellIds,
            attributes: targetAttributes,
            classCode: equipMudWallSigil ? 5_005 : 142);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            caster.PlayerSpells,
            worldData,
            PounceRuntimeSpellId,
            out _,
            out var spell));
        var shiftedCaster = caster with { Character = caster.Character with { X = 840, Y = 584 } };
        var normalDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            shiftedCaster,
            target,
            spell,
            worldData,
            1U);
        Assert.True(normalDamage > 1);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePounceRuntimeQuery(840, 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        return new PounceBackDamageResult(
            monster,
            monsterStates,
            casterSent,
            targetSent,
            normalDamage,
            finalCaster,
            finalTarget);
    }

    private sealed record PounceBackDamageResult(
        LegacyMonsterInstance Monster,
        MonsterRuntimeStateStore MonsterStates,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        uint NormalDamage,
        WorldSession FinalCaster,
        WorldSession FinalTarget);
}
