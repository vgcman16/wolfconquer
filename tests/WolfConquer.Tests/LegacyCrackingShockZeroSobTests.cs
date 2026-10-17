using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(300, 120U, 0U, 1_015, 836)]
    [InlineData(1, 1U, 1U, 1_015, 836)]
    [InlineData(300, 120U, 0U, 1_039, 836)]
    [InlineData(300, 120U, 0U, 1_015, 842)]
    public async Task HandlerCrackingShockZeroSobRetainsAcceptedHitWithoutHpOrProgressPublication(
        int strength,
        uint spellDamage,
        uint infinitySpellPercent,
        int mapId,
        int x)
    {
        var sobNpc = CreateCrackingShockZeroSob(mapId) with { X = x };
        var result = await RunCrackingShockZeroSobAsync(
            sobNpc,
            strength: strength,
            spellDamage: spellDamage,
            infinitySpellPercent: infinitySpellPercent);

        // Physical.OnNpcs: 300 * 120% * 0% = 0, or minimum-one damage
        // followed by floor(1 * 1%) = 0. Shock enqueues after Npc.Execute.
        AssertCrackingShockZeroSobAnimations(result, sobNpc, expectedDamage: 0);
        var visible = result.SobNpcStates.GetVisible(sobNpc);
        Assert.Equal(mapId == 1_039 ? 10_000 : 50_000, visible.Life);
        Assert.Equal(visible.Life, visible.MaxLife);
        AssertCrackingShockZeroSobHasNoHpOrDeathFrames(result, sobNpc);
        AssertCrackingShockZeroSobResources(result);

        var learned = Assert.Single(result.Caster.PlayerSpells.Spells);
        Assert.Equal(0, learned.Level);
        Assert.Equal(0, learned.Experience);
        Assert.DoesNotContain(result.CasterSent, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Equal(0, result.Caster.Character.Experience);
        Assert.Empty(result.Caster.PlayerProficiencies.Proficiencies);
        Assert.True(HasLegacyStatusFlag(result.Caster.Character.Attributes, InfinityStatusFlag));
    }

    [Theory]
    [InlineData(false, 0U, 360U)]
    [InlineData(true, 50U, 180U)]
    [InlineData(true, 100U, 360U)]
    public async Task HandlerCrackingShockZeroSobInfinityUsesSpellPercentOnlyWhileFlagged(
        bool hasInfinity,
        uint infinitySpellPercent,
        uint expectedDamage)
    {
        var sobNpc = CreateCrackingShockZeroSob();
        var result = await RunCrackingShockZeroSobAsync(
            sobNpc,
            infinitySpellPercent: infinitySpellPercent,
            hasInfinity: hasInfinity);

        AssertCrackingShockZeroSobAnimations(result, sobNpc, expectedDamage);
        Assert.Equal(50_000L - expectedDamage, result.SobNpcStates.GetVisible(sobNpc).Life);
        // Npc.Execute's nonlethal HitPoints setter does not send an HP packet.
        AssertCrackingShockZeroSobHasNoHpOrDeathFrames(result, sobNpc);
        AssertCrackingShockZeroSobResources(result);
    }

    [Theory]
    [InlineData(843, 50_000, 10, false)]
    [InlineData(836, 0, 10, false)]
    [InlineData(836, 50_000, 10, true)]
    [InlineData(836, 50_000, 0, false)]
    public async Task HandlerCrackingShockZeroSobDoesNotAdmitOutOfRangeDeadOrUnsupportedTargets(
        int x,
        int life,
        int kind,
        bool alreadyDead)
    {
        var sobNpc = CreateCrackingShockZeroSob() with { X = x, Life = life, Kind = kind };
        var result = await RunCrackingShockZeroSobAsync(sobNpc, alreadyDead: alreadyDead);

        AssertCrackingShockZeroSobAnimations(result, sobNpc, expectedDamage: null);
        Assert.Equal(alreadyDead ? 0 : life, result.SobNpcStates.GetVisible(sobNpc).Life);
        AssertCrackingShockZeroSobHasNoHpOrDeathFrames(result, sobNpc);
        AssertCrackingShockZeroSobResources(result);
        Assert.DoesNotContain(result.CasterSent, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
    }

    [Fact]
    public async Task HandlerCrackingShockZeroSobTrainingDeathStillPublishesD13HpResetBeforeAnimation()
    {
        var sobNpc = CreateCrackingShockZeroSob(mapId: 1_039);
        var result = await RunCrackingShockZeroSobAsync(
            sobNpc,
            strength: 10_000,
            hasInfinity: false);

        AssertCrackingShockZeroSobAnimations(result, sobNpc, expectedDamage: 12_000);
        Assert.Equal(10_000, result.SobNpcStates.GetVisible(sobNpc).Life);
        foreach (var frames in new[] { result.CasterSent, Assert.Single(result.Targets).Sent })
        {
            var hpFrames = frames.Select(ReadFrame)
                .Where(frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) && packet.Uid == sobNpc.Id)
                .ToArray();
            Assert.Equal(2, hpFrames.Length);
            Assert.All(hpFrames, frame => Assert.Equal((ushort)2176, frame.Header.Opcode));
            Assert.True(LegacyMsgUpdatePacket.TryParse(hpFrames[0], out var hp));
            Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, 10_000)], hp.Updates);
            Assert.True(LegacyMsgUpdatePacket.TryParse(hpFrames[1], out var maxHp));
            Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 10_000)], maxHp.Updates);
            var deathIndex = SingleFrameIndex(frames, frame =>
                MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Death && packet.Query.OpponentUid == sobNpc.Id);
            var hpIndex = SingleFrameIndex(frames, frame =>
                LegacyMsgUpdatePacket.TryParse(frame, out var packet) && packet.Uid == sobNpc.Id &&
                packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
            var maxHpIndex = SingleFrameIndex(frames, frame =>
                LegacyMsgUpdatePacket.TryParse(frame, out var packet) && packet.Uid == sobNpc.Id &&
                packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.MaxHitpoints));
            var animationIndex = SingleFrameIndex(frames, frame => frame.Header.Opcode == 2239);
            Assert.True(deathIndex < hpIndex && hpIndex < maxHpIndex && maxHpIndex < animationIndex);
        }

        AssertCrackingShockZeroSobResources(result);
    }

    private static void AssertCrackingShockZeroSobResources(CrackingShockCombatDebtResult result)
    {
        if (result.Caster.Character.MapId != 1_039)
        {
            AssertCrackingShockDebtResources(result);
            return;
        }

        // CanUseSpell.Verified exempts training map 1039 from both costs.
        Assert.Equal(50, result.Caster.Character.MinMana);
        Assert.Equal("100", result.Caster.Character.Attributes["Stamina"]);
        Assert.Equal((830L, 584L), (result.Caster.Character.X, result.Caster.Character.Y));
        foreach (var frames in new[] { result.CasterSent, Assert.Single(result.Targets).Sent })
        {
            Assert.DoesNotContain(frames, bytes =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Uid == PlayerUid &&
                packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana));
        }
    }

    private static SobNpc CreateCrackingShockZeroSob(int mapId = 1_015)
    {
        return CreateMoveLineSobNpc(id: 65_082, x: 836, y: 584) with
        {
            MapId = mapId,
            Kind = mapId == 1_039 ? LegacySobNpcEventRules.StakeKind : LegacySobNpcEventRules.PoleKind,
        };
    }

    private static async Task<CrackingShockCombatDebtResult> RunCrackingShockZeroSobAsync(
        SobNpc sobNpc,
        int strength = 300,
        uint spellDamage = 120,
        uint infinitySpellPercent = 0,
        bool hasInfinity = true,
        bool alreadyDead = false)
    {
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [sobNpc],
            magicRows:
            [
                CreateCrackingShockRow(experience: 1, damage: spellDamage),
                CreateCrackingShockRow(level: 1, experience: 200, damage: spellDamage),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var observerSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var observerConnection = CreateSendingConnection(observerSent);
        var caster = CreateSession(
            minMana: 50,
            strength: strength,
            spirit: 0,
            stamina: 100,
            spellEntries: [new PlayerSpellSnapshotEntry(CrackingShockSpellId, 0, 0, 0, 2, 2)],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = hasInfinity ? BuildStatusFlags(InfinityStatusFlag) : BuildStatusFlags(),
                ["InfinitySDamage"] = infinitySpellPercent.ToString(CultureInfo.InvariantCulture),
                ["InfinityDamage"] = "999",
            },
            mapId: sobNpc.MapId,
            classCode: 9_001);
        // Visible to the caster, but outside the attack radius around 836,584.
        var observer = CreateSession(
            uid: TargetPlayerUid,
            x: 824,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [],
            mapId: sobNpc.MapId);
        sessions.Set(casterConnection, caster);
        sessions.Set(observerConnection, observer);
        var sobNpcStates = new SobNpcRuntimeStateStore();
        if (alreadyDead)
        {
            var death = sobNpcStates.ApplyDamage(sobNpc, uint.MaxValue, DateTimeOffset.UtcNow);
            Assert.True(death.Killed);
        }

        using var log = new StringWriter();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                caster,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: 836,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CrackingShockSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.True(sessions.TryGet(observerConnection, out var updatedObserver));
        Assert.NotNull(updatedObserver);
        Assert.Equal(100_000, updatedObserver.Character.MinHitPoints);
        return new CrackingShockCombatDebtResult(
            updatedCaster,
            casterSent,
            [new CrackingShockCombatDebtTarget(TargetPlayerUid, updatedObserver, observerSent)],
            sobNpcStates);
    }

    private static void AssertCrackingShockZeroSobAnimations(
        CrackingShockCombatDebtResult result,
        SobNpc sobNpc,
        uint? expectedDamage)
    {
        foreach (var frames in new[] { result.CasterSent, Assert.Single(result.Targets).Sent })
        {
            var animation = AssertCrackingShockDebtAnimation(
                result with { CasterSent = frames },
                expectedX: 836,
                expectedSoul: 2);
            if (expectedDamage is not { } damage)
            {
                Assert.Empty(animation.Targets);
                continue;
            }

            var hit = Assert.Single(animation.Targets);
            Assert.Equal(checked((uint)sobNpc.Id), hit.Uid);
            Assert.Equal(damage, hit.Damage);
            Assert.True(hit.Hit);
            Assert.Equal(MsgAttackEffect.None, hit.Effect);
        }
    }

    private static void AssertCrackingShockZeroSobHasNoHpOrDeathFrames(
        CrackingShockCombatDebtResult result,
        SobNpc sobNpc)
    {
        foreach (var frames in new[] { result.CasterSent, Assert.Single(result.Targets).Sent })
        {
            Assert.DoesNotContain(frames, bytes =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == sobNpc.Id);
            Assert.DoesNotContain(frames, bytes =>
                MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.AttackType == MsgAttackType.Death && packet.Query.OpponentUid == sobNpc.Id);
        }
    }
}
