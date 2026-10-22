using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Complete leaf methods only: no ExecuteLogin worker, dispatch, server database,
// map publication, client host, OS socket or callback/provider is constructed.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeQueuedPersistenceTests
{
    private const string UserRows = "; owned sentinel\r\n[Character]\r\nName=Stored\r\n" +
        "GuildRank=6\r\nGuildID=444\r\nSpouseUID=88\r\nSpouse=StoredSpouse\r\n" +
        "Treasury=99\r\nUntouched=exact-value\r\n[Other]\r\nGuildRank=77\r\nSpouse=OtherSpouse\r\n";

    private sealed class Files : IDisposable
    {
        private readonly string priorLocation = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo priorCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo priorUiCulture = CultureInfo.CurrentUICulture;
        private readonly HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> directories = new(StringComparer.OrdinalIgnoreCase);
        internal string Root { get; } = Path.Combine(Path.GetTempPath(),
            "wolf-native-queue-leaves-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(Root, "Users");
        internal string Maps => Path.Combine(Root, "maps");

        internal Files(bool users = true)
        {
            CheckChain(Path.GetTempPath());
            Directory.CreateDirectory(Root);
            directories.Add(Root);
            if (users) CreateDirectory(Users);
            CreateDirectory(Maps);
            LegacyNativeServerConfig.DbLocation = Root;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        internal string User(uint uid) => Track(Path.Combine(Users,
            uid.ToString(CultureInfo.InvariantCulture) + ".ini"));
        internal string Map(uint id) => Track(Path.Combine(Maps,
            id.ToString(CultureInfo.InvariantCulture) + ".ini"));
        internal void Seed(string path, string text = UserRows) => File.WriteAllText(Track(path), text, Encoding.Unicode);
        internal LegacyNativeItemCounterShellIni Profile(string path) => new(Track(path), overridePath: true);

        internal void CreateDirectory(string path)
        {
            Guard(path);
            Directory.CreateDirectory(path);
            directories.Add(path);
        }

        private string Track(string path)
        {
            Guard(path);
            files.Add(path);
            return path;
        }

        private void Guard(string path)
        {
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
                throw new IOException("Not an owned persistence leaf.");
            CheckChain(Path.GetDirectoryName(full)!);
        }

        private static void CheckChain(string path)
        {
            for (string? cursor = Path.GetFullPath(path); cursor is not null; cursor = Path.GetDirectoryName(cursor))
                if ((Directory.Exists(cursor) || File.Exists(cursor)) &&
                    (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked persistence fixture path rejected.");
        }

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = priorLocation;
            CultureInfo.CurrentCulture = priorCulture;
            CultureInfo.CurrentUICulture = priorUiCulture;
            string full = Path.GetFullPath(Root);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full),
                    Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)) ||
                !Regex.IsMatch(Path.GetFileName(full), "^wolf-native-queue-leaves-[0-9a-f]{32}$"))
                throw new IOException("Owned persistence cleanup containment failed.");
            foreach (string directory in directories.Where(Directory.Exists))
            {
                CheckChain(directory);
                foreach (string entry in Directory.GetFileSystemEntries(directory))
                    if ((!files.Contains(entry) && !directories.Contains(entry)) ||
                        (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Unexpected persistence fixture entry; preserving files.");
            }
            foreach (string file in files)
                if (File.Exists(file)) File.Delete(file);
            foreach (string directory in directories.OrderByDescending(value => value.Length))
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: false);
        }
    }

    // Physical native recycle bin and socket SendQueue, each holding actual owned
    // packet/byte objects. No fake factory, send callback, pump or worker is used.
    private sealed class SpousePackets : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object priorBin = Bin.GetValue(null)!;
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Queue<byte[]> Queue => Native.Queue;

        internal unsafe SpousePackets()
        {
            Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0,
                (IntPtr)Native.Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Native.Packet.Seek(0);
            var owned = new ConcurrentQueue<LegacyNativePacket>();
            owned.Enqueue(Native.Packet);
            Bin.SetValue(null, owned);
        }

        public void Dispose()
        {
            Bin.SetValue(null, priorBin);
            Native.Dispose();
        }
    }

    private static readonly byte[] SpouseNoneFrame =
        Convert.FromHexString("1000F40711000000060104004E6F6E65");
}
