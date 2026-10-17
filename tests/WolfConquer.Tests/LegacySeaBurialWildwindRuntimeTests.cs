using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerSeaBurialWildwindCreatesSourceFloorEffectAndDelayedBombPulse()
    {
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_968, x: 835, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn],
            [sobNpc],
            [CreateSeaBurialWildwindRow()]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    SeaBurialSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0),
            ],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "SeaBurialTarget",
            x: 834,
            y: 584,
            minHitPoints: 500_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates,
            FloorItems: floorItems);
        var query = new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: 0,
            X: 838,
            Y: 584,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: SeaBurialSpellId);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            SeaBurialSpellId,
            out var playerSpell,
            out var spell));
        var expectedDirectPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * 7 / 11;
        var expectedFloorPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);

        var before = DateTimeOffset.UtcNow;
        var handled = await InvokeLegacySeaBurialWildwindAsync(
            context,
            casterSession,
            query,
            playerSpell,
            spell);
        var after = DateTimeOffset.UtcNow;

        Assert.True(handled);
        var floorItem = Assert.Single(
            ParseLegacyFloorItems(casterSent),
            packet => packet.ItemId == 1_850U &&
                packet.DropType == LegacyFloorItemDropType.Effect);
        Assert.Equal((ushort)830, floorItem.X);
        Assert.Equal((ushort)584, floorItem.Y);
        Assert.Equal((byte)16, floorItem.PlusOrColor);
        Assert.Equal(PlayerUid, floorItem.ItemOwnerUid);
        Assert.Equal(77U, floorItem.GuildId);
        Assert.Equal((byte)3, floorItem.FlowerType);
        Assert.Equal(3UL, floorItem.Timer);
        Assert.Equal((ushort)856, floorItem.OwnerX);
        Assert.Equal((ushort)584, floorItem.OwnerY);
        Assert.Equal("SeaBurialTrap", floorItem.Name);

        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.Equal(1_015, runtimeEntry.MapId);
        Assert.Equal(floorItem, runtimeEntry.Packet);
        Assert.NotNull(runtimeEntry.Effect);
        Assert.Equal(SeaBurialSpellId, runtimeEntry.Effect!.SpellId);
        Assert.Equal((ushort)856, runtimeEntry.Effect.OriginX);
        Assert.Equal((ushort)584, runtimeEntry.Effect.OriginY);
        Assert.Equal(18, runtimeEntry.Effect.Distance);
        Assert.Equal(2.0, runtimeEntry.Effect.HitRadius);
        Assert.Equal(PlayerUid, runtimeEntry.Effect.RuntimeOwnerUid);
        Assert.InRange(
            runtimeEntry.Effect.DueAt,
            before.AddMilliseconds(3_000),
            after.AddMilliseconds(3_000));

        var directAnimation = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == SeaBurialSpellId &&
                animation.BombFlag == 0);
        Assert.Equal(PlayerUid, directAnimation.AttackerUid);
        Assert.Equal((ushort)856, directAnimation.X);
        Assert.Equal((ushort)584, directAnimation.Y);
        Assert.Contains(directAnimation.Targets, target => target.Uid == TargetPlayerUid);
        Assert.Equal(
            expectedDirectPlayerDamage,
            Assert.Single(directAnimation.Targets, target => target.Uid == TargetPlayerUid).Damage);

        var targetFrameCountBeforeRuntime = targetSent.Count;
        Assert.True(sessions.TryGet(targetConnection, out var afterDirectTargetSession));
        Assert.NotNull(afterDirectTargetSession);
        Assert.Equal(500_000 - expectedDirectPlayerDamage, afterDirectTargetSession.Character.MinHitPoints);

        var processedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            runtimeEntry.Effect.DueAt.AddMilliseconds(1),
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        Assert.False(floorItems.TryGetVisibleEntry(floorItem.Uid, out _));
        var runtimeAnimation = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == SeaBurialSpellId &&
                animation.BombFlag == 1);
        Assert.Equal(floorItem.Uid, runtimeAnimation.AttackerUid);
        Assert.Equal((ushort)848, runtimeAnimation.X);
        Assert.Equal((ushort)584, runtimeAnimation.Y);
        var runtimePlayerTarget = Assert.Single(runtimeAnimation.Targets, target => target.Uid == TargetPlayerUid);
        Assert.Equal(expectedFloorPlayerDamage * 2 / 3, runtimePlayerTarget.Damage);
        Assert.True(runtimePlayerTarget.Hit);

        var targetRuntimeAnimation = Assert.Single(
            ParseSpellAnimations(targetSent.Skip(targetFrameCountBeforeRuntime)),
            animation => animation.SpellId == SeaBurialSpellId &&
                animation.BombFlag == 1);
        Assert.Equal(
            runtimeAnimation.Targets
                .Select(target => (target.Uid, target.Damage))
                .OrderBy(target => target.Uid)
                .ThenBy(target => target.Damage)
                .ToArray(),
            targetRuntimeAnimation.Targets
                .Select(target => (target.Uid, target.Damage))
                .OrderBy(target => target.Uid)
                .ThenBy(target => target.Damage)
                .ToArray());

        Assert.True(sessions.TryGet(targetConnection, out var afterFloorTargetSession));
        Assert.NotNull(afterFloorTargetSession);
        Assert.Equal(
            500_000 - expectedDirectPlayerDamage - expectedFloorPlayerDamage,
            afterFloorTargetSession.Character.MinHitPoints);

        var remove = Assert.Single(
            ParseLegacyFloorItems(casterSent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);
        Assert.Equal(0UL, remove.Timer);
        Assert.Equal((byte)3, remove.FlowerType);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy SeaBurial Wildwind magic", output);
        Assert.Contains("spellId=14680", output);
        Assert.Contains("endpointX=856", output);
        Assert.Contains("source=Wildwind.cs case 14680", output);
        Assert.Contains("WorldServer handled legacy SeaBurial Wildwind floor spell runtime", output);
        Assert.Contains("source=ThreadPool.cs case 14680", output);
    }

    private static async ValueTask<bool> InvokeLegacySeaBurialWildwindAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerSeaBurialWildwindMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        return await Assert.IsType<ValueTask<bool>>(result);
    }

    private static MagicType CreateSeaBurialWildwindRow()
    {
        return CreateMagicRow(
            SeaBurialSpellId,
            sort: 90,
            useMana: 0,
            damage: 30_100,
            maxTargets: 0,
            range: 18,
            useStamina: 0,
            coldTime: 0,
            damageOnHuman: 1,
            damage2: 30_145,
            gDamage: 100);
    }
}
