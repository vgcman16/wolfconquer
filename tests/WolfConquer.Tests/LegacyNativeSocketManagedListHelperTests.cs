using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

// Only null socket slots are supplied. No BCL Socket is constructed and no Handle getter is entered.
public sealed class LegacyNativeSocketManagedListHelperTests
{
    [Fact]
    public void BothGenuineHelpersKeepPrivateStaticManagedSignaturesAndParameterNames()
    {
        var descriptors = Helper("SocketListToFileDescriptorSet", typeof(List<Socket>));
        var selection = Helper("SelectFileDescriptor", typeof(List<Socket>), typeof(IntPtr[]));
        Assert.Equal(typeof(IntPtr[]), descriptors.ReturnType);
        Assert.Equal(typeof(void), selection.ReturnType);
        Assert.Equal(new[] { "socketList" }, descriptors.GetParameters().Select(parameter => parameter.Name));
        Assert.Equal(new[] { "socketList", "fileDescriptorSet" }, selection.GetParameters().Select(parameter => parameter.Name));
        foreach (var method in new[] { descriptors, selection })
        {
            Assert.True(method.IsPrivate && method.IsStatic);
            Assert.False(method.IsGenericMethodDefinition || method.IsAbstract);
            Assert.False((method.Attributes & MethodAttributes.PinvokeImpl) != 0);
            Assert.Null(method.GetCustomAttribute<DllImportAttribute>());
            Assert.NotNull(method.GetMethodBody());
            Assert.All(method.GetParameters(), parameter =>
            {
                Assert.False(parameter.IsOptional || parameter.HasDefaultValue);
                Assert.False(parameter.IsIn || parameter.IsOut);
            });
        }
    }

    [Fact]
    public void NullSocketListReturnsNullDescriptorSet()
    {
        Assert.Null(Descriptors(null));
    }

    [Fact]
    public void EmptySocketListReturnsNullWithoutChangingTheList()
    {
        var sockets = new List<Socket>();
        Assert.Null(Descriptors(sockets));
        Assert.Empty(sockets);
    }

    [Fact]
    public void OneNullSocketProducesCountHeaderAndOneZeroSlot()
    {
        var sockets = new List<Socket> { null! };
        Assert.Equal(new[] { new IntPtr(1), IntPtr.Zero }, Descriptors(sockets));
        Assert.Single(sockets);
        Assert.Null(sockets[0]);
    }

