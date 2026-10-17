using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerDirectPhysicalTargetNeptuneReceivePublishesUnchangedHitpoints()
    {
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), []);
        var caster = CreateSession(x: 830, mapId: 1000, strength: 301, spirit: 0, classCode: 9001,
            attributes: new Dictionary<string, string> { ["PkMode"] = "0" });
        var target = CreateSession(uid: TargetPlayerUid, name: "NeptunePhysicalTarget", x: 832,
            mapId: 1000, minHitPoints: 100_000, classCode: 5005,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyNeptuneCurseFlag),
                ["MaxHitpoints"] = "100000",
            });
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var sessions = new WorldSessionStore();
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();

        await PacketHandlers.HandleAsync(new WorldPacketHandlerContext(
                casterConnection, log, caster, WorldData: worldData, Sessions: sessions,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 832, Y: 584,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));

        // CanAttackPlayer's Neptune gate requires DBSpell != null; direct melee passes null.
        // Receive.Player:557 zeros damage but reaches the ordinary HP setter at :799.
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        Assert.Equal(100_000, finalTarget.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(finalTarget.Character.Attributes, LegacyNeptuneCurseFlag));
        Assert.Contains("WorldServer handled legacy direct player physical", log.ToString());
        Assert.Contains("target NeptuneCurse player-hit damage block", log.ToString());
        var hpFrames = targetSent.Select(ReadFrame).Where(frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type is
                LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(2, hpFrames.Length);
        Assert.All(hpFrames, frame => Assert.Equal((ushort)2176, frame.Header.Opcode));
        Assert.True(LegacyMsgUpdatePacket.TryParse(hpFrames[0], out var maximum));
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 100_000)], maximum.Updates);
        Assert.True(LegacyMsgUpdatePacket.TryParse(hpFrames[1], out var current));
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 100_000),
             new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, 100_000)], current.Updates);
        GC.KeepAlive(casterConnection);
    }
}
