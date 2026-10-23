using WolfConquer.Core.World;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
	public class Rank
	{
		public Entry? BestOfClass;

		public Dictionary<uint, Entry> Items = new Dictionary<uint, Entry>();

		public Type _Type;

		public int MaxItems = 0;

		public Rank(Type typ)
		{
			_Type = typ;
		}

		public void Remove(uint UID)
		{
			lock (SynRoot)
			{
				if (Items.Remove(UID))
				{
					Items = Items.OrderByDescending((KeyValuePair<uint, Entry> p) => p.Value.TotalPoints).Take(MaxItems).ToDictionary((KeyValuePair<uint, Entry> pair) => pair.Key, (KeyValuePair<uint, Entry> pair) => pair.Value);
				}
			}
		}

		public void AddItem(Entry item)
		{
			if (item.TotalPoints < 20000)
			{
				return;
			}
			lock (SynRoot)
			{
				try
				{
					if (!Items.ContainsKey(item.UID))
					{
						Items.Add(item.UID, item);
					}
					else if (Items.ContainsKey(item.UID))
					{
						Items[item.UID] = item;
					}
					Items = Items.OrderByDescending((KeyValuePair<uint, Entry> p) => p.Value.TotalPoints).Take(MaxItems).ToDictionary((KeyValuePair<uint, Entry> pair) => pair.Key, (KeyValuePair<uint, Entry> pair) => pair.Value);
				}
				finally
				{
					try
					{
						BestOfClass = Items.Values.First();
					}
					catch
					{
						LegacyNativeConsole.SaveException(new Exception("Prestige Rank Type " + _Type.ToString() + "is empty."));
					}
				}
			}
		}
	}
}
