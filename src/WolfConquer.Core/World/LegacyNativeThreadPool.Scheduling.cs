using System;
using System.Threading;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeThreadPool
{
	public static void Execute(Action<int> action, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
	{
		GenericThreadPool!.Subscribe(new LegacyNativeLazyDelegate(action, timeOut, priority));
	}

	public static void Execute<T>(Action<T, int> action, T param, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
	{
		GenericThreadPool!.Subscribe(new LegacyNativeLazyDelegate<T>(action, timeOut, priority), param);
	}

	public static IDisposable Subscribe(Action<int> action, int period = 1, ThreadPriority priority = ThreadPriority.Normal)
	{
		return GenericThreadPool!.Subscribe(new LegacyNativeTimerRule(action, period, priority));
	}

	public static IDisposable Subscribe<T>(Action<T, int> action, T param, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
	{
		return GenericThreadPool!.Subscribe(new LegacyNativeTimerRule<T>(action, timeOut, priority), param);
	}

	public static IDisposable Subscribe<T>(LegacyNativeTimerRule<T> rule, T param, LegacyNativeStandalonePool pool)
	{
		return pool.Subscribe(rule, param);
	}

	public static IDisposable Subscribe<T>(LegacyNativeTimerRule<T> rule, T param, LegacyNativeStaticPool pool)
	{
		return pool.Subscribe(rule, param);
	}

	public static IDisposable Subscribe<T>(LegacyNativeTimerRule<T> rule, T param)
	{
		return GenericThreadPool!.Subscribe(rule, param);
	}
}
