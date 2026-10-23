namespace WolfConquer.Core.World;

internal struct LegacyNativeSpecialItemWatcher
{
	public uint ID;

	public LegacyNativeMobRateWatcher? Rate;

	public LegacyNativeSpecialItemWatcher(uint ID, int Tick)
	{
		this.ID = ID;
		Rate = new LegacyNativeMobRateWatcher(Tick);
	}
}
