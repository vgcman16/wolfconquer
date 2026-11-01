using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTeleportAttributePreservationTests
{
    // Existing unbound component contract, actual Store/CAS and public handler
    // routes. No native attachment, owner, lease or receipt is fabricated.
    private sealed class Component : IDisposable
    {
        private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Handlers =
            PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        private readonly WorldSessionStore store;
        private readonly ConquerTcpConnection connection;
        private readonly StringWriter log = new();
        private readonly List<List<byte[]>> peerOutput = [];
        private readonly List<ConquerTcpConnection> peers = [];
        internal readonly List<byte[]> Sent = [];
        internal int Publications;
        internal WorldSession Current => Assert.IsType<WorldSession>(store.TryGet(connection, out var current) ? current : null);
        internal Component(IReadOnlyDictionary<string, string> attributes, long map = 1015)
        {
            store = new WorldSessionStore((_, _, _) => Publications++);
            connection = Connection(Sent);
            store.Set(connection, Session(Character(attributes, map)));
        }
        internal List<byte[]> AddPeer(uint uid, uint dynamicId)
        {
            var output = new List<byte[]>(); peerOutput.Add(output);
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture) };
            var character = Character(fields, 1000, uid) with { X = 500, Y = 650 };
            var peer = Connection(output); peers.Add(peer);
            store.Set(peer, Session(character)); return output;
        }
        private static WorldSession Session(CharacterSnapshot character) => new(character.Uid, character.Name, character,
            PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);
        private WorldPacketHandlerContext Context() => new(connection, log, Current, Sessions: store,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        internal ValueTask<WorldSession?> TeleportAsync(LegacyTeleportDestination destination) =>
            WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(Context(), Current, destination, 4_321, CancellationToken.None);
        internal async Task TalkAsync(string message) => await Handlers.HandleAsync(Context(),
            ReadFrame(LegacyMsgMessagePacket.SystemTalk(message).ToFrameBytes()));
        internal void ClearOutput() { Sent.Clear(); foreach (var output in peerOutput) output.Clear(); }
        private static ConquerTcpConnection Connection(List<byte[]> output) => new(null,
            new IPEndPoint(IPAddress.Loopback, 9958), (memory, _) =>
            { output.Add(memory.ToArray()); return ValueTask.CompletedTask; });
        internal static PacketFrame ReadFrame(byte[] bytes)
        {
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame;
        }
        public void Dispose()
        {
            store.TryRemove(connection, out _);
            foreach (var peer in peers) store.TryRemove(peer, out _);
            log.Dispose();
        }
    }
}
