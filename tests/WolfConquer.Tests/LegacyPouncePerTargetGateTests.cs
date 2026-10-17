using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int PouncePerTargetGateSolidFlag = 0x164;

    [Fact]
    public async Task HandlerPounceRechecksLaterPlayerKilledByEarlierHeavensWonderBeforeReceiveHooks()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreatePounceRuntimeRow(),
            CreateCounterKillRuntimeRow(HeavensWonderRuntimeSpellId, coolDown: 0, coldTime: 1_000));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new[] { new List<byte[]>(), new List<byte[]>() };
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnections = targetSent.Select(CreateSendingConnection).ToArray();
        var caster = CreateSession(
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: [PounceRuntimeSpellId],
            playerItems: CreatePounceEquipment(mainShield: true, alternateShield: false));
        var targets = new[]
        {
            CreateSession(
                uid: TargetPlayerUid,
                name: "PounceGateFirstCandidate",
                x: 842,
                y: 584,
                minHitPoints: 100_000,
                stamina: 100,
                spellIds: []),
            CreateSession(
                uid: TargetPlayerUid + 1,
                name: "PounceGateSecondCandidate",
                x: 843,
                y: 584,
                minHitPoints: 100_000,
                stamina: 100,
                spellIds: []),
        };
        sessions.Set(casterConnection, caster);
        for (var index = 0; index < targets.Length; index++)
        {
            sessions.Set(targetConnections[index], targets[index]);
        }

        // SessionStore enumeration is the source of the selected target order.
        // Assign the counter and fragile target only after observing that order.
        var targetOrder = GetVisibleTargetOrder(sessions, casterConnection, PlayerUid);
        Assert.Equal(2, targetOrder.Count);
        var counterIndex = Array.FindIndex(targets, target => target.Character.Uid == targetOrder[0]);
        var fragileIndex = Array.FindIndex(targets, target => target.Character.Uid == targetOrder[1]);
        Assert.True(counterIndex >= 0);
        Assert.True(fragileIndex >= 0);
        var counterCaster = targets[counterIndex] with
        {
            Character = targets[counterIndex].Character with
            {
                Strength = 1_000,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Stamina"] = "100",
                    [CounterKill16350BackDamageRollAttribute] = "0",
                },
            },
            PlayerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(HeavensWonderRuntimeSpellId)]),
        };
        var fragileTarget = targets[fragileIndex] with
        {
            Character = targets[fragileIndex].Character with
            {
                MinHitPoints = 1,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["MaxHitpoints"] = "100000",
                    ["LegacySpeffectLevel.Solid"] = "4",
                    ["LegacySpeffectRoll.Solid"] = "0",
                    ["LegacySpeffectLevel.Bloodthirst"] = "4",
                    ["LegacySpeffectRoll.Bloodthirst"] = "0",
                },
            },
        };
        sessions.Set(targetConnections[counterIndex], counterCaster);
        sessions.Set(targetConnections[fragileIndex], fragileTarget);
        Assert.Equal(targetOrder, GetVisibleTargetOrder(sessions, casterConnection, PlayerUid));
        var counterUid = checked((uint)counterCaster.Character.Uid);
        var fragileUid = checked((uint)fragileTarget.Character.Uid);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePounceRuntimeQuery(840, 584)).ToFrameBytes()));

        var wonder = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Equal(counterUid, wonder.AttackerUid);
        var lethalCounterHit = Assert.Single(wonder.Targets, target => target.Uid == fragileUid);
        Assert.True(lethalCounterHit.Damage >= 1);
        Assert.True(sessions.TryGet(targetConnections[fragileIndex], out var finalFragile));
        Assert.NotNull(finalFragile);
        Assert.Equal(0, finalFragile.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(finalFragile.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(HasLegacyStatusFlag(finalFragile.Character.Attributes, PouncePerTargetGateSolidFlag));
        Assert.False(finalFragile.Character.Attributes.ContainsKey("Solid"));

        var incoming = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == PounceRuntimeSpellId);
        Assert.Equal(counterUid, Assert.Single(incoming.Targets).Uid);
        Assert.DoesNotContain(incoming.Targets, target => target.Uid == fragileUid);
        var fragileFrames = targetSent[fragileIndex];
        Assert.DoesNotContain(
            fragileFrames,
            bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == fragileUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.BloodthirstElan ||
                    (entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                     entry.Flag == PouncePerTargetGateSolidFlag)));
        var hitpointIndices = AssertExistingNormalDeathCombatPhases(fragileFrames, fragileUid,
            (100_000, 1), (100_000, 1), (100_000, 1), (100_000, 0));
        Assert.All(hitpointIndices, index =>
            Assert.Equal((ushort)2176, ReadFrame(fragileFrames[index]).Header.Opcode));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(fragileFrames[hitpointIndices[^1]]), out var finalHp));
        Assert.Equal(0UL, Assert.Single(finalHp.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).Value);
        var wonderIndex = SingleFrameIndex(
            fragileFrames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.All(hitpointIndices, index => Assert.True(index < wonderIndex));
        Assert.Single(
            fragileFrames,
            bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
                attack.Query.AttackType == MsgAttackType.Death &&
                attack.Query.Uid == counterUid && attack.Query.OpponentUid == fragileUid);
        Assert.True(sessions.TryGet(targetConnections[counterIndex], out var finalCounterCaster));
        Assert.NotNull(finalCounterCaster);
        Assert.True(finalCounterCaster.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
    }

    [Fact]
    public async Task HandlerPounceRejectsSameMapAndCoordinatesInAnotherDynamicInstanceBeforeWildPhoenix()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreatePounceRuntimeRow(),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var caster = CreateSession(
            x: 840,
            y: 584,
            strength: 1_000,
            stamina: 100,
            spellIds: [PounceRuntimeSpellId],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DynamicID"] = "71",
            },
            playerItems: CreatePounceEquipment(mainShield: true, alternateShield: false));
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "PounceOtherInstance",
            x: 840,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DynamicID"] = "72",
                ["MaxHitpoints"] = "100000",
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "1",
                ["LegacySpeffectLevel.Solid"] = "4",
                ["LegacySpeffectRoll.Solid"] = "0",
                ["LegacySpeffectLevel.Bloodthirst"] = "4",
                ["LegacySpeffectRoll.Bloodthirst"] = "0",
            });
        Assert.Equal(caster.Character.MapId, target.Character.MapId);
        Assert.Equal((caster.Character.X, caster.Character.Y), (target.Character.X, target.Character.Y));
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePounceRuntimeQuery(840, 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        Assert.Equal(100_000, finalTarget.Character.MinHitPoints);
        Assert.Equal("72", finalTarget.Character.Attributes["DynamicID"]);
        Assert.Equal("1", finalTarget.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(finalTarget.Character.Attributes, LegacyWildPhoenixFlag));
        Assert.False(HasLegacyStatusFlag(finalTarget.Character.Attributes, PouncePerTargetGateSolidFlag));
        Assert.False(finalTarget.Character.Attributes.ContainsKey("Solid"));
        Assert.DoesNotContain(
            targetSent,
            bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type is
                    LegacyMsgUpdateDataType.ArchiveSkill or LegacyMsgUpdateDataType.BloodthirstElan or
                    LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        AssertPounceSourceAnimation(animation, 840, 584, expectedSoul: 0);
        Assert.Empty(animation.Targets);
        var animationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var parsed) &&
                parsed.SpellId == PounceRuntimeSpellId);
        Assert.Equal((ushort)2239, ReadFrame(casterSent[animationIndex]).Header.Opcode);
        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.Equal("50", finalCaster.Character.Attributes["Stamina"]);
        Assert.Equal("71", finalCaster.Character.Attributes["DynamicID"]);
        AssertNoPounceMovementFrame(casterSent);
    }
}
