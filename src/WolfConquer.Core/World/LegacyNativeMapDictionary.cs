namespace WolfConquer.Core.World;

// Genuine native storage and full independent method families. Not a plain-Dict
// substitute: whole lazy ContainsKey/indexer and EnterMap live in actual partials.
internal partial class LegacyNativeMapDictionary<T1, T2>
{
    private Dictionary<T1, T2> DictBase;
    public Dictionary<T1, T2> Base => DictBase;
    public int Count => DictBase.Count;

	public Dictionary<T1, T2>.ValueCollection Values
	{
		get
		{
			if (DictBase == null)
			{
				DictBase = new Dictionary<T1, T2>();
			}
			return DictBase.Values;
		}
	}

	public LegacyNativeMapDictionary(int capacity)
	{
		DictBase = new Dictionary<T1, T2>(capacity);
	}

	public LegacyNativeMapDictionary()
	{
		DictBase = new Dictionary<T1, T2>();
	}

	public bool Add(T1 key, T2 value)
	{
		if (!DictBase.ContainsKey(key))
		{
			DictBase.Add(key, value);
			return true;
		}
		return false;
	}

	public void Remove(T1 key)
	{
		DictBase.Remove(key);
	}

	public bool ContainsValue(T2 value)
	{
		return DictBase.ContainsValue(value);
	}

	public void Clear()
	{
		DictBase.Clear();
	}

	public bool TryGetValue(T1 key, out T2 value)
	{
		return DictBase.TryGetValue(key, out value);
	}
}
