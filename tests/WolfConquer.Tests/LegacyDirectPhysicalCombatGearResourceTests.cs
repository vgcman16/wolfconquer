using System.Collections.Concurrent;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort DirectGearResourceSpellId = 17_460;
    private const uint DirectGearResourceGearId = 201;

    [Theory]
    [InlineData("success")]
    [InlineData("insufficient-mana")]
    [InlineData("remove-target-on-mana")]
    [InlineData("rebind-attacker-on-mana")]
    public async Task DirectGearResourcePhysicalReplacementPreparesOnceAndChecksParticipantsAfterManaSend(string scenario)
    {
        var result = await DirectGearResourceRunAsync(scenario);

        var expectedResourceTypes = scenario == "insufficient-mana"
            ? new[] { LegacyMsgUpdateDataType.Stamina }
            : new[] { LegacyMsgUpdateDataType.Stamina, LegacyMsgUpdateDataType.Mana };
        Assert.Equal(expectedResourceTypes, result.ResourceObservations.Select(observation => observation.ResourceType));
        Assert.All(result.ResourceObservations, observation =>
        {
            // CanAttackPlayer's accepted Verified call marks the attacker before
            // MsgAttackPacket.cs:642 enters CheckCombatGear and spends resources.
            Assert.Equal(PlayerUid, observation.AttackerUid);
            Assert.True(observation.FlashingName);
        });
        Assert.DoesNotContain(result.DeliveryOrder, bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical &&
            attack.Query.Uid == PlayerUid && attack.Query.OpponentUid == TargetPlayerUid);
        var staminaUpdates = ParseMsgUpdates(result.CasterFrames)
            .Where(update => update.Uid == PlayerUid)
            .SelectMany(update => update.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.Stamina).ToArray();
        Assert.Equal(95UL, Assert.Single(staminaUpdates).Value);
        var manaUpdates = ParseMsgUpdates(result.CasterFrames)
            .Where(update => update.Uid == PlayerUid)
            .SelectMany(update => update.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.Mana).ToArray();

        if (scenario == "insufficient-mana")
        {
            Assert.Empty(manaUpdates);
            Assert.Empty(result.TargetHitpointsObservedAtMana);
            Assert.Equal(0, result.ResourceCallbackTriggers);
            Assert.NotNull(result.Caster);
            Assert.Equal(6, result.Caster.Character.MinMana);
            // CanUseSpell spends stamina before checking mana; failure must not
            // refund it, fall through to an ordinary hit, or emit a spell animation.
            Assert.Equal("95", result.Caster.Character.Attributes["Stamina"]);
        }
        else
        {
            Assert.Equal(493UL, Assert.Single(manaUpdates).Value);
            Assert.Equal(100_000, Assert.Single(result.TargetHitpointsObservedAtMana));
            Assert.Equal(scenario == "success" ? 0 : 1, result.ResourceCallbackTriggers);
            Assert.NotNull(result.Caster);
            if (scenario == "rebind-attacker-on-mana")
            {
                Assert.Same(result.Replacement, result.Caster);
                Assert.Equal(77, result.Caster.Character.MinMana);
                Assert.Equal("33", result.Caster.Character.Attributes["Stamina"]);
                Assert.Equal(77_777, result.Caster.Character.MinHitPoints);
                Assert.Equal("replacement", result.Caster.Character.Attributes["DirectGearResourceMarker"]);
            }
            else
            {
                Assert.Equal(PlayerUid, result.Caster.Character.Uid);
                Assert.Equal(493, result.Caster.Character.MinMana);
                Assert.Equal("95", result.Caster.Character.Attributes["Stamina"]);
                Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
            }
        }

        if (scenario == "success")
        {
            foreach (var frames in new[] { result.CasterFrames, result.TargetFrames })
            {
                var animation = Assert.Single(ParseSpellAnimations(frames));
                Assert.Equal(DirectGearResourceSpellId, animation.SpellId);
                Assert.Equal(PlayerUid, animation.AttackerUid);
                var hit = Assert.Single(animation.Targets);
                Assert.Equal(TargetPlayerUid, hit.Uid);
                Assert.True(hit.Hit);
                Assert.True(hit.Damage > 0);
            }

            var staminaIndex = SingleFrameIndex(result.CasterFrames, frame =>
                LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina && entry.Value == 95));
            var manaIndex = SingleFrameIndex(result.CasterFrames, frame =>
                LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Mana && entry.Value == 493));
            var staminaDelivery = Array.IndexOf(result.DeliveryOrder, result.CasterFrames[staminaIndex]);
            var manaDelivery = Array.IndexOf(result.DeliveryOrder, result.CasterFrames[manaIndex]);
            Assert.True(staminaDelivery < manaDelivery);
            var animationIndices = Enumerable.Range(0, result.DeliveryOrder.Length).Where(index =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(result.DeliveryOrder[index]), out var animation) &&
                animation.AttackerUid == PlayerUid && animation.SpellId == DirectGearResourceSpellId).ToArray();
            Assert.Equal(2, animationIndices.Length);
            Assert.All(animationIndices, index =>
            {
                Assert.Equal((ushort)2239, ReadFrame(result.DeliveryOrder[index]).Header.Opcode);
                Assert.True(manaDelivery < index);
            });
            Assert.NotNull(result.Target);
            Assert.InRange(result.Target.Character.MinHitPoints, 1, 99_999);
            var publishedHitpoints = ParseMsgUpdates(result.TargetFrames)
                .Where(update => update.Uid == TargetPlayerUid)
                .SelectMany(update => update.Updates)
                .Where(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).ToArray();
            Assert.Equal(checked((ulong)result.Target.Character.MinHitPoints), Assert.Single(publishedHitpoints).Value);
        }
        else
        {
            Assert.Empty(ParseSpellAnimations(result.DeliveryOrder));
            Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder), update =>
                update.Updates.Any(entry => entry.Type is
                    LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
            if (scenario == "remove-target-on-mana")
            {
                Assert.Null(result.Target);
            }
            else
            {
                Assert.NotNull(result.Target);
                Assert.Equal(100_000, result.Target.Character.MinHitPoints);
            }
        }
    }

    private static async Task<DirectGearResourceResult> DirectGearResourceRunAsync(string scenario)
    {
        var worldData = CreateMagicCatalogCore(CreateMonsterFamily(), spawns: [], sobNpcs: [],
            magicRows:
            [
                CreateMagicRow(DirectGearResourceSpellId, sort: 87, useMana: 7,
                    damage: 120, experience: 1_000_000, range: 20, useStamina: 5,
                    coolDown: 0, coldTime: 0),
            ]);
        var caster = CreateSession(mapId: 1_000, classCode: 2_005, strength: 300, spirit: 0,
            minHitPoints: 100_000, minMana: scenario == "insufficient-mana" ? 6 : 500,
            stamina: 100, spellIds: [DirectGearResourceSpellId],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["MaxHitpoints"] = "100000",
                ["LegacyCombatGearAttackSpellIndex"] = "0",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1,
                Items =
                [
                    new CombatGearSnapshotItem(DirectGearResourceGearId, Level: 1, Exp: 0,
                        Equip: 1, Transform: 0, MasteryScore: 0, Animas: [], BlackJades: []),
                ],
            },
        };
        var target = CreateSession(uid: TargetPlayerUid, name: "DirectGearResourceTarget", mapId: 1_000,
            x: 839, y: 584, strength: 0, spirit: 0, minHitPoints: 100_000, classCode: 9_001,
            spellIds: [], attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "100000",
            });
        var replacement = CreateSession(uid: TargetPlayerUid + 2_000, name: "DirectGearResourceReplacement",
            mapId: 1_000, minHitPoints: 77_777, minMana: 77, stamina: 33, spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "77777",
                ["DirectGearResourceMarker"] = "replacement",
            });
        var sessions = new WorldSessionStore();
        var casterFrames = new ConcurrentQueue<byte[]>();
        var targetFrames = new ConcurrentQueue<byte[]>();
        var deliveryOrder = new ConcurrentQueue<byte[]>();
        var targetHitpointsObservedAtMana = new ConcurrentQueue<long>();
        var resourceObservations = new ConcurrentQueue<DirectGearResourcePublicationObservation>();
        var resourceCallbackTriggers = 0;
        ConquerTcpConnection? casterConnection = null;
        ConquerTcpConnection? targetConnection = null;

        ValueTask Capture(ConcurrentQueue<byte[]> frames, bool casterRecipient, ReadOnlyMemory<byte> frame)
        {
            var bytes = frame.ToArray();
            frames.Enqueue(bytes);
            deliveryOrder.Enqueue(bytes);
            if (casterRecipient && LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var resourceUpdate) &&
                resourceUpdate.Uid == PlayerUid)
            {
                foreach (var entry in resourceUpdate.Updates.Where(entry => entry.Type is
                    LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana))
                {
                    // Capture the live original caster before any Mana callback
                    // below can remove the target or replace this connection's UID.
                    Assert.True(sessions.TryGet(casterConnection!, out var currentResourceCaster));
                    Assert.NotNull(currentResourceCaster);
                    resourceObservations.Enqueue(new DirectGearResourcePublicationObservation(
                        entry.Type,
                        currentResourceCaster.Character.Uid,
                        HasLegacyStatusFlag(currentResourceCaster.Character.Attributes, LegacyFlashingNameFlag)));
                }
            }
            if (casterRecipient && LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == PlayerUid && update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Mana && entry.Value == 493))
            {
                Assert.True(sessions.TryGet(targetConnection!, out var currentTarget));
                Assert.NotNull(currentTarget);
                targetHitpointsObservedAtMana.Enqueue(currentTarget.Character.MinHitPoints);
                if (scenario is "remove-target-on-mana" or "rebind-attacker-on-mana" &&
                    Interlocked.CompareExchange(ref resourceCallbackTriggers, 1, 0) == 0)
                {
                    if (scenario == "remove-target-on-mana")
                    {
                        Assert.True(sessions.TryRemove(targetConnection!, out var removed));
                        Assert.Same(currentTarget, removed);
                    }
                    else
                    {
                        sessions.Set(casterConnection!, replacement);
                    }
                }
            }
            return ValueTask.CompletedTask;
        }

        casterConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) => Capture(casterFrames, casterRecipient: true, frame));
        targetConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9959),
            sendAsync: (frame, _) => Capture(targetFrames, casterRecipient: false, frame));
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(casterConnection, TextWriter.Synchronized(log), caster,
            WorldData: worldData, Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(), ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 120, Y: 121,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        sessions.TryGet(casterConnection, out var currentCaster);
        sessions.TryGet(targetConnection, out var currentTarget);
        return new DirectGearResourceResult(currentCaster, currentTarget, replacement,
            casterFrames.ToArray(), targetFrames.ToArray(), deliveryOrder.ToArray(),
            targetHitpointsObservedAtMana.ToArray(), resourceObservations.ToArray(),
            Volatile.Read(ref resourceCallbackTriggers));
    }

    private sealed record DirectGearResourcePublicationObservation(
        LegacyMsgUpdateDataType ResourceType,
        long AttackerUid,
        bool FlashingName);

    private sealed record DirectGearResourceResult(
        WorldSession? Caster,
        WorldSession? Target,
        WorldSession Replacement,
        byte[][] CasterFrames,
        byte[][] TargetFrames,
        byte[][] DeliveryOrder,
        long[] TargetHitpointsObservedAtMana,
        DirectGearResourcePublicationObservation[] ResourceObservations,
        int ResourceCallbackTriggers);
}
