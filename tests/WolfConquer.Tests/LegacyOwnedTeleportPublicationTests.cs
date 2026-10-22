using System.Buffers;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedTeleportStopsAfterOriginalOwnerFrameRetires(bool registerReplacement)
    {
        var f = new OwnedTeleportFixture();
        WorldSession? replacement = null;
        f.OnOwnerFrame = () =>
        {
            Assert.True(f.Sessions.TryRemove(f.Connection, out _));
            if (registerReplacement)
            {
                replacement = CreateSession(uid: f.Original.Character.Uid, name: "replacement", mapId: 1_000);
                f.Sessions.Set(f.Connection, replacement);
            }
        };
        Assert.Null(await f.RunAsync());
        AssertOwnedTeleportAction(Assert.Single(f.OwnerFrames), MsgActionType.Teleport);
        Assert.Empty(f.PeerFrames);
        if (registerReplacement)
        {
            Assert.True(f.Sessions.TryGet(f.Connection, out var stored));
            Assert.Same(replacement, stored);
            Assert.Equal(1_000, stored!.Character.MapId);
        }
        else
            Assert.False(f.Sessions.TryGet(f.Connection, out _));
    }

    [Fact]
    public async Task OwnedTeleportStopsAtOldMapPeerCallbackEvenWhenReboundRecordIsIdentical()
    {
        var f = new OwnedTeleportFixture(addPeer: true);
        WorldSession? rebound = null;
        f.OnPeerFrame = () =>
        {
            Assert.True(f.Sessions.TryGet(f.Connection, out rebound));
            Assert.Equal(700, rebound!.Character.MapId); // Original destination commit already happened.
            Assert.True(f.Sessions.TryRemove(f.Connection, out _));
            f.Sessions.Set(f.Connection, rebound!); // SAME record; native CLIENT identity changed.
        };
        Assert.Null(await f.RunAsync());
        Assert.Empty(f.OwnerFrames);
        Assert.True(LegacyRemoveEntityPacket.TryParse(OwnedTeleportReadFrame(Assert.Single(f.PeerFrames)), out _));
        Assert.True(f.Sessions.TryGet(f.Connection, out var stored));
        Assert.Same(rebound, stored);
    }

    [Fact]
    public async Task OwnedTeleportAcceptsSameClientRecordCopyWithoutDroppingUnrelatedMutation()
    {
        var f = new OwnedTeleportFixture();
        var calls = 0;
        f.OnOwnerFrame = () =>
        {
            if (++calls != 1) return;
            Assert.True(f.Sessions.TryGet(f.Connection, out var current));
            var copied = Assert.IsType<WorldSession>(current);
            f.Sessions.Set(f.Connection, copied with { Character = copied.Character with { Money = 123 } });
        };
        var result = Assert.IsType<WorldSession>(await f.RunAsync());
        Assert.Equal(123, result.Character.Money);
        Assert.True(f.Sessions.TryGet(f.Connection, out var stored));
        Assert.Same(result, stored);
        Assert.Equal(700, result.Character.MapId);
        Assert.Collection(f.OwnerFrames,
            bytes => AssertOwnedTeleportAction(bytes, MsgActionType.Teleport),
            bytes => AssertOwnedTeleportAction(bytes, MsgActionType.LegacyMerchantStartup),
            bytes => AssertOwnedTeleportAction(bytes, MsgActionType.StopVending),
            bytes => AssertOwnedTeleportAction(bytes, MsgActionType.LegacyColor),
            bytes => Assert.True(LegacyMapStatusPacket.TryParse(OwnedTeleportReadFrame(bytes), out _)));
        Assert.Empty(f.PeerFrames);
    }

    [Fact]
    public void OwnedTeleportCommitRejectsNewClientEvenIfCasRecordReferenceMatches()
    {
        var f = new OwnedTeleportFixture();
        Assert.True(f.Sessions.TryRemove(f.Connection, out _));
        f.Sessions.Set(f.Connection, f.Original);
        var destination = f.Original with { Character = f.Original.Character with { MapId = 700 } };
        Assert.False(WorldPacketHandlers.TryCommitOwnedLegacyTeleport(
            f.Context with { TeleportOwner = f.Owner }, f.Original, destination));
        Assert.True(f.Sessions.TryGet(f.Connection, out var stored));
        Assert.Same(f.Original, stored);
        Assert.Empty(f.OwnerFrames);
        Assert.Empty(f.PeerFrames);
    }

    [Fact]
    public void OwnedTeleportCaptureDoesNotInventStoreOrUidFallback()
    {
        var f = new OwnedTeleportFixture();
        Assert.False(WorldPacketHandlers.TryCaptureLegacyTeleportOwner(
            f.Context with { Sessions = null }, out var missingStore));
        Assert.Null(missingStore);
        Assert.True(f.Sessions.TryRemove(f.Connection, out _));
        Assert.False(WorldPacketHandlers.TryCaptureLegacyTeleportOwner(f.Context, out var unregistered));
        Assert.Null(unregistered);
        Assert.Empty(f.OwnerFrames);
    }

    private static PacketFrame OwnedTeleportReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes),
            out var frame, out _, out _));
        return frame;
    }

    private static void AssertOwnedTeleportAction(byte[] bytes, MsgActionType type)
    {
        Assert.True(MsgActionPacket.TryParse(OwnedTeleportReadFrame(bytes), out var packet));
        Assert.Equal(type, packet.Type);
    }

    private sealed class OwnedTeleportFixture
    {
        public WorldSessionStore Sessions { get; } = new();
        public List<byte[]> OwnerFrames { get; } = [];
        public List<byte[]> PeerFrames { get; } = [];
        public Action? OnOwnerFrame { get; set; }
        public Action? OnPeerFrame { get; set; }
        public ConquerTcpConnection Connection { get; }
        public WorldSession Original { get; }
        public WorldPacketHandlerContext Context { get; }
        public LegacyTeleportOwner Owner { get; }

        public OwnedTeleportFixture(bool addPeer = false)
        {
            Connection = new(null, null, (frame, _) =>
            {
                OwnerFrames.Add(frame.ToArray());
                OnOwnerFrame?.Invoke();
                return ValueTask.CompletedTask;
            });
            Original = CreateSession(uid: 30_001, mapId: 1_015, x: 50, y: 50,
                attributes: new Dictionary<string, string> { ["SetLocationType"] = "1" });
            Sessions.Set(Connection, Original);
            Context = new(Connection, new StringWriter(), Original, Sessions: Sessions);
            Assert.True(WorldPacketHandlers.TryCaptureLegacyTeleportOwner(Context, out var owner));
            Owner = Assert.IsType<LegacyTeleportOwner>(owner);
            if (addPeer)
            {
                var peer = new ConquerTcpConnection(null, null, (frame, _) =>
                {
                    PeerFrames.Add(frame.ToArray());
                    OnPeerFrame?.Invoke();
                    return ValueTask.CompletedTask;
                });
                Sessions.Set(peer, CreateSession(uid: 30_002, mapId: 1_015, x: 50, y: 50));
            }
        }

        public ValueTask<WorldSession?> RunAsync() =>
            WorldPacketHandlers.ExecuteOwnedLegacyTeleportAsync(
                Context, Owner, new LegacyTeleportRequest("owned-teleport-supporting-seam", 700, 40_001),
                new LegacyTeleportDestination(700, 60, 61, 40_001), 0, CancellationToken.None);
    }
}
