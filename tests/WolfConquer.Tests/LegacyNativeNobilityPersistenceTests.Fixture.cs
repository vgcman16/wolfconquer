using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Nobility = WolfConquer.Core.World.LegacyNativeNobility;
using Rank = WolfConquer.Core.World.LegacyNativeNobilityRank;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeNobilityPersistenceTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly Nobility.NobilityRanking oldRanking = LegacyNativePoolNobility.NobilityRanking;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? oldClients = LegacyNativePoolClients.GamePoll;
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldRandomReset = LegacyNativePoolRandom.ResetRandom;
        private readonly string oldLocation = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture, oldUi = CultureInfo.CurrentUICulture;
        private readonly Dictionary<FieldInfo, object?> oldParsers = typeof(LegacyNativeItemCounterShellIni)
            .GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => typeof(Delegate).IsAssignableFrom(field.FieldType))
            .ToDictionary(field => field, field => field.GetValue(null));
        private readonly ConcurrentQueue<LegacyNativePacket> ownedBin = new();
        private readonly List<LegacyNativePacket> packets = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal Nobility.NobilityRanking Ranking { get; } = new();
        internal int Available => LegacyNativePacketRecycle.Count;
        internal OwnedUsers Files { get; }

        internal Scope(bool createUsers = true)
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Native = new NativeSpellTestFixture();
            Files = new OwnedUsers(createUsers);
            LegacyNativeServerConfig.DbLocation = Files.Root;
            LegacyNativePoolNobility.NobilityRanking = Ranking;
            LegacyNativePoolClients.GamePoll = new();
            for (int index = 0; index < 12; index++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Fill(packet);
                packets.Add(packet);
                ownedBin.Enqueue(packet);
            }
            Bin.SetValue(null, ownedBin);
        }

        internal Nobility OwnNobility(ulong donation = 400000001, int position = 20)
        {
            var row = new Nobility(Player.UID, "owned", donation, 1002, 1) { Position = position };
            Player.Nobility = row;
            return row;
        }

        internal void UseNullBin() => Bin.SetValue(null, null);

        internal void UnserializedDisposed(int index)
        {
            Assert.Equal(321, packets[index].Size);
            Assert.Equal(0, packets[index].Position);
        }

        private static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(), 0,
                (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321;
            packet.Seek(237);
        }

        public void Dispose()
        {
            LegacyNativePoolNobility.NobilityRanking = oldRanking;
            LegacyNativePoolClients.GamePoll = oldClients;
            LegacyNativeServerConfig.DbLocation = oldLocation;
            Bin.SetValue(null, oldBin);
            foreach (var packet in packets) packet.Dispose();
            Native.Dispose(); // Restores the exact genuine prior packet seal.
            foreach (var pair in oldParsers) pair.Key.SetValue(null, pair.Value);
            LegacyNativePoolRandom.GetRandom = oldRandom;
            LegacyNativePoolRandom.ResetRandom = oldRandomReset;
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUi;
            Files.Dispose();
        }
    }

    private static string Ini(uint uid, string name, ulong donation, string body = "1002", string face = "0") =>
        "[Character]\r\nUID=" + uid + "\r\nName=" + name + "\r\nDonationNobility=" + donation + "\r\nBody=" + body + "\r\nFace=" + face + "\r\n";

    private static void PeriodBetween(LegacyNativeDaysNobility days, DateTime before, DateTime after, int count) =>
        Assert.InRange(days.PeriodTime, before.AddDays(count), after.AddDays(count));
}
