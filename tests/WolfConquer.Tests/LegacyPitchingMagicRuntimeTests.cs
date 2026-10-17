using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort PitchingSpellId = 14_270;
    private const byte PitchingSort = 92;
    private const int PitchingCycloneFlag = 0x17;
    private const int PitchingSupermanFlag = 0x12;
    private const int PitchingXpListFlag = 0x04;

    [Fact]
    public async Task HandlerPitching14270SuccessPreparesResourcesRemovesXpStateThenCentersTargetAnimation()
    {
        var result = await RunPitchingCastAsync(
            casterBattlePower: 100,
            targetBattlePower: 101,
            forcedRoll: 94,
            targetAttributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(
                    PitchingXpListFlag,
                    PitchingCycloneFlag,
                    PitchingSupermanFlag),
                ["XPCount"] = "37",
                ["LegacyCycloneExpiresAtUtc"] = DateTimeOffset.UtcNow.AddSeconds(20).ToString("O"),
                ["LegacySupermanExpiresAtUtc"] = DateTimeOffset.UtcNow.AddSeconds(20).ToString("O"),
            });

        Assert.Equal(493, result.CasterSession.Character.MinMana);
        Assert.Equal("97", result.CasterSession.Character.Attributes["Stamina"]);
        Assert.True(result.CasterSession.MagicCooldowns?.ContainsKey(PitchingSpellId));

        var targetSession = Assert.IsType<WorldSession>(result.TargetSession);
        Assert.False(HasLegacyStatusFlag(targetSession.Character.Attributes, PitchingCycloneFlag));
        Assert.False(HasLegacyStatusFlag(targetSession.Character.Attributes, PitchingSupermanFlag));
        Assert.False(HasLegacyStatusFlag(targetSession.Character.Attributes, PitchingXpListFlag));
        Assert.Equal("0", targetSession.Character.Attributes["XPCount"]);
        Assert.False(targetSession.Character.Attributes.ContainsKey("LegacyCycloneExpiresAtUtc"));
        Assert.False(targetSession.Character.Attributes.ContainsKey("LegacySupermanExpiresAtUtc"));

        var targetUpdates = ParseMsgUpdates(result.TargetSent);
        var targetStatusUpdates = targetUpdates
            .Where(update => update.Uid == TargetPlayerUid)
            .SelectMany(update => update.Updates)
            .Where(update => update.Type == LegacyMsgUpdateDataType.StatusFlag)
            .ToArray();
        Assert.Equal(2, targetStatusUpdates.Length);
        Assert.NotNull(targetStatusUpdates[0].Values);
        Assert.False(IsLegacyStatusFlagSet(targetStatusUpdates[0].Values!, PitchingCycloneFlag));
        Assert.False(IsLegacyStatusFlagSet(targetStatusUpdates[0].Values!, PitchingSupermanFlag));
        Assert.True(IsLegacyStatusFlagSet(targetStatusUpdates[0].Values!, PitchingXpListFlag));
        Assert.NotNull(targetStatusUpdates[1].Values);
        Assert.False(IsLegacyStatusFlagSet(targetStatusUpdates[1].Values!, PitchingCycloneFlag));
        Assert.False(IsLegacyStatusFlagSet(targetStatusUpdates[1].Values!, PitchingSupermanFlag));
        Assert.False(IsLegacyStatusFlagSet(targetStatusUpdates[1].Values!, PitchingXpListFlag));
        Assert.Contains(targetUpdates, update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)]));
        Assert.DoesNotContain(targetUpdates, update =>
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.XPCircle));

        var animation = Assert.Single(ParseSpellAnimations(result.CasterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(TargetPlayerUid, animation.AttackedUid);
        Assert.Equal(833, animation.X);
        Assert.Equal(586, animation.Y);
        Assert.Equal(PitchingSpellId, animation.SpellId);
        var animationTarget = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, animationTarget.Uid);
        Assert.Equal(0U, animationTarget.Damage);
        Assert.True(animationTarget.Hit);
        Assert.Equal(MsgAttackEffect.None, animationTarget.Effect);
        Assert.Single(ParseSpellAnimations(result.TargetSent));
        Assert.Empty(ParseLegacySpellPackets(result.CasterSent));

        var staminaIndex = FindPitchingFrameIndex(result.CasterSent, frame =>
            IsPitchingScalarUpdate(frame, PlayerUid, LegacyMsgUpdateDataType.Stamina, 97));
        var manaIndex = FindPitchingFrameIndex(result.CasterSent, frame =>
            IsPitchingScalarUpdate(frame, PlayerUid, LegacyMsgUpdateDataType.Mana, 493));
        var cooldownIndex = FindPitchingFrameIndex(result.CasterSent, frame =>
            LegacyMagicColdTimePacket.TryParse(frame, out var packet) &&
            packet.Spells.SequenceEqual([new LegacyMagicColdTimeSpell(PitchingSpellId, 300)]));
        var firstTargetStatusIndex = FindPitchingFrameIndex(result.CasterSent, frame =>
            IsPitchingStatusUpdate(frame, TargetPlayerUid));
        var animationIndex = FindPitchingFrameIndex(result.CasterSent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) && packet.SpellId == PitchingSpellId);
        Assert.True(staminaIndex >= 0);
        Assert.True(manaIndex > staminaIndex);
        Assert.True(cooldownIndex > manaIndex);
        Assert.True(firstTargetStatusIndex > cooldownIndex);
        Assert.True(animationIndex > firstTargetStatusIndex);

        var firstTargetStatusPacketIndex = FindPitchingFrameIndex(result.TargetSent, frame =>
            IsPitchingStatusUpdate(frame, TargetPlayerUid));
        var xpListIndex = FindPitchingFrameIndex(result.TargetSent, frame =>
            IsPitchingScalarUpdate(frame, TargetPlayerUid, LegacyMsgUpdateDataType.XPList, 0));
        var secondTargetStatusPacketIndex = FindPitchingFrameIndex(
            result.TargetSent,
            frame => IsPitchingStatusUpdate(frame, TargetPlayerUid),
            startIndex: firstTargetStatusPacketIndex + 1);
        var targetAnimationIndex = FindPitchingFrameIndex(result.TargetSent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) && packet.SpellId == PitchingSpellId);
        Assert.True(firstTargetStatusPacketIndex >= 0);
        Assert.True(xpListIndex > firstTargetStatusPacketIndex);
        Assert.True(secondTargetStatusPacketIndex > xpListIndex);
        Assert.True(targetAnimationIndex > secondTargetStatusPacketIndex);

        Assert.Contains("WorldServer handled legacy Pitching magic", result.Log);
        Assert.Contains("successRate=95 succeeded=True", result.Log);
        Assert.Contains("removedXpSkillCount=2", result.Log);
        Assert.Contains("removedXpList=True xpCountReset=True", result.Log);
        Assert.Contains("source=Pitching.cs case 14270", result.Log);
    }

    [Theory]
    [InlineData(100, 101, 94, true)]
    [InlineData(100, 101, 95, false)]
    [InlineData(100, 120, 0, false)]
    [InlineData(120, 100, 99, true)]
    public async Task HandlerPitching14270UsesExactBattlePowerProbabilityExpression(
        int casterBattlePower,
        int targetBattlePower,
        uint forcedRoll,
        bool expectedHit)
    {
        var result = await RunPitchingCastAsync(
            casterBattlePower,
            targetBattlePower,
            forcedRoll,
            targetAttributes: new Dictionary<string, string>
            {
                ["XPCount"] = "19",
            });

        var animation = Assert.Single(ParseSpellAnimations(result.CasterSent));
        var target = Assert.Single(animation.Targets);
        Assert.Equal(expectedHit, target.Hit);
        Assert.Equal(0U, target.Damage);

        var targetSession = Assert.IsType<WorldSession>(result.TargetSession);
        Assert.Equal(expectedHit ? "0" : "19", targetSession.Character.Attributes["XPCount"]);
        Assert.DoesNotContain(ParseMsgUpdates(result.TargetSent), update =>
            update.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.StatusFlag or
                    LegacyMsgUpdateDataType.XPList or
                    LegacyMsgUpdateDataType.XPCircle));

        var expectedRate = 100 - (5 * (targetBattlePower - casterBattlePower));
        Assert.Contains($"successRate={expectedRate} succeeded={expectedHit}", result.Log);
    }

    [Fact]
    public async Task HandlerPitching14270MissingVisiblePlayerStillPreparesResourcesAndCooldownWithoutAnimation()
    {
        var result = await RunPitchingCastAsync(
            casterBattlePower: 100,
            targetBattlePower: 100,
            forcedRoll: 0,
            targetMapId: 1_002,
            targetAttributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(PitchingXpListFlag, PitchingSupermanFlag),
                ["XPCount"] = "27",
            });

        Assert.Equal(493, result.CasterSession.Character.MinMana);
        Assert.Equal("97", result.CasterSession.Character.Attributes["Stamina"]);
        Assert.True(result.CasterSession.MagicCooldowns?.ContainsKey(PitchingSpellId));
        Assert.Single(ParseMagicColdTimePackets(result.CasterSent));
        Assert.Empty(ParseSpellAnimations(result.CasterSent));
        Assert.Empty(result.TargetSent);

        var targetSession = Assert.IsType<WorldSession>(result.TargetSession);
        Assert.True(HasLegacyStatusFlag(targetSession.Character.Attributes, PitchingSupermanFlag));
        Assert.True(HasLegacyStatusFlag(targetSession.Character.Attributes, PitchingXpListFlag));
        Assert.Equal("27", targetSession.Character.Attributes["XPCount"]);
        Assert.Contains("source Pitching.cs returns after CanUseSpell.Verified when the visible player target is absent", result.Log);
    }

    private static async Task<LegacyPitchingCastResult> RunPitchingCastAsync(
        int casterBattlePower,
        int targetBattlePower,
        uint forcedRoll,
        IReadOnlyDictionary<string, string>? targetAttributes = null,
        long targetMapId = 1_015)
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                PitchingSpellId,
                sort: PitchingSort,
                useMana: 7,
                useStamina: 3,
                coolDown: 1_000,
                coldTime: 300));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            minMana: 500,
            stamina: 100,
            spellEntries: [CreatePlayerSpell(PitchingSpellId)],
            attributes: new Dictionary<string, string>
            {
                ["BattlePower"] = casterBattlePower.ToString(),
                [WorldPacketHandlers.RuntimeLegacyPitchingRollAttribute] = forcedRoll.ToString(),
            });
        var targetRuntimeAttributes = targetAttributes is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(targetAttributes);
        targetRuntimeAttributes["BattlePower"] = targetBattlePower.ToString();
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "PitchingTarget",
            x: 833,
            y: 586,
            attributes: targetRuntimeAttributes,
            mapId: targetMapId);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 700,
                Y: 701,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: PitchingSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        return new LegacyPitchingCastResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent.ToArray(),
            targetSent.ToArray(),
            log.ToString());
    }

    private static int FindPitchingFrameIndex(
        IReadOnlyList<byte[]> frames,
        Func<PacketFrame, bool> predicate,
        int startIndex = 0)
    {
        for (var index = Math.Max(0, startIndex); index < frames.Count; index++)
        {
            if (predicate(ReadFrame(frames[index])))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsPitchingScalarUpdate(
        PacketFrame frame,
        uint uid,
        LegacyMsgUpdateDataType type,
        ulong value)
    {
        return LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
            update.Uid == uid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(type, value)]);
    }

    private static bool IsPitchingStatusUpdate(PacketFrame frame, uint uid)
    {
        return LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
            update.Uid == uid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag);
    }

    private readonly record struct LegacyPitchingCastResult(
        WorldSession CasterSession,
        WorldSession? TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Log);
}
