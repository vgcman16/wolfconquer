using System.Reflection;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task HandlerThundercloudPetAttack447TargetKindsDamagesVisibleMonsterAndSendsPetAnimation()
    {
        using var fixture = await CreateThundercloudPetAttackTargetKindsFixtureAsync(
            mapId: 1_015,
            ownerX: 830,
            ownerY: 584,
            petX: 831,
            petY: 585);
        if (fixture is null)
        {
            return;
        }

        var monster = FindVisibleMonster(
            fixture.WorldData,
            mapId: 1_015,
            name: "Birdman",
            x: 837,
            y: 573);
        var monsterStates = GetMonsterStates(fixture.Handler);
        Assert.True(
            monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var beforeAttack));
        var frameStart = fixture.Sent.Count;

        await fixture.Handler.HandleFrameAsync(
            fixture.Connection,
            ReadFrame(new MsgActionPacket
            {
                ObjId = ThundercloudOwnerUid,
                TargetUid = monster.Uid,
                Timestamp = 123_456,
                NpcId = unchecked((int)ThundercloudPetUid),
                Type = (MsgActionType)447,
            }.ToFrameBytes()));

        var remainsVisible = monsterStates.TryGetVisibleInstance(
            monster,
            DateTimeOffset.UtcNow,
            out var afterAttack);
        Assert.True(
            !remainsVisible || afterAttack.Hitpoints < beforeAttack.Hitpoints,
            fixture.Log.ToString());

        var animation = Assert.Single(
            ParseThundercloudPetAttackAnimations(fixture.Sent, frameStart));
        Assert.Equal(ThundercloudPetUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(monster.X, animation.X);
        Assert.Equal(monster.Y, animation.Y);
        Assert.Equal(ThundercloudSpellId, animation.SpellId);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.True(target.Hit);
        Assert.True(target.Damage > 0);
    }

    [Fact]
    public async Task HandlerThundercloudPetAttack447TargetKindsDamagesVisibleSobNpcAndSendsPetAnimation()
    {
        using var fixture = await CreateThundercloudPetAttackTargetKindsFixtureAsync(
            mapId: LegacySobNpcEventRules.TrainingMapId,
            ownerX: 32,
            ownerY: 207,
            petX: 33,
            petY: 207);
        if (fixture is null)
        {
            return;
        }

        var sobNpc = fixture.WorldData.SobNpcsByMapId[LegacySobNpcEventRules.TrainingMapId]
            .Single(candidate => candidate.Id == 6_003);
        var sobNpcStates = GetThundercloudPetAttackSobNpcStates(fixture.Handler);
        var beforeAttack = sobNpcStates.GetVisible(sobNpc);
        var frameStart = fixture.Sent.Count;

        await fixture.Handler.HandleFrameAsync(
            fixture.Connection,
            ReadFrame(new MsgActionPacket
            {
                ObjId = ThundercloudOwnerUid,
                TargetUid = checked((uint)sobNpc.Id),
                Timestamp = 123_456,
                NpcId = unchecked((int)ThundercloudPetUid),
                Type = (MsgActionType)447,
            }.ToFrameBytes()));

        var afterAttack = sobNpcStates.GetVisible(sobNpc);
        Assert.True(afterAttack.Life < beforeAttack.Life, fixture.Log.ToString());

        var animation = Assert.Single(
            ParseThundercloudPetAttackAnimations(fixture.Sent, frameStart));
        Assert.Equal(ThundercloudPetUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((ushort)sobNpc.X, animation.X);
        Assert.Equal((ushort)sobNpc.Y, animation.Y);
        Assert.Equal(ThundercloudSpellId, animation.SpellId);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.True(target.Hit);
        Assert.True(target.Damage > 0);
    }

    private static IReadOnlyList<LegacySpellAnimationPacket> ParseThundercloudPetAttackAnimations(
        IReadOnlyList<byte[]> sent,
        int startIndex)
    {
        return sent
            .Skip(startIndex)
            .Select(bytes =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var parsed)
                    ? parsed
                    : null)
            .OfType<LegacySpellAnimationPacket>()
            .Where(packet => packet.SpellId == ThundercloudSpellId)
            .ToArray();
    }

    private static SobNpcRuntimeStateStore GetThundercloudPetAttackSobNpcStates(
        WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sobNpcStates",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<SobNpcRuntimeStateStore>(field.GetValue(handler));
    }

    private static async Task<ThundercloudPetAttackTargetKindsFixture?>
        CreateThundercloudPetAttackTargetKindsFixtureAsync(
            int mapId,
            int ownerX,
            int ownerY,
            int petX,
            int petY)
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return null;
        }

        var dbRoot = Path.Combine(
            Path.GetTempPath(),
            "wolf-world-thundercloud-pet-attack-target-kinds-" + Guid.NewGuid());
        var usersRoot = Path.Combine(dbRoot, "Users");
        var spellsRoot = Path.Combine(dbRoot, "PlayersSpells");
        StringWriter? log = null;
        try
        {
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(spellsRoot);
            WriteSnapshot(
                usersRoot,
                ThundercloudOwnerUid,
                uid: ThundercloudOwnerUid,
                mapId: mapId,
                x: ownerX,
                y: ownerY,
                classCode: 16_000,
                livePets:
                    $"{ThundercloudPetUid},980,3,999996641,140,14,14,Thundercloud,{petX},{petY}",
                extraCharacterLines: "PkMode=0\r\n" + DisableLegacyWeaponProcLines);
            File.WriteAllBytes(
                Path.Combine(spellsRoot, $"{ThundercloudOwnerUid}.bin"),
                PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([ThundercloudCallPetSpellId]));

            var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var ticket = handoff.Issue(
                ThundercloudOwnerUid,
                "user02",
                ConquerRuntimeDefaults.WorldServer);
            log = new StringWriter();
            var handler = new WorldServerFrameHandler(
                new CharacterSnapshotLoader(usersRoot),
                handoff,
                worldData,
                log);
            var sent = new List<byte[]>();
            var connection = CreateSendingConnection(sent);

            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

            return new ThundercloudPetAttackTargetKindsFixture(
                dbRoot,
                handler,
                worldData,
                connection,
                sent,
                log);
        }
        catch
        {
            log?.Dispose();
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }

            throw;
        }
    }

    private sealed class ThundercloudPetAttackTargetKindsFixture(
        string dbRoot,
        WorldServerFrameHandler handler,
        WorldDataCatalog worldData,
        ConquerTcpConnection connection,
        List<byte[]> sent,
        StringWriter log) : IDisposable
    {
        public WorldServerFrameHandler Handler { get; } = handler;

        public WorldDataCatalog WorldData { get; } = worldData;

        public ConquerTcpConnection Connection { get; } = connection;

        public List<byte[]> Sent { get; } = sent;

        public StringWriter Log { get; } = log;

        public void Dispose()
        {
            Log.Dispose();
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }
}
