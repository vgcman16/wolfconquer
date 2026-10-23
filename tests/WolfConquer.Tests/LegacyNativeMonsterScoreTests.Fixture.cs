using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMonsterScoreTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeSafeRandom random = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime reset = LegacyNativePoolRandom.ResetRandom;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativeGameMap Map { get; }
        internal LegacyNativeMonsterRole Monster { get; }

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Native = new NativeSpellTestFixture();
            Map = new LegacyNativeGameMap(64, 64, 1002) { View = new LegacyNativeMapView(64, 64) };
            Client.Player.Name = "Wolf"; Client.Player.ServerID = 2;
            Client.Player.Map = 1002; Client.Player.X = 10; Client.Player.Y = 10;
            Map.Enquer(Client);
            var family = new LegacyNativeMonsterFamily
            {
                ID = 3130, Name = "FixtureBoss", Mesh = 100, Level = 100, MaxHealth = 1000, Boss = 1
            };
            Monster = new LegacyNativeMonsterRole(family, 400001, "owned-score", Map)
            {
                Map = 1002, X = 11, Y = 10
            };
            Assert.True(Map.View!.EnterMap((ILegacyNativeMapObj)Monster));
        }

        internal (LegacyNativeGameClientRuntime Client, Queue<byte[]> Queue) Peer(uint uid, string name, ushort server = 2, ushort x = 12)
        {
            var queue = new Queue<byte[]>();
            var client = NativeSpellTestFixture.CreateOwner(queue);
            client.Player.UID = uid; client.Player.Name = name; client.Player.ServerID = server;
            client.Player.Map = Map.ID; client.Player.X = x; client.Player.Y = 10;
            Map.Enquer(client);
            return (client, queue);
        }

        public void Dispose()
        {
            Native.Dispose();
            LegacyNativePoolRandom.GetRandom = random;
            LegacyNativePoolRandom.ResetRandom = reset;
            CultureInfo.CurrentCulture = culture;
        }
    }

    private sealed class Logs : IDisposable
    {
        private static readonly FieldInfo Startup = typeof(LegacyNativeApplication).GetField("startupPath", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly FieldInfo Counter = typeof(LegacyNativeConsole).GetField("ExceptionsCounter", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previous;
        private readonly LegacyNativeCounter counter;
        private readonly uint count;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-monster-score-" + Guid.NewGuid().ToString("N"));

        internal Logs()
        {
            previous = Startup.GetValue(null);
            counter = (LegacyNativeCounter)Counter.GetValue(null)!;
            count = counter.Count;
            CheckChain(Path.GetTempPath());
            Directory.CreateDirectory(Root);
            Startup.SetValue(null, Root); // Owned cache MODEL; actual logging methods run unchanged.
        }

        internal void AssertTwoRealLogs()
        {
            Assert.Equal(unchecked(count + 2), counter.Count);
            string[] logs = Directory.GetFiles(Root, "*.txt", SearchOption.AllDirectories);
            Assert.Equal(2, logs.Length);
            foreach (string path in logs)
            {
                string body = File.ReadAllText(path);
                Assert.Contains("----Exception message----", body);
                Assert.Contains("----Stack trace----", body);
                Assert.Contains("UpdateScore", body);
            }
        }

        private static void CheckChain(string path)
        {
            for (string? cursor = Path.GetFullPath(path); cursor != null; cursor = Path.GetDirectoryName(cursor))
                if ((Directory.Exists(cursor) || File.Exists(cursor)) &&
                    (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked score fixture path rejected.");
        }

        public void Dispose()
        {
            Startup.SetValue(null, previous);
            counter.Set(count);
            string full = Path.GetFullPath(Root);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full),
                    Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)) ||
                !Regex.IsMatch(Path.GetFileName(full), "^wolf-native-monster-score-[0-9a-f]{32}$"))
                throw new IOException("Owned score-log containment failed.");
            CheckChain(full);
            var directories = new List<string>();
            var files = new List<string>();
            var pending = new Stack<string>();
            pending.Push(full);
            while (pending.Count != 0)
            {
                string directory = pending.Pop();
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Linked score-log child; preserving files.");
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        directories.Add(entry); pending.Push(entry);
                    }
                    else
                    {
                        if (!entry.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                            !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(Path.GetRelativePath(full, entry).Split(Path.DirectorySeparatorChar)[0]), "Exceptions"))
                            throw new IOException("Unexpected score-log child; preserving files.");
                        files.Add(entry);
                    }
                }
            }
            foreach (string file in files) File.Delete(file);
            foreach (string directory in directories.OrderByDescending(value => value.Length)) Directory.Delete(directory, recursive: false);
            Directory.Delete(full, recursive: false);
        }
    }

    private static LegacyNativeMsgBossHarmRankingEntry Hunter(uint uid, uint score, string name = "Hunter", uint rank = 0, uint server = 2) =>
        new() { HunterUID = uid, HunterScore = score, HunterName = name, Rank = rank, ServerID = server };

    private static void V(List<byte> bytes, ulong value)
    {
        do { byte next = (byte)(value & 127); value >>= 7; bytes.Add((byte)(next | (value == 0 ? 0 : 128))); } while (value != 0);
    }
    private static void F(List<byte> bytes, uint field, ulong value) { V(bytes, (ulong)field << 3); V(bytes, value); }
    private static byte[] Ranking(uint type, uint monsterId, params LegacyNativeMsgBossHarmRankingEntry?[] hunters)
    {
        var body = new List<byte>(); F(body, 1, type); F(body, 2, monsterId);
        foreach (var hunter in hunters)
        {
            if (hunter == null) continue;
            var row = new List<byte>(); F(row, 1, hunter.Rank); F(row, 2, hunter.ServerID); F(row, 3, hunter.HunterUID);
            if (hunter.HunterName != null)
            {
                byte[] name = Encoding.UTF8.GetBytes(hunter.HunterName); V(row, 34); V(row, (ulong)name.Length); row.AddRange(name);
            }
            F(row, 5, hunter.HunterScore);
            V(body, 26); V(body, (ulong)row.Count); body.AddRange(row);
        }
        var frame = new List<byte>(); frame.AddRange(BitConverter.GetBytes((ushort)(body.Count + 4)));
        frame.AddRange(BitConverter.GetBytes((ushort)2001)); frame.AddRange(body); return frame.ToArray();
    }

    private static LegacyNativeMsgBossHarmRanking Read(byte[] bytes)
    {
        Assert.Equal((ushort)2001, BitConverter.ToUInt16(bytes, 2));
        using var packet = new LegacyNativePacket(bytes);
        return packet.ProtoBufferDeserialize(new LegacyNativeMsgBossHarmRanking());
    }
}
