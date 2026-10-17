using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int LightningShieldAttachStatusDurationSeconds = 2_592_000;

    [Fact]
    public async Task HandlerLightningShield15750SelfCastAppliesLongStatusAndSelfAnimation()
    {
        var worldData = CreateLightningShieldAttachStatusCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateLightningShieldAttachStatusSession(stamina: 100);
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
            ReadFrame(new MsgAttackPacket(CreateLightningShieldAttachStatusQuery()).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(496, updatedSession.Character.MinMana);
        Assert.Equal("97", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyLightningShieldFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyLightningShieldRemoveOnDead"));
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyLightningShieldExpiresAtUtc",
            beforeCast,
            afterCast,
            LightningShieldAttachStatusDurationSeconds);

        Assert.Empty(ParseLightningShieldAttachStatusAttackPackets(sent));

        Assert.Contains(ParseMsgUpdates(sent), update => update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, LegacyLightningShieldFlag) &&
                !IsLegacyStatusFlagSet(entry.Values, LegacyLightningShieldActivatedFlag)));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.AzureShield);

        AssertLightningShieldAttachStatusAnimation(Assert.Single(ParseSpellAnimations(sent)));
        Assert.Empty(ParseLegacySpellPackets(sent));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy LightningShield AttachStatus magic", output);
        Assert.Contains("statusFlag=240", output);
        Assert.Contains("toggledOn=True", output);
        Assert.Contains("duration=2592000", output);
        Assert.Contains("removeOnDead=False", output);
        Assert.Contains("source=AttachStatus.cs case 15750", output);
    }

    [Fact]
    public async Task HandlerLightningShield15750RecastRemovesBothRegistrationsButRetainsMeterAndSendsReset()
    {
        var worldData = CreateLightningShieldAttachStatusCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateLightningShieldAttachStatusSession(
            stamina: 100,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyLightningShieldFlag, LegacyLightningShieldActivatedFlag),
                ["LegacyLightningShieldExpiresAtUtc"] = "2026-07-23T00:00:00.0000000+00:00",
                ["LightningShieldLeft"] = "12345",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateLightningShieldAttachStatusQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyLightningShieldFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyLightningShieldExpiresAtUtc"));
        Assert.Equal("12345", updatedSession.Character.Attributes["LightningShieldLeft"]);

        Assert.Empty(ParseLightningShieldAttachStatusAttackPackets(sent));
        var vectors = ParseMsgUpdates(sent).Where(update => update.Uid == PlayerUid)
            .SelectMany(update => update.Updates).Where(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag).ToArray();
        Assert.Collection(vectors,
            entry =>
            {
                Assert.NotNull(entry.Values);
                Assert.False(IsLegacyStatusFlagSet(entry.Values, LegacyLightningShieldFlag));
                Assert.True(IsLegacyStatusFlagSet(entry.Values, LegacyLightningShieldActivatedFlag));
            },
            entry =>
            {
                Assert.NotNull(entry.Values);
                Assert.False(IsLegacyStatusFlagSet(entry.Values, LegacyLightningShieldFlag));
                Assert.False(IsLegacyStatusFlagSet(entry.Values, LegacyLightningShieldActivatedFlag));
            });
        var wire = sent.Where(bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.StatusFlag or LegacyMsgUpdateDataType.AzureShield))
            .ToArray();
        Assert.Equal(3, wire.Length);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(wire[2]), out var reset));
        Assert.Single(reset.Updates, entry => entry.Type == LegacyMsgUpdateDataType.AzureShield);

        var resetUpdate = Assert.Single(
            ParseMsgUpdates(sent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.AzureShield);
        Assert.Equal(0UL, resetUpdate.Value);
        Assert.Equal((uint)LegacyLightningShieldActivatedFlag, resetUpdate.Flag);
        Assert.Equal(0U, resetUpdate.Time);
        Assert.Equal(0U, resetUpdate.Damage);
        Assert.Equal(0U, resetUpdate.SpellLevel);

        AssertLightningShieldAttachStatusAnimation(Assert.Single(ParseSpellAnimations(sent)));

        var output = log.ToString();
        Assert.Contains("toggledOn=False", output);
        Assert.Contains("clearedActivated=True", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresOnlyLightningShield15750BaseRegistrationAndRetainsUntracked237Meter()
    {
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new ConcurrentQueue<byte[]>();
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Enqueue(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        var now = DateTimeOffset.UtcNow;
        var clock = new ManualRuntimeTimeProvider(now);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromMilliseconds(10), clock),
            log: TextWriter.Synchronized(log),
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var session = CreateLightningShieldAttachStatusSession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyLightningShieldFlag, LegacyLightningShieldActivatedFlag),
                ["LegacyLightningShieldExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LightningShieldLeft"] = "12345",
            });
        SetHandlerSession(handler, connection, session);
        var global = typeof(WorldServerFrameHandler).GetField("_legacyRuntimeGlobalLoopStarted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(global);

        try
        {
            var start = typeof(WorldServerFrameHandler).GetMethod("TryStartLegacyGlobalRuntimeUpdateLoop",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(start);
            start.Invoke(handler, [cancel.Token]);
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1, TimeSpan.FromSeconds(2));
            clock.Advance(TimeSpan.FromMilliseconds(10));
            await WaitUntilAsync(
                () => !AssertHandlerSession(handler, connection).Character.Attributes
                    .ContainsKey("LegacyLightningShieldExpiresAtUtc"),
                TimeSpan.FromSeconds(1));
        }
        finally
        {
            await cancel.CancelAsync();
            await WaitUntilAsync(() => clock.ScheduledTimerCount == 0 && Equals(0, global.GetValue(handler)),
                TimeSpan.FromSeconds(2));
        }

        var expired = AssertHandlerSession(handler, connection);
        Assert.False(HasLegacyStatusFlag(expired.Character.Attributes, LegacyLightningShieldFlag));
        Assert.True(HasLegacyStatusFlag(expired.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.Equal("12345", expired.Character.Attributes["LightningShieldLeft"]);
        Assert.False(expired.Character.Attributes.ContainsKey("LegacyLightningShieldExpiresAtUtc"));
        var update = Assert.Single(ParseMsgUpdates(sent));
        var vector = Assert.Single(update.Updates, entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag);
        Assert.NotNull(vector.Values);
        Assert.False(IsLegacyStatusFlagSet(vector.Values, LegacyLightningShieldFlag));
        Assert.True(IsLegacyStatusFlagSet(vector.Values, LegacyLightningShieldActivatedFlag));
        Assert.DoesNotContain(ParseMsgUpdates(sent).SelectMany(packet => packet.Updates),
            entry => entry.Type is LegacyMsgUpdateDataType.AzureShield or
                LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints);
        Assert.Contains("WorldServer expired legacy LightningShield flags", log.ToString());
    }

    private static MsgAttackQuery CreateLightningShieldAttachStatusQuery()
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: PlayerUid,
            X: 836,
            Y: 590,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: LegacyLightningShieldSpellId);
    }

    private static WorldDataCatalog CreateLightningShieldAttachStatusCatalog()
    {
        return CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                LegacyLightningShieldSpellId,
                sort: 6,
                level: 4,
                useMana: 4,
                experience: 50,
                useStamina: 3,
                duration: 77,
                coldTime: 0));
    }

    private static WorldSession CreateLightningShieldAttachStatusSession(
        ulong? stamina = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return CreateSession(
            stamina: stamina,
            spellEntries:
            [
                CreatePlayerSpell(LegacyLightningShieldSpellId, level: 4),
            ],
            attributes: attributes);
    }

    private static void AssertLightningShieldAttachStatusAnimation(LegacySpellAnimationPacket spell)
    {
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(836, spell.X);
        Assert.Equal(590, spell.Y);
        Assert.Equal(LegacyLightningShieldSpellId, spell.SpellId);
        Assert.Equal(4, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static IReadOnlyList<MsgAttackPacket> ParseLightningShieldAttachStatusAttackPackets(
        IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.SpellId == LegacyLightningShieldSpellId)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
