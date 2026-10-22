using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EncryptedLoginLoadsNativeNameAndSpouseWithoutNormalizingTheRawSnapshot(bool longValues)
    {
        using var input = new LoginInput();
        var rawName = longValues ? new string('N', 300) : "Native Name";
        var rawSpouse = longValues ? new string('S', 300) : null;
        var expectedName = longValues ? new string('N', 254) : rawName;
        var expectedSpouse = longValues ? new string('S', 254) : "None";
        var text = Encoding.UTF8.GetString(input.InitialBytes)
            .Replace("Name=HairfaceOwner\r\n", "Name=" + rawName + "\r\n" +
                (rawSpouse is null ? "" : "Spouse=" + rawSpouse + "\r\n"), StringComparison.Ordinal);
        var expectedBytes = Encoding.UTF8.GetBytes(text.Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
        File.WriteAllBytes(input.FilePath, Encoding.UTF8.GetBytes(text)); // Owned GUID fixture only.

        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var view = player.View;
            Assert.Equal("", player.Name);
            Assert.Equal("None", player.Spouse);
            player.Name = "Previous Name";
            player.Spouse = "Previous Spouse";

            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);

            Assert.Same(player, client.Player);
            Assert.Same(player, accepted.NativePlayer);
            Assert.Same(view, player.View);
            Assert.Same(client, player.Owner);
            Assert.Equal(expectedName, player.Name);
            Assert.Equal(expectedSpouse, player.Spouse);
            Assert.Equal(rawName, accepted.Character.Name);
            Assert.Equal(rawName, accepted.Character.Attributes["Name"]);
            if (rawSpouse is null) Assert.False(accepted.Character.Attributes.ContainsKey("Spouse"));
            else Assert.Equal(rawSpouse, accepted.Character.Attributes["Spouse"]);
            var nativeHero = LegacyProtoHeroInfoPacket.FromSnapshot(accepted.Character, nativePlayer: player);
            var fallbackHero = LegacyProtoHeroInfoPacket.FromSnapshot(accepted.Character);
            var nativeLoader = LegacyLoaderHeroInfoPacket.FromSnapshot(accepted.Character, nativePlayer: player);
            var fallbackLoader = LegacyLoaderHeroInfoPacket.FromSnapshot(accepted.Character);
            Assert.Equal(expectedName, nativeHero.GetStringField(46));
            Assert.Equal(expectedSpouse, nativeHero.GetStringField(47));
            Assert.Equal(rawName, fallbackHero.GetStringField(46));
            Assert.Equal(rawSpouse ?? "None", fallbackHero.GetStringField(47));
            Assert.Equal(expectedName, nativeLoader.Name);
            Assert.Equal(rawName, fallbackLoader.Name);
            Assert.False(client.FullLoading);
            Assert.Null(client.Map);
            Assert.Null(client.Socket);
            Assert.Equal(expectedBytes, File.ReadAllBytes(input.FilePath));
            Assert.Contains("legacy MsgLoginGame Success", log.Text);
        });
    }
}
