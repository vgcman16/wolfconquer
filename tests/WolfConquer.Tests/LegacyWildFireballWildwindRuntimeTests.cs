using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerWildFireballWildwindCreatesSourceFloorTrailAndHitsPlayersWithinCasterRadius()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateWildFireballWildwindRow(damage2: 30_280));
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var nearSent = new List<byte[]>();
        var farSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var nearConnection = CreateSendingConnection(nearSent);
        var farConnection = CreateSendingConnection(farSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [WildFireballSpellId],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
            });
        var nearTargetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "NearTarget",
            x: 835,
            y: 584,
            minHitPoints: 100_000,
            spellIds: Array.Empty<ushort>());
        var farTargetSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "FarTarget",
            x: 837,
            y: 584,
            minHitPoints: 100_000,
            spellIds: Array.Empty<ushort>());
        sessions.Set(casterConnection, casterSession);
        sessions.Set(nearConnection, nearTargetSession);
        sessions.Set(farConnection, farTargetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            FloorItems: floorItems);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            WildFireballSpellId,
            out _,
            out var spell));
        var expectedBaseDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            nearTargetSession,
            spell,
            worldData,
            1U);
        var expectedDamage = (uint)((ulong)expectedBaseDamage * 20 / 35);
        var before = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: 0,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: WildFireballSpellId)).ToFrameBytes()));

        var after = DateTimeOffset.UtcNow;
        var casterFloorItem = Assert.Single(ParseLegacyFloorItems(casterSent));
        Assert.Equal(LegacyFloorItemDropType.Effect, casterFloorItem.DropType);
        Assert.Equal(18_456U, casterFloorItem.ItemId);
        Assert.Equal((ushort)833, casterFloorItem.X);
        Assert.Equal((ushort)584, casterFloorItem.Y);
        Assert.Equal((ushort)0, casterFloorItem.MaxLife);
        Assert.Equal(0U, casterFloorItem.Life);
        Assert.Equal((byte)20, casterFloorItem.PlusOrColor);
        Assert.Equal(PlayerUid, casterFloorItem.ItemOwnerUid);
        Assert.Equal(77U, casterFloorItem.GuildId);
        Assert.Equal((byte)0, casterFloorItem.FlowerType);
        Assert.Equal(0UL, casterFloorItem.Timer);
        Assert.Equal((ushort)833, casterFloorItem.OwnerX);
        Assert.Equal((ushort)584, casterFloorItem.OwnerY);
        Assert.Equal("trap", casterFloorItem.Name);
        Assert.Equal(casterFloorItem, Assert.Single(ParseLegacyFloorItems(nearSent)));
        Assert.Equal(casterFloorItem, Assert.Single(ParseLegacyFloorItems(farSent)));

        Assert.True(floorItems.TryGetVisibleEntry(casterFloorItem.Uid, out var firstRuntimeEntry));
        Assert.True(floorItems.TryGetVisibleEntry(casterFloorItem.Uid + 1, out var secondRuntimeEntry));
        Assert.Equal(1_015, firstRuntimeEntry.MapId);
        Assert.Equal(casterFloorItem, firstRuntimeEntry.Packet);
        Assert.Equal((ushort)840, secondRuntimeEntry.Packet.X);
        Assert.Equal((ushort)584, secondRuntimeEntry.Packet.Y);
        Assert.Equal((ushort)840, secondRuntimeEntry.Packet.OwnerX);
        Assert.Equal((ushort)584, secondRuntimeEntry.Packet.OwnerY);
        Assert.NotNull(firstRuntimeEntry.Effect);
        Assert.NotNull(secondRuntimeEntry.Effect);
        foreach (var entry in new[] { firstRuntimeEntry, secondRuntimeEntry })
        {
            Assert.Equal(WildFireballSpellId, entry.Effect!.SpellId);
            Assert.Equal((ushort)842, entry.Effect.OriginX);
            Assert.Equal((ushort)584, entry.Effect.OriginY);
            Assert.Equal(15, entry.Effect.Distance);
            Assert.Equal(5.0, entry.Effect.HitRadius);
            Assert.Equal(18, entry.Effect.RemovalVisibilityRadius);
            Assert.Equal((byte)0, entry.Effect.SpellLevel);
            Assert.Equal((byte)0, entry.Effect.LevelHu);
            Assert.Equal(PlayerUid, entry.Effect.RuntimeOwnerUid);
        }

        Assert.InRange(firstRuntimeEntry.Effect!.DueAt, before, after.AddMilliseconds(50));
        Assert.InRange(secondRuntimeEntry.Effect!.DueAt, before.AddMilliseconds(500), after.AddMilliseconds(550));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var nearSpell = Assert.Single(ParseSpellAnimations(nearSent));
        var farSpell = Assert.Single(ParseSpellAnimations(farSent));
        foreach (var animation in new[] { casterSpell, nearSpell, farSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal((ushort)830, animation.X);
            Assert.Equal((ushort)584, animation.Y);
            Assert.Equal(WildFireballSpellId, animation.SpellId);
            Assert.Equal((byte)0, animation.SpellLevel);
            Assert.Equal((byte)0, animation.LevelHu);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(nearConnection, out var updatedNearTarget));
        Assert.NotNull(updatedNearTarget);
        Assert.Equal(100_000 - expectedDamage, updatedNearTarget.Character.MinHitPoints);
        Assert.True(sessions.TryGet(farConnection, out var updatedFarTarget));
        Assert.NotNull(updatedFarTarget);
        Assert.Equal(100_000, updatedFarTarget.Character.MinHitPoints);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy WildFireball Wildwind magic", output);
        Assert.Contains("spellId=16520", output);
        Assert.Contains("sort=90", output);
        Assert.Contains("floorTrapCount=2", output);
        Assert.Contains("storedFloorTrapCount=2", output);
        Assert.Contains("sentFloorTrapCount=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains($"totalPlayerDamage={expectedDamage}", output);
        Assert.Contains("playerDamageScale=20/35", output);
        Assert.Contains("source=Wildwind.cs case 16520", output);
    }

    [Fact]
    public async Task RuntimeWildFireballWildwindFloorTrapHitsMonsterAndSobThenRemovesEffect()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_520, x: 834, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [sobNpc],
            [CreateWildFireballWildwindRow(damage2: 30_280)]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var observerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var observerConnection = CreateSendingConnection(observerSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [WildFireballSpellId],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
            });
        var observerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Observer",
            x: 842,
            y: 584,
            spellIds: Array.Empty<ushort>());
        sessions.Set(casterConnection, casterSession);
        sessions.Set(observerConnection, observerSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates,
            FloorItems: floorItems);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: 0,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: WildFireballSpellId)).ToFrameBytes()));

        var floorItem = Assert.Single(ParseLegacyFloorItems(casterSent), packet => packet.DropType == LegacyFloorItemDropType.Effect);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.NotNull(runtimeEntry.Effect);
        var processedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            runtimeEntry.Effect!.DueAt.AddMilliseconds(1),
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        Assert.False(floorItems.TryGetVisibleEntry(floorItem.Uid, out _));
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid + 1, out _));
        var runtimeAnimation = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.AttackerUid == floorItem.Uid &&
                animation.SpellId == WildFireballSpellId &&
                animation.BombFlag == 1);
        Assert.Equal(0U, runtimeAnimation.AttackedUid);
        Assert.Equal(floorItem.X, runtimeAnimation.X);
        Assert.Equal(floorItem.Y, runtimeAnimation.Y);
        Assert.Equal(0, runtimeAnimation.SpellLevel);
        Assert.Equal(0, runtimeAnimation.LevelHu);
        var damageByUid = runtimeAnimation.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { monster.Uid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.All(runtimeAnimation.Targets, target => Assert.True(target.Hit));
        Assert.All(runtimeAnimation.Targets, target => Assert.True(target.Damage > 0));

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.True(damagedMonster.Hitpoints < monster.MaxLife);
        Assert.True(sobNpcStates.GetVisible(sobNpc).Life < sobNpc.Life);
        var remove = Assert.Single(
            ParseLegacyFloorItems(casterSent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);
        Assert.Equal(floorItem.ItemId, remove.ItemId);
        Assert.Contains(
            ParseLegacyFloorItems(observerSent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy WildFireball Wildwind floor spell runtime", output);
        Assert.Contains("trapCount=1", output);
        Assert.Contains("targetCount=2", output);
        Assert.Contains("source=ThreadPool.cs cases 16520/16530", output);
    }

    private static MagicType CreateWildFireballWildwindRow(int damage2)
    {
        return CreateMagicRow(
            WildFireballSpellId,
            sort: 90,
            useMana: 0,
            damage: 30_120,
            maxTargets: 5,
            range: 15,
            useStamina: 0,
            coolDown: 0,
            coldTime: 0,
            damage2: damage2);
    }
}
