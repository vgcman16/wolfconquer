using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerMoveLineAgainstCaptureTheFlagBasesHonorsContextBackedCanAttackNpcGate()
    {
        var monsterFamily = CreateMonsterFamily();
        var sameGuildBase = CreateCaptureTheFlagBaseSobNpc(id: 9_001, x: 85, y: 70);
        var inactiveBase = CreateCaptureTheFlagBaseSobNpc(id: 9_002, x: 87, y: 70);
        var enemyBase = CreateCaptureTheFlagBaseSobNpc(id: 9_003, x: 89, y: 70);
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sameGuildBase, inactiveBase, enemyBase],
            CreateMoveLineRow());
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: enemyBase.MapId,
            x: 80,
            y: 70,
            strength: 300,
            stamina: 100,
            spellIds: [MoveLineSpellId],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GuildID"] = "42",
                ["CaptureTheFlagBases"] = "9001,85,70,42,0,1;9002,87,70,99,0,0;9003,89,70,99,0,1",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)enemyBase.Id),
                X: 92,
                Y: 70,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: MoveLineSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(MoveLineSpellId, spell.SpellId);
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(92, spell.X);
        Assert.Equal(70, spell.Y);

        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)enemyBase.Id), target.Uid);
        Assert.True(target.Damage > 0);
        Assert.True(target.Hit);

        Assert.Equal(sameGuildBase.Life, sobNpcStates.GetVisible(sameGuildBase).Life);
        Assert.Equal(inactiveBase.Life, sobNpcStates.GetVisible(inactiveBase).Life);
        Assert.Equal(enemyBase.Life - (int)target.Damage, sobNpcStates.GetVisible(enemyBase).Life);
    }

    private static SobNpc CreateCaptureTheFlagBaseSobNpc(int id, int x, int y)
    {
        return new SobNpc(
            Id: id,
            Name: "CaptureTheFlagBase",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: 2_057,
            X: x,
            Y: y,
            Life: 50_000,
            MaxLife: 50_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 8,
            RawLine: $"{id},CaptureTheFlagBase,10,1137,2057,{x},{y},50000,50000,17,0",
            Fields:
            [
                id.ToString(),
                "CaptureTheFlagBase",
                "10",
                "1137",
                "2057",
                x.ToString(),
                y.ToString(),
                "50000",
                "50000",
                "17",
                "0",
            ]);
    }
}
