using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using NinjaType = WolfConquer.Core.World.LegacyNativeNinja.ItemType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCanUseSpellTests
{
    [Fact]
    public Task FutureColdTimeRejectsOnlyAfterBothResourceDebitsAndRetainsOutReferences() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.UseStamina = 3;
        f.Row.UseMana = 5;
        f.Row.ColdTime = 1000;
        var prior = DateTime.Now.AddMinutes(10);
        f.Spell.ColdTime = prior;
        Assert.False(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal((ushort)97, f.Player.Stamina);
        Assert.Equal((ushort)95, f.Player.Mana);
        Assert.Equal(prior, f.Spell.ColdTime);
        Assert.False(f.Spell.IsSpellWithColdTime);
        Assert.Equal(Update(17, LegacyMsgUpdateDataType.Stamina, 97), Assert.Single(f.Queue));
        Assert.Equal("", f.Log.ToString());
    });

    [Theory]
    [InlineData((ushort)15710)]
    [InlineData((ushort)20280)]
    public Task TwoMetadataIdExclusionsSkipColdTimeAndMissingNinjaRegardlessOfTheStoredSpellId(ushort metadataId) => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.ColdTime = 1000;
        f.Row.ID = metadataId;
        f.Spell.ID = 777;
        f.Spell.ColdTime = DateTime.MaxValue;
        f.Owner.MyNinja = null;
        Assert.True(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal(DateTime.MaxValue, f.Spell.ColdTime);
        Assert.False(f.Spell.IsSpellWithColdTime);
    });

    [Theory]
    [InlineData((int)Flag.CannonBarrage)]
    [InlineData((int)Flag.BlackbeardsRage)]
    public Task RegisteredBypassFlagsSkipColdTimeButRawBitsDoNot(int flagValue) => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        var flag = (Flag)flagValue;
        f.Row.ColdTime = 1000;
        f.Spell.ColdTime = DateTime.MaxValue;
        f.Owner.MyNinja = null;
        f.Player.BitVector.Add((int)flag);
        Assert.False(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        f.Player.BitVector.TryAdd((int)flag, 7, false, 0);
        Assert.True(f.Run(out spell, out row));
        Assert.Equal(DateTime.MaxValue, f.Spell.ColdTime);
        Assert.False(f.Spell.IsSpellWithColdTime);
    });

    [Fact]
    public Task MissingNinjaIsReadEvenForAnUnrelatedStoredIdAndCatchDoesNotUndoCosts() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.ColdTime = 1000;
        f.Row.UseStamina = 3;
        f.Row.UseMana = 5;
        f.Owner.MyNinja = null;
        f.Spell.ID = 777; // The first TryGetValueEquip is left of the ID comparison.
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row);
        Assert.Equal((ushort)97, f.Player.Stamina);
        Assert.Equal((ushort)95, f.Player.Mana);
        Assert.False(f.Spell.IsSpellWithColdTime);
        Assert.Equal(default, f.Spell.ColdTime);
        Assert.Contains("NullReferenceException", f.Log.ToString());
    });

    [Theory]
    [InlineData((ulong)NinjaType.WildSigilRapid, (ushort)16520)]
    [InlineData((ulong)NinjaType.FlameSigilScorch, (ushort)16540)]
    [InlineData((ulong)NinjaType.DustSigilExtinction, (ushort)16460)]
    [InlineData((ulong)NinjaType.SickleSigilGallop, (ushort)16500)]
    public Task EachActualEquippedNinjaRowReducesColdTimeUsingStoredIdAndCurrentMetadataPower(ulong typeValue, ushort storedId)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        var type = (NinjaType)typeValue;
        SetClassLevel(f.Player, 5000, 15);
        f.Row.ColdTime = 1000;
        f.Spell.ID = storedId;
        var equipped = new LegacyNativeNinja.Item { ItemID = (uint)type, Level = 3, Position = 1 };
        f.Owner.MyNinja!.Items[999] = equipped; // Dictionary key is not ItemID.
        var metadata = new LegacyNativeNinjaFile.NinjaSprintItem { ItemID = (uint)type, Level = 3, Power = 250 };
        LegacyNativeNinjaFile.gouyu_type[777] = metadata;
        var before = DateTime.Now;
        Assert.True(f.Run(out var spell, out var row));
        var after = DateTime.Now;
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.True(f.Spell.IsSpellWithColdTime);
        Assert.InRange(f.Spell.ColdTime, before.AddMilliseconds(750), after.AddMilliseconds(750));
        Assert.Same(metadata, equipped.DBItem);
        metadata.Power = 1500;
        f.Spell.ColdTime = DateTime.MinValue;
        before = DateTime.Now;
        Assert.True(f.Run(out spell, out row));
        after = DateTime.Now;
        Assert.InRange(f.Spell.ColdTime, before.AddMilliseconds(-500), after.AddMilliseconds(-500));
        Assert.Same(equipped, f.Owner.MyNinja.Items[999]);
    });

    [Fact]
    public Task MissingNinjaMetadataRowUsesAFreshZeroPowerRowButNullTableFaultsBeforeColdTimeCommit() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        SetClassLevel(f.Player, 5000, 15);
        f.Row.ColdTime = 1000;
        f.Spell.ID = 16520;
        f.Owner.MyNinja!.Items[1] = new() { ItemID = 102, Level = 3, Position = 1 };
        var before = DateTime.Now;
        Assert.True(f.Run(out _, out _));
        var after = DateTime.Now;
        Assert.InRange(f.Spell.ColdTime, before.AddMilliseconds(1000), after.AddMilliseconds(1000));
        f.Spell.ColdTime = DateTime.MinValue;
        f.Spell.IsSpellWithColdTime = false;
        LegacyNativeNinjaFile.gouyu_type = null!;
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row);
        Assert.False(f.Spell.IsSpellWithColdTime);
        Assert.Equal(DateTime.MinValue, f.Spell.ColdTime);
        Assert.Contains("NullReferenceException", f.Log.ToString());
    });

    [Fact]
    public Task FirstNinjaQueryPrecedesTheUnrelatedStoredIdCheck() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        SetClassLevel(f.Player, 5000, 15);
        f.Row.ColdTime = 1000;
        f.Spell.ID = 777;
        f.Owner.MyNinja!.Items = null!;
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row);
        Assert.Contains("NullReferenceException", f.Log.ToString());
        Assert.False(f.Spell.IsSpellWithColdTime);
    });
}
