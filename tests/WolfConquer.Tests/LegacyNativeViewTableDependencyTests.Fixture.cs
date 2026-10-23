using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.Data.LegacyNativeRoulettes.RouletteTable;
using Color = WolfConquer.Core.World.LegacyNativeMsgRouletteOpenGui.Color;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeViewTableDependencyTests
{
    // Owned component fixture, not full native client birth, socket creation or dispatch.
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly string oldDb = LegacyNativeServerConfig.DbLocation;
        private readonly Dictionary<uint, LegacyNativePokerTableRuntime>? oldPoker = LegacyNativePokerDatabase.Tables;
        private readonly Dictionary<uint, Table> oldRoulette = LegacyNativeRoulettes.RoulettesPoll;
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly List<LegacyNativePacket> owned = new();
        internal readonly string Temp = Path.Combine(Path.GetTempPath(), "wolf-native-view-tables-" + Guid.NewGuid().ToString("N"));
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;

        internal unsafe Scope()
        {
            Directory.CreateDirectory(Temp);
            LegacyNativeServerConfig.DbLocation = Temp + Path.DirectorySeparatorChar;
            LegacyNativePokerDatabase.Tables = null;
            LegacyNativeRoulettes.RoulettesPoll = new();
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(173);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            Native = new();
            Client.Player.Name = "Wolf";
            Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            // The native Reuse does not enqueue: own enough finite packets for every control.
            for (int index = 0; index < 48; index++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
                owned.Add(packet);
                bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
        }

        internal LegacyNativeGameClientRuntime Peer(uint uid, Queue<byte[]> queue)
        {
            var result = NativeSpellTestFixture.CreateOwner(queue);
            result.Player.UID = uid;
            result.Player.Name = "Peer" + uid;
            return result;
        }

        internal Table Table(uint uid = 700)
        {
            return new Table { SpawnPacket = new() { UID = uid, TableNumber = 9, MoneyType = LegacyNativeMsgRouletteTable.TableType.Money, X = 10, Y = 11, Mesh = 12 } };
        }

        internal Table.Member Member(LegacyNativeGameClientRuntime? owner = null, Color color = Color.Blue)
        {
            return new Table.Member { Owner = owner ?? Client, Color = color };
        }

        internal void File(string filename, params string[] rows) => System.IO.File.WriteAllLines(Path.Combine(Temp, filename), rows);

        public void Dispose()
        {
            Bin.SetValue(null, oldBin);
            foreach (var packet in owned) packet.Dispose();
            Native.Dispose();
            LegacyNativeServerConfig.DbLocation = oldDb;
            LegacyNativePokerDatabase.Tables = oldPoker;
            LegacyNativeRoulettes.RoulettesPoll = oldRoulette;
            LegacyNativePoolRandom.GetRandom = oldRandom;
            LegacyNativePoolRandom.ResetRandom = oldReset;
            string resolved = Path.GetFullPath(Temp);
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(resolved).StartsWith("wolf-native-view-tables-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the exact owned table fixture.");
            Directory.Delete(resolved, recursive: true);
        }
    }

    private static ushort Op(byte[] packet) => BitConverter.ToUInt16(packet, 2);
    private static byte[] Bytes(LegacyNativePacket packet) => NativeSpellTestFixture.Bytes(packet);
    private static byte[] Frame(ushort op, Action<BinaryWriter> body)
    {
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory, Encoding.ASCII, leaveOpen: true);
        writer.Write((ushort)0); writer.Write(op); body(writer);
        writer.Flush();
        var bytes = memory.ToArray();
        BitConverter.GetBytes((ushort)bytes.Length).CopyTo(bytes, 0);
        return bytes;
    }
    private static void Name(BinaryWriter writer, string value)
    {
        byte[] field = new byte[32];
        Encoding.ASCII.GetBytes(value.AsSpan(0, Math.Min(value.Length, 32)), field);
        writer.Write(field);
    }
}
