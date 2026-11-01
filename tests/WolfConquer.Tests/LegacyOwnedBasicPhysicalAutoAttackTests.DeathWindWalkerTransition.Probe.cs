using System.Collections;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    // Read-only observation of tokens issued by the actual private death query.
    // No native member is assigned, no token constructed/registered, and no
    // mutation/commit method is invoked from this probe.
    private sealed class ChangedWindWalkerTokenProbe
    {
        private readonly LegacyNativeStatusFlagsBigVector32 vector;
        private readonly object change, native, origin;
        private ChangedWindWalkerTokenProbe(LegacyNativeStatusFlagsBigVector32 vector,
            object change, object native, object origin)
        { this.vector = vector; this.change = change; this.native = native; this.origin = origin; }

        internal static ChangedWindWalkerTokenProbe Capture(LegacyNativeStatusFlagsBigVector32 vector, bool committed)
        {
            var changes = Table(vector, "windWalkerChanges").Where(row =>
                row.Key is LegacyNativeStatusFlagsBigVector32.WindWalkerChange { Changed: true, Proposed.Present: false }).ToArray();
            var entry = Assert.Single(changes);
            var native = entry.Value.GetType().GetProperty("Change", BindingFlags.Instance | BindingFlags.Public)!.GetValue(entry.Value);
            Assert.NotNull(native);
            var origin = Assert.Single(Table(vector, "windWalkerOrigins"), row => ReferenceEquals(row.Key, entry.Key)).Value;
            Assert.Equal("Query", origin.GetType().GetProperty("Kind")!.GetValue(origin)!.ToString());
            var result = new ChangedWindWalkerTokenProbe(vector, entry.Key, native, origin);
            Assert.Equal(committed, result.IsCommitted());
            return result;
        }

        internal void AssertPending()
        {
            Assert.False(IsCommitted());
            Assert.Single(Table(vector, "pendingChanges"), row => ReferenceEquals(row.Key, native));
            Assert.Single(Table(vector, "windWalkerChanges"), row => ReferenceEquals(row.Key, change));
        }

        internal void AssertCommitted()
        {
            Assert.True(IsCommitted());
            Assert.DoesNotContain(Table(vector, "pendingChanges"), row => ReferenceEquals(row.Key, native));
            Assert.Same(origin, Assert.Single(Table(vector, "windWalkerOrigins"), row => ReferenceEquals(row.Key, change)).Value);
        }

        private bool IsCommitted() => Assert.IsType<bool>(origin.GetType()
            .GetField("Committed", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(origin));

        private static (object Key, object Value)[] Table(object owner, string name)
        {
            var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var table = Assert.IsAssignableFrom<IEnumerable>(field.GetValue(owner));
            return table.Cast<object>().Select(entry =>
            {
                var type = entry.GetType();
                var key = type.GetProperty("Key")!.GetValue(entry);
                var value = type.GetProperty("Value")!.GetValue(entry);
                Assert.NotNull(key); Assert.NotNull(value);
                return (key, value);
            }).ToArray();
        }
    }
}
