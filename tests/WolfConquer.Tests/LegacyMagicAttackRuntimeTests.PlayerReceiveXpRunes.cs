using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint LegacyXpKillerLevelSixItemId = 4_030_606;
    private const uint LegacyXpKillerLevelNineItemId = 4_030_609;
    private const uint LegacyXpBoosterLevelNineItemId = 4_031_109;
    private const uint LegacyTidalWaveLevelNineItemId = 4_033_109;
    private const uint LegacyQuenchLevelNineItemId = 4_033_609;
    private const int LegacyTidalWaveFlag = 0xF5;
    private const int LegacyQuenchFlag = 0xF6;
    private const string RuntimeLegacyTidalWaveStampUtcAttribute = "LegacyTidalWaveStampUtc";
    private const string RuntimeLegacyTidalWaveExpiresAtUtcAttribute = "LegacyTidalWaveExpiresAtUtc";
    private const string RuntimeLegacyQuenchStampUtcAttribute = "LegacyQuenchStampUtc";
    private const string RuntimeLegacyQuenchExpiresAtUtcAttribute = "LegacyQuenchExpiresAtUtc";

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesXpBoosterToAttackerXpSkill()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet(), CreateItemType((int)LegacyXpBoosterLevelNineItemId, "XPBooster(+9)")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(42);
        var casterSession = CreateSession(
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = LegacyXpBoosterLevelNineItemId,
                ItemId = LegacyXpBoosterLevelNineItemId,
                Position = 101,
            }),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyCycloneFlag),
                ["XPBooster"] = "2",
                [RuntimeLegacyCycloneExpiresAtUtcAttribute] = expiresAt.ToString("O", CultureInfo.InvariantCulture),
            });
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
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
        Assert.Equal("1", updatedCasterSession.Character.Attributes["XPBooster"]);
        Assert.Equal(
            expiresAt.AddSeconds(1).ToString("O", CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes[RuntimeLegacyCycloneExpiresAtUtcAttribute]);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesXpKillerLevelNineToTargetXpSkill()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet(), CreateItemType((int)LegacyXpKillerLevelNineItemId, "XPKiller(+9)")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var targetExpiresAt = DateTimeOffset.UtcNow.AddSeconds(42);
        var casterSession = CreateSession(
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = LegacyXpKillerLevelNineItemId,
                ItemId = LegacyXpKillerLevelNineItemId,
                Position = 101,
            }));
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyCycloneFlag),
                ["XPCount"] = "29",
                [RuntimeLegacyCycloneExpiresAtUtcAttribute] =
                    targetExpiresAt.ToString("O", CultureInfo.InvariantCulture),
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
        Assert.Equal("1", updatedTargetSession.Character.Attributes["XPKiller"]);
        Assert.Equal("20", updatedTargetSession.Character.Attributes["XPCount"]);
        Assert.Equal(
            targetExpiresAt.AddSeconds(-9).ToString("O", CultureInfo.InvariantCulture),
            updatedTargetSession.Character.Attributes[RuntimeLegacyCycloneExpiresAtUtcAttribute]);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerSkipsXpKillerLevelSixWhenTargetXpCountAtLeastThirty()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet(), CreateItemType((int)LegacyXpKillerLevelSixItemId, "XPKiller(+6)")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var targetExpiresAt = DateTimeOffset.UtcNow.AddSeconds(42);
        var casterSession = CreateSession(
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = LegacyXpKillerLevelSixItemId,
                ItemId = LegacyXpKillerLevelSixItemId,
                Position = 101,
            }));
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyCycloneFlag),
                ["XPCount"] = "30",
                [RuntimeLegacyCycloneExpiresAtUtcAttribute] =
                    targetExpiresAt.ToString("O", CultureInfo.InvariantCulture),
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
        Assert.False(updatedTargetSession.Character.Attributes.ContainsKey("XPKiller"));
        Assert.Equal("30", updatedTargetSession.Character.Attributes["XPCount"]);
        Assert.Equal(
            targetExpiresAt.ToString("O", CultureInfo.InvariantCulture),
            updatedTargetSession.Character.Attributes[RuntimeLegacyCycloneExpiresAtUtcAttribute]);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesTidalWaveToAttackerAndSendsSourceUpdateToTarget()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateItemType((int)LegacyTidalWaveLevelNineItemId, "TidalWave(+9)")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = LegacyTidalWaveLevelNineItemId,
                ItemId = LegacyTidalWaveLevelNineItemId,
                Position = 101,
            }),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyTidalWaveStampUtc"] = DateTimeOffset.UtcNow
                    .AddMinutes(-2)
                    .ToString("O", CultureInfo.InvariantCulture),
            });
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
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
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacyTidalWaveFlag));
        Assert.True(updatedCasterSession.Character.Attributes.ContainsKey(RuntimeLegacyTidalWaveStampUtcAttribute));
        Assert.True(updatedCasterSession.Character.Attributes.ContainsKey(RuntimeLegacyTidalWaveExpiresAtUtcAttribute));
        Assert.Contains(
            targetSent,
            frame => HasSingleLegacyUpdate(
                frame,
                TargetPlayerUid,
                LegacyMsgUpdateDataType.TidalWave,
                LegacyTidalWaveFlag,
                time: 60));
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesQuenchToTarget()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateItemType((int)LegacyQuenchLevelNineItemId, "Quench(+9)")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = LegacyQuenchLevelNineItemId,
                ItemId = LegacyQuenchLevelNineItemId,
                Position = 101,
            }),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyQuenchStampUtc"] = DateTimeOffset.UtcNow
                    .AddMinutes(-2)
                    .ToString("O", CultureInfo.InvariantCulture),
            });
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
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
        Assert.True(updatedCasterSession.Character.Attributes.ContainsKey(RuntimeLegacyQuenchStampUtcAttribute));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyQuenchFlag));
        Assert.True(updatedTargetSession.Character.Attributes.ContainsKey(RuntimeLegacyQuenchExpiresAtUtcAttribute));
        Assert.Contains(
            targetSent,
            frame => HasSingleLegacyUpdate(
                frame,
                TargetPlayerUid,
                LegacyMsgUpdateDataType.Quench,
                LegacyQuenchFlag,
                time: 60));
    }

    private static bool HasSingleLegacyUpdate(
        byte[] frame,
        uint uid,
        LegacyMsgUpdateDataType dataType,
        int flag,
        uint time)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Uid == uid &&
            update.Updates.Count == 1 &&
            update.Updates[0].Type == dataType &&
            update.Updates[0].Flag == checked((uint)flag) &&
            update.Updates[0].Time == time;
    }
}
