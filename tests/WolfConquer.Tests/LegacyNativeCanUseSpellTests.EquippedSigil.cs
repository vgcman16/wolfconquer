using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCanUseSpellTests
{
    [Theory]
    [InlineData("null-items")]
    [InlineData("null-item-row")]
    [InlineData("missing-metadata")]
    [InlineData("null-metadata")]
    [InlineData("null-metadata-row")]
    [InlineData("null-metadata-tail")]
    [InlineData("wrong-level")]
    [InlineData("foreign-owner")]
    public Task InvalidRequiredCooldownSigilCannotDebitAuthorizeOrPublishAsOrdinaryAbsence(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        SetClassLevel(f.Player, 5000, 15);
        f.SelectIdentity(16520);
        f.Row.ColdTime = 1000;
        f.Row.UseStamina = 3;
        f.Row.UseMana = 5;
        f.Spell.UseSpellSoul = 1;
        f.Player.RandomSpell = 16520;
        LegacyNativePoolMagic.WeaponSpells[410] = [16520];
        f.Owner.MyNinja!.Items[999] = new() { ItemID = 102, Level = 3, Position = 1 };
        LegacyNativeNinjaFile.gouyu_type[777] = new() { ItemID = 102, Level = 3, Power = 250 };
        switch (corruption)
        {
            case "null-items": f.Owner.MyNinja.Items = null!; break;
            case "null-item-row": f.Owner.MyNinja.Items[999] = null!; break;
            case "missing-metadata": LegacyNativeNinjaFile.gouyu_type.Clear(); break;
            case "null-metadata": LegacyNativeNinjaFile.gouyu_type = null!; break;
            case "null-metadata-row": LegacyNativeNinjaFile.gouyu_type[777] = null!; break;
            case "null-metadata-tail": LegacyNativeNinjaFile.gouyu_type[778] = null!; break;
            case "wrong-level": LegacyNativeNinjaFile.gouyu_type[777].Level = 4; break;
            case "foreign-owner": f.Owner.MyNinja.user = new LegacyNativeGameClientRuntime(); break;
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }
        var before = Capture(f);

        Assert.False(f.Run(out var spell, out var row));

        Assert.Null(spell);
        Assert.Null(row);
        Unchanged(f, before);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task GenuineUnequippedOrNonNinjaCooldownDoesNotReadOptionalMissingMetadata(bool nonNinja)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        SetClassLevel(f.Player, nonNinja ? 2000u : 5000u, 15);
        f.SelectIdentity(16520);
        f.Row.ColdTime = 1000;
        f.Row.UseStamina = 3;
        LegacyNativeNinjaFile.gouyu_type = null!;
        if (nonNinja)
        {
            f.Owner.Equipment = null;
            f.Owner.MyNinja!.Items = null!;
        }
        var before = DateTime.Now;

        Assert.True(f.Run(out var spell, out var row));

        var after = DateTime.Now;
        Assert.Same(f.Spell, spell);
        Assert.Same(f.Row, row);
        Assert.Equal((ushort)97, f.Player.Stamina);
        Assert.Equal((ushort)100, f.Player.Mana);
        Assert.InRange(f.Spell.ColdTime, before.AddMilliseconds(1000), after.AddMilliseconds(1000));
        Assert.Equal((ushort)2176, BitConverter.ToUInt16(Assert.Single(f.Queue), 2));
    });
}
