using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int PounceReceiveEnvelopeSageFlag = 0x111;
    private const int PounceReceiveEnvelopeSolidFlag = 0x164;
    private const int PounceReceiveEnvelopeHawksFlag = 0x100;
    private const uint PounceReceiveEnvelopeOutsideUid = TargetPlayerUid + 2;

    [Fact]
    public async Task HandlerPounceRunsReceiveEnvelopeAndPublishesEachPlayerBeforeNextTarget()
    {
        var result = await RunPounceReceiveEnvelopeAsync();

        Assert.True(HasLegacyStatusFlag(result.Caster.Character.Attributes, PounceReceiveEnvelopeSageFlag));
        Assert.Equal((840L, 590L), (result.Caster.Character.X, result.Caster.Character.Y));
        Assert.Equal("50", result.Caster.Character.Attributes["Stamina"]);
        Assert.Null(result.Caster.MagicCooldowns);
        Assert.True(result.Caster.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
        AssertNoPounceMovementFrame(result.CasterSent);

        var sageArchiveIndex = AssertPounceReceiveEnvelopeArchive(
            result.CasterSent,
            PlayerUid,
            PounceReceiveEnvelopeSageFlag,
            duration: 10,
            damage: 0);
        var sageStatusIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, PounceReceiveEnvelopeSageFlag)));
        var solidDeliveryIndices = new List<int>();
        var firstHitpointDeliveryIndices = new List<int>();
        var lastHitpointDeliveryIndices = new List<int>();
        foreach (var uid in result.TargetOrder)
        {
            var target = Assert.Single(result.Targets, candidate => candidate.Uid == uid);
            Assert.Equal(100_000L - target.ExpectedAppliedDamage, target.Session.Character.MinHitPoints);
            Assert.Equal((840L, target.OriginalY), (target.Session.Character.X, target.Session.Character.Y));
            Assert.True(HasLegacyStatusFlag(target.Session.Character.Attributes, PounceReceiveEnvelopeSolidFlag));
            Assert.Equal("2400", target.Session.Character.Attributes["Solid"]);
            Assert.False(HasLegacyStatusFlag(target.Session.Character.Attributes, PounceReceiveEnvelopeHawksFlag));
            AssertNoPounceMovementFrame(target.Sent);

            var solidStatusIndex = SingleFrameIndex(
                target.Sent,
                frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                    update.Uid == uid &&
                    update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                        entry.Values is not null &&
                        IsLegacyStatusFlagSet(entry.Values, PounceReceiveEnvelopeSolidFlag) &&
                        IsLegacyStatusFlagSet(entry.Values, PounceReceiveEnvelopeHawksFlag)));
            var solidArchiveIndex = AssertPounceReceiveEnvelopeArchive(
                target.Sent,
                uid,
                PounceReceiveEnvelopeSolidFlag,
                duration: 5,
                damage: 2_400);
            var hitpointIndices = AssertPounceReceiveEnvelopeHitpointPair(target);
            var firstHitpointDeliveryIndex = result.DeliveryOrder.IndexOf(target.Sent[hitpointIndices[0]]);
            var solidStatusDeliveryIndex = result.DeliveryOrder.IndexOf(target.Sent[solidStatusIndex]);
            Assert.True(solidStatusDeliveryIndex < firstHitpointDeliveryIndex);
            Assert.True(result.DeliveryOrder.IndexOf(target.Sent[solidArchiveIndex]) < firstHitpointDeliveryIndex);
            AssertPounceReceiveEnvelopeAnimation(target.Sent, result);
            solidDeliveryIndices.Add(solidStatusDeliveryIndex);
            firstHitpointDeliveryIndices.Add(firstHitpointDeliveryIndex);
            lastHitpointDeliveryIndices.Add(result.DeliveryOrder.IndexOf(target.Sent[hitpointIndices[1]]));
        }

        Assert.Equal(2, result.TargetOrder.Count);
        Assert.True(lastHitpointDeliveryIndices[0] < solidDeliveryIndices[1]);
        Assert.True(result.DeliveryOrder.IndexOf(result.CasterSent[sageArchiveIndex]) < firstHitpointDeliveryIndices[0]);
        Assert.True(result.DeliveryOrder.IndexOf(result.CasterSent[sageStatusIndex]) < firstHitpointDeliveryIndices[0]);
        AssertPounceReceiveEnvelopeAnimation(result.CasterSent, result);

        Assert.True(result.MonsterStates.TryGetVisibleInstance(
            result.Monster,
            DateTimeOffset.UtcNow,
            out var damagedMonster));
        Assert.Equal(result.Monster.MaxLife - result.MonsterDamage, damagedMonster.Hitpoints);
        var monsterHitpointIndices = Enumerable.Range(0, result.CasterSent.Count)
            .Where(index => LegacyMsgUpdatePacket.TryParse(ReadFrame(result.CasterSent[index]), out var update) &&
                update.Uid == result.Monster.Uid &&
                update.Updates.Any(entry => entry.Type is
                    LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints))
            .ToArray();
        Assert.Equal(2, monsterHitpointIndices.Length);
        Assert.All(monsterHitpointIndices, index =>
        {
            Assert.Equal((ushort)2176, ReadFrame(result.CasterSent[index]).Header.Opcode);
            Assert.True(result.DeliveryOrder.IndexOf(result.CasterSent[index]) < solidDeliveryIndices[0]);
        });

        var outside = Assert.Single(result.Targets, target => target.Uid == PounceReceiveEnvelopeOutsideUid);
        Assert.Equal(100_000, outside.Session.Character.MinHitPoints);
        Assert.Equal((840L, 596L), (outside.Session.Character.X, outside.Session.Character.Y));
        Assert.False(HasLegacyStatusFlag(outside.Session.Character.Attributes, PounceReceiveEnvelopeSolidFlag));
        Assert.True(HasLegacyStatusFlag(outside.Session.Character.Attributes, PounceReceiveEnvelopeHawksFlag));
        Assert.DoesNotContain(
            result.DeliveryOrder,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                (update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.BloodthirstElan) ||
                 (update.Uid == PounceReceiveEnvelopeOutsideUid &&
                  update.Updates.Any(entry => entry.Type is
                      LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints))));
        Assert.Contains("Chance / 100", result.Log);
        Assert.Contains("playerTargets=2", result.Log);
    }

    [Theory]
    [InlineData(false, 100, "100")]
    [InlineData(true, 49, "49")]
    public async Task HandlerPounceShieldOrResourceRejectDoesNotShiftOrRunReceiveEnvelope(
        bool hasShield,
        int stamina,
        string expectedStamina)
    {
        var result = await RunPounceReceiveEnvelopeAsync(hasShield, checked((ulong)stamina));

        Assert.Equal((830L, 584L), (result.Caster.Character.X, result.Caster.Character.Y));
        Assert.Equal(expectedStamina, result.Caster.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(result.Caster.Character.Attributes, PounceReceiveEnvelopeSageFlag));
        AssertNoPounceMovementFrame(result.CasterSent);
        Assert.All(result.Targets, target =>
        {
            Assert.Equal(100_000, target.Session.Character.MinHitPoints);
            Assert.False(HasLegacyStatusFlag(target.Session.Character.Attributes, PounceReceiveEnvelopeSolidFlag));
            Assert.True(HasLegacyStatusFlag(target.Session.Character.Attributes, PounceReceiveEnvelopeHawksFlag));
        });
        Assert.Empty(ParseSpellAnimations(result.DeliveryOrder));
        Assert.Empty(result.DeliveryOrder);
        Assert.DoesNotContain(
            result.DeliveryOrder,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                update.Updates.Any(entry => entry.Type is
                    LegacyMsgUpdateDataType.ArchiveSkill or LegacyMsgUpdateDataType.BloodthirstElan or
                    LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        Assert.DoesNotContain("Chance / 100", result.Log);
    }

    private static async Task<PounceReceiveEnvelopeResult> RunPounceReceiveEnvelopeAsync(
        bool hasShield = true,
        ulong stamina = 100)
    {
        var values = Enumerable.Repeat(0, 15).ToArray();
        values[0] = 17;
        values[5] = 5_000;
        values[6] = 10;
        values[7] = 0;
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 845, boundY: 590);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [spawn],
            sobNpcs: [],
            magicRows: [CreatePounceRuntimeRow()],
            gouyuImmortals:
            [
                new GouyuImmortal(17, values, "fixture://gouyu_immortal", 1, "", []),
            ]);
        var casterSession = CreateSession(
            strength: 1_000,
            stamina: stamina,
            classCode: 5_005,
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
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["LegacyNinjaLevels"] = "17",
                ["LegacyNinjaSageModeRoll"] = "0",
                ["HawksAmbitionPower"] = "25",
            },
            playerItems: CreatePounceEquipment(hasShield, alternateShield: false));
        var targetSessions = Enumerable.Range(0, 3)
            .Select(index => CreateSession(
                uid: TargetPlayerUid + index,
                name: "PounceReceiveTarget" + index,
                x: 840,
                y: 594 + index,
                minHitPoints: 100_000,
                spellIds: [],
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["StatusFlags"] = BuildStatusFlags(PounceReceiveEnvelopeHawksFlag),
                    ["MaxHitpoints"] = "100000",
                    ["LegacySpeffectLevel.Solid"] = "4",
                    ["LegacySpeffectRoll.Solid"] = "0",
                    ["LegacySpeffectLevel.Bloodthirst"] = "4",
                    ["LegacySpeffectRoll.Bloodthirst"] = "0",
                }))
            .ToArray();
        var sessions = new WorldSessionStore();
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = targetSessions.Select(_ => new List<byte[]>()).ToArray();
        var deliveryOrder = new List<byte[]>();
        var casterConnection = CreatePounceReceiveEnvelopeConnection(casterSent, deliveryOrder);
        var targetConnections = targetSent
            .Select(sent => CreatePounceReceiveEnvelopeConnection(sent, deliveryOrder))
            .ToArray();
        sessions.Set(casterConnection, casterSession);
        for (var index = 0; index < targetSessions.Length; index++)
        {
            sessions.Set(targetConnections[index], targetSessions[index]);
        }

        var targetOrder = GetVisibleTargetOrder(sessions, casterConnection, PlayerUid)
            .Where(uid => uid != PounceReceiveEnvelopeOutsideUid)
            .ToArray();
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            PounceRuntimeSpellId,
            out _,
            out var spell));
        var shiftedCaster = casterSession with
        {
            Character = casterSession.Character with { X = 840, Y = 590 },
        };
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            shiftedCaster,
            worldData,
            monster,
            monsterFamily,
            spell);
        var expectedDamage = targetSessions.Select(target => InvokePrivateUInt32(
                "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
                shiftedCaster,
                target,
                spell,
                worldData,
                1U))
            .ToArray();
        var queryFrame = ReadFrame(new MsgAttackPacket(CreatePounceRuntimeQuery()).ToFrameBytes());
        Assert.Equal((ushort)2135, queryFrame.Header.Opcode);
        await PacketHandlers.HandleAsync(context, queryFrame);

        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        var targets = new List<PounceReceiveEnvelopeTarget>();
        for (var index = 0; index < targetSessions.Length; index++)
        {
            Assert.True(sessions.TryGet(targetConnections[index], out var finalTarget));
            Assert.NotNull(finalTarget);
            var requestedDamage = expectedDamage[index];
            targets.Add(new PounceReceiveEnvelopeTarget(
                checked((uint)finalTarget.Character.Uid),
                finalTarget,
                targetSent[index],
                targetSessions[index].Character.Y,
                requestedDamage,
                requestedDamage - requestedDamage * 25 / 100));
        }

        return new PounceReceiveEnvelopeResult(
            finalCaster,
            casterSent,
            targets,
            targetOrder,
            deliveryOrder,
            monster,
            monsterStates,
            expectedMonsterDamage,
            log.ToString());
    }

    private static ConquerTcpConnection CreatePounceReceiveEnvelopeConnection(
        List<byte[]> recipientSent,
        List<byte[]> deliveryOrder)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                recipientSent.Add(bytes);
                deliveryOrder.Add(bytes);
                return ValueTask.CompletedTask;
            });
    }

    private static int AssertPounceReceiveEnvelopeArchive(
        IReadOnlyList<byte[]> frames,
        uint uid,
        int flag,
        uint duration,
        uint damage)
    {
        var index = SingleFrameIndex(
            frames,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == uid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == flag &&
                    entry.Time == duration && entry.Damage == damage));
        Assert.Equal((ushort)2176, ReadFrame(frames[index]).Header.Opcode);
        return index;
    }

    private static int[] AssertPounceReceiveEnvelopeHitpointPair(PounceReceiveEnvelopeTarget target)
    {
        var indices = Enumerable.Range(0, target.Sent.Count)
            .Where(index => LegacyMsgUpdatePacket.TryParse(ReadFrame(target.Sent[index]), out var update) &&
                update.Uid == target.Uid &&
                update.Updates.Any(entry => entry.Type is
                    LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints))
            .ToArray();
        Assert.Equal(2, indices.Length);
        Assert.All(indices, index => Assert.Equal((ushort)2176, ReadFrame(target.Sent[index]).Header.Opcode));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(target.Sent[indices[0]]), out var first));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(target.Sent[indices[1]]), out var second));
        Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints], first.Updates.Select(entry => entry.Type));
        Assert.Equal(
            [LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints],
            second.Updates.Select(entry => entry.Type));
        Assert.Equal(100_000UL, first.Updates[0].Value);
        Assert.Equal(first.Updates[0].Value, second.Updates[0].Value);
        Assert.Equal(100_000U - target.ExpectedAppliedDamage, second.Updates[1].Value);
        var animationIndex = SingleFrameIndex(
            target.Sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == PounceRuntimeSpellId);
        Assert.All(indices, index => Assert.True(index < animationIndex));
        return indices;
    }

    private static void AssertPounceReceiveEnvelopeAnimation(
        IReadOnlyList<byte[]> frames,
        PounceReceiveEnvelopeResult result)
    {
        var animation = Assert.Single(ParseSpellAnimations(frames));
        var index = SingleFrameIndex(
            frames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var parsed) &&
                parsed.SpellId == PounceRuntimeSpellId);
        Assert.Equal((ushort)2239, ReadFrame(frames[index]).Header.Opcode);
        AssertPounceSourceAnimation(animation, expectedX: 840, expectedY: 590, expectedSoul: 3);
        Assert.Equal(
            new[] { result.Monster.Uid }.Concat(result.TargetOrder).ToArray(),
            animation.Targets.Select(target => target.Uid).ToArray());
        Assert.All(animation.Targets, animationTarget =>
        {
            var receiveDamage = animationTarget.Uid == result.Monster.Uid
                ? result.MonsterDamage
                : Assert.Single(result.Targets, candidate => candidate.Uid == animationTarget.Uid).ExpectedAppliedDamage;
            Assert.Equal(receiveDamage, animationTarget.Damage);
            Assert.True(animationTarget.Hit);
            Assert.Equal(MsgAttackEffect.None, animationTarget.Effect);
            Assert.Equal((0, 0), (animationTarget.NewX, animationTarget.NewY));
        });
    }

    private sealed record PounceReceiveEnvelopeTarget(
        uint Uid,
        WorldSession Session,
        IReadOnlyList<byte[]> Sent,
        long OriginalY,
        uint RequestedDamage,
        uint ExpectedAppliedDamage);

    private sealed record PounceReceiveEnvelopeResult(
        WorldSession Caster,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<PounceReceiveEnvelopeTarget> Targets,
        IReadOnlyList<uint> TargetOrder,
        List<byte[]> DeliveryOrder,
        LegacyMonsterInstance Monster,
        MonsterRuntimeStateStore MonsterStates,
        uint MonsterDamage,
        string Log);
}
