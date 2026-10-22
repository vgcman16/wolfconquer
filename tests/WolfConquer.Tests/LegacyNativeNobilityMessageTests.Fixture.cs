using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeNobilityMessageTests : IDisposable
{
    private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;

    public LegacyNativeNobilityMessageTests()
    {
        // Scope the genuine shared RNG before any fixture Player field birth.
        LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
    }

    public void Dispose() => LegacyNativePoolRandom.GetRandom = previousRandom;

    private sealed class Scope : IDisposable
    {
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? previousPool = LegacyNativePoolClients.GamePoll;
        private readonly CultureInfo previousCulture = CultureInfo.CurrentCulture, previousUi = CultureInfo.CurrentUICulture;
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativePacket Packet => Native.Packet;
        internal ConcurrentDictionary<uint, LegacyNativeGameClientRuntime> Pool { get; } = new();

        internal unsafe Scope()
        {
            LegacyNativePoolClients.GamePoll = Pool;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(), 0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Packet.Size = 321; Packet.Seek(237);
        }

        public void Dispose()
        {
            LegacyNativePoolClients.GamePoll = previousPool;
            CultureInfo.CurrentCulture = previousCulture; CultureInfo.CurrentUICulture = previousUi;
            Native.Dispose();
        }
    }

    private static byte[] Icon(uint uid, string text)
    {
        byte[] value = Encoding.UTF8.GetBytes(text), bytes = new byte[123 + value.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2091);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), uid);
        bytes[120] = 1; BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(121), checked((ushort)value.Length));
        value.CopyTo(bytes, 123); return bytes;
    }
}
