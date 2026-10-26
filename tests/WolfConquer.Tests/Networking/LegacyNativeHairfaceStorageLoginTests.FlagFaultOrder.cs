using System.Reflection;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Fact]
    public async Task ReflectionPrefixNegativeHairfaceCountRetainsAllEarlierAndLaterState()
    {
        using var catalog = new PrefixCatalogScope(new LegacyNativeHairfaceStorageTable.Hairface
        {
            ID = 999, Type = LegacyHairFaceStorageType.Avatar, Classes = [7], Colors = [0],
        });
        using var input = new LoginInput();
        var bytes = WritePrefixFields(input,
            "HairfaceStorage=1/999/0/-1/\r\nTailedBeastActivated=False\r\nSpawinEffect=False\r\nSpawinEffectSkills=False\r\n");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            player.Online = 9;
            player.SpawinEffect = true;
            player.SpawinEffectSkills = true;
            var before = ObservePrefixPlayer(player);
            var beasts = new LegacyNativeBeastsRuntime(client) { Activated = true };
            var storage = new LegacyNativeHairfaceStorageRuntime(client);
            var storageState = ReadEmptyState(storage);
            client.Beasts = beasts;
            client.HairfaceStorage = storage;
            var snapshot = ReadPrefixSnapshot(input);
            var prefix = typeof(WorldServerFrameHandler).GetMethod("TryApplyLegacyExistingCharacterLocalPrefix",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!;
            object?[] arguments = [birth.Connection, snapshot, null, null];

            Assert.False((bool)prefix.Invoke(handler, arguments)!);

            Assert.Same(snapshot, arguments[2]);
            Assert.Equal(before, ObservePrefixPlayer(player));
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Same(beasts, client.Beasts);
            Assert.True(beasts.Activated);
            Assert.Equal(0U, beasts.Points);
            Assert.Equal(0U, beasts.flag);
            Assert.Equal((byte)0, beasts.Level);
            Assert.Equal((byte)1, beasts.FixedLevel);
            AssertPreservedHairface(client, storage, client, storageState);
            Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
            Assert.Equal(7L, ReadPrefixSnapshot(input).Online);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }
}
