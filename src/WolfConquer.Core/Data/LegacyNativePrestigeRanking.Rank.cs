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
				var source = CapturePrestigeRankSource(this);
				var prepared = PreparePrestigeRankChange(source, UID, replacement: null);
				if (prepared is not null) PublishPrestigeRank(prepared);
			}
		}

		public void AddItem(Entry item)
		{
			ArgumentNullException.ThrowIfNull(item);
			lock (SynRoot)
			{
				var source = CapturePrestigeRankSource(this);
				var prepared = PreparePrestigeRankChange(source, item.UID,
					item.TotalPoints >= 20_000 ? item : null);
				if (prepared is not null) PublishPrestigeRank(prepared);
			}
		}
	}
}
