using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int HellVortexInstanceGateSolidFlag = 0x164;

    [Fact]
    public async Task HandlerHellVortexCrossInstanceSelectedPlayerCentersOnlyOwnInstanceRecipients()
    {
        var result = await RunHellVortexInstanceGateAsync(crossInstanceSelected: true);

        // Source TryGetValue does not call CanSee; only the enumerated recipients are instance-filtered.
        var animation = AssertHellVortexInstanceGateAnimation(result);
        Assert.Equal(TargetPlayerUid + 1, Assert.Single(animation.Targets).Uid);
        AssertHellVortexInstanceGateUntouched(result, result.Targets[0]);
        AssertHellVortexInstanceGateReceived(result, result.Targets[1], animation);
        AssertHellVortexInstanceGateUntouched(result, result.Targets[2]);
        Assert.Equal("71", result.Targets[1].Session.Character.Attributes["DynamicID"]);
        Assert.Equal("72", result.Targets[0].Session.Character.Attributes["DynamicID"]);
    }

    [Fact]
    public async Task HandlerHellVortexValidSelectedPlayerExcludesOtherInstanceBeforeWildPhoenixAndSolid()
    {
        var result = await RunHellVortexInstanceGateAsync();

        var animation = AssertHellVortexInstanceGateAnimation(result);
        Assert.Equal(new[] { TargetPlayerUid, TargetPlayerUid + 1 }, animation.Targets.Select(hit => hit.Uid));
        foreach (var target in result.Targets.Take(2))
        {
            AssertHellVortexInstanceGateReceived(result, target, animation);
        }
        AssertHellVortexInstanceGateUntouched(result, result.Targets[2]);
    }

    [Fact]
    public async Task HandlerHellVortexSelectedInstanceChangeDoesNotInvalidateRawCenterLookup()
    {
        var result = await RunHellVortexInstanceGateAsync(moveSelectedAfterFirstHit: true);

        Assert.Equal(1, result.InstanceMoves);
        Assert.Equal("72", result.Targets[0].Session.Character.Attributes["DynamicID"]);
        var animation = AssertHellVortexInstanceGateAnimation(result);
        Assert.Equal(new[] { TargetPlayerUid, TargetPlayerUid + 1 }, animation.Targets.Select(hit => hit.Uid));
        AssertHellVortexInstanceGateReceived(result, result.Targets[0], animation);
        AssertHellVortexInstanceGateReceived(result, result.Targets[1], animation);
        AssertHellVortexInstanceGateUntouched(result, result.Targets[2]);
    }

    [Theory]
    [InlineData(830, 853, 847, true)]
    [InlineData(845, 864, 858, false)]
    public async Task HandlerHellVortexSelectedPlayerUsesNeighboringBlocksRatherThanViewDistance(
        int casterX, int selectedX, int bystanderX, bool receives)
    {
        var result = await RunHellVortexInstanceGateAsync(
            casterX: casterX, selectedX: selectedX, bystanderX: bystanderX);

        var animation = AssertHellVortexInstanceGateAnimation(result);
        AssertHellVortexInstanceGateUntouched(result, result.Targets[0]);
        AssertHellVortexInstanceGateUntouched(result, result.Targets[2]);
        if (receives)
        {
            Assert.Equal(TargetPlayerUid + 1, Assert.Single(animation.Targets).Uid);
            AssertHellVortexInstanceGateReceived(result, result.Targets[1], animation);
        }
        else
        {
            Assert.Empty(animation.Targets);
            AssertHellVortexInstanceGateUntouched(result, result.Targets[1]);
        }
    }

    [Fact]
    public async Task HandlerHellVortexImmortalForceGatePublishesForbiddenBeforeLaterReceive()
    {
        var result = await RunHellVortexInstanceGateAsync(selectedImmortalForce: true);
        var animation = AssertHellVortexInstanceGateAnimation(result);
        Assert.Equal(TargetPlayerUid + 1, Assert.Single(animation.Targets).Uid);
        AssertHellVortexInstanceGateUntouched(result, result.Targets[0]);
        AssertHellVortexInstanceGateReceived(result, result.Targets[1], animation);
        AssertHellVortexInstanceGateUntouched(result, result.Targets[2]);
        var forbiddenIndex = SingleFrameIndex(result.CasterSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == PlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ForbiddenPirate && entry.Value == 1));
        Assert.Equal((ushort)2176, ReadFrame(result.CasterSent[forbiddenIndex]).Header.Opcode);
        var solidIndex = SingleFrameIndex(result.Targets[1].Sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == TargetPlayerUid + 1 &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == HellVortexInstanceGateSolidFlag));
        Assert.True(result.DeliveryOrder.ToList().IndexOf(result.CasterSent[forbiddenIndex]) <
            result.DeliveryOrder.ToList().IndexOf(result.Targets[1].Sent[solidIndex]));
    }

    private static async Task<HellVortexInstanceGateResult> RunHellVortexInstanceGateAsync(
        bool crossInstanceSelected = false,
        bool moveSelectedAfterFirstHit = false,
        int casterX = 830,
        int selectedX = 840,
        int bystanderX = 841,
        bool selectedImmortalForce = false)
    {
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [],
            CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 0, useStamina: 0,
                damage: 200, coldTime: 0, damage3: 30_030),
            CreateMagicRow(WildPhoenixSpellId, sort: 6, useMana: 0, damage: 4, coldTime: 0,
                duration: 32, damageOnHuman: 18, damage2: 7));
        var caster = CreateSession(x: casterX, y: 584, strength: 301, classCode: 5_005,
            spellIds: [HellVortexSpellId],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DynamicID"] = "71",
                ["PkMode"] = "0",
            });
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new[] { new List<byte[]>(), new List<byte[]>(), new List<byte[]>() };
        var deliveryOrder = new List<byte[]>();
        List<byte[]>? selectedSent = null;
        ConquerTcpConnection? selectedConnection = null;
        var instanceMoves = 0;
        ConquerTcpConnection Capture(List<byte[]> sent) => new(
            localEndPoint: null,
            remoteEndPoint: null,
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                sent.Add(bytes);
                deliveryOrder.Add(bytes);
                if (moveSelectedAfterFirstHit && instanceMoves == 0 &&
                    ReferenceEquals(sent, selectedSent) && selectedConnection is not null &&
                    LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                    update.Uid == TargetPlayerUid &&
                    update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints))
                {
                    // Re-enter at the completed first receive's publication boundary, without a timer race.
                    Assert.True(sessions.TryGet(selectedConnection, out var selected));
                    Assert.NotNull(selected);
                    Assert.True(selected.Character.MinHitPoints < 100_000);
                    sessions.Set(selectedConnection, selected with
                    {
                        Character = selected.Character with
                        {
                            Attributes = new Dictionary<string, string>(selected.Character.Attributes,
                                StringComparer.OrdinalIgnoreCase)
                            { ["DynamicID"] = "72" },
                        },
                    });
                    instanceMoves++;
                }
                return ValueTask.CompletedTask;
            });
        var casterConnection = Capture(casterSent);
        var targetConnections = targetSent.Select(Capture).ToArray();
        sessions.Set(casterConnection, caster);
        for (var index = 0; index < targetConnections.Length; index++)
        {
            sessions.Set(targetConnections[index], CreateSession(
                uid: TargetPlayerUid + index, name: "HellVortexInstancePlaceholder" + index, spellIds: []));
        }

        // RoleView.Roles order is not UID order; assign selected, bystander, foreign roles to observed slots.
        var indices = GetVisibleTargetOrder(sessions, casterConnection, PlayerUid)
            .Select(uid => checked((int)(uid - TargetPlayerUid))).ToArray();
        Assert.Equal(3, indices.Length);
        selectedConnection = targetConnections[indices[0]];
        selectedSent = targetSent[indices[0]];
        for (var role = 0; role < indices.Length; role++)
        {
            var foreign = role == 2;
            var target = CreateSession(
                uid: TargetPlayerUid + role, name: "HellVortexInstanceTarget" + role,
                x: role == 0 ? selectedX : bystanderX, y: 584, minHitPoints: 100_000,
                spellIds: foreign ? [WildPhoenixSpellId] : [],
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DynamicID"] = foreign || (role == 0 && crossInstanceSelected) ? "72" : "71",
                    ["MaxHitpoints"] = "100000",
                    ["StatusFlags"] = foreign ? BuildStatusFlags(LegacyWildPhoenixFlag) :
                        role == 0 && selectedImmortalForce ? BuildStatusFlags(LegacyImmortalForceFlag) : BuildStatusFlags(),
                    [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                    [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = foreign ? "1" : "0",
                    ["LegacySpeffectLevel.Solid"] = "4",
                    ["LegacySpeffectRoll.Solid"] = "0",
                    ["LegacySpeffectLevel.Bloodthirst"] = "4",
                    ["LegacySpeffectRoll.Bloodthirst"] = "0",
                });
            sessions.Set(targetConnections[indices[role]], target);
        }
        Assert.Equal(new[] { TargetPlayerUid, TargetPlayerUid + 1, TargetPlayerUid + 2 },
            GetVisibleTargetOrder(sessions, casterConnection, PlayerUid));

        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(casterConnection, log, caster,
            WorldData: worldData, Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(), SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 900, Y: 901,
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                SpellId: HellVortexSpellId)).ToFrameBytes()));

        var targets = indices.Select((index, role) =>
        {
            Assert.True(sessions.TryGet(targetConnections[index], out var target));
            Assert.NotNull(target);
            return new HellVortexInstanceGateTarget(checked(TargetPlayerUid + (uint)role), target, targetSent[index]);
        }).ToArray();
        GC.KeepAlive(targetConnections);
        return new(casterSent, targets, deliveryOrder, instanceMoves);
    }

    private static LegacySpellAnimationPacket AssertHellVortexInstanceGateAnimation(HellVortexInstanceGateResult result)
    {
        var animation = Assert.Single(ParseSpellAnimations(result.CasterSent));
        var index = SingleFrameIndex(result.CasterSent, frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        Assert.Equal((ushort)2239, ReadFrame(result.CasterSent[index]).Header.Opcode);
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((900, 901), (animation.X, animation.Y));
        Assert.Equal(HellVortexSpellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(0, animation.LevelHu);
        Assert.Equal(0, animation.BombFlag);
        return animation;
    }

    private static void AssertHellVortexInstanceGateUntouched(
        HellVortexInstanceGateResult result, HellVortexInstanceGateTarget target)
    {
        Assert.Equal(100_000, target.Session.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(target.Session.Character.Attributes, HellVortexInstanceGateSolidFlag));
        Assert.False(target.Session.Character.Attributes.ContainsKey("Solid"));
        var foreign = target.Uid == TargetPlayerUid + 2;
        Assert.Equal(foreign ? "1" : "0",
            target.Session.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.Equal(foreign, HasLegacyStatusFlag(target.Session.Character.Attributes, LegacyWildPhoenixFlag));
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder), update => update.Uid == target.Uid &&
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.StatusFlag or
                LegacyMsgUpdateDataType.ArchiveSkill or LegacyMsgUpdateDataType.BloodthirstElan or
                LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
    }

    private static void AssertHellVortexInstanceGateReceived(
        HellVortexInstanceGateResult result, HellVortexInstanceGateTarget target, LegacySpellAnimationPacket animation)
    {
        var hit = Assert.Single(animation.Targets, hit => hit.Uid == target.Uid);
        Assert.True(hit.Hit);
        Assert.True(hit.Damage > 0);
        Assert.Equal(100_000L - hit.Damage, target.Session.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(target.Session.Character.Attributes, HellVortexInstanceGateSolidFlag));
        Assert.Equal("2400", target.Session.Character.Attributes["Solid"]);
        var solidIndex = SingleFrameIndex(target.Sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == target.Uid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == HellVortexInstanceGateSolidFlag && entry.Time == 5 && entry.Damage == 2_400));
        var hpIndex = SingleFrameIndex(target.Sent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == target.Uid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                entry.Value == checked((ulong)target.Session.Character.MinHitPoints)));
        Assert.Equal((ushort)2176, ReadFrame(target.Sent[solidIndex]).Header.Opcode);
        Assert.Equal((ushort)2176, ReadFrame(target.Sent[hpIndex]).Header.Opcode);
        Assert.True(solidIndex < hpIndex);
        var animationIndex = SingleFrameIndex(result.CasterSent, frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        Assert.True(result.DeliveryOrder.ToList().IndexOf(target.Sent[hpIndex]) <
            result.DeliveryOrder.ToList().IndexOf(result.CasterSent[animationIndex]));
    }

    private sealed record HellVortexInstanceGateTarget(uint Uid, WorldSession Session, IReadOnlyList<byte[]> Sent);

    private sealed record HellVortexInstanceGateResult(
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<HellVortexInstanceGateTarget> Targets,
        IReadOnlyList<byte[]> DeliveryOrder,
        int InstanceMoves);
}
