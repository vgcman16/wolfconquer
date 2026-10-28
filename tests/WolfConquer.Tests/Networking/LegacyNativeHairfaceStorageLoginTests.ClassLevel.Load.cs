using System.Globalization;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData(7051L, 140)]
    [InlineData(5057L, 255)]
    [InlineData(4294967295L, 65535)]
    public async Task ColdPrefixPublishesExactSavedClassAndUshortLevelAfterItsActualFileCommit(long job, int level)
    {
        using var input = new LoginInput();
        var bytes = WriteClassLevelInput(input, job, level);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            player.TransformationID = 17; // Genuine cold setter; no live send.
            var flags = player.BitVector;
            var snapshot = ReadPrefixSnapshot(input);
            Assert.True(LegacyNativeExistingCharacterPrefix.TryPrepare(birth.Connection, snapshot, input.Users,
                out var prepared, out var reason), reason);
            Assert.NotNull(prepared);
            Assert.Equal(0U, player.Class);
            Assert.Equal((ushort)0, player.Level);
            Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));

            Assert.True(prepared.TryCommit(out var updated, out reason), reason);

            Assert.Equal(checked((uint)job), player.Class);
            Assert.Equal(checked((ushort)level), player.Level);
            Assert.Equal((ushort)17, player.TransformationID);
            Assert.Equal(job, updated.Class);
            Assert.Equal((long)level, updated.Level);
            Assert.Same(client, player.Owner);
            Assert.Same(player, client.Player);
            Assert.Same(flags, player.BitVector);
            Assert.True(prepared.IsPublishedAdmissionOwnerCurrent());
            Assert.Equal(PresenceImage(bytes), File.ReadAllBytes(input.FilePath));
            Assert.False(prepared.TryCommit(out _, out _));
            Assert.Equal(checked((ushort)level), player.Level);
            Assert.Null(client.MyNinja);
            Assert.Null(client.MySpells);
            Assert.Null(client.MyProfs);
            Assert.Null(client.OrdinarySpellBinding);
            Assert.Null(client.Equipment);
            Assert.False(client.FullLoading);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(140)]
    [InlineData(255)]
    public async Task OrdinaryLoginHydratesSavedClassAndByteLevelOnTheGenuineBornPlayer(int level)
    {
        using var input = new LoginInput();
        WriteClassLevelInput(input, 7051, level);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Same(player, accepted.NativePlayer);
            Assert.Equal(7051U, player.Class);
            Assert.Equal(checked((ushort)level), player.Level);
            Assert.Equal(accepted.Character.Class, (long)player.Class);
            Assert.Equal(accepted.Character.Level, (long)player.Level);
            Assert.Equal(Uid, player.UID);
            Assert.Equal(1U, player.Online);
            Assert.False(client.FullLoading);
            Assert.Null(client.MyNinja);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
            Assert.False(binding.IsRetired);
            Assert.Same(client, binding.Client);
            Assert.Same(player, binding.Player);
            Assert.Same(client.MySpells, binding.Storage);
            Assert.Same(client.MyProfs, binding.Proficiencies);
            Assert.Same(accepted.PlayerSpells, binding.Snapshot);
            Assert.Same(accepted.PlayerProficiencies, binding.ProficiencySnapshot);
            Assert.True(binding.PhysicalOwnerIsCurrentPlain());
            Assert.Same(accepted, handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.Contains("legacy MsgLoginGame Success", log.Text);
            Assert.Same(accepted, (await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(checked((ushort)level), player.Level);
        });
    }

    private static byte[] WriteClassLevelInput(LoginInput input, long job, int level)
    {
        var text = Encoding.UTF8.GetString(input.InitialBytes)
            .Replace("Class=7051\r\n", "Class=" + job.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal)
            .Replace("Level=140\r\n", "Level=" + level.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(text);
        File.WriteAllBytes(input.FilePath, bytes);
        return bytes;
    }

    private static byte[] PresenceImage(byte[] before) => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(before)
        .Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
}
