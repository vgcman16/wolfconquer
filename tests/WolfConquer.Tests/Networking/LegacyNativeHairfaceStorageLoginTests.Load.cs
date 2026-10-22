using System.Text;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.World.LegacyNativeHairfaceStorageTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Fact]
    public async Task AcceptedLoginReadsSavedHairfaceIntoTheSameForeignOwnedStorage()
    {
        var previous = Table.Hairfaces;
        try
        {
            var catalog = SavedCatalogRow();
            Table.Hairfaces = [catalog];
            using var input = new LoginInput();
            WriteSavedHairface(input, "1/4201/6/2/1/0/");
            await WithHandlerAsync(input, async (fixture, handler, log) =>
            {
                var (wire, birth) = await ConnectBornAsync(fixture);
                var client = birth.Connection.NativeGameClient!;
                var (_, foreignBirth) = await ConnectBornAsync(fixture);
                var foreignOwner = foreignBirth.Connection.NativeGameClient!;
                var existing = new LegacyNativeHairfaceStorageRuntime(foreignOwner);
                var before = existing.Objects;
                client.HairfaceStorage = existing;

                var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);

                Assert.Same(client, accepted.NativeGameClient);
                Assert.Same(existing, client.HairfaceStorage);
                Assert.Same(foreignOwner, ReadPrivateField(existing, "Owner"));
                Assert.NotSame(before, existing.Objects);
                var loaded = Assert.Single(existing.Objects);
                Assert.NotSame(catalog, loaded);
                Assert.Equal(4201u, loaded.ID);
                Assert.Equal(LegacyHairFaceStorageType.Avatar, loaded.Type);
                Assert.Equal((byte)6, loaded.EquippedColor);
                Assert.Equal(new byte[] { 1, 0 }, loaded.Colors);
                Assert.Same(catalog.Classes, loaded.Classes);
                Assert.Equal("1/4201/6/2/1/0/", existing.ToString());
                Assert.Null(foreignOwner.HairfaceStorage);
                Assert.Equal("1/4201/6/2/1/0/", accepted.Character.Attributes["HairfaceStorage"]);
                Assert.Equal(1L, accepted.Character.Online);
                Assert.Contains("legacy MsgLoginGame Success", log.Text);
            });
        }
        finally
        {
            Table.Hairfaces = previous;
        }
    }

    [Fact]
    public async Task AcceptedLoginUsesNativeDefaultIniBufferNotTheUntruncatedSnapshotAttribute()
    {
        var previous = Table.Hairfaces;
        try
        {
            Table.Hairfaces = [SavedCatalogRow()];
            using var input = new LoginInput();
            var saved = "2/99/0/130/" + string.Concat(Enumerable.Repeat("0/", 130)) + "4201/0/1/1/";
            Assert.True(saved.Length > 254);
            WriteSavedHairface(input, saved);
            await WithHandlerAsync(input, async (fixture, handler, log) =>
            {
                var (wire, birth) = await ConnectBornAsync(fixture);

                var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);

                var storage = Assert.IsType<LegacyNativeHairfaceStorageRuntime>(
                    birth.Connection.NativeGameClient!.HairfaceStorage);
                Assert.Empty(storage.Objects);
                Assert.Equal("0/", storage.ToString());
                Assert.Equal(saved, accepted.Character.Attributes["HairfaceStorage"]);
                Assert.Contains("legacy MsgLoginGame Success", log.Text);
            });
        }
        finally
        {
            Table.Hairfaces = previous;
        }
    }

    private static Table.Hairface SavedCatalogRow() => new()
    {
        ID = 4201,
        Type = LegacyHairFaceStorageType.Avatar,
        Name = "Native catalog face",
        Classes = [7],
        Colors = [1, 0, 0, 0, 0, 0, 0],
    };

    private static void WriteSavedHairface(LoginInput input, string value)
    {
        var text = Encoding.UTF8.GetString(input.InitialBytes).Replace("RelicSpirit=True\r\n",
            "RelicSpirit=True\r\nHairfaceStorage=" + value + "\r\n", StringComparison.Ordinal);
        File.WriteAllBytes(input.FilePath, Encoding.UTF8.GetBytes(text));
    }
}
