using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Associate = WolfConquer.Core.World.LegacyNativeAssociateRuntime;
using Info = WolfConquer.Core.World.LegacyNativeMsgApprenticeInformation;

namespace WolfConquer.Tests;

// Native complete mentor/Associate components, not host/login adoption or live-client proof.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMentorShareTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly List<LegacyNativeTutorInfo.TutorType> oldTutors = LegacyNativeTutorInfo.TutorTypes;
        private readonly int[] oldLimits = LegacyNativeTutorInfo.BattleLimit;
        private readonly ConcurrentDictionary<uint, Associate.MyAsociats> oldAssociates = Associate.Associates;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? oldClients = LegacyNativePoolClients.GamePoll;
        private readonly Dictionary<uint, LegacyNativeProfessionTable.Benefit> oldBenefits = LegacyNativeProfessionTable.Benefits;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture;
        private readonly List<LegacyNativePacket> packets = new();
        private readonly Dictionary<LegacyNativeGameClientRuntime, Queue<byte[]>> queues = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;

        internal Scope()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(53);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativeTutorInfo.TutorTypes = [Tutor()];
            LegacyNativeTutorInfo.BattleLimit = Enumerable.Repeat(80, 400).ToArray();
            Associate.Associates = new(); LegacyNativePoolClients.GamePoll = new(); LegacyNativeProfessionTable.Benefits = new();
            Native = new NativeSpellTestFixture();
            queues[Client] = Queue;
            Configure(Client, 17, 140, 160);
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int index = 0; index < 12; index++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE); Fill(packet); packets.Add(packet); bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin); Fill(Packet);
        }

        internal LegacyNativeGameClientRuntime Peer(uint uid, ushort level = 100, int equipmentBp = 100, bool online = true)
        {
            var queue = new Queue<byte[]>(); var client = NativeSpellTestFixture.CreateOwner(queue);
            queues[client] = queue; Configure(client, uid, level, equipmentBp);
            if (online) LegacyNativePoolClients.GamePoll![uid] = client;
            return client;
        }

        private static void Configure(LegacyNativeGameClientRuntime client, uint uid, ushort level, int equipmentBp)
        {
            client.Player.UID = uid; client.Player.Name = "P" + uid; client.Player.Spouse = "S" + uid; client.Player.Signature = "B" + uid;
            Set(client.Player, "_level", level); Set(client.Player, "_class", 1005u); Set(client.Player, "_pkpoints", (ushort)(uid + 1));
            client.Equipment = new LegacyNativeEquipItemStorage(client) { BattlePower = equipmentBp };
            client.Player.Nobility = new LegacyNativeNobility(uid, "N" + uid, 0, 0, 0);
            client.Player.Associate = new Associate.MyAsociats(uid) { MyClient = client };
            client.Player.ExtraBattlePower = 37;
        }

        internal Queue<byte[]> Sent(LegacyNativeGameClientRuntime client) => queues[client];
        internal void ClearQueues() { foreach (var queue in queues.Values) queue.Clear(); }
        internal void NullBin() => Bin.SetValue(null, null);

        public void Dispose()
        {
            Bin.SetValue(null, oldBin); foreach (var packet in packets) packet.Dispose(); Native.Dispose();
            LegacyNativePoolRandom.GetRandom = oldRandom; LegacyNativePoolRandom.ResetRandom = oldReset;
            LegacyNativeTutorInfo.TutorTypes = oldTutors; LegacyNativeTutorInfo.BattleLimit = oldLimits;
            Associate.Associates = oldAssociates; LegacyNativePoolClients.GamePoll = oldClients;
            LegacyNativeProfessionTable.Benefits = oldBenefits; CultureInfo.CurrentCulture = oldCulture;
        }
    }

    private sealed class Files : IDisposable
    {
        private readonly string prior = LegacyNativeServerConfig.DbLocation;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-mentor-" + Guid.NewGuid().ToString("N"));
        internal string Location => Root + Path.DirectorySeparatorChar;
        internal Files() { Directory.CreateDirectory(Root); LegacyNativeServerConfig.DbLocation = Location; }
        internal void Write(string name, string content) => File.WriteAllText(Path.Combine(Root, name), content, new UTF8Encoding(true));
        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = prior;
            string exact = Path.GetFullPath(Root);
            string prefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + "wolf-native-mentor-";
            if (!exact.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Owned mentor fixture escaped temporary directory.");
            if (Directory.Exists(exact)) Directory.Delete(exact, recursive: true);
        }
    }

    private static LegacyNativeTutorInfo.TutorType Tutor(int min = 1, int max = 140, int count = 5, int percent = 50, int index = 1) => new()
    { Index = index, MinLevel = min, MaxLevel = max, StudentNum = count, BattleLevShare = percent };

    private static void Set(LegacyNativePlayerRuntime player, string field, object value) => typeof(LegacyNativePlayerRuntime)
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(player, value);

    private static unsafe void Fill(LegacyNativePacket packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(), 0,
            (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
        packet.Size = 321; packet.Seek(237);
    }

    private sealed record ParsedInfo(uint Mode, uint Mentor, uint Apprentice, uint Mesh, uint Share,
        uint Constant, uint Date, uint Level, uint Class, ushort Pk, uint Online, uint Unknown,
        ulong Experience, ushort Blessing, ushort Composing, string[] Names);

    private static ParsedInfo ReadInfo(byte[] bytes)
    {
        Assert.Equal(bytes.Length, BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        Assert.Equal((ushort)2200, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)));
        Assert.All(bytes.AsSpan(42, 22).ToArray(), value => Assert.Equal((byte)0, value));
        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
        int cursor = 84; Assert.Equal((byte)4, bytes[cursor++]); var names = new string[4];
        for (int index = 0; index < 4; index++)
        {
            int length = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor)); cursor += 2;
            names[index] = Encoding.UTF8.GetString(bytes, cursor, length); cursor += length;
        }
        Assert.Equal(bytes.Length, cursor);
        return new ParsedInfo(U32(4), U32(8), U32(12), U32(16), U32(20), U32(24), U32(28), U32(32), U32(36),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(40)), U32(64), U32(68),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(72)), BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(80)),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(82)), names);
    }

    private static byte[] InfoWire(Info info)
    {
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0); writer.Write(info.PacketID); writer.Write((uint)info.Mode); writer.Write(info.Mentor_ID);
        writer.Write(info.Apprentice_ID); writer.Write(info.Mesh); writer.Write(info.Shared_Battle_Power); writer.Write(info._999999);
        writer.Write(info.Enrole_date); writer.Write(info.Level); writer.Write(info.Class); writer.Write(info.PkPoints);
        writer.Write(new byte[22]); writer.Write(info.Online); writer.Write(info.UnKnow2); writer.Write(info.Apprentice_Experience);
        writer.Write(info.Apprentice_Blessing); writer.Write(info.Apprentice_Composing); writer.Write((byte)4);
        foreach (string? text in new[] { info.MentorName, info.ApprenticeName, info.ApprenticeSpouse, info.PersonalSignature })
        {
            byte[] value = string.IsNullOrEmpty(text) ? [] : Encoding.UTF8.GetBytes(text);
            writer.Write((ushort)value.Length); writer.Write(value);
        }
        writer.Flush(); byte[] result = memory.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(result, (ushort)result.Length); return result;
    }

    private static void LinkMentor(LegacyNativeGameClientRuntime student, LegacyNativeGameClientRuntime mentor, ulong timer = 77)
    {
        student.Player.MyMentor = mentor.Player.Associate;
        student.Player.Associate!.Add(4, new Associate.Member { UID = mentor.Player.UID, Timer = timer });
    }

    private static void LinkApprentice(LegacyNativeGameClientRuntime mentor, LegacyNativeGameClientRuntime student, ulong timer = 88)
    {
        mentor.Player.Associate!.Add(5, new Associate.Member { UID = student.Player.UID, Timer = timer });
        mentor.Player.Associate.OnlineApprentice[student.Player.UID] = student;
    }
}
