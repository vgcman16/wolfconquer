using System.Reflection;
using Holder = WolfConquer.Core.Data.LegacyNativeDataHolder;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeDataHolderTests
{
    private const string Tail = ";Persist Security Info=True;Pooling=true; Min Pool Size = 32;  Max Pool Size = 300;";

    // Shared state is an explicit component MODEL, never a cold-start assertion.
    private sealed class RestoreString : IDisposable
    {
        private readonly string? previous = Holder.ConnectionString;
        public void Dispose() => Holder.ConnectionString = previous;
    }

    [Fact]
    public void PartialOwnerHasOnlyMutableStringStorageAndTheExactFactorySignature()
    {
        var type = typeof(Holder);
        Assert.True(type.IsPublic && type.IsAbstract && type.IsSealed);
        var field = Assert.Single(type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("ConnectionString", field.Name);
        Assert.Equal(typeof(string), field.FieldType);
        Assert.False(field.IsInitOnly);
        Assert.False(field.IsLiteral);
        Assert.Equal(NullabilityState.Nullable, new NullabilityInfoContext().Create(field).ReadState);
        var method = Assert.Single(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(candidate => !candidate.IsSpecialName));
        Assert.Equal("CreateConnection", method.Name);
        Assert.Equal(typeof(bool), method.ReturnType);
        Assert.Equal(new[] { "host", "username", "password", "database" },
            method.GetParameters().Select(parameter => parameter.Name));
        Assert.All(method.GetParameters(), parameter => Assert.Equal(typeof(string), parameter.ParameterType));
        var getter = Assert.Single(type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)); Assert.Equal("MySqlConnection", getter.Name);
        Assert.Equal(typeof(MySql.Data.MySqlClient.MySqlConnection), getter.PropertyType); Assert.True(getter.CanRead && !getter.CanWrite);
    }

    [Fact]
    public void NullableMutableStorageKeepsTheExactAssignedReferenceInExplicitModelStates()
    {
        using var restore = new RestoreString();
        Holder.ConnectionString = null;
        Assert.Null(Holder.ConnectionString);
        var exact = new string("dummy-direct-reference".ToCharArray());
        Holder.ConnectionString = exact;
        Assert.Same(exact, Holder.ConnectionString);
        Holder.ConnectionString = null;
        Assert.Null(Holder.ConnectionString);
    }

    [Fact]
    public void FactoryPreservesNativeArgumentOrderAndEveryFixedLiteralSegment()
    {
        using var restore = new RestoreString();
        Assert.True(Holder.CreateConnection("dummy-host", "dummy-user", "dummy-password", "dummy-database"));
        Assert.Equal("Server=dummy-host;Port=3306;Database=dummy-database;Uid=dummy-user;Password=dummy-password"
            + Tail, Holder.ConnectionString);
    }

    [Fact]
    public void SeparatorsEqualsAndWhitespaceAreConcatenatedWithoutEscapingOrTrimming()
    {
        using var restore = new RestoreString();
        Assert.True(Holder.CreateConnection(" dummy-host;X=1 ", " dummy-user=2; ", " dummy-password;Y=3 ", " dummy-database;Z=4 "));
        Assert.Equal("Server= dummy-host;X=1 ;Port=3306;Database= dummy-database;Z=4 ;Uid= dummy-user=2; ;Password= dummy-password;Y=3 "
            + Tail, Holder.ConnectionString);
    }

    [Fact]
    public void NullEmptyAndMixedArgumentsKeepNativeEmptyConcatenationSlots()
    {
        using var restore = new RestoreString();
        Assert.True(Holder.CreateConnection(null, null, null, null));
        Assert.Equal("Server=;Port=3306;Database=;Uid=;Password=" + Tail, Holder.ConnectionString);
        Assert.True(Holder.CreateConnection("", "", "", ""));
        Assert.Equal("Server=;Port=3306;Database=;Uid=;Password=" + Tail, Holder.ConnectionString);
        Assert.True(Holder.CreateConnection(null, "dummy-user", null, "dummy-database"));
        Assert.Equal("Server=;Port=3306;Database=dummy-database;Uid=dummy-user;Password=" + Tail, Holder.ConnectionString);
    }

    [Fact]
    public void RepeatedFactoryCallsOverwriteStorageAndKeepTheEscapedPriorStringUnchanged()
    {
        using var restore = new RestoreString();
        Assert.True(Holder.CreateConnection("dummy-host-a", "dummy-user-a", "dummy-password-a", "dummy-database-a"));
        var escaped = Holder.ConnectionString;
        Assert.True(Holder.CreateConnection("dummy-host-b", "dummy-user-b", "dummy-password-b", "dummy-database-b"));
        Assert.Equal("Server=dummy-host-b;Port=3306;Database=dummy-database-b;Uid=dummy-user-b;Password=dummy-password-b"
            + Tail, Holder.ConnectionString);
        Assert.Equal("Server=dummy-host-a;Port=3306;Database=dummy-database-a;Uid=dummy-user-a;Password=dummy-password-a"
            + Tail, escaped);
        Assert.NotSame(escaped, Holder.ConnectionString);
    }

    [Fact]
    public void ExternalFieldReplacementIsObservedAndTheNextCallDoesNotAppendToIt()
    {
        using var restore = new RestoreString();
        var external = new string("dummy-external-unparsed-storage".ToCharArray());
        Holder.ConnectionString = external;
        Assert.Same(external, Holder.ConnectionString);
        Assert.True(Holder.CreateConnection("dummy-host", null, "dummy-password", null));
        Assert.Equal("Server=dummy-host;Port=3306;Database=;Uid=;Password=dummy-password" + Tail, Holder.ConnectionString);
        Assert.Equal("dummy-external-unparsed-storage", external);
    }

    [Fact]
    public void UnusableSyntheticEndpointTextStillReturnsTrueWithoutEagerProviderValidation()
    {
        using var restore = new RestoreString();
        Assert.True(Holder.CreateConnection("dummy.invalid;Port=not-a-port", "dummy-user", "dummy-password", "dummy?database"));
        Assert.Equal("Server=dummy.invalid;Port=not-a-port;Port=3306;Database=dummy?database;Uid=dummy-user;Password=dummy-password"
            + Tail, Holder.ConnectionString);
        // This assertion is not an independent network/no-I/O observation.
        // The exact pinned native method body is only concatenation, assignment and true.
    }
}
