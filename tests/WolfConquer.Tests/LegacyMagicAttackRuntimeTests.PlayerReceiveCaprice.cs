using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint LegacyCapriceLevelTwentySevenItemId = 4_028_027;
    private const uint LegacyConsolidationLevelNineItemId = 4_036_209;



    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesCapriceXpListRemovalWhenRollSelectsBranchTwo()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet(), CreateItemType((int)LegacyCapriceLevelTwentySevenItemId, "Caprice (+27)")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            stamina: 40,
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = LegacyCapriceLevelTwentySevenItemId,
                ItemId = LegacyCapriceLevelTwentySevenItemId,
                Position = 101,
            }),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyCapriceRoll"] = "0",
                ["LegacyCapriceBranch"] = "2",
                ["LegacyCapriceStampUtc"] = DateTimeOffset.UtcNow
                    .AddMinutes(-1)
                    .ToString("O", CultureInfo.InvariantCulture),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            stamina: 80,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag),
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

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("40", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.True(updatedCasterSession.Character.Attributes.ContainsKey("LegacyCapriceStampUtc"));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.False(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyXpListFlag));
        Assert.Equal("80", updatedTargetSession.Character.Attributes["Stamina"]);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
    }
}
