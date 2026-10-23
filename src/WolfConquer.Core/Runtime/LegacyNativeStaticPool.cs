using System;
using System.Threading;
using WolfConquer.Core.World;
using System.Collections.Generic;
using System.Security;

namespace WolfConquer.Core.Runtime;

internal class LegacyNativeStaticPool : IDisposable
{
	public const int SleepTime = 1;

	internal object object_0;

	internal object object_1;

	internal Dictionary<int, LegacyNativeTimerSubscriptionBase>? dictionary_0;

	internal Queue<LegacyNativeTimerSubscriptionBase>? queue_0;

	internal List<Thread>? list_0;

	protected internal Thread? propagationThread;

	internal volatile bool bool_0;

	internal volatile bool bool_1;

	internal int int_0;

	internal int int_1;

	internal int int_2;

	internal int int_3;

	internal int int_4;

	public int Threads => int_0;

	public int IdleThreads => int_1;

	public int InUseThreads => int_2;

	public int Treshold => queue_0!.Count;

	public LegacyNativeStaticPool(int maximumPoolSize = 32)
	{
		LegacyNativeTime32.NativeNowPrelude.smethod_0();
		bool_1 = false;
		object_1 = new object();
		object_0 = new object();
		dictionary_0 = new Dictionary<int, LegacyNativeTimerSubscriptionBase>();
		queue_0 = new Queue<LegacyNativeTimerSubscriptionBase>();
		list_0 = new List<Thread>();
		int_3 = maximumPoolSize;
		int_4 = maximumPoolSize;
	}

	~LegacyNativeStaticPool()
	{
		method_1(bool_2: false);
	}

	void IDisposable.Dispose()
	{
		method_1(bool_2: true);
	}

	[SecuritySafeCritical]
	internal void method_0()
	{
		if (!bool_1)
		{
			Interlocked.Increment(ref int_0);
			Interlocked.Increment(ref int_1);
			Thread thread = new Thread(method_2, 1048576);
			list_0!.Add(thread);
			thread.Priority = ThreadPriority.Normal;
			thread.Start();
		}
	}

	internal void method_1(bool bool_2)
	{
		if (bool_1)
		{
			return;
		}
		bool_1 = true;
		bool_0 = false;
		if (bool_2)
		{
			foreach (Thread item in list_0!)
			{
				#pragma warning disable SYSLIB0006 // Original Abort call; .NET runtime limitation is not repaired here.
				item.Abort();
#pragma warning restore SYSLIB0006
			}
		}
		dictionary_0!.Clear();
		dictionary_0 = null;
		queue_0 = null;
		list_0 = null;
	}

	internal void method_2()
	{
		Thread currentThread = Thread.CurrentThread;
		while (bool_0)
		{
			Thread.Sleep(1);
			if (!method_3(out var class2_))
			{
				continue;
			}
			if (class2_!.bool_0)
			{
				Interlocked.Decrement(ref int_1);
				Interlocked.Increment(ref int_2);
				currentThread.Priority = class2_!.vmethod_3();
				try
				{
					class2_!.vmethod_0();
				}
				catch (Exception value)
				{
					Console.WriteLine(value);
				}
				finally
				{
					class2_!.bool_1 = false;
				}
				currentThread.Priority = ThreadPriority.Normal;
				Interlocked.Decrement(ref int_2);
				Interlocked.Increment(ref int_1);
			}
			else
			{
				method_4(class2_!.GetHashCode());
			}
		}
		Interlocked.Decrement(ref int_1);
	}

	internal bool method_3(out LegacyNativeTimerSubscriptionBase? class2_0)
	{
		class2_0 = null;
		lock (object_0)
		{
			if (queue_0!.Count != 0)
			{
				LegacyNativeTimerSubscriptionBase @class = queue_0!.Dequeue();
				class2_0 = @class;
			}
		}
		return class2_0 != null;
	}

	internal void method_4(int int_5)
	{
		lock (object_1)
		{
			dictionary_0!.Remove(int_5);
		}
	}

	public IDisposable Subscribe(LegacyNativeTimerRule instruction)
	{
		LegacyNativeTimerSubscriptionBase? @class = null;
		lock (object_1)
		{
			@class = new LegacyNativeTimerSubscription(instruction);
			if (instruction is LegacyNativeLazyDelegate)
			{
				@class.method_1(instruction.int_0);
			}
			dictionary_0![@class.GetHashCode()] = @class;
		}
		return @class;
	}

	public IDisposable Subscribe<T>(LegacyNativeTimerRule<T> instruction, T param)
	{
		LegacyNativeTimerSubscriptionBase? @class = null;
		lock (object_1)
		{
			@class = new LegacyNativeTimerSubscription<T>(instruction, param);
			if (instruction is LegacyNativeLazyDelegate<T>)
			{
				@class.method_1(instruction.int_0);
			}
			dictionary_0![@class.GetHashCode()] = @class;
		}
		return @class;
	}

	public LegacyNativeStaticPool Run()
	{
		bool_0 = true;
		for (int i = 0; i < int_3; i++)
		{
			method_0();
		}
		propagationThread = new Thread(method_5);
		propagationThread.Start();
		return this;
	}

	private void method_5()
	{
		while (bool_0)
		{
			Queue<LegacyNativeTimerSubscriptionBase> queue = new Queue<LegacyNativeTimerSubscriptionBase>();
			Queue<int> queue2 = new Queue<int>();
			lock (object_1)
			{
				foreach (LegacyNativeTimerSubscriptionBase value in dictionary_0!.Values)
				{
					if (value.bool_0)
					{
						if (!value.bool_1 && value.method_0())
						{
							value.bool_1 = true;
							queue.Enqueue(value);
						}
					}
					else
					{
						queue2.Enqueue(value.GetHashCode());
					}
				}
				while (queue2.Count != 0)
				{
					dictionary_0!.Remove(queue2.Dequeue());
				}
			}
			if (queue.Count != 0)
			{
				lock (object_0)
				{
					while (queue.Count != 0)
					{
						queue_0!.Enqueue(queue.Dequeue());
					}
				}
			}
			Thread.Sleep(1);
		}
	}

	public void Clear()
	{
		lock (object_0)
		{
			queue_0!.Clear();
		}
	}

	public override string ToString()
	{
		return $"{queue_0!.Count} waiting exec, {dictionary_0!.Count} subscriptions, {int_0} threads: {int_2} in use, {int_1} idle";
	}
}
