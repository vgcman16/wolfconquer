using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using MySql.Data.MySqlClient;
using Holder = WolfConquer.Core.Data.LegacyNativeDataHolder;

namespace WolfConquer.Tests;

// Public NuGet-provider constructor/string-setter controls only; never Open/query/network APIs.
// Shared field states are declared MODEL substitutions, not host startup or cold-start proof.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeDataHolderConnectionTests
{
    [Fact]
    public void GetterHasTheReadOnlyStaticPublicNuGetProviderTypeAndIdentity()
    {
        var property = Assert.Single(typeof(Holder).GetProperties(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("MySqlConnection", property.Name);
        Assert.Equal(typeof(MySqlConnection), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
        Assert.Null(property.SetMethod);
        var method = Assert.IsAssignableFrom<MethodInfo>(property.GetMethod);
        Assert.True(method.IsPublic && method.IsStatic && method.IsSpecialName);
        Assert.Empty(method.GetParameters());
        var identity = property.PropertyType.Assembly.GetName();
        Assert.Equal("MySql.Data", identity.Name);
        Assert.Equal(new Version(6, 3, 6, 0), identity.Version);
        Assert.Equal("C5687FC88969C44D", Convert.ToHexString(identity.GetPublicKeyToken()!));
        var loadedPath = property.PropertyType.Assembly.Location;
        Assert.False(string.IsNullOrWhiteSpace(loadedPath));
        Assert.Equal("22CFA2467F2CE262990F1E4DC3D9355DA76C295DA5EA5202D1F35F3CDD7D2823",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(loadedPath))));
    }

    [Fact]
    public void FactoryRawOptionsProduceFreshClosedConnectionsWithoutChangingStorage()
    {
        using var restore = new RestoreString();
        Assert.True(Holder.CreateConnection(
            "dummy-host.invalid", "dummy-user", "dummy-password-goal546", "dummy-database-a"));
        var raw = Holder.ConnectionString;
        using var first = Holder.MySqlConnection;
        using var second = Holder.MySqlConnection;

        Assert.NotSame(first, second);
        Assert.IsType<MySqlConnection>(first);
        Assert.IsType<MySqlConnection>(second);
        Assert.Equal(ConnectionState.Closed, first.State);
        Assert.Equal(ConnectionState.Closed, second.State);
        var parsed = new MySqlConnectionStringBuilder(first.ConnectionString);
        Assert.Equal("dummy-host.invalid", parsed.Server);
        Assert.Equal("dummy-user", parsed.UserID);
        Assert.Equal("dummy-password-goal546", parsed.Password);
        Assert.Equal("dummy-database-a", parsed.Database);
        Assert.Equal(3306u, parsed.Port);
        Assert.True(parsed.PersistSecurityInfo);
        Assert.True(parsed.Pooling);
        Assert.Equal(32u, parsed.MinimumPoolSize);
        Assert.Equal(300u, parsed.MaximumPoolSize);
        Assert.Same(raw, Holder.ConnectionString);
    }

    [Fact]
    public void NullAndEmptyFieldStatesUseTheRealProviderDefaultsWithoutRepairingStorage()
    {
        using var restore = new RestoreString();
        Holder.ConnectionString = null;
        using var fromNull = Holder.MySqlConnection;
        Assert.Equal(ConnectionState.Closed, fromNull.State);
        Assert.Equal("", fromNull.ConnectionString);
        Assert.Equal("", fromNull.Database);
        Assert.Null(Holder.ConnectionString);

        Holder.ConnectionString = "";
        using var fromEmpty = Holder.MySqlConnection;
        Assert.NotSame(fromNull, fromEmpty);
        Assert.Equal(ConnectionState.Closed, fromEmpty.State);
        Assert.Equal("", fromEmpty.ConnectionString);
        Assert.Same(string.Empty, Holder.ConnectionString);
    }

    [Fact]
    public void EachGetterRereadsLiveStorageAndOlderConnectionsRetainTheirSettings()
    {
        using var restore = new RestoreString();
        Holder.ConnectionString = "Server=dummy-a.invalid;Database=dummy-a;Uid=dummy-user;Password=dummy-password-a";
        using var first = Holder.MySqlConnection;
        var escapedRaw = Holder.ConnectionString;
        Holder.ConnectionString = "Server=dummy-b.invalid;Database=dummy-b;Uid=dummy-user;Password=dummy-password-b";
        using var second = Holder.MySqlConnection;

        Assert.NotSame(first, second);
        Assert.Equal("dummy-a", first.Database);
        Assert.Equal("dummy-b", second.Database);
        Assert.Equal(ConnectionState.Closed, first.State);
        Assert.Equal(ConnectionState.Closed, second.State);
        second.ConnectionString = "Database=dummy-direct-replacement";
        Assert.Equal("dummy-a", first.Database);
        Assert.Equal("dummy-direct-replacement", second.Database);
        Assert.Equal("Server=dummy-a.invalid;Database=dummy-a;Uid=dummy-user;Password=dummy-password-a", escapedRaw);
        Assert.Equal("Server=dummy-b.invalid;Database=dummy-b;Uid=dummy-user;Password=dummy-password-b", Holder.ConnectionString);
    }

    [Fact]
    public void UnknownKeywordThrowsTheProviderArgumentFaultAndKeepsExactRawReference()
    {
        using var restore = new RestoreString();
        var raw = new string("dummy-unsupported-option=dummy-value".ToCharArray());
        Holder.ConnectionString = raw;

        Assert.Throws<ArgumentException>(() => { _ = Holder.MySqlConnection; });
        Assert.Same(raw, Holder.ConnectionString);
    }

    [Fact]
    public void NumericOverflowAndBooleanConversionFaultsAreNotCaughtOrNormalized()
    {
        using var restore = new RestoreString();
        Holder.ConnectionString = "Port=dummy-not-a-number";
        Assert.Throws<FormatException>(() => { _ = Holder.MySqlConnection; });
        Assert.Equal("Port=dummy-not-a-number", Holder.ConnectionString);
        Holder.ConnectionString = "Port=4294967296";
        Assert.Throws<OverflowException>(() => { _ = Holder.MySqlConnection; });
        Assert.Equal("Port=4294967296", Holder.ConnectionString);
        Holder.ConnectionString = "Pooling=dummy-not-a-bool";
        Assert.Throws<FormatException>(() => { _ = Holder.MySqlConnection; });
        Assert.Equal("Pooling=dummy-not-a-bool", Holder.ConnectionString);
    }

    [Fact]
    public void MalformedSyntaxFaultDoesNotHideLaterLiveValidStorage()
    {
        using var restore = new RestoreString();
        Holder.ConnectionString = "Server";
        Assert.Throws<ArgumentException>(() => { _ = Holder.MySqlConnection; });
        Assert.Equal("Server", Holder.ConnectionString);
        Holder.ConnectionString = "Database=dummy-after-fault";
        using var valid = Holder.MySqlConnection;
        Assert.Equal("dummy-after-fault", valid.Database);
        Assert.Equal(ConnectionState.Closed, valid.State);
        Assert.Equal("Database=dummy-after-fault", Holder.ConnectionString);
    }

    [Fact]
    public void FactoryStillConcatenatesTrueBeforeTheGetterPerformsProviderValidation()
    {
        using var restore = new RestoreString();
        Assert.True(Holder.CreateConnection(
            "dummy-host.invalid;dummy-unsupported-option=dummy-value",
            "dummy-user", "dummy-password-deferred", "dummy-database"));
        var raw = Holder.ConnectionString;

        Assert.Contains("dummy-unsupported-option=dummy-value", raw);
        Assert.Throws<ArgumentException>(() => { _ = Holder.MySqlConnection; });
        Assert.Same(raw, Holder.ConnectionString);
    }

    private sealed class RestoreString : IDisposable
    {
        private readonly string? previous = Holder.ConnectionString;
        public void Dispose() => Holder.ConnectionString = previous;
    }
}
