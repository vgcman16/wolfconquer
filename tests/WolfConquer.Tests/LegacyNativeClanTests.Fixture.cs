using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeClanTests
{
    private sealed class State : IDisposable
    {
        private readonly ConcurrentDictionary<uint, LegacyNativeClanRuntime> clans = LegacyNativeClanRuntime.Clans;
        private readonly ConcurrentDictionary<uint, string?> names = LegacyNativeClanRuntime.ChangeNameRecords;
        private readonly LegacyNativeCounter counter = LegacyNativeClanRuntime.CounterClansID;
        private readonly Dictionary<uint, List<uint>> ally = LegacyNativeClanTable.clanally;
        private readonly Dictionary<uint, List<uint>> enemy = LegacyNativeClanTable.clanenemy;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? clients = LegacyNativePoolClients.GamePoll;
        private readonly LegacyNativeSafeRandom random = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime reset = LegacyNativePoolRandom.ResetRandom;

        internal State()
        {
            LegacyNativeClanRuntime.Clans = new();
            LegacyNativeClanRuntime.ChangeNameRecords = new();
            LegacyNativeClanRuntime.CounterClansID = new(10);
            LegacyNativeClanTable.clanally = new();
            LegacyNativeClanTable.clanenemy = new();
            LegacyNativePoolClients.GamePoll = new();
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(83);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
        }

        public void Dispose()
        {
            LegacyNativeClanRuntime.Clans = clans;
            LegacyNativeClanRuntime.ChangeNameRecords = names;
            LegacyNativeClanRuntime.CounterClansID = counter;
            LegacyNativeClanTable.clanally = ally;
            LegacyNativeClanTable.clanenemy = enemy;
            LegacyNativePoolClients.GamePoll = clients;
            LegacyNativePoolRandom.GetRandom = random;
            LegacyNativePoolRandom.ResetRandom = reset;
        }
    }

    private sealed class Files : IDisposable
    {
        private readonly string previous = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        private readonly HashSet<string> ownedFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> ownedDirectories = new(StringComparer.OrdinalIgnoreCase);
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-clan-" + Guid.NewGuid().ToString("N"));
        internal string Clans => Path.Combine(Root, "Clans");
        internal string Users => Path.Combine(Root, "Users");

        internal Files(bool users = true)
        {
            CheckChain(Path.GetTempPath());
            Directory.CreateDirectory(Clans);
            ownedDirectories.Add(Root);
            ownedDirectories.Add(Clans);
            if (users)
            {
                Directory.CreateDirectory(Users);
                ownedDirectories.Add(Users);
            }
            LegacyNativeServerConfig.DbLocation = Root + Path.DirectorySeparatorChar;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        internal string SaveTarget(string file)
        {
            string path = Path.GetFullPath(Path.Combine(Clans, file));
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(path), Path.GetFullPath(Clans)))
                throw new IOException("Not an owned clan file.");
            ownedFiles.Add(path);
            return path;
        }
        internal void Clan(string file, params string[] records) =>
            File.WriteAllLines(SaveTarget(file), new[] { "Count=" + records.Length }.Concat(records));
        internal void User(string file, string fields)
        {
            string path = Path.GetFullPath(Path.Combine(Users, file));
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(path), Path.GetFullPath(Users)))
                throw new IOException("Not an owned member file.");
            ownedFiles.Add(path);
            File.WriteAllText(path, "[Character]\r\n" + fields, Encoding.Unicode);
        }

        private static void CheckChain(string path)
        {
            for (string? cursor = Path.GetFullPath(path); cursor != null; cursor = Path.GetDirectoryName(cursor))
                if ((Directory.Exists(cursor) || File.Exists(cursor)) &&
                    (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked fixture path rejected.");
        }

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = previous;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
            string full = Path.GetFullPath(Root);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full),
                    Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)) ||
                !Regex.IsMatch(Path.GetFileName(full), "^wolf-native-clan-[0-9a-f]{32}$"))
                throw new IOException("Owned clan cleanup containment failed.");
            CheckChain(full);
            foreach (string entry in Directory.EnumerateFileSystemEntries(full, "*", SearchOption.AllDirectories))
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0 ||
                    (!ownedFiles.Contains(entry) && !ownedDirectories.Contains(entry)))
                    throw new IOException("Unexpected or linked fixture child; preserving files.");
            foreach (string file in ownedFiles)
                if (File.Exists(file)) File.Delete(file);
            foreach (string directory in ownedDirectories.OrderByDescending(value => value.Length))
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: false);
        }
    }

    private sealed class Packets : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object previous = Bin.GetValue(null)!;
        private readonly LegacyNativeSafeRandom random = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime reset = LegacyNativePoolRandom.ResetRandom;
        private readonly List<LegacyNativePacket> buffers = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativePacket Stream => Native.Packet;

        internal unsafe Packets(int count = 3)
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(83);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            Native = new NativeSpellTestFixture();
            Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)Native.Packet.Memory, LegacyNativePacket.MAX_SIZE);
            var owned = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < count; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
                buffers.Add(packet);
                owned.Enqueue(packet);
            }
            Bin.SetValue(null, owned);
        }

        internal void FailNextTake() => Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>(new[] { (LegacyNativePacket)null! }));
        public void Dispose()
        {
            Bin.SetValue(null, previous);
            foreach (var packet in buffers) packet.Dispose();
            Native.Dispose();
            LegacyNativePoolRandom.GetRandom = random;
            LegacyNativePoolRandom.ResetRandom = reset;
        }
    }

    private static LegacyNativeClanRuntime Clan(uint id, string name) => new() { ID = id, Name = name };
    private static uint U32(byte[] bytes, int offset) => BitConverter.ToUInt32(bytes, offset);
}
