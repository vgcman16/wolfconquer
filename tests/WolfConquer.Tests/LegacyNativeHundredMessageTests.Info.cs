using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Attr = WolfConquer.Core.Data.LegacyNativeHundredWeaponsTable.AttributeType;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;
using Info = WolfConquer.Core.World.LegacyNativeMsgHundredWeaponsInfo;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredMessageTests
{
    [Fact]
    public Task InfoProducerSortsPhysicalSubtypesKeepsAliasesFullBitWordAndNativeScoresWithoutUnlockOrClassGate() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var sword = scope.Row(Weapon.Sword, 3, 100, 200, 300, 400, 500);
            sword.Progress = uint.MaxValue; sword.AppearancePosition = byte.MaxValue; sword.BitVector.bits[0] = uint.MaxValue;
            var scepter = scope.Row(Weapon.Scepter, 4, 2, 3, 4, 5, 6);
            scepter.Progress = 17; scepter.AppearancePosition = 9; scepter.BitVector.bits[0] = 0x80000001;
            scope.Meta(71, Weapon.Sword, 3, 10, 20, 30, 40, 50); scope.Meta(72, Weapon.Scepter, 4, 1, 1, 1, 1, 1);
            scope.Weapons.Objects.Add(Weapon.Sword, scepter); // Keys intentionally disagree with physical subtypes.
            scope.Weapons.Objects.Add(Weapon.Hammer, sword); scope.Weapons.Objects.Add(Weapon.Blade, sword);
            Assert.False(scope.Weapons.Unlocked); Assert.False(scope.Weapons.Valid());

            Assert.Same(scope.Packet, scope.Packet.CreateHundredWeaponsInfo(scope.Client, Info.ActionID.RequestInfo));

            var parsed = scope.Read<Info.MsgHundredWeaponsInfoProto>(2352);
            Assert.Equal(Info.ActionID.RequestInfo, parsed.Type); Assert.Equal(17u, parsed.OwnerUID); Assert.Equal(3, parsed.Weapons.Length);
            object[] swordExpected = [(ushort)420, 255u, uint.MaxValue, (byte)3, uint.MaxValue, 10090, 20180, 30270, 40360, 50450, 11250u];
            Assert.Equal(swordExpected, Values(parsed.Weapons[0])); Assert.Equal(swordExpected, Values(parsed.Weapons[1]));
            Assert.NotSame(parsed.Weapons[0], parsed.Weapons[1]);
            Assert.Equal(new object[] { (ushort)481, 9u, 0x80000001u, (byte)4, 17u, 10001, 20002, 30003, 40004, 50005, 4125u }, Values(parsed.Weapons[2]));
            Assert.Same(scepter, scope.Weapons.Objects[Weapon.Sword]); Assert.Same(sword, scope.Weapons.Objects[Weapon.Hammer]);
            Assert.Same(sword, scope.Weapons.Objects[Weapon.Blade]); Assert.Equal(uint.MaxValue, sword.BitVector.bits[0]);
            Assert.Equal(new[] { 100, 200, 300, 400, 500 }, sword.Attributes.Values); Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task InfoProducerReadsCurrentClientOwnerAndCurrentMetadataCatalogOnEachInvocation() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var first = scope.Row(Weapon.Sword, 3, 100, 200, 300, 400, 500);
            scope.Weapons.Objects.Add(Weapon.Sword, first); var oldMeta = scope.Meta(1, Weapon.Sword, 3, 10, 20, 30, 40, 50);
            scope.Packet.CreateHundredWeaponsInfo(scope.Client, Info.ActionID.Update);
            Assert.Equal(11250u, Assert.Single(scope.Read<Info.MsgHundredWeaponsInfoProto>(2352).Weapons).Score);
            var replacement = new LegacyNativeHundredWeapons(null!);
            var live = scope.Row(Weapon.Sword, 3, 100, 200, 300, 400, 500); live.Progress = 7; live.BitVector.bits[0] = 17;
            replacement.Objects.Add(Weapon.Blade, live); scope.Client.HundredWeapons = replacement; scope.Client.Player.UID = 29;
            var current = new LegacyNativeHundredWeaponsTable.HundredWeapon
                { WeaponSubtype = Weapon.Sword, Level = 3, Attributes = Scope.Attrs(1, 2, 3, 4, 5) };
            LegacyNativeHundredWeaponsTable.HundredWeaponsList = new() { [987] = current };

            scope.Packet.CreateHundredWeaponsInfo(scope.Client, Info.ActionID.Add);

            var parsed = scope.Read<Info.MsgHundredWeaponsInfoProto>(2352); Assert.Equal(29u, parsed.OwnerUID); Assert.Equal(Info.ActionID.Add, parsed.Type);
            Assert.Equal(new object[] { (ushort)420, 0u, 17u, (byte)3, 7u, 10099, 20198, 30297, 40396, 50495, 10575u }, Values(Assert.Single(parsed.Weapons)));
            Assert.Same(first, scope.Weapons.Objects[Weapon.Sword]); Assert.Equal(0u, first.Progress);
            Assert.Same(oldMeta, scope.Metadata[1]); Assert.Equal(10, oldMeta.Attributes[Attr.Hitpoints]);
            Assert.Same(current, LegacyNativeHundredWeaponsTable.HundredWeaponsList[987]); Assert.Same(replacement, scope.Client.HundredWeapons);
        });

    [Fact]
    public Task InfoProducerNullClientAndEmptyOwnerEncodeRequiredDefaultsWithoutTouchingMetadata() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); LegacyNativeHundredWeaponsTable.HundredWeaponsList = null!;
            scope.Packet.CreateHundredWeaponsInfo(null!, Info.ActionID.RequestInfo);
            byte[] nullClientBytes = NativeSpellTestFixture.Bytes(scope.Packet);
            var missing = scope.Read<Info.MsgHundredWeaponsInfoProto>(2352);
            Assert.Equal(Info.ActionID.RequestInfo, missing.Type); Assert.Equal(0u, missing.OwnerUID); Assert.Null(missing.Weapons);
            scope.Client.Player.UID = 0; scope.Packet.CreateHundredWeaponsInfo(scope.Client, Info.ActionID.RequestInfo);
            Assert.Equal(nullClientBytes, NativeSpellTestFixture.Bytes(scope.Packet));
            var empty = scope.Read<Info.MsgHundredWeaponsInfoProto>(2352); Assert.NotSame(missing, empty); Assert.Null(empty.Weapons);
            Assert.Empty(scope.Weapons.Objects); Assert.Null(LegacyNativeHundredWeaponsTable.HundredWeaponsList);
            Assert.Throws<NullReferenceException>(() => Info.CreateHundredWeaponsInfo(null!, null!, Info.ActionID.None));
        });

    [Fact]
    public Task InfoProducerKeepsUncheckedCombinedSumsAndSignedDifferenceArithmeticWithFullNativeWidths() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var row = scope.Row(Weapon.Sword, 1, int.MaxValue, 0, 0, 0, 0);
            var metadata = scope.Meta(1, Weapon.Sword, 1, int.MaxValue, 0, 0, 0, 0); scope.Weapons.Objects.Add(Weapon.Blade, row);
            scope.Packet.CreateHundredWeaponsInfo(scope.Client, Info.ActionID.Update);
            // Each real int Sum succeeds; adding the two int.MaxValue sums wraps before multiplication.
            Assert.Equal(990u, Assert.Single(scope.Read<Info.MsgHundredWeaponsInfoProto>(2352).Weapons).Score);
            row.Level = byte.MaxValue; metadata.Level = byte.MaxValue; row.Progress = uint.MaxValue;
            row.AppearancePosition = byte.MaxValue; row.BitVector.bits[0] = uint.MaxValue;
            row.Attributes = Scope.Attrs(int.MinValue, int.MaxValue, 0, 0, 0);
            metadata.Attributes = Scope.Attrs(int.MaxValue, int.MinValue, 0, 0, 0);
            scope.Packet.CreateHundredWeaponsInfo(scope.Client, Info.ActionID.Update);
            Assert.Equal(new object[] { (ushort)420, 255u, uint.MaxValue, byte.MaxValue, uint.MaxValue,
                10001, 19999, 30000, 40000, 50000, 254990u }, Values(Assert.Single(scope.Read<Info.MsgHundredWeaponsInfoProto>(2352).Weapons)));
        });
}
