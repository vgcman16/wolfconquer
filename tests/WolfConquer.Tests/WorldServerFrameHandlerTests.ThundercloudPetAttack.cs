using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private const uint ThundercloudOwnerUid = 1_293_600;
    private const uint ThundercloudTargetUid = 1_293_601;
    private const uint ThundercloudPetUid = 700_201;
    private const ushort ThundercloudSpellId = 13_190;
    private const ushort ThundercloudCallPetSpellId = 12_840;
    private const long ThundercloudTargetHitPoints = 1_000_000;

    [Fact]
    public async Task HandlerThundercloudPetAttack447DamagesVisiblePlayerAndSendsPetAnimation()
    {
        using var fixture = await CreateThundercloudPetAttackFixtureAsync(ThundercloudPetUid);
        if (fixture is null)
        {
            return;
        }

        var targetBefore = GetSession(fixture.Handler, fixture.TargetConnection);
        var ownerFrameStart = fixture.OwnerSent.Count;

        await fixture.Handler.HandleFrameAsync(
            fixture.OwnerConnection,
            ReadFrame(new MsgActionPacket
            {
                ObjId = ThundercloudOwnerUid,
                TargetUid = ThundercloudTargetUid,
                Timestamp = 123_456,
                NpcId = unchecked((int)ThundercloudPetUid),
                Type = (MsgActionType)447,
            }.ToFrameBytes()));

        var targetAfter = GetSession(fixture.Handler, fixture.TargetConnection);
        Assert.True(
            targetAfter.Character.MinHitPoints < targetBefore.Character.MinHitPoints,
            fixture.Log.ToString());

        var animation = Assert.Single(
            fixture.OwnerSent
                .Skip(ownerFrameStart)
                .Select(bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var parsed) ? parsed : null)
                .OfType<LegacySpellAnimationPacket>(),
            packet => packet.SpellId == ThundercloudSpellId);
        Assert.Equal(ThundercloudPetUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((ushort)targetAfter.Character.X, animation.X);
        Assert.Equal((ushort)targetAfter.Character.Y, animation.Y);
        Assert.Equal(ThundercloudSpellId, animation.SpellId);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(ThundercloudTargetUid, target.Uid);
        Assert.True(target.Hit);
        Assert.True(target.Damage > 0);
    }

    [Fact]
    public async Task HandlerThundercloudPetAttack447EnforcesPerPetOneSecondAttackSpeed()
    {
        using var fixture = await CreateThundercloudPetAttackFixtureAsync(ThundercloudPetUid);
        if (fixture is null)
        {
            return;
        }

        var hitPointsBefore = GetSession(fixture.Handler, fixture.TargetConnection).Character.MinHitPoints;
        var ownerFrameStart = fixture.OwnerSent.Count;
        var attack = new MsgActionPacket
        {
            ObjId = ThundercloudOwnerUid,
            TargetUid = ThundercloudTargetUid,
            Timestamp = 123_456,
            NpcId = unchecked((int)ThundercloudPetUid),
            Type = (MsgActionType)447,
        };

        await fixture.Handler.HandleFrameAsync(
            fixture.OwnerConnection,
            ReadFrame(attack.ToFrameBytes()));
        var hitPointsAfterFirstAttack = GetSession(fixture.Handler, fixture.TargetConnection).Character.MinHitPoints;

        await fixture.Handler.HandleFrameAsync(
            fixture.OwnerConnection,
            ReadFrame((attack with { Timestamp = 123_457 }).ToFrameBytes()));
        var hitPointsAfterSecondAttack = GetSession(fixture.Handler, fixture.TargetConnection).Character.MinHitPoints;

        Assert.True(hitPointsAfterFirstAttack < hitPointsBefore, fixture.Log.ToString());
        Assert.Equal(hitPointsAfterFirstAttack, hitPointsAfterSecondAttack);
        Assert.Single(
            fixture.OwnerSent
                .Skip(ownerFrameStart)
                .Select(bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var parsed) ? parsed : null)
                .OfType<LegacySpellAnimationPacket>(),
            packet =>
                packet.AttackerUid == ThundercloudPetUid &&
                packet.SpellId == ThundercloudSpellId);
    }

    [Fact]
    public async Task HandlerThundercloudPetAttack447RejectsPetUidNotOwnedInLivePets()
    {
        const uint OwnedPetUid = 700_202;
        using var fixture = await CreateThundercloudPetAttackFixtureAsync(OwnedPetUid);
        if (fixture is null)
        {
            return;
        }

        var hitPointsBefore = GetSession(fixture.Handler, fixture.TargetConnection).Character.MinHitPoints;
        var ownerFrameStart = fixture.OwnerSent.Count;
        var targetFrameStart = fixture.TargetSent.Count;

        await fixture.Handler.HandleFrameAsync(
            fixture.OwnerConnection,
            ReadFrame(new MsgActionPacket
            {
                ObjId = ThundercloudOwnerUid,
                TargetUid = ThundercloudTargetUid,
                Timestamp = 123_456,
                NpcId = unchecked((int)ThundercloudPetUid),
                Type = (MsgActionType)447,
            }.ToFrameBytes()));

        Assert.Equal(
            hitPointsBefore,
            GetSession(fixture.Handler, fixture.TargetConnection).Character.MinHitPoints);
        Assert.DoesNotContain(
            fixture.OwnerSent.Skip(ownerFrameStart).Concat(fixture.TargetSent.Skip(targetFrameStart)),
            bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                animation.AttackerUid == ThundercloudPetUid &&
                animation.SpellId == ThundercloudSpellId);
    }

    private static async Task<ThundercloudPetAttackFixture?> CreateThundercloudPetAttackFixtureAsync(
        uint ownedPetUid)
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return null;
        }

        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-world-thundercloud-pet-attack-" + Guid.NewGuid());
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
                mapId: 1_015,
                x: 830,
                y: 584,
                classCode: 16_000,
                livePets: $"{ownedPetUid},980,3,999996641,140,14,14,Thundercloud,831,585",
                extraCharacterLines: "PkMode=0\r\n" + DisableLegacyWeaponProcLines);
            WriteSnapshot(
                usersRoot,
                ThundercloudTargetUid,
                uid: ThundercloudTargetUid,
                mapId: 1_015,
                x: 832,
                y: 584,
                name: "ThundercloudTarget",
                minHitPoints: ThundercloudTargetHitPoints);
            File.WriteAllBytes(
                Path.Combine(spellsRoot, $"{ThundercloudOwnerUid}.bin"),
                PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([ThundercloudCallPetSpellId]));

            var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var ownerTicket = handoff.Issue(
                ThundercloudOwnerUid,
                "user02",
                ConquerRuntimeDefaults.WorldServer);
            var targetTicket = handoff.Issue(
                ThundercloudTargetUid,
                "target",
                ConquerRuntimeDefaults.WorldServer);
            log = new StringWriter();
            var handler = new WorldServerFrameHandler(
                new CharacterSnapshotLoader(usersRoot),
                handoff,
                worldData,
                log);
            var ownerSent = new List<byte[]>();
            var targetSent = new List<byte[]>();
            var ownerConnection = CreateSendingConnection(ownerSent);
            var targetConnection = CreateSendingConnection(targetSent);

            await handler.HandleFrameAsync(
                ownerConnection,
                ReadFrame(new InternalWorldEnterRequestPacket(ownerTicket.Token).ToFrameBytes()));
            await handler.HandleFrameAsync(
                targetConnection,
                ReadFrame(new InternalWorldEnterRequestPacket(targetTicket.Token).ToFrameBytes()));

            return new ThundercloudPetAttackFixture(
                dbRoot,
                handler,
                ownerConnection,
                targetConnection,
                ownerSent,
                targetSent,
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

    private sealed class ThundercloudPetAttackFixture(
        string dbRoot,
        WorldServerFrameHandler handler,
        ConquerTcpConnection ownerConnection,
        ConquerTcpConnection targetConnection,
        List<byte[]> ownerSent,
        List<byte[]> targetSent,
        StringWriter log) : IDisposable
    {
        public WorldServerFrameHandler Handler { get; } = handler;

        public ConquerTcpConnection OwnerConnection { get; } = ownerConnection;

        public ConquerTcpConnection TargetConnection { get; } = targetConnection;

        public List<byte[]> OwnerSent { get; } = ownerSent;

        public List<byte[]> TargetSent { get; } = targetSent;

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
