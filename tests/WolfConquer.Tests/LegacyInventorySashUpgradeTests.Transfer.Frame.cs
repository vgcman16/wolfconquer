using System.Text.Json;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualD13FramePhysicalSashCommitsCompleteItemFileBeforeEverySuccessFrameAndCannotRepeat(bool withdraw) =>
        RunAsync(async fixture =>
        {
            await fixture.EnterTransferFrameAsync(withdraw);
            var before = fixture.CaptureTransferFrame();
            var expectedFiles = ExpectedTransferFiles(fixture, before, withdraw);
            var observed = 0;
            fixture.SendObserver = _ =>
            {
                fixture.AssertFiles(expectedFiles);
                observed++;
            };

            await fixture.FrameAsync(Fixture.TransferRequest(withdraw));

            Assert.Equal(withdraw ? 2 : 4, observed);
            fixture.AssertTransferFrameResult(before, withdraw);
            fixture.SendObserver = null;
            var frameCount = fixture.Sent.Count;

            await fixture.FrameAsync(Fixture.TransferRequest(withdraw));

            fixture.AssertFiles(expectedFiles);
            Assert.Equal(frameCount, fixture.Sent.Count);
        });

    [WindowsTradeJournalFailureFact]
    public Task ActualD13FramePhysicalSashDepositHeldLeafPublishesNothingAndSameEnteredOwnerRetries() =>
        RunTransferFrameSharingFailureAsync(withdraw: false);

    [WindowsTradeJournalFailureFact]
    public Task ActualD13FramePhysicalSashWithdrawHeldLeafPublishesNothingAndSameEnteredOwnerRetries() =>
        RunTransferFrameSharingFailureAsync(withdraw: true);

    private static Task RunTransferFrameSharingFailureAsync(bool withdraw) => RunAsync(async fixture =>
    {
        await fixture.EnterTransferFrameAsync(withdraw);
        var before = fixture.CaptureTransferFrame();
        fixture.HoldTransferItemFile();

        await fixture.FrameAsync(Fixture.TransferRequest(withdraw));

        fixture.ReleaseFiles(); // The actual Frame task completed before releasing the owned lease.
        fixture.AssertFiles(before.Files);
        Assert.Equal(before.FrameCount, fixture.Sent.Count);
        await fixture.FrameAsync(Fixture.TransferRequest(withdraw));
        fixture.AssertTransferFrameResult(before, withdraw);
    });

    private sealed partial class Fixture
    {
        internal static byte[] TransferRequest(bool withdraw) => new LegacyWarehousePacket(TransferSashUid,
            withdraw ? LegacyWarehouseAction.ShashWithdrawItem : LegacyWarehouseAction.ShashDepositItem,
            TransferItemUid, 0, []).ToFrameBytes();

        internal async Task EnterTransferFrameAsync(bool deposited)
        {
            SeedTransfer(deposited: deposited);
            var oldPath = CharacterPath;
            Account = Uid; // Actual public auth loader binds this account to the saved character.
            File.Move(oldPath, CharacterPath); // Owned fixture setup only, before admission/recovery.
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var loader = new CharacterSnapshotLoader(Users, Items, playerSpellsRoot: null,
                playerProficienciesRoot: null, playersCombatGearRoot: null, playerQuestsRoot: null);
            frameOwner = new WorldServerFrameHandler(loader, handoff, worldData: null,
                runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: Log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            var ticket = handoff.Issue(Account, "sash-transfer-frame", ConquerRuntimeDefaults.WorldServer);
            var first = Sent.Count;
            await FrameAsync(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes());
            var parsed = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath);
            Assert.Equal(WorldSnapshotResponsePacket.Build(WorldSnapshotWireStatus.Success, Account, parsed), Sent[first]);
            await FrameAsync(new MsgActionPacket { ObjId = Uid, Type = MsgActionType.SetLocation }.ToFrameBytes());
            // Capture oracles only after legitimate startup scalar/item normalization.
            // The separate business-fixture Store is never injected into this Frame owner.
            var entered = CaptureTransferFrame();
            Assert.Equal(deposited ? 1 : 2, entered.Owner.PlayerItems.InventoryItems.Count);
            Assert.Equal(deposited ? 1 : 0, Assert.Single(entered.Owner.PlayerItems.InventoryItems,
                item => item.Uid == TransferSashUid).DepositedItems.Count);
        }

        internal Snapshot CaptureTransferFrame()
        {
            var character = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath);
            var items = new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(Uid);
            // File-derived expected image only. It is not published as a session or identity.
            var expected = new WorldSession(Account, "sash-transfer-frame", character, items,
                PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: Users);
            return new(expected, JsonSerializer.Serialize(expected), Files(), Sent.Count);
        }

        internal void AssertTransferFrameResult(Snapshot before, bool withdraw)
        {
            var expectedItems = ExpectedTransferItems(before.Owner.PlayerItems, withdraw);
            AssertFiles(ExpectedTransferFiles(this, before, withdraw));
            var actual = new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(Uid);
            Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(actual));
            AssertTransferFrames(this, before, withdraw);
            Assert.False(LegacyTradeJournal.HasPending(Users));
        }
    }
}
