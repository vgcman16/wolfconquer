using System.Reflection;
using System.Runtime.CompilerServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeSharedPathMetadataTests
{
    [Fact]
    public void NativePathMethodKeepsItsExactPublicRequiredExtensionSignature()
    {
        var method = typeof(LegacyNativeSharedExtensions).GetMethod(
            "RemoveIllegalCharacters", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
            binder: null, types: new[] { typeof(string), typeof(bool), typeof(bool) }, modifiers: null)!;
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
        Assert.True(method.IsPublic && method.IsStatic);
        Assert.False(method.IsGenericMethod);
        Assert.NotNull(method.GetCustomAttribute<ExtensionAttribute>());
        var parameters = method.GetParameters();
        Assert.Equal(new[] { typeof(string), typeof(bool), typeof(bool) }, parameters.Select(p => p.ParameterType));
        Assert.Equal(new[] { "str", "path", "file" }, parameters.Select(p => p.Name));
        Assert.All(parameters, parameter =>
        {
            Assert.False(parameter.IsOptional);
            Assert.False(parameter.HasDefaultValue);
        });
        Assert.Null(typeof(LegacyNativeSharedExtensions).TypeInitializer);
    }
}
