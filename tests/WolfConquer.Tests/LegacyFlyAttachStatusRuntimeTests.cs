using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort FlyAttachStatusSpellId = 8_003;
    private const int FlyRideBlockFlag = 0x32;
    private const int FlyDurationSeconds = 55;

    [Fact]
    public async Task HandlerFly8003SelfCastAppliesFlyStatusAndAnimationWithoutProgress()
    {
        var worldData = CreateFlyCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateFlySession(stamina: 100);
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
            ReadFrame(new MsgAttackPacket(CreateFlyQuery()).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Equal("96", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, FlyStatusFlag));
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyFlyRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            WorldPacketHandlers.RuntimeLegacyFlyExpiresAtUtcAttribute,
            beforeCast,
            afterCast,
            FlyDurationSeconds);

        Assert.Empty(ParseFlyAttackPackets(sent));
        Assert.Empty(ParseLegacySpellPackets(sent));
        Assert.Empty(ParseFlyMessages(sent));
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, FlyStatusFlag)));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(832, spell.X);
        Assert.Equal(587, spell.Y);
        Assert.Equal(FlyAttachStatusSpellId, spell.SpellId);
        Assert.Equal(2, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal((uint)FlyDurationSeconds, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy Fly AttachStatus magic", output);
        Assert.Contains("statusFlag=27", output);
        Assert.Contains("duration=55", output);
        Assert.Contains("source=AttachStatus.cs case 8003", output);
    }

    [Fact]
    public async Task HandlerFly8003BlockedByRideSpendsResourcesAndSendsSourceMessageOnly()
    {
        var worldData = CreateFlyCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateFlySession(
            stamina: 100,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(FlyRideBlockFlag),
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
            ReadFrame(new MsgAttackPacket(CreateFlyQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Equal("96", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, FlyStatusFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, FlyRideBlockFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyFlyExpiresAtUtcAttribute));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyFlyRemoveOnDead"));

        var message = Assert.Single(ParseFlyMessages(sent));
        Assert.Equal("You can't use this skill right now!", message.Content[3]);
        Assert.Empty(ParseFlyAttackPackets(sent));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseLegacySpellPackets(sent));
        Assert.DoesNotContain(ParseMsgUpdates(sent), update =>
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, FlyStatusFlag)));

        var output = log.ToString();
        Assert.Contains("WorldServer blocked legacy Fly AttachStatus magic", output);
        Assert.Contains("source=AttachStatus.cs case 8003", output);
    }

    private static MsgAttackQuery CreateFlyQuery()
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: PlayerUid,
            X: 832,
            Y: 587,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: FlyAttachStatusSpellId);
    }

    private static WorldDataCatalog CreateFlyCatalog()
    {
        return CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                FlyAttachStatusSpellId,
                sort: 6,
                level: 2,
                useMana: 7,
                experience: 50,
                useStamina: 4,
                duration: FlyDurationSeconds,
                coldTime: 0));
    }

    private static WorldSession CreateFlySession(
        ulong? stamina = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return CreateSession(
            stamina: stamina,
            spellEntries:
            [
                CreatePlayerSpell(FlyAttachStatusSpellId, level: 2),
            ],
            attributes: attributes);
    }

    private static IReadOnlyList<MsgAttackPacket> ParseFlyAttackPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.SpellId == FlyAttachStatusSpellId)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseFlyMessages(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
