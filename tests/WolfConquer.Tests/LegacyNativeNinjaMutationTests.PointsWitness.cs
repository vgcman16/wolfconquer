using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMutationTests
{
    private static readonly FieldInfo PointsStateField = typeof(LegacyNativeNinja)
        .GetField("persistenceState", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo PointsGateField = typeof(LegacyNativeNinja)
        .GetField("persistenceLoadGate", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo PointsBinField = typeof(LegacyNativePacketRecycle)
        .GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly FieldInfo PointsSealField = typeof(LegacyNativePacket)
        .GetField("seal", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void SeedPointCredit(Scope scope)
    {
        scope.Ninja.Exp = 71;
        scope.Ninja.Flag = 0x100000001UL;
        scope.Ninja.Fire = 11; scope.Ninja.Water = 12; scope.Ninja.Earth = 13;
        scope.Ninja.Wind = 14; scope.Ninja.Lightning = 15;
        scope.Ninja.tmp_Fire = 21; scope.Ninja.tmp_Water = 22; scope.Ninja.tmp_Earth = 23;
        scope.Ninja.tmp_Wind = 24; scope.Ninja.tmp_Lightning = 25;
        scope.Ninja.FireMastery = 31; scope.Ninja.WaterMastery = 32; scope.Ninja.EarthMastery = 33;
        scope.Ninja.WindMastery = 34; scope.Ninja.LightningMastery = 35;
        scope.Ninja.UnlockMastery = 7;
        var item = scope.Item(100, 100, 9);
        item.Position = 1; item.Exp = 73; item.UK = 91;
        scope.Ninja.AddPoints(0); // Real prior notice and serializer warm-up.
        Assert.Equal(Frame(2, 0, 0, 71), Assert.Single(scope.Queue));
    }

    private sealed class PointCreditImage
    {
        private readonly Scope scope;
        private readonly object state;
        private readonly Dictionary<string, object?> stateValues;
        private readonly LegacyNativeGameClientRuntime? user;
        private readonly LegacyNativeNinja? current;
        private readonly LegacyNativePlayerRuntime? player;
        private readonly ConcurrentDictionary<uint, LegacyNativeNinja.Item> map;
        private readonly KeyValuePair<uint, LegacyNativeNinja.Item>[] rows;
        private readonly uint[][] values;
        private readonly LegacyNativePacketMutationWitness[] packets;
        private readonly byte[][] frames, bytes;
        private readonly int available;

        internal PointCreditImage(Scope scope)
        {
            this.scope = scope;
            state = PointsStateField.GetValue(scope.Ninja)!;
            stateValues = StateValues(state);
            user = scope.Ninja.user; current = scope.Client.MyNinja; player = scope.Client.Player;
            map = scope.Ninja.Items;
            rows = map.ToArray(); values = rows.Select(pair => ItemValues(pair.Value)).ToArray();
            packets = Enumerable.Range(0, 4).Select(index => new LegacyNativePacketMutationWitness(scope.Used(index)))
                .Append(new(scope.Packet)).ToArray();
            frames = scope.Queue.ToArray(); bytes = frames.Select(frame => frame.ToArray()).ToArray();
            available = scope.Available;
        }

        internal void RequireUnchanged(bool requireAvailable = true)
        {
            Assert.Same(state, PointsStateField.GetValue(scope.Ninja));
            RequireNonPointState((uint)stateValues["Exp"]!);
            Assert.Equal(frames.Length, scope.Queue.Count);
            for (var index = 0; index < frames.Length; index++)
            {
                Assert.Same(frames[index], scope.Queue.ElementAt(index));
                Assert.Equal(bytes[index], scope.Queue.ElementAt(index));
            }
            if (requireAvailable) Assert.Equal(available, scope.Available);
            foreach (var packet in packets) packet.RequireUnchanged();
        }

        internal void RequireNonPointState(uint expectedExp)
        {
            Assert.Same(user, scope.Ninja.user); Assert.Same(current, scope.Client.MyNinja);
            Assert.Same(player, scope.Client.Player);
            Assert.Equal(expectedExp, scope.Ninja.Exp);
            var currentValues = StateValues(PointsStateField.GetValue(scope.Ninja)!);
            Assert.Equal(stateValues.Keys, currentValues.Keys);
            foreach (var pair in stateValues.Where(pair => pair.Key != "Exp"))
                Assert.Equal(pair.Value, currentValues[pair.Key]);
            Assert.Same(map, scope.Ninja.Items);
            var actual = map.ToArray();
            Assert.Equal(rows.Select(pair => pair.Key), actual.Select(pair => pair.Key));
            for (var index = 0; index < rows.Length; index++)
            {
                Assert.Same(rows[index].Value, actual[index].Value);
                Assert.Equal(values[index], ItemValues(actual[index].Value));
            }
        }

        private static Dictionary<string, object?> StateValues(object value) => value.GetType()
            .GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(property => property.Name, property => property.GetValue(value));
        private static uint[] ItemValues(LegacyNativeNinja.Item row) =>
            [row.ItemID, row.Level, row.Position, row.Exp, row.UK];
    }
}
