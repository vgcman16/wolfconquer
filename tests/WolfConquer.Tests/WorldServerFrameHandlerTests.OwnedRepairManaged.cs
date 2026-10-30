using System.Globalization;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task WhollyManagedRepairFirstSendSeesItsActualConfiguredTwoFileCommit()
    {
        const uint uid = 1293600, itemUid = 2001, itemId = 410009;
        string users = Path.Combine(_usersRoot, "Users"), items = Path.Combine(_usersRoot, "Items");
        Directory.CreateDirectory(users); Directory.CreateDirectory(items);
        WriteSnapshot(users, uid, uid: uid, money: 1000);
        var target = new PlayerItemSnapshotItem { Uid = itemUid, ItemId = itemId,
            Durability = 5, MaximumDurability = 10, StackSize = 1 };
        string characterPath = Path.Combine(users, uid + ".ini"), itemsPath = Path.Combine(items, uid + ".bin");
        new PlayerItemSnapshotLoader(items).SaveByCharacterUid(uid,
            PlayerItemSnapshot.Empty with { InventoryItems = new[] { target } });
        var fields = Enumerable.Repeat("0", 66).ToArray(); fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "OwnedManagedRepair"; fields[ItemType.GoldWorthFieldIndex] = "100";
        fields[ItemType.DurabilityFieldIndex] = "10"; fields[ItemType.StackSizeFieldIndex] = "1";
        var data = LegacyItemWorldDataFixture.Create(new ItemType((int)itemId, fields[1], fields));
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(uid, "owned-managed-repair", ConquerRuntimeDefaults.WorldServer);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(users, items), handoff, data, log);
        var sent = new List<byte[]>(); bool observe = false; int observations = 0;
        byte[] character = Array.Empty<byte>(), itemImage = Array.Empty<byte>();
        ConquerTcpConnection? connection = null;
        connection = CreateSendingConnection(sent, (_, _) =>
        {
            if (observe)
            {
                observations++;
                var current = GetSession(handler, connection!);
                Assert.Null(connection!.NativeGameClient); Assert.Null(current.NativeGameClient);
                Assert.Equal(943, current.Character.Money);
                Assert.Equal((ushort)10, Assert.Single(current.PlayerItems.InventoryItems).Durability);
                Assert.Equal(character, File.ReadAllBytes(characterPath)); Assert.Equal(itemImage, File.ReadAllBytes(itemsPath));
                Assert.False(LegacyTradeJournal.HasPending(users));
            }
            return ValueTask.CompletedTask;
        });
        try
        {
            await handler.HandleFrameAsync(connection, ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
            var before = GetSession(handler, connection); Assert.Null(before.NativeGameClient);
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(characterPath), out var encoding);
            Assert.Contains("Money=1000", text, StringComparison.Ordinal);
            character = encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace("Money=1000", "Money=943", StringComparison.Ordinal))).ToArray();
            itemImage = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems with
                { InventoryItems = new[] { Assert.Single(before.PlayerItems.InventoryItems).WithDurability(10) } });
            int first = sent.Count; observe = true;

            await handler.HandleFrameAsync(connection, ReadFrame(new MsgItemUsagePacket(itemUid, 0,
                MsgItemUsageAction.RepairItem, 1234, 0, 0, 0).ToFrameBytes()));

            observe = false; Assert.Equal(3, observations); Assert.Equal(first + 3, sent.Count);
            AssertSingleUpdate(sent[first], uid, LegacyMsgUpdateDataType.Money, 943);
            AssertSingleUpdate(sent[first + 1], uid, LegacyMsgUpdateDataType.Money, 943);
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[first + 2]), out var repaired));
            Assert.Equal(itemUid, repaired.Uid); Assert.Equal((ushort)10, repaired.Durability);
        }
        finally { observe = false; await connection.CompleteTransportCloseAsync(); }
    }
}
