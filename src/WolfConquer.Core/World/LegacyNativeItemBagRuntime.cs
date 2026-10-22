using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WolfConquer.Core.World;

// Exact native SafeDictionary<uint, actual item> specialization for the two bags.
// Inherited Dictionary APIs are intentionally NOT made uniformly thread-safe/cache-aware.
internal class LegacyNativeItemBagRuntime : Dictionary<uint, LegacyNativeGameItemRuntime>
{
	public object SyncRoot = null!;

	public bool Update = false;

	public LegacyNativeGameItemRuntime[] MyArray = new LegacyNativeGameItemRuntime[0];

	public new LegacyNativeGameItemRuntime? this[uint key]
	{
		get
		{
			if (ContainsKey(key))
			{
				return base[key];
			}
			return default(LegacyNativeGameItemRuntime);
		}
		set
		{
			base[key] = value!;
		}
	}

	public LegacyNativeItemBagRuntime()
	{
		MyArray = new LegacyNativeGameItemRuntime[0];
		SyncRoot = new object();
	}

	public LegacyNativeItemBagRuntime(int cap)
		: base(cap)
	{
		SyncRoot = new object();
	}

	public new void Add(uint key, LegacyNativeGameItemRuntime value)
	{
		try
		{
			Monitor.Enter(SyncRoot);
			base[key] = value;
			Update = true;
		}
		finally
		{
			Monitor.Exit(SyncRoot);
		}
	}

	public new void Remove(uint key)
	{
		try
		{
			Monitor.Enter(SyncRoot);
			base.Remove(key);
			Update = true;
		}
		finally
		{
			Monitor.Exit(SyncRoot);
		}
	}

	public LegacyNativeGameItemRuntime[] GetValues()
	{
		if (Update)
		{
			try
			{
				Monitor.Enter(SyncRoot);
				Update = false;
				MyArray = base.Values.ToArray();
			}
			finally
			{
				Monitor.Exit(SyncRoot);
			}
			return MyArray;
		}
		return MyArray;
	}

	public new void Clear()
	{
		lock (SyncRoot)
		{
			base.Clear();
		}
		Update = true;
	}
}
