using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort InfinityAttachStatusSpellId = 14_380;
    private const int FlyStatusFlag = 0x1B;
    private const int InfinityStatusFlag = 0xD7;

    [Fact]
    public async Task HandlerInfinity14380SelfCastAppliesFlyInfinityDamageAnimationAndInfinityUpdate()
    {
        const uint duration = 23;
        const int spellDamagePercent = 125;
        const int monsterDamagePercent = 175;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                InfinityAttachStatusSpellId,
                sort: 6,
                useMana: 0,
                coldTime: 0,
                duration: duration,
                damageOnHuman: spellDamagePercent,
                damageOnMonster: monsterDamagePercent));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [InfinityAttachStatusSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: PlayerUid,
                X: 841,
                Y: 588,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: InfinityAttachStatusSpellId)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, FlyStatusFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, InfinityStatusFlag));
        Assert.Equal(spellDamagePercent.ToString(), updatedSession.Character.Attributes["InfinitySDamage"]);
        Assert.Equal(monsterDamagePercent.ToString(), updatedSession.Character.Attributes["InfinityDamage"]);
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyFlyRemoveOnDead"]);
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyInfinityRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyFlyExpiresAtUtc",
            beforeCast,
            afterCast,
            expectedDurationSeconds: checked((int)duration));
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyInfinityExpiresAtUtc",
            beforeCast,
            afterCast,
            expectedDurationSeconds: checked((int)duration));

        var sentSnapshot = sent.ToArray();
        var interactionIndex = SingleFrameIndex(
            sentSnapshot,
            frame => MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == InfinityAttachStatusSpellId);
        var statusIndexes = sentSnapshot
            .Select((bytes, index) => new
            {
                Frame = ReadFrame(bytes),
                Index = index,
            })
            .Where(item => LegacyMsgUpdatePacket.TryParse(item.Frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag))
            .Select(item => item.Index)
            .ToArray();
        Assert.Equal(2, statusIndexes.Length);
        var firstStatus = ParseMsgUpdates([sentSnapshot[statusIndexes[0]]]).Single();
        var secondStatus = ParseMsgUpdates([sentSnapshot[statusIndexes[1]]]).Single();
        var firstValues = Assert.Single(firstStatus.Updates).Values;
        var secondValues = Assert.Single(secondStatus.Updates).Values;
        Assert.NotNull(firstValues);
        Assert.NotNull(secondValues);
        Assert.True(IsLegacyStatusFlagSet(firstValues!, FlyStatusFlag));
        Assert.False(IsLegacyStatusFlagSet(firstValues, InfinityStatusFlag));
        Assert.True(IsLegacyStatusFlagSet(secondValues!, FlyStatusFlag));
        Assert.True(IsLegacyStatusFlagSet(secondValues, InfinityStatusFlag));
        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == InfinityAttachStatusSpellId);
        var infinityUpdateIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Infinity));
        Assert.True(interactionIndex < statusIndexes[0]);
        Assert.True(statusIndexes[0] < statusIndexes[1]);
        Assert.True(statusIndexes[1] < animationIndex);
        Assert.True(animationIndex < infinityUpdateIndex);
        Assert.DoesNotContain(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill));

        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(841, spell.X);
        Assert.Equal(588, spell.Y);
        Assert.Equal(InfinityAttachStatusSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var infinityEntry = Assert.Single(
            ParseMsgUpdates([sentSnapshot[infinityUpdateIndex]]).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.Infinity);
        Assert.Equal((uint)InfinityStatusFlag, infinityEntry.Flag);
        Assert.Equal(duration, infinityEntry.Time);
        Assert.Equal(0U, infinityEntry.Damage);
        Assert.Equal(0U, infinityEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy Infinity AttachStatus magic", output);
        Assert.Contains("source=AttachStatus.cs case 14380", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresInfinity14380FlyInfinityAndClearsDamageAttributes()
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
            spellIds: [InfinityAttachStatusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(FlyStatusFlag, InfinityStatusFlag),
                ["InfinitySDamage"] = "125",
                ["InfinityDamage"] = "175",
                ["LegacyFlyExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacyInfinityExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacyFlyRemoveOnDead"] = "true",
                ["LegacyInfinityRemoveOnDead"] = "true",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, FlyStatusFlag) &&
                        !HasLegacyStatusFlag(current.Character.Attributes, InfinityStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("InfinitySDamage") &&
                        !current.Character.Attributes.ContainsKey("InfinityDamage") &&
                        !current.Character.Attributes.ContainsKey("LegacyFlyExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyInfinityExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyFlyRemoveOnDead") &&
                        !current.Character.Attributes.ContainsKey("LegacyInfinityRemoveOnDead");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, FlyStatusFlag));
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, InfinityStatusFlag));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("InfinitySDamage"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("InfinityDamage"));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, FlyStatusFlag) &&
                !IsLegacyStatusFlagSet(entry.Values, InfinityStatusFlag)));
            Assert.Contains("WorldServer expired legacy Infinity/Fly flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }
}
