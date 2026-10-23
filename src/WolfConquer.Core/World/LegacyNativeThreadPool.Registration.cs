using System;
using System.Threading;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeThreadPool
{
	public static bool Register(LegacyNativeGameClientRuntime client)
	{
		if (client.TimerSubscriptions == null)
		{
			client.TimerSubscriptions = new IDisposable[5]
			{
				Subscribe(Characters!, client),
				Subscribe(AutoAttack!, client),
				Subscribe(Buffers!, client),
				Subscribe(Monsters!, client),
				Subscribe(Floor!, client)
			};
			return true;
		}
		return false;
	}

	public static void Unregister(LegacyNativeGameClientRuntime client)
	{
		lock (client.TimerSyncRoot!)
		{
			if (client.TimerSubscriptions != null)
			{
				IDisposable[] timerSubscriptions = client.TimerSubscriptions;
				foreach (IDisposable disposable in timerSubscriptions)
				{
					disposable.Dispose();
				}
				client.TimerSubscriptions = null;
			}
		}
	}
}
