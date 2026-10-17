using System.Globalization;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private const uint PetAttachStatusOwnerUid = 1_293_600;
    private const uint PetAttachStatusPetUid = 700_201;
    private const ushort PetAttachStatusSpellId = 12_970;
    private const int PetAttachStatusRevengeTailFlag = 0xCA;
    private const int PetAttachStatusThunderboltAttackSpeedMilliseconds = 500;
    private const int PetAttachStatusThunderboltDurationSeconds = 15;
    private const string PetAttachStatusLivePet =
        "700201,980,3,999996641,140,14,14,Thundercloud,831,585";

    [Fact]
    public async Task HandlerThundercloudPetAttachStatus12970PersistsThunderboltModeAndSendsSourceAnimation()
    {
        using var fixture = await CreateThundercloudPetAttachStatusFixtureAsync(revengeTailActive: false);
        if (fixture is null)
        {
            return;
        }

        var castStartedAt = DateTimeOffset.UtcNow;
        var frameStart = fixture.Sent.Count;

        await SendThundercloudPetAttachStatusCastAsync(fixture);

        var castFinishedAt = DateTimeOffset.UtcNow;
        var castFrames = fixture.Sent.Skip(frameStart).ToArray();
        var interactionIndex = Array.FindIndex(
            castFrames,
            bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == PetAttachStatusSpellId);
        Assert.True(interactionIndex >= 0);
        var interaction = castFrames[interactionIndex];
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(interaction), out var parsedInteraction));
        Assert.Equal(PetAttachStatusOwnerUid, parsedInteraction.Query.Uid);
        Assert.Equal(PetAttachStatusOwnerUid, parsedInteraction.Query.OpponentUid);
        Assert.Equal((ushort)833, parsedInteraction.Query.X);
        Assert.Equal((ushort)586, parsedInteraction.Query.Y);

        var animationIndex = Array.FindIndex(
            castFrames,
            bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.SpellId == PetAttachStatusSpellId);
        Assert.True(animationIndex > interactionIndex);
        Assert.True(LegacySpellAnimationPacket.TryParse(
            ReadFrame(castFrames[animationIndex]),
            out var animation));
        Assert.Equal(PetAttachStatusOwnerUid, animation.AttackerUid);
        Assert.Equal(PetAttachStatusOwnerUid, animation.AttackedUid);
        Assert.Equal((ushort)833, animation.X);
        Assert.Equal((ushort)586, animation.Y);
        Assert.Equal((byte)0, animation.SpellLevel);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(PetAttachStatusPetUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var updated = GetSession(fixture.Handler, fixture.Connection);
        Assert.Equal(
            500,
            Assert.Single(updated.PlayerSpells.Spells, entry => entry.Id == PetAttachStatusSpellId).Experience);
        var livePetFields = updated.Character.Attributes["LivePets"].Split(
            ',',
            StringSplitOptions.TrimEntries);
        Assert.Equal(12, livePetFields.Length);
        Assert.Equal(PetAttachStatusPetUid.ToString(CultureInfo.InvariantCulture), livePetFields[0]);
        Assert.Equal("0", livePetFields[10]);
        Assert.True(
            long.TryParse(
                livePetFields[11],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var thunderboltExpiresAtMilliseconds));
        var thunderboltExpiresAt = DateTimeOffset.FromUnixTimeMilliseconds(thunderboltExpiresAtMilliseconds);
        Assert.InRange(
            thunderboltExpiresAt,
            castStartedAt.AddSeconds(PetAttachStatusThunderboltDurationSeconds - 1),
            castFinishedAt.AddSeconds(PetAttachStatusThunderboltDurationSeconds + 1));

        var petAttackSpeedMilliseconds = thunderboltExpiresAt > castFinishedAt
            ? PetAttachStatusThunderboltAttackSpeedMilliseconds
            : 1_000;
        Assert.Equal(PetAttachStatusThunderboltAttackSpeedMilliseconds, petAttackSpeedMilliseconds);
    }

    [Fact]
    public async Task HandlerThundercloudPetAttachStatus12970RevengeTailReturnsBeforePetMutation()
    {
        using var fixture = await CreateThundercloudPetAttachStatusFixtureAsync(revengeTailActive: true);
        if (fixture is null)
        {
            return;
        }

        var before = GetSession(fixture.Handler, fixture.Connection);
        Assert.True(HasLegacyStatusFlag(
            LegacyPlayerSpawnPacket.FromSnapshot(before.Character).StatusFlags,
            PetAttachStatusRevengeTailFlag));
        var livePetsBefore = before.Character.Attributes["LivePets"];
        var frameStart = fixture.Sent.Count;

        await SendThundercloudPetAttachStatusCastAsync(fixture);

        var updated = GetSession(fixture.Handler, fixture.Connection);
        Assert.False(HasLegacyStatusFlag(
            LegacyPlayerSpawnPacket.FromSnapshot(updated.Character).StatusFlags,
            PetAttachStatusRevengeTailFlag));
        Assert.Equal(livePetsBefore, updated.Character.Attributes["LivePets"]);
        Assert.Equal(
            0,
            Assert.Single(updated.PlayerSpells.Spells, entry => entry.Id == PetAttachStatusSpellId).Experience);
        var castFrames = fixture.Sent.Skip(frameStart).ToArray();
        Assert.Single(
            castFrames,
            bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == PetAttachStatusSpellId);
        Assert.DoesNotContain(
            castFrames,
            bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                animation.SpellId == PetAttachStatusSpellId);
    }

    [Fact]
    public async Task HandlerThundercloudPetAttachStatus12970SendsEmptyAnimationAndProgressWithoutPet()
    {
        using var fixture = await CreateThundercloudPetAttachStatusFixtureAsync(
            revengeTailActive: false,
            livePets: null);
        if (fixture is null)
        {
            return;
        }

        var frameStart = fixture.Sent.Count;

        await SendThundercloudPetAttachStatusCastAsync(fixture);

        var castFrames = fixture.Sent.Skip(frameStart).ToArray();
        var animation = Assert.Single(
            castFrames
                .Select(bytes =>
                    LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var parsed) ? parsed : null)
                .OfType<LegacySpellAnimationPacket>(),
            packet => packet.SpellId == PetAttachStatusSpellId);
        Assert.Empty(animation.Targets);

        var updated = GetSession(fixture.Handler, fixture.Connection);
        Assert.False(updated.Character.Attributes.ContainsKey("LivePets"));
        Assert.Equal(
            500,
            Assert.Single(updated.PlayerSpells.Spells, entry => entry.Id == PetAttachStatusSpellId).Experience);
    }

    [Fact]
    public async Task HandlerThundercloudPetAttachStatus12970DoesNotRefreshActiveThunderbolt()
    {
        var existingExpiry = DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeMilliseconds();
        var livePet = PetAttachStatusLivePet + ",0," +
            existingExpiry.ToString(CultureInfo.InvariantCulture);
        using var fixture = await CreateThundercloudPetAttachStatusFixtureAsync(
            revengeTailActive: false,
            livePets: livePet);
        if (fixture is null)
        {
            return;
        }

        await SendThundercloudPetAttachStatusCastAsync(fixture);

        var updated = GetSession(fixture.Handler, fixture.Connection);
        var livePetFields = updated.Character.Attributes["LivePets"].Split(
            ',',
            StringSplitOptions.TrimEntries);
        Assert.Equal(existingExpiry.ToString(CultureInfo.InvariantCulture), livePetFields[11]);
    }

    private static async Task SendThundercloudPetAttachStatusCastAsync(
        ThundercloudPetAttachStatusFixture fixture)
    {
        await fixture.Handler.HandleFrameAsync(
            fixture.Connection,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PetAttachStatusOwnerUid,
                OpponentUid: PetAttachStatusOwnerUid,
                X: 833,
                Y: 586,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: PetAttachStatusSpellId)).ToFrameBytes()));
    }

    private static async Task<ThundercloudPetAttachStatusFixture?>
        CreateThundercloudPetAttachStatusFixtureAsync(
            bool revengeTailActive,
            string? livePets = PetAttachStatusLivePet)
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return null;
        }

        var dbRoot = Path.Combine(
            Path.GetTempPath(),
            "wolf-world-thundercloud-pet-attach-status-" + Guid.NewGuid());
        var usersRoot = Path.Combine(dbRoot, "Users");
        var spellsRoot = Path.Combine(dbRoot, "PlayersSpells");
        StringWriter? log = null;
        try
        {
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(spellsRoot);
            WriteSnapshot(
                usersRoot,
                PetAttachStatusOwnerUid,
                uid: PetAttachStatusOwnerUid,
                mapId: 1_015,
                x: 830,
                y: 584,
                classCode: 16_000,
                statusFlags: revengeTailActive
                    ? BuildStatusFlags(PetAttachStatusRevengeTailFlag)
                    : null,
                livePets: livePets,
                extraCharacterLines: revengeTailActive
                    ? "LegacyRevengeTailChange=5\r\n" + DisableLegacyWeaponProcLines
                    : DisableLegacyWeaponProcLines);
            File.WriteAllBytes(
                Path.Combine(spellsRoot, $"{PetAttachStatusOwnerUid}.bin"),
                PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([PetAttachStatusSpellId]));

            var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var ticket = handoff.Issue(
                PetAttachStatusOwnerUid,
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

            return new ThundercloudPetAttachStatusFixture(
                dbRoot,
                handler,
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

    private sealed class ThundercloudPetAttachStatusFixture(
        string dbRoot,
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        List<byte[]> sent,
        StringWriter log) : IDisposable
    {
        public WorldServerFrameHandler Handler { get; } = handler;

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
