using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;
using Status = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem.QuestStatus;

namespace WolfConquer.Tests;

// Real component owners only. No whole Inventory.Update/ExecuteLogin acceptance.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime previousReset = LegacyNativePoolRandom.ResetRandom;
        private readonly List<LegacyNativeRuneTable.RandomAttribute> previousAttributes = LegacyNativeRuneTable.RandomAttributes;
        private readonly List<uint> previousKingdom = LegacyNativeQuestInfo.KingDomMissions;
        private readonly string previousDb = LegacyNativeServerConfig.DbLocation;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativeQuestsRuntime Quests { get; }
        internal Random Control { get; private set; } = null!;
        internal string? OwnedDirectory { get; private set; }

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativeRuneTable.RandomAttributes = new();
            LegacyNativeQuestInfo.KingDomMissions = [35024, 35007, 35025, 35028, 35034];
            Native = new NativeSpellTestFixture();
            Assert.Null(Player.QuestGUI);
            Quests = new LegacyNativeQuestsRuntime(Player);
            Player.QuestGUI = Quests; // Explicit actual-owner fixture, NOT client birth.
            ResetRandom(71);
            Fill(Packet);
        }

        internal void ResetRandom(int seed)
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(seed);
            Control = new Random(seed);
        }

        internal Row Row(uint key, uint uid, Status status, params uint[] intentions)
        {
            var row = new Row { UID = uid, Status = status, Time = 123, Intentions = intentions };
            Quests.src[key] = row;
            return row;
        }

        internal void OwnFiles(bool createQuestDirectory = true)
        {
            OwnedDirectory = Path.Combine(Path.GetTempPath(), "WolfConquer-InventoryMutation-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(OwnedDirectory);
            LegacyNativeServerConfig.DbLocation = OwnedDirectory;
            if (createQuestDirectory)
                Directory.CreateDirectory(Path.Combine(OwnedDirectory, "Quests"));
        }

        internal string QuestPath => Path.Combine(OwnedDirectory!, "Quests", Player.UID + ".bin");

        internal void AssertTail() =>
            Assert.Equal(Control.Next(), LegacyNativePoolRandom.GetRandom.Next());

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
            LegacyNativeServerConfig.DbLocation = previousDb;
            LegacyNativeQuestInfo.KingDomMissions = previousKingdom;
            LegacyNativeRuneTable.RandomAttributes = previousAttributes;
            LegacyNativePoolRandom.GetRandom = previousRandom;
            LegacyNativePoolRandom.ResetRandom = previousReset;
            if (OwnedDirectory != null)
            {
                string exact = Path.GetFullPath(OwnedDirectory);
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                Assert.StartsWith(temp, exact, StringComparison.OrdinalIgnoreCase);
                Assert.StartsWith("WolfConquer-InventoryMutation-", Path.GetFileName(exact), StringComparison.Ordinal);
                if (Directory.Exists(exact))
                    Directory.Delete(exact, recursive: true);
            }
        }
    }

    private static LegacyNativeRuneTable.RandomAttribute Attribute(uint item, LegacyNativeChiAttribute attribute) =>
        new() { ItemID = item, Attribute = attribute, Min = 1, Max = 100, dwParam = true };

    private static byte[] DataFrame(uint uid, params uint[] intentions)
    {
        var body = new List<byte>();
        void U16(ushort value) { body.Add((byte)value); body.Add((byte)(value >> 8)); }
        void U32(uint value)
        {
            body.Add((byte)value); body.Add((byte)(value >> 8));
            body.Add((byte)(value >> 16)); body.Add((byte)(value >> 24));
        }
        U16(0); U16((ushort)intentions.Length); U32(uid);
        foreach (uint intention in intentions) U32(intention);
        if (uid == 35024) foreach (uint intention in intentions) U32(intention);
        U32(0);
        if (uid == 35007)
        {
            body.AddRange(new byte[12]);
            foreach (uint intention in intentions) U32(intention);
        }
        var bytes = new byte[body.Count + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)bytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2136);
        body.CopyTo(bytes, 4);
        return bytes;
    }

    private static byte[] QuestFile(params (uint Id, uint Status, uint Time, uint[] Intentions)[] rows)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(rows.Length);
        foreach (var row in rows)
        {
            writer.Write(row.Id); writer.Write(row.Status); writer.Write(row.Time);
            writer.Write(row.Intentions.Length);
            foreach (uint intention in row.Intentions) writer.Write(intention);
        }
        return stream.ToArray();
    }

    private static void LoadOwned(Scope scope, byte[] bytes)
    {
        File.WriteAllBytes(scope.QuestPath, bytes);
        var file = new LegacyNativeBinaryFile();
        Assert.True(file.Open(scope.QuestPath, FileMode.Open));
        try { LegacyNativeRoleQuests.LoadOpenedFile(file, scope.Client); }
        finally { file.Close(); } // Explicit fixture ownership; native Load has no Close.
    }
}