    [Fact]
    public void ThreeNullSocketsProduceCountPlusOneSlotsWithoutHandleAccess()
    {
        var sockets = new List<Socket> { null!, null!, null! };
        Assert.Equal(new[] { new IntPtr(3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero }, Descriptors(sockets));
        Assert.Equal(3, sockets.Count);
        Assert.All(sockets, socket => Assert.Null(socket));
    }

    [Fact]
    public void RepeatedDescriptorCreationAllocatesFreshArrays()
    {
        var sockets = new List<Socket> { null!, null! };
        var first = Descriptors(sockets)!;
        var second = Descriptors(sockets)!;
        Assert.NotSame(first, second);
        first[0] = new IntPtr(99);
        first[1] = new IntPtr(7);
        Assert.Equal(new[] { new IntPtr(2), IntPtr.Zero, IntPtr.Zero }, second);
        Assert.Equal(2, sockets.Count);
        Assert.All(sockets, socket => Assert.Null(socket));
    }

    [Fact]
    public void NullSelectionListReturnsBeforeNullOrEmptyDescriptorAccess()
    {
        Select(null, null);
        Select(null, Array.Empty<IntPtr>());
    }

    [Fact]
    public void EmptySelectionListReturnsBeforeNullOrEmptyDescriptorAccess()
    {
        var sockets = new List<Socket>();
        Select(sockets, null);
        Select(sockets, Array.Empty<IntPtr>());
        Assert.Empty(sockets);
    }

    [Fact]
    public void ZeroDescriptorCountClearsOneNullSocketWithoutReadingAHandle()
    {
        var sockets = new List<Socket> { null! };
        Select(sockets, new[] { IntPtr.Zero });
        Assert.Empty(sockets);
    }

    [Fact]
    public void ZeroDescriptorCountClearsAllNullSlotsAndIgnoresTrailingData()
    {
        var sockets = new List<Socket> { null!, null!, null! };
        var alias = sockets;
        var descriptors = new[] { IntPtr.Zero, new IntPtr(-1), new IntPtr(int.MaxValue) };
        Select(sockets, descriptors);
        Assert.Same(sockets, alias);
        Assert.Empty(alias);
        Assert.Equal(new[] { IntPtr.Zero, new IntPtr(-1), new IntPtr(int.MaxValue) }, descriptors);
    }

    [Fact]
    public void NonemptyListWithNullDescriptorArrayFaultsBeforeChangingNullSlots()
    {
        var sockets = new List<Socket> { null!, null! };
        AssertSelectionFault<NullReferenceException>(sockets, null);
        Assert.Equal(2, sockets.Count);
        Assert.All(sockets, socket => Assert.Null(socket));
    }

    [Fact]
    public void NonemptyListWithEmptyDescriptorArrayFaultsBeforeChangingNullSlots()
    {
        var sockets = new List<Socket> { null!, null! };
        AssertSelectionFault<IndexOutOfRangeException>(sockets, Array.Empty<IntPtr>());
        Assert.Equal(2, sockets.Count);
        Assert.All(sockets, socket => Assert.Null(socket));
    }

    [Fact]
    public void PositiveCountWithoutFirstSlotIndexesArrayBeforeNullSocketHandle()
    {
        var sockets = new List<Socket> { null! };
        AssertSelectionFault<IndexOutOfRangeException>(sockets, new[] { new IntPtr(1) });
        Assert.Single(sockets);
        Assert.Null(sockets[0]);
    }

    [Fact]
    public void PositiveCountWithFirstSlotThenFaultsOnNullSocketWithoutRemoval()
    {
        var sockets = new List<Socket> { null!, null! };
        var descriptors = new[] { new IntPtr(1), IntPtr.Zero };
        AssertSelectionFault<NullReferenceException>(sockets, descriptors);
        Assert.Equal(2, sockets.Count);
        Assert.All(sockets, socket => Assert.Null(socket));
        Assert.Equal(new[] { new IntPtr(1), IntPtr.Zero }, descriptors);
    }

    [Fact]
    public void MaximumPositiveCountStillIndexesMissingFirstSlotBeforeNullHandle()
    {
        var sockets = new List<Socket> { null! };
        AssertSelectionFault<IndexOutOfRangeException>(sockets, new[] { new IntPtr(int.MaxValue) });
        Assert.Single(sockets);
        Assert.Null(sockets[0]);
    }

    [Fact]
    public void ExtraDescriptorSlotsCannotSkipFirstNullSocketFault()
    {
        var sockets = new List<Socket> { null!, null! };
        AssertSelectionFault<NullReferenceException>(sockets,
            new[] { new IntPtr(2), new IntPtr(17), new IntPtr(23), new IntPtr(31) });
        Assert.Equal(2, sockets.Count);
        Assert.All(sockets, socket => Assert.Null(socket));
    }

    [Fact]
    public void NegativeOneCountSkipsInnerLoopAndPreservesEveryNullSlot()
    {
        var sockets = new List<Socket> { null!, null!, null! };
        Select(sockets, new[] { new IntPtr(-1) });
        Assert.Equal(3, sockets.Count);
        Assert.All(sockets, socket => Assert.Null(socket));
    }

    [Fact]
    public void MinimumSignedCountSkipsInnerLoopWithoutArrayOrHandleAccess()
    {
        var sockets = new List<Socket> { null!, null! };
        Select(sockets, new[] { new IntPtr(int.MinValue) });
        Assert.Equal(2, sockets.Count);
        Assert.All(sockets, socket => Assert.Null(socket));
    }

    [Fact]
    public void NegativeCountIgnoresTrailingDescriptorsAndLeavesTheirValuesIntact()
    {
        var sockets = new List<Socket> { null! };
        var descriptors = new[] { new IntPtr(-7), new IntPtr(123) };
        Select(sockets, descriptors);
        Assert.Single(sockets);
        Assert.Null(sockets[0]);
        Assert.Equal(new[] { new IntPtr(-7), new IntPtr(123) }, descriptors);
    }

    [Fact]
    public void ZeroCountClearRemainsVisibleThroughTheOriginalListReference()
    {
        var sockets = new List<Socket> { null!, null! };
        var sameList = sockets;
        Select(sockets, new[] { IntPtr.Zero });
        Assert.Same(sockets, sameList);
        Assert.Empty(sameList);
        Assert.Null(Descriptors(sameList));
    }

    private static MethodInfo Helper(string name, params Type[] parameterTypes) =>
        typeof(LegacyNativeWs2_32).GetMethod(name,
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly,
            binder: null, types: parameterTypes, modifiers: null)!;

    private static IntPtr[]? Descriptors(List<Socket>? sockets) =>
        (IntPtr[]?)Helper("SocketListToFileDescriptorSet", typeof(List<Socket>))
            .Invoke(null, new object?[] { sockets });

    private static void Select(List<Socket>? sockets, IntPtr[]? descriptors) =>
        Helper("SelectFileDescriptor", typeof(List<Socket>), typeof(IntPtr[]))
            .Invoke(null, new object?[] { sockets, descriptors });

    private static void AssertSelectionFault<TException>(List<Socket> sockets, IntPtr[]? descriptors)
        where TException : Exception
    {
        var reflectionFault = Assert.Throws<TargetInvocationException>(() => Select(sockets, descriptors));
        Assert.IsType<TException>(reflectionFault.InnerException);
    }
}
