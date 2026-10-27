using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData(256)]
    [InlineData(65535)]
    public void RetainedAdmissionJournalImageKeepsItsEstablishedPhysicalUshortLevelDomain(int level)
    {
        using var input = new LoginInput();
        var image = WriteClassLevelInput(input, 7051, level);
        // This real journal/replay validator must not inherit the fresh
        // startup codec's narrower byte field or reinterpret retained images.
        LegacyTradeJournal.ValidateOrdinaryAdmissionCharacterImage(image, Uid);
        Assert.Equal(image, File.ReadAllBytes(input.FilePath));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public async Task ColdPrefixRefusesUnrepresentablePhysicalLevelBeforeAnyPublication(int level)
    {
        using var input = new LoginInput();
        var bytes = WriteClassLevelInput(input, 7051, level);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var player = birth.Connection.NativeGameClient!.Player;
            var snapshot = ReadPrefixSnapshot(input);
            Assert.False(LegacyNativeExistingCharacterPrefix.TryPrepare(birth.Connection, snapshot, input.Users,
                out var prepared, out _));
            Assert.Null(prepared);
            Assert.Equal(0U, player.Class);
            Assert.Equal((ushort)0, player.Level);
            Assert.Equal(0U, player.UID);
            Assert.Equal(0U, player.Online);
            Assert.Null(birth.Connection.NativeGameClient.Beasts);
            Assert.Null(birth.Connection.NativeGameClient.HairfaceStorage);
            Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
        });
    }

    [Theory]
    [InlineData(256)]
    [InlineData(65535)]
    public async Task OrdinaryLoginRefusesLoaderHeroLevelOverflowBeforeDurablePresenceOrNativePublication(int level)
    {
        using var input = new LoginInput();
        WriteClassLevelInput(input, 7051, level);
        var files = CaptureAdmissionFiles(input);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var native = birth.Connection.NativeGameClient!;
            var rejected = await SendLoginAsync(fixture, wire);
            Assert.Null(rejected.After);
            Assert.Equal(0U, native.Player.UID);
            Assert.Equal(0U, native.Player.Class);
            Assert.Equal((ushort)0, native.Player.Level);
            Assert.Equal(0U, native.Player.Online);
            Assert.Null(native.Beasts);
            Assert.Null(native.HairfaceStorage);
            AssertAdmissionFiles(files);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            Assert.DoesNotContain("ordinary admission committed", log.Text);
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
        });
    }

    [Fact]
    public async Task ChangedPhysicalLevelAfterPreparationCannotCommitItsStalePrefix()
    {
        using var input = new LoginInput();
        var bytes = File.ReadAllBytes(input.FilePath);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            Assert.True(LegacyNativeExistingCharacterPrefix.TryPrepare(birth.Connection, ReadPrefixSnapshot(input), input.Users,
                out var prepared, out var reason), reason);
            // Controlled mutation of actual storage; no fake owner or production hook.
            SetClassLevelField(player, "_level", (ushort)9);
            Assert.False(prepared!.TryCommit(out _, out _));
            Assert.Equal((ushort)9, player.Level);
            Assert.Equal(0U, player.Class);
            Assert.Equal(0U, player.UID);
            Assert.Equal(0U, player.Online);
            Assert.Null(client.Beasts);
            Assert.Null(client.HairfaceStorage);
            Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
        });
    }

    private static void SetClassLevelField(LegacyNativePlayerRuntime player, string name, object value) =>
        typeof(LegacyNativePlayerRuntime).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, value);
}
