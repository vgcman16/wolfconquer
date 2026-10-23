using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole native MsgFloorItem.MsgItem owner; same item/map/packet aliases.
internal partial class LegacyNativeFloorItemRuntime : ILegacyNativeMapObj
{
	public enum ItemType
	{
		Item,
		Money,
		Cps,
		Effect
	}

	public static LegacyNativeCounter UIDS = new LegacyNativeCounter(900027u);

	public DateTime Expire = default(DateTime);

	public uint ItemOwner;

	public bool ToMySelf;

	public DateTime AttackStamp = DateTime.Now;

	public LegacyNativeGameItemRuntime? ItemBase;

	public LegacyNativeMsgItemPacket MsgFloor;

	public LegacyNativeGameMap GMap;

	public ItemType Typ;

	public uint Gold;

	public uint ConquerPoints;

	private int SpecialSecounds = 0;

	public LegacyNativeGameClientRuntime? OwnerEffert = null;

	public LegacyNativeMagicType.Magic? DBSkill = null;

	public byte SpellSoul = 0;

	public bool ExpireMySelf => DateTime.Now > Expire.AddSeconds(20.0);

	public bool AllowDynamic { get; set; }

	public uint IndexInScreen { get; set; }

	public uint DynamicID { get; set; }

	public uint Map { get; set; }

	public bool Alive => Expire.AddSeconds((SpecialSecounds != 0) ? SpecialSecounds : 40) > DateTime.Now;

	public LegacyNativeMapObjectType ObjType { get; set; }

	public uint UID
	{
		get
		{
			return MsgFloor.m_UID;
		}
		set
		{
			MsgFloor.m_UID = value;
		}
	}

	public ushort X
	{
		get
		{
			return MsgFloor.m_X;
		}
		set
		{
			MsgFloor.m_X = value;
		}
	}

	public ushort Y
	{
		get
		{
			return MsgFloor.m_Y;
		}
		set
		{
			MsgFloor.m_Y = value;
		}
	}

	public bool IsTrap()
	{
		return MsgFloor.DropType == LegacyNativeMsgDropID.Effect;
	}

	public LegacyNativeFloorItemRuntime(LegacyNativeGameItemRuntime? item, ushort x, ushort y, ItemType Mode, uint Amount, uint dinamicid, uint _mapid, uint _ItemOwner, bool _ToMySelf, LegacyNativeGameMap _map, int specialSecound = 0)
	{
		AllowDynamic = false;
		SpecialSecounds = specialSecound;
		GMap = _map;
		ItemOwner = _ItemOwner;
		ToMySelf = _ToMySelf;
		Map = _mapid;
		DynamicID = dinamicid;
		Typ = Mode;
		MsgFloor = LegacyNativeMsgItemPacket.Create();
		if (item != null)
		{
			ItemBase = item;
			if (item.Purification.ItemUID != 0 && item.Purification.InLife)
			{
				MsgFloor.m_ID = item.Purification.PurificationItemID;
			}
			else
			{
				MsgFloor.m_ID = item.ITEM_ID;
			}
			MsgFloor.m_Color = (byte)item.Color;
			MsgFloor.Plus = item.Plus;
		}
		MsgFloor.ItemOwnerUID = ItemOwner;
		MsgFloor.m_UID = UIDS.Next;
		MsgFloor.m_X = x;
		MsgFloor.m_Y = y;
		ObjType = LegacyNativeMapObjectType.Item;
		switch (Mode)
		{
		case ItemType.Cps:
			ConquerPoints += Amount;
			break;
		case ItemType.Money:
			Gold += Amount;
			break;
		}
		Expire = DateTime.Now;
	}

	public bool CanSee(ILegacyNativeMapObj obj)
	{
		if (DynamicID != obj.DynamicID)
		{
			return false;
		}
		if (Map != obj.Map)
		{
			return false;
		}
		if (LegacyNativeRoleCore.GetDistance(MsgFloor.m_X, MsgFloor.m_Y, obj.X, obj.Y) >= 18)
		{
			return false;
		}
		return true;
	}

	public void Send(LegacyNativePacket msg)
	{
		foreach (ILegacyNativeMapObj item in GMap.View!.Roles(LegacyNativeMapObjectType.Player, X, Y, (ILegacyNativeMapObj p) => CanSee(p)))
		{
			item.Send(msg);
		}
	}

	public void SendAll(LegacyNativePacket stream, LegacyNativeMsgDropID Typ)
	{
		if (Alive)
		{
			MsgFloor.DropType = Typ;
			stream = stream.ItemPacketCreate(MsgFloor);
			Send(stream);
		}
		else if (Typ == LegacyNativeMsgDropID.Remove || Typ == LegacyNativeMsgDropID.RemoveEffect)
		{
			MsgFloor.DropType = Typ;
			if (MsgFloor.m_ID == 41 || MsgFloor.m_ID == 42)
			{
				MsgFloor.m_ID = 50u;
			}
			stream = stream.ItemPacketCreate(MsgFloor);
			Send(stream);
			if ((GMap.cells[X, Y] & LegacyNativeMapFlagType.Item) == LegacyNativeMapFlagType.Item)
			{
				GMap.cells[X, Y] &= ~LegacyNativeMapFlagType.Item;
			}
		}
	}

	public LegacyNativePacket GetArray(LegacyNativePacket stream, bool View, bool st)
	{
		return stream = stream.ItemPacketCreate(MsgFloor);
	}
}
