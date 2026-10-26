using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Fact]
    public async Task RealOwnedSpendingMovementAndEmptyGearRemainValidWithoutAnyStarterEntitlement()
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        using var uids = new AdmissionUidFixture(input);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, _) = await ConnectBornAsync(fixture);
            Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
        }, worldData: AdmissionCatalog(), admissionUids: uids.Owner);
        var loader = new CharacterSnapshotLoader(input.Users);
        var saved = loader.TryLoadByAccountIdForAdmission(Uid).Snapshot!;
        // Genuine owned writers set up the state reachable after removing gear,
        // spending/transferring money and moving, without resetting UID history.
        new PlayerItemSnapshotLoader(Path.Combine(AdmissionRoot(input), "PlayersItems"))
            .SaveByCharacterUid(Uid, PlayerItemSnapshot.Empty);
        Assert.True(CharacterSnapshotScalarPersistence.TryPersist(input.Users,
            saved with { Money = 0, X = saved.X + 1 }, out _, out var reason), reason);
        var files = CaptureAdmissionFiles(input);
        var uidState = uids.StateBytes();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(0L, admitted.Character.Money);
            Assert.Equal(saved.X + 1, admitted.Character.X);
            Assert.Empty(admitted.PlayerItems.EquipmentItems);
            Assert.NotNull(birth.Connection.NativeGameClient!.Beasts);
            Assert.NotNull(birth.Connection.NativeGameClient.HairfaceStorage);
            AssertAdmissionFiles(files);
            Assert.Equal(uidState, uids.StateBytes());
            Assert.False(AdmissionOwner<LegacyTradeRecoveryState>(handler, "_tradeRecovery").IsBlocked);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            Assert.Contains("legacy MsgLoginGame Success", log.Text);
            AssertNoOrdinaryAllocation(uids, uidState);
        }, worldData: AdmissionCatalog(), admissionUids: uids.Owner);
        Assert.Equal(0L, loader.TryLoadByAccountIdForAdmission(Uid).Snapshot!.Money);
        Assert.Empty(loader.LoadPlayerItemsByCharacterUid(Uid).EquipmentItems);
    }

    [Fact]
    public async Task ZeroAllocationOrdinaryAdmissionDoesNotCreateStarterCompletionEvidence()
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input); // Mature Reborn2: no starter payout/UID allocation.
        using var uids = new AdmissionUidFixture(input);
        var state = uids.StateBytes();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, _) = await ConnectBornAsync(fixture);
            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(140L, admitted.Character.Level);
            Assert.Equal(1000L, admitted.Character.Money);
            Assert.Empty(admitted.PlayerItems.EquipmentItems);
            Assert.Equal(state, uids.StateBytes());
            Assert.True(uids.Registry.TryReadProducerIntents(uids.Domain, out var work, out var reason), reason);
            Assert.Empty(work);
            Assert.False(AdmissionOwner<LegacyTradeRecoveryState>(handler, "_tradeRecovery").IsBlocked);
        }, admissionUids: uids.Owner);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreshOwnerAndDefaultFrameCannotUseCorruptOrUnknownOwnedHistoryAsAdmissionAuthority(bool unknownFile)
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        using var uids = new AdmissionUidFixture(input);
        SeedRetiredOrdinaryWork(input, uids, allocated: true, out var after);
        uids.Dispose();
        InstallRetiredAfterFacts(input, after);
        uids.Open(bootstrap: false);
        var before = CaptureAdmissionFiles(input);
        var state = uids.StateBytes();
        uids.Dispose();
        var history = Path.Combine(Path.GetDirectoryName(uids.StatePath)!, "history");
        var path = unknownFile ? Path.Combine(history, "foreign.json") : Assert.Single(Directory.GetFiles(history, "*.json"));
        File.WriteAllBytes(path, [0x7b, 0x7d]);
        Assert.False(LegacyItemUidRegistry.TryOpen(uids.OptionsForReopen, out var refused, out _));
        Assert.Null(refused);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var player = ObservePrefixPlayer(birth.Connection.NativeGameClient!.Player);
            Assert.Null((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(player, ObservePrefixPlayer(birth.Connection.NativeGameClient.Player));
            Assert.Null(birth.Connection.NativeGameClient.Beasts);
            Assert.Null(birth.Connection.NativeGameClient.HairfaceStorage);
            Assert.Equal(new byte[] { 0x7b, 0x7d }, File.ReadAllBytes(path));
            Assert.Equal(state, uids.StateBytes());
            AssertAdmissionFiles(before);
            Assert.DoesNotContain("ordinary admission committed key=", log.Text);
        });
    }
}
