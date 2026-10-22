using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint LegacySlowPaceLevelNineItemId = 4_036_309;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesSlowPaceDodgeReduction()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = LegacySlowPaceLevelNineItemId,
                ItemId = LegacySlowPaceLevelNineItemId,
                Position = 101,
            }),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacySlowPaceRoll"] = "0",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Dodge"] = "100",
            });
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

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
        Assert.Equal("50", updatedTargetSession.Character.Attributes["Dodge"]);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
    }
}
