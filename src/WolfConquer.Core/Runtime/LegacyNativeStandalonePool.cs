using System;
using System.Threading;
using WolfConquer.Core.World;
using System.Collections.Generic;

namespace WolfConquer.Core.Runtime;

internal class LegacyNativeStandalonePool : IDisposable
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

	public LegacyNativeStandalonePool(int minimumPoolSize = 6, int maximumPoolSize = 32)
	{
		LegacyNativeTime32.NativeNowPrelude.smethod_0();
		bool_1 = false;
		object_1 = new object();
		object_0 = new object();
		dictionary_0 = new Dictionary<int, LegacyNativeTimerSubscriptionBase>();
		queue_0 = new Queue<LegacyNativeTimerSubscriptionBase>();
		list_0 = new List<Thread>();
		int_3 = minimumPoolSize;
		int_4 = maximumPoolSize;
	}

	~LegacyNativeStandalonePool()
	{
		method_2(bool_2: false);
	}

	void IDisposable.Dispose()
	{
		method_2(bool_2: true);
	}

	internal void method_0()
	{
		if (!bool_1)
		{
			Interlocked.Increment(ref int_0);
			Interlocked.Increment(ref int_1);
			Thread thread = new Thread(method_3);
			list_0!.Add(thread);
			thread.Priority = ThreadPriority.Normal;
			thread.IsBackground = false;
			thread.Start();
		}
	}

	internal void method_1()
	{
		if (bool_1)
		{
			return;
		}
		foreach (Thread item in list_0!)
		{
			if (!item.IsBackground)
			{
				item.IsBackground = true;
				Interlocked.Decrement(ref int_0);
				list_0!.Remove(item);
				break;
			}
		}
	}

	internal void method_2(bool bool_2)
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

	internal void method_3()
	{
		Thread currentThread = Thread.CurrentThread;
		while (bool_0 && !currentThread.IsBackground)
		{
			Thread.Sleep(1);
			if (!method_4(out var class2_))
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
				method_5(class2_!.GetHashCode());
			}
		}
		Interlocked.Decrement(ref int_1);
	}

	internal bool method_4(out LegacyNativeTimerSubscriptionBase? class2_0)
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

	internal void method_5(int int_5)
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

	public LegacyNativeStandalonePool Run()
	{
		bool_0 = true;
		for (int i = 0; i < int_3; i++)
		{
			method_0();
		}
		propagationThread = new Thread(method_7);
		propagationThread.Start();
		return this;
	}

	internal void method_6()
	{
		int num = int_2;
		int num2 = int_0;
		if ((num == num2 || queue_0!.Count / 10 >= num2) && num2 < int_4)
		{
			method_0();
		}
		if (num <= num2 / 4 && num2 > int_3)
		{
			method_1();
		}
	}

	private void method_7()
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
			method_6();
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
