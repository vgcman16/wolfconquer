using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort CrackingShockDebtSpellId = 15_720;

    [Fact]
    public async Task HandlerCrackingShockPreservesOrdinaryAcceptedPlayerWhoseScaledDamageIsZero()
    {
        // Physical.OnPlayer: 82%, then DamageOnMonster % 1000, minimum 1.
        // Shock.cs then applies 75 / 100 and still enqueues the zero-damage hit.
        var result = await RunCrackingShockCombatDebtAsync(
            queryX: 836,
            damageOnMonster: 0,
            targets: [(TargetPlayerUid, 840, 584)]);

        var animation = AssertCrackingShockDebtAnimation(result, expectedX: 836);
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, hit.Uid);
        Assert.Equal(0U, hit.Damage);
        Assert.True(hit.Hit);
        Assert.Equal(MsgAttackEffect.None, hit.Effect);
        Assert.Equal(100_000, Assert.Single(result.Targets).Session.Character.MinHitPoints);
        AssertCrackingShockDebtResources(result);
    }

    [Fact]
    public async Task HandlerCrackingShockIncludesPlayerAtSixTilesAndExcludesPlayerAtSevenTiles()
    {
        var result = await RunCrackingShockCombatDebtAsync(
            queryX: 836,
            damageOnMonster: 30_095,
            targets:
            [
                (TargetPlayerUid, 842, 584),
                (TargetPlayerUid + 1, 843, 584),
            ]);

        var animation = AssertCrackingShockDebtAnimation(result, expectedX: 836);
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, hit.Uid);
        Assert.True(hit.Damage > 0);
        Assert.True(hit.Hit);
        var boundary = Assert.Single(result.Targets, target => target.Uid == TargetPlayerUid);
        var outside = Assert.Single(result.Targets, target => target.Uid == TargetPlayerUid + 1);
        Assert.Equal(100_000L - hit.Damage, boundary.Session.Character.MinHitPoints);
        Assert.Equal(100_000, outside.Session.Character.MinHitPoints);
        Assert.DoesNotContain(
            ParseSpellAnimations(outside.Sent).SelectMany(packet => packet.Targets),
            target => target.Uid == outside.Uid);
        AssertCrackingShockDebtResources(result);
    }

    [Fact]
    public async Task HandlerCrackingShockUsesUnchangedQueryCenterAndPublishesProgressBeforeAnimation()
    {
        // All targets remain visible to the caster at 830,584. The source uses
        // Attack.X/Y (840,584), without pushing that point six tiles from caster.
        var result = await RunCrackingShockCombatDebtAsync(
            queryX: 840,
            damageOnMonster: 30_095,
            targets: [(TargetPlayerUid, 845, 584)],
            includeProgressMonster: true);

        var animation = AssertCrackingShockDebtAnimation(result, expectedX: 840);
        Assert.Equal(2, animation.Targets.Count);
        var playerHit = Assert.Single(animation.Targets, target => target.Uid == TargetPlayerUid);
        Assert.True(playerHit.Damage > 0);
        Assert.Equal(100_000L - playerHit.Damage, Assert.Single(result.Targets).Session.Character.MinHitPoints);
        var monsterHit = Assert.Single(animation.Targets, target => target.Uid != TargetPlayerUid);
        Assert.True(monsterHit.Damage > 0);

        var animationIndex = SingleFrameIndex(
            result.CasterSent,
            frame => frame.Header.Opcode == 2239);
        var progressIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellPacket.TryParse(frame, out var packet) &&
                packet.Id == CrackingShockDebtSpellId);
        Assert.True(progressIndex < animationIndex);
        Assert.Equal(1, Assert.Single(result.Caster.PlayerSpells.Spells).Level);
        AssertCrackingShockDebtResources(result);
    }

    [Fact]
    public async Task HandlerCrackingShockValidEmptyCastStillSendsOneEmptyD13Animation()
    {
        var result = await RunCrackingShockCombatDebtAsync(
            queryX: 836,
            damageOnMonster: 30_095,
            targets: []);

        var animation = AssertCrackingShockDebtAnimation(result, expectedX: 836);
        Assert.Empty(animation.Targets);
        Assert.DoesNotContain(result.CasterSent, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
        AssertCrackingShockDebtResources(result);
    }

    [Fact]
    public async Task HandlerCrackingShockSobOverkillDisplaysRequestedDamageWithoutInflatingProgress()
    {
        const uint expectedRequestedDamage = 360;
        var sobNpc = CreateMoveLineSobNpc(id: 65_081, x: 836, y: 584) with
        {
            Life = 10,
            MaxLife = 10,
        };
        var result = await RunCrackingShockCombatDebtAsync(
            queryX: 836,
            damageOnMonster: 30_095,
            targets: [],
            sobNpc: sobNpc,
            useSpellSoul: 2);

        // Physical.OnNpcs is 300 * 120 / 100. CalculateSoul is identity.
        // Npc.Execute kills the 10-HP object but does not replace SpellObj.Damage.
        var animation = AssertCrackingShockDebtAnimation(result, expectedX: 836, expectedSoul: 2);
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), hit.Uid);
        Assert.Equal(expectedRequestedDamage, hit.Damage);
        Assert.True(hit.Hit);
        Assert.Equal(MsgAttackEffect.None, hit.Effect);
        var visibleSobNpc = result.SobNpcStates.GetVisible(sobNpc);
        Assert.Equal(0, visibleSobNpc.Life);
        Assert.Equal(10, visibleSobNpc.MaxLife);

        // A non-training SOB death on map 1015 returns zero experience, not 360.
        var learnedSpell = Assert.Single(result.Caster.PlayerSpells.Spells);
        Assert.Equal(0, learnedSpell.Level);
        Assert.Equal(0, learnedSpell.Experience);
        Assert.DoesNotContain(result.CasterSent, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
        AssertCrackingShockDebtResources(result);
    }

    private static async Task<CrackingShockCombatDebtResult> RunCrackingShockCombatDebtAsync(
        ushort queryX,
        int damageOnMonster,
        IReadOnlyList<(uint Uid, ushort X, ushort Y)> targets,
        bool includeProgressMonster = false,
        SobNpc? sobNpc = null,
        byte useSpellSoul = 0)
    {
        var family = CreateMonsterFamily();
        MonsterSpawn[] spawns = includeProgressMonster
            ? new[] { CreateSingleInstanceMonsterSpawn(boundX: 845, boundY: 585) }
            : [];
        var worldData = CreateMagicCatalogCore(
            family,
            spawns,
            sobNpc is null ? [] : [sobNpc],
            [
                CreateCrackingShockDebtRow(level: 0, experience: 1, damageOnMonster),
                CreateCrackingShockDebtRow(level: 1, experience: 200, damageOnMonster),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var caster = CreateSession(
            x: 830,
            y: 584,
            minMana: 50,
            minHitPoints: 100_000,
            strength: 300,
            spirit: 0,
            stamina: 100,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    CrackingShockDebtSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: useSpellSoul,
                    UseSpellSoul: useSpellSoul),
            ],
            classCode: 9_001);
        sessions.Set(casterConnection, caster);
        var targetSessions = targets.Select(target => CreateSession(
            uid: target.Uid,
            name: "CrackingShockDebt" + target.Uid,
            x: target.X,
            y: target.Y,
            minHitPoints: 100_000,
            spellIds: [])).ToArray();
        var targetSent = targets.Select(_ => new List<byte[]>()).ToArray();
        var targetConnections = targetSent.Select(CreateSendingConnection).ToArray();
        for (var index = 0; index < targets.Count; index++)
        {
            sessions.Set(targetConnections[index], targetSessions[index]);
        }

        using var log = new StringWriter();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: sobNpcStates,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref caster);
        context = combatSpellOwner.Context;
        var query = new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: targets.Count == 0 ? 0 : targets[0].Uid,
            X: queryX,
            Y: 584,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: CrackingShockDebtSpellId);
        var frame = ReadFrame(new MsgAttackPacket(query).ToFrameBytes());
        Assert.Equal((ushort)2135, frame.Header.Opcode);
        await PacketHandlers.HandleAsync(context, frame);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        var updatedTargets = new List<CrackingShockCombatDebtTarget>();
        for (var index = 0; index < targets.Count; index++)
        {
            Assert.True(sessions.TryGet(targetConnections[index], out var updatedTarget));
            Assert.NotNull(updatedTarget);
            updatedTargets.Add(new CrackingShockCombatDebtTarget(
                targets[index].Uid,
                updatedTarget,
                targetSent[index]));
        }

        return new CrackingShockCombatDebtResult(updatedCaster, casterSent, updatedTargets, sobNpcStates);
    }

    private static MagicType CreateCrackingShockDebtRow(byte level, uint experience, int damageOnMonster)
    {
        return CreateMagicRow(
            CrackingShockDebtSpellId,
            sort: (byte)LegacyMagicSort.Shock,
            level: level,
            useMana: 7,
            damage: 120,
            experience: experience,
            maxTargets: 6,
            range: 15,
            useStamina: 5,
            coolDown: 0,
            coldTime: 0,
            damageOnMonster: damageOnMonster);
    }

    private static LegacySpellAnimationPacket AssertCrackingShockDebtAnimation(
        CrackingShockCombatDebtResult result,
        ushort expectedX,
        byte expectedSoul = 0)
    {
        var frame = Assert.Single(result.CasterSent.Select(ReadFrame), frame => frame.Header.Opcode == 2239);
        Assert.True(LegacySpellAnimationPacket.TryParse(frame, out var animation));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(expectedX, animation.X);
        Assert.Equal((ushort)584, animation.Y);
        Assert.Equal(CrackingShockDebtSpellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(expectedSoul, animation.LevelHu);
        return animation;
    }

    private static void AssertCrackingShockDebtResources(CrackingShockCombatDebtResult result)
    {
        Assert.Equal(43, result.Caster.Character.MinMana);
        Assert.Equal("95", result.Caster.Character.Attributes["Stamina"]);
        Assert.Equal((830L, 584L), (result.Caster.Character.X, result.Caster.Character.Y));
        var resources = result.CasterSent.Select(ReadFrame)
            .Where(frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == PlayerUid &&
                packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana))
            .ToArray();
        Assert.Equal(2, resources.Length);
        Assert.All(resources, frame => Assert.Equal((ushort)2176, frame.Header.Opcode));
    }

    private sealed record CrackingShockCombatDebtResult(
        WorldSession Caster,
        List<byte[]> CasterSent,
        IReadOnlyList<CrackingShockCombatDebtTarget> Targets,
        SobNpcRuntimeStateStore SobNpcStates);

    private sealed record CrackingShockCombatDebtTarget(uint Uid, WorldSession Session, List<byte[]> Sent);
}
