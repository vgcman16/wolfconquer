using System;
using System.Threading;
namespace WolfConquer.Core.Runtime;

internal class LegacyNativeTimerRule<T>
{
	internal Action<T, int>? action_0;

	internal int int_0;

	internal bool bool_0;

	internal ThreadPriority threadPriority_0;

	public LegacyNativeTimerRule(Action<T, int> action, int period, ThreadPriority priority = ThreadPriority.Normal)
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



internal class LegacyNativeLazyDelegate<T> : LegacyNativeTimerRule<T>
{
	public LegacyNativeLazyDelegate(Action<T, int> action, int dueTime, ThreadPriority priority = ThreadPriority.Normal)
		: base(action, dueTime, priority)
	{
		bool_0 = false;
	}
}
