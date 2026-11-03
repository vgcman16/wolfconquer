using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Reuse the actual connected native/catalog fixture; no substitute owner or publisher.
public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturedSqlDataPreparesRealNativeParticipantsWithoutPublishingOrReadingAShadowFile(bool existingFile)
    {
        using var input = new LoginInput(create: existingFile);
        using var catalog = new PrefixCatalogScope(SavedCatalogRow());
        var source = LegacyCapturedCharacterPrefixTests.CharacterBytes("RelicSpirit=True\r\n" +
            "TailedBeastActivated=True\r\nTailedBeastTotalPoints=2000\r\nTailedBeastFlag=19\r\n" +
            "HairfaceStorage=1/4201/6/2/1/0/\r\n");
        source = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(source).Replace("Online=1", "Online=7", StringComparison.Ordinal));
        var captured = LegacyCapturedCharacterPrefixTests.Capture(source);
        var files = Directory.GetFiles(input.Users);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            client.Player.Online = 9;
            var before = ObservePrefixPlayer(client.Player);
            var flags = client.Player.BitVector; var random = client.Player.MyRandom;
            Assert.True(LegacyNativeCapturedCharacterPrefix.TryPrepare(birth.Connection, captured,
                out var prepared, out var reason), reason);
            var value = Assert.IsType<LegacyNativeCapturedCharacterPrefix>(prepared);
            Assert.True(value.IsCurrent());
            Assert.Same(captured.Image, value.Image);
            Assert.Equal(501L, value.Image.AccountId);
            Assert.Equal(9001U, value.PreparedPlayerValues.Uid);
            Assert.NotEqual(Uid, value.PreparedPlayerValues.Uid); // Contradictory owned file is not a fallback source.
            Assert.Equal(7U, captured.SavedPlayerValues.Online);
            Assert.Equal(10U, value.PreparedPlayerValues.Online); // Actual before9 +1, never saved7/marker1.
            Assert.Equal(1L, value.PreparedSnapshot.Online);
            Assert.Equal("1", value.PreparedSnapshot.Attributes["Online"]);
            Assert.True(value.PreparedPlayerValues.RelicSpirit);
            Assert.Equal(source, value.Image.CopyCharacterBytes());
            Assert.Equal(captured.CopyOnlineCharacterBytes(), value.CopyOnlineCharacterBytes());
            Assert.IsType<Dictionary<string, string>>(value.PreparedSnapshot.Attributes)["Name"] = "foreign";
            var copy = value.CopyOnlineCharacterBytes(); copy.AsSpan().Fill(0);
            Assert.Equal("SqlStateSynthetic", value.PreparedSnapshot.Name);
            Assert.Equal("SqlStateSynthetic", value.PreparedSnapshot.Attributes["Name"]);
            Assert.Equal(before, ObservePrefixPlayer(client.Player));
            Assert.Same(flags, client.Player.BitVector); Assert.Same(random, client.Player.MyRandom);
            Assert.Null(client.Beasts); Assert.Null(client.HairfaceStorage); Assert.False(client.FullLoading);
            Assert.Null(client.OrdinarySpellBinding);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
            Assert.True(value.IsCurrent());
        });
        Assert.Equal(files, Directory.GetFiles(input.Users));
        if (existingFile) Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
        else Assert.Empty(Directory.GetFiles(input.Users));
    }

    [Fact]
    public async Task CapturedHairfaceUsesTheFullStrictSavedLineBeyondTheOldWin32Buffer()
    {
        using var input = new LoginInput();
        using var catalog = new PrefixCatalogScope(SavedCatalogRow());
        var saved = "1/4201/0/200/" + string.Join('/', Enumerable.Repeat("0", 200)) + "/";
        Assert.True(saved.Length > 254);
        var captured = LegacyCapturedCharacterPrefixTests.Capture(
            LegacyCapturedCharacterPrefixTests.CharacterBytes("HairfaceStorage=" + saved + "\r\n"));
        await WithHandlerAsync(input, async (fixture, handler, _) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            Assert.True(LegacyNativeCapturedCharacterPrefix.TryPrepare(birth.Connection, captured,
                out var prepared, out var reason), reason);
            Assert.True(Assert.IsType<LegacyNativeCapturedCharacterPrefix>(prepared).IsCurrent());
            Assert.Equal(saved, captured.HairfaceLine);
            Assert.Equal(0U, birth.Connection.NativeGameClient!.Player.UID);
            Assert.Null(birth.Connection.NativeGameClient.HairfaceStorage);
            Assert.Null(birth.Connection.NativeGameClient.Beasts);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
        });
    }
}
