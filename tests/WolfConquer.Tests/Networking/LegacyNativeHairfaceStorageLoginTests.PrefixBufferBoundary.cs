using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactNativeHairfaceBoundaryAcceptsCompleteDataAndRejectsAHiddenTail(bool extraTail)
    {
        var row = SavedCatalogRow();
        row.ID = 42010;
        using var catalog = new PrefixCatalogScope(row);
        using var input = new LoginInput();
        var complete = "1/42010/0/120/" + string.Concat(Enumerable.Repeat("0/", 120));
        Assert.Equal(254, complete.Length);
        WriteSavedHairface(input, complete + (extraTail ? "hidden-tail" : ""));
        var bytes = File.ReadAllBytes(input.FilePath);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var before = ObservePrefixPlayer(client.Player);
            var dispatch = await SendLoginAsync(fixture, wire);
            if (extraTail)
            {
                Assert.Null(dispatch.Before);
                Assert.Null(dispatch.After);
                Assert.Equal(before, ObservePrefixPlayer(client.Player));
                Assert.Null(client.HairfaceStorage);
                Assert.Null(client.Beasts);
                Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
                Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
                Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
            }
            else
            {
                Assert.IsType<WorldSession>(dispatch.After);
                var loaded = Assert.Single(client.HairfaceStorage!.Objects);
                Assert.Equal(42010U, loaded.ID);
                Assert.Equal(new byte[120], loaded.Colors);
                Assert.Equal(complete, client.HairfaceStorage.ToString());
                Assert.Equal(1U, client.Player.Online);
                Assert.Equal(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes)
                    .Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal)), File.ReadAllBytes(input.FilePath));
                Assert.Contains("legacy MsgLoginGame Success", log.Text);
            }
        });
    }
}
