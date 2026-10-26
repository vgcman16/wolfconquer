using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MailboxPointsCurrentChecksDoNotInvokeSourceDictionaryCallbacks(bool published)
    {
        using var tables = new Tables();
        var (client, ninja) = MailboxNinja();
        var armed = false;
        var comparer = new MailboxPointMutatingComparer(() =>
        {
            if (!armed) return;
            armed = false;
            ninja.Exp++;
        });
        var items = new ConcurrentDictionary<uint, LegacyNativeNinja.Item>(comparer);
        foreach (var pair in ninja.Items) Assert.True(items.TryAdd(pair.Key, pair.Value));
        ninja.Items = items;
        Assert.True(ninja.TryPrepareMailboxPoints(client, MailboxNinjaLine, 50, out var projection));
        if (published) Assert.True(projection!.TryPublishCommitted());
        var expected = ninja.Exp;
        armed = true;
        var current = published ? projection!.IsPublishedCurrent() : projection!.IsCurrent();
        Assert.Equal(expected, ninja.Exp);
        Assert.True(current);
        Assert.Null(client.Socket);
    }

    private sealed class MailboxPointMutatingComparer(Action lookup) : IEqualityComparer<uint>
    {
        public bool Equals(uint left, uint right) => left == right;
        public int GetHashCode(uint value) { lookup(); return value.GetHashCode(); }
    }
}
