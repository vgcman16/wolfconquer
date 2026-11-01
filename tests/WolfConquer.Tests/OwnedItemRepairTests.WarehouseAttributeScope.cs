using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedItemRepairTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task AmbiguousUnrelatedPersistenceFieldsRefuseRealSilverBankWithoutFilesCurrencyOrSuccess(bool deposit) =>
        WithWarehouseRepair(false, async f =>
        {
            await f.Login();
            var current = f.Current;
            var fields = new Dictionary<string, string>(current.Character.Attributes, StringComparer.Ordinal)
            { ["TestMarker"] = "first" };
            f.Store.Set(f.Connection, current with { Character = current.Character with { Attributes = fields } });
            var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
            var request = new MsgItemUsagePacket(0, 100, deposit ? MsgItemUsageAction.DepositWarehouse :
                MsgItemUsageAction.WarehouseWithdraw, 1234, 0, 0, 0);

            // Declared negative escape after genuine publication; admission
            // remains strict while the real bank action must reject the drift.
            fields.Add("testmarker", "second");
            try
            {
                await f.Send(request.ToFrameBytes());

                Assert.Same(before, f.Current); Assert.Same(fields, f.Current.Character.Attributes);
                Assert.Equal((1000L, 555L), (f.Native.Player.Money, f.Native.Player.WHMoney));
                Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
                Assert.Contains("warehouseMoneyCharacterFieldsAmbiguous", f.Log.ToString(), StringComparison.Ordinal);
                Assert.DoesNotContain("Frame exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            finally { fields.Remove("testmarker"); }
        });
}
