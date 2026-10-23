using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativePerfectionEffectTests
{
    private static readonly string[] Fields =
    [
        "ToxinEraser", "LightOfStamina", "DivineGuard", "CalmWind", "LuckyStrike", "StrikeLock",
        "FreeSoul", "StraightLife", "DrainingTouch", "BloodSpawn", "CoreStrike", "KillingFlash",
        "MirrorOfSin", "InvisibleArrow", "ShieldBreak", "AbsoluteLuck",
    ];

    // Independent literal native threshold ledger: first eligible integer, resulting value.
    private static readonly (int Level, double Value)[][] Bands =
    [
        [(1,30),(45,35),(85,40),(110,45),(145,50),(185,60),(200,70),(230,80),(260,90),(300,100),(601,200)],
        [(1,5),(601,8)],
        [(1,5),(601,8)],
        [(3,1),(90,2),(170,3),(250,4),(285,5),(601,8)],
        [(15,1),(115,2),(215,3),(314,4),(320,5),(601,7)],
        [(20,1),(105,2),(175,3),(308,4),(321,5),(601,7)],
        [(25,1),(95,2),(130,3),(195,4),(279,5),(601,11)],
        [(30,1),(125,2),(210,3),(265,4),(303,5),(601,9)],
        [(35,1),(220,2),(276,3),(601,10)],
        [(40,1),(240,2),(288,3),(601,5)],
        [(50,1),(80,2),(135,3),(205,4),(245,5),(601,8)],
        [(55,1),(165,2),(294,3),(601,10)],
        [(60,1),(190,2),(291,3),(601,5)],
        [(65,1),(225,2),(282,3),(601,8)],
        [(155,1),(255,2),(297,3),(312,4),(322,5),(601,8)],
        [(325,5),(601,7)],
    ];

    private static readonly (string Name, int Field, byte Level)[] Runes =
    [
        ("Sanctity",2,1), ("FireBlast",10,2), ("Serenity",13,3),
        ("DrainingTouchBooster",8,4), ("BloodSpawnBooster",9,5),
        ("FreeSoulBooster",6,6), ("EnormousCrusher",14,7),
    ];

    private static double[] Values(LegacyNativePerfectionEffect state) => Fields.Select(name =>
        (double)typeof(LegacyNativePerfectionEffect).GetField(name)!.GetValue(state)!).ToArray();

    private static double[] Expected(int level) => Bands.Select(bands =>
        bands.Where(band => level >= band.Level).Select(band => band.Value).LastOrDefault()).ToArray();

    private static void Poison(LegacyNativePerfectionEffect state)
    {
        foreach (string name in Fields)
            typeof(LegacyNativePerfectionEffect).GetField(name)!.SetValue(state, double.NaN);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly LegacyNativeItemTypeRuneMetadata? prior = LegacyNativePoolItems.ItemsBase;
        private readonly LegacyNativeSafeRandom priorRandom = LegacyNativePoolRandom.GetRandom;
        internal LegacyNativeGameClientRuntime Client { get; }
        internal LegacyNativeRuneItemStorage Storage { get; }

        internal Fixture(bool eligible = false)
        {
            LegacyNativePoolItems.ItemsBase = new();
            // Player birth consumes the shared native seed before its constructor body.
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            Client = new();
            Client.Equipment = new(Client);
            Storage = new(Client);
            Client.Rune = Storage;
            if (eligible)
            {
                Client.Player.Reborn = 2;
                typeof(LegacyNativePlayerRuntime).GetField("_level", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Client.Player, (ushort)15);
            }
        }

        internal LegacyNativeGameItemRuntime AddRune(int index)
        {
            var rune = Runes[index];
            uint id = 4_031_100u + rune.Level;
            LegacyNativePoolItems.ItemsBase![id] = new() { ID = id, Name = rune.Name };
            var item = new LegacyNativeGameItemRuntime { Uid = (uint)index + 1, ITEM_ID = id, Position = (ushort)(103 + index) };
            Assert.True(Storage.Add(item));
            return item;
        }

        public void Dispose()
        {
            LegacyNativePoolItems.ItemsBase = prior;
            LegacyNativePoolRandom.GetRandom = priorRandom;
        }
    }
}
