using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    private const uint Uid = 1_293_600;

    [Fact]
    public async Task AcceptedLoginPreservesExistingHairfaceAndItsConstructorOwner()
    {
        using var input = new LoginInput();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            var (_, foreignBirth) = await ConnectBornAsync(fixture);
            var foreignOwner = foreignBirth.Connection.NativeGameClient!;
            Assert.NotSame(client, foreignOwner);
            var existing = new LegacyNativeHairfaceStorageRuntime(client);
            var state = ReadEmptyState(existing);
            client.HairfaceStorage = existing; // Controlled preexisting state, not a birth path.

            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);

            AssertAcceptedPlayer(client, accepted, player, flags, random);
            AssertPreservedHairface(client, existing, client, state);
            Assert.Null(foreignOwner.HairfaceStorage);
            Assert.Null(foreignOwner.Beasts);
            Assert.Equal(0U, foreignOwner.Player.UID);
            Assert.Equal(0U, foreignOwner.Player.Online);
            Assert.False(foreignOwner.Player.RelicSpirit);
            AssertMarkedFile(input);
            Assert.Contains("legacy MsgLoginGame Success", log.Text);
        });
    }

    [Fact]
    public async Task FailedLocalWriteRetainsExistingHairfaceBeforeConditionalBirth()
    {
        using var input = new LoginInput();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            player.Online = 9;
            var existing = new LegacyNativeHairfaceStorageRuntime(client);
            var state = ReadEmptyState(existing);
            client.HairfaceStorage = existing;

            using (var held = new FileStream(input.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var rejected = await SendLoginAsync(fixture, wire);
                Assert.Null(rejected.Before);
                Assert.Null(rejected.After);
                Assert.Equal(0U, player.UID);
                Assert.Equal(9U, player.Online);
                Assert.False(player.RelicSpirit);
                Assert.Same(player, client.Player);
                Assert.Same(flags, player.BitVector);
                Assert.Same(random, player.MyRandom);
                Assert.Null(client.Beasts);
                AssertPreservedHairface(client, existing, client, state);
                Assert.Contains("reason=snapshotWrite", log.Text);
                Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
            }
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            var fresh = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
            Assert.Equal(CharacterSnapshotLoadStatus.Success, fresh.Status);
            Assert.Equal(7L, fresh.Snapshot!.Online);
            Assert.Equal("1", fresh.Snapshot.Attributes["Angle"]);
        });
    }

    [Fact]
    public async Task RepeatedAcceptedLoginRetainsTheSameBornHairface()
    {
        using var input = new LoginInput();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            Assert.Null(client.HairfaceStorage);
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            AssertAcceptedPlayer(client, accepted, player, flags, random);
            var storage = AssertNewHairface(client);
            var state = ReadEmptyState(storage);
            var bytes = File.ReadAllBytes(input.FilePath);

            var repeated = await SendLoginAsync(fixture, wire);

            Assert.Same(accepted, repeated.Before);
            Assert.Same(accepted, repeated.After);
            AssertPreservedHairface(client, storage, client, state);
            AssertAcceptedPlayer(client, accepted, player, flags, random);
            Assert.Equal(input.ExpectedMarkerBytes, bytes);
            Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
            AssertMarkedFile(input);
            Assert.Contains("ignored repeated legacy MsgLoginGame", log.Text);
        });
    }

    [Fact]
    public async Task DuplicateRejectionLeavesHairfaceAbsentAndThirdRetryOwnsANewHairface()
    {
        using var input = new LoginInput();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (firstWire, firstBirth) = await ConnectBornAsync(fixture);
            var firstClient = firstBirth.Connection.NativeGameClient!;
            var first = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, firstWire)).After);
            var firstStorage = AssertNewHairface(firstClient);
            var firstState = ReadEmptyState(firstStorage);
            var (secondWire, secondBirth) = await ConnectBornAsync(fixture);
            var secondClient = secondBirth.Connection.NativeGameClient!;
            Assert.Null(secondClient.HairfaceStorage);

            var rejected = await SendLoginAsync(fixture, secondWire);

            Assert.Null(rejected.After);
            Assert.Null(secondClient.HairfaceStorage);
            Assert.Null(secondClient.Beasts);
            Assert.Equal(0U, secondClient.Player.UID);
            Assert.Equal(0U, secondClient.Player.Online);
            Assert.False(secondClient.Player.RelicSpirit);
            AssertPreservedHairface(firstClient, firstStorage, firstClient, firstState);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(firstBirth.Connection));
            AssertMarkedFile(input);
            Assert.Contains("rejected duplicate legacy MsgLoginGame", log.Text);

            var (thirdWire, thirdBirth) = await ConnectBornAsync(fixture);
            var thirdClient = thirdBirth.Connection.NativeGameClient!;
            Assert.Null(thirdClient.HairfaceStorage);
            var third = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, thirdWire)).After);
            var thirdStorage = AssertNewHairface(thirdClient);
            var thirdState = ReadEmptyState(thirdStorage);
            Assert.NotSame(firstClient, thirdClient);
            Assert.NotSame(firstStorage, thirdStorage);
            Assert.NotSame(firstState.Items, thirdState.Items);
            Assert.NotSame(firstState.Objects, thirdState.Objects);
            Assert.Same(thirdClient, third.NativeGameClient);
            Assert.Same(thirdClient.Player, third.NativePlayer);
            Assert.NotSame(first.NativePlayer, third.NativePlayer);
            Assert.Equal(Uid, thirdClient.Player.UID);
            Assert.Equal(1U, thirdClient.Player.Online);
            Assert.True(thirdClient.Player.RelicSpirit);
            AssertPreservedHairface(firstClient, firstStorage, firstClient, firstState);
            AssertMarkedFile(input);
        });
    }

    [Fact]
    public async Task DirectionCopyRetainsTheAcceptedHairfaceAndPrivateState()
    {
        using var input = new LoginInput();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            AssertAcceptedPlayer(client, accepted, player, flags, random);
            var storage = AssertNewHairface(client);
            var state = ReadEmptyState(storage);
            var beasts = client.Beasts;

            await wire.SendAsync(new MsgActionPacket
            {
                ObjId = Uid, Type = MsgActionType.ChangeDirection, Facing = 3,
            }.ToFrameBytes(), fixture.Token);
            var copied = Assert.IsType<WorldSession>((await fixture.NextAsync()).After);

            Assert.NotSame(accepted, copied);
            AssertAcceptedPlayer(client, copied, player, flags, random);
            AssertPreservedHairface(client, storage, client, state);
            Assert.Same(beasts, client.Beasts);
            Assert.Equal("3", copied.Character.Attributes["Angle"]);
            // Direction changes the live session only; immediate saved Angle remains one.
            AssertMarkedFile(input);
        });
    }

    [Fact]
    public async Task InternalHandoffLeavesTheSocketBornHairfaceFieldUnborn()
    {
        using var input = new LoginInput();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            Assert.Null(client.HairfaceStorage);
            var ticket = handoff.Issue(Uid, "hairface-handoff", ConquerRuntimeDefaults.WorldServer);

            await wire.SendAsync(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes(), fixture.Token);
            var dispatch = await fixture.NextAsync();

            Assert.Equal(WorldPacketOpcodes.InternalWorldEnterRequest, dispatch.Opcode);
            var accepted = Assert.IsType<WorldSession>(dispatch.After);
            Assert.Same(client, accepted.NativeGameClient);
            Assert.Same(player, accepted.NativePlayer);
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Null(client.HairfaceStorage);
            Assert.Null(client.Beasts);
            Assert.Equal(0U, player.UID);
            Assert.Equal(0U, player.Online);
            Assert.False(player.RelicSpirit);
            Assert.Equal(7L, accepted.Character.Online);
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
            var fresh = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
            Assert.Equal(CharacterSnapshotLoadStatus.Success, fresh.Status);
            Assert.Equal(7L, fresh.Snapshot!.Online);
            Assert.Equal("1", fresh.Snapshot.Attributes["Angle"]);
        }, handoff: handoff);
    }

    [Fact]
    public async Task NewRoleLeavesTheSocketBornHairfaceFieldUnborn()
    {
        using var input = new LoginInput(create: false);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            Assert.Null((await SendLoginAsync(fixture, wire)).After);
            Assert.Null(client.HairfaceStorage);
            Assert.False(File.Exists(input.FilePath));

            await wire.SendAsync(new LegacyNewRolePacket("HairfaceRole", 1008, 12).ToFrameBytes(), fixture.Token);
            var created = await fixture.NextAsync();

            Assert.Equal(LegacyNewRolePacket.Opcode, created.Opcode);
            var accepted = Assert.IsType<WorldSession>(created.After);
            Assert.Same(client, accepted.NativeGameClient);
            Assert.Same(player, accepted.NativePlayer);
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Null(client.HairfaceStorage);
            Assert.Null(client.Beasts);
            Assert.Equal(0U, player.UID);
            Assert.Equal(0U, player.Online);
            Assert.False(player.RelicSpirit);
            Assert.Equal(0L, accepted.Character.Online);
            var createdBytes = File.ReadAllBytes(input.FilePath);
            var fresh = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
            Assert.Equal(CharacterSnapshotLoadStatus.Success, fresh.Status);
            Assert.Equal(0L, fresh.Snapshot!.Online);
            Assert.Equal(createdBytes, File.ReadAllBytes(input.FilePath));
            Assert.Contains("legacy NewRole Success", log.Text);
            Assert.DoesNotContain("accepted-login local prefix", log.Text);
        });
    }

    private static LegacyNativeHairfaceStorageRuntime AssertNewHairface(LegacyNativeGameClientRuntime client)
    {
        var storage = Assert.IsType<LegacyNativeHairfaceStorageRuntime>(client.HairfaceStorage);
        Assert.Same(client, ReadPrivateField(storage, "Owner"));
        ReadEmptyState(storage);
        return storage;
    }

    private static (List<LegacyNativeHairfaceStorageTable.Hairface> Items,
        LegacyNativeHairfaceStorageTable.Hairface[] Objects) ReadEmptyState(
        LegacyNativeHairfaceStorageRuntime storage)
    {
        var items = Assert.IsType<List<LegacyNativeHairfaceStorageTable.Hairface>>(
            ReadPrivateField(storage, "items"));
        var objects = Assert.IsType<LegacyNativeHairfaceStorageTable.Hairface[]>(
            ReadPrivateField(storage, "objects"));
        Assert.Empty(items);
        Assert.Empty(objects);
        Assert.Same(objects, storage.Objects);
        Assert.Same(storage.Objects, storage.Objects);
        return (items, objects);
    }

    private static void AssertPreservedHairface(
        LegacyNativeGameClientRuntime client, LegacyNativeHairfaceStorageRuntime expected,
        LegacyNativeGameClientRuntime expectedOwner,
        (List<LegacyNativeHairfaceStorageTable.Hairface> Items,
            LegacyNativeHairfaceStorageTable.Hairface[] Objects) state)
    {
        Assert.Same(expected, client.HairfaceStorage);
        Assert.Same(expectedOwner, ReadPrivateField(expected, "Owner"));
        var current = ReadEmptyState(expected);
        Assert.Same(state.Items, current.Items);
        Assert.Same(state.Objects, current.Objects);
    }

    private static object? ReadPrivateField(LegacyNativeHairfaceStorageRuntime storage, string name) =>
        typeof(LegacyNativeHairfaceStorageRuntime)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.GetValue(storage);

    private static void AssertAcceptedPlayer(
        LegacyNativeGameClientRuntime client, WorldSession session, LegacyNativePlayerRuntime player,
        LegacyNativeStatusFlagsBigVector32 flags, Random random)
    {
        Assert.Same(client, session.NativeGameClient);
        Assert.Same(player, session.NativePlayer);
        Assert.Same(player, client.Player);
        Assert.Same(client, player.Owner);
        Assert.Same(flags, player.BitVector);
        Assert.Same(random, player.MyRandom);
        Assert.Equal(Uid, player.UID);
        Assert.Equal(1U, player.Online);
        Assert.True(player.RelicSpirit);
        Assert.Equal(1L, session.Character.Online);
        Assert.Equal("1", session.Character.Attributes["Online"]);
        var beasts = Assert.IsType<LegacyNativeBeastsRuntime>(client.Beasts);
        Assert.Same(client, beasts.Owner);
        Assert.Equal((byte)1, beasts.FixedLevel);
        Assert.False(beasts.Activated);
        Assert.Equal(0U, beasts.Points);
        Assert.Equal(0U, beasts.flag);
        Assert.Equal((byte)0, beasts.Level);
    }

    private static void AssertMarkedFile(LoginInput input)
    {
        Assert.Equal(input.ExpectedMarkerBytes, File.ReadAllBytes(input.FilePath));
        var fresh = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
        Assert.Equal(CharacterSnapshotLoadStatus.Success, fresh.Status);
        Assert.Equal(1L, fresh.Snapshot!.Online);
        Assert.Equal("1", fresh.Snapshot.Attributes["Online"]);
        Assert.Equal("1", fresh.Snapshot.Attributes["Angle"]);
    }

    private static async Task<(NativePlayerWireClient Wire, NativePlayerDispatch Birth)> ConnectBornAsync(
        NativePlayerLoopback fixture)
    {
        var wire = await fixture.ConnectGameAsync(false);
        var birth = await fixture.NextAsync();
        Assert.Equal(NativePlayerLoopback.ProbeOpcode, birth.Opcode);
        Assert.Null(birth.Before);
        Assert.Null(birth.After);
        var client = Assert.IsType<LegacyNativeGameClientRuntime>(birth.Connection.NativeGameClient);
        Assert.Same(client, client.Player.Owner);
        Assert.Null(client.HairfaceStorage);
        return (wire, birth);
    }

    private static async Task<NativePlayerDispatch> SendLoginAsync(
        NativePlayerLoopback fixture, NativePlayerWireClient wire)
    {
        await wire.SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes(), fixture.Token);
        var dispatch = await fixture.NextAsync();
        Assert.Equal(LegacyMsgLoginGamePacket.Opcode, dispatch.Opcode);
        return dispatch;
    }

    private static async Task WithHandlerAsync(
        LoginInput input,
        Func<NativePlayerLoopback, WorldServerFrameHandler, NativePlayerDiagnosticLog, Task> body,
        AccountHandoffService? handoff = null)
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        using var log = new NativePlayerDiagnosticLog();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(input.Users), handoff,
            worldData: null, runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: log,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer,
            fixture => body(fixture, handler, log),
            handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
        log.AssertNoWorldRuntimeDiagnostics();
    }

    private sealed class LoginInput : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-hairface-login-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(root, "Users");
        internal string FilePath => Path.Combine(Users, Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        internal byte[] InitialBytes { get; }
        internal byte[] ExpectedMarkerBytes { get; }

        internal LoginInput(bool create = true)
        {
            Directory.CreateDirectory(Users);
            var lines = new List<string>
            {
                "[Character]", "UID=" + Uid.ToString(CultureInfo.InvariantCulture), "Name=HairfaceOwner",
                "Body=1008", "Face=154", "Class=7051", "Map=1015", "X=830", "Y=584",
                "PMap=1000", "PMapX=500", "PMapY=650", "Level=140",
                "Experience=0", "Reborn=2", "FirstClass=0", "SecoundeClass=7050", "Haire=27",
                "Agility=36", "Strength=0", "Vitaliti=447", "Spirit=0", "Atributes=12",
                "MinHitPoints=10836", "MinMana=5000", "Money=1000", "ConquerPoints=0",
                "BankCps=0", "BoundConquerPoints=0", "PkPoints=0", "Online=7", "Angle=1",
                "Day=" + DateTime.Now.DayOfYear.ToString(CultureInfo.InvariantCulture),
                "RelicSpirit=True", "", "[Unrelated]", "Keep=unchanged",
            };
            var text = string.Join("\r\n", lines) + "\r\n";
            InitialBytes = Encoding.UTF8.GetBytes(text);
            ExpectedMarkerBytes = Encoding.UTF8.GetBytes(text.Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
            if (create) File.WriteAllBytes(FilePath, InitialBytes);
        }

        public void Dispose()
        {
            var absolute = Path.GetFullPath(root);
            var temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            if (!Path.IsPathFullyQualified(root) ||
                !string.Equals(Path.GetDirectoryName(absolute), temporary, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(absolute).StartsWith("wolf-hairface-login-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the owned login fixture.");
            if (Directory.Exists(absolute)) Directory.Delete(absolute, recursive: true);
        }
    }
}
