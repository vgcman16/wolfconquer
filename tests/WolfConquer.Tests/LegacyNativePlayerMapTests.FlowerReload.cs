using System.Collections.Concurrent;
using ProtoBuf;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Reuses the actual already-integrated full native map/spawn fixture, not a new projection.
public sealed partial class LegacyNativePlayerMapTests
{
    [Fact]
    public Task WholeOnlineFemaleFlowerRankChangeRebuildsAndBroadcastsTheActualNativePlayerSpawn() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var prior = LegacyNativePoolClients.GamePoll;
        try
        {
            LegacyNativePoolClients.GamePoll = new() { [scope.Player.UID] = scope.Client };
            scope.Player.Name = "Wolf"; scope.Player.Map = 1002; scope.Player.X = 10; scope.Player.Y = 10;
            var flowers = scope.Player.Flowers = new(scope.Player.UID, scope.Player.Name);
            flowers.RedRoses.Amount = 100;
            var observerQueue = new Queue<byte[]>();
            var observer = NativeSpellTestFixture.CreateOwner(observerQueue);
            observer.Player.UID = 99; observer.Player.Map = 1002; observer.Player.X = 11; observer.Player.Y = 10;
            var map = scope.Client.Map = new LegacyNativeGameMap(64, 64, 1002) { View = new LegacyNativeMapView(64, 64) };
            Assert.True(map.View.EnterMap(observer.Player));
            var rank = new LegacyNativeFlowersRuntime.FlowerRanking();

            rank.UpdateRank(flowers.RedRoses, flowers.RedRoses.Type);

            Assert.Equal(1, flowers.RedRoses.Rank);
            Assert.Equal(30000002u, scope.Player.FlowerRank);
            Assert.Equal(3, scope.Native.Queue.Count);
            Assert.All(scope.Native.Queue, bytes => Assert.Equal((ushort)2165, BitConverter.ToUInt16(bytes, 2)));
            var frame = Assert.Single(observerQueue);
            Assert.Equal((ushort)2227, BitConverter.ToUInt16(frame, 2));
            Assert.Equal(frame.Length, BitConverter.ToUInt16(frame, 0));
            using var payload = new MemoryStream(frame, 4, frame.Length - 4);
            var spawn = Serializer.Deserialize<LegacyNativePlayerRuntime.SpawnPacketProto>(payload);
            Assert.Equal(scope.Player.UID, spawn.UID);
            Assert.Equal(30010002u, spawn.FlowerIcon);
        }
        finally { LegacyNativePoolClients.GamePoll = prior; }
    });
}
