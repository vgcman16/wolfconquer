using System.Reflection;
using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusFlagsTests
{
    [Fact]
    public void RepeatedValidatedReadsRetainPrivateCacheButReturnDistinctDefensiveArrays()
    {
        var flags = new Flags(32);
        Assert.True(flags.TryAdd(6, 7, false, 0));
        var row = flags.ArrayFlags[6];
        var first = flags.GetFlags();
        var privateCache = ReadPrivateCache(flags);
        var second = flags.GetFlags();
        Assert.NotSame(first, second);
        Assert.NotSame(privateCache, first);
        Assert.NotSame(privateCache, second);
        Assert.Same(privateCache, ReadPrivateCache(flags));
        Assert.Same(row, Assert.Single(first));
        Assert.Same(row, Assert.Single(second));
    }

    [Fact]
    public void EqualValuedReplacementRegistrationRefreshesCacheByPhysicalRowIdentity()
    {
        var flags = new Flags(32);
        Assert.True(flags.TryAdd(6, 7, false, 0));
        var old = Assert.Single(flags.GetFlags());
        var oldCache = ReadPrivateCache(flags);
        var replacement = new Flags.Flag(old.Key, old.Secounds, old.RemoveOnDead, old.InvokerSecouds)
        {
            Timer = old.Timer,
            TimerInvoker = old.TimerInvoker,
        };
        // A deterministic coherent raw registration replacement is not an atomic writer API.
        flags.ArrayFlags[6] = replacement;
        var current = Assert.Single(flags.GetFlags());
        Assert.Same(replacement, current);
        Assert.NotSame(old, current);
        Assert.NotSame(oldCache, ReadPrivateCache(flags));
        Assert.Same(replacement, Assert.Single(ReadPrivateCache(flags)!));
    }

    [Fact]
    public void MissingPrivateCacheDoesNotHideValidAuthoritativeRegistration()
    {
        var flags = new Flags(32);
        Assert.True(flags.TryAdd(6, 7, false, 0));
        var row = flags.ArrayFlags[6];
        CacheField.SetValue(flags, null);
        Assert.Same(row, Assert.Single(flags.GetFlags()));
        Assert.Same(row, Assert.Single(ReadPrivateCache(flags)!));
        Assert.True(flags.ContainFlag(6));
    }

    [Fact]
    public void PoisonedPrivateCacheCannotGrantOrRemoveAuthoritativeMembership()
    {
        var flags = new Flags(32);
        Assert.True(flags.TryAdd(6, 7, false, 0));
        var row = flags.ArrayFlags[6];
        var forged = new Flags.Flag(7, 9, true, 0);
        CacheField.SetValue(flags, new[] { forged });
        Assert.Same(row, Assert.Single(flags.GetFlags()));
        Assert.Same(row, Assert.Single(ReadPrivateCache(flags)!));
        Assert.False(flags.ContainFlag(7));
        Assert.True(flags.TryClearDeath());
        Assert.Same(row, flags.ArrayFlags[6]);
    }

    private static readonly FieldInfo CacheField = typeof(Flags).GetField("Array",
        BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static Flags.Flag[]? ReadPrivateCache(Flags flags) => (Flags.Flag[]?)CacheField.GetValue(flags);
}
