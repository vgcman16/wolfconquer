using System.Collections.ObjectModel;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedItemRepairTests
{
    [Fact]
    public Task ActualWarehouseBankAcceptsReadonlyInputAndPublishesPreparedWalletBeforeReplies() =>
        WithWarehouseRepair(false, async f =>
        {
            await f.Login();
            var before = f.Current;
            var values = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            var probe = new WarehouseInputDictionary(values);
            f.Store.Set(f.Connection, before with { Character = before.Character with
                { Attributes = new ReadOnlyDictionary<string, string>(probe) } });
            var itemBytes = File.ReadAllBytes(f.ItemsPath);
            var first = f.Sent.Count;
            Exception? observed = null;
            f.OnSend = sentFrame =>
            {
                probe.RefuseEnumeration = true;
                observed ??= Record.Exception(() =>
                {
                    Assert.Equal((700L, 855L), (f.Native.Player.Money, f.Native.Player.WHMoney));
                    Assert.Equal(700L, f.Current.Character.Money);
                    Assert.Equal("855", f.Current.Character.Attributes["WHMoney"]);
                    Assert.Equal("855", f.Current.Character.Attributes["WarehouseMoney"]);
                    var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
                        File.ReadAllBytes(f.CharacterPath), out _));
                    Assert.Equal(700L, saved.Money);
                    Assert.Equal("855", saved.Attributes["WHMoney"]);
                    Assert.Equal("855", saved.Attributes["WarehouseMoney"]);
                });
            };
            try
            {
                var request = new MsgItemUsagePacket(0, 300, MsgItemUsageAction.DepositWarehouse, 0, 0, 0, 0);
                await f.Send(request.ToFrameBytes());
                Assert.Null(observed);
                var frames = f.Sent.Skip(first).ToArray();
                Assert.Equal(3, frames.Length);
                var money = new LegacyMsgUpdatePacket(checked((uint)f.Current.Character.Uid),
                    [new(LegacyMsgUpdateDataType.Money, 700)]).ToFrameBytes();
                Assert.Equal(money, frames[0]); Assert.Equal(money, frames[1]);
                Assert.Equal(request.ToFrameBytes(), frames[2]);
                Assert.Equal(itemBytes, File.ReadAllBytes(f.ItemsPath));
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
            }
            finally { f.OnSend = null; probe.RefuseEnumeration = false; }
        });
}
