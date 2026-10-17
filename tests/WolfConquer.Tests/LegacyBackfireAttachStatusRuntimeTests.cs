using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort BackfireAttachStatusSpellId = 12_680;
    private const int BackfireStatusFlag = 0xB3;

    [Fact]
    public async Task HandlerBackfire12680SelfCastAppliesTenSecondStatusAndSelfAnimation()
    {
        var worldData = CreateBackfireCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateBackfireSession(stamina: 100);
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
                OpponentUid: 0,
                X: 828,
                Y: 586,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: BackfireAttachStatusSpellId)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(494, updatedSession.Character.MinMana);
        Assert.Equal("97", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, BackfireStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyBackfireRemoveOnDead"));
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyBackfireExpiresAtUtc",
            beforeCast,
            afterCast,
            expectedDurationSeconds: 10);

        Assert.Empty(ParseBackfireAttackPackets(sent));

        Assert.Contains(ParseMsgUpdates(sent), update => update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, BackfireStatusFlag)));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(828, spell.X);
        Assert.Equal(586, spell.Y);
        Assert.Equal(BackfireAttachStatusSpellId, spell.SpellId);
        Assert.Equal(2, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.Empty(ParseLegacySpellPackets(sent));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy Backfire AttachStatus magic", output);
        Assert.Contains("statusFlag=179", output);
        Assert.Contains("duration=10", output);
        Assert.Contains("removeOnDead=False", output);
        Assert.Contains("source=AttachStatus.cs case 12680", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresBackfire12680Status()
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
        var session = CreateBackfireSession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(BackfireStatusFlag),
                ["LegacyBackfireExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, BackfireStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("LegacyBackfireExpiresAtUtc");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, BackfireStatusFlag));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, BackfireStatusFlag)));
            Assert.Contains("WorldServer expired legacy Backfire flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static WorldDataCatalog CreateBackfireCatalog()
    {
        return CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                BackfireAttachStatusSpellId,
                sort: 6,
                level: 2,
                useMana: 6,
                experience: 50,
                useStamina: 3,
                duration: 99,
                coldTime: 0));
    }

    private static WorldSession CreateBackfireSession(
        ulong? stamina = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return CreateSession(
            stamina: stamina,
            spellEntries:
            [
                CreatePlayerSpell(BackfireAttachStatusSpellId, level: 2),
            ],
            attributes: attributes);
    }

    private static IReadOnlyList<MsgAttackPacket> ParseBackfireAttackPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.SpellId == BackfireAttachStatusSpellId)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
