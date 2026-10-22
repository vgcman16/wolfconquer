using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMyCombatGearPirateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CurrentOwnerItemsAreNotClassFilteredOrDeduplicatedAndGetTwoFixedAddsPerValue(bool enabled) => NativePacketTestThread.Run(() =>
    {
        using var tables = new NativeCombatGearQueryScope(enabled);
        using var fixture = new NativeSpellTestFixture();
        var (caller, current) = GearOwners(fixture);
        var shared = new Gear.Weapon { ItemID = Gear.TypeID.Dragonhowl, Equip = 0 };
        current.Items[Gear.TypeID.Pirate1] = shared;
        current.Items[Gear.TypeID.Pirate2] = shared; // Actual duplicate VALUES, no item-key/class filtering.
        LegacyNativeCombatGearTable.Info = null!; // Non-Pirate value must not ask for native Gear.Skills.
        var old = Seed(fixture.Storage, 21540);
        Assert.Equal(0u, fixture.Owner.Player.Class);

        caller.IsPirate(1);

        Added(fixture.Queue, (21540, 0), (21920, 0), (21540, 0), (21920, 0));
        Assert.Equal(new ushort[] { 21540, 21920 }, fixture.Storage.ClientSpells.Keys.OrderBy(id => id));
        Assert.NotSame(old, fixture.Storage.ClientSpells[21540]);
        Assert.Equal((ushort)0, fixture.Storage.ClientSpells[21540].Level);
        Assert.Equal(0, fixture.Storage.ClientSpells[21540].Experience);
        Assert.Equal((byte)0, fixture.Storage.ClientSpells[21540].SoulLevel);
        Assert.Same(shared, current.Items[Gear.TypeID.Pirate1]);
        Assert.Same(shared, current.Items[Gear.TypeID.Pirate2]);
        Assert.Null(caller.Items);
        Assert.Same(current, fixture.Owner.CombatGear);
        Assert.Null(fixture.Owner.Socket!.Connection); // MODEL queue only, no Create/pump.
    });

    [Fact]
    public Task BothJadeAttributesThenNonzeroPirateSkillsThenFixedSpellsUseSourceOrderAndLevels() => NativePacketTestThread.Run(() =>
    {
        using var tables = new NativeCombatGearQueryScope();
        using var fixture = new NativeSpellTestFixture();
        var (caller, current) = GearOwners(fixture);
        var weapon = PirateWeapon();
        weapon.BlackJades = [new Gear.BlackJade(71, 0), new Gear.BlackJade(72, 0)];
        Jade(24, 17001007, 24, 17002009);
        LegacyNativeDaoqiDictTypeTable.Info[72] = new() { attr_type1 = 1, attr_value1 = 19001005, attr_type2 = 24, attr_value2 = 0 };
        Skills(weapon, [0, 18001003, 18002099, 0]);
        current.Items[Gear.TypeID.Dragonhowl] = weapon; // Pirate value, non-Pirate dictionary key.

        caller.IsPirate(1);

        Added(fixture.Queue, (17001, 7), (17002, 9), (0, 0), (18001, 3), (18002, 99), (21540, 0), (21920, 0));
        Assert.Equal(7, fixture.Storage.ClientSpells.Count);
        Assert.False(fixture.Storage.ClientSpells.ContainsKey(19001));
        Assert.Equal((ushort)0, fixture.Storage.ClientSpells[0].ID); // Type24/value0 is NOT a native skip.
        Assert.All(fixture.Storage.ClientSpells.Values, spell =>
        {
            Assert.Equal(0, spell.Experience);
            Assert.Equal((byte)0, spell.SoulLevel);
            Assert.Equal((byte)0, spell.PreviousLevel);
        });
    });

    [Fact]
    public Task RepeatedJadeReferencesAndRepeatedSkillsRemainRepeatedNativeAdds() => NativePacketTestThread.Run(() =>
    {
        using var tables = new NativeCombatGearQueryScope();
        using var fixture = new NativeSpellTestFixture();
        var (caller, current) = GearOwners(fixture);
        var weapon = PirateWeapon();
        var shared = new Gear.BlackJade(71, 0);
        weapon.BlackJades = [shared, shared];
        Jade(24, 17001007);
        Skills(weapon, [17001009, 17001009]);
        current.Items[Gear.TypeID.Pirate2] = weapon;
        var old = Seed(fixture.Storage, 17001);

        caller.IsPirate(1);

        Added(fixture.Queue, (17001, 7), (17001, 7), (17001, 9), (17001, 9), (21540, 0), (21920, 0));
        Assert.Equal(3, fixture.Storage.ClientSpells.Count);
        Assert.NotSame(old, fixture.Storage.ClientSpells[17001]);
        Assert.Equal((ushort)9, fixture.Storage.ClientSpells[17001].Level);
        Assert.Equal(0, fixture.Storage.ClientSpells[17001].Experience);
        Assert.Same(shared, weapon.BlackJades[0]);
        Assert.Same(shared, weapon.BlackJades[1]);
    });

    [Fact]
    public Task NativeUShortIdCastsAndChildSpellAdmissionRemainUnrepaired() => NativePacketTestThread.Run(() =>
    {
        using var tables = new NativeCombatGearQueryScope();
        using var fixture = new NativeSpellTestFixture();
        var (caller, current) = GearOwners(fixture);
        var weapon = PirateWeapon();
        Jade(24, uint.MaxValue, 24, 1105009);
        Skills(weapon, [4000001, 4070099, 4071002, uint.MaxValue]);
        current.Items[Gear.TypeID.Pirate2] = weapon;

        caller.IsPirate(1);

        Added(fixture.Queue, (35127, 95), (4071, 2), (35127, 95), (21540, 0), (21920, 0));
        Assert.False(fixture.Storage.ClientSpells.ContainsKey(1105));
        Assert.False(fixture.Storage.ClientSpells.ContainsKey(4000));
        Assert.False(fixture.Storage.ClientSpells.ContainsKey(4070));
        Assert.Equal(4, fixture.Storage.ClientSpells.Count);
    });

    [Fact]
    public Task CurrentMySpellsReferenceKeepsItsActualForeignOwnerSendAndRemovalUid() => NativePacketTestThread.Run(() =>
    {
        using var tables = new NativeCombatGearQueryScope();
        using var fixture = new NativeSpellTestFixture();
        var (caller, current) = GearOwners(fixture);
        var foreignQueue = new Queue<byte[]>();
        var foreign = NativeSpellTestFixture.CreateOwner(foreignQueue);
        foreign.Player.UID = 99;
        var foreignStorage = new LegacyNativeSpellStorage(foreign);
        fixture.Owner.MySpells = foreignStorage; // Explicit physical-reference prestate, not rebinding the owner.
        current.Items[Gear.TypeID.Pirate1] = new Gear.Weapon { ItemID = Gear.TypeID.Dragonhowl };

        caller.IsPirate(1);

        Added(foreignQueue, (21540, 0), (21920, 0));
        Assert.Empty(fixture.Queue);
        Assert.Empty(fixture.Storage.ClientSpells);
        foreignQueue.Clear();
        caller.IsPirate(0);
        Assert.Equal(new ushort[] { 21540, 21920 }, foreignQueue.Select(frame => Removed(frame, 99)));
        Assert.Empty(foreignStorage.ClientSpells);
        Assert.Empty(fixture.Queue);
        Assert.Null(foreign.Socket!.Connection);
    });
}
