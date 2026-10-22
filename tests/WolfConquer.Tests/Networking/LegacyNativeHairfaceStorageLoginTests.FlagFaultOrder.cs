using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Fact]
    public async Task ReflectionPrefixHairfaceFaultRetainsLaterFlagsAfterEarlierMarkerAndCounterPublication()
    {
        using var input = new LoginInput();
        var fixtureText = Encoding.UTF8.GetString(input.InitialBytes).Replace(
            "RelicSpirit=True\r\n",
            "RelicSpirit=True\r\nHairfaceStorage=1/999/0/-1/\r\n" +
            "TailedBeastActivated=False\r\nSpawinEffect=False\r\nSpawinEffectSkills=False\r\n",
            StringComparison.Ordinal);
        var fixtureBytes = Encoding.UTF8.GetBytes(fixtureText);
        var expectedMarkerBytes = Encoding.UTF8.GetBytes(fixtureText.Replace(
            "Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
        File.WriteAllBytes(input.FilePath, fixtureBytes); // This LoginInput owns the GUID temporary root.

        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            player.Online = 9;
            player.RelicSpirit = false;
            player.SpawinEffect = true;
            player.SpawinEffectSkills = true;
            var beasts = new LegacyNativeBeastsRuntime(client) { Activated = true };
            var storage = new LegacyNativeHairfaceStorageRuntime(client);
            var storageState = ReadEmptyState(storage);
            client.Beasts = beasts;
            client.HairfaceStorage = storage;
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));

            var load = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
            Assert.Equal(CharacterSnapshotLoadStatus.Success, load.Status);
            var snapshot = load.Snapshot!;
            Assert.Equal(7L, snapshot.Online);
            Assert.Equal("1/999/0/-1/", snapshot.Attributes["HairfaceStorage"]);
            Assert.Equal("False", snapshot.Attributes["TailedBeastActivated"]);
            Assert.Equal("False", snapshot.Attributes["SpawinEffect"]);
            Assert.Equal("False", snapshot.Attributes["SpawinEffectSkills"]);
            Assert.Equal(fixtureBytes, File.ReadAllBytes(input.FilePath));
            var prefix = typeof(WorldServerFrameHandler).GetMethod(
                "TryApplyLegacyExistingCharacterLocalPrefix",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.NotNull(prefix);
            object?[] arguments = [birth.Connection, snapshot, null, null];

            // Invoke the actual private body against a socket-born owner. This is
            // reflection-body fault-order proof, not a TCP login-dispatch fault test.
            var wrapper = Assert.Throws<TargetInvocationException>(() => prefix.Invoke(handler, arguments));

            Assert.IsType<OverflowException>(wrapper.InnerException);
            Assert.True(beasts.Activated);
            Assert.True(player.SpawinEffect);
            Assert.True(player.SpawinEffectSkills);
            Assert.Same(client, birth.Connection.NativeGameClient);
            Assert.Same(player, client.Player);
            Assert.Same(client, player.Owner);
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Same(beasts, client.Beasts);
            Assert.Same(client, beasts.Owner);
            AssertPreservedHairface(client, storage, client, storageState);
            Assert.Equal(Uid, player.UID);
            Assert.Equal(10u, player.Online);
            Assert.True(player.RelicSpirit);
            Assert.Equal(expectedMarkerBytes, File.ReadAllBytes(input.FilePath));
            var after = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
            Assert.Equal(CharacterSnapshotLoadStatus.Success, after.Status);
            Assert.Equal(1L, after.Snapshot!.Online);
            Assert.Equal("1/999/0/-1/", after.Snapshot.Attributes["HairfaceStorage"]);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }
}
