using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using NinjaType = WolfConquer.Core.World.LegacyNativeNinja.ItemType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCanUseSpellTests
{
    [Fact]
    public Task FutureCooldownRejectsWithoutDebitsOutputsOrQueuedUpdates()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.UseStamina = 3;
        f.Row.UseMana = 5;
        f.Row.ColdTime = 1000;
        f.Spell.ColdTime = DateTime.Now.AddMinutes(10);
        f.Owner.FullLoading = true;
        var before = Capture(f);
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row); Unchanged(f, before);
        Assert.Equal("", f.Log.ToString());
    });

    [Theory]
    [InlineData((ushort)15710)]
    [InlineData((ushort)20280)]
    public Task MetadataExclusionsPreserveSuccessfulBypassWithoutNinjaLookup(ushort metadataId)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.ColdTime = 1000;
        f.SelectIdentity(metadataId);
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
    public Task RegisteredBypassFlagsRetainSuccessWhileRawBitsDoNotBypass(int flagValue)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        var flag = (Flag)flagValue;
        f.Row.ColdTime = 1000;
        f.Spell.ColdTime = DateTime.MaxValue;
        f.Owner.MyNinja = null;
        f.Player.BitVector.Add((int)flag);
        var before = Capture(f);
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row); Unchanged(f, before);
        Assert.Contains("Spell admission preparation", f.Log.ToString());
        f.Player.BitVector.Remove((int)flag); // Restore coherent empty fixture state before the positive grant.
        Assert.True(f.Player.BitVector.TryAdd((int)flag, 7, false, 0));
        Assert.True(f.Run(out spell, out row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal(DateTime.MaxValue, f.Spell.ColdTime);
        Assert.False(f.Spell.IsSpellWithColdTime);
    });

    [Fact]
    public Task UnrelatedSpellDoesNotRequireNinjaOwnerAndCanCommitOrdinaryCosts()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.ColdTime = 1000;
        f.Row.UseStamina = 3;
        f.Row.UseMana = 5;
        f.Owner.MyNinja = null;
        f.SelectIdentity(777);
        var before = DateTime.Now;
        Assert.True(f.Run(out var spell, out var row));
        var after = DateTime.Now;
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal((ushort)97, f.Player.Stamina);
        Assert.Equal((ushort)95, f.Player.Mana);
        Assert.True(f.Spell.IsSpellWithColdTime);
        Assert.InRange(f.Spell.ColdTime, before.AddMilliseconds(1000), after.AddMilliseconds(1000));
        Assert.Equal("", f.Log.ToString());
    });

    [Theory]
    [InlineData((ulong)NinjaType.WildSigilRapid, (ushort)16520)]
    [InlineData((ulong)NinjaType.FlameSigilScorch, (ushort)16540)]
    [InlineData((ulong)NinjaType.DustSigilExtinction, (ushort)16460)]
    [InlineData((ulong)NinjaType.SickleSigilGallop, (ushort)16500)]
    public Task RelevantEquippedSigilRetainsNormalReductionAndCannotBackdateDeadline(ulong typeValue, ushort storedId)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        var type = (NinjaType)typeValue;
        SetClassLevel(f.Player, 5000, 15);
        f.Row.ColdTime = 1000;
        f.SelectIdentity(storedId);
        var equipped = new LegacyNativeNinja.Item { ItemID = (uint)type, Level = 3, Position = 1 };
        f.Owner.MyNinja!.Items[999] = equipped;
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
        Assert.InRange(f.Spell.ColdTime, before, after);
        Assert.Same(equipped, f.Owner.MyNinja.Items[999]);
    });

    [Fact]
    public Task AbsentSigilMetadataUsesZeroReductionButMalformedTableRejectsBeforeCommit()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        SetClassLevel(f.Player, 5000, 15);
        f.Row.ColdTime = 1000;
        f.SelectIdentity(16520);
        f.Row.UseStamina = 3;
        f.Row.UseMana = 5;
        f.Owner.MyNinja!.Items[1] = new() { ItemID = 102, Level = 3, Position = 1 };
        var before = DateTime.Now;
        Assert.True(f.Run(out _, out _));
        var after = DateTime.Now;
        Assert.InRange(f.Spell.ColdTime, before.AddMilliseconds(1000), after.AddMilliseconds(1000));
        f.Spell.ColdTime = DateTime.MinValue;
        f.Spell.IsSpellWithColdTime = false;
        LegacyNativeNinjaFile.gouyu_type = null!;
        var snapshot = Capture(f);
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row); Unchanged(f, snapshot);
        Assert.Contains("Spell admission preparation", f.Log.ToString());
    });

    [Fact]
    public Task UnrelatedStoredIdDoesNotEnumerateMalformedNinjaItems()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        SetClassLevel(f.Player, 5000, 15);
        f.Row.ColdTime = 1000;
        f.SelectIdentity(777);
        f.Owner.MyNinja!.Items = null!;
        Assert.True(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.True(f.Spell.IsSpellWithColdTime);
        Assert.Equal("", f.Log.ToString());
    });
}
