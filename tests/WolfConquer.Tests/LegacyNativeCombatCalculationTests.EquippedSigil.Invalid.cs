using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData("null-items")]
    [InlineData("null-item-row")]
    [InlineData("null-item-tail")]
    [InlineData("missing-metadata")]
    [InlineData("null-metadata")]
    [InlineData("null-metadata-row")]
    [InlineData("null-metadata-tail")]
    [InlineData("wrong-item-id")]
    [InlineData("wrong-level")]
    public Task RequiredEquippedSigilInvalidStateRejectsBeforeDamageLuckySendsOrRng(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        Set(s.Player, "_class", 5000u);
        s.Client.PerfectionStatus.LuckyStrike = 100;
        s.Player.KylinSigil = 2;
        s.Player.SendString(s.Native.Packet, LegacyNativeMsgStringPacket.StringID.Effect, false, "prior-sigil");
        var item = InstallPhysicalWhirlSigil(s);
        CorruptPhysicalWhirlSigil(s, item, corruption);
        var spell = new LegacyNativeMagicType.Magic { ID = 16420, Damage = 5 };
        var beforeSpell = PhysicalAdmissionSpellValues(spell);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        var resources = (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell);
        var sentinel = PhysicalAdmissionSentinel();
        var result = sentinel;
        var beforeResult = PhysicalAdmissionResultValues(result);

        Assert.Throws<InvalidDataException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out result));

        Assert.Same(sentinel, result);
        Assert.Equal(beforeResult, PhysicalAdmissionResultValues(result));
        Assert.Equal(beforeSpell, PhysicalAdmissionSpellValues(spell));
        Assert.Equal(resources, (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell));
        prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    private static LegacyNativeNinja.Item InstallPhysicalWhirlSigil(Scope scope)
    {
        var item = new LegacyNativeNinja.Item { ItemID = 404, Position = 1, Level = 1 };
        scope.Client.MyNinja!.Items[900] = item;
        LegacyNativeNinjaFile.gouyu_type[700] = new() { ItemID = 404, Level = 1, Power = -1, Damage = 20 };
        return item;
    }

    private static void CorruptPhysicalWhirlSigil(Scope scope, LegacyNativeNinja.Item item, string corruption)
    {
        switch (corruption)
        {
            case "null-items": scope.Client.MyNinja!.Items = null!; break;
            case "null-item-row": scope.Client.MyNinja!.Items[900] = null!; break;
            case "null-item-tail":
                scope.Client.MyNinja!.Items[901] = new();
                var keys = scope.Client.MyNinja.Items.ToArray().Select(pair => pair.Key).ToArray();
                scope.Client.MyNinja.Items[keys[0]] = item;
                scope.Client.MyNinja.Items[keys[1]] = null!;
                Assert.Same(item, scope.Client.MyNinja.Items.Values.First());
                break;
            case "missing-metadata": LegacyNativeNinjaFile.gouyu_type.Clear(); break;
            case "null-metadata": LegacyNativeNinjaFile.gouyu_type = null!; break;
            case "null-metadata-row": LegacyNativeNinjaFile.gouyu_type[700] = null!; break;
            case "null-metadata-tail": LegacyNativeNinjaFile.gouyu_type[701] = null!; break;
            case "wrong-item-id": LegacyNativeNinjaFile.gouyu_type[700].ItemID = 403; break;
            case "wrong-level": LegacyNativeNinjaFile.gouyu_type[700].Level = 2; break;
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }
    }
}
