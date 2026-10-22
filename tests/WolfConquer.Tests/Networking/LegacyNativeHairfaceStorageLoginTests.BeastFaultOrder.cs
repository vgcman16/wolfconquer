using System.Collections.Generic;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Fact]
    public async Task ReflectionPrefixEmptyBeastCachePublishesPointsBeforeNullRowFault()
    {
        await AssertBeastSetterFaultOrderAsync(missingFirstLevel: false);
    }

    [Fact]
    public async Task ReflectionPrefixMissingFirstBeastLevelPublishesActivatedLevelResetBeforeKeyFault()
    {
        await AssertBeastSetterFaultOrderAsync(missingFirstLevel: true);
    }

    private static async Task AssertBeastSetterFaultOrderAsync(bool missingFirstLevel)
    {
        using var input = new LoginInput();
        var savedActivated = missingFirstLevel ? "True" : "False";
        var fixtureText = Encoding.UTF8.GetString(input.InitialBytes).Replace(
            "RelicSpirit=True\r\n",
            "RelicSpirit=True\r\nHairfaceStorage=0/\r\nTailedBeastActivated=" + savedActivated +
            "\r\nSpawinEffect=True\r\nSpawinEffectSkills=False\r\n" +
            "TailedBeastTotalPoints=7\r\nTailedBeastFlag=123\r\n" +
            "DeityLandLuckyPoints=1234\r\nHeroPoints=5678\r\n" +
            "ClassExperience=56789\r\nSavePromote=54321\r\nSkinUnlock=23\r\n",
            StringComparison.Ordinal);
        var fixtureBytes = Encoding.UTF8.GetBytes(fixtureText);
        var expectedMarkerBytes = Encoding.UTF8.GetBytes(fixtureText.Replace(
            "Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
        File.WriteAllBytes(input.FilePath, fixtureBytes); // Only this LoginInput's owned GUID fixture.
        var outerCache = LegacyNativeBeastsAtrribute.Attributes;

        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            player.Online = 9;
            player.RelicSpirit = false;
            player.SpawinEffect = false;
            player.SpawinEffectSkills = true;
            player.DeityLandLuckyPoints = 11;
            player.HeroPoints = 22;
            player.ClassExperience = 31;
            player.SavePromote = 32;
            player.SkinUnlock = 33;
            player.Body = 2002;
            player.Face = 17;
            const uint flagSentinel = 0xA5A5A5A5;
            var beasts = new LegacyNativeBeastsRuntime(client)
            {
                Activated = !missingFirstLevel,
                Points = 13,
                flag = flagSentinel,
                Level = 0,
                FixedLevel = 5,
            };
            var storage = new LegacyNativeHairfaceStorageRuntime(client);
            var storageState = ReadEmptyState(storage);
            client.Beasts = beasts;
            client.HairfaceStorage = storage;
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));

            var load = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
            Assert.Equal(CharacterSnapshotLoadStatus.Success, load.Status);
            var snapshot = load.Snapshot!;
            Assert.Equal(7L, snapshot.Online);
            Assert.Equal("0/", snapshot.Attributes["HairfaceStorage"]);
            Assert.Equal(savedActivated, snapshot.Attributes["TailedBeastActivated"]);
            Assert.Equal("True", snapshot.Attributes["SpawinEffect"]);
            Assert.Equal("False", snapshot.Attributes["SpawinEffectSkills"]);
            Assert.Equal("7", snapshot.Attributes["TailedBeastTotalPoints"]);
            Assert.Equal("123", snapshot.Attributes["TailedBeastFlag"]);
            Assert.Equal("1234", snapshot.Attributes["DeityLandLuckyPoints"]);
            Assert.Equal("5678", snapshot.Attributes["HeroPoints"]);
            Assert.Equal(fixtureBytes, File.ReadAllBytes(input.FilePath));

            // Replace the actual native cache only inside serialized WithHandlerAsync;
            // its owned NativeLoginBeastMetadataScope restores the outer reference.
            var faultCache = new Dictionary<byte, LegacyNativeBeastsAtrribute.Attribute>();
            if (missingFirstLevel)
            {
                faultCache.Add(2, new LegacyNativeBeastsAtrribute.Attribute { Level = 2, RequiredPoints = 1000 });
            }
            LegacyNativeBeastsAtrribute.Attributes = faultCache;
            var prefix = typeof(WorldServerFrameHandler).GetMethod(
                "TryApplyLegacyExistingCharacterLocalPrefix",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.NotNull(prefix);
            object?[] arguments = [birth.Connection, snapshot, null, null];

            // Actual private prefix body on a socket-born owner, not TCP fault dispatch.
            var wrapper = Assert.Throws<TargetInvocationException>(() => prefix.Invoke(handler, arguments));

            if (missingFirstLevel)
            {
                Assert.IsType<KeyNotFoundException>(wrapper.InnerException);
                Assert.Equal((byte)1, beasts.Level);
                Assert.Equal(13u, beasts.Points);
            }
            else
            {
                Assert.IsType<NullReferenceException>(wrapper.InnerException);
                Assert.Equal((byte)0, beasts.Level);
                Assert.Equal(20u, beasts.Points);
            }
            Assert.Equal((byte)5, beasts.FixedLevel);
            Assert.Equal(missingFirstLevel, beasts.Activated);
            Assert.True(player.SpawinEffect);
            Assert.False(player.SpawinEffectSkills);
            Assert.Equal(flagSentinel, beasts.flag); // Native line774 is after the faulted setter.
            Assert.Equal(11u, player.DeityLandLuckyPoints); // Native775 must not run after the fault.
            Assert.Equal(22u, player.HeroPoints); // Native776 must not run after the fault.
            Assert.Equal(31u, player.ClassExperience); // Native777+ remains ordered after the fault.
            Assert.Equal(32u, player.SavePromote);
            Assert.Equal((byte)33, player.SkinUnlock);
            Assert.Equal((ushort)2002, player.Body); // Native785..786 remain after the Beast fault.
            Assert.Equal((ushort)17, player.Face);
            Assert.Same(faultCache, LegacyNativeBeastsAtrribute.Attributes);
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
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
        Assert.Same(outerCache, LegacyNativeBeastsAtrribute.Attributes);
    }
}
