using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("65535", "65535", 65535, 65535)]
    [InlineData("000010080000", "000001540000", 1008, 154)]
    public async Task EncryptedLoginLoadsBodyAndFaceFromTheBoundedNativeReader(
        string rawBody, string rawFace, ushort expectedBody, ushort expectedFace)
    {
        using var input = new LoginInput();
        var text = Encoding.UTF8.GetString(input.InitialBytes)
            .Replace("Body=1008\r\n", "Body=" + rawBody + "\r\n", StringComparison.Ordinal)
            .Replace("Face=154\r\n", "Face=" + rawFace + "\r\n", StringComparison.Ordinal);
        var expectedBytes = Encoding.UTF8.GetBytes(text.Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
        File.WriteAllBytes(input.FilePath, Encoding.UTF8.GetBytes(text)); // Owned GUID fixture only.

        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            var view = player.View;
            var caches = view.src;
            var actions = view.MobActions;
            player.Body = 2002;
            player.Face = 17;
            player.TransformationID = 7; // Retained state, not loaded from snapshot attributes.

            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);

            Assert.Same(client, accepted.NativeGameClient);
            Assert.Same(player, accepted.NativePlayer);
            Assert.Same(player, client.Player);
            Assert.Same(client, player.Owner);
            Assert.Equal(expectedBody, player.Body);
            Assert.Equal(expectedFace, player.Face);
            Assert.Equal((ushort)7, player.TransformationID);
            Assert.Equal(unchecked((uint)(7 * 10_000_000 + expectedFace * 10_000 + expectedBody)), player.Mesh);
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Same(view, player.View);
            Assert.Same(client, view.Owner);
            Assert.Same(caches, view.src);
            Assert.Same(actions, view.MobActions);
            Assert.False(client.FullLoading);
            Assert.Null(client.Socket);
            Assert.Null(client.Map); // No map publication/transport binding inferred from login.
            Assert.Equal(rawBody, accepted.Character.Attributes["Body"]);
            Assert.Equal(rawFace, accepted.Character.Attributes["Face"]);
            Assert.Equal(long.Parse(rawBody), accepted.Character.Body);
            Assert.Equal(long.Parse(rawFace), accepted.Character.Face);
            var hero = LegacyProtoHeroInfoPacket.FromSnapshot(accepted.Character, nativePlayer: player);
            Assert.Equal((ulong)player.Mesh, hero.GetNumericField(3));
            Assert.Equal(expectedBytes, File.ReadAllBytes(input.FilePath));
            Assert.True(log.Text.Contains("legacy MsgLoginGame Success", StringComparison.Ordinal), log.Text);
        });
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("not-body", "not-face")]
    [InlineData("-1", "65536")]
    public async Task ReflectionPrefixRejectsAStaleBodyFaceSnapshotBeforeNativeOrSavedChanges(
        string? rawBody, string? rawFace)
    {
        using var input = new LoginInput();
        // A previously read valid snapshot cannot authorize loading a different
        // file, even where the native INI reader has numeric fallback defaults.
        var snapshot = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid).Snapshot!;
        var text = Encoding.UTF8.GetString(input.InitialBytes)
            .Replace("Body=1008\r\n", rawBody is null ? "" : "Body=" + rawBody + "\r\n", StringComparison.Ordinal)
            .Replace("Face=154\r\n", rawFace is null ? "" : "Face=" + rawFace + "\r\n", StringComparison.Ordinal);
        var expectedBytes = Encoding.UTF8.GetBytes(text);
        File.WriteAllBytes(input.FilePath, expectedBytes);

        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var view = player.View;
            player.Body = 2002;
            player.Face = 17;
            var prefix = typeof(WorldServerFrameHandler).GetMethod("TryApplyLegacyExistingCharacterLocalPrefix",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!;
            object?[] arguments = [birth.Connection, snapshot, null, null];

            Assert.False((bool)prefix.Invoke(handler, arguments)!);

            Assert.Equal((ushort)2002, player.Body);
            Assert.Equal((ushort)17, player.Face);
            Assert.Equal(172002u, player.Mesh);
            Assert.Same(player, client.Player);
            Assert.Same(view, player.View);
            Assert.False(client.FullLoading);
            Assert.Null(client.Map);
            Assert.Null(client.Socket);
            Assert.Null(client.Beasts);
            Assert.Null(client.HairfaceStorage);
            Assert.Equal(0U, player.UID);
            Assert.Equal(0U, player.Online);
            var updated = Assert.IsType<CharacterSnapshot>(arguments[2]);
            Assert.Same(snapshot, updated);
            Assert.Equal(1008L, updated.Body);
            Assert.Equal(154L, updated.Face);
            Assert.Equal("1008", updated.Attributes["Body"]);
            Assert.Equal("154", updated.Attributes["Face"]);
            Assert.Equal(expectedBytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }
}
