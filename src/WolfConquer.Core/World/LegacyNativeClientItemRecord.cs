using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;
using MsgGameItem = WolfConquer.Core.World.LegacyNativeGameItemRuntime;
using MsgItemExtra = WolfConquer.Core.World.LegacyNativeItemExtra;
using RelicAttribute = WolfConquer.Core.World.LegacyNativeRelicAttribute;

namespace WolfConquer.Core.World;

// Exact ClientItems.DBItem storage and read/write conversion; default packing retained.
// This is not a DTO, byte/file reader, Perfection record or load/save-route owner.
// The caller owns filling the raw record. No null/length/read-result policy is invented here.
internal partial struct LegacyNativeClientItemRecord
{
	public uint UID;

	public uint ITEM_ID;

	public ushort Durability;

	public ushort MaximDurability;

	public ushort Position;

	public uint SocketProgress;

	public Flags.Gem SocketOne;

	public Flags.Gem SocketTwo;

	public Flags.ItemEffect Effect;

	public byte Plus;

	public byte Bless;

	public byte Bound;

	public byte Enchant;

	public byte Suspicious;

	public byte Locked;

	public uint PlusProgress;

	public ushort Inscribed;

	public uint Activate;

	public uint TimeLeftInMinutes;

	public ushort StackSize;

	public uint WH_ID;

	public Flags.Color Color;

	public int UnLockTimer;

	public uint ItemPoints;

	public uint RemainingTime;

	public uint test3;

	public uint test4;

	public uint PurificationItemID;

	public uint PurificationLevel;

	public uint PurificationDuration;

	public long PurificationAddedOn;

	public uint EffectID;

	public uint EffectLevel;

	public uint EffectPercent;

	public uint EffectPercent2;

	public uint EffectDuration;

	public long EffectAddedOn;

	public uint DepositeCount;

	public uint AnimaItemID;

	public uint RuneEXP;

	public uint Attribute1;

	public uint Attribute2;

	public uint Attribute3;

	public uint Attribute4;

	public uint Attribute5;

	public long Expiration;

	public uint MythSoul;

	public uint MythSoulPoints;

	internal MsgGameItem GetDataItem() => GetDataItemCore(null);

    internal MsgGameItem GetDataItem(LegacyOwnedNativeItemImage.CatalogEvidence catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return GetDataItemCore(catalog);
    }

    private MsgGameItem GetDataItemCore(LegacyOwnedNativeItemImage.CatalogEvidence? catalog)
	{
		MsgGameItem msgGameItem = new MsgGameItem();
		msgGameItem.Uid = UID;
		msgGameItem.Effect = Effect;
		if (catalog is null) msgGameItem.ITEM_ID = ITEM_ID;
        else msgGameItem.SetOwnedItemId(ITEM_ID, catalog);
		msgGameItem.Durability = Durability;
		msgGameItem.MaximDurability = MaximDurability;
		msgGameItem.Position = Position;
		msgGameItem.SocketProgress = SocketProgress;
		msgGameItem.SocketOne = SocketOne;
		msgGameItem.SocketTwo = SocketTwo;
		msgGameItem.Plus = Plus;
		msgGameItem.Bless = Bless;
		msgGameItem.Bound = Bound;
		msgGameItem.Enchant = Enchant;
		msgGameItem.Suspicious = Suspicious;
		msgGameItem.Locked = Locked;
		msgGameItem.PlusProgress = PlusProgress;
		msgGameItem.Inscribed = Inscribed;
		msgGameItem.Activate = Activate;
		msgGameItem.TimeLeftInMinutes = TimeLeftInMinutes;
		msgGameItem.StackSize = StackSize;
		msgGameItem.ItemPoints = ItemPoints;
		msgGameItem.UnLockTimer = UnLockTimer;
		msgGameItem.WH_ID = WH_ID;
		msgGameItem.Color = Color;
		msgGameItem.Purification.ItemUID = UID;
		msgGameItem.Purification.PurificationItemID = PurificationItemID;
		msgGameItem.Purification.PurificationLevel = PurificationLevel;
		msgGameItem.Purification.PurificationDuration = PurificationDuration;
		msgGameItem.Purification.AddedOn = DateTime.FromBinary(PurificationAddedOn);
		if (!msgGameItem.Purification.InLife)
		{
			msgGameItem.Purification = default(MsgItemExtra.Purification);
		}
		msgGameItem.Refinary.ItemUID = UID;
		msgGameItem.Refinary.EffectID = EffectID;
		msgGameItem.Refinary.EffectLevel = EffectLevel;
		msgGameItem.Refinary.EffectPercent = EffectPercent;
		msgGameItem.Refinary.EffectPercent2 = EffectPercent2;
		msgGameItem.Refinary.EffectDuration = EffectDuration;
		msgGameItem.Refinary.AddedOn = DateTime.FromBinary(EffectAddedOn);
		if (!msgGameItem.Refinary.InLife)
		{
			msgGameItem.Refinary = default(MsgItemExtra.Refinery);
		}
		msgGameItem.AnimaItemID = AnimaItemID;
		msgGameItem.RuneEXP = RuneEXP;
		msgGameItem.RelicAttributes = new RelicAttribute[5];
		msgGameItem.RelicAttributes[0] = new RelicAttribute(Attribute1);
		msgGameItem.RelicAttributes[1] = new RelicAttribute(Attribute2);
		msgGameItem.RelicAttributes[2] = new RelicAttribute(Attribute3);
		msgGameItem.RelicAttributes[3] = new RelicAttribute(Attribute4);
		msgGameItem.RelicAttributes[4] = new RelicAttribute(Attribute5);
		msgGameItem.EndDate = DateTime.FromBinary(Expiration);
		msgGameItem.MythSoul = MythSoul;
		msgGameItem.MythSoulPoints = MythSoulPoints;
		return msgGameItem;
	}
}
