using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using ItemFixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinarySpellCallerTests
{
    [Fact]
    public async Task BookV9PersistedCommitDoesNotRepeatOrdinaryObserver()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            // Reuse the owned physical book fixture before ordinary admission.
            // This is an unbound Store control with its own real Recovery.
            var beforeCharacter = File.ReadAllBytes(f.CharacterPath);
            var beforeSpells = File.ReadAllBytes(f.SpellsPath);
            var character = CharacterSnapshotParser.ParseText(
                CharacterSnapshotFileEncoding.Decode(beforeCharacter, out _));
            var items = new PlayerItemSnapshotLoader(f.Items).LoadByCharacterUid(ItemFixture.Uid);
            var spells = new PlayerSpellSnapshotLoader(f.Spells).LoadByCharacterUid(ItemFixture.Uid);
            var session = new WorldSession(ItemFixture.Uid, "owned-book-control", character, items, spells,
                PlayerProficiencySnapshot.Empty, UsersRoot: f.Users);
            var sent = new List<byte[]>();
            var connection = new ConquerTcpConnection(null, null,
                (frame, _) => { sent.Add(frame.ToArray()); return ValueTask.CompletedTask; });
            int notifications = 0; bool armed = false;
            var store = new WorldSessionStore((_, _, _) =>
            {
                notifications++;
                if (armed) throw new IOException("ordinary observer must not run during persisted commit");
            });
            store.Set(connection, session);
            var context = LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection, f.Log, Session: session, WorldData: f.Catalog, Sessions: store,
                PlayerSpellsRoot: f.Spells, PlayerItemsRoot: f.Items)
                { TradeOperations = new WorldOperationBarrier() }, ref session);
            Assert.True(context.TradeRecovery!.IsReadyForCharacterItemsSpells(f.Users, f.Items, f.Spells));
            var handler = typeof(WorldPacketHandlers).GetMethod("HandleMsgItemUsageAsync",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(handler);
            int beforeNotifications = notifications; armed = true;

            var pending = (ValueTask)handler!.Invoke(null, [context, Frame(BookRequest()), CancellationToken.None])!;
            await pending;
            Assert.Equal(beforeNotifications, notifications);
            Assert.True(store.TryGet(connection, out var current));
            Assert.NotSame(session, current);
            Assert.NotNull(current);
            Assert.Equal(beforeCharacter, File.ReadAllBytes(f.CharacterPath));
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty), File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(Convert.FromHexString("02000000").Concat(beforeSpells.Skip(4))
                .Concat(Convert.FromHexString("500500000000000000000000")).ToArray(), File.ReadAllBytes(f.SpellsPath));
            Assert.DoesNotContain(current.PlayerItems.InventoryItems, row => row.Uid == ItemFixture.ItemUid);
            Assert.Contains(current.PlayerSpells.Spells, row => row.Id == 1360);
            Assert.False(context.TradeRecovery.IsBlocked);
            Assert.Equal(5, sent.Count);
            Assert.True(MsgItemUsagePacket.TryParse(Frame(sent[0]), out var consume));
            Assert.Equal(MsgItemUsageAction.RemoveInventory, consume.Action);
            Assert.Equal(ItemFixture.ItemUid, consume.Id);
            Assert.True(LegacySpellPacket.TryParse(Frame(sent[1]), out var learned));
            Assert.Equal((ushort)1360, learned.Id);
            Assert.Equal(Convert.FromHexString("19003508000000005005000000000000000000000000000000"), sent[1]);
            Assert.True(LegacyNpcReplyPacket.TryParse(Frame(sent[2]), out var dialog));
            Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
            Assert.Equal("You~have~learned~1360.", dialog.Text);
            Assert.True(LegacyNpcReplyPacket.TryParse(Frame(sent[3]), out var option));
            Assert.Equal(LegacyNpcReplyInteractType.Option, option.InteractType);
            Assert.Equal("I~see.", option.Text);
            Assert.True(LegacyNpcReplyPacket.TryParse(Frame(sent[4]), out var finish));
            Assert.Equal(LegacyNpcReplyInteractType.Finish, finish.InteractType);
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });
    }
}
