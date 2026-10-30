using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneGameplayTests
{
    [Theory]
    [InlineData("missing")] [InlineData("same-client-replacement")] [InlineData("foreign-owner")]
    [InlineData("missing-table-row")] [InlineData("null-table-row")] [InlineData("slot-overflow")]
    public Task NewRuneEquipRequiresTheActualPublishedBeastAndValidCurrentMetadata(string fault) => Run(async f =>
    {
        var actual = f.Native.Beasts!; var owner = actual.Owner;
        var table = LegacyNativeBeastsAtrribute.Attributes; var row = table[1]; var slots = row.YellowRuneSlots;
        try
        {
            switch (fault)
            {
                case "missing": f.Native.Beasts = null!; break;
                case "same-client-replacement": f.Native.Beasts = new LegacyNativeBeastsRuntime(f.Native); break;
                case "foreign-owner": actual.Owner = new LegacyNativeGameClientRuntime(); break;
                case "missing-table-row": table.Clear(); break;
                case "null-table-row": table[1] = null!; break;
                case "slot-overflow": row.YellowRuneSlots = byte.MaxValue; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            // Slot102 is otherwise admitted by the actual original five slots;
            // an unrelated ordinary slot rejection cannot mask the owner check.
            await f.AssertRefusedAsync(() => f.DispatchAsync(F.Equip()));
        }
        finally { f.Native.Beasts = actual; actual.Owner = owner; row.YellowRuneSlots = slots; table.Clear(); table.Add(1, row); }
    });

    [Theory]
    [InlineData((byte)0)] [InlineData((byte)1)]
    public Task RuneStorageRetiresTheActualLastProviderThroughTheOwnedSpellTransaction(byte storageType) => Run(async f =>
    {
        if (storageType == 0) await f.SendAsync(new LegacyRuneStoragePacket(1, F.Shared, 0).ToFrameBytes());
        await f.DispatchAsync(F.Equip()); f.AssertMarker("14160:16");
        var ledger = File.ReadAllBytes(f.Data.State); int first = f.Sent.Count;
        await f.SendAsync(new LegacyRuneStoragePacket(storageType, F.First, 0).ToFrameBytes());
        Assert.Equal(storageType == 0 ? (ushort)212 : (ushort)211, Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == F.First).Position);
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == F.FirstSpell); f.AssertConverged(); f.AssertMarker("");
        Assert.Equal(ledger, File.ReadAllBytes(f.Data.State)); Assert.Equal(12, f.Sent.Count - first);
        Assert.Equal(new MsgActionPacket { ObjId = F.Uid, DwParam = F.FirstSpell, Type = (MsgActionType)109 }.ToFrameBytes(), f.Sent[first]);
        Assert.Equal(new LegacyRuneStoragePacket(storageType, F.First, 0).ToFrameBytes(), f.Sent[first + 1]);
        LegacySelectedEquipmentQueryAssertions.NeutralQuery(f.Sent, first + 2, F.Uid);
    });
}
