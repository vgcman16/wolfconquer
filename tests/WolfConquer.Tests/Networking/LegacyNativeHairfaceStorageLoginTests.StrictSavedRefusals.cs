using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    private static async Task AssertStrictSavedLoginRefusalAsync(LoginInput input, byte[] beforeBytes,
        string expectedReason = "normalPrefixOptionalValueInvalid")
    {
        // Otherwise complete managed data genuinely reaches the native prefix's strict scalar read.
        var loaded = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
        Assert.True(loaded.Succeeded, loaded.Reason); Assert.NotNull(loaded.Snapshot);
        Assert.Equal(beforeBytes, File.ReadAllBytes(input.FilePath));
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!; var player = client.Player;
            player.Body = 2002; player.Face = 17; player.TransformationID = 7;
            player.DeityLandLuckyPoints = 711; player.HeroPoints = 812;
            player.SpawinEffect = true; player.SpawinEffectSkills = false;
            var before = LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player);
            var flags = player.BitVector; var random = player.MyRandom;
            var view = player.View; var caches = view.src; var actions = view.MobActions;
            var mesh = player.Mesh;
            var beasts = new LegacyNativeBeastsRuntime(client)
            { Activated = true, Points = 17, FixedLevel = 3, flag = 0x40000008U };
            client.Beasts = beasts;
            var beastLevel = beasts.Level;
            var storage = new LegacyNativeHairfaceStorageRuntime(client);
            var storageState = ReadEmptyState(storage); client.HairfaceStorage = storage;

            // This is the genuine encrypted client frame through the registered World handler.
            var rejected = await SendLoginAsync(fixture, wire);

            Assert.Null(rejected.Before); Assert.Null(rejected.After);
            Assert.Same(client, birth.Connection.NativeGameClient); Assert.Same(player, client.Player); Assert.Same(client, player.Owner);
            Assert.Equal(before, LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player));
            Assert.Equal((ushort)7, player.TransformationID); Assert.Equal(mesh, player.Mesh);
            Assert.Same(flags, player.BitVector); Assert.Same(random, player.MyRandom);
            Assert.Same(view, player.View); Assert.Same(client, view.Owner); Assert.Same(caches, view.src); Assert.Same(actions, view.MobActions);
            Assert.Same(beasts, client.Beasts); Assert.Same(client, beasts.Owner);
            Assert.True(beasts.Activated); Assert.Equal(17U, beasts.Points); Assert.Equal(0x40000008U, beasts.flag);
            Assert.Equal((byte)3, beasts.FixedLevel); Assert.Equal(beastLevel, beasts.Level);
            AssertPreservedHairface(client, storage, client, storageState);
            Assert.Equal(beforeBytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(client.OrdinarySpellBinding); Assert.False(client.FullLoading); Assert.Null(client.Socket); Assert.Null(client.Map);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.False(string.IsNullOrWhiteSpace(expectedReason));
            Assert.Contains(expectedReason, log.Text);
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }
}
