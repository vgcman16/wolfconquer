using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMutationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task InvalidFlagSealRefusesBeforeFlagStatePacketLoanOrOutput(bool remove) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.Flag = remove ? 1UL : 0;
        typeof(LegacyNativePacket).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, "short");
        var before = new FlagMutationWitness(scope);

        var error = Record.Exception(() => InvokeFlagMutation(scope, remove));

        before.RequireUnchanged();
        Assert.IsType<InvalidDataException>(error);
    });

    [Theory]
    [InlineData(false, "player")]
    [InlineData(true, "player")]
    [InlineData(false, "foreign-player")]
    [InlineData(true, "foreign-player")]
    [InlineData(false, "foreign-user")]
    [InlineData(true, "foreign-user")]
    [InlineData(false, "detached")]
    [InlineData(true, "detached")]
    [InlineData(false, "zero-uid")]
    [InlineData(true, "zero-uid")]
    public Task FlagEmissionRequiresTheCurrentPhysicalPositiveActor(bool remove, string invalid) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var foreign = new NativeSpellTestFixture();
        scope.Ninja.Flag = remove ? 1UL : 0;
        if (invalid == "player") scope.Client.Player = null!;
        if (invalid == "foreign-player") scope.Client.Player = foreign.Owner.Player; // Same UID17, different constructor.
        if (invalid == "foreign-user") scope.Ninja.user = foreign.Owner;
        if (invalid == "detached") scope.Client.MyNinja = new(scope.Client);
        if (invalid == "zero-uid") scope.Client.Player.UID = 0;
        var before = new FlagMutationWitness(scope);

        var error = Record.Exception(() => InvokeFlagMutation(scope, remove));

        before.RequireUnchanged();
        Assert.Empty(foreign.Queue);
        Assert.IsType<InvalidOperationException>(error);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualItemComparerCannotPublishFlagsInsideActiveNinjaPreparation(bool remove) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.Flag = remove ? 1UL : 0;
        typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(scope.Client.Player, 5000u);
        scope.Meta(1, 100, 9, 1);
        var existing = scope.Item(100, 100, 9);
        var comparer = new FlagPreparationComparer(() => InvokeFlagMutation(scope, remove));
        var map = new ConcurrentDictionary<uint, LegacyNativeNinja.Item>(comparer);
        Assert.True(map.TryAdd(100, existing));
        scope.Ninja.Items = map;
        var before = new FlagMutationWitness(scope);
        comparer.Armed = true;

        var error = Record.Exception(() => scope.Ninja.AddItem(100, 9, 1, 4, 5));

        before.RequireUnchanged();
        Assert.Same(map, scope.Ninja.Items);
        Assert.Same(existing, Assert.Single(map).Value);
        Assert.True(comparer.Triggered);
        Assert.IsType<InvalidOperationException>(error);
    });

    private sealed class FlagPreparationComparer(Action reenter) : IEqualityComparer<uint>
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
}
