using ProtoBuf.Meta;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    // Literal native registration order, including DaoQi tags 8/9/10 before tag 7.
    // No readiness facade, alternate model, duplicate suppression or rollback.
    private static void RegisterMessages()
    {
        MetaType metaType = RuntimeTypeModel.Default.Add(typeof(LegacyNativeMsgCombatGear), applyDefaultBehaviour: false);
        metaType.AddField(1, "Type").IsRequired = true;
        metaType.AddField(2, "UID").IsRequired = true;
        metaType.AddField(3, "Items").IsRequired = true;
        MetaType metaType2 = RuntimeTypeModel.Default.Add(typeof(LegacyNativeMsgCombatGear.Item), applyDefaultBehaviour: false);
        metaType2.AddField(1, "ID").IsRequired = true;
        metaType2.AddField(2, "Level").IsRequired = true;
        metaType2.AddField(3, "Exp").IsRequired = true;
        metaType2.AddField(4, "Equip").IsRequired = true;
        metaType2.AddField(5, "Transform").IsRequired = true;
        metaType2.AddField(6, "Data1").IsRequired = true;
        metaType2.AddField(7, "Data2").IsRequired = true;
        metaType2.AddField(8, "Data3").IsRequired = true;
        metaType2.AddField(9, "Data4").IsRequired = true;
        metaType2.AddField(10, "Data5").IsRequired = true;
        metaType2.AddField(11, "Data6").IsRequired = true;
        metaType2.AddField(12, "MasteryScore").IsRequired = true;
        MetaType metaType3 = RuntimeTypeModel.Default.Add(typeof(LegacyNativeMsgCombatGearOpt), applyDefaultBehaviour: false);
        metaType3.AddField(1, "Type").IsRequired = true;
        metaType3.AddField(2, "ItemID").IsRequired = true;
        metaType3.AddField(3, "Transform").IsRequired = true;
        metaType3.AddField(4, "UID").IsRequired = true;
        metaType3.AddField(5, "Exp").IsRequired = true;
        metaType3.AddField(6, "U").IsRequired = true;
        metaType3.AddField(7, "SelectMaterial").IsRequired = true;
        MetaType metaType4 = RuntimeTypeModel.Default.Add(typeof(LegacyNativeMsgCombatGearOpt.Items), applyDefaultBehaviour: false);
        metaType4.AddField(1, "UID").IsRequired = true;
        metaType4.AddField(2, "Size").IsRequired = true;
        MetaType metaType5 = RuntimeTypeModel.Default.Add(typeof(LegacyNativeMsgCombatGearSkin), applyDefaultBehaviour: false);
        metaType5.AddField(1, "Type").IsRequired = true;
        metaType5.AddField(2, "Weapon").IsRequired = true;
        MetaType metaType6 = RuntimeTypeModel.Default.Add(typeof(LegacyNativeMsgCombatGearSkin.WeaponInfo), applyDefaultBehaviour: false);
        metaType6.AddField(1, "ID").IsRequired = true;
        metaType6.AddField(2, "Type").IsRequired = true;
        metaType6.AddField(3, "Data1").IsRequired = true;
        metaType6.AddField(4, "Data2").IsRequired = true;
        metaType6.AddField(5, "Data3").IsRequired = true;
        metaType6.AddField(6, "Data4").IsRequired = true;
        metaType6.AddField(7, "Data5").IsRequired = true;
        metaType6.AddField(8, "Data6").IsRequired = true;
        metaType6.AddField(9, "Data7").IsRequired = true;
        MetaType metaType7 = RuntimeTypeModel.Default.Add(typeof(LegacyNativeMsgDaoQi), applyDefaultBehaviour: false);
        metaType7.AddField(1, "Type").IsRequired = true;
        metaType7.AddField(2, "ItemID").IsRequired = true;
        metaType7.AddField(3, "TaoistReiki").IsRequired = true;
        metaType7.AddField(4, "LOTTERY").IsRequired = true;
        metaType7.AddField(5, "Data5").IsRequired = true;
        metaType7.AddField(6, "Data6").IsRequired = true;
        metaType7.AddField(8, "Data8").IsRequired = true;
        metaType7.AddField(9, "PirateExp").IsRequired = true;
        metaType7.AddField(10, "Data10").IsRequired = true;
        metaType7.AddField(7, "MasteryPoints").IsRequired = true;
        metaType7.AddField(11, "Position").IsRequired = true;
        metaType7.AddField(12, "Done").IsRequired = true;
        metaType7.AddField(13, "Items").IsRequired = true;
        metaType7.AddField(14, "Positions").IsRequired = true;
        MetaType metaType8 = RuntimeTypeModel.Default.Add(typeof(LegacyNativeMsgDaoQi.Item), applyDefaultBehaviour: false);
        metaType8.AddField(1, "Id").IsRequired = true;
        MetaType metaType9 = RuntimeTypeModel.Default.Add(typeof(LegacyNativeMsgAirPowerUp), applyDefaultBehaviour: false);
        metaType9.AddField(1, "Type").IsRequired = true;
        metaType9.AddField(2, "UserId").IsRequired = true;
        metaType9.AddField(3, "PowerType").IsRequired = true;
        metaType9.AddField(4, "Powers").IsRequired = true;
        MetaType metaType10 = RuntimeTypeModel.Default.Add(typeof(LegacyNativeMsgAirPowerUp.Power), applyDefaultBehaviour: false);
        metaType10.AddField(1, "Id").IsRequired = true;
        metaType10.AddField(2, "SenseLevel").IsRequired = true;
        metaType10.AddField(3, "SenseAipPower").IsRequired = true;
        metaType10.AddField(4, "ArmedLevel").IsRequired = true;
        metaType10.AddField(5, "ArmedAipPower").IsRequired = true;
        metaType10.AddField(6, "OverLoadLevel").IsRequired = true;
        metaType10.AddField(7, "OverLoadAipPower").IsRequired = true;
        metaType10.AddField(8, "Points").IsRequired = true;
        MetaType metaType11 = RuntimeTypeModel.Default.Add(typeof(LegacyNativeMsgDictLottery), applyDefaultBehaviour: false);
        metaType11.AddField(1, "Type").IsRequired = true;
        metaType11.AddField(2, "Done").IsRequired = true;
        metaType11.AddField(3, "Items2").IsRequired = true;
        MetaType metaType12 = RuntimeTypeModel.Default.Add(typeof(LegacyNativeMsgDictLottery.Lottery), applyDefaultBehaviour: false);
        metaType12.AddField(1, "Type").IsRequired = true;
        metaType12.AddField(2, "Exp").IsRequired = true;
    }
}
