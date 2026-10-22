using System.Reflection;
using WolfConquer.Core.World;
using Pipe = WolfConquer.Core.World.LegacyNativePipeClientRuntime;
using Socket = WolfConquer.Core.Networking.LegacyNativeSecuritySocket;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePipeTransportTests
{
    [Fact]
    public Task DefaultPipeRetainsNativeNullOwnersAndLiteralFieldDefaults() => NativePacketTestThread.Run(() =>
    {
        var first = new Pipe();
        var second = new Pipe();
        Assert.False(first.Alive);
        Assert.Null(first.Socket);
        Assert.Null(first.Owner);
        Assert.Equal(0u, first.NewUserID);
        Assert.Equal("", first.ConnectToAddress);
        Assert.Equal((ushort)0, first.ConnectToPort);
        Assert.NotSame(first, second);
        Assert.False(second.Alive);
        Assert.Null(second.Socket);
        Assert.Null(second.Owner);
    });

    [Fact]
    public Task ConnectedConstructorBindsOnlyPassedSocketAndOwnerWithoutPublishingOwnerPipe() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.True(scope.Pipe.Alive);
        Assert.True(scope.Socket.Alive);
        Assert.Same(scope.Socket, scope.Pipe.Socket);
        Assert.Same(scope.Owner, scope.Pipe.Owner);
        Assert.Same(scope.Pipe, scope.Socket.Client);
        Assert.Null(scope.Owner.PipeClient);
        Assert.False(scope.Owner.IsConnectedInterServer());
        Assert.Equal("", scope.Owner.MacAddress);
        Assert.Equal(0u, scope.Pipe.NewUserID);
        Assert.Equal("", scope.Pipe.ConnectToAddress);
        Assert.Equal((ushort)0, scope.Pipe.ConnectToPort);
        Assert.Empty(scope.Queue);
        Assert.Null(scope.Socket.Connection);
    });

    [Fact]
    public Task ConstructorDoesNotMakePassedDeadSocketAliveOrRequireAnOwner() => NativePacketTestThread.Run(() =>
    {
        var socket = NewSocket();
        var pipe = new Pipe(socket, null!);
        Assert.True(pipe.Alive);
        Assert.False(socket.Alive);
        Assert.Same(socket, pipe.Socket);
        Assert.Same(pipe, socket.Client);
        Assert.Null(pipe.Owner);
        Assert.Null(QueueField.GetValue(socket));
        Assert.Null(socket.Connection);
    });

    [Fact]
    public Task ConstructorReplacesSocketClientWithoutRewritingEscapedPipeOwnerOrOwnerReference() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var first = scope.Pipe;
        scope.Owner.PipeClient = first;
        var next = new Pipe(scope.Socket, null!);
        Assert.Same(next, scope.Socket.Client);
        Assert.Same(first, scope.Owner.PipeClient);
        Assert.Same(scope.Owner, first.Owner);
        Assert.Null(next.Owner);
        Assert.Same(scope.Socket, first.Socket);
        Assert.True(first.Alive);
        Assert.True(next.Alive);
        Assert.True(scope.Owner.IsConnectedInterServer());
    });

    [Fact]
    public Task NullSocketConstructorThrowsInsteadOfInventingTransportOwner() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Throws<NullReferenceException>(() => new Pipe(null!, scope.Owner));
        Assert.Null(scope.Owner.PipeClient);
        Assert.Same(scope.Pipe, scope.Socket.Client);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task InterServerConnectionPredicateReadsOnlyNullablePipeFieldNotAliveSocketOrPlayer() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Owner.Player = null!;
        scope.Owner.Socket = null;
        scope.Owner.MacAddress = null!;
        Assert.False(scope.Owner.IsConnectedInterServer());
        var notConnected = new Pipe();
        scope.Owner.PipeClient = notConnected;
        Assert.True(scope.Owner.IsConnectedInterServer());
        Assert.False(notConnected.Alive);
        Assert.Null(notConnected.Socket);
        Assert.Null(notConnected.Owner);
        scope.Owner.PipeClient = scope.Pipe;
        Assert.True(scope.Owner.IsConnectedInterServer());
        scope.Owner.PipeClient = null;
        Assert.False(scope.Owner.IsConnectedInterServer());
        Assert.Null(scope.Owner.Player);
        Assert.Null(scope.Owner.Socket);
        Assert.Null(scope.Owner.MacAddress);
    });

    [Fact]
    public Task MutableNativeTransportFieldsPreserveAliasesNullsAndIndependentMacStorage() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var socket = NewSocket();
        var pipe = new Pipe();
        object marker = new();
        socket.Client = marker;
        pipe.Socket = socket;
        pipe.Owner = scope.Owner;
        pipe.NewUserID = uint.MaxValue;
        pipe.ConnectToAddress = null!;
        pipe.ConnectToPort = ushort.MaxValue;
        scope.Owner.MacAddress = "synthetic-earth-id";
        scope.Owner.PipeClient = pipe;
        Assert.Same(marker, socket.Client); // Direct field assignment does not bind Client.
        Assert.Same(socket, pipe.Socket);
        Assert.Same(scope.Owner, pipe.Owner);
        Assert.False(pipe.Alive);
        Assert.Equal(uint.MaxValue, pipe.NewUserID);
        Assert.Null(pipe.ConnectToAddress);
        Assert.Equal(ushort.MaxValue, pipe.ConnectToPort);
        Assert.Equal("synthetic-earth-id", scope.Owner.MacAddress);
        Assert.True(scope.Owner.IsConnectedInterServer());
        pipe.Owner = null!;
        pipe.Socket = null!;
        Assert.True(scope.Owner.IsConnectedInterServer());
        Assert.Equal("synthetic-earth-id", scope.Owner.MacAddress);
    });

    [Fact]
    public void AlivePropertyAndTransportSlotsRetainNativeWidthsAndMutability()
    {
        var alive = typeof(Pipe).GetProperty("Alive")!;
        Assert.Equal(typeof(bool), alive.PropertyType);
        Assert.True(alive.GetMethod!.IsPublic);
        Assert.True(alive.SetMethod!.IsPrivate);
        Assert.Equal(typeof(Socket), typeof(Pipe).GetField("Socket")!.FieldType);
        Assert.Equal(typeof(LegacyNativeGameClientRuntime), typeof(Pipe).GetField("Owner")!.FieldType);
        Assert.Equal(typeof(uint), typeof(Pipe).GetField("NewUserID")!.FieldType);
        Assert.Equal(typeof(string), typeof(Pipe).GetField("ConnectToAddress")!.FieldType);
        Assert.Equal(typeof(ushort), typeof(Pipe).GetField("ConnectToPort")!.FieldType);
        Assert.Equal(typeof(object), typeof(Socket).GetField("Client")!.FieldType);
        Assert.Equal(typeof(string), typeof(LegacyNativeGameClientRuntime).GetField("MacAddress")!.FieldType);
        var ownerPipe = typeof(LegacyNativeGameClientRuntime).GetField("PipeClient",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(typeof(Pipe), ownerPipe.FieldType);
        Assert.False(ownerPipe.IsInitOnly);
        Assert.False(typeof(Pipe).GetField("Socket")!.IsInitOnly);
    }
}
