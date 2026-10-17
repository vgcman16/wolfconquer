using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint HellVortexBackDamageRevengeDamage = 5_000;
    private const string HellVortexBackDamageRevengeChanges = "LegacyRevengeTailChange";

    [Theory]
    [InlineData(false, 0U)]
    [InlineData(true, 0U)]
    [InlineData(true, 150U)]
    public async Task HandlerHellVortexBackDamageCountersPrecedeCallerScalingAndRetainNestedState(
        bool defensiveStance,
        uint infinityPercent)
    {
        var result = await RunHellVortexBackDamageAsync(
            defensiveStance: defensiveStance,
            infinityPercent: infinityPercent,
            revengeCharges: 2);
        var wonder = Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Equal(TargetPlayerUid, wonder.AttackerUid);
        Assert.Equal(0U, wonder.AttackedUid);
        Assert.Equal((0, 0), (wonder.X, wonder.Y));
        var wonderHit = Assert.Single(wonder.Targets);
        Assert.Equal(PlayerUid, wonderHit.Uid);
        Assert.True(wonderHit.Damage > 0);

        var revenge = Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == RevengeTailAttachStatusSpellId);
        Assert.Equal(TargetPlayerUid, revenge.AttackerUid);
        Assert.Equal(0U, revenge.AttackedUid);
        Assert.Equal((842, 584), (revenge.X, revenge.Y));
        Assert.Equal(1, revenge.BombFlag);
        var revengeHit = Assert.Single(revenge.Targets);
        Assert.Equal(PlayerUid, revengeHit.Uid);
        Assert.Equal(HellVortexBackDamageRevengeDamage, revengeHit.Damage);
        Assert.True(revengeHit.Hit);
        Assert.Equal(
            100_000L - wonderHit.Damage - HellVortexBackDamageRevengeDamage,
            result.Caster.Character.MinHitPoints);
        Assert.Equal(493, result.Caster.Character.MinMana);
        Assert.Equal("89", result.Caster.Character.Attributes["Stamina"]);
        Assert.Equal((840L, 584L), (result.Caster.Character.X, result.Caster.Character.Y));
        Assert.Equal(493, result.Target.Character.MinMana);
        Assert.Equal("95", result.Target.Character.Attributes["Stamina"]);
        Assert.True(result.Target.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
        Assert.Equal("1", result.Target.Character.Attributes[HellVortexBackDamageRevengeChanges]);
        Assert.True(HasLegacyStatusFlag(result.Target.Character.Attributes, RevengeTailStatusFlag));

        AssertHellVortexBackDamageIncoming(result, defensiveStance ? MsgAttackEffect.Block : MsgAttackEffect.None);
        var wonderIndex = HellVortexBackDamageAnimationIndex(result.TargetSent, HeavensWonderRuntimeSpellId);
        var revengeIndex = HellVortexBackDamageAnimationIndex(result.TargetSent, RevengeTailAttachStatusSpellId);
        var incomingHp = Assert.Single(HellVortexBackDamageHitpointIndices(result.TargetSent, TargetPlayerUid));
        Assert.True(wonderIndex < revengeIndex);
        Assert.True(revengeIndex < incomingHp,
            "Physical.cs:979 and BackDmg.cs:20-62 finish nested retaliation before HellVortex.cs:45-46 scales/receives.");
        var casterHp = HellVortexBackDamageHitpointIndices(result.CasterSent, PlayerUid);
        Assert.Equal(2, casterHp.Length);
        Assert.True(casterHp[0] < HellVortexBackDamageAnimationIndex(result.CasterSent, HeavensWonderRuntimeSpellId));
        Assert.True(casterHp[1] < HellVortexBackDamageAnimationIndex(result.CasterSent, RevengeTailAttachStatusSpellId));
    }

    [Theory]
    [InlineData(0U, false)]
    [InlineData(1U, true)]
    public async Task HandlerHellVortexBackDamageRevengeTailUsesStrictUnscaledPhysicalThreshold(
        uint thresholdDelta,
        bool shouldRetaliate)
    {
        var baseline = await RunHellVortexBackDamageAsync(learnWonder: false);
        var threshold = baseline.PhysicalTailDamage + thresholdDelta;
        Assert.True(baseline.IncomingDamage < threshold);
        var result = await RunHellVortexBackDamageAsync(
            learnWonder: false,
            revengeCharges: 1,
            revengeDamage: threshold);

        AssertHellVortexBackDamageIncoming(result, MsgAttackEffect.None);
        var counters = ParseSpellAnimations(result.CasterSent)
            .Where(animation => animation.SpellId == RevengeTailAttachStatusSpellId)
            .ToArray();
        if (shouldRetaliate)
        {
            var hit = Assert.Single(Assert.Single(counters).Targets);
            Assert.Equal(PlayerUid, hit.Uid);
            Assert.Equal(threshold, hit.Damage);
            Assert.Equal(100_000L - threshold, result.Caster.Character.MinHitPoints);
            Assert.Equal("0", result.Target.Character.Attributes[HellVortexBackDamageRevengeChanges]);
            Assert.False(HasLegacyStatusFlag(result.Target.Character.Attributes, RevengeTailStatusFlag));
            Assert.True(
                Assert.Single(HellVortexBackDamageHitpointIndices(result.CasterSent, PlayerUid)) <
                HellVortexBackDamageAnimationIndex(result.CasterSent, RevengeTailAttachStatusSpellId));
        }
        else
        {
            Assert.Empty(counters);
            Assert.Empty(HellVortexBackDamageHitpointIndices(result.CasterSent, PlayerUid));
            Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
            Assert.Equal("1", result.Target.Character.Attributes[HellVortexBackDamageRevengeChanges]);
            Assert.True(HasLegacyStatusFlag(result.Target.Character.Attributes, RevengeTailStatusFlag));
        }
    }

    private static void AssertHellVortexBackDamageIncoming(
        HellVortexBackDamageResult result,
        MsgAttackEffect expectedEffect)
    {
        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            var incoming = Assert.Single(
                ParseSpellAnimations(frames),
                animation => animation.SpellId == HellVortexSpellId);
            AssertHellVortexRetaliationAnimation(incoming, expectedSoul: 3);
            var hit = Assert.Single(incoming.Targets);
            Assert.Equal(TargetPlayerUid, hit.Uid);
            Assert.Equal(result.IncomingDamage, hit.Damage);
            Assert.Equal(expectedEffect, hit.Effect);
            Assert.True(hit.Hit);
            Assert.Equal((0, 0), (hit.NewX, hit.NewY));
            Assert.Equal((ushort)2239,
                ReadFrame(frames[HellVortexBackDamageAnimationIndex(frames, HellVortexSpellId)]).Header.Opcode);
        }

        Assert.Equal(100_000L - result.IncomingDamage, result.Target.Character.MinHitPoints);
        var hpIndex = Assert.Single(HellVortexBackDamageHitpointIndices(result.TargetSent, TargetPlayerUid));
        Assert.Equal((ushort)2176, ReadFrame(result.TargetSent[hpIndex]).Header.Opcode);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.TargetSent[hpIndex]), out var hp));
        Assert.Equal(
            checked((ulong)result.Target.Character.MinHitPoints),
            Assert.Single(hp.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).Value);
        Assert.True(hpIndex < HellVortexBackDamageAnimationIndex(result.TargetSent, HellVortexSpellId));
    }

    private static void AssertHellVortexRetaliationAnimation(
        LegacySpellAnimationPacket animation,
        byte expectedSoul)
    {
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(HellVortexSpellId, animation.SpellId);
        Assert.Equal((900, 901), (animation.X, animation.Y));
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(expectedSoul, animation.LevelHu);
        Assert.Equal(0, animation.BombFlag);
    }

    [Theory]
    [InlineData(1U)]
    [InlineData(2U)]
    public async Task HandlerHellVortexLethalWonderStillConsumesFollowingRevengeTailCharge(uint charges)
    {
        var result = await RunHellVortexBackDamageAsync(
            revengeCharges: charges,
            casterHitpoints: 1);

        var wonder = Assert.Single(ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.True(Assert.Single(wonder.Targets, hit => hit.Uid == PlayerUid).Damage > 1);
        var revenge = Assert.Single(ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == RevengeTailAttachStatusSpellId);
        Assert.Equal(1, revenge.BombFlag);
        Assert.Equal(TargetPlayerUid, revenge.AttackerUid);
        Assert.Equal((842, 584), (revenge.X, revenge.Y));
        var revengeHit = Assert.Single(revenge.Targets);
        Assert.Equal(PlayerUid, revengeHit.Uid);
        Assert.Equal(HellVortexBackDamageRevengeDamage, revengeHit.Damage);
        Assert.Equal((charges - 1).ToString(CultureInfo.InvariantCulture),
            result.Target.Character.Attributes[HellVortexBackDamageRevengeChanges]);
        Assert.Equal(charges > 1, HasLegacyStatusFlag(result.Target.Character.Attributes, RevengeTailStatusFlag));
        Assert.Equal(0, result.Caster.Character.MinHitPoints);
        Assert.True(HellVortexBackDamageAnimationIndex(result.CasterSent, HeavensWonderRuntimeSpellId) <
            HellVortexBackDamageAnimationIndex(result.CasterSent, RevengeTailAttachStatusSpellId));
        Assert.True(HellVortexBackDamageAnimationIndex(result.CasterSent, RevengeTailAttachStatusSpellId) <
            HellVortexBackDamageAnimationIndex(result.CasterSent, HellVortexSpellId));
    }

    private static int HellVortexBackDamageAnimationIndex(IReadOnlyList<byte[]> frames, ushort spellId)
    {
        var index = SingleFrameIndex(frames, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var animation) && animation.SpellId == spellId);
        Assert.Equal((ushort)2239, ReadFrame(frames[index]).Header.Opcode);
        return index;
    }

    private static int[] HellVortexBackDamageHitpointIndices(IReadOnlyList<byte[]> frames, uint uid)
    {
        var indices = Enumerable.Range(0, frames.Count)
            .Where(index => LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
                update.Uid == uid && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints))
            .ToArray();
        Assert.All(indices, index => Assert.Equal((ushort)2176, ReadFrame(frames[index]).Header.Opcode));
        return indices;
    }

    private static MagicType CreateHellVortexRetaliationRow()
    {
        return CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 7, damage: 200,
            useStamina: 11, coldTime: 0, damage3: 30_030);
    }

    private static MsgAttackQuery CreateHellVortexRetaliationQuery(uint selectedUid)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: selectedUid,
            X: 900,
            Y: 901,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: HellVortexSpellId);
    }

    private static uint CalculateHellVortexRetaliationPhysicalTail(
        WorldSession caster,
        WorldSession target,
        WorldDataCatalog worldData,
        bool defensiveStance = false,
        uint infinityPercent = 0)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            caster.PlayerSpells, worldData, HellVortexSpellId, out _, out var spell));
        var damage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage", caster, target, spell, worldData, 1U);
        // No equipment/chi: no breakthrough. Physical.cs:913-934 applies stance,
        // then Infinity, then the nonpenetrating +50%, before BackDmg at :979.
        if (defensiveStance)
        {
            damage /= 2;
        }
        if (infinityPercent > 0)
        {
            damage = damage * infinityPercent / 100;
        }
        return damage + damage * 50 / 100;
    }

    private static async Task<HellVortexBackDamageResult> RunHellVortexBackDamageAsync(
        bool defensiveStance = false,
        uint infinityPercent = 0,
        bool learnWonder = true,
        uint revengeCharges = 0,
        uint revengeDamage = HellVortexBackDamageRevengeDamage,
        uint casterHitpoints = 100_000)
    {
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [],
            CreateHellVortexRetaliationRow(),
            CreateCounterKillRuntimeRow(HeavensWonderRuntimeSpellId, coolDown: 0, coldTime: 1_000),
            CreateMagicRow(RevengeTailAttachStatusSpellId, sort: 6, useMana: 0, damage: revengeDamage, coldTime: 0));
        var flags = new List<int>();
        var spells = new List<ushort>();
        var attributes = new Dictionary<string, string>();
        if (defensiveStance)
        {
            flags.Add(DefensiveStanceStatusFlag);
        }
        if (learnWonder)
        {
            spells.Add(HeavensWonderRuntimeSpellId);
            attributes[CounterKill16350BackDamageRollAttribute] = "0";
        }
        if (revengeCharges > 0)
        {
            spells.Add(RevengeTailAttachStatusSpellId);
            flags.Add(RevengeTailStatusFlag);
            attributes[HellVortexBackDamageRevengeChanges] = revengeCharges.ToString(CultureInfo.InvariantCulture);
        }
        attributes["StatusFlags"] = BuildStatusFlags(flags.ToArray());
        var casterAttributes = new Dictionary<string, string>();
        if (infinityPercent > 0)
        {
            casterAttributes["StatusFlags"] = BuildStatusFlags(InfinityStatusFlag);
            casterAttributes["InfinitySDamage"] = infinityPercent.ToString(CultureInfo.InvariantCulture);
        }
        var caster = CreateSession(x: 840, minHitPoints: casterHitpoints, strength: 1_000, stamina: 100,
            spellEntries: [CreatePlayerSpell(HellVortexSpellId) with { SoulLevel = 3, UseSpellSoul = 3 }],
            attributes: casterAttributes);
        var target = CreateSession(uid: TargetPlayerUid, name: "HellVortexBackDamageTarget", x: 842,
            minHitPoints: 100_000, strength: 1_000, stamina: 100, spellIds: spells, attributes: attributes);
        Assert.Empty(caster.PlayerItems.EquipmentItems);
        var physicalTail = CalculateHellVortexRetaliationPhysicalTail(
            caster, target, worldData, defensiveStance, infinityPercent);
        Assert.True(physicalTail > 1);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(casterConnection, log, caster,
            WorldData: worldData, Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(), SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(CreateHellVortexRetaliationQuery(TargetPlayerUid)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        return new(casterSent, targetSent, physicalTail, physicalTail * 12 / 28, finalCaster, finalTarget);
    }

    private sealed record HellVortexBackDamageResult(
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        uint PhysicalTailDamage,
        uint IncomingDamage,
        WorldSession Caster,
        WorldSession Target);
}
