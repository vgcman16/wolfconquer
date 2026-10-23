using System.Collections.Generic;
using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

public sealed class LegacyNativeHairfaceStorageConstructorTests
{
    [Fact]
    public async Task RealSocketOwnerConstructionRetainsPrivateOwnerAndDistinctEmptyStorage()
    {
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            _ = await fixture.ConnectGameAsync(false);
            var birth = await fixture.NextAsync();
            Assert.Equal(NativePlayerLoopback.ProbeOpcode, birth.Opcode);
            var client = Assert.IsType<LegacyNativeGameClientRuntime>(birth.Connection.NativeGameClient);
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            Assert.Null(client.HairfaceStorage);

            var first = new LegacyNativeHairfaceStorageRuntime(client);
            var second = new LegacyNativeHairfaceStorageRuntime(client);

            Assert.Same(client, ReadPrivateField(first, "Owner"));
            Assert.Same(client, ReadPrivateField(second, "Owner"));
            var firstState = AssertEmptyStorage(first);
            var secondState = AssertEmptyStorage(second);
            Assert.NotSame(first, second);
            Assert.NotSame(firstState.Items, secondState.Items);
            Assert.NotSame(firstState.Objects, secondState.Objects);
            Assert.Null(client.HairfaceStorage);
            Assert.Same(player, client.Player);
            Assert.Same(client, player.Owner);
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
        });
    }

    [Fact]
    public async Task SeparateSocketOwnersNeverCrossAdoptHairfaceStorage()
    {
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            _ = await fixture.ConnectGameAsync(false);
            var firstBirth = await fixture.NextAsync();
            _ = await fixture.ConnectGameAsync(false);
            var secondBirth = await fixture.NextAsync();
            Assert.Equal(NativePlayerLoopback.ProbeOpcode, firstBirth.Opcode);
            Assert.Equal(NativePlayerLoopback.ProbeOpcode, secondBirth.Opcode);
            var firstClient = Assert.IsType<LegacyNativeGameClientRuntime>(firstBirth.Connection.NativeGameClient);
            var secondClient = Assert.IsType<LegacyNativeGameClientRuntime>(secondBirth.Connection.NativeGameClient);
            var firstPlayer = firstClient.Player;
            var secondPlayer = secondClient.Player;
            var firstFlags = firstPlayer.BitVector;
            var secondFlags = secondPlayer.BitVector;
            var firstRandom = firstPlayer.MyRandom;
            var secondRandom = secondPlayer.MyRandom;

            var first = new LegacyNativeHairfaceStorageRuntime(firstClient);
            var second = new LegacyNativeHairfaceStorageRuntime(secondClient);

            Assert.NotSame(firstBirth.Connection, secondBirth.Connection);
            Assert.NotSame(firstClient, secondClient);
            Assert.NotSame(firstPlayer, secondPlayer);
            Assert.Same(firstClient, ReadPrivateField(first, "Owner"));
            Assert.Same(secondClient, ReadPrivateField(second, "Owner"));
            var firstState = AssertEmptyStorage(first);
            var secondState = AssertEmptyStorage(second);
            Assert.NotSame(firstState.Items, secondState.Items);
            Assert.NotSame(firstState.Objects, secondState.Objects);
            Assert.Null(firstClient.HairfaceStorage);
            Assert.Null(secondClient.HairfaceStorage);
            Assert.Same(firstPlayer, firstClient.Player);
            Assert.Same(secondPlayer, secondClient.Player);
            Assert.Same(firstClient, firstPlayer.Owner);
            Assert.Same(secondClient, secondPlayer.Owner);
            Assert.Same(firstFlags, firstPlayer.BitVector);
            Assert.Same(secondFlags, secondPlayer.BitVector);
            Assert.Same(firstRandom, firstPlayer.MyRandom);
            Assert.Same(secondRandom, secondPlayer.MyRandom);
        });
    }

    [Fact]
    public void NullOwnerIsRejectedBeforeAnUnusableStorageCanBePublished()
    {
        var error = Assert.Throws<ArgumentNullException>(
            () => new LegacyNativeHairfaceStorageRuntime(null!));

        Assert.Equal("client", error.ParamName);
    }

    private static (List<LegacyNativeHairfaceStorageTable.Hairface> Items,
        LegacyNativeHairfaceStorageTable.Hairface[] Objects) AssertEmptyStorage(
        LegacyNativeHairfaceStorageRuntime storage)
    {
        var items = Assert.IsType<List<LegacyNativeHairfaceStorageTable.Hairface>>(
            ReadPrivateField(storage, "items"));
        var objects = Assert.IsType<LegacyNativeHairfaceStorageTable.Hairface[]>(
            ReadPrivateField(storage, "objects"));
        Assert.Empty(items);
        Assert.Empty(objects);
        Assert.Same(objects, storage.Objects);
        Assert.Same(storage.Objects, storage.Objects);
        return (items, objects);
    }

    private static object? ReadPrivateField(LegacyNativeHairfaceStorageRuntime storage, string name) =>
        typeof(LegacyNativeHairfaceStorageRuntime)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.GetValue(storage);
}
