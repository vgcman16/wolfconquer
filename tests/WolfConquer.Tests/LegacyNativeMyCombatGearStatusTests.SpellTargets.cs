using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMyCombatGearStatusTests
{
    [Theory]
    [InlineData("status")]
    [InlineData("null-status")]
    [InlineData("perfection")]
    [InlineData("null-perfection")]
    [InlineData("scalar")]
    public Task CommittedRankSpellStopsStaleStatusWorkAfterARealComparerCallout(string change) =>
        NativePacketTestThread.Run(() =>
        {
            using var test = new SelectedStatusCase();
            var fixture = test.Fixture;
            test.Gear.SetValue("HolyProtection", 25.9);
            Assert.True(fixture.Owner.Player.BitVector.TryAdd(376, 60, false, 0));
            var status = fixture.Owner.Status;
            var perfection = fixture.Owner.PerfectionStatus;
            var replacementStatus = new LegacyNativeMsgStatus { MaxAttack = 901, ItemBless = 47 };
            var replacementPerfection = new LegacyNativePerfectionEffect { CoreStrike = 321 };
            var expectedStatus = PublicFields(status);
            var expectedPerfection = PublicFields(perfection);
            if (change == "scalar")
            {
                var names = typeof(LegacyNativeMsgStatus).GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .OrderBy(field => field.Name, StringComparer.Ordinal).Select(field => field.Name).ToArray();
                expectedStatus[Array.IndexOf(names, nameof(LegacyNativeMsgStatus.MaxAttack))] = status.MaxAttack + 1;
            }
            var replacementStatusImage = PublicFields(replacementStatus);
            var replacementPerfectionImage = PublicFields(replacementPerfection);
            var comparer = new RankSpellCallbackComparer();
            fixture.Storage.ClientSpells = new ConcurrentDictionary<ushort, LegacyNativeMsgSpell>(
                fixture.Storage.ClientSpells.ToArray(), comparer);
            comparer.Callback = () =>
            {
                switch (change)
                {
                    case "status": fixture.Owner.Status = replacementStatus; break;
                    case "null-status": fixture.Owner.Status = null!; break;
                    case "perfection": fixture.Owner.PerfectionStatus = replacementPerfection; break;
                    case "null-perfection": fixture.Owner.PerfectionStatus = null!; break;
                    case "scalar": status.MaxAttack++; break;
                    default: throw new InvalidOperationException("Unknown owned target change.");
                }
                // Expectations were captured before execution; never accept any
                // earlier caller stores by copying them into the expected image.
            };
            comparer.Armed = true;

            var error = Record.Exception(test.Gear.Status);

            Assert.True(comparer.Fired);
            Assert.Null(error); // A committed spell is not reported as rejected/rolled back.
            Assert.Equal(expectedStatus, PublicFields(status));
            Assert.Equal(expectedPerfection, PublicFields(perfection));
            Assert.Equal(replacementStatusImage, PublicFields(replacementStatus));
            Assert.Equal(replacementPerfectionImage, PublicFields(replacementPerfection));
            Assert.Equal(50U, status.ItemBless); // Later selected-flag work also stopped.
            if (change == "status") Assert.Same(replacementStatus, fixture.Owner.Status);
            else if (change == "null-status") Assert.Null(fixture.Owner.Status);
            else Assert.Same(status, fixture.Owner.Status);
            if (change == "perfection") Assert.Same(replacementPerfection, fixture.Owner.PerfectionStatus);
            else if (change == "null-perfection") Assert.Null(fixture.Owner.PerfectionStatus);
            else Assert.Same(perfection, fixture.Owner.PerfectionStatus);
            Assert.False(fixture.Storage.ClientSpells.ContainsKey(21930));
            Assert.Equal(2, fixture.Queue.Count);
            Assert.Equal(Convert.FromHexString("1400FE07081118AAAB014800606D680070007800"),
                fixture.Queue.Last());
        });

    private sealed class RankSpellCallbackComparer : IEqualityComparer<ushort>
    {
        internal bool Armed;
        internal bool Fired;
        internal Action? Callback;
        private int rankSpellReads;
        public bool Equals(ushort x, ushort y) => x == y;
        public int GetHashCode(ushort value)
        {
            // First lookup is Status.ContainsKey; the second is the genuine
            // original-map lookup in SpellStorage's prepared Remove (or old Remove).
            if (Armed && value == 21930 && ++rankSpellReads == 2)
            {
                Fired = true;
                Callback!();
            }
            return value.GetHashCode();
        }
    }
}
