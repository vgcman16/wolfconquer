using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMyCombatGearPirateTests
{
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)2)]
    [InlineData((byte)255)]
    public Task EveryValueOtherThanOneRemovesJadeSkillsAndFixedSpellsInOrder(byte mode) => NativePacketTestThread.Run(() =>
    {
        using var tables = new NativeCombatGearQueryScope();
        using var fixture = new NativeSpellTestFixture();
        var (caller, current) = GearOwners(fixture);
        var weapon = PirateWeapon();
        weapon.BlackJades = [new Gear.BlackJade(71, 0), new Gear.BlackJade(72, 0)];
        Jade(24, 17001007, 24, 17002009);
        LegacyNativeDaoqiDictTypeTable.Info[72] = new() { attr_type1 = 23, attr_value1 = 19001005 };
        Skills(weapon, [0, 18001003, 18002007, 18001003]);
        current.Items[Gear.TypeID.Dragonhowl] = weapon;
        foreach (ushort id in new ushort[] { 17001, 17002, 18001, 18002, 21540, 21920 }) Seed(fixture.Storage, id);
        var ignored = Seed(fixture.Storage, 19001);
        var unrelated = Seed(fixture.Storage, 12345);

        caller.IsPirate(mode);

        Assert.Equal(new ushort[] { 17001, 17002, 18001, 18002, 21540, 21920 },
            fixture.Queue.Select(frame => Removed(frame, fixture.Owner.Player.UID)));
        Assert.Equal(2, fixture.Storage.ClientSpells.Count);
        Assert.Same(ignored, fixture.Storage.ClientSpells[19001]);
        Assert.Same(unrelated, fixture.Storage.ClientSpells[12345]);
        Assert.Null(caller.Items);
        Assert.Same(current, fixture.Owner.CombatGear);
    });

    [Fact]
    public Task EmptyCurrentItemsNeverDereferencesMissingMySpellsButTheFirstRealItemDoes() => NativePacketTestThread.Run(() =>
    {
        using var tables = new NativeCombatGearQueryScope();
        using var fixture = new NativeSpellTestFixture();
        var (caller, current) = GearOwners(fixture);
        fixture.Owner.MySpells = null;
        caller.IsPirate(1);
        caller.IsPirate(255);
        Assert.Empty(fixture.Queue);
        current.Items[Gear.TypeID.Pirate1] = new Gear.Weapon { ItemID = Gear.TypeID.Dragonhowl, BlackJades = [] };
        Assert.Throws<NullReferenceException>(() => caller.IsPirate(1));
        Assert.Throws<NullReferenceException>(() => caller.IsPirate(0));
        Assert.Empty(fixture.Queue);
        Assert.Empty(fixture.Storage.ClientSpells);
        Assert.Null(fixture.Owner.MySpells);
    });

    [Theory]
    [InlineData("current")]
    [InlineData("items")]
    [InlineData("weapon")]
    [InlineData("jades")]
    [InlineData("jade")]
    public Task MissingPhysicalOwnersAndElementsFaultWithoutInventedRepairOrPublication(string missing) => NativePacketTestThread.Run(() =>
    {
        using var tables = new NativeCombatGearQueryScope();
        using var fixture = new NativeSpellTestFixture();
        var (caller, current) = GearOwners(fixture);
        var weapon = new Gear.Weapon { ItemID = Gear.TypeID.Dragonhowl };
        current.Items[Gear.TypeID.Dragonhowl] = weapon;
        switch (missing)
        {
            case "current": fixture.Owner.CombatGear = null; break;
            case "items": current.Items = null!; break;
            case "weapon": current.Items[Gear.TypeID.Dragonhowl] = null!; break;
            case "jades": weapon.BlackJades = null!; break;
            case "jade": weapon.BlackJades = [null!]; break;
            default: throw new InvalidOperationException("Unknown owned fault fixture.");
        }

        Assert.Throws<NullReferenceException>(() => caller.IsPirate(1));

        Assert.Empty(fixture.Queue);
        Assert.Empty(fixture.Storage.ClientSpells);
        Assert.Null(caller.Items);
        if (missing == "current") Assert.Null(fixture.Owner.CombatGear);
        if (missing == "items") Assert.Null(current.Items);
        if (missing == "weapon") Assert.Null(current.Items[Gear.TypeID.Dragonhowl]);
        if (missing == "jades") Assert.Null(weapon.BlackJades);
        if (missing == "jade") Assert.Null(Assert.Single(weapon.BlackJades));
    });

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)0)]
    public Task MissingJadeTableFaultsBeforeAnyAddOrRemovalOfTheFixedSpells(byte mode) => NativePacketTestThread.Run(() =>
    {
        using var tables = new NativeCombatGearQueryScope();
        using var fixture = new NativeSpellTestFixture();
        var (caller, current) = GearOwners(fixture);
        current.Items[Gear.TypeID.Pirate1] = new Gear.Weapon
        {
            ItemID = Gear.TypeID.Dragonhowl, BlackJades = [new Gear.BlackJade(71, 0)]
        };
        var first = Seed(fixture.Storage, 21540);
        var second = Seed(fixture.Storage, 21920);
        LegacyNativeDaoqiDictTypeTable.Info = null!;

        Assert.Throws<NullReferenceException>(() => caller.IsPirate(mode));

        Assert.Empty(fixture.Queue);
        Assert.Equal(2, fixture.Storage.ClientSpells.Count);
        Assert.Same(first, fixture.Storage.ClientSpells[21540]);
        Assert.Same(second, fixture.Storage.ClientSpells[21920]);
        Assert.Null(LegacyNativeDaoqiDictTypeTable.Info);
    });

    [Theory]
    [InlineData("missing-row", (byte)1)]
    [InlineData("null-skills", (byte)1)]
    [InlineData("null-table", (byte)1)]
    [InlineData("missing-row", (byte)0)]
    [InlineData("null-skills", (byte)0)]
    [InlineData("null-table", (byte)0)]
    public Task PirateMetadataFaultRefusesBeforeTheJadePrefixAndFixedSpellTail(string missing, byte mode) => NativePacketTestThread.Run(() =>
    {
        using var tables = new NativeCombatGearQueryScope();
        using var fixture = new NativeSpellTestFixture();
        var (caller, current) = GearOwners(fixture);
        var weapon = PirateWeapon();
        current.Items[Gear.TypeID.Pirate2] = weapon;
        Jade(24, 17001007);
        var oldJade = Seed(fixture.Storage, 17001);
        var fixedFirst = Seed(fixture.Storage, 21540);
        var fixedSecond = Seed(fixture.Storage, 21920);
        if (missing == "null-skills") Skills(weapon, null);
        if (missing == "null-table") LegacyNativeCombatGearTable.Info = null!;
        var spells = new LegacyNativeSpellMutationWitness(fixture.Storage);
        var packet = new LegacyNativePacketMutationWitness(fixture.Packet);

        Assert.Throws<NullReferenceException>(() => caller.IsPirate(mode));

        spells.RequireUnchanged(); packet.RequireUnchanged(); Assert.Empty(fixture.Queue);
        Assert.Same(oldJade, fixture.Storage.ClientSpells[17001]);
        Assert.Equal((ushort)5, oldJade.Level); Assert.Equal(88, oldJade.Experience);
        Assert.Equal(3, fixture.Storage.ClientSpells.Count);
        Assert.Same(fixedFirst, fixture.Storage.ClientSpells[21540]);
        Assert.Same(fixedSecond, fixture.Storage.ClientSpells[21920]);
        Assert.Equal((ushort)5, fixedFirst.Level);
        Assert.Equal(88, fixedSecond.Experience);
    });
}
