namespace WolfConquer.Core.World;

// Whole real native lazy contract; no warm-only or dictionary-only fallback.
internal partial class LegacyNativeMapDictionary<T1,T2>
{
	public T2 this[T1 key]
	{
		get
		{
			if (ContainsKey(key))
			{
				return DictBase[key];
			}
			return default(T2)!;
		}
	}

public bool ContainsKey(T1 key)
	{
		if (!DictBase.ContainsKey(key) && Convert.ToUInt32(key) != 0)
		{
			LegacyNativeGameMap.EnterMap(Convert.ToInt32(key));
		}
		return DictBase.ContainsKey(key);
	}
}
