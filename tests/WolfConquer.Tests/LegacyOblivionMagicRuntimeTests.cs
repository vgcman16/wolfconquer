using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort OblivionSpellId = 10_390;
    private const byte OblivionSort = 53;
    private const int OblivionColdTimeMilliseconds = 45_000;
    private const string OblivionExpiresAtUtcAttribute = "LegacyOblivionExpiresAtUtc";

    [Fact]
    public async Task HandlerOblivion10390ReplacesXpSkillOpensThirtySecondStatusAndPreservesPacketOrder()
    {
        var result = await RunOblivionCastAsync(
            new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(
                    LegacyXpListFlag,
                    LegacySupermanFlag),
                ["XPCount"] = "37",
                [RuntimeLegacySupermanExpiresAtUtcAttribute] = DateTimeOffset.UtcNow
                    .AddSeconds(20)
                    .ToString("O"),
            });

        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyOblivionFlag));
        Assert.Equal("0", result.Session.Character.Attributes["XPCount"]);
        Assert.False(result.Session.Character.Attributes.ContainsKey(RuntimeLegacySupermanExpiresAtUtcAttribute));
        AssertLegacyXpSkillExpiresAround(
            result.Session.Character.Attributes,
            OblivionExpiresAtUtcAttribute,
            result.BeforeCast,
            result.AfterCast,
            expectedDurationSeconds: 30);
        Assert.True(result.Session.MagicCooldowns?.ContainsKey(OblivionSpellId));

        var coldTime = Assert.Single(ParseMagicColdTimePackets(result.Sent));
        Assert.Equal(
            [new LegacyMagicColdTimeSpell(OblivionSpellId, OblivionColdTimeMilliseconds)],
            coldTime.Spells);

        var updates = ParseMsgUpdates(result.Sent);
        Assert.Contains(updates, update =>
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)]));
        Assert.Contains(updates, update =>
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPCircle, 0)]));

        var statusUpdates = updates
            .Where(IsOblivionStatusUpdate)
            .Select(update => Assert.Single(update.Updates).Values)
            .ToArray();
        Assert.Equal(3, statusUpdates.Length);
        Assert.NotNull(statusUpdates[0]);
        Assert.False(IsLegacyStatusFlagSet(statusUpdates[0]!, LegacyXpListFlag));
        Assert.True(IsLegacyStatusFlagSet(statusUpdates[0]!, LegacySupermanFlag));
        Assert.False(IsLegacyStatusFlagSet(statusUpdates[0]!, LegacyOblivionFlag));
        Assert.NotNull(statusUpdates[1]);
        Assert.False(IsLegacyStatusFlagSet(statusUpdates[1]!, LegacyXpListFlag));
        Assert.False(IsLegacyStatusFlagSet(statusUpdates[1]!, LegacySupermanFlag));
        Assert.False(IsLegacyStatusFlagSet(statusUpdates[1]!, LegacyOblivionFlag));
        Assert.NotNull(statusUpdates[2]);
        Assert.False(IsLegacyStatusFlagSet(statusUpdates[2]!, LegacyXpListFlag));
        Assert.False(IsLegacyStatusFlagSet(statusUpdates[2]!, LegacySupermanFlag));
        Assert.True(IsLegacyStatusFlagSet(statusUpdates[2]!, LegacyOblivionFlag));

        var animation = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(832, animation.X);
        Assert.Equal(586, animation.Y);
        Assert.Equal(OblivionSpellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(3, animation.LevelHu);
        Assert.Equal(0, animation.SpellEffect);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Empty(ParseLegacySpellPackets(result.Sent));

        var coldTimeIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => LegacyMagicColdTimePacket.TryParse(frame, out _));
        var xpListIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => IsOblivionScalarUpdate(frame, LegacyMsgUpdateDataType.XPList));
        var firstStatusIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => IsOblivionStatusUpdate(frame));
        var xpCircleIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => IsOblivionScalarUpdate(frame, LegacyMsgUpdateDataType.XPCircle));
        var secondStatusIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => IsOblivionStatusUpdate(frame),
            firstStatusIndex + 1);
        var thirdStatusIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => IsOblivionStatusUpdate(frame),
            secondStatusIndex + 1);
        var animationIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == OblivionSpellId);
        Assert.True(coldTimeIndex >= 0);
        Assert.True(xpListIndex > coldTimeIndex);
        Assert.True(firstStatusIndex > xpListIndex);
        Assert.True(xpCircleIndex > firstStatusIndex);
        Assert.True(secondStatusIndex > xpCircleIndex);
        Assert.True(thirdStatusIndex > secondStatusIndex);
        Assert.True(animationIndex > thirdStatusIndex);

        Assert.Contains("WorldServer handled legacy Oblivion magic", result.Log);
        Assert.Contains("consumedXpList=True autoHunt=False", result.Log);
        Assert.Contains($"replacedXpSkillFlag={LegacySupermanFlag}", result.Log);
        Assert.Contains("openedOblivion=True refreshedOblivion=False", result.Log);
        Assert.Contains("durationSeconds=30; source=Oblivion.cs", result.Log);
    }

    [Fact]
    public async Task HandlerOblivion10390WithoutXpListStillOpensStatusWithoutXpListUpdate()
    {
        var result = await RunOblivionCastAsync(
            new Dictionary<string, string>
            {
                ["XPCount"] = "19",
            });

        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyOblivionFlag));
        Assert.Equal("0", result.Session.Character.Attributes["XPCount"]);
        AssertLegacyXpSkillExpiresAround(
            result.Session.Character.Attributes,
            OblivionExpiresAtUtcAttribute,
            result.BeforeCast,
            result.AfterCast,
            expectedDurationSeconds: 30);

        var updates = ParseMsgUpdates(result.Sent);
        Assert.DoesNotContain(updates, update =>
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.XPList));
        Assert.Contains(updates, update =>
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPCircle, 0)]));
        Assert.Single(updates, IsOblivionStatusUpdate);
        Assert.Single(ParseSpellAnimations(result.Sent));

        var coldTimeIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => LegacyMagicColdTimePacket.TryParse(frame, out _));
        var xpCircleIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => IsOblivionScalarUpdate(frame, LegacyMsgUpdateDataType.XPCircle));
        var statusIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => IsOblivionStatusUpdate(frame));
        var animationIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == OblivionSpellId);
        Assert.True(coldTimeIndex >= 0);
        Assert.True(xpCircleIndex > coldTimeIndex);
        Assert.True(statusIndex > xpCircleIndex);
        Assert.True(animationIndex > statusIndex);
        Assert.Contains("consumedXpList=False autoHunt=False", result.Log);
    }

    [Fact]
    public async Task HandlerOblivion10390DuringAutoHuntOnlyConsumesXpListThenSendsAnimation()
    {
        var result = await RunOblivionCastAsync(
            new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(
                    LegacyXpListFlag,
                    LegacySupermanFlag),
                ["XPCount"] = "55",
                ["OnAutoHunt"] = "1",
            });

        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacySupermanFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, LegacyOblivionFlag));
        Assert.Equal("55", result.Session.Character.Attributes["XPCount"]);
        Assert.False(result.Session.Character.Attributes.ContainsKey(OblivionExpiresAtUtcAttribute));

        var updates = ParseMsgUpdates(result.Sent);
        Assert.Contains(updates, update =>
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)]));
        Assert.DoesNotContain(updates, update =>
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.XPCircle));
        var status = Assert.Single(updates, IsOblivionStatusUpdate);
        var statusValues = Assert.Single(status.Updates).Values;
        Assert.NotNull(statusValues);
        Assert.False(IsLegacyStatusFlagSet(statusValues!, LegacyXpListFlag));
        Assert.True(IsLegacyStatusFlagSet(statusValues!, LegacySupermanFlag));
        Assert.False(IsLegacyStatusFlagSet(statusValues!, LegacyOblivionFlag));
        Assert.Single(ParseSpellAnimations(result.Sent));

        var coldTimeIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => LegacyMagicColdTimePacket.TryParse(frame, out _));
        var xpListIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => IsOblivionScalarUpdate(frame, LegacyMsgUpdateDataType.XPList));
        var statusIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => IsOblivionStatusUpdate(frame));
        var animationIndex = FindOblivionFrameIndex(
            result.Sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == OblivionSpellId);
        Assert.True(coldTimeIndex >= 0);
        Assert.True(xpListIndex > coldTimeIndex);
        Assert.True(statusIndex > xpListIndex);
        Assert.True(animationIndex > statusIndex);
        Assert.Contains("consumedXpList=True autoHunt=True", result.Log);
        Assert.Contains("openedOblivion=False refreshedOblivion=False", result.Log);
    }

    [Fact]
    public async Task RuntimeLoopExpiresOblivionStatusAtItsStoredDeadline()
    {
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var now = DateTimeOffset.UtcNow;
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(now.AddSeconds(10))),
            log: log);
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyOblivionFlag),
                [OblivionExpiresAtUtcAttribute] = now.AddSeconds(-1).ToString("O"),
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, LegacyOblivionFlag) &&
                        !current.Character.Attributes.ContainsKey(OblivionExpiresAtUtcAttribute);
                },
                TimeSpan.FromSeconds(1));

            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, LegacyOblivionFlag)));
            Assert.Contains("WorldServer expired legacy XP skill flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static async Task<LegacyOblivionCastResult> RunOblivionCastAsync(
        IReadOnlyDictionary<string, string> attributes)
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                OblivionSpellId,
                sort: OblivionSort,
                useMana: 0,
                damage: 33,
                needLevel: 15,
                useStamina: 0,
                coolDown: 0,
                coldTime: OblivionColdTimeMilliseconds,
                duration: 30,
                gDamage: 100));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            x: 830,
            y: 584,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    OblivionSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 3,
                    UseSpellSoul: 3),
            ],
            attributes: attributes);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            OblivionSpellId,
            out var playerSpell,
            out var spell));
        var query = new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: 0,
            X: 832,
            Y: 586,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: OblivionSpellId);

        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerOblivionMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var beforeCast = DateTimeOffset.UtcNow;
        var invocation = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        var handled = await Assert.IsType<ValueTask<bool>>(invocation);
        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(handled);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new LegacyOblivionCastResult(
            updatedSession,
            sent.ToArray(),
            log.ToString(),
            beforeCast,
            afterCast);
    }

    private static bool IsOblivionStatusUpdate(LegacyMsgUpdatePacket update)
    {
        return update.Uid == PlayerUid &&
            update.Updates.Count == 1 &&
            update.Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag;
    }

    private static bool IsOblivionStatusUpdate(PacketFrame frame)
    {
        return LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
            IsOblivionStatusUpdate(update);
    }

    private static bool IsOblivionScalarUpdate(
        PacketFrame frame,
        LegacyMsgUpdateDataType type)
    {
        return LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(type, 0)]);
    }

    private static int FindOblivionFrameIndex(
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

    private readonly record struct LegacyOblivionCastResult(
        WorldSession Session,
        IReadOnlyList<byte[]> Sent,
        string Log,
        DateTimeOffset BeforeCast,
        DateTimeOffset AfterCast);
}
