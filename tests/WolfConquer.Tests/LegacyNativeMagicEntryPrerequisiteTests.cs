using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeMagicEntryPrerequisiteTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckSpellsUsesOnlyItsExplicitLoadGateAndCompleteNativeEnum(bool loaded)
    {
        using var scope = new NativeCombatGearQueryScope(loaded);
        LegacyNativeCombatGearTable.Info = null!; // No row lookup, registration or readiness inference.
        var defined = Enum.GetValues<Gear.SpellID>().Select(value => (ushort)value).ToHashSet();
        for (int id = ushort.MinValue; id <= ushort.MaxValue; id++)
            Assert.Equal(loaded && defined.Contains((ushort)id), Gear.CheckSpells((ushort)id));
        Assert.Null(LegacyNativeCombatGearTable.Info);
    }

    [Fact]
    public void InteractQueryCopiesEveryNativeFieldIntoAnIndependentMutableClass()
    {
        var original = new LegacyNativeInteractQuery
        {
            UID = uint.MaxValue, OpponentUID = 2, X = ushort.MaxValue, Y = 4,
            AtkType = 5, Damage = int.MinValue, CombatGear = ulong.MaxValue,
            KilledMonster = -7, SpellLevel = 8, SpellID = 9,
            ResponseDamage = 10, Effect = uint.MaxValue, Unknown = 12,
        };
        var copy = LegacyNativeInteractQuery.ShallowCopy(original);
        Assert.NotSame(original, copy);
        Assert.False(typeof(LegacyNativeInteractQuery).IsValueType);
        Assert.Equal(uint.MaxValue, copy.UID);
        Assert.Equal(2u, copy.OpponentUID);
        Assert.Equal(ushort.MaxValue, copy.X);
        Assert.Equal((ushort)4, copy.Y);
        Assert.Equal((ushort)5, copy.AtkType);
        Assert.Equal(int.MinValue, copy.Damage);
        Assert.Equal(ulong.MaxValue, copy.CombatGear);
        Assert.Equal(-7, copy.KilledMonster);
        Assert.Equal((ushort)8, copy.SpellLevel);
        Assert.Equal((ushort)9, copy.SpellID);
        Assert.Equal(10u, copy.ResponseDamage);
        Assert.Equal(uint.MaxValue, copy.Effect);
        Assert.Equal(12u, copy.Unknown);
        copy.Damage = 100;
        original.UID = 13;
        Assert.Equal(int.MinValue, original.Damage);
        Assert.Equal(uint.MaxValue, copy.UID);
    }

    [Fact]
    public void InteractQueryKeepsImplicitZeroDefaultsAndLiteralNullCopyFault()
    {
        var query = new LegacyNativeInteractQuery();
        foreach (var field in typeof(LegacyNativeInteractQuery).GetFields())
            Assert.Equal(Activator.CreateInstance(field.FieldType), field.GetValue(query));
        Assert.Throws<NullReferenceException>(() => LegacyNativeInteractQuery.ShallowCopy(null!));
    }

    [Fact]
    public void GuildMemberIsAnActualIndependentPhysicalReferenceWithoutMembershipBirth()
    {
        var owner = new LegacyNativeGameClientRuntime();
        Assert.Null(owner.Player.MyGuildMember);
        Assert.Null(owner.Player.MyGuild);
        var member = new LegacyNativeGuildRuntime.Member { Class = 7001 };
        owner.Player.MyGuildMember = member; // Explicit component prestate, not AddPlayer or a Class setter.
        member.Class = uint.MaxValue;
        Assert.Same(member, owner.Player.MyGuildMember);
        Assert.Equal(uint.MaxValue, owner.Player.MyGuildMember.Class);
        Assert.Equal(0u, owner.Player.Class);
        Assert.Null(owner.Player.MyGuild);
    }
}
