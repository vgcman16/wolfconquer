using System.Net.Sockets;
using System.Reflection;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

// Future Windows BCL resource controls: unbound IPv4/TCP sockets only.
// No native import, SocketSelect, worker, endpoint, or network operation is invoked.
public sealed class LegacyNativeSocketRealHandleTests
{
    [Fact]
    public void OneLiveSocketProducesItsActualHandleAfterTheCountHeader()
    {
        using var socket = NewOwnedSocket();
        var handle = socket.Handle;
        var sockets = new List<Socket> { socket };

        Assert.Equal(new[] { new IntPtr(1), handle }, Descriptors(sockets));
        AssertSameSockets(sockets, socket);
    }

    [Fact]
    public void DescriptorSlotsPreserveTheRealSocketInputOrder()
    {
        using var first = NewOwnedSocket();
        using var second = NewOwnedSocket();
        using var third = NewOwnedSocket();
        AssertDistinctHandles(first, second, third);
        var sockets = new List<Socket> { third, first, second };

        Assert.Equal(new[] { new IntPtr(3), third.Handle, first.Handle, second.Handle }, Descriptors(sockets));
        AssertSameSockets(sockets, third, first, second);
    }

    [Fact]
    public void RepeatedSocketReferencesProduceRepeatedRealHandleSlots()
    {
        using var first = NewOwnedSocket();
        using var second = NewOwnedSocket();
        AssertDistinctHandles(first, second);
        var sockets = new List<Socket> { first, first, second, first };

        Assert.Equal(new[] { new IntPtr(4), first.Handle, first.Handle, second.Handle, first.Handle },
            Descriptors(sockets));
        AssertSameSockets(sockets, first, first, second, first);
    }

    [Fact]
    public void NullSlotsRetainTheirPositionsAroundRealSocketHandles()
    {
        using var first = NewOwnedSocket();
        using var second = NewOwnedSocket();
        AssertDistinctHandles(first, second);
        var sockets = new List<Socket> { first, null!, second, null! };

        Assert.Equal(new[] { new IntPtr(4), first.Handle, IntPtr.Zero, second.Handle, IntPtr.Zero },
            Descriptors(sockets));
        AssertSameSockets(sockets, first, null, second, null);
    }

    [Fact]
    public void DisposedDescriptorEntryUsesTheDirectBclGetterOutcomeWithoutChangingTheList()
    {
        using var first = NewOwnedSocket();
        using var disposed = NewOwnedSocket();
        using var last = NewOwnedSocket();
        AssertDistinctHandles(first, disposed, last);
        disposed.Dispose();
        var direct = DirectDisposedHandleOutcome(disposed);
        var sockets = new List<Socket> { first, disposed, last };

        if (direct.Fault is null)
        {
            Assert.Equal(new[] { new IntPtr(3), first.Handle, direct.Handle, last.Handle },
                Descriptors(sockets));
        }
        else
        {
            var reflectionFault = Assert.Throws<TargetInvocationException>(() => { _ = Descriptors(sockets); });
            Assert.NotNull(reflectionFault.InnerException);
            Assert.Equal(direct.Fault.GetType(), reflectionFault.InnerException!.GetType());
        }
        AssertSameSockets(sockets, first, disposed, last);
    }

    [Fact]
    public void OneMatchingRealHandleRetainsTheSameSocketObject()
    {
        using var socket = NewOwnedSocket();
        var sockets = new List<Socket> { socket };
        var descriptors = new[] { new IntPtr(1), socket.Handle };

        Select(sockets, descriptors);

        AssertSameSockets(sockets, socket);
        Assert.Equal(new[] { new IntPtr(1), socket.Handle }, descriptors);
    }

