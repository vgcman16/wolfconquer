using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

// Metadata only: never invoke SocketSelect, its managed helpers, or any native import.
public sealed class LegacyNativeSocketSelectHelperTests
{
    [Fact]
    public void SocketSelectKeepsPublicStaticInt32ReturnAndAllFiveLiteralParameterTypesAndNames()
    {
        var method = SocketSelectMethod();
        Assert.NotNull(method);
        Assert.True(method.IsPublic && method.IsStatic);
        Assert.False(method.IsPrivate || method.IsAbstract || method.IsGenericMethodDefinition);
        Assert.Equal(typeof(int), method.ReturnType);
        Assert.Equal(new[] { typeof(int), typeof(List<Socket>), typeof(List<Socket>), typeof(List<Socket>), typeof(int) },
            method.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(new[] { "FD_SETSIZE", "readsocket", "writesockets", "errorsockets", "stampms" },
            method.GetParameters().Select(parameter => parameter.Name));
    }

    [Fact]
    public void SocketSelectKeepsRequiredUndirectedParametersAndManagedBodyWithoutImportMetadata()
    {
        var method = SocketSelectMethod();
        Assert.NotNull(method);
        Assert.False((method.Attributes & MethodAttributes.PinvokeImpl) != 0);
        Assert.Null(method.GetCustomAttribute<DllImportAttribute>());
        Assert.NotNull(method.GetMethodBody());
        Assert.Equal(5, method.GetParameters().Length);
        Assert.All(method.GetParameters(), parameter =>
        {
            Assert.False(parameter.IsOptional || parameter.HasDefaultValue);
            Assert.False(parameter.IsIn || parameter.IsOut);
        });
    }

    private static MethodInfo SocketSelectMethod() =>
        typeof(LegacyNativeWs2_32).GetMethod("SocketSelect",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly,
            binder: null,
            types: new[] { typeof(int), typeof(List<Socket>), typeof(List<Socket>), typeof(List<Socket>), typeof(int) },
            modifiers: null)!;
}
