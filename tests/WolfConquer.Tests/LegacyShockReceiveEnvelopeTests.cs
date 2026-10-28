using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerCrackingShockPlayerOpeningReceivePublishesSolidBeforeHpAtInclusiveQueryRadius()
    {
        const int solidFlag = 0x164;
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateCrackingShockDebtRow(level: 0, experience: 0, damageOnMonster: 30_095)]);
        var caster = CreateSession(
            strength: 300,
            spirit: 0,
            minMana: 50,
            stamina: 100,
            classCode: 9_001,
            spellEntries: [new PlayerSpellSnapshotEntry(CrackingShockSpellId, 0, 0, 0, 2, 2)]);
        // Shock.cs uses Attack.X/Y, not the caster, and includes distance six.
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "ShockReceiveTarget",
            x: 842,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [],
            attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "100000",
                ["LegacySpeffectLevel.Solid"] = "4",
                ["LegacySpeffectRoll.Solid"] = "0",
            });
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var solidObservations = new List<WorldSession>();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                targetSent.Add(bytes);
                if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                    update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == solidFlag))
                {
                    solidObservations.Add(Assert.Single(
                        sessions.Snapshot(), active => active.Session.Character.Uid == TargetPlayerUid).Session);
                }

                return ValueTask.CompletedTask;
            });
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                caster,
                WorldData: worldData,
                Sessions: sessions,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 836,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CrackingShockSpellId)).ToFrameBytes()));

        // ReceiveAttack.Player opens with speffect group 3 before HP mutation;
        // speffect_attribute.GetSolid(4) supplies 2400 for five seconds.
        var beforeHit = Assert.Single(solidObservations);
        Assert.Equal(100_000, beforeHit.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(beforeHit.Character.Attributes, solidFlag));
        Assert.Equal("2400", beforeHit.Character.Attributes["Solid"]);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        Assert.True(HasLegacyStatusFlag(finalTarget.Character.Attributes, solidFlag));
        Assert.Equal("2400", finalTarget.Character.Attributes["Solid"]);
        Assert.Equal((842L, 584L), (finalTarget.Character.X, finalTarget.Character.Y));

        uint requestedDamage = 0;
        foreach (var frames in new[] { casterSent, targetSent })
        {
            var animationFrame = Assert.Single(frames.Select(ReadFrame), frame => frame.Header.Opcode == 2239);
            Assert.True(LegacySpellAnimationPacket.TryParse(animationFrame, out var animation));
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal((836, 584), (animation.X, animation.Y));
            Assert.Equal(CrackingShockSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(2, animation.LevelHu);
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, hit.Uid);
            Assert.True(hit.Hit);
            Assert.Equal(MsgAttackEffect.None, hit.Effect);
            Assert.True(hit.Damage > 0);
            if (requestedDamage != 0)
            {
                Assert.Equal(requestedDamage, hit.Damage);
            }

            requestedDamage = hit.Damage;
        }

        // Keep this receive-envelope proof independent of missing physical tails.
        Assert.Equal(100_000L - requestedDamage, finalTarget.Character.MinHitPoints);
        var hpIndices = Enumerable.Range(0, targetSent.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(targetSent[index]), out var update) &&
            update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(2, hpIndices.Length);
        Assert.All(hpIndices, index => Assert.Equal((ushort)2176, ReadFrame(targetSent[index]).Header.Opcode));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(targetSent[hpIndices[0]]), out var maxHp));
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 100_000)], maxHp.Updates);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(targetSent[hpIndices[1]]), out var hp));
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 100_000),
             new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, 100_000U - requestedDamage)],
            hp.Updates);
        var solidStatusIndex = SingleFrameIndex(targetSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null && IsLegacyStatusFlagSet(entry.Values, solidFlag)));
        var solidArchiveIndex = SingleFrameIndex(targetSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == solidFlag && entry.Time == 5 && entry.Damage == 2_400 && entry.SpellLevel == 0));
        Assert.Equal((ushort)2176, ReadFrame(targetSent[solidStatusIndex]).Header.Opcode);
        Assert.Equal((ushort)2176, ReadFrame(targetSent[solidArchiveIndex]).Header.Opcode);
        Assert.True(solidStatusIndex < solidArchiveIndex && solidArchiveIndex < hpIndices[0]);
        var animationIndex = SingleFrameIndex(targetSent, frame => frame.Header.Opcode == 2239);
        Assert.True(hpIndices[0] < hpIndices[1] && hpIndices[1] < animationIndex);
    }

    [Fact]
    public async Task HandlerCrackingShockMixedBossPlayerPublishesMonsterHpBeforeSolidAndPlayerHp()
    {
        const int solidFlag = 0x164;
        static bool IsCastAnimation(PacketFrame frame) =>
            frame.Header.Opcode == 2239 &&
            LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
            animation.AttackerUid == PlayerUid && animation.SpellId == CrackingShockSpellId;

        var family = CreateHighLifeMonsterFamily();
        family = family with
        {
            Attributes = new Dictionary<string, string>(family.Attributes) { ["Boss"] = "1" },
        };
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 584);
        var worldData = CreateMagicCatalogCore(
            family,
            spawns: [spawn],
            sobNpcs: [],
            magicRows:
            [
                CreateCrackingShockDebtRow(level: 0, experience: 1, damageOnMonster: 30_095),
                CreateCrackingShockDebtRow(level: 1, experience: 200, damageOnMonster: 30_095),
            ]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, family);
        Assert.True(monster.Boss);
        Assert.Equal(500_000U, monster.MaxLife);
        var monsterStates = new MonsterRuntimeStateStore();
        var caster = CreateSession(
            strength: 300,
            spirit: 0,
            minMana: 50,
            stamina: 100,
            classCode: 9_001,
            spellEntries: [new PlayerSpellSnapshotEntry(CrackingShockSpellId, 0, 0, 0, 2, 2)]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "ShockMixedReceiveTarget",
            x: 842,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [],
            attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "100000",
                ["LegacySpeffectLevel.Solid"] = "4",
                ["LegacySpeffectRoll.Solid"] = "0",
            });
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var deliveryOrder = new List<byte[]>();
        var casterConnection = CreateStarArrowReceiveEnvelopeConnection(casterSent, deliveryOrder);
        var targetConnection = CreateStarArrowReceiveEnvelopeConnection(targetSent, deliveryOrder);
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(new WorldPacketHandlerContext(
                casterConnection,
                log,
                caster,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13), ref caster);
        await PacketHandlers.HandleAsync(
            combatSpellOwner.Context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 836,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CrackingShockSpellId)).ToFrameBytes()));

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.True(visibleMonster.Hitpoints > 0 && visibleMonster.Hitpoints < 500_000);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        Assert.True(HasLegacyStatusFlag(finalTarget.Character.Attributes, solidFlag));
        Assert.Equal("2400", finalTarget.Character.Attributes["Solid"]);
        foreach (var frames in new[] { casterSent, targetSent })
        {
            var animationFrame = Assert.Single(frames.Select(ReadFrame), IsCastAnimation);
            Assert.True(LegacySpellAnimationPacket.TryParse(animationFrame, out var animation));
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal((836, 584), (animation.X, animation.Y));
            Assert.Equal(CrackingShockSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(2, animation.LevelHu);
            Assert.Equal(new[] { monster.Uid, TargetPlayerUid }, animation.Targets.Select(hit => hit.Uid));
            Assert.All(animation.Targets, hit =>
            {
                Assert.True(hit.Hit);
                Assert.True(hit.Damage > 0);
                Assert.Equal(MsgAttackEffect.None, hit.Effect);
            });
            Assert.Equal(500_000U - animation.Targets[0].Damage, visibleMonster.Hitpoints);
            Assert.Equal(100_000L - animation.Targets[1].Damage, finalTarget.Character.MinHitPoints);
        }

        var monsterHpIndices = Enumerable.Range(0, casterSent.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(casterSent[index]), out var update) &&
            update.Uid == monster.Uid && update.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(2, monsterHpIndices.Length);
        Assert.All(monsterHpIndices, index => Assert.Equal((ushort)2176, ReadFrame(casterSent[index]).Header.Opcode));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(casterSent[monsterHpIndices[0]]), out var monsterMaxHp));
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 500_000)], monsterMaxHp.Updates);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(casterSent[monsterHpIndices[1]]), out var monsterHp));
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 500_000),
             new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, visibleMonster.Hitpoints)],
            monsterHp.Updates);
        var solidStatusIndex = SingleFrameIndex(targetSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null && IsLegacyStatusFlagSet(entry.Values, solidFlag)));
        var solidArchiveIndex = SingleFrameIndex(targetSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == solidFlag && entry.Time == 5 && entry.Damage == 2_400 && entry.SpellLevel == 0));
        Assert.Equal((ushort)2176, ReadFrame(targetSent[solidStatusIndex]).Header.Opcode);
        Assert.Equal((ushort)2176, ReadFrame(targetSent[solidArchiveIndex]).Header.Opcode);
        var playerHpIndices = Enumerable.Range(0, targetSent.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(targetSent[index]), out var update) &&
            update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(2, playerHpIndices.Length);
        Assert.All(playerHpIndices, index => Assert.Equal((ushort)2176, ReadFrame(targetSent[index]).Header.Opcode));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(targetSent[playerHpIndices[0]]), out var playerMaxHp));
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 100_000)], playerMaxHp.Updates);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(targetSent[playerHpIndices[1]]), out var playerHp));
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 100_000),
             new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, checked((ulong)finalTarget.Character.MinHitPoints))],
            playerHp.Updates);

        // Shock.cs completes Monster.Execute (including boss HP) before Player.Execute.
        var monsterHpDelivery = deliveryOrder.IndexOf(casterSent[monsterHpIndices[1]]);
        Assert.True(deliveryOrder.IndexOf(casterSent[monsterHpIndices[0]]) < monsterHpDelivery);
        Assert.True(monsterHpDelivery < deliveryOrder.IndexOf(targetSent[solidStatusIndex]));
        Assert.True(monsterHpDelivery < deliveryOrder.IndexOf(targetSent[solidArchiveIndex]));
        Assert.True(monsterHpDelivery < deliveryOrder.IndexOf(targetSent[playerHpIndices[0]]));
        Assert.True(solidStatusIndex < solidArchiveIndex && solidArchiveIndex < playerHpIndices[0]);
        Assert.True(playerHpIndices[0] < playerHpIndices[1]);
        var targetAnimationIndex = SingleFrameIndex(targetSent, IsCastAnimation);
        Assert.True(playerHpIndices[1] < targetAnimationIndex);
        var casterAnimationIndex = SingleFrameIndex(casterSent, IsCastAnimation);
        var progressFrames = casterSent.Select(ReadFrame).Where(frame =>
            LegacySpellPacket.TryParse(frame, out var packet) && packet.Id == CrackingShockSpellId).ToArray();
        Assert.True(progressFrames.Length == 1, "Expected exactly one CrackingShock progress frame. Handler log:\n" + log);
        var progressIndex = SingleFrameIndex(casterSent, frame =>
            LegacySpellPacket.TryParse(frame, out var packet) && packet.Id == CrackingShockSpellId);
        Assert.True(progressIndex < casterAnimationIndex);
        Assert.True(deliveryOrder.IndexOf(targetSent[playerHpIndices[1]]) < deliveryOrder.IndexOf(casterSent[casterAnimationIndex]));
        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.Equal(4_999L, finalCaster.Character.MinHitPoints);
        Assert.Equal(160L, finalCaster.Character.Experience);
        Assert.Equal(1, Assert.Single(finalCaster.PlayerSpells.Spells).Level);
        var saved = new PlayerSpellSnapshotLoader(combatSpellOwner.Context.PlayerSpellsRoot!)
            .LoadByCharacterUid(PlayerUid);
        Assert.Equal(Assert.Single(finalCaster.PlayerSpells.Spells), Assert.Single(saved.Spells));
    }
}
