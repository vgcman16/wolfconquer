using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedItemRepairTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ActualRepairFirstSendSeesBothDurableFilesAndTheCanonicalNativeGraph(bool meteor, bool equipped)
    {
        await WithRepair(meteor, equipped, async f =>
        {
            await f.Login(); var before = f.Current; var binding = AssertGraph(f);
            var inventory = f.Native.Inventory; var equipment = f.Native.Equipment;
            byte[] expectedCharacter = ExpectedCharacter(File.ReadAllBytes(f.CharacterPath), meteor);
            byte[] expectedItems = PlayerItemSnapshotLoader.ToLegacyBytes(ExpectedItems(before, meteor));
            int first = f.Sent.Count, observations = 0;
            f.OnSend = _ =>
            {
                observations++;
                Assert.NotSame(before, f.Current); Assert.Same(binding, AssertGraph(f));
                Assert.Equal(meteor ? 1000 : 943, f.Current.Character.Money);
                Assert.Equal(expectedCharacter, File.ReadAllBytes(f.CharacterPath));
                Assert.Equal(expectedItems, File.ReadAllBytes(f.ItemsPath));
                Assert.False(LegacyTradeJournal.HasPending(f.Users)); Assert.False(f.Recovery.IsBlocked);
                var target = equipped ? f.Native.Equipment!.ClientItems[RepairedTarget(f.Current).Uid] :
                    f.Native.Inventory!.ClientItems[RepairedTarget(f.Current).Uid];
                Assert.Equal((ushort)10, target.Durability);
                Assert.False(Monitor.IsEntered(f.Trades.SyncRoot));
                Assert.False(Monitor.IsEntered(f.Native.SpellAdmissionSyncRoot));
                Assert.False(Monitor.IsEntered(f.Native.Player.SpellAdmissionSyncRoot));
            };

            await f.Send(Request()); f.OnSend = null;

            Assert.Contains(meteor ? "handled MsgItemUsage zero-durability repair" : "handled MsgItemUsage paid repair",
                f.Log.ToString(), StringComparison.Ordinal);
            Assert.Contains(meteor ? "payment=Meteors cost=5; persistedBeforeReply=True" :
                "payment=Money cost=57; persistedBeforeReply=True", f.Log.ToString(), StringComparison.Ordinal);
            Assert.Same(inventory, f.Native.Inventory); Assert.Same(equipment, f.Native.Equipment);
            Assert.Equal(meteor ? 6 : 3, observations);
            var sent = f.Sent.Skip(first).ToArray();
            if (!meteor) AssertPaidFrames(sent);
            else
            {
                Assert.Equal(6, sent.Length);
                var materials = before.PlayerItems.InventoryItems.Where(row => row.ItemId is MeteorId or TearId).ToArray();
                for (int index = 0; index < 5; index++)
                {
                    Assert.True(LegacyGameItemPacket.TryParse(Frame(sent[index]), out var debit));
                    Assert.Equal(materials[index].Uid, debit.Uid); Assert.Equal(materials[index].ItemId, debit.ItemId);
                    Assert.Equal((ushort)9, debit.StackSize); Assert.Equal(LegacyGameItemPacket.UpdateItemMode, debit.Mode);
                }
                Assert.True(LegacyGameItemPacket.TryParse(Frame(sent[5]), out var target));
                Assert.Equal(RepairedTarget(before).Uid, target.Uid); Assert.Equal((ushort)10, target.Durability);
            }
            var durable = f.Files(); var current = f.Current;
            await f.Send(Request());
            Assert.Same(current, f.Current); Assert.Equal(first + observations, f.Sent.Count); f.CheckFiles(durable);
            Assert.Equal(meteor ? (ushort)0 : (ushort)5, RepairedTarget(before).Durability);
        });
    }

    [Fact]
    public async Task ActualMeteorRepairRetainsPhysicalDebitOrderThroughStackRemovalBeforeTargetOutput()
    {
        var material = Material(0, count: 5); var target = Target(meteor: true);
        var initial = PlayerItemSnapshot.Empty with { InventoryItems = new[] { target, material } };
        await WithRepair(true, false, async f =>
        {
            await f.Login(); int first = f.Sent.Count;
            f.OnSend = _ =>
            {
                AssertGraph(f); Assert.Single(f.Current.PlayerItems.InventoryItems);
                Assert.False(f.Native.Inventory!.ClientItems.ContainsKey(material.Uid));
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems), File.ReadAllBytes(f.ItemsPath));
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
            };
            await f.Send(Request()); f.OnSend = null;
            var sent = f.Sent.Skip(first).ToArray(); Assert.Equal(6, sent.Length);
            for (int index = 0; index < 4; index++)
            {
                Assert.True(LegacyGameItemPacket.TryParse(Frame(sent[index]), out var debit));
                Assert.Equal(material.Uid, debit.Uid); Assert.Equal((ushort)(4 - index), debit.StackSize);
            }
            Assert.True(MsgItemUsagePacket.TryParse(Frame(sent[4]), out var removal));
            Assert.Equal(material.Uid, removal.Id); Assert.Equal(MsgItemUsageAction.RemoveInventory, removal.Action);
            Assert.True(LegacyGameItemPacket.TryParse(Frame(sent[5]), out var repaired));
            Assert.Equal(target.Uid, repaired.Uid); Assert.Equal((ushort)10, repaired.Durability);
        }, saved: initial);
    }
}
