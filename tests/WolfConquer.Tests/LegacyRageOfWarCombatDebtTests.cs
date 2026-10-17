using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerRageOfWarKeepsOrdinaryZeroDamagePlayerInInitialAndFloorAnimations(bool delayed)
    {
        var family = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 991, boundX: 833, boundY: 584);
        // Matching Physical's DamageOnMonster scaling and the caller's integer
        // fraction can produce zero. Line.cs and ThreadPool.cs still enqueue it.
        var worldData = CreateMagicCatalog(family, [spawn], CreateRageOfWarRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, family);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var targetConnection = CreateSendingConnection(targetSent);
        var caster = CreateSession(strength: 300, stamina: 100, spellIds: [RageOfWarSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "ZeroRageTarget",
            x: 834,
            y: delayed ? 586 : 584,
            minHitPoints: 100_000);
        sessions.Set(connection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var floorItems = new FloorItemRuntimeStateStore();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            FloorItems: floorItems,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: RageOfWarSpellId,
                x: 850,
                y: 584)).ToFrameBytes()));

        if (delayed)
        {
            var floor = Assert.Single(ParseLegacyFloorItems(sent), item => item.ItemId == 1_500);
            Assert.True(floorItems.TryGetVisibleEntry(floor.Uid, out var entry));
            Assert.NotNull(entry.Effect);
            Assert.Equal(1, await WorldPacketHandlers.SendDueLegacyRageOfWarFloorSpellsAsync(
                context,
                entry.Effect!.DueAt.AddMilliseconds(1),
                CancellationToken.None));
        }

        foreach (var frames in new[] { sent, targetSent })
        {
            var animation = Assert.Single(
                ParseSpellAnimations(frames),
                packet => packet.SpellId == RageOfWarSpellId && packet.BombFlag == (delayed ? 1U : 0U));
            var hit = Assert.Single(animation.Targets, candidate => candidate.Uid == TargetPlayerUid);
            Assert.Equal(0U, hit.Damage);
            Assert.True(hit.Hit);
            Assert.Equal(MsgAttackEffect.None, hit.Effect);
            Assert.Contains(frames, bytes =>
                ReadFrame(bytes).Header.Opcode == 2239 &&
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.SpellId == RageOfWarSpellId && packet.BombFlag == (delayed ? 1U : 0U));
        }

        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        Assert.Equal(100_000, finalTarget.Character.MinHitPoints);
        Assert.Contains("playerTargets=1", log.ToString());
    }
}