    [Fact]
    public void DescriptorOrderDoesNotReorderSurvivingSocketReferences()
    {
        using var first = NewOwnedSocket();
        using var middle = NewOwnedSocket();
        using var last = NewOwnedSocket();
        AssertDistinctHandles(first, middle, last);
        var sockets = new List<Socket> { first, middle, last };
        var descriptors = new[] { new IntPtr(2), last.Handle, first.Handle };

        Select(sockets, descriptors);

        AssertSameSockets(sockets, first, last);
        Assert.Equal(new[] { new IntPtr(2), last.Handle, first.Handle }, descriptors);
    }

    [Fact]
    public void OneDescriptorRetainsEveryRepeatedReferenceToItsSocket()
    {
        using var retained = NewOwnedSocket();
        using var removed = NewOwnedSocket();
        AssertDistinctHandles(retained, removed);
        var sockets = new List<Socket> { retained, retained, removed, retained };

        Select(sockets, new[] { new IntPtr(1), retained.Handle });

        AssertSameSockets(sockets, retained, retained, retained);
    }

    [Fact]
    public void RepeatedDescriptorsDoNotDuplicateASurvivingSocket()
    {
        using var removed = NewOwnedSocket();
        using var retained = NewOwnedSocket();
        AssertDistinctHandles(removed, retained);
        var sockets = new List<Socket> { removed, retained };

        Select(sockets, new[] { new IntPtr(2), retained.Handle, retained.Handle });

        AssertSameSockets(sockets, retained);
    }

    [Fact]
    public void AdjacentLeadingNonmembersAreRemovedWithoutSkippingTheShiftedSocket()
    {
        using var first = NewOwnedSocket();
        using var second = NewOwnedSocket();
        using var retained = NewOwnedSocket();
        AssertDistinctHandles(first, second, retained);
        var sockets = new List<Socket> { first, second, retained };

        Select(sockets, new[] { new IntPtr(1), retained.Handle });

        AssertSameSockets(sockets, retained);
    }

    [Fact]
    public void AlternatingNonmembersAreRemovedWhileSurvivorOrderIsPreserved()
    {
        using var first = NewOwnedSocket();
        using var second = NewOwnedSocket();
        using var third = NewOwnedSocket();
        using var fourth = NewOwnedSocket();
        AssertDistinctHandles(first, second, third, fourth);
        var sockets = new List<Socket> { first, second, third, fourth };

        Select(sockets, new[] { new IntPtr(2), fourth.Handle, second.Handle });

        AssertSameSockets(sockets, second, fourth);
    }

    [Fact]
    public void AdjacentTrailingNonmembersAreBothRemoved()
    {
        using var retained = NewOwnedSocket();
        using var second = NewOwnedSocket();
        using var third = NewOwnedSocket();
        AssertDistinctHandles(retained, second, third);
        var sockets = new List<Socket> { retained, second, third };

        Select(sockets, new[] { new IntPtr(1), retained.Handle });

        AssertSameSockets(sockets, retained);
    }

    [Fact]
    public void ZeroCountClearsTheRealListWithoutDisposingItsSockets()
    {
        using var first = NewOwnedSocket();
        using var second = NewOwnedSocket();
        AssertDistinctHandles(first, second);
        var firstHandle = first.Handle;
        var secondHandle = second.Handle;
        var sockets = new List<Socket> { first, second };
        var alias = sockets;

        Select(sockets, new[] { IntPtr.Zero });

        Assert.Same(sockets, alias);
        Assert.Empty(alias);
        Assert.Equal(firstHandle, first.Handle);
        Assert.Equal(secondHandle, second.Handle);
    }

    [Fact]
    public void EarlierRemovalSurvivesALaterNullEntryGetterFault()
    {
        using var removed = NewOwnedSocket();
        using var retained = NewOwnedSocket();
        AssertDistinctHandles(removed, retained);
        var sockets = new List<Socket> { removed, null!, retained };

        Assert.IsType<NullReferenceException>(SelectionFault(sockets, new[] { new IntPtr(1), retained.Handle }));

        AssertSameSockets(sockets, null, retained);
    }

