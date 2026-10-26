using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Fact]
    public async Task PrefixPreparationIsReadOnlyAndSingleCommitPublishesThePreparedAbsoluteState()
    {
        using var input = new LoginInput();
        var beforeBytes = WritePrefixFields(input,
            "ClassExperience=56789\r\nTailedBeastActivated=True\r\nTailedBeastTotalPoints=1234\r\nTailedBeastFlag=2147483665\r\n");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var before = ObservePrefixPlayer(client.Player);
            var snapshot = ReadPrefixSnapshot(input);

            Assert.True(LegacyNativeExistingCharacterPrefix.TryPrepare(birth.Connection, snapshot, input.Users,
                out var prepared, out var reason), reason);
            Assert.NotNull(prepared);
            Assert.Equal(before, ObservePrefixPlayer(client.Player));
            Assert.Null(client.HairfaceStorage);
            Assert.Null(client.Beasts);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.Equal(beforeBytes, File.ReadAllBytes(input.FilePath));

            Assert.True(prepared.TryCommit(out var updated, out reason), reason);

            Assert.Equal(Uid, client.Player.UID);
            Assert.Equal(1U, client.Player.Online);
            Assert.Equal(56789U, client.Player.ClassExperience);
            Assert.Equal(1L, updated.Online);
            Assert.Equal("1", updated.Attributes["Online"]);
            var beasts = Assert.IsType<LegacyNativeBeastsRuntime>(client.Beasts);
            Assert.Same(client, beasts.Owner);
            Assert.True(beasts.Activated);
            Assert.Equal(234U, beasts.Points);
            Assert.Equal((byte)1, beasts.Level);
            Assert.Equal((byte)2, beasts.FixedLevel);
            Assert.Equal(0x80000011U, beasts.flag);
            AssertNewHairface(client);
            var committed = File.ReadAllBytes(input.FilePath);
            Assert.Equal(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(beforeBytes)
                .Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal)), committed);
            var after = ObservePrefixPlayer(client.Player);
            Assert.False(prepared.TryCommit(out var rejected, out _));
            Assert.Same(snapshot, rejected);
            Assert.Equal(after, ObservePrefixPlayer(client.Player));
            Assert.Equal(committed, File.ReadAllBytes(input.FilePath));
            Assert.Null(client.MyNinja);
            Assert.Null(client.Equipment);
            Assert.False(client.FullLoading);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }

    [Fact]
    public async Task RealLoginPreservesNativeQuotedBooleanParsing()
    {
        using var input = new LoginInput();
        var bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(input.InitialBytes)
            .Replace("RelicSpirit=True\r\n", "RelicSpirit=\"True\"\r\n", StringComparison.Ordinal));
        File.WriteAllBytes(input.FilePath, bytes);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.True(birth.Connection.NativeGameClient!.Player.RelicSpirit);
            Assert.Equal("\"True\"", accepted.Character.Attributes["RelicSpirit"]);
            Assert.Equal(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes)
                .Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal)), File.ReadAllBytes(input.FilePath));
            Assert.Contains("legacy MsgLoginGame Success", log.Text);
        });
    }

    [Fact]
    public async Task CompleteEmptySavedLoadReplacesObsoleteHairfaceRowsWithoutCatalogAliasing()
    {
        using var catalog = new PrefixCatalogScope(SavedCatalogRow());
        using var input = new LoginInput();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var storage = new LegacyNativeHairfaceStorageRuntime(client);
            storage.Load("1/4201/6/2/1/0/");
            var oldObjects = storage.Objects;
            Assert.Single(oldObjects);
            client.HairfaceStorage = storage;

            Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);

            Assert.Same(storage, client.HairfaceStorage);
            Assert.Same(client, ReadPrivateField(storage, "Owner"));
            Assert.NotSame(oldObjects, storage.Objects);
            Assert.Empty(storage.Objects);
            Assert.Equal("0/", storage.ToString());
            Assert.Equal(4201U, oldObjects[0].ID);
            Assert.Equal(new byte[] { 1, 0 }, oldObjects[0].Colors);
            AssertMarkedFile(input);
        });
    }
}
