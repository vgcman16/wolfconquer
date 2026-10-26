using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task OrdinaryLoginPreservesEvenStarterShapedSavedProgressInsteadOfAuthorizingAMigration(int level)
    {
        using var input = new LoginInput();
        SeedOwnedOrdinaryParticipants(input);
        File.WriteAllText(input.FilePath, File.ReadAllText(input.FilePath)
            .Replace("Level=1\r\n", "Level=" + level + "\r\n", StringComparison.Ordinal)
            .Replace("Strength=0\r\n", "Strength=155\r\n", StringComparison.Ordinal)
            .Replace("Agility=0\r\n", "Agility=60\r\n", StringComparison.Ordinal)
            .Replace("Vitaliti=0\r\n", "Vitaliti=92\r\n", StringComparison.Ordinal)
            .Replace("Money=1000\r\n", "Money=10000000\r\n", StringComparison.Ordinal)
            .Replace("\r\nConquerPoints=0\r\n", "\r\nConquerPoints=50000\r\n", StringComparison.Ordinal));
        var before = new CharacterSnapshotLoader(input.Users).TryLoadByAccountIdForAdmission(Uid).Snapshot!;
        var bytes = File.ReadAllBytes(input.FilePath);
        var itemsPath = Path.Combine(AdmissionRoot(input), "PlayersItems", Uid + ".bin");
        var items = File.ReadAllBytes(itemsPath);
        using var uids = new AdmissionUidFixture(input);
        var uidBytes = uids.StateBytes();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            AssertOrdinaryBusinessPreserved(before, accepted.Character);
            Assert.Equal((long)level, accepted.Character.Level);
            Assert.Equal(10000000L, accepted.Character.Money);
            Assert.Equal(50000L, accepted.Character.ConquerPoints);
            Assert.Equal(new uint[] { 100 }, accepted.PlayerItems.EquipmentItems.Select(row => row.Uid));
            Assert.Equal(items, File.ReadAllBytes(itemsPath));
            AssertOnlyPresenceChanged(input, bytes);
            AssertNoOrdinaryAllocation(uids, uidBytes);
            Assert.Equal(new ushort[] { 8002, 1110 }, accepted.PlayerSpells.Spells.Select(row => row.Id));
            Assert.Equal((ushort)3, accepted.PlayerSpells.Spells[0].Level);
            Assert.Equal(123, accepted.PlayerSpells.Spells[0].Experience);
            Assert.Equal(1U, birth.Connection.NativeGameClient!.Player.Online);
            Assert.NotNull(birth.Connection.NativeGameClient.RawEquipmentObservation);
            Assert.False(birth.Connection.NativeGameClient.FullLoading);
            var after = CaptureAdmissionFiles(input);
            Assert.Same(accepted, (await SendLoginAsync(fixture, wire)).After);
            AssertAdmissionFiles(after);
            AssertNoOrdinaryAllocation(uids, uidBytes);
            Assert.Contains("legacy MsgLoginGame Success", log.Text);
        }, worldData: AdmissionCatalog(), admissionUids: uids.Owner);
    }

    [Fact]
    public async Task EmptyOwnedInventoryNeedsNoStarterAllocatorAndCreatesNoUntrackedBirthGrant()
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        var before = new CharacterSnapshotLoader(input.Users).TryLoadByAccountIdForAdmission(Uid).Snapshot!;
        var bytes = File.ReadAllBytes(input.FilePath);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var commitObservations = ObserveRawAdmissionCommit(log, () => AssertOnlyPresenceChanged(input, bytes));
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(1, commitObservations());
            AssertOrdinaryBusinessPreserved(before, accepted.Character, expectedRuntimeHitpoints: 0);
            Assert.Equal(1000L, accepted.Character.Money);
            Assert.Empty(accepted.PlayerItems.InventoryItems);
            Assert.Empty(accepted.PlayerItems.EquipmentItems);
            Assert.False(File.Exists(Path.Combine(AdmissionRoot(input), "PlayersItems", Uid + ".bin")));
            Assert.False(Directory.Exists(Path.Combine(AdmissionRoot(input), "PlayersItems", ".managed-item-uids")));
            Assert.Equal(new ushort[] { 1110 }, accepted.PlayerSpells.Spells.Select(row => row.Id));
            var committed = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(bytes)
                .Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
            Assert.Equal(OrdinaryAdmissionImageExpectations.RuntimeHpSave(committed, 0, 0, 0),
                File.ReadAllBytes(input.FilePath));
            Assert.Equal(1U, birth.Connection.NativeGameClient!.Player.Online);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
        });
    }
}
