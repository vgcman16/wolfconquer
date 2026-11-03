using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("player")]
    [InlineData("beast-values")]
    [InlineData("beast-slot")]
    [InlineData("beast-metadata")]
    [InlineData("hairface-slot")]
    [InlineData("catalog-row")]
    [InlineData("catalog-generation")]
    public async Task CapturedPreparationReadinessExpiresOnRealParticipantOrCatalogChanges(string change)
    {
        using var input = new LoginInput();
        var row = SavedCatalogRow();
        using var catalog = new PrefixCatalogScope(row);
        var captured = LegacyCapturedCharacterPrefixTests.Capture(
            LegacyCapturedCharacterPrefixTests.CharacterBytes("HairfaceStorage=1/4201/0/0/\r\n"));
        await WithHandlerAsync(input, async (fixture, handler, _) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var beasts = new LegacyNativeBeastsRuntime(client) { Points = 17, flag = 19, FixedLevel = 3 };
            var storage = new LegacyNativeHairfaceStorageRuntime(client);
            client.Beasts = beasts; client.HairfaceStorage = storage;
            Assert.True(LegacyNativeCapturedCharacterPrefix.TryPrepare(birth.Connection, captured,
                out var prepared, out var reason), reason);
            var value = Assert.IsType<LegacyNativeCapturedCharacterPrefix>(prepared);
            Assert.True(value.IsCurrent());
            switch (change)
            {
                case "player": client.Player.Online = 42; break;
                case "beast-values": beasts.Points = 42; break;
                case "beast-slot": client.Beasts = new(client); break;
                case "beast-metadata": LegacyNativeBeastsAtrribute.Attributes[1].RequiredPoints++; break;
                case "hairface-slot": client.HairfaceStorage = new(client); break;
                case "catalog-row": row.Cost++; break;
                case "catalog-generation": LegacyNativeHairfaceStorageTable.Hairfaces = [SavedCatalogRow()]; break;
            }
            var changedPlayer = ObservePrefixPlayer(client.Player);
            var changedBeasts = client.Beasts; var changedStorage = client.HairfaceStorage;
            Assert.False(value.IsCurrent()); Assert.False(value.IsCurrent());
            Assert.Equal(changedPlayer, ObservePrefixPlayer(client.Player));
            Assert.Same(changedBeasts, client.Beasts); Assert.Same(changedStorage, client.HairfaceStorage);
            Assert.Empty(storage.Objects);
            Assert.Equal(change == "beast-values" ? 42U : 17U, beasts.Points);
            Assert.Equal(19U, beasts.flag); Assert.Equal((byte)3, beasts.FixedLevel);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
        });
    }

    [Fact]
    public async Task ActualTransportCloseCleanupExpiresReadinessAndRefusesNewPreparation()
    {
        using var input = new LoginInput();
        var captured = LegacyCapturedCharacterPrefixTests.Capture(LegacyCapturedCharacterPrefixTests.CharacterBytes());
        await WithHandlerAsync(input, async (fixture, handler, _) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var before = ObservePrefixPlayer(player);
            Assert.False(birth.Connection.HasObservedTransportClose);
            Assert.True(LegacyNativeCapturedCharacterPrefix.TryPrepare(birth.Connection, captured,
                out var prepared, out var reason), reason);
            Assert.True(Assert.IsType<LegacyNativeCapturedCharacterPrefix>(prepared).IsCurrent());
            await fixture.CloseClientTransportAsync(); // Genuine peer half-close and awaited server EOF/owned drain.
            Assert.True(birth.Connection.HasObservedTransportClose);
            Assert.Same(client, birth.Connection.NativeGameClient); Assert.Same(player, client.Player);
            Assert.False(prepared.IsCurrent());
            Assert.False(LegacyNativeCapturedCharacterPrefix.TryPrepare(birth.Connection, captured, out var later, out reason));
            Assert.Null(later); Assert.Equal("capturedNativePrefixOwnerUnavailable", reason);
            Assert.Equal(before, ObservePrefixPlayer(player));
            Assert.Null(client.Beasts); Assert.Null(client.HairfaceStorage);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
        });
    }
}
