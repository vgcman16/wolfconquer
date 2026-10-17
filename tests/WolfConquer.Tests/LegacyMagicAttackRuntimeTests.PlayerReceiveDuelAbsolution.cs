using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int PlayerReceiveAbsolutionFlag = 0xD8;
    private const int PlayerReceiveDuelFlag = 0xE1;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerClearsDuelAndAbsolution()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(PlayerReceiveAbsolutionFlag),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(PlayerReceiveDuelFlag, PlayerReceiveAbsolutionFlag),
                ["XPCount"] = "37",
            });
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
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, PlayerReceiveDuelFlag));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, PlayerReceiveAbsolutionFlag));
        Assert.False(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, PlayerReceiveAbsolutionFlag));
        Assert.Equal("0", updatedTargetSession.Character.Attributes["XPCount"]);

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
            Assert.True(target.Hit);
        }

        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
    }
}
