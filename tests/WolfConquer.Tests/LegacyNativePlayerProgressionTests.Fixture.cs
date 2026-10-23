using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Component-only: actual native Player, table owners and send queue. No writable
// substitute Level, original worker, SQL, game process or host-dispatch adoption.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativePlayerProgressionTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeSafeRandom priorRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime priorReset = LegacyNativePoolRandom.ResetRandom;
        private readonly Dictionary<LegacyNativeDbLevExp.Sort, Dictionary<byte, LegacyNativeDbLevExp>> priorLevels =
            LegacyNativePoolLevelInfo.LevelInfo;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal Dictionary<byte, LegacyNativeDbLevExp> Rows { get; } = new();

        internal Scope()
        {
            // Bind before physical Player birth, which consumes the real shared RNG.
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativePoolLevelInfo.LevelInfo = new()
            {
                [LegacyNativeDbLevExp.Sort.User] = Rows
            };
            Native = new NativeSpellTestFixture();
            Fill(Packet);
        }

        internal void Level(ushort value) =>
            typeof(LegacyNativePlayerRuntime).GetField("_level",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Player, value);

        internal LegacyNativeDbLevExp Row(byte level, ulong experience, int time)
        {
            var row = new LegacyNativeDbLevExp
            {
                Action = LegacyNativeDbLevExp.Sort.User,
                Level = level,
                Experience = experience,
                UpLevTime = time,
                MentorUpLevTime = 19
            };
            Rows[level] = row;
            return row;
        }

        private static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321;
            packet.Seek(237);
        }

        public void Dispose()
        {
            Native.Dispose();
            LegacyNativePoolLevelInfo.LevelInfo = priorLevels;
            LegacyNativePoolRandom.GetRandom = priorRandom;
            LegacyNativePoolRandom.ResetRandom = priorReset;
        }
    }

    private static byte[] VipStatusFrame(uint mode)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2284);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), mode);
        return bytes;
    }

    private static byte[] VipUpdateFrame(byte vip, int position = 4)
    {
        // Independent exact protobuf contract, not another invocation of producer.
        var query = new List<byte> { 0x08, 0x27 };
        if (vip != 0)
        {
            query.Add(0x10);
            if (vip >= 128)
            {
                query.Add((byte)(vip | 128));
                query.Add((byte)(vip >> 7));
            }
            else
            {
                query.Add(vip);
            }
        }
        var payload = new List<byte> { 0x08, 17, 0x12, (byte)query.Count };
        payload.AddRange(query);
        // SendUpdate does not initialize the writer: preserve the fixture's dirty
        // prefix and append at the actual incoming cursor, just like native code.
        var frame = Enumerable.Repeat((byte)0xA5, payload.Count + position).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2176);
        payload.CopyTo(frame, position);
        return frame;
    }
}
