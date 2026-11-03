using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("hairface-shape")]
    [InlineData("hairface-catalog")]
    [InlineData("beast-level")]
    [InlineData("missing-beast-metadata")]
    [InlineData("foreign-beasts")]
    [InlineData("forged-beast-owner")]
    [InlineData("foreign-hairface")]
    [InlineData("online-overflow")]
    [InlineData("missing-capture")]
    public async Task CapturedNativeRefusalLeavesEveryActualOwnerAndSavedImageUntouched(string failure)
    {
        using var input = new LoginInput();
        using var catalog = new PrefixCatalogScope(SavedCatalogRow());
        string fields = failure switch
        {
            "hairface-shape" => "HairfaceStorage=1/4201/0/0/hidden\r\n",
            "hairface-catalog" => "HairfaceStorage=1/9999/0/0/\r\n",
            "beast-level" => "TailedBeastTotalPoints=4294967295\r\n",
            _ => ""
        };
        var captured = LegacyCapturedCharacterPrefixTests.Capture(LegacyCapturedCharacterPrefixTests.CharacterBytes(fields));
        var source = captured.Image.CopyCharacterBytes();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var (_, foreignBirth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!; var foreign = foreignBirth.Connection.NativeGameClient!;
            if (failure is "foreign-beasts" or "forged-beast-owner")
            {
                client.Beasts = new(foreign) { Points = 17, flag = 19 };
                if (failure == "forged-beast-owner") client.Beasts.Owner = client;
            }
            if (failure == "foreign-hairface") client.HairfaceStorage = new(foreign);
            if (failure == "online-overflow") client.Player.Online = uint.MaxValue;
            if (failure == "missing-beast-metadata") LegacyNativeBeastsAtrribute.Attributes.Clear();
            var before = ObservePrefixPlayer(client.Player); var foreignBefore = ObservePrefixPlayer(foreign.Player);
            var beasts = client.Beasts; var hairface = client.HairfaceStorage;
            Assert.False(LegacyNativeCapturedCharacterPrefix.TryPrepare(birth.Connection,
                failure == "missing-capture" ? null : captured, out var prepared, out var reason));
            Assert.Null(prepared);
            var expected = failure switch
            {
                "hairface-shape" or "hairface-catalog" => "capturedNativePrefixHairfaceUnavailable",
                "beast-level" or "missing-beast-metadata" => "capturedNativePrefixBeastsUnavailable",
                "foreign-beasts" or "forged-beast-owner" or "foreign-hairface" => "capturedNativePrefixInvalidData",
                "online-overflow" => "capturedNativePrefixOnlineOverflow",
                _ => "capturedNativePrefixImageMissing"
            };
            Assert.Equal(expected, reason);
            Assert.Equal(before, ObservePrefixPlayer(client.Player)); Assert.Equal(foreignBefore, ObservePrefixPlayer(foreign.Player));
            Assert.Same(beasts, client.Beasts); Assert.Same(hairface, client.HairfaceStorage);
            if (beasts is not null) { Assert.Equal(17U, beasts.Points); Assert.Equal(19U, beasts.flag); }
            Assert.Null(foreign.Beasts); Assert.Null(foreign.HairfaceStorage);
            Assert.Equal(source, captured.Image.CopyCharacterBytes());
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(foreignBirth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }
}
