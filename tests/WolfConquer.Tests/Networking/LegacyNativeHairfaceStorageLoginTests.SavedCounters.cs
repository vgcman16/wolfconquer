using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("12345", "4294967295", 12345U, uint.MaxValue)]
    [InlineData(null, null, 0U, 0U)]
    [InlineData("-1", "4294967296", 0U, 0U)]
    public async Task SavedDeityAndHeroCountersOverwriteTheSamePhysicalPlayerWithNativeUInt32Reads(
        string? deity, string? hero, uint expectedDeity, uint expectedHero)
    {
        using var input = new LoginInput();
        var original = WritePostHairfaceFlagValues(input, "False", "True", "False");
        var inserted = new StringBuilder("HairfaceStorage=0/\r\n");
        if (deity is not null) inserted.Append("DeityLandLuckyPoints=").Append(deity).Append("\r\n");
        if (hero is not null) inserted.Append("HeroPoints=").Append(hero).Append("\r\n");
        var text = Encoding.UTF8.GetString(original).Replace(
            "HairfaceStorage=0/\r\n", inserted.ToString(), StringComparison.Ordinal);
        var fixtureBytes = Encoding.UTF8.GetBytes(text);
        var expectedMarker = Encoding.UTF8.GetBytes(text.Replace(
            "Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
        File.WriteAllBytes(input.FilePath, fixtureBytes); // This LoginInput owns the temporary file.

        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            player.DeityLandLuckyPoints = 711;
            player.HeroPoints = 812;
            player.SpawinEffect = true;
            player.SpawinEffectSkills = false;
            var (_, foreignBirth) = await ConnectBornAsync(fixture);
            var foreignOwner = foreignBirth.Connection.NativeGameClient!;
            var foreignPlayer = foreignOwner.Player;
            foreignPlayer.DeityLandLuckyPoints = 913;
            foreignPlayer.HeroPoints = 1014;
            var beasts = new LegacyNativeBeastsRuntime(foreignOwner)
            {
                Activated = false, Points = 17, FixedLevel = 3, flag = 0,
            };
            client.Beasts = beasts;
            var storage = new LegacyNativeHairfaceStorageRuntime(foreignOwner);
            var storageState = ReadEmptyState(storage);
            client.HairfaceStorage = storage;
            Assert.Equal(fixtureBytes, File.ReadAllBytes(input.FilePath));

            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);

            Assert.Same(client, accepted.NativeGameClient);
            Assert.Same(player, accepted.NativePlayer);
            Assert.Same(player, client.Player);
            Assert.Same(client, player.Owner);
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Equal(expectedDeity, player.DeityLandLuckyPoints);
            Assert.Equal(expectedHero, player.HeroPoints);
            Assert.Equal(Uid, player.UID);
            Assert.Equal(1U, player.Online);
            Assert.True(player.RelicSpirit);
            Assert.True(player.SpawinEffect);
            Assert.False(player.SpawinEffectSkills);
            Assert.Same(beasts, client.Beasts);
            Assert.Same(foreignOwner, beasts.Owner);
            Assert.False(beasts.Activated);
            Assert.Equal(17U, beasts.Points);
            Assert.Equal((byte)3, beasts.FixedLevel);
            Assert.Equal((byte)0, beasts.Level);
            Assert.Equal(0U, beasts.flag);
            AssertPreservedHairface(client, storage, foreignOwner, storageState);
            Assert.Same(foreignPlayer, foreignOwner.Player);
            Assert.Equal(913U, foreignPlayer.DeityLandLuckyPoints);
            Assert.Equal(1014U, foreignPlayer.HeroPoints);
            Assert.Equal(0U, foreignPlayer.UID);
            Assert.Equal(0U, foreignPlayer.Online);
            Assert.Null(foreignOwner.Beasts);
            Assert.Null(foreignOwner.HairfaceStorage);
            AssertSavedFlagText(accepted, "DeityLandLuckyPoints", deity);
            AssertSavedFlagText(accepted, "HeroPoints", hero);
            Assert.Equal(1L, accepted.Character.Online);
            Assert.Equal(expectedMarker, File.ReadAllBytes(input.FilePath));
            Assert.Contains("legacy MsgLoginGame Success", log.Text);
        });
    }
}
