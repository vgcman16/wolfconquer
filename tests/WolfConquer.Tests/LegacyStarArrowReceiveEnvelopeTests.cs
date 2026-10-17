using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag = 0x111;
    private const int StarArrowReceiveEnvelopeSolidFlag = 0x164;
    private const int StarArrowReceiveEnvelopeHawksAmbitionFlag = 0x100;
    private const uint StarArrowReceiveEnvelopeNatureShieldItemId = 4_038_109;

    [Theory]
    [InlineData(0)]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    [InlineData(DefensiveStanceStatusFlag)]
    public async Task HandlerStarArrowAcceptedPlayerRunsOpeningReceiveHooksBeforeSingleDamagePublication(
        int targetStatusFlag)
    {
        var result = await RunStarArrowReceiveEnvelopeAsync(targetStatusFlag);

        Assert.Equal((830L, 584L), (result.Target.Character.X, result.Target.Character.Y));
        Assert.Equal(100_000L - result.ExpectedAppliedDamage, result.Target.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(
            result.Caster.Character.Attributes,
            StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag));
        Assert.True(HasLegacyStatusFlag(
            result.Target.Character.Attributes,
            StarArrowReceiveEnvelopeSolidFlag));
        Assert.Equal("2400", result.Target.Character.Attributes["Solid"]);
        Assert.False(HasLegacyStatusFlag(
            result.Target.Character.Attributes,
            StarArrowReceiveEnvelopeHawksAmbitionFlag));

        AssertStarArrowReceiveEnvelopeAnimation(result.CasterSent, result);
        AssertStarArrowReceiveEnvelopeAnimation(result.TargetSent, result);
        var hitpointIndex = AssertStarArrowReceiveEnvelopeSingleHitpointPair(result);
        var solidStatusIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, StarArrowReceiveEnvelopeSolidFlag) &&
                    IsLegacyStatusFlagSet(entry.Values, StarArrowReceiveEnvelopeHawksAmbitionFlag)));
        var sageStatusIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag)));
        var solidArchiveIndex = AssertStarArrowReceiveEnvelopeArchive(
            result.TargetSent,
            TargetPlayerUid,
            StarArrowReceiveEnvelopeSolidFlag,
            duration: 5,
            damage: 2_400);
        var sageArchiveIndex = AssertStarArrowReceiveEnvelopeArchive(
            result.CasterSent,
            PlayerUid,
            StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag,
            duration: 10,
            damage: 0);
        var pullIndex = AssertStarArrowReceiveEnvelopePull(result.CasterSent);

        var firstHitpointDeliveryIndex = result.DeliveryOrder.IndexOf(result.TargetSent[hitpointIndex]);
        foreach (var hookFrame in new[]
        {
            result.TargetSent[solidStatusIndex],
            result.TargetSent[solidArchiveIndex],
            result.CasterSent[sageStatusIndex],
            result.CasterSent[sageArchiveIndex],
        })
        {
            var hookDeliveryIndex = result.DeliveryOrder.IndexOf(hookFrame);
            Assert.True(result.DeliveryOrder.IndexOf(result.CasterSent[pullIndex]) < hookDeliveryIndex);
            Assert.True(hookDeliveryIndex < firstHitpointDeliveryIndex);
        }

        // The target starts at full HP. Bloodthirst must inspect that state before
        // the incoming hit, and the opening carrier must not consume HawksAmbition.
        Assert.DoesNotContain(
            result.DeliveryOrder,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.BloodthirstElan));
        Assert.Contains("Chance / 100", result.Log);
        Assert.DoesNotContain("StarArrow NatureShield early return", result.Log);
    }

    [Fact]
    public async Task HandlerStarArrowNatureShieldAvoidancePullsWithoutOpeningReceiveHooks()
    {
        var result = await RunStarArrowReceiveEnvelopeAsync(natureShieldAvoids: true);

        Assert.Equal((830L, 584L), (result.Target.Character.X, result.Target.Character.Y));
        Assert.Equal(100_000, result.Target.Character.MinHitPoints);
        AssertStarArrowReceiveEnvelopePull(result.CasterSent);
        Assert.Empty(ParseSpellAnimations(result.CasterSent));
        Assert.Empty(ParseSpellAnimations(result.TargetSent));
        Assert.False(HasLegacyStatusFlag(
            result.Caster.Character.Attributes,
            StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag));
        Assert.False(HasLegacyStatusFlag(
            result.Target.Character.Attributes,
            StarArrowReceiveEnvelopeSolidFlag));
        Assert.True(HasLegacyStatusFlag(
            result.Target.Character.Attributes,
            StarArrowReceiveEnvelopeHawksAmbitionFlag));
        Assert.False(result.Target.Character.Attributes.ContainsKey("Solid"));
        Assert.DoesNotContain(
            result.DeliveryOrder,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                update.Updates.Any(entry =>
                    entry.Type is LegacyMsgUpdateDataType.ArchiveSkill or
                        LegacyMsgUpdateDataType.BloodthirstElan or
                        LegacyMsgUpdateDataType.Hitpoints or
                        LegacyMsgUpdateDataType.MaxHitpoints));
        Assert.Contains("StarArrow NatureShield early return after player pull", result.Log);
        Assert.DoesNotContain("Chance / 100", result.Log);
        Assert.DoesNotContain("ReceiveAttack.Player speffect Solid", result.Log);
    }

    private static async Task<StarArrowReceiveEnvelopeResult> RunStarArrowReceiveEnvelopeAsync(
        int targetStatusFlag = 0,
        bool natureShieldAvoids = false)
    {
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateStarArrowRuntimeRow(range: 2)],
            items:
            [
                .. CreateRangeStatusPhysicalDamageItems(),
                CreateItemType((int)StarArrowReceiveEnvelopeNatureShieldItemId, "NatureShield(+9)"),
            ],
            gouyuImmortals:
            [
                CreateStarArrowReceiveEnvelopeGouyuImmortal(),
            ]);
        var targetFlags = targetStatusFlag == 0
            ? new[] { StarArrowReceiveEnvelopeHawksAmbitionFlag }
            : new[] { StarArrowReceiveEnvelopeHawksAmbitionFlag, targetStatusFlag };
        var casterSession = CreateSession(
            strength: 300,
            classCode: 5_005,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    StarArrowRuntimeSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 3),
            ],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["LegacyNinjaLevels"] = "17",
                ["LegacyNinjaSageModeRoll"] = "0",
                ["HawksAmbitionPower"] = "25",
            },
            playerItems: CreateRangeStatusEquipment(RangeStatusPhysicalIncreaseItemId, position: 10));
        var targetItems = CreateRangeStatusEquipment(RangeStatusPhysicalDecreaseItemId, position: 11) with
        {
            RuneItems =
            [
                new PlayerItemSnapshotItem
                {
                    Uid = StarArrowReceiveEnvelopeNatureShieldItemId,
                    ItemId = StarArrowReceiveEnvelopeNatureShieldItemId,
                    Position = 101,
                },
            ],
        };
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "StarArrowReceiveTarget",
            x: 836,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(targetFlags),
                ["MaxHitpoints"] = "100000",
                [StarArrowNatureShieldRollAttribute] = natureShieldAvoids ? "0" : "99",
                ["LegacySpeffectLevel.Solid"] = "4",
                ["LegacySpeffectRoll.Solid"] = "0",
                ["LegacySpeffectLevel.Bloodthirst"] = "4",
                ["LegacySpeffectRoll.Bloodthirst"] = "0",
            },
            playerItems: targetItems);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var deliveryOrder = new List<byte[]>();
        var casterConnection = CreateStarArrowReceiveEnvelopeConnection(casterSent, deliveryOrder);
        var targetConnection = CreateStarArrowReceiveEnvelopeConnection(targetSent, deliveryOrder);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var (playerSpell, spell) = ResolveStarArrowRuntimeSpell(
            casterSession,
            worldData,
            StarArrowRuntimeSpellId);
        var pulledTarget = targetSession with
        {
            Character = targetSession.Character with { X = 830, Y = 584 },
        };
        var statusDefended = targetStatusFlag is
            LegacyPhysicalSpellsShurikenVortexFlag or LegacyPhysicalSpellsManiacDanceFlag;
        var rangeDamage = statusDefended
            ? RangeStatusExpectedExtraAttack
            : InvokePrivateUInt32(
                "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
                casterSession,
                pulledTarget,
                spell,
                worldData,
                1U);
        var requestedDamage = InvokePrivateUInt32(
            "CalculateLegacyAttackStatus7000SoulDamage",
            rangeDamage,
            playerSpell.UseSpellSoul);
        var effect = MsgAttackEffect.None;
        if (targetStatusFlag == DefensiveStanceStatusFlag)
        {
            var reduction = requestedDamage * 40 / 100;
            requestedDamage = requestedDamage <= reduction ? 1U : requestedDamage - reduction;
            effect = MsgAttackEffect.Block;
        }

        var expectedAppliedDamage = requestedDamage - requestedDamage * 25 / 100;
        Assert.True(await InvokeLegacyStarArrowAsync(
            context,
            casterSession,
            CreateStarArrowRuntimeQuery(StarArrowRuntimeSpellId, TargetPlayerUid, x: 836, y: 584),
            playerSpell,
            spell));
        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        return new StarArrowReceiveEnvelopeResult(
            finalCaster,
            finalTarget,
            casterSent,
            targetSent,
            deliveryOrder,
            requestedDamage,
            expectedAppliedDamage,
            effect,
            log.ToString());
    }

    private static ConquerTcpConnection CreateStarArrowReceiveEnvelopeConnection(
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

    private static void AssertStarArrowReceiveEnvelopeAnimation(
        IReadOnlyList<byte[]> frames,
        StarArrowReceiveEnvelopeResult result)
    {
        var animation = Assert.Single(ParseSpellAnimations(frames));
        var animationFrameIndex = SingleFrameIndex(
            frames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var parsed) &&
                parsed.SpellId == StarArrowRuntimeSpellId);
        Assert.Equal((ushort)2239, ReadFrame(frames[animationFrameIndex]).Header.Opcode);
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((836, 584), (animation.X, animation.Y));
        Assert.Equal(StarArrowRuntimeSpellId, animation.SpellId);
        Assert.Equal(3, animation.LevelHu);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(result.ExpectedAppliedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(result.Effect, target.Effect);
        Assert.Equal((830, 584), (target.NewX, target.NewY));
    }

    private static int AssertStarArrowReceiveEnvelopeSingleHitpointPair(StarArrowReceiveEnvelopeResult result)
    {
        var frames = result.TargetSent;
        var hitpointIndices = Enumerable.Range(0, frames.Count)
            .Where(index => LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type is
                    LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints))
            .ToArray();
        Assert.Equal(2, hitpointIndices.Length);
        Assert.All(hitpointIndices, index =>
            Assert.Equal((ushort)2176, ReadFrame(frames[index]).Header.Opcode));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[hitpointIndices[0]]), out var first));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[hitpointIndices[1]]), out var second));
        Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints], first.Updates.Select(entry => entry.Type));
        Assert.Equal(
            [LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints],
            second.Updates.Select(entry => entry.Type));
        Assert.Equal(100_000UL, first.Updates[0].Value);
        Assert.Equal(first.Updates[0].Value, second.Updates[0].Value);
        Assert.Equal(100_000U - result.ExpectedAppliedDamage, second.Updates[1].Value);
        var animationIndex = SingleFrameIndex(
            frames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == StarArrowRuntimeSpellId);
        Assert.All(hitpointIndices, index => Assert.True(index < animationIndex));
        return hitpointIndices[0];
    }

    private static int AssertStarArrowReceiveEnvelopeArchive(
        IReadOnlyList<byte[]> frames,
        uint uid,
        int flag,
        uint duration,
        uint damage)
    {
        return SingleFrameIndex(
            frames,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == uid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == flag &&
                    entry.Time == duration &&
                    entry.Damage == damage));
    }

    private static int AssertStarArrowReceiveEnvelopePull(IReadOnlyList<byte[]> frames)
    {
        return SingleFrameIndex(
            frames,
            frame => LegacyPlayerSpawnPacket.TryParse(frame, out var spawn) &&
                spawn.GetNumericField(StarArrowPlayerSpawnUidField) == TargetPlayerUid &&
                spawn.GetNumericField(StarArrowPlayerSpawnXField) == 830 &&
                spawn.GetNumericField(StarArrowPlayerSpawnYField) == 584);
    }

    private static GouyuImmortal CreateStarArrowReceiveEnvelopeGouyuImmortal()
    {
        var values = Enumerable.Repeat(0, 15).ToArray();
        values[0] = 17;
        values[5] = 5_000;
        values[6] = 10;
        values[7] = 0;
        return new GouyuImmortal(
            Id: 17,
            Values: values,
            SourceFile: "fixture://gouyu_immortal",
            LineNumber: 1,
            RawLine: "",
            Fields: []);
    }

    private sealed record StarArrowReceiveEnvelopeResult(
        WorldSession Caster,
        WorldSession Target,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        List<byte[]> DeliveryOrder,
        uint RequestedDamage,
        uint ExpectedAppliedDamage,
        MsgAttackEffect Effect,
        string Log);
}
