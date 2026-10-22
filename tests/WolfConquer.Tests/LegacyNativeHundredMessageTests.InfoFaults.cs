using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Attr = WolfConquer.Core.Data.LegacyNativeHundredWeaponsTable.AttributeType;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;
using Info = WolfConquer.Core.World.LegacyNativeMsgHundredWeaponsInfo;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredMessageTests
{
    [Theory]
    [InlineData("player")] [InlineData("owner")] [InlineData("objects")] [InlineData("row")]
    [InlineData("attributes")] [InlineData("metadata")] [InlineData("match")] [InlineData("metadataAttributes")]
    public Task InfoProducerSourceFaultsOccurBeforeInitWriterAndDoNotRepairOrSend(string missing) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var row = scope.Row(Weapon.Blade, 1, 1, 2, 3, 4, 5);
            var metadata = scope.Meta(1, Weapon.Blade, 1, 0, 0, 0, 0, 0); scope.Weapons.Objects.Add(Weapon.Blade, row);
            switch (missing)
            {
                case "player": scope.Client.Player = null!; break;
                case "owner": scope.Client.HundredWeapons = null!; break;
                case "objects": scope.Weapons.Objects = null!; break;
                case "row": scope.Weapons.Objects[Weapon.Blade] = null!; break;
                case "attributes": row.Attributes = null!; break;
                case "metadata": LegacyNativeHundredWeaponsTable.HundredWeaponsList = null!; break;
                case "match": scope.Metadata.Clear(); break;
                case "metadataAttributes": metadata.Attributes = null!; break;
            }
            Assert.Throws<NullReferenceException>(() => scope.Packet.CreateHundredWeaponsInfo(scope.Client, Info.ActionID.RequestInfo));
            scope.Untouched(); Assert.Empty(scope.Native.Queue);
            if (missing == "owner") Assert.Null(scope.Client.HundredWeapons);
            if (missing == "objects") Assert.Null(scope.Weapons.Objects);
            if (missing == "metadata") Assert.Null(LegacyNativeHundredWeaponsTable.HundredWeaponsList);
        });

    [Theory]
    [InlineData("attribute")] [InlineData("bitmap")] [InlineData("bits")]
    public Task InfoProducerAttributeAndBitmapFaultsPrecedeLaterMetadataOrCheckedScoreFaults(string missing) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var row = scope.Row(Weapon.Blade, 1, int.MaxValue, 1, 0, 0, 0);
            scope.Meta(1, Weapon.Blade, 1, 0, 0, 0, 0, 0); scope.Weapons.Objects.Add(Weapon.Blade, row);
            if (missing == "attribute")
            {
                row.Attributes.Remove(Attr.Hitpoints); LegacyNativeHundredWeaponsTable.HundredWeaponsList = null!;
                Assert.Throws<KeyNotFoundException>(() => scope.Packet.CreateHundredWeaponsInfo(scope.Client, Info.ActionID.Update));
            }
            else if (missing == "bitmap")
            {
                row.BitVector = null!;
                Assert.Throws<NullReferenceException>(() => scope.Packet.CreateHundredWeaponsInfo(scope.Client, Info.ActionID.Update));
            }
            else
            {
                row.BitVector.bits = [];
                Assert.Throws<IndexOutOfRangeException>(() => scope.Packet.CreateHundredWeaponsInfo(scope.Client, Info.ActionID.Update));
            }
            scope.Untouched(); Assert.Empty(scope.Native.Queue);
        });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task InfoProducerRetainsCheckedLinqSumOverflowInEitherActualAttributeDictionary(bool metadataOverflow) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var row = scope.Row(Weapon.Blade, 1, 0, 0, 0, 0, 0);
            var metadata = scope.Meta(1, Weapon.Blade, 1, 0, 0, 0, 0, 0); scope.Weapons.Objects.Add(Weapon.Blade, row);
            if (metadataOverflow) metadata.Attributes = Scope.Attrs(int.MaxValue, 1, 0, 0, 0);
            else row.Attributes = Scope.Attrs(int.MaxValue, 1, 0, 0, 0);
            Assert.Throws<OverflowException>(() => scope.Packet.CreateHundredWeaponsInfo(scope.Client, Info.ActionID.Update));
            scope.Untouched(); Assert.Empty(scope.Native.Queue); Assert.Same(row, scope.Weapons.Objects[Weapon.Blade]);
        });
}
