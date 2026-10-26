using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("locked")]
    [InlineData("plus")]
    [InlineData("damaged")]
    [InlineData("stacked")]
    [InlineData("bound")]
    public async Task OrdinaryLoginRetainsNonneutralOwnedEquipmentWithoutTreatingItAsStarterRepair(string state)
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        var items = new[] { (Id: 132009U, Pos: (ushort)3), (Id: 420301U, Pos: (ushort)4),
            (Id: 420301U, Pos: (ushort)5), (Id: 150003U, Pos: (ushort)6) }
            .Select((row, index) => new PlayerItemSnapshotItem { Uid = 100U + (uint)index, ItemId = row.Id,
                Position = row.Pos, Durability = state == "damaged" && index == 0 ? (ushort)320 : (ushort)321,
                MaximumDurability = 321, Color = 3, Locked = state == "locked" && index == 0 ? (byte)1 : (byte)0,
                Plus = state == "plus" && index == 0 ? (byte)1 : (byte)0,
                Bound = state == "bound" && index == 0 ? (byte)1 : (byte)0,
                StackSize = state == "stacked" && index == 0 ? (ushort)2 : (ushort)0 }).ToArray();
        File.WriteAllBytes(Path.Combine(AdmissionRoot(input), "PlayersItems", Uid + ".bin"),
            PlayerItemSnapshotLoader.ToLegacyBytes(new([], [], [], items, [])));
        using var uids = new AdmissionUidFixture(input);
        var itemsPath = Path.Combine(AdmissionRoot(input), "PlayersItems", Uid + ".bin");
        var itemBytes = File.ReadAllBytes(itemsPath);
        var characterBytes = File.ReadAllBytes(input.FilePath);
        var beforeCharacter = new CharacterSnapshotLoader(input.Users).TryLoadByAccountIdForAdmission(Uid).Snapshot!;
        var uidBytes = uids.StateBytes();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var commitObservations = ObserveRawAdmissionCommit(log, () => AssertOnlyPresenceChanged(input, characterBytes));
            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(1, commitObservations());
            AssertOrdinaryBusinessPreserved(beforeCharacter, admitted.Character, expectedRuntimeHitpoints: 0);
            Assert.Equal(new uint[] { 100, 101, 102, 103 }, admitted.PlayerItems.EquipmentItems.Select(row => row.Uid));
            Assert.Equal(itemBytes, File.ReadAllBytes(itemsPath));
            var committed = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(characterBytes)
                .Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
            Assert.Equal(OrdinaryAdmissionImageExpectations.RuntimeHpSave(committed, 0, 0, 0),
                File.ReadAllBytes(input.FilePath));
            AssertNoOrdinaryAllocation(uids, uidBytes);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            Assert.Contains("ordinary admission committed", log.Text);
        }, worldData: AdmissionCatalog(), admissionUids: uids.Owner);
    }

    [Fact]
    public async Task UidManagedNewRoleCannotFallBackToTheUntrackedLegacyStarterAllocator()
    {
        using var input = new LoginInput(create: false);
        PrepareAdmissionInput(input);
        using var uids = new AdmissionUidFixture(input);
        var uidBytes = uids.StateBytes();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            Assert.Null((await SendLoginAsync(fixture, wire)).After);
            await wire.SendAsync(new LegacyNewRolePacket("OwnedBirth", 1008, 12).ToFrameBytes(), fixture.Token);
            Assert.Null((await fixture.NextAsync()).After);
            Assert.False(File.Exists(input.FilePath));
            Assert.Equal(0U, birth.Connection.NativeGameClient!.Player.UID);
            Assert.Null(birth.Connection.NativeGameClient.Beasts);
            Assert.Null(birth.Connection.NativeGameClient.HairfaceStorage);
            Assert.Equal(uidBytes, uids.StateBytes());
            Assert.DoesNotContain("legacy NewRole Success", log.Text);
        }, worldData: AdmissionCatalog(), admissionUids: uids.Owner);
    }
}
