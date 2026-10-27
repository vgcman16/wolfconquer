using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WolfConquer.Core.World;

// Concrete native non-wire Wardrobe owner. Real packets/EquipItem are absent, not no-ops.
internal partial class LegacyNativeWardrobeRuntime
{
	public enum ItemsType : byte
	{
		Garment,
		Mount,
		Count
	}

	public ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>[] Items;

	public LegacyNativeGameClientRuntime? Owner;
	private readonly LegacyNativeGameClientRuntime? constructorOwner;

	public ItemsType GetItemType(uint ID)
	{
		if (LegacyNativeItemPosition.ItemPosition(ID) == 17)
		{
			return ItemsType.Mount;
		}
		if (LegacyNativeItemPosition.ItemPosition(ID) == 9)
		{
			return ItemsType.Garment;
		}
		return ItemsType.Count;
	}

	public bool HaveItems()
	{
		ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>[] items = Items;
		foreach (ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> concurrentDictionary in items)
		{
			if (concurrentDictionary.Count > 0)
			{
				return true;
			}
		}
		return false;
	}

	public LegacyNativeWardrobeRuntime(LegacyNativeGameClientRuntime? _owner)
	{
		Owner = _owner;
		constructorOwner = _owner;
		Items = new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>[2];
		for (int i = 0; i < 2; i++)
		{
			Items[i] = new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>();
		}
	}

	public bool Contain(uint ID)
	{
		ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>[] items = Items;
		foreach (ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> concurrentDictionary in items)
		{
			ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> concurrentDictionary2 = concurrentDictionary;
			foreach (LegacyNativeGameItemRuntime value in concurrentDictionary2.Values)
			{
				if (value.ITEM_ID == ID)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool ContainUID(uint UID)
	{
		ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>[] items = Items;
		foreach (ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> concurrentDictionary in items)
		{
			ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> concurrentDictionary2 = concurrentDictionary;
			foreach (LegacyNativeGameItemRuntime value in concurrentDictionary2.Values)
			{
				if (value.Uid == UID)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool TryGetItem(uint UID, out LegacyNativeGameItemRuntime? item)
	{
		Items[0].TryGetValue(UID, out item);
		if (item != null)
		{
			return true;
		}
		Items[1].TryGetValue(UID, out item);
		if (item != null)
		{
			return true;
		}
		item = null;
		return false;
	}

	public bool AddItem(LegacyNativeGameItemRuntime item)
	{
		ItemsType itemType = GetItemType(item.ITEM_ID);
		if (itemType == ItemsType.Count)
		{
			return false;
		}
		ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> concurrentDictionary = Items[(uint)itemType];
		if (!concurrentDictionary.ContainsKey(item.Uid))
		{
			item.WH_ID = 100u;
			concurrentDictionary.TryAdd(item.Uid, item);
			return true;
		}
		return false;
	}

	public bool RemoveItem(uint UID, out LegacyNativeGameItemRuntime? item)
	{
		item = null;
		ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>[] items = Items;
		foreach (ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> concurrentDictionary in items)
		{
			ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> concurrentDictionary2 = concurrentDictionary;
			if (concurrentDictionary2.TryRemove(UID, out item))
			{
				break;
			}
		}
		if (item != null)
		{
			item.WH_ID = 0u;
			item.Position = 0;
		}
		return item != null;
	}

	public IEnumerable<LegacyNativeGameItemRuntime> GetAllItems()
	{
		ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>[] items = Items;
		foreach (ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> objects in items)
		{
			foreach (LegacyNativeGameItemRuntime value in objects.Values)
			{
				yield return value;
			}
		}
	}

	public int GetCountItems()
	{
		int num = 0;
		ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>[] items = Items;
		foreach (ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> concurrentDictionary in items)
		{
			num += concurrentDictionary.Count;
		}
		return num;
	}
}
