using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EncryptedLoginLoadsCompleteNameAndSpouseWithoutNormalizingTheRawSnapshot(bool maximumLength)
    {
        using var input = new LoginInput();
        var rawName = maximumLength ? new string('N', 254) : "Native Name";
        var rawSpouse = maximumLength ? new string('S', 254) : null;
        var expectedName = rawName;
        var expectedSpouse = rawSpouse ?? "None";
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

    [Theory]
    [InlineData(255, 12)]
    [InlineData(300, 12)]
    [InlineData(11, 255)]
    [InlineData(11, 300)]
    [InlineData(300, 300)]
    public async Task EncryptedLoginRefusesTruncatedNameOrSpouseWithoutNormalizingOrPublishing(
        int nameLength, int spouseLength)
    {
        using var input = new LoginInput();
        var rawName = new string('N', nameLength);
        var rawSpouse = new string('S', spouseLength);
        var text = Encoding.UTF8.GetString(input.InitialBytes)
            .Replace("Name=HairfaceOwner\r\n", "Name=" + rawName + "\r\nSpouse=" + rawSpouse + "\r\n", StringComparison.Ordinal);
        var before = Encoding.UTF8.GetBytes(text);
        File.WriteAllBytes(input.FilePath, before); // Owned GUID fixture only.

        // The genuine Win32 read accepts at most 254 characters with its 255-character buffer.
        // Check Name and Spouse separately, retaining the original refusal for both 300-character values.
        await AssertStrictSavedLoginRefusalAsync(input, before, "normalPrefixFieldTruncated");
    }
}
