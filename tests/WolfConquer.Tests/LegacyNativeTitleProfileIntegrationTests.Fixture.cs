using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Metadata = WolfConquer.Core.Data.LegacyNativeTitleStorage;

namespace WolfConquer.Tests;

// Cross-owner comma-record integration only: no SaveClient/INI write, account
// login, automatic title qualification, Process handler or appearance adoption.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeTitleProfileIntegrationTests
{
    private sealed class Scope : IDisposable
    {
        private readonly Dictionary<uint, Metadata> oldTitles = Metadata.Titles;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo oldUiCulture = CultureInfo.CurrentUICulture;
        internal Dictionary<uint, Metadata> Rows { get; } = new();
        internal NativeSpellTestFixture Source { get; } = new();
        internal NativeSpellTestFixture Reloaded { get; } = new();

        internal unsafe Scope()
        {
            Metadata.Titles = Rows;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            foreach (var native in new[] { Source, Reloaded })
            {
                Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                    0, (IntPtr)native.Packet.Memory, LegacyNativePacket.MAX_SIZE);
                native.Packet.Size = 321;
                native.Packet.Seek(237);
                Assert.Null(native.Owner.Socket!.Connection);
                Assert.Null(native.Owner.Socket.Crypto);
                Assert.False(native.Owner.FullLoading);
            }
        }

        internal Metadata Row(uint key, uint id, uint sub, uint score)
        {
            var row = new Metadata { ID = id, SubID = sub, Score = score, Name = "Owned integration row" };
            Rows.Add(key, row);
            return row;
        }

        public void Dispose()
        {
            Metadata.Titles = oldTitles;
            // Each genuine fixture restores the seal it observed: reverse birth order.
            Reloaded.Dispose();
            Source.Dispose();
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUiCulture;
        }
    }

    // Independent required-field protobuf writer, not the production serializer.
    // The parent's separate pinned-library literal controls anchor this shape.
    private static byte[] Frame(uint action, uint score, uint id, uint sub,
        bool title = true, uint titleValue = 0)
    {
        var payload = new List<byte>();
        UInt(payload, 1, action); UInt(payload, 2, score);
        UInt(payload, 3, id); UInt(payload, 4, sub);
        if (title)
        {
            var nested = new List<byte>();
            UInt(nested, 1, id); UInt(nested, 2, sub);
            UInt(nested, 3, titleValue); UInt(nested, 4, 0);
            payload.Add(0x2A); Varint(payload, (uint)nested.Count); payload.AddRange(nested);
        }
        var frame = new byte[4 + payload.Count];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2313);
        payload.CopyTo(frame, 4);
        return frame;
    }

    private static void UInt(List<byte> bytes, int tag, uint value)
    {
        bytes.Add((byte)(tag << 3));
        Varint(bytes, value);
    }

    private static void Varint(List<byte> bytes, uint value)
    {
        while (value > 127)
        {
            bytes.Add((byte)((value & 127) | 128));
            value >>= 7;
        }
        bytes.Add((byte)value);
    }

    private static void Frames(Queue<byte[]> queue, params byte[][] expected)
    {
        var actual = queue.ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i]);
            if (i > 0) Assert.NotSame(actual[i - 1], actual[i]);
        }
    }
}
