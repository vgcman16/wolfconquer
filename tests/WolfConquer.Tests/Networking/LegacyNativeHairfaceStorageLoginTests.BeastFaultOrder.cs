using System.Collections.Generic;
using System.Reflection;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Fact]
    public async Task ReflectionPrefixEmptyBeastCacheRefusesBeforeAnyNativeOrSavedPublication()
    {
        await AssertBeastMetadataRefusalAsync(missingFirstLevel: false);
    }

    [Fact]
    public async Task ReflectionPrefixMissingFirstBeastLevelRefusesBeforeAnyNativeOrSavedPublication()
    {
        await AssertBeastMetadataRefusalAsync(missingFirstLevel: true);
    }

    private static async Task AssertBeastMetadataRefusalAsync(bool missingFirstLevel)
    {
        using var input = new LoginInput();
        var bytes = WritePrefixFields(input,
            "HairfaceStorage=0/\r\nTailedBeastActivated=True\r\nTailedBeastTotalPoints=7\r\nTailedBeastFlag=123\r\n" +
            "SpawinEffect=True\r\nSpawinEffectSkills=False\r\nDeityLandLuckyPoints=1234\r\nHeroPoints=5678\r\n" +
            "ClassExperience=56789\r\nSavePromote=54321\r\nSkinUnlock=23\r\n");
        var outerCache = LegacyNativeBeastsAtrribute.Attributes;
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            player.Online = 9;
            player.SpawinEffectSkills = true;
            player.DeityLandLuckyPoints = 11;
            player.HeroPoints = 22;
            player.ClassExperience = 31;
            player.SavePromote = 32;
            player.SkinUnlock = 33;
            player.Body = 2002;
            player.Face = 17;
            var before = ObservePrefixPlayer(player);
            var beasts = new LegacyNativeBeastsRuntime(client)
            {
                Activated = !missingFirstLevel, Points = 13, flag = 0xA5A5A5A5, Level = 0, FixedLevel = 5,
            };
            var storage = new LegacyNativeHairfaceStorageRuntime(client);
            var storageState = ReadEmptyState(storage);
            client.Beasts = beasts;
            client.HairfaceStorage = storage;
            var snapshot = ReadPrefixSnapshot(input);
            var faultCache = new Dictionary<byte, LegacyNativeBeastsAtrribute.Attribute>();
            if (missingFirstLevel) faultCache.Add(2, new() { Level = 2, RequiredPoints = 1000 });
            LegacyNativeBeastsAtrribute.Attributes = faultCache;
            var prefix = typeof(WorldServerFrameHandler).GetMethod("TryApplyLegacyExistingCharacterLocalPrefix",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!;
            object?[] arguments = [birth.Connection, snapshot, null, null];

            Assert.False((bool)prefix.Invoke(handler, arguments)!);

            Assert.Same(snapshot, arguments[2]);
            Assert.Equal(before, ObservePrefixPlayer(player));
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Same(beasts, client.Beasts);
            Assert.Same(client, beasts.Owner);
            Assert.Equal(!missingFirstLevel, beasts.Activated);
            Assert.Equal(13U, beasts.Points);
            Assert.Equal(0xA5A5A5A5U, beasts.flag);
            Assert.Equal((byte)0, beasts.Level);
            Assert.Equal((byte)5, beasts.FixedLevel);
            AssertPreservedHairface(client, storage, client, storageState);
            Assert.Same(faultCache, LegacyNativeBeastsAtrribute.Attributes);
            Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
            Assert.Equal(7L, ReadPrefixSnapshot(input).Online);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
        Assert.Same(outerCache, LegacyNativeBeastsAtrribute.Attributes);
    }
}
