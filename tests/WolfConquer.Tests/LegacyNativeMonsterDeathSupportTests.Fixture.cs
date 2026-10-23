using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMonsterDeathSupportTests
{
    private sealed class Files : IDisposable
    {
        private readonly List<LegacyNativeSpiritTable.Spirit> rates = LegacyNativeSpiritTable.SpiritRates;
        private readonly string db = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        private bool ownedFile;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-spirit-" + Guid.NewGuid().ToString("N"));
        internal string Pathname => Path.Combine(Root, "spirit_rate.txt");
        internal Files()
        {
            CheckChain(Path.GetTempPath()); Directory.CreateDirectory(Root);
            LegacyNativeSpiritTable.SpiritRates = new(); LegacyNativeServerConfig.DbLocation = Root + Path.DirectorySeparatorChar;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }
        internal void Write(params string[] lines)
        {
            string full = Path.GetFullPath(Pathname);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), Path.GetFullPath(Root))) throw new IOException("Spirit fixture escaped.");
            ownedFile = true; File.WriteAllLines(full, lines);
        }
        private static void CheckChain(string path)
        {
            for (string? cursor = Path.GetFullPath(path); cursor != null; cursor = Path.GetDirectoryName(cursor))
                if ((Directory.Exists(cursor) || File.Exists(cursor)) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked spirit fixture path rejected.");
        }
        public void Dispose()
        {
            LegacyNativeSpiritTable.SpiritRates = rates; LegacyNativeServerConfig.DbLocation = db;
            CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture;
            string full = Path.GetFullPath(Root);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)) ||
                !Regex.IsMatch(Path.GetFileName(full), "^wolf-native-spirit-[0-9a-f]{32}$")) throw new IOException("Spirit cleanup containment failed.");
            CheckChain(full);
            foreach (string entry in Directory.EnumerateFileSystemEntries(full))
                if (!ownedFile || !StringComparer.OrdinalIgnoreCase.Equals(entry, Path.GetFullPath(Pathname)) ||
                    (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new IOException("Unexpected spirit fixture child preserved.");
            if (ownedFile && File.Exists(Pathname)) File.Delete(Pathname);
            Directory.Delete(full, recursive: false);
        }
    }
    private sealed class Packets : IDisposable
    {
        private readonly LegacyNativeSafeRandom random = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime reset = LegacyNativePoolRandom.ResetRandom;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePacket Stream => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal unsafe Packets()
        {
            LegacyNativePoolRandom.GetRandom = new(321); LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            Native = new(); Prepare(Client, 17, 64, 64);
            Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)Stream.Memory, LegacyNativePacket.MAX_SIZE);
        }
        internal static void Prepare(LegacyNativeGameClientRuntime client, uint uid, ushort x, ushort y)
        {
            client.Player.UID = uid; client.Player.Name = "Wolf" + uid; client.Player.Map = 1002;
            client.Player.DynamicID = 7; client.Player.X = x; client.Player.Y = y; client.Player.IsCheckedPass = true;
        }
        internal LegacyNativeGameClientRuntime Peer(uint uid, ushort x, ushort y, Queue<byte[]> queue)
        {
            var peer = NativeSpellTestFixture.CreateOwner(queue); Prepare(peer, uid, x, y); return peer;
        }
        public void Dispose()
        {
            Native.Dispose(); LegacyNativePoolRandom.GetRandom = random; LegacyNativePoolRandom.ResetRandom = reset;
        }
    }
    private static ushort Opcode(byte[] bytes) => BitConverter.ToUInt16(bytes, 2);
}
