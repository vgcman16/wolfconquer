using System.Buffers;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicMixedProducerTests
{
    private sealed partial class MixedFixture
    {
        private RootLoopbackFramePeer? peer;
        internal void OpenRuntime()
        {
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var actual, out var reason), reason);
            Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(actual);
        }

        internal async Task ReopenFrameAsync()
        {
            OpenRuntime(); observerFailure = null;
            Handler = new(Data.Loader, null, Catalog, LegacyRuntimeUpdateLoopOptions.Disabled, log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
            lifetime = new(); var native = new LegacyNativeGameClientRuntime();
            // A real loopback socket and the production registered writer own
            // the gate. Client observation remains ordered and independent.
            peer = new RootLoopbackFramePeer(bytes =>
            {
                try { BeforeSend?.Invoke(bytes); } catch (Exception error) { observerFailure ??= error; throw; }
                Sent.Add(bytes); return ValueTask.CompletedTask;
            }, native);
            connection = peer.Connection;
            await SendAsync(new LegacyMsgLoginGamePacket(LegacyRelicMixedProducerData.CharacterUid).ToFrameBytes());
            Assert.Same(native, Native); Assert.NotNull(native.OrdinarySpellBinding);
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(connection, Current));
            AssertConverged(); Sent.Clear();
        }

        internal async Task SendAsync(byte[] bytes)
        {
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
            await Handler.HandleFrameAsync(connection!, frame, lifetime!.Token);
            if (peer is { } actualPeer) await actualPeer.DrainObservations();
            if (observerFailure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
        }

        internal async Task CloseAsync()
        {
            BeforeSend = null; var errors = new List<Exception>();
            if (connection is not null)
            {
                try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
                try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
            }
            connection = null;
            if (peer is { } actualPeer)
            {
                try { await actualPeer.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
                peer = null;
            }
            if (Runtime is not null) try { await Runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            Runtime = null!; lifetime?.Dispose(); lifetime = null;
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
    }
}
