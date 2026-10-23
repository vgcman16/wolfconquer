namespace WolfConquer.Core.World;

internal class LegacyNativeWrappingCounter
{
	private uint Start = 0u;

	private uint finish = uint.MaxValue;

	public uint Finish
	{
		get
		{
			return finish;
		}
		set
		{
			finish = value;
		}
	}

	public uint Now { get; set; }

	public uint Next
	{
		get
		{
			Now++;
			if (Now == Finish)
			{
				Now = Start;
			}
			return Now;
		}
	}

	public LegacyNativeWrappingCounter()
	{
		Now = Start;
	}

	public LegacyNativeWrappingCounter(uint startFrom)
	{
		Start = startFrom;
		Now = startFrom;
	}
}
