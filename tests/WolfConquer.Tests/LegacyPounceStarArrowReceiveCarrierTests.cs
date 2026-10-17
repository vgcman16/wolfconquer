using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false, 20, 10, 100, 0, 100)]
    [InlineData(true, 20, 10, 100, 0, 100)]
    [InlineData(false, 10, 0, 100, 0, 100)]
    [InlineData(true, 10, 0, 100, 0, 100)]
    [InlineData(false, 6, 0, 100, 4, 96)]
    [InlineData(true, 6, 0, 100, 4, 96)]
    [InlineData(false, 6, 0, 3, 4, 0)]
    [InlineData(true, 6, 0, 3, 4, 0)]
    public async Task HandlerPounceAndStarArrowD13AnimationUsesShieldAdjustedUncappedReceiveDamage(
        bool starArrow,
        int shieldLeft,
        int expectedShieldLeft,
        int startingHitpoints,
        int expectedAnimationDamage,
        int expectedHitpoints)
    {
        // Physical.cs:157-160 / Range.cs:100-103 return 1; Base.cs:280-282
        // leaves soul unchanged. ReceiveAttack/Player.cs:97-99 makes it 10,
        // then :214-224 subtracts the shield without capping to HP loss.
        var result = await RunPounceStarArrowD13ReceiveCarrierAsync(
            starArrow,
            startingHitpoints: startingHitpoints,
            shieldLeft: shieldLeft);

        Assert.Equal(expectedHitpoints, result.Target.Character.MinHitPoints);
        Assert.Equal(
            expectedShieldLeft.ToString(CultureInfo.InvariantCulture),
            result.Target.Character.Attributes["LightningShieldLeft"]);
        var shieldIndex = AssertPounceStarArrowD13ShieldDrain(result, checked((uint)expectedShieldLeft));
        var hpIndices = AssertPounceStarArrowD13HitpointPair(result, checked((uint)expectedHitpoints));
        Assert.True(shieldIndex < hpIndices[0]);
        AssertPounceStarArrowD13ReceiveAnimation(result, checked((uint)expectedAnimationDamage));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerPounceAndStarArrowD13OrdinaryZeroPublishesOneUnchangedHitpointPair(bool starArrow)
    {
        var result = await RunPounceStarArrowD13ReceiveCarrierAsync(starArrow, hookMoonPower: 0);

        // ReceiveAttack/Player.cs:799 assigns HP even when damage is zero;
        // Role/Player.cs:2613-2629 publishes that unchanged assignment.
        Assert.Equal(100, result.Target.Character.MinHitPoints);
        AssertPounceStarArrowD13HitpointPair(result, expectedHitpoints: 100);
        Assert.DoesNotContain(ParseMsgUpdates(result.TargetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.AzureShield);
        AssertPounceStarArrowD13ReceiveAnimation(result, expectedDamage: 0);
    }

    [Theory]
    [InlineData(false, 6, 0, 100, 99, 1)]
    [InlineData(true, 6, 0, 100, 99, 1)]
    [InlineData(false, 20, 10, 100, 99, 1)]
    [InlineData(true, 20, 10, 100, 99, 1)]
    [InlineData(false, 6, 0, 3, 4, 0)]
    [InlineData(true, 6, 0, 3, 4, 0)]
    public async Task HandlerPounceAndStarArrowD13AttackerNeptuneUsesPostShieldDamageBeforeHpCap(
        bool starArrow,
        int shieldLeft,
        int expectedShieldLeft,
        int startingHitpoints,
        int expectedAnimationDamage,
        int expectedHitpoints)
    {
        var result = await RunPounceStarArrowD13ReceiveCarrierAsync(
            starArrow,
            startingHitpoints: startingHitpoints,
            shieldLeft: shieldLeft,
            attackerNeptuneCurse: true);

        // Player.cs:561-563 replaces post-shield 4 (or 0) with HP-1 only
        // when it is below current HP; overkill 4 remains 4, not applied HP 3.
        Assert.Equal(expectedHitpoints, result.Target.Character.MinHitPoints);
        Assert.Equal(
            expectedShieldLeft.ToString(CultureInfo.InvariantCulture),
            result.Target.Character.Attributes["LightningShieldLeft"]);
        var shieldIndex = AssertPounceStarArrowD13ShieldDrain(result, checked((uint)expectedShieldLeft));
        var hpIndices = AssertPounceStarArrowD13HitpointPair(result, checked((uint)expectedHitpoints));
        Assert.True(shieldIndex < hpIndices[0]);
        AssertPounceStarArrowD13ReceiveAnimation(result, checked((uint)expectedAnimationDamage));
    }

    private static async Task<PounceStarArrowD13ReceiveCarrierResult> RunPounceStarArrowD13ReceiveCarrierAsync(
        bool starArrow,
        int startingHitpoints = 100,
        int shieldLeft = 0,
        uint hookMoonPower = 1_000,
        bool attackerNeptuneCurse = false)
    {
        var spellId = starArrow ? StarArrowRuntimeSpellId : PounceRuntimeSpellId;
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [],
            magicRows:
            [
                starArrow ? CreateStarArrowRuntimeRow(range: 2) : CreatePounceRuntimeRow(),
                CreateMagicRow(LegacyLightningShieldSpellId, sort: 0, level: 3, useMana: 0, damage: 0, coldTime: 0),
            ]);
        var casterFlags = new List<int> { LegacyHookMoonCasterFlag };
        if (attackerNeptuneCurse)
        {
            casterFlags.Add(LegacyNeptuneCurseFlag);
        }

        var caster = CreateSession(
            stamina: 100,
            spellEntries: [new PlayerSpellSnapshotEntry(spellId, 0, 0, 0, 3, 3)],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
                ["StatusFlags"] = BuildStatusFlags(casterFlags.ToArray()),
                ["HookMoonPower"] = hookMoonPower.ToString(CultureInfo.InvariantCulture),
            },
            playerItems: starArrow
                ? PlayerItemSnapshot.Empty
                : CreatePounceEquipment(mainShield: true, alternateShield: false));
        var targetFlags = new List<int> { LegacyPhysicalSpellsShurikenVortexFlag };
        var targetAttributes = new Dictionary<string, string>
        {
            ["MaxHitpoints"] = "100",
        };
        if (shieldLeft > 0)
        {
            targetFlags.Add(LegacyLightningShieldActivatedFlag);
            targetAttributes["LightningShieldLeft"] = shieldLeft.ToString(CultureInfo.InvariantCulture);
        }

        targetAttributes["StatusFlags"] = BuildStatusFlags(targetFlags.ToArray());
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "ReceiveCarrierTarget",
            x: starArrow ? 836 : 842,
            y: 584,
            minHitPoints: startingHitpoints,
            spirit: 0,
            spellEntries: [CreatePlayerSpell(LegacyLightningShieldSpellId, level: 3)],
            attributes: targetAttributes);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var deliveryOrder = new List<byte[]>();
        var casterConnection = CreatePounceReceiveEnvelopeConnection(casterSent, deliveryOrder);
        var targetConnection = CreatePounceReceiveEnvelopeConnection(targetSent, deliveryOrder);
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        if (starArrow)
        {
            var (playerSpell, spell) = ResolveStarArrowRuntimeSpell(caster, worldData, spellId);
            Assert.True(await InvokeLegacyStarArrowAsync(
                context,
                caster,
                CreateStarArrowRuntimeQuery(spellId, TargetPlayerUid),
                playerSpell,
                spell));
        }
        else
        {
            await PacketHandlers.HandleAsync(context,
                ReadFrame(new MsgAttackPacket(CreatePounceRuntimeQuery(x: 840, y: 584)).ToFrameBytes()));
        }

        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        Assert.Equal(starArrow ? (830L, 584L) : (840L, 584L),
            (finalCaster.Character.X, finalCaster.Character.Y));
        Assert.Equal(starArrow ? (830L, 584L) : (842L, 584L),
            (finalTarget.Character.X, finalTarget.Character.Y));
        return new PounceStarArrowD13ReceiveCarrierResult(
            starArrow, finalCaster, finalTarget, casterSent, targetSent, deliveryOrder);
    }

    private static int AssertPounceStarArrowD13ShieldDrain(
        PounceStarArrowD13ReceiveCarrierResult result,
        uint expectedShieldLeft)
    {
        var index = SingleFrameIndex(result.TargetSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var parsedUpdate) &&
            parsedUpdate.Uid == TargetPlayerUid &&
            parsedUpdate.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield));
        var shieldFrame = ReadFrame(result.TargetSent[index]);
        Assert.Equal((ushort)2176, shieldFrame.Header.Opcode);
        Assert.True(LegacyMsgUpdatePacket.TryParse(shieldFrame, out var update));
        var drainEntry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.AzureShield, drainEntry.Type);
        Assert.Equal(0UL, drainEntry.Value);
        Assert.Equal((uint)LegacyLightningShieldActivatedFlag, drainEntry.Flag);
        Assert.Equal(expectedShieldLeft, drainEntry.Damage);
        Assert.Equal(3U, drainEntry.SpellLevel);
        Assert.InRange(drainEntry.Time, 0U, 10U);
        return index;
    }

    private static int[] AssertPounceStarArrowD13HitpointPair(
        PounceStarArrowD13ReceiveCarrierResult result,
        uint expectedHitpoints)
    {
        var frames = result.TargetSent;
        var indices = Enumerable.Range(0, frames.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
            update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type is
                LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        if (expectedHitpoints == 0)
        {
            indices = AssertExistingNormalDeathCombatPhases(frames, TargetPlayerUid,
                (100, 3), (100, 3), (100, 3), (100, 0));
        }
        else
        {
            Assert.Equal(2, indices.Length);
            Assert.All(indices, index => Assert.Equal((ushort)2176, ReadFrame(frames[index]).Header.Opcode));
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[indices[0]]), out var maxHp));
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[indices[1]]), out var hp));
            Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints], maxHp.Updates.Select(entry => entry.Type));
            Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints],
                hp.Updates.Select(entry => entry.Type));
            Assert.Equal(100UL, maxHp.Updates[0].Value);
            Assert.Equal(100UL, hp.Updates[0].Value);
            Assert.Equal((ulong)expectedHitpoints, hp.Updates[1].Value);
        }
        foreach (var recipient in new[] { result.CasterSent, result.TargetSent })
        {
            var animationIndex = SingleFrameIndex(recipient, frame => frame.Header.Opcode == 2239);
            Assert.True(result.DeliveryOrder.IndexOf(frames[indices[1]]) <
                result.DeliveryOrder.IndexOf(recipient[animationIndex]));
        }

        return indices;
    }

    private static void AssertPounceStarArrowD13ReceiveAnimation(
        PounceStarArrowD13ReceiveCarrierResult result,
        uint expectedDamage)
    {
        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            var animationFrame = Assert.Single(frames.Select(ReadFrame), frame => frame.Header.Opcode == 2239);
            Assert.True(LegacySpellAnimationPacket.TryParse(animationFrame, out var animation));
            Assert.Single(ParseSpellAnimations(frames));
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(result.StarArrow ? StarArrowRuntimeSpellId : PounceRuntimeSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(3, animation.LevelHu);
            Assert.Equal(result.StarArrow ? (836, 584) : (840, 584), (animation.X, animation.Y));
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, hit.Uid);
            Assert.Equal(expectedDamage, hit.Damage);
            Assert.True(hit.Hit);
            Assert.Equal(MsgAttackEffect.None, hit.Effect);
            // StarArrow.cs:91-94 sets MoveX/Y after ReceiveAttack.Player;
            // Pounce.cs:43-44 queues the same carrier without adding movement.
            Assert.Equal(result.StarArrow ? (830, 584) : (0, 0), (hit.NewX, hit.NewY));
        }
    }

    private sealed record PounceStarArrowD13ReceiveCarrierResult(
        bool StarArrow,
        WorldSession Caster,
        WorldSession Target,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        List<byte[]> DeliveryOrder);
}
