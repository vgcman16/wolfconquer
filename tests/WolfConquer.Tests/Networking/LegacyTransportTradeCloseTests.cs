using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Real DH/cipher/socket dispatch and orderly peer EOF, with strongly retained
// socket-born owners. These are backend file/session tests, not game-client,
// authentication, SQL, power-loss, or native SaveClient-queue parity proof.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyTransportTradeCloseTests
{
    [Fact]
    public async Task PendingRecipientRealCloseReleasesRequesterForAnotherRealPeer()
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        using var files = new SnapshotInput();
        using var log = new NativePlayerDiagnosticLog();
        var handler = CreateHandler(files, log);
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            var requester = await Login(fixture, handler, SnapshotInput.LeftUid, 0);
            var departed = await Login(fixture, handler, SnapshotInput.RightUid, 1);
            var next = await Login(fixture, handler, SnapshotInput.ThirdUid, 2);
            await Trade(fixture, requester, new(0) { UID = departed.Uid });
            Assert.False(Current(handler, requester).LegacyTradeActive);
            Assert.False(Current(handler, departed).LegacyTradeActive);
            Assert.Contains("type=0; result=requested", log.Text);

            // Recipient has sent no Trade packet. Only its genuine transport ends.
            await fixture.CloseClientTransportAsync(departed.SocketIndex);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(departed.Connection));
            Assert.Same(departed.Client, departed.Connection.NativeGameClient);
            Assert.Same(departed.Player, departed.Client.Player);

            await Trade(fixture, requester, new(0) { UID = next.Uid });
            await Trade(fixture, next, new(1) { UID = requester.Uid });
            Assert.True(Current(handler, requester).LegacyTradeActive);
            Assert.True(Current(handler, next).LegacyTradeActive);
            await Trade(fixture, requester, new(2));
            Assert.False(Current(handler, requester).LegacyTradeActive);
            Assert.False(Current(handler, next).LegacyTradeActive);
            files.AssertWallet(requester.Uid, 1_000, 50, hasClaim: false);
            files.AssertWallet(departed.Uid, 2_000, 60, hasClaim: false);
            files.AssertWallet(next.Uid, 3_000, 70, hasClaim: false);
            files.AssertUnchangedItemFiles();
            log.AssertNoWorldRuntimeDiagnostics();
        }, handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
    }

    [Fact]
    public async Task RealCloseRefundsMoneyAndCpEscrowOnceWithoutMovingOfferedInventoryOrWarehouseItems()
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        using var files = new SnapshotInput();
        using var log = new NativePlayerDiagnosticLog();
        var handler = CreateHandler(files, log);
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            var left = await Login(fixture, handler, SnapshotInput.LeftUid, 0);
            var right = await Login(fixture, handler, SnapshotInput.RightUid, 1);
            await OpenAndOffer(fixture, handler, files, left, right);
            Assert.Contains("type=6; result=itemOffered", log.Text);

            await fixture.CloseClientTransportAsync(right.SocketIndex);

            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(right.Connection));
            Assert.Same(right.Client, right.Connection.NativeGameClient);
            Assert.Same(right.Player, right.Client.Player);
            Assert.False(Current(handler, left).LegacyTradeActive);
            Assert.Equal(1_000, Current(handler, left).Character.Money);
            Assert.Equal(50, Current(handler, left).Character.ConquerPoints);
            files.AssertWallet(left.Uid, 1_000, 50, hasClaim: false);
            files.AssertWallet(right.Uid, 2_000, 60, hasClaim: false);
            files.AssertUnchangedItemFiles();

            // Both the cancellation callback and the awaited transport-close
            // path may run. Further real cancel frames must not refund again.
            await Trade(fixture, left, new(2));
            await Trade(fixture, left, new(2));
            Assert.False(Current(handler, left).LegacyTradeActive);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(right.Connection));
            files.AssertWallet(left.Uid, 1_000, 50, hasClaim: false);
            files.AssertWallet(right.Uid, 2_000, 60, hasClaim: false);
            files.AssertUnchangedItemFiles();
            log.AssertNoWorldRuntimeDiagnostics();
        }, handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
    }

    [WindowsTradeJournalFailureFact]
    public async Task RealCloseRetainsEscrowAndDepartedSessionWhenActualItemFileSharingRefusesRefund()
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        using var files = new SnapshotInput();
        using var log = new NativePlayerDiagnosticLog();
        var handler = CreateHandler(files, log);
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            var left = await Login(fixture, handler, SnapshotInput.LeftUid, 0);
            var right = await Login(fixture, handler, SnapshotInput.RightUid, 1);
            await OpenAndOffer(fixture, handler, files, left, right);
            Assert.Contains("type=6; result=itemOffered", log.Text);
            var leftEscrow = Current(handler, left);
            var rightEscrow = Current(handler, right);

            // A real Windows sharing violation, not a fabricated persistence
            // result. The socket still reaches server EOF under this lock.
            using (File.Open(files.ItemPath(right.Uid), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await fixture.CloseClientTransportAsync(right.SocketIndex);
                Assert.Same(leftEscrow, Current(handler, left));
                Assert.Same(rightEscrow, Current(handler, right));
                Assert.True(Current(handler, left).LegacyTradeActive);
                Assert.True(Current(handler, right).LegacyTradeActive);
                files.AssertWallet(left.Uid, 700, 50, hasClaim: true);
                files.AssertWallet(right.Uid, 2_000, 50, hasClaim: true);
                Assert.Contains("Trade disconnect cleanup retained unresolved escrow", log.Text);
                Assert.Same(right.Client, right.Connection.NativeGameClient);
                Assert.Same(right.Player, right.Client.Player);
            }

            files.AssertUnchangedItemFiles();
            // After the actual refusal is released, the surviving participant
            // can explicitly retry cancellation; a second retry is idempotent.
            await Trade(fixture, left, new(2));
            Assert.False(Current(handler, left).LegacyTradeActive);
            files.AssertWallet(left.Uid, 1_000, 50, hasClaim: false);
            files.AssertWallet(right.Uid, 2_000, 60, hasClaim: false);
            await Trade(fixture, left, new(2));
            files.AssertWallet(left.Uid, 1_000, 50, hasClaim: false);
            files.AssertWallet(right.Uid, 2_000, 60, hasClaim: false);
            files.AssertUnchangedItemFiles();
            log.AssertNoWorldRuntimeDiagnostics();
        }, handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
    }

    private static WorldServerFrameHandler CreateHandler(SnapshotInput files, NativePlayerDiagnosticLog log) =>
        new(new CharacterSnapshotLoader(files.Users, files.Items), handoffService: null,
            worldData: files.Catalog,
            runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled with { LocalServerIdentity = new(2) },
            log: log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);

    private static async Task<Peer> Login(NativePlayerLoopback fixture, WorldServerFrameHandler handler,
        uint uid, int socketIndex)
    {
        var wire = await fixture.ConnectGameAsync(false);
        var birth = await fixture.NextAsync();
        Assert.Equal(NativePlayerLoopback.ProbeOpcode, birth.Opcode);
        var client = Assert.IsType<LegacyNativeGameClientRuntime>(birth.Connection.NativeGameClient);
        var player = Assert.IsType<LegacyNativePlayerRuntime>(client.Player);
        Assert.Equal(0u, player.UID);
        await wire.SendAsync(new LegacyMsgLoginGamePacket(uid).ToFrameBytes(), fixture.Token);
        var login = await fixture.NextAsync();
        Assert.Equal(LegacyMsgLoginGamePacket.Opcode, login.Opcode);
        Assert.Same(birth.Connection, login.Connection);
        var peer = new Peer(uid, socketIndex, wire, birth.Connection, client, player);
        var session = Current(handler, peer);
        Assert.Equal((long)uid, session.AccountId);
        Assert.Equal((long)uid, session.Character.Uid);
        Assert.Same(client, session.NativeGameClient);
        Assert.Same(player, session.NativePlayer);
        Assert.Equal(uid, player.UID);
        Assert.Equal((ushort)2, player.ServerID);
        Assert.True(player.IsCheckedPass);
        return peer;
    }

    private static async Task Trade(NativePlayerLoopback fixture, Peer peer, LegacyTradePacket packet)
    {
        await peer.Wire.SendAsync(packet.ToNativeFrameBytes(), fixture.Token);
        var dispatched = await fixture.NextAsync();
        Assert.Equal(WorldPacketOpcodes.LegacyTrade, dispatched.Opcode);
        Assert.Same(peer.Connection, dispatched.Connection);
        Assert.Same(peer.Client, dispatched.Connection.NativeGameClient);
        Assert.Same(peer.Player, dispatched.After?.NativePlayer);
    }

    private static async Task OpenAndOffer(NativePlayerLoopback fixture, WorldServerFrameHandler handler,
        SnapshotInput files, Peer left, Peer right)
    {
        await Trade(fixture, left, new(0) { UID = right.Uid });
        await Trade(fixture, right, new(1) { UID = left.Uid });
        Assert.True(Current(handler, left).LegacyTradeActive);
        Assert.True(Current(handler, right).LegacyTradeActive);
        await Trade(fixture, left, new(6) { ItemUID = SnapshotInput.LeftItemUid });
        await Trade(fixture, left, new(7) { Money = 300 });
        await Trade(fixture, right, new(12) { CPS = 10 });
        Assert.Equal(700, Current(handler, left).Character.Money);
        Assert.Equal(50, Current(handler, right).Character.ConquerPoints);
        files.AssertWallet(left.Uid, 700, 50, hasClaim: true);
        files.AssertWallet(right.Uid, 2_000, 50, hasClaim: true);
        files.AssertUnchangedItemFiles();
    }

    private static WorldSession Current(WorldServerFrameHandler handler, Peer peer) =>
        Assert.IsType<WorldSession>(handler.ReadCurrentSessionForNativeOwnerObservation(peer.Connection));

    private sealed record Peer(uint Uid, int SocketIndex, NativePlayerWireClient Wire,
        ConquerTcpConnection Connection, LegacyNativeGameClientRuntime Client, LegacyNativePlayerRuntime Player);

    private sealed class SnapshotInput : IDisposable
    {
        internal const uint LeftUid = 1_399_201;
        internal const uint RightUid = 1_399_202;
        internal const uint ThirdUid = 1_399_203;
        internal const uint LeftItemUid = 5_001;
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-transport-trade-close-" + Guid.NewGuid().ToString("N"));
        private readonly Dictionary<uint, byte[]> itemPreimages = [];
        internal string Users => Path.Combine(root, "Users");
        internal string Items => Path.Combine(root, "PlayersItems");
        internal WorldDataCatalog Catalog { get; }
        internal string ItemPath(uint uid) => Path.Combine(Items, uid.ToString(CultureInfo.InvariantCulture) + ".bin");

        internal SnapshotInput()
        {
            foreach (var family in new[] { "Users", "PlayersItems", "PlayersSpells", "PlayersProfs", "PlayersCombatGear", "Quests" })
                Directory.CreateDirectory(Path.Combine(root, family));
            var fields = Enumerable.Repeat("0", 66).ToArray();
            fields[0] = "1088001";
            fields[1] = "Meteor";
            fields[ItemType.DurabilityFieldIndex] = "10";
            Catalog = LegacyItemWorldDataFixture.Create(new ItemType(1_088_001, fields[1], fields));
            Seed(LeftUid, 1_000, 50, LeftItemUid);
            Seed(RightUid, 2_000, 60, 5_002);
            Seed(ThirdUid, 3_000, 70, 5_003);
        }

        private void Seed(uint uid, long money, long cps, uint itemUid)
        {
            File.WriteAllText(Path.Combine(Users, uid.ToString(CultureInfo.InvariantCulture) + ".ini"), $$"""
                [Character]
                UID={{uid}}
                Name=Trader{{uid}}
                Body=1003
                Face=154
                Haire=27
                Class=10
                FirstClass=0
                SecoundeClass=0
                Reborn=0
                Level=120
                Experience=0
                Map=1002
                X=410
                Y=354
                PMap=1002
                PMapX=410
                PMapY=354
                Strength=100
                Agility=50
                Vitaliti=100
                Spirit=0
                Atributes=0
                MinHitPoints=1000
                MinMana=100
                Money={{money}}
                ConquerPoints={{cps}}
                BoundConquerPoints=0
                BankCps=0
                PkPoints=0
                Online=0
                SecurityPass=0,0,0
                WHMoney=0
                Angle=1
                Day={{DateTime.Now.DayOfYear}}
                """);
            var inventory = new PlayerItemSnapshotItem
            {
                Uid = itemUid, ItemId = 1_088_001, Durability = 5, MaximumDurability = 10, StackSize = 1,
            };
            var warehouse = new PlayerItemSnapshotItem
            {
                Uid = uid + 100, ItemId = 1_088_001, WarehouseId = 1000, StackSize = 1,
            };
            new PlayerItemSnapshotLoader(Items).SaveByCharacterUid(uid,
                new PlayerItemSnapshot([inventory], [], [], [], []) { WarehouseItems = [warehouse] });
            new PlayerSpellSnapshotLoader(Path.Combine(root, "PlayersSpells")).SaveByCharacterUid(uid, PlayerSpellSnapshot.Empty);
            new PlayerProficiencySnapshotLoader(Path.Combine(root, "PlayersProfs")).SaveByCharacterUid(uid, PlayerProficiencySnapshot.Empty);
            new CombatGearSnapshotLoader(Path.Combine(root, "PlayersCombatGear")).SaveByCharacterUid(uid, CombatGearSnapshot.Empty);
            new PlayerQuestSnapshotLoader(Path.Combine(root, "Quests")).SaveByCharacterUid(uid, PlayerQuestSnapshot.Empty);
            itemPreimages.Add(uid, File.ReadAllBytes(ItemPath(uid)));
        }

        internal void AssertWallet(uint uid, long money, long cps, bool hasClaim)
        {
            var character = CharacterSnapshotParser.ParseText(File.ReadAllText(
                Path.Combine(Users, uid.ToString(CultureInfo.InvariantCulture) + ".ini")));
            Assert.Equal(money, character.Money);
            Assert.Equal(cps, character.ConquerPoints);
            Assert.True(LegacyTradeEscrowClaims.TryRead(character.Attributes, out var claim, out var reason), reason);
            Assert.Equal(hasClaim, claim is not null);
        }

        internal void AssertUnchangedItemFiles()
        {
            foreach (var (uid, original) in itemPreimages)
                Assert.Equal(original, File.ReadAllBytes(ItemPath(uid)));
        }

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true); // Only this generated GUID fixture.
        }
    }
}
