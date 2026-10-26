using WolfConquer.Core.World;
using Table = WolfConquer.Core.World.LegacyNativeHairfaceStorageTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("1/4201/0/-1/")]
    [InlineData("1/4201/0/2/1/")]
    [InlineData("1/4201/0/1/256/")]
    [InlineData("1/4201/256/0/")]
    [InlineData("1/4201/0/0/extra/")]
    [InlineData("2/4201/0/0/4201/0/0/")]
    [InlineData("1/9999/0/0/")]
    [InlineData("0/extra/")]
    public async Task RealLoginRejectsIncompleteOrAmbiguousSavedHairfaceBeforeAllPrefixEffects(string saved)
    {
        var previous = Table.Hairfaces;
        try
        {
            Table.Hairfaces = [SavedCatalogRow()];
            using var input = new LoginInput();
            WriteSavedHairface(input, saved);
            var bytes = File.ReadAllBytes(input.FilePath);
            await WithHandlerAsync(input, async (fixture, handler, log) =>
            {
                var (wire, birth) = await ConnectBornAsync(fixture);
                var client = birth.Connection.NativeGameClient!;
                var state = ObservePrefixPlayer(client.Player);
                var storage = new LegacyNativeHairfaceStorageRuntime(client);
                var empty = ReadEmptyState(storage);
                var beasts = new LegacyNativeBeastsRuntime(client) { Points = 17, FixedLevel = 3, flag = 19 };
                client.HairfaceStorage = storage;
                client.Beasts = beasts;

                var dispatch = await SendLoginAsync(fixture, wire);

                Assert.Null(dispatch.Before);
                Assert.Null(dispatch.After);
                Assert.Equal(state, ObservePrefixPlayer(client.Player));
                AssertPreservedHairface(client, storage, client, empty);
                Assert.Same(beasts, client.Beasts);
                Assert.Equal(17U, beasts.Points);
                Assert.Equal(19U, beasts.flag);
                Assert.Equal((byte)3, beasts.FixedLevel);
                Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
                Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
                Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
            });
        }
        finally { Table.Hairfaces = previous; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedDuplicateCatalogIdCannotChooseAnArbitraryType(bool sameType)
    {
        var previous = Table.Hairfaces;
        try
        {
            var first = SavedCatalogRow();
            var duplicate = SavedCatalogRow();
            duplicate.Type = sameType ? first.Type : LegacyHairFaceStorageType.Hairstyle;
            duplicate.Cost = first.Cost + 1;
            Table.Hairfaces = [first, duplicate];
            using var input = new LoginInput();
            WriteSavedHairface(input, "1/4201/0/0/");
            var bytes = File.ReadAllBytes(input.FilePath);
            await WithHandlerAsync(input, async (fixture, handler, log) =>
            {
                var (wire, birth) = await ConnectBornAsync(fixture);
                var client = birth.Connection.NativeGameClient!;
                var before = ObservePrefixPlayer(client.Player);
                Assert.Null((await SendLoginAsync(fixture, wire)).After);
                Assert.Equal(before, ObservePrefixPlayer(client.Player));
                Assert.Null(client.Beasts);
                Assert.Null(client.HairfaceStorage);
                Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
                Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
            });
        }
        finally { Table.Hairfaces = previous; }
    }

    [Theory]
    [InlineData("hairface")]
    [InlineData("beast")]
    [InlineData("forged-public-beast-owner")]
    public async Task RealLoginRefusesForeignConstructorOwnershipBeforeAnyPrefixPublication(string kind)
    {
        using var input = new LoginInput();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var (_, foreignBirth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var foreign = foreignBirth.Connection.NativeGameClient!;
            if (kind == "hairface") client.HairfaceStorage = new(foreign);
            else
            {
                client.Beasts = new(foreign) { Points = 17 };
                if (kind == "forged-public-beast-owner") client.Beasts.Owner = client;
            }
            var before = ObservePrefixPlayer(client.Player);
            var foreignBefore = ObservePrefixPlayer(foreign.Player);
            var storage = client.HairfaceStorage;
            var beasts = client.Beasts;

            Assert.Null((await SendLoginAsync(fixture, wire)).After);

            Assert.Equal(before, ObservePrefixPlayer(client.Player));
            Assert.Equal(foreignBefore, ObservePrefixPlayer(foreign.Player));
            Assert.Same(storage, client.HairfaceStorage);
            Assert.Same(beasts, client.Beasts);
            Assert.Null(foreign.HairfaceStorage);
            Assert.Null(foreign.Beasts);
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }
}
