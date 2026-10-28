namespace WolfConquer.Tests;

public sealed partial class LegacyFullAccountClassMutationTests
{
    internal sealed partial class DurableClassFixture
    {
        internal void UsePaidNobility(IReadOnlyDictionary<string, string> paid)
        {
            Assert.Equal(Original.Character.PaidNobility.ToArray(), paid.ToArray());
            Assert.True(Store.TryGet(Connection, out var current));
            Assert.Same(Original, current);
            var updated = Original with { Character = Original.Character with { PaidNobility = paid } };
            Store.Set(Connection, updated);
            Original = updated;
        }

        internal void UseLiteralUsersRoot(string literal)
        {
            Assert.True(Path.IsPathFullyQualified(literal));
            Assert.Equal(Users, Path.TrimEndingDirectorySeparator(Path.GetFullPath(literal)));
            Assert.True(Store.TryGet(Connection, out var current));
            Assert.Same(Original, current);
            var updated = Original with { UsersRoot = literal };
            Store.Set(Connection, updated);
            Original = updated;
        }
    }
}
