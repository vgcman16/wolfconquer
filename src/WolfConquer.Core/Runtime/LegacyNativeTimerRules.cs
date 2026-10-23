using System;
using System.Threading;
namespace WolfConquer.Core.Runtime;

internal class LegacyNativeTimerRule
{
	internal Action<int>? action_0;

	internal int int_0;

	internal bool bool_0;

	internal ThreadPriority threadPriority_0;

	public LegacyNativeTimerRule(Action<int> action, int period, ThreadPriority priority = ThreadPriority.Normal)
	{
		action_0 = action;
		int_0 = period;
		bool_0 = true;
		threadPriority_0 = priority;
	}

	~LegacyNativeTimerRule()
	{
		action_0 = null;
	}
}



internal class LegacyNativeLazyDelegate : LegacyNativeTimerRule
{
	public LegacyNativeLazyDelegate(Action<int> action, int dueTime, ThreadPriority priority = ThreadPriority.Normal)
		: base(action, dueTime, priority)
	{
		bool_0 = false;
	}
}
