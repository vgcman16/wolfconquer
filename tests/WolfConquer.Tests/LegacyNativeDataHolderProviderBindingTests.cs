using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MySql.Data.MySqlClient;
using WolfConquer.Core.Data;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

// Verification metadata only: no getter, connection construction, Open, or query.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeDataHolderProviderBindingTests
{
    private readonly ITestOutputHelper output;

    public LegacyNativeDataHolderProviderBindingTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void ActualTestHostAssembliesEmitPublicNuGetProviderBinding()
    {
        var records = new[]
        {
            Observe("Core", typeof(LegacyNativeDataHolder).Assembly),
            Observe("Tests", typeof(LegacyNativeDataHolderProviderBindingTests).Assembly),
            Observe("PublicNuGetProvider", typeof(MySqlConnection).Assembly)
        };
        var provider = records[2];
        Assert.Equal("MySql.Data", provider.Name);
        Assert.Equal("6.3.6.0", provider.Version);
        Assert.Equal("C5687FC88969C44D", provider.PublicKeyToken);
        // This pins the public NuGet 6.3.6 package, not the omitted private DLL.
        Assert.Equal(354304, provider.Bytes);
        Assert.Equal("22CFA2467F2CE262990F1E4DC3D9355DA76C295DA5EA5202D1F35F3CDD7D2823", provider.Sha256);
        var json = JsonSerializer.Serialize(records);
        output.WriteLine("PUBLIC_NUGET_PROVIDER_BINDING:" +
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }

    private static LoadedAssembly Observe(string role, Assembly assembly)
    {
        var location = assembly.Location;
        Assert.False(string.IsNullOrWhiteSpace(location));
        var bytes = File.ReadAllBytes(location);
        var identity = assembly.GetName();
        return new LoadedAssembly(role, location, assembly.FullName!, identity.Name!,
            identity.Version!.ToString(), Convert.ToHexString(identity.GetPublicKeyToken()!),
            Convert.ToHexString(SHA256.HashData(bytes)), bytes.Length);
    }

    private sealed record LoadedAssembly(string Role, string Location, string FullName,
        string Name, string Version, string PublicKeyToken, string Sha256, int Bytes);
}
