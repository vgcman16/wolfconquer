namespace WolfConquer.Core.World;

internal class LegacyNativeMobRateWatcher
{
	private int tick;

	private int count;

	public static implicit operator bool(LegacyNativeMobRateWatcher q)
	{
		bool result = false;
		q.count++;
		if (q.count == q.tick)
		{
			q.count = 0;
			result = true;
		}
		return result;
	}

	public LegacyNativeMobRateWatcher(int Tick)
	{
		tick = Tick;
		count = 0;
	}
}
