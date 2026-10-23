using System;
using System.Reflection;
using System.Threading;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Runtime;

internal abstract class LegacyNativeTimerSubscriptionBase : IDisposable
{
	internal static volatile int int_0 = int.MinValue;

	internal bool bool_0;

	internal bool bool_1;

	internal LegacyNativeTime32 time32_0;

	protected int int_1;

	public LegacyNativeTimerSubscriptionBase()
	{
		int_0++;
		int_1 = int_0;
		bool_0 = true;
		bool_1 = false;
		method_1(0);
	}

	internal abstract void vmethod_0();

	~LegacyNativeTimerSubscriptionBase()
	{
		((IDisposable)this).Dispose();
	}

	internal bool method_0()
	{
		return LegacyNativeTime32.Now > time32_0;
	}

	internal void method_1(int int_2)
	{
		time32_0 = LegacyNativeTime32.Now.AddMilliseconds(int_2);
	}

	void IDisposable.Dispose()
	{
		bool_0 = false;
		vmethod_1();
	}

	internal abstract void vmethod_1();

	internal abstract MethodInfo vmethod_2();

	internal abstract ThreadPriority vmethod_3();

	public override int GetHashCode()
	{
		return int_1;
	}
}



internal sealed class LegacyNativeTimerSubscription<T> : LegacyNativeTimerSubscriptionBase
{
	private LegacyNativeTimerRule<T>? timerRule_0;

	private T? gparam_0;

	public LegacyNativeTimerSubscription(LegacyNativeTimerRule<T> timerRule_1, T gparam_1)
	{
		timerRule_0 = timerRule_1;
		gparam_0 = gparam_1;
	}

	internal override void vmethod_0()
	{
		if (timerRule_0 == null)
		{
			return;
		}
		timerRule_0.action_0!(gparam_0!, LegacyNativeTime32.Now.Value);
		if (timerRule_0 != null)
		{
			if (!timerRule_0.bool_0)
			{
				((IDisposable)this).Dispose();
			}
			else
			{
				method_1(timerRule_0.int_0);
			}
		}
	}

	internal override void vmethod_1()
	{
		timerRule_0 = null;
		gparam_0 = default(T);
	}

	internal override MethodInfo vmethod_2()
	{
		return timerRule_0!.action_0!.Method;
	}

	internal override ThreadPriority vmethod_3()
	{
		return timerRule_0!.threadPriority_0;
	}
}



internal sealed class LegacyNativeTimerSubscription : LegacyNativeTimerSubscriptionBase
{
	private LegacyNativeTimerRule? timerRule_0;

	public LegacyNativeTimerSubscription(LegacyNativeTimerRule timerRule_1)
	{
		timerRule_0 = timerRule_1;
	}

	internal override void vmethod_0()
	{
		if (timerRule_0 == null)
		{
			return;
		}
		timerRule_0.action_0!(LegacyNativeTime32.Now.Value);
		if (timerRule_0 != null)
		{
			if (!timerRule_0.bool_0)
			{
				((IDisposable)this).Dispose();
			}
			else
			{
				method_1(timerRule_0.int_0);
			}
		}
	}

	internal override void vmethod_1()
	{
		timerRule_0 = null;
	}

	internal override MethodInfo vmethod_2()
	{
		return timerRule_0!.action_0!.Method;
	}

	internal override ThreadPriority vmethod_3()
	{
		return timerRule_0!.threadPriority_0;
	}
}
