using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Equip = WolfConquer.Core.World.LegacyNativeEquipItemStorage;
using Item = WolfConquer.Core.World.LegacyNativeGameItemRuntime;
using Position = WolfConquer.Core.World.LegacyNativeGameItemFlags.ConquerItem;
using Chi = WolfConquer.Core.World.LegacyNativeChiRuntime;

namespace WolfConquer.Tests;

// Full native helper methods only. QueryEquipment/UpdateStats/online ranking and
// SaveClient remain held; these do not simulate a successful query via delegates.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeEquipmentQueryPrerequisiteTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly LegacyNativeItemTypeRuneMetadata? oldItems = LegacyNativePoolItems.ItemsBase;
        private readonly Dictionary<byte,LegacyNativeBeastsAtrribute.Attribute> oldBeasts = LegacyNativeBeastsAtrribute.Attributes;
        private readonly Dictionary<uint,LegacyNativeProfessionTable.Benefit> oldBenefits = LegacyNativeProfessionTable.Benefits;
        private readonly Dictionary<ushort,LegacyNativeItemRefineUpgrade.Effect> oldEffects = LegacyNativeItemRefineUpgrade.Effects;
        private readonly Dictionary<uint,LegacyNativeSpecialEffectAttributeTable.Attribute>? oldSpecial = LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture, oldUiCulture = CultureInfo.CurrentUICulture;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal LegacyNativeMsgStatus Status => Client.Status;
        internal Equip Equipment { get; }

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom=new LegacyNativeSafeRandom(71);
            LegacyNativePoolItems.ItemsBase=new();
            LegacyNativeBeastsAtrribute.Attributes=new();
            LegacyNativeProfessionTable.Benefits=new(); LegacyNativeItemRefineUpgrade.Effects=new();
            LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute=new();
            CultureInfo.CurrentCulture=CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture=CultureInfo.InvariantCulture;
            Native=new NativeSpellTestFixture();
            Fill(Native.Packet);
            Equipment=new Equip(Client); Client.Equipment=Equipment;
            Client.Rune=new LegacyNativeRuneItemStorage(Client);
            Client.HundredWeapons=new LegacyNativeHundredWeapons(Client);
            Client.Beasts=new LegacyNativeBeastsRuntime(Client);
        }

        internal Item Add(uint key, ushort position, uint id=410009)
        {
            var item=new Item{Uid=key,Position=position,ITEM_ID=id}; Equipment.ClientItems[key]=item; return item;
        }

        public void Dispose()
        {
            Native.Dispose(); LegacyNativePoolRandom.GetRandom=oldRandom;
            LegacyNativePoolItems.ItemsBase=oldItems; LegacyNativeBeastsAtrribute.Attributes=oldBeasts;
            LegacyNativeProfessionTable.Benefits=oldBenefits; LegacyNativeItemRefineUpgrade.Effects=oldEffects;
            LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute=oldSpecial;
            CultureInfo.CurrentCulture=oldCulture; CultureInfo.CurrentUICulture=oldUiCulture;
        }
    }

    private static Tuple<Item?,Item?> Weapons(Equip equipment) =>
        (Tuple<Item?,Item?>)typeof(Equip).GetMethod("ComputeWeapons",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(equipment,null)!;

    private static void SetClass(LegacyNativePlayerRuntime player,uint value) => typeof(LegacyNativePlayerRuntime)
        .GetField("_class",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(player,value);
    private static void SetLevel(LegacyNativePlayerRuntime player,ushort value) => typeof(LegacyNativePlayerRuntime)
        .GetField("_level",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(player,value);

    private static unsafe void Fill(WolfConquer.Core.Networking.LegacyNativePacket packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xA5,WolfConquer.Core.Networking.LegacyNativePacket.MAX_SIZE).ToArray(),0,
            (IntPtr)packet.Memory,WolfConquer.Core.Networking.LegacyNativePacket.MAX_SIZE);
        packet.Size=321;packet.Seek(237);
    }

    private static uint[] StatusValues(LegacyNativeMsgStatus status) =>
    [status.CriticalStrike,status.SkillCStrike,status.Immunity,status.Counteraction,status.Breakthrough,status.MaxHitpoints,
     status.MaxAttack,status.MinAttack,status.MagicAttack,status.MagicDefence,status.PhysicalDamageIncrease,
     status.PhysicalDamageDecrease,status.MagicDamageIncrease,status.MagicDamageDecrease,status.Defence,status.MDefence];

    private static Chi ChiValues() => new(77)
    {
        CriticalStrike=101,SkillCriticalStrike=202,Immunity=303,Counteraction=404,Breakthrough=505,MaxLife=606,
        AddAttack=707,AddMagicAttack=808,AddMagicDefense=909,FinalAttack=1010,FinalDefense=1111,
        FinalMagicAttack=1212,FinalMagicDefense=1313,
    };

    private static LegacyNativeHundredWeaponInfo Hundred(int hp,int attack,int defence,int magic,int mdef,bool active=true)
    {
        var item=new LegacyNativeHundredWeaponInfo
        {
            BitVector=new LegacyNativeBitVector32(32),
            Attributes=new()
            {
                [LegacyNativeHundredWeaponsTable.AttributeType.Hitpoints]=hp,
                [LegacyNativeHundredWeaponsTable.AttributeType.PhysicalAttack]=attack,
                [LegacyNativeHundredWeaponsTable.AttributeType.PhysicalDefense]=defence,
                [LegacyNativeHundredWeaponsTable.AttributeType.MagicAttack]=magic,
                [LegacyNativeHundredWeaponsTable.AttributeType.MagicDefense]=mdef,
            },
        };
        if(active)item.BitVector.Add(1); return item;
    }
}
