using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

// Actual native maps, view blocks, clients, file formats and catalog owner.
// No EnterMap/world population, client process or ordinary-Dictionary adapter.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMapLoadingTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeMapDictionary<uint, LegacyNativeGameMap>? oldMaps = LegacyNativePoolMaps.ServerMaps;
        private readonly Dictionary<int, string> oldContents = LegacyNativeGameMap.MapContents;
        private readonly object oldEnter = LegacyNativeGameMap.EnterObj;
        private readonly LegacyNativeCounter oldDynamic = LegacyNativeGameMap.DinamicIDS;
        private readonly LegacyNativeCounter oldMonsterUid = LegacyNativeMobCollection.GenerateUid;
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly string oldClient = LegacyNativeServerConfig.CO2Folder;
        private readonly string oldDb = LegacyNativeServerConfig.DbLocation;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal Queue<byte[]> Queue => Native.Queue;
        internal Random Control { get; private set; } = null!;
        internal string? OwnedDirectory { get; private set; }
        internal string ClientDirectory => Path.Combine(OwnedDirectory!, "client");
        internal string DbDirectory => Path.Combine(OwnedDirectory!, "db");

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativePoolMaps.ServerMaps = new LegacyNativeMapDictionary<uint, LegacyNativeGameMap>();
            LegacyNativeGameMap.MapContents = new();
            LegacyNativeGameMap.EnterObj = new object();
            LegacyNativeGameMap.DinamicIDS = new LegacyNativeCounter(10000001);
            LegacyNativeMobCollection.GenerateUid = new LegacyNativeCounter(400000);
            Native = new NativeSpellTestFixture();
            ResetRandom(71);
        }

        internal void ResetRandom(int seed)
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(seed);
            Control = new Random(seed);
        }

        internal void AssertTail() => Assert.Equal(Control.Next(), LegacyNativePoolRandom.GetRandom.Next());

        internal void OwnFiles(bool trailingDbSeparator = true)
        {
            OwnedDirectory = Path.Combine(Path.GetTempPath(), "WolfConquer-MapLoading-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ClientDirectory, "ini"));
            Directory.CreateDirectory(Path.Combine(DbDirectory, "maps"));
            LegacyNativeServerConfig.CO2Folder = ClientDirectory;
            LegacyNativeServerConfig.DbLocation = DbDirectory +
                (trailingDbSeparator ? Path.DirectorySeparatorChar.ToString() : "");
        }

        internal string WriteMap(string name, int width, int height,
            Func<int, int, (short Mask, short Floor, short Altitude)>? cell = null, byte[]? tail = null)
        {
            string path = Path.Combine(ClientDirectory, name);
            File.WriteAllBytes(path, Dmap(width, height, cell, tail));
            return name;
        }

        internal LegacyNativeGameMap Loaded(int id)
        {
            Assert.True(LegacyNativePoolMaps.ServerMaps!.TryGetValue(unchecked((uint)id), out var map));
            Assert.NotNull(map);
            return map;
        }

        public void Dispose()
        {
            Native.Dispose();
            LegacyNativePoolMaps.ServerMaps = oldMaps;
            LegacyNativeGameMap.MapContents = oldContents;
            LegacyNativeGameMap.EnterObj = oldEnter;
            LegacyNativeGameMap.DinamicIDS = oldDynamic;
            LegacyNativeMobCollection.GenerateUid = oldMonsterUid;
            LegacyNativePoolRandom.GetRandom = oldRandom;
            LegacyNativePoolRandom.ResetRandom = oldReset;
            LegacyNativeServerConfig.CO2Folder = oldClient;
            LegacyNativeServerConfig.DbLocation = oldDb;
            if (OwnedDirectory != null)
            {
                string exact = Path.GetFullPath(OwnedDirectory);
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                Assert.StartsWith(temp, exact, StringComparison.OrdinalIgnoreCase);
                Assert.StartsWith("WolfConquer-MapLoading-", Path.GetFileName(exact), StringComparison.Ordinal);
                if (Directory.Exists(exact)) Directory.Delete(exact, recursive: true);
            }
        }
    }

    private static LegacyNativeGameMap Map(int width = 32, int height = 32, int id = 700, Cell fill = Cell.Valid)
    {
        var map = new LegacyNativeGameMap(width, height, id)
        {
            View = new LegacyNativeMapView(width, height)
        };
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                map.cells[x, y] = fill;
        return map;
    }

    private static byte[] Dmap(int width, int height,
        Func<int, int, (short Mask, short Floor, short Altitude)>? cell = null, byte[]? tail = null)
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        writer.Write(Enumerable.Repeat((byte)0xA5, 268).ToArray());
        writer.Write(width);
        writer.Write(height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var values = cell?.Invoke(x, y) ?? ((short)0, (short)0, (short)0);
                writer.Write(values.Item1); writer.Write(values.Item2); writer.Write(values.Item3);
            }
            writer.Write(unchecked((int)0xDEADBEEF)); // Native row checksum is read and ignored.
        }
        if (tail != null) writer.Write(tail);
        return output.ToArray();
    }

    private static byte[] Index(params (int Key, byte[] Name, int Ignored)[] entries)
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        writer.Write(entries.Length);
        foreach (var entry in entries)
        {
            writer.Write(entry.Key); writer.Write(entry.Name.Length); writer.Write(entry.Name); writer.Write(entry.Ignored);
        }
        return output.ToArray();
    }

    private static byte[] Name(string value) => Encoding.ASCII.GetBytes(value);
}
