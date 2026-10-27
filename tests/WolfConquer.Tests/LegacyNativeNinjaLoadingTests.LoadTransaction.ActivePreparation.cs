using System.Collections.Concurrent;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData(false, "storage")]
    [InlineData(false, "add")]
    [InlineData(false, "rank")]
    [InlineData(false, "badge")]
    [InlineData(true, "storage")]
    [InlineData(true, "add")]
    [InlineData(true, "rank")]
    [InlineData(true, "badge")]
    public Task RealNinjaPreparationComparersCannotPublishNestedMutations(bool appearance, string nested) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedSpellTransaction(scope);
            // AddItem, UpdateRank and badge-only operations need no spell owner.
            // Their active-Ninja guard must not rely on optional MySpells.
            if (nested != "storage") scope.Client.MySpells = null;
            Action reenter = () =>
            {
                switch (nested)
                {
                    case "storage": scope.Spells.Add(scope.Packet, 1000, 8); break;
                    case "add": scope.Ninja.AddItem(100, 9, 1, 4, 5); break;
                    case "rank": scope.Ninja.UpdateRank(); break;
                    case "badge": scope.Ninja.GetLevel(); break;
                }
            };
            var itemComparer = new ActiveItemComparer(reenter);
            ActiveFlagComparer? flagComparer = null;
            if (appearance)
            {
                var vector = scope.Client.Player.BitVector;
                flagComparer = new(vector, reenter);
                vector.ArrayFlags = new ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag>(flagComparer)
                {
                    [(int)Flag.WeeklyPKChampion] = new((int)Flag.WeeklyPKChampion, 10, false, 0),
                };
                vector.Add((int)Flag.WeeklyPKChampion);
            }
            else InstallPreparationItemMap(scope, itemComparer);
            var before = new LoadingTransactionWitness(scope);
            if (flagComparer is not null) flagComparer.Armed = true;
            else itemComparer.Armed = true;

            var error = Record.Exception(() =>
            {
                if (appearance) scope.Ninja.UpdateRank();
                else scope.Ninja.AddItem(100, 9, 1, 4, 5);
            });

            before.RequireUnchanged();
            Assert.True(flagComparer?.Triggered ?? itemComparer.Triggered);
            // Actual flag membership catches the nested guard's InvalidOp and
            // returns false; ContainFlag then reports InvalidData. Item lookup
            // propagates the direct guard refusal without that flag boundary.
            if (appearance) Assert.IsType<InvalidDataException>(error);
            else Assert.IsType<InvalidOperationException>(error);
        });

    private sealed class ActiveItemComparer(Action reenter) : IEqualityComparer<uint>
    {
        internal bool Armed;
        internal bool Triggered;
        public bool Equals(uint first, uint second) => first == second;
        public int GetHashCode(uint value)
        {
            if (Armed && value == 100) { Armed = false; Triggered = true; reenter(); }
            return value.GetHashCode();
        }
    }

    private sealed class ActiveFlagComparer(LegacyNativeStatusFlagsBigVector32 vector, Action reenter) : IEqualityComparer<int>
    {
        internal bool Armed;
        internal bool Triggered;
        public bool Equals(int first, int second) => first == second;
        public int GetHashCode(int value)
        {
            if (Armed && value == (int)Flag.WeeklyPKChampion)
            {
                Armed = false; Triggered = true;
                Assert.True(Monitor.IsEntered(vector.SyncRoot));
                reenter();
            }
            return value.GetHashCode();
        }
    }
}
