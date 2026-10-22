using System.Reflection;
using System.Reflection.Emit;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearEntryTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(150, 150)]
    [InlineData(255, 255)]
    [InlineData(256, 0)]
    [InlineData(65535, 255)]
    public void EntryLevelIsTheNativeByteCastOfPhysicalUShortPrestate(int saved, int expected)
    {
        using var scope = new NativeCombatGearQueryScope(false);
        var owner = PopulatedOwner();
        owner.FullLoading = true;
        PhysicalPrestate(owner.Player, 2001, (ushort)saved, 1);
        var entry = new LegacyNativeMyCombatGear(owner).GetEntry();
        Assert.Equal((ushort)saved, owner.Player.Level);
        Assert.Equal((byte)expected, entry.Level);
        Assert.Null(owner.Socket);
        // Deliberately reflective prestate; native Level setter caps/side effects are NOT ported here.
    }

    [Theory]
    [InlineData(0u, 907u)]
    [InlineData(1u, 1u)]
    [InlineData(uint.MaxValue, uint.MaxValue)]
    public void OnlyNonzeroSpecialWingOverridesThePhysicalOrdinaryWing(uint special, uint expected)
    {
        using var scope = new NativeCombatGearQueryScope(false);
        var owner = PopulatedOwner();
        owner.Player.SpecialWingID = special;
        var entry = new LegacyNativeMyCombatGear(owner).GetEntry();
        Assert.Equal(expected, entry.Wing);
        Assert.Equal(907u, owner.Player.WingId);
        Assert.Equal(special, owner.Player.SpecialWingID);
    }

    [Fact]
    public void IsOpenUsesCurrentPhysicalClassAndThisGearValuesNotBaseClassOrClientCombatGearSlot()
    {
        using var scope = new NativeCombatGearQueryScope();
        var owner = PopulatedOwner();
        var gear = new LegacyNativeMyCombatGear(owner);
        gear.SetClass(7051);
        var warrior = Equipped(Gear.TypeID.Dragonhowl);
        var pirate = Equipped(Gear.TypeID.Pirate2);
        gear.Items[Gear.TypeID.Pirate1] = warrior;
        gear.Items[Gear.TypeID.Dragonhowl] = pirate;
        owner.CombatGear = new LegacyNativeMyCombatGear(owner);
        owner.CombatGear.Items[Gear.TypeID.Bloodlust] = Equipped(Gear.TypeID.Bloodlust);

        Assert.Same(warrior, gear.isOpen());
        Assert.True(gear.isOpen(Gear.TypeID.Dragonhowl));
        Assert.False(gear.isOpen(Gear.TypeID.Pirate1));
        Assert.Equal((uint)Gear.TypeID.Dragonhowl, gear.GetEntry().WeaponWar);

        PhysicalPrestate(owner.Player, 7001, 140, 1);
        Assert.Same(pirate, gear.isOpen());
        Assert.True(gear.isOpen(Gear.TypeID.Pirate2));
        Assert.False(gear.isOpen(Gear.TypeID.Dragonhowl));
        Assert.Equal(7001u, gear.GetEntry().Class);
        Assert.Equal((uint)Gear.TypeID.Pirate2, gear.GetEntry().WeaponWar);
        pirate.Equip = 2;
        Assert.Null(gear.isOpen());
        Assert.False(gear.isOpen(Gear.TypeID.Pirate2));
        Assert.Equal(0u, gear.GetEntry().WeaponWar);
        Assert.Equal(7051u, gear.GetClass());
        Assert.NotSame(gear, owner.CombatGear);
    }

    [Fact]
    public void DisabledMetadataGatesIsOpenWithoutRepairButLoadedMatchingClassReachesNullItems()
    {
        using var scope = new NativeCombatGearQueryScope(false);
        var owner = PopulatedOwner();
        var gear = new LegacyNativeMyCombatGear(owner) { Items = null! };
        Assert.Null(gear.isOpen());
        Assert.False(gear.isOpen(Gear.TypeID.Dragonhowl));
        Assert.Equal(2001u, gear.GetEntry().Class);
        Assert.Equal(0u, gear.GetEntry().WeaponWar);
        WolfConquer.Core.Data.LegacyNativeCombatGearTable.m_Load = true;
        Assert.Throws<NullReferenceException>(() => gear.isOpen());
        Assert.Throws<NullReferenceException>(() => gear.GetEntry());
        Assert.Null(gear.Items);
        Assert.Equal(2001u, owner.Player.Class);
    }

    [Fact]
    public void EntryRetainsTwoActualIsOpenCallSitesWithoutAnOverridableTestSeam()
    {
        var type = typeof(LegacyNativeMyCombatGear);
        var entry = type.GetMethod("GetEntry", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        var open = type.GetMethod("isOpen", Type.EmptyTypes)!;
        Assert.False(open.IsVirtual);
        var instructions = Members(entry);
        Assert.Equal(2, instructions.Count(instruction => Equals(instruction.Member, open)));
        foreach (var name in new[] { "_class", "_level", "_hair" })
        {
            var field = PlayerField(name);
            Assert.DoesNotContain(instructions, instruction => instruction.Op == OpCodes.Stfld && Equals(instruction.Member, field));
        }
        Assert.DoesNotContain(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            field => typeof(Delegate).IsAssignableFrom(field.FieldType));
        // This verifies actual compiled call sites, not instrumentation of racing runtime calls.
    }
}
