using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCanUseSpellTests
{
    [Theory]
    [InlineData("attack")]
    [InlineData("client")]
    [InlineData("database")]
    public Task NullApiInputsRejectWithoutTouchingTheExistingClient(string missing)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        var before = Capture(f);
        Assert.False(LegacyNativeCanUseSpell.Verified(missing == "attack" ? null! : f.Attack,
            missing == "client" ? null! : f.Owner, missing == "database" ? null! : f.Rows,
            out var spell, out var row));
        Assert.Null(spell); Assert.Null(row); Unchanged(f, before);
        Assert.Equal("", f.Log.ToString());
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AdmissionDoesNotDrawOrReplaceSharedRandomState(bool accepted)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.UseStamina = 3;
        if (!accepted) f.Player.Stamina = 2;
        var random = new LegacyNativeSafeRandom(41539);
        LegacyNativePoolRandom.GetRandom = random;
        var next = new LegacyNativeSafeRandom(41539).Next();
        Assert.Equal(accepted, f.Run(out _, out _));
        Assert.Same(random, LegacyNativePoolRandom.GetRandom);
        Assert.Equal(next, random.Next());
    });

    [Theory]
    [InlineData("stamina")]
    [InlineData("mana")]
    [InlineData("cooldown")]
    [InlineData("level")]
    [InlineData("rune")]
    [InlineData("sigil")]
    [InlineData("range")]
    public Task EveryRejectionPreservesWeaponSoulResourcesCooldownAndExistingPackets(string reason)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Owner.FullLoading = true;
        f.Player.Mana = 100; // Keep a real pre-existing queued update in the snapshot.
        f.Player.RandomSpell = 100;
        LegacyNativePoolMagic.WeaponSpells[410] = [100];
        f.Owner.OnSoulSpell = 999;
        f.Spell.UseSpellSoul = 3;
        f.Row.UseStamina = 3;
        f.Row.UseMana = 5;
        f.Row.ColdTime = 1000;
        if (reason == "stamina") f.Player.Stamina = 2;
        else if (reason == "mana") f.Row.UseMana = 101;
        else if (reason == "cooldown") f.Spell.ColdTime = DateTime.Now.AddMinutes(10);
        else if (reason == "level") f.Spell.Level = 3;
        else if (reason == "rune") f.Owner.Rune = null;
        else if (reason == "sigil")
        {
            f.SelectIdentity(16520); f.Owner.MyNinja = null;
            f.Player.RandomSpell = 16520; LegacyNativePoolMagic.WeaponSpells[410] = [16520];
        }
        else f.Row.Type = LegacyNativeMagicType.MagicSort.Attack;
        var before = Capture(f);
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row); Unchanged(f, before);
    });

    [Theory]
    [InlineData((ushort)0, false)]
    [InlineData((ushort)1, true)]
    public Task OnlyPositiveManaNeedsTheRealRuneCalculationOwner(ushort manaCost, bool rejected)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Owner.Rune = null;
        f.Row.UseStamina = 3;
        f.Row.UseMana = manaCost;
        var before = Capture(f);
        Assert.Equal(!rejected, f.Run(out var spell, out var row));
        if (rejected) { Assert.Null(spell); Assert.Null(row); Unchanged(f, before); }
        else { Assert.Same(f.Spell, spell); Assert.Same(f.Row, row); }
        Assert.Equal("", f.Log.ToString());
    });

    [Theory]
    [InlineData(-2147483648, 1000)]
    [InlineData(-1, 1000)]
    [InlineData(0, 1000)]
    [InlineData(250, 750)]
    [InlineData(1000, 0)]
    [InlineData(2147483647, 0)]
    public Task SigilReductionIsBoundedAndCannotOverflowOrBackdateCooldown(int power, int duration)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        SetClassLevel(f.Player, 5000, 15);
        f.SelectIdentity(16520);
        f.Row.ColdTime = 1000;
        f.Owner.MyNinja!.Items[1] = new() { ItemID = 102, Level = 3, Position = 1 };
        LegacyNativeNinjaFile.gouyu_type[1] = new() { ItemID = 102, Level = 3, Power = power };
        var before = DateTime.Now;
        Assert.True(f.Run(out _, out _));
        var after = DateTime.Now;
        Assert.InRange(f.Spell.ColdTime, before.AddMilliseconds(duration), after.AddMilliseconds(duration));
    });

    [Fact]
    public Task MaximumRowCostsDoNotWrapStaminaOrManaIntoAnAcceptedFreeCast()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.UseStamina = byte.MaxValue;
        f.Row.UseMana = ushort.MaxValue;
        f.Player.Stamina = 260;
        f.Player.Mana = ushort.MaxValue;
        f.Player.BitVector.TryAdd((int)LegacyNativeMsgUpdate.Flags.ScurvyBomb, 5, false, 0);
        var before = Capture(f);
        Assert.False(f.Run(out var spell, out var row)); // 255+6 >260, not a byte-wrapped5.
        Assert.Null(spell); Assert.Null(row); Unchanged(f, before);
        f.Player.Stamina = 261;
        Assert.True(f.Run(out spell, out row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Equal((ushort)0, f.Player.Stamina);
        Assert.Equal((ushort)0, f.Player.Mana);
    });

    [Fact]
    public Task ForeignPhysicalPlayerCannotCommitAnotherClientsResources()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        var actual = f.Owner.Player;
        var foreign = NativeSpellTestFixture.CreateOwner(new());
        f.Owner.Player = foreign.Player;
        try
        {
            var before = Capture(f);
            Assert.False(f.Run(out var spell, out var row));
            Assert.Null(spell); Assert.Null(row); Unchanged(f, before);
        }
        finally { f.Owner.Player = actual; }
    });

    [Theory]
    [InlineData("learned-id")]
    [InlineData("metadata-id")]
    [InlineData("metadata-level")]
    [InlineData("negative-cooldown")]
    public Task InconsistentOrMalformedMetadataRejectsWithoutAdmissionEffects(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Row.UseStamina = 3; f.Row.UseMana = 5;
        f.Player.RandomSpell = 100; f.Owner.OnSoulSpell = 999;
        LegacyNativePoolMagic.WeaponSpells[410] = [100];
        if (corruption == "learned-id") f.Spell.ID = 777;
        else if (corruption == "metadata-id") f.Row.ID = 777;
        else if (corruption == "metadata-level") f.Row.Level = 3;
        else f.Row.ColdTime = -1;
        var before = Capture(f);
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row); Unchanged(f, before);
    });
}
