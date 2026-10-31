using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneBagTests
{
    private sealed partial class BagFixture
    {
        internal static byte[] Request(bool intoBag, uint uid = Target, ulong returnParameter = 1)
        {
            var bytes = new byte[99]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 99);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2137); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8), intoBag ? 0 : returnParameter);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 61); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(18), 511043495);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(22), 112244); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(26), 55);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(30), 66); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(91), 8888);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(95), uint.MaxValue); return bytes;
        }
        internal async Task SendAsync(byte[] bytes)
        {
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
            await Handler.HandleFrameAsync(connection!, frame, lifetime!.Token);
            if (observerFailure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
        }
        internal Task DispatchAsync() => SendAsync(Request(IntoBag));
        internal LegacyOwnedAdmissionRuntime Open()
        {
            bool success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason);
            if (opened is not null) owners.Add(opened); Assert.True(success, reason); return Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
        }
        internal async Task ReopenFrameAsync(Action? beforeLogin = null)
        {
            Runtime = Open(); beforeLogin?.Invoke();
            Handler = new(Data.Loader, null, Catalog, LegacyRuntimeUpdateLoopOptions.Disabled, Log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
            lifetime = new(); var cancellation = lifetime; var native = new LegacyNativeGameClientRuntime();
            connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
            {
                token.ThrowIfCancellationRequested(); var bytes = memory.ToArray();
                try { BeforeSend?.Invoke(bytes); } catch (Exception error) { observerFailure ??= error; throw; }
                Sent.Add(bytes); return ValueTask.CompletedTask;
            }, _ => { cancellation.Cancel(); return ValueTask.CompletedTask; }, native);
            await SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes()); Assert.Same(native, Native);
            Assert.NotNull(native.OrdinarySpellBinding); Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(connection, Current));
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
            foreach (var owner in owners.AsEnumerable().Reverse()) try { await owner.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            owners.Clear(); lifetime?.Dispose(); lifetime = null;
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
        internal void RestoreMetadata() { beasts?.Dispose(); beasts = null; Log.Dispose(); }
    }
}
