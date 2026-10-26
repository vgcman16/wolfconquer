using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    public static IEnumerable<object[]> RequiredPhysicalOwnerCases()
    {
        foreach (var corruption in new[]
        {
            "missing-rune", "unowned-rune", "foreign-rune", "rebound-rune",
            "missing-gear", "foreign-gear", "rebound-gear", "missing-family", "missing-name",
        })
        {
            yield return [corruption, (ushort)16330];
        }

        foreach (var spellId in new ushort[] { 16420, 16430 })
        {
            foreach (var corruption in new[] { "missing-ninja", "unowned-ninja", "foreign-ninja", "rebound-ninja" })
            {
                yield return [corruption, spellId];
            }
        }
    }

    [Theory]
    [MemberData(nameof(RequiredPhysicalOwnerCases))]
    public Task RequiredPhysicalOwnersRejectBeforeDamageCounterSharedSpellRngOrSends(string corruption, ushort spellId)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        s.Player.KylinSigil = 2;
        s.Client.PerfectionStatus.LuckyStrike = 100;
        s.Player.SendString(s.Native.Packet, LegacyNativeMsgStringPacket.StringID.Effect, false, "prior-required-owner");
        var spell = PhysicalAdmissionSpell();
        spell.ID = spellId;
        CorruptRequiredPhysicalOwner(s.Client, monster, corruption);
        var owners = new object?[] { s.Client.Rune, s.Client.CombatGear, s.Client.MyNinja, monster.Family, monster.Name };
        var vector = new PhysicalAdmissionVectorSnapshot(s.Player);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        var spellBefore = PhysicalAdmissionSpellValues(spell);
        var resources = (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell);
        var sentinel = PhysicalAdmissionSentinel();
        var result = sentinel;
        var resultBefore = PhysicalAdmissionResultValues(result);

        Assert.Throws<InvalidDataException>(() => LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out result));

        Assert.Same(sentinel, result);
        Assert.Equal(resultBefore, PhysicalAdmissionResultValues(result));
        Assert.Equal(spellBefore, PhysicalAdmissionSpellValues(spell));
        Assert.Equal(resources, (s.Player.Stamina, s.Player.Mana, s.Client.OnSoulSpell));
        var current = new object?[] { s.Client.Rune, s.Client.CombatGear, s.Client.MyNinja, monster.Family, monster.Name };
        for (var index = 0; index < owners.Length; index++) Assert.Same(owners[index], current[index]);
        vector.AssertUnchanged();
        prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Theory]
    [InlineData("missing-rune")]
    [InlineData("missing-gear")]
    [InlineData("missing-ninja")]
    [InlineData("missing-family")]
    [InlineData("missing-name")]
    public Task FloorMonsterDoesNotRequireUnusedCombatOwnersOrMetadata(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        monster.IsFloor = true;
        var spell = new LegacyNativeMagicType.Magic { ID = 16420, Damage = 5 };
        CorruptRequiredPhysicalOwner(s.Client, monster, corruption);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);

        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out var result);

        Assert.Equal(monster.UID, result.UID);
        Assert.Equal(2u, result.Damage);
        Assert.Equal(1u, result.Hit);
        Assert.Equal(LegacyNativeMsgAttackPacket.AttackEffect.None, result.Effect);
        Assert.Equal(5f, spell.Damage);
        prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });

    [Theory]
    [InlineData(500, 1200u)]
    [InlineData(16330, 1800u)]
    [InlineData(16540, 1200u)]
    public Task UnrelatedPhysicalSpellsDoNotRequireOrProbeMissingNinjaOwner(int spellId, uint expected)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var monster = CombatMonster(s);
        s.Client.MyNinja = null;

        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster,
            new LegacyNativeMagicType.Magic { ID = (ushort)spellId, Damage = 100 }, out var result);

        Assert.Equal(expected, result.Damage);
        Assert.Null(s.Client.MyNinja);
        Assert.Empty(s.Queue);
        AssertPhysicalAdmissionFourDraws();
    });

    private static void CorruptRequiredPhysicalOwner(
        LegacyNativeGameClientRuntime client, LegacyNativeMonsterRole monster, string corruption)
    {
        switch (corruption)
        {
            case "missing-rune": client.Rune = null; break;
            case "unowned-rune": client.Rune = new LegacyNativeRuneItemStorage(null); break;
            case "foreign-rune": client.Rune = new LegacyNativeRuneItemStorage(new LegacyNativeGameClientRuntime()); break;
            case "rebound-rune":
                client.Rune = new LegacyNativeRuneItemStorage(new LegacyNativeGameClientRuntime()) { Owner = client };
                break;
            case "missing-gear": client.CombatGear = null; break;
            case "foreign-gear": client.CombatGear = new LegacyNativeMyCombatGear(new LegacyNativeGameClientRuntime()); break;
            case "rebound-gear":
                client.CombatGear = new LegacyNativeMyCombatGear(new LegacyNativeGameClientRuntime()) { user = client };
                break;
            case "missing-ninja": client.MyNinja = null; break;
            case "unowned-ninja": client.MyNinja = new LegacyNativeNinja { user = client }; break;
            case "foreign-ninja": client.MyNinja = new LegacyNativeNinja(new LegacyNativeGameClientRuntime()); break;
            case "rebound-ninja":
                client.MyNinja = new LegacyNativeNinja(new LegacyNativeGameClientRuntime()) { user = client };
                break;
            case "replace-gear": client.CombatGear = new LegacyNativeMyCombatGear(client); break;
            case "missing-family": monster.Family = null!; break;
            case "missing-name": monster.Name = null; break;
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }
    }
}
