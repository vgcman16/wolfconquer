using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort CastPrayAttachStatusSpellId = 9_876;
    private const int CastPrayStatusFlag = 0x1E;
    private const int CastPrayDurationSeconds = 2_592_000;

    [Fact]
    public async Task HandlerCastPray9876SelfCastAppliesThirtyDayStatusAndSelfAnimation()
    {
        var worldData = CreateCastPrayCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateCastPraySession(stamina: 100);
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
            ReadFrame(new MsgAttackPacket(CreateCastPrayQuery()).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(492, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, CastPrayStatusFlag));
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyCastPrayRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            WorldPacketHandlers.RuntimeLegacyCastPrayExpiresAtUtcAttribute,
            beforeCast,
            afterCast,
            CastPrayDurationSeconds);

        Assert.Empty(ParseCastPrayAttackPackets(sent));
        Assert.Empty(ParseLegacySpellPackets(sent));
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, CastPrayStatusFlag)));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(836, spell.X);
        Assert.Equal(589, spell.Y);
        Assert.Equal(CastPrayAttachStatusSpellId, spell.SpellId);
        Assert.Equal(3, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CastPray AttachStatus magic", output);
        Assert.Contains("statusFlag=30", output);
        Assert.Contains("durationSeconds=2592000", output);
        Assert.Contains("removeOnDead=True", output);
        Assert.Contains("source=AttachStatus.cs case 9876", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresCastPray9876StatusAndRemoveOnDeadAttribute()
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
        var session = CreateCastPraySession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(CastPrayStatusFlag),
                [WorldPacketHandlers.RuntimeLegacyCastPrayExpiresAtUtcAttribute] =
                    now.AddSeconds(-1).ToString("O"),
                ["LegacyCastPrayRemoveOnDead"] = "true",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, CastPrayStatusFlag) &&
                        !current.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyCastPrayExpiresAtUtcAttribute) &&
                        !current.Character.Attributes.ContainsKey("LegacyCastPrayRemoveOnDead");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, CastPrayStatusFlag));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, CastPrayStatusFlag)));
            Assert.Contains("WorldServer expired legacy CastPray flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static MsgAttackQuery CreateCastPrayQuery()
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: 0,
            X: 836,
            Y: 589,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: CastPrayAttachStatusSpellId);
    }

    private static WorldDataCatalog CreateCastPrayCatalog()
    {
        return CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                CastPrayAttachStatusSpellId,
                sort: 6,
                level: 3,
                useMana: 8,
                experience: 50,
                useStamina: 5,
                duration: 44,
                coldTime: 0));
    }

    private static WorldSession CreateCastPraySession(
        ulong? stamina = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return CreateSession(
            stamina: stamina,
            spellEntries:
            [
                CreatePlayerSpell(CastPrayAttachStatusSpellId, level: 3),
            ],
            attributes: attributes);
    }

    private static IReadOnlyList<MsgAttackPacket> ParseCastPrayAttackPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.SpellId == CastPrayAttachStatusSpellId)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
