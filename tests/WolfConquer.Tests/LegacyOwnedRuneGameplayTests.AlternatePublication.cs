using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneGameplayTests
{
    [Fact]
    public Task ActualAlternateTogglePublishesTheNativeModeAndRuneSelectionTogether() => Run(async f =>
    {
        await f.DispatchAsync(F.Equip(F.First, 122));
        var equipment = f.Native.Equipment!; var files = f.Data.Files(); int first = f.Sent.Count;
        Assert.False(f.Current.AlternateEquipmentActive); Assert.False(equipment.Alternante);
        foreach (var active in new[] { true, false })
        {
            await f.DispatchAsync(new MsgItemUsagePacket(0, 0,
                active ? MsgItemUsageAction.Alternante : MsgItemUsageAction.UnAlternante, 0, 0, 0, 0));
            Assert.Same(equipment, f.Native.Equipment);
            Assert.Equal(active, f.Current.AlternateEquipmentActive); Assert.Equal(active, equipment.Alternante);
            Assert.Equal(active, f.Native.Rune!.WornObjects.Any(row => row.Uid == F.First));
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Native.OrdinarySpellBinding!));
            f.Data.AssertFiles(files); f.AssertConverged();
        }
        Assert.Equal(first, f.Sent.Count);
    });

    [Fact]
    public Task BoundAlternatePublicationRejectsAnAlreadyDriftedNativeModeBeforeChangingAnyState() => Run(async f =>
    {
        await f.DispatchAsync(F.Equip());
        var before = f.Current; var equipment = f.Native.Equipment!;
        var files = f.Data.Files(); int first = f.Sent.Count;
        equipment.Alternante = true;
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                f.Store.Set(f.Connection, before with { AlternateEquipmentActive = true }, out _));
            Assert.Same(before, f.Current); Assert.True(equipment.Alternante);
            Assert.Equal(first, f.Sent.Count); f.Data.AssertFiles(files);
            Assert.False(f.Runtime.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        }
        finally { equipment.Alternante = false; }
        f.AssertConverged();
    });
}
