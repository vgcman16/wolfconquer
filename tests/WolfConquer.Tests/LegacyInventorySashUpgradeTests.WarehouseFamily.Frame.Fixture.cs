using System.Text.Json;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private sealed partial class Fixture
    {
        internal static byte[] WarehouseFamilyFrameRequest(LegacyWarehouseAction action) =>
            new LegacyWarehousePacket(WarehouseFamilyBank(action), action,
                WarehouseFamilyItemUid, 0, []).ToFrameBytes();

        internal async Task EnterWarehouseFamilyFrameAsync(LegacyWarehouseAction action)
        {
            SeedWarehouseFamily(action);
            var oldPath = CharacterPath;
            Account = Uid;
            File.Move(oldPath, CharacterPath); // Owned setup before public admission, never after an action.
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

            // The actual Frame owns its Store/Trades/barrier/recovery; setup owners are never injected.
            // This existing public-handoff fixture is managed, not a manufactured native Player.
            Assert.Null(Connection.NativeGameClient);
            var entered = CaptureTransferFrame();
            Assert.True(LegacySecondaryPasswordState.TryRead(entered.Owner.Character.Attributes, out var pin, out var reason), reason);
            Assert.Equal(0U, pin.Password);
            Assert.Equal("2", entered.Owner.Character.Attributes["InventorySashCount"]);
            var moving = Assert.Single(IsWarehouseFamilyWithdraw(action)
                ? entered.Owner.PlayerItems.WarehouseItems : entered.Owner.PlayerItems.InventoryItems,
                item => item.Uid == WarehouseFamilyItemUid);
            Assert.Equal(IsWarehouseFamilyWithdraw(action) ? WarehouseFamilyBank(action) : 0U, moving.WarehouseId);
            Assert.Equal(12U, Assert.Single(moving.DepositedItems).Uid);
        }

        internal void AssertWarehouseFamilyFrameResult(Snapshot before, LegacyWarehouseAction action)
        {
            var bank = WarehouseFamilyBank(action);
            AssertFiles(ExpectedWarehouseFamilyFiles(this, before, action, bank));
            var expectedItems = ExpectedWarehouseFamilyItems(before.Owner.PlayerItems, action, bank);
            var actual = new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(Uid);
            Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(actual));
            AssertWarehouseFamilyFrames(this, before, action, bank);
            Assert.False(LegacyTradeJournal.HasPending(Users));
        }
    }
}
