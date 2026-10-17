using System.Collections.Concurrent;
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
    private const ushort DirectTripleNonPlayerSpellId = 17_490;
    private const uint DirectTripleNonPlayerGearId = 201;
    private const int DirectTripleNonPlayerSobUid = 65_174;
    private const uint DirectTripleNonPlayerInitialHp = 100_000;

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task DirectTripleNonPlayerMixedReceiveUsesActualRowMaxBonusBeforeFinalAnimation(byte soul)
    {
        var result = await DirectTripleNonPlayerRunAsync(soul);

        // Source GetDamage:1129-1228 chooses MAX for monsters/SOBs, including
        // actual row2857's encoded250 and85 candidates, and adds the bonus:
        // base300 + floor(300*250/100) =1050. Monster level121 vs caster120
        // bypasses the level bonus (Base.CalcDamageUser2Monster:67-100);
        // defense0 and Defense2=10000 leave1050 unchanged. Player MIN includes
        // zeros, hence base300 plus the normal no-breakthrough50% =450.
        Assert.Equal(98_950U, result.MonsterHitpoints);
        Assert.Equal(99_550, result.Target.Character.MinHitPoints);
        Assert.Equal(98_950, result.SobHitpoints);
        foreach (var frames in new[] { result.CasterFrames, result.TargetFrames })
        {
            var animation = Assert.Single(ParseSpellAnimations(frames));
            Assert.Equal(DirectTripleNonPlayerSpellId, animation.SpellId);
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal((839, 584), ((int)animation.X, (int)animation.Y));
            Assert.Equal(soul, animation.LevelHu);
            Assert.Equal(new uint[] { result.MonsterUid, TargetPlayerUid, DirectTripleNonPlayerSobUid },
                animation.Targets.Select(target => target.Uid));
            Assert.Equal(new uint[] { 1_050, 450, 1_050 },
                animation.Targets.Select(target => target.Damage));
            Assert.All(animation.Targets, target => Assert.True(target.Hit));
            var animationFrame = Assert.Single(frames, bytes =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.SpellId == DirectTripleNonPlayerSpellId);
            Assert.Equal((ushort)2239, ReadFrame(animationFrame).Header.Opcode);
        }
        Assert.DoesNotContain(result.DeliveryOrder, bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical && attack.Query.Uid == PlayerUid);

        // Monster.Execute happens before the player loop, and Npc.Execute
        // follows it (CombatGearAttack:2235-2266), all before the final2239.
        // Boss1 here only enables the source nonlethal monster HP publication;
        // no BossKiller, receive modifiers, score/reward or lethal case is claimed.
        var monsterObservations = result.Observations.Where(item => item.Phase == "monster").ToArray();
        Assert.NotEmpty(monsterObservations);
        Assert.All(monsterObservations, item =>
        {
            Assert.Equal(98_950U, item.MonsterHitpoints);
            Assert.Equal(100_000, item.PlayerHitpoints);
            Assert.Equal(100_000, item.SobHitpoints);
        });
        var playerObservations = result.Observations.Where(item => item.Phase == "player").ToArray();
        Assert.NotEmpty(playerObservations);
        Assert.All(playerObservations, item =>
        {
            Assert.Equal(98_950U, item.MonsterHitpoints);
            Assert.Equal(99_550, item.PlayerHitpoints);
            Assert.Equal(100_000, item.SobHitpoints);
        });
        var animationObservations = result.Observations.Where(item => item.Phase == "animation").ToArray();
        Assert.NotEmpty(animationObservations);
        Assert.All(animationObservations, item =>
        {
            Assert.Equal(98_950U, item.MonsterHitpoints);
            Assert.Equal(99_550, item.PlayerHitpoints);
            Assert.Equal(98_950, item.SobHitpoints);
        });
        var monsterHpIndices = DirectTripleNonPlayerHitpointIndices(result.DeliveryOrder, result.MonsterUid);
        var playerHpIndices = DirectTripleNonPlayerHitpointIndices(result.DeliveryOrder, TargetPlayerUid);
        var animationIndex = Array.FindIndex(result.DeliveryOrder, bytes =>
            LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.SpellId == DirectTripleNonPlayerSpellId);
        Assert.NotEmpty(monsterHpIndices);
        Assert.NotEmpty(playerHpIndices);
        Assert.True(monsterHpIndices.Max() < playerHpIndices.Min());
        Assert.True(playerHpIndices.Max() < animationIndex);
        // Ordinary SobNpc.HitPoints is a plain setter (SobNpc:97-106), so no
        // invented generic SOB HP packet is required; its state is observed above.

        Assert.Equal(0, result.Caster.Character.Experience);
        Assert.Equal(0, Assert.Single(result.Caster.PlayerSpells.Spells).Experience);
        Assert.DoesNotContain(result.DeliveryOrder, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder), update =>
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
        Assert.Equal(500, result.Caster.Character.MinMana);
        Assert.Equal("100", result.Caster.Character.Attributes["Stamina"]);
    }

    private static int[] DirectTripleNonPlayerHitpointIndices(byte[][] frames, uint uid)
    {
        return Enumerable.Range(0, frames.Length).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
            update.Uid == uid && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints))
            .ToArray();
    }

    private static async Task<DirectTripleNonPlayerResult> DirectTripleNonPlayerRunAsync(byte soul)
    {
        var family = CreateMonsterFamily() with
        {
            Attributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
            {
                ["life"] = "100000",
                ["level"] = "121",
                ["defense"] = "0",
                ["defense2"] = "10000",
                ["Boss"] = "1",
                ["attack_range"] = "0",
            },
        };
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 31_749, boundX: 842, boundY: 584)
            with { MapId = 1_000 };
        var sobNpc = CreateMoveLineSobNpc(DirectTripleNonPlayerSobUid, x: 844, y: 584) with
        {
            MapId = 1_000,
            Life = checked((int)DirectTripleNonPlayerInitialHp),
            MaxLife = checked((int)DirectTripleNonPlayerInitialHp),
        };
        var worldData = CreateMagicCatalogCore(family, [spawn], [sobNpc],
        [
            CreateMagicRow(DirectTripleNonPlayerSpellId, sort: 99, useMana: 0,
                damage: 30_250, experience: 1_000_000, range: 4, useStamina: 0,
                coolDown: 0, coldTime: 0, damageOnHuman: 0, damage2: 33,
                damage3: 2, damageOnMonster: 30_085, gDamage: 0),
        ]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, family);
        Assert.True(monster.Boss);
        Assert.Equal((byte)121, monster.Level);
        Assert.Equal(DirectTripleNonPlayerInitialHp, monster.MaxLife);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobStates = new SobNpcRuntimeStateStore();
        var caster = CreateSession(mapId: 1_000, classCode: 2_005, strength: 300, spirit: 0,
            minHitPoints: DirectTripleNonPlayerInitialHp, stamina: 100,
            spellEntries: [CreatePlayerSpell(DirectTripleNonPlayerSpellId) with { SoulLevel = soul, UseSpellSoul = soul }],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["MaxHitpoints"] = "100000",
                ["BattlePower"] = "120",
                ["LegacyCombatGearAttackSpellIndex"] = "3",
                ["LegacyCombatGearAttackRateRoll"] = "99",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1,
                Items = [new CombatGearSnapshotItem(DirectTripleNonPlayerGearId, Level: 1, Exp: 0,
                    Equip: 1, Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
            },
        };
        var target = CreateSession(uid: TargetPlayerUid, name: "DirectTripleNonPlayerTarget",
            mapId: 1_000, x: 839, y: 584, strength: 0, spirit: 0, classCode: 2_005,
            minHitPoints: DirectTripleNonPlayerInitialHp, spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "100000",
                ["BattlePower"] = "120",
            });
        var fields = LegacyPlayerStatusCalculator.CalculateFields(caster.Character, caster.PlayerItems,
            worldData, DirectTripleNonPlayerInitialHp, prestigeLevel: 0);
        Assert.Equal(300U, fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(301U, fields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(0U, fields[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
        Assert.Equal(0U, fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        var sessions = new WorldSessionStore();
        var casterFrames = new ConcurrentQueue<byte[]>();
        var targetFrames = new ConcurrentQueue<byte[]>();
        var deliveryOrder = new ConcurrentQueue<byte[]>();
        var observations = new ConcurrentQueue<DirectTripleNonPlayerObservation>();
        ConquerTcpConnection? targetConnection = null;

        ValueTask Capture(ConcurrentQueue<byte[]> frames, ReadOnlyMemory<byte> frame)
        {
            var bytes = frame.ToArray();
            frames.Enqueue(bytes);
            deliveryOrder.Enqueue(bytes);
            string? phase = null;
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints))
            {
                phase = update.Uid == monster.Uid ? "monster" : update.Uid == TargetPlayerUid ? "player" : null;
            }
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                animation.SpellId == DirectTripleNonPlayerSpellId)
            {
                phase = "animation";
            }
            if (phase is not null)
            {
                Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var currentMonster));
                Assert.True(sessions.TryGet(targetConnection!, out var currentTarget));
                Assert.NotNull(currentTarget);
                observations.Enqueue(new DirectTripleNonPlayerObservation(phase, currentMonster.Hitpoints,
                    currentTarget.Character.MinHitPoints, sobStates.GetVisible(sobNpc).Life));
            }
            return ValueTask.CompletedTask;
        }

        var casterConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) => Capture(casterFrames, frame));
        targetConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9959),
            sendAsync: (frame, _) => Capture(targetFrames, frame));
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(casterConnection, TextWriter.Synchronized(log), caster,
            WorldData: worldData, Sessions: sessions, MonsterStates: monsterStates, SobNpcStates: sobStates,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: TargetPlayerUid,
                X: 120, Y: 121, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        Assert.True(sessions.TryGet(casterConnection, out var currentCaster));
        Assert.NotNull(currentCaster);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var finalMonster));
        return new DirectTripleNonPlayerResult(currentCaster, finalTarget, monster.Uid,
            finalMonster.Hitpoints, sobStates.GetVisible(sobNpc).Life, casterFrames.ToArray(), targetFrames.ToArray(),
            deliveryOrder.ToArray(), observations.ToArray());
    }

    private sealed record DirectTripleNonPlayerObservation(
        string Phase,
        uint MonsterHitpoints,
        long PlayerHitpoints,
        int SobHitpoints);

    private sealed record DirectTripleNonPlayerResult(
        WorldSession Caster,
        WorldSession Target,
        uint MonsterUid,
        uint MonsterHitpoints,
        int SobHitpoints,
        byte[][] CasterFrames,
        byte[][] TargetFrames,
        byte[][] DeliveryOrder,
        DirectTripleNonPlayerObservation[] Observations);
}
