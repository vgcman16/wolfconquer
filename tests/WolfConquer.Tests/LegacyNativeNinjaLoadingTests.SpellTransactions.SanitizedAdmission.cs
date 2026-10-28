using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NegativeSpellExperienceRefusesBeforeComparerCalloutsAndAnyOutput(bool update) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedSpellTransaction(scope);
            var comparer = InstallSpellMutationComparer(scope,
                () => throw new InvalidOperationException("Invalid intent must not reach this comparer."));
            var before = new LoadingTransactionWitness(scope);
            comparer.Armed = true;

            var error = Record.Exception(() =>
            {
                if (update) scope.Spells.Update(scope.Packet, new LegacyNativeMsgSpell
                {
                    ID = 1000, Level = 8, SoulLevel = 2, PreviousLevel = 7,
                    Experience = -13, Type = 12, Soul = 11,
                });
                else scope.Spells.Add(scope.Packet, 1000, 8, 2, 7, -13);
            });

            before.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
            Assert.False(comparer.Triggered);
            Assert.True(comparer.Armed);
            Assert.Same(comparer, scope.Spells.ClientSpells.Comparer);
        });

    [Theory]
    [InlineData("add")]
    [InlineData("update")]
    public Task ValidSpellIntentReachesItsRealComparerAndRetainsSuccessfulWire(string operation) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedSpellTransaction(scope);
            var comparer = InstallSpellMutationComparer(scope, () => { });
            var original = scope.Spells.ClientSpells;
            var originalTarget = original[1000];
            var unaffected = original.Where(pair => pair.Key != 1000).ToArray();
            var escaped = new LegacyNativeSpellMutationWitness(scope.Spells);
            comparer.Armed = true;

            InvokeSpellTransaction(scope, operation, scope.Packet);

            Assert.True(comparer.Triggered);
            Assert.Same(comparer, scope.Spells.ClientSpells.Comparer);
            Assert.NotSame(original, scope.Spells.ClientSpells);
            escaped.RequireEscapedMapUnchanged();
            Assert.Equal(original.Keys.Order(), scope.Spells.ClientSpells.Keys.Order());
            foreach (var pair in unaffected) Assert.Same(pair.Value, scope.Spells.ClientSpells[pair.Key]);
            var result = scope.Spells.ClientSpells[1000];
            Assert.NotSame(originalTarget, result);
            Assert.Equal((ushort)1000, result.ID);
            Assert.Equal((ushort)8, result.Level);
            Assert.Equal(13, result.Experience);
            Assert.Equal((byte)2, result.SoulLevel);
            Assert.Equal((byte)7, result.PreviousLevel);
            // Independent complete2101 bytes: XP13, ID1000, level8, native
            // Add/Update fresh-row fields and the real SoulLevel2 OR mask.
            scope.Sent(Convert.FromHexString("19003508" + "0D000000" + "E8030000" +
                "08000000" + "00000000" + "00" + "12000000"));
        });
}