    [Fact]
    public void EarlierRemovalSurvivesALaterDisposedEntryGetterOutcome()
    {
        using var removed = NewOwnedSocket();
        using var disposed = NewOwnedSocket();
        using var retained = NewOwnedSocket();
        AssertDistinctHandles(removed, disposed, retained);
        disposed.Dispose();
        var direct = DirectDisposedHandleOutcome(disposed);
        var sockets = new List<Socket> { removed, disposed, retained };
        var retainedHandle = retained.Handle;
        var descriptors = new[] { new IntPtr(1), retainedHandle };

        if (direct.Fault is null)
        {
            Select(sockets, descriptors);

            if (direct.Handle == retainedHandle)
                AssertSameSockets(sockets, disposed, retained);
            else
                AssertSameSockets(sockets, retained);
        }
        else
        {
            var targetFault = SelectionFault(sockets, descriptors);
            Assert.Equal(direct.Fault.GetType(), targetFault.GetType());
            AssertSameSockets(sockets, disposed, retained);
        }
        Assert.DoesNotContain(sockets, socket => ReferenceEquals(socket, removed));
        Assert.Equal(new[] { new IntPtr(1), retainedHandle }, descriptors);
    }

    [Fact]
    public void FirstMatchingHandleShortCircuitsAnOtherwiseMissingDescriptorTail()
    {
        using var retained = NewOwnedSocket();
        var sockets = new List<Socket> { retained };
        var descriptors = new[] { new IntPtr(2), retained.Handle };

        Select(sockets, descriptors);

        AssertSameSockets(sockets, retained);
        Assert.Equal(new[] { new IntPtr(2), retained.Handle }, descriptors);
    }

    [Fact]
    public void NonmatchingRealHandleReachesAMissingTailBeforeAnyRemoval()
    {
        using var first = NewOwnedSocket();
        using var descriptorOwner = NewOwnedSocket();
        AssertDistinctHandles(first, descriptorOwner);
        var sockets = new List<Socket> { first, null! };
        var descriptors = new[] { new IntPtr(2), descriptorOwner.Handle };

        Assert.IsType<IndexOutOfRangeException>(SelectionFault(sockets, descriptors));

        AssertSameSockets(sockets, first, null);
        Assert.Equal(new[] { new IntPtr(2), descriptorOwner.Handle }, descriptors);
    }

    private static Socket NewOwnedSocket()
    {
        Assert.True(OperatingSystem.IsWindows(), "These mandatory controls require Windows BCL socket resources.");
        return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    private static void AssertDistinctHandles(params Socket[] sockets)
    {
        var handles = sockets.Select(socket => socket.Handle).ToArray();
        Assert.Equal(handles.Length, handles.Distinct().Count());
    }

    private static void AssertSameSockets(List<Socket> actual, params Socket?[] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var index = 0; index < expected.Length; index++)
            Assert.Same(expected[index], actual[index]);
    }

    private static (IntPtr Handle, Exception? Fault) DirectDisposedHandleOutcome(Socket socket)
    {
        var handle = IntPtr.Zero;
        var fault = Record.Exception(() => { handle = socket.Handle; });
        return (handle, fault);
    }

    private static MethodInfo Helper(string name, params Type[] parameterTypes) =>
        typeof(LegacyNativeWs2_32).GetMethod(name,
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly,
            binder: null, types: parameterTypes, modifiers: null)!;

    private static IntPtr[] Descriptors(List<Socket> sockets) =>
        (IntPtr[])Helper("SocketListToFileDescriptorSet", typeof(List<Socket>))
            .Invoke(null, new object?[] { sockets })!;

    private static void Select(List<Socket> sockets, IntPtr[] descriptors) =>
        Helper("SelectFileDescriptor", typeof(List<Socket>), typeof(IntPtr[]))
            .Invoke(null, new object?[] { sockets, descriptors });

    private static Exception SelectionFault(List<Socket> sockets, IntPtr[] descriptors)
    {
        var fault = Assert.Throws<TargetInvocationException>(() => Select(sockets, descriptors));
        Assert.NotNull(fault.InnerException);
        return fault.InnerException!;
    }
}
