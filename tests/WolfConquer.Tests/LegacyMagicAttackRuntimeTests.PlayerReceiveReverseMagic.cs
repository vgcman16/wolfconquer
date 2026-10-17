using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint LegacyReverseMagicLevelNineItemId = 4_033_909;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesReverseMagicFromTargetRune()
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
                ["StatusFlags"] = BuildStatusFlags(LegacyCycloneFlag),
                ["XPCount"] = "77",
                ["LegacyReverseMagicRoll"] = "0",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = 40_339_009,
                ItemId = LegacyReverseMagicLevelNineItemId,
                Position = 101,
            }));
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

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("0", updatedCasterSession.Character.Attributes["XPCount"]);
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacyCycloneFlag));

        Assert.Contains(
            ParseMsgUpdates(casterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    !IsLegacyStatusFlagSet(entry.Values, LegacyCycloneFlag)));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Contains("WorldServer applied legacy ReverseMagic player-hit reaction", log.ToString());
    }
}
